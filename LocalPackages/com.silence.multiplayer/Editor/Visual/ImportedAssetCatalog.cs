using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Silence.Core;

namespace Silence.Multiplayer.Editor.Visual
{
    [Serializable] public sealed class ImportedTextureSet
    {
        public string name;
        public Texture2D color,normal,roughness,smoothness,occlusion,height,metallic;
        public int smoothnessChannel,occlusionChannel,heightChannel;
        public Color tint=Color.white;
        public float defaultSmoothness=.3f;
        public bool HasData=>roughness!=null || smoothness!=null || occlusion!=null || height!=null || metallic!=null;
    }
    public static class ImportedAssetCatalog
    {
        public const string TerrainUrl="https://assetstore.unity.com/packages/2d/textures-materials/floors/realistic-terrain-textures-free-279940";
        public const string MotionUrl="https://assetstore.unity.com/packages/3d/animations/human-basic-motions-free-154271";
        public static string FindFolder(string preferred,string words)
        {
            if(AssetDatabase.IsValidFolder("Assets/"+preferred))return "Assets/"+preferred;
            return AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal) && AssetDatabase.IsValidFolder(p))
                .Where(p=>ImportedAssetModel.Words(p).Contains(words)).OrderBy(p=>p.Length).ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
        }
        public static void RequireFolder(string root)
        {if(string.IsNullOrEmpty(root) || !root.StartsWith("Assets/",StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(root))throw new InvalidOperationException("Выбери импортированную папку внутри Assets.");}
        public static AnimationClip[] Motions(string root)
        {
            RequireFolder(root);
            return AssetDatabase.FindAssets("t:AnimationClip",new[]{root}).Concat(AssetDatabase.FindAssets("t:Model",new[]{root})).Distinct()
                .Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
                .Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal) && !c.legacy && c.isHumanMotion && c.length>.05f)
                .Distinct().OrderBy(c=>AssetDatabase.GetAssetPath(c),StringComparer.Ordinal).ThenBy(c=>c.name,StringComparer.Ordinal).ToArray();
        }
        public static AnimationClip Suggest(AnimationClip[] clips,ImportedMotion role)
        {
            var ranked=clips.Select(c=>new{clip=c,score=ImportedAssetModel.MotionScore(ClassificationName(c),AssetDatabase.GetAssetPath(c),role)})
                .Where(x=>x.score>=0).OrderByDescending(x=>x.score).ToArray();
            // Multiple variants are exposed in the workshop; never silently pick
            // one of equally ranked recordings as if its identity were known.
            return ranked.Length==0 || ranked.Length>1 && ranked[0].score==ranked[1].score?null:ranked[0].clip;
        }
        private static string ClassificationName(AnimationClip clip)=>ImportedAssetModel.Motion(clip.name).HasValue?clip.name:Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(clip));
        public static ImportedMotion? Role(AnimationClip clip)=>ImportedAssetModel.Motion(ClassificationName(clip));
        public static ImportedTextureSet[] Textures(string root)
        {
            RequireFolder(root);var result=new List<ImportedTextureSet>();
            Texture2D Tex(Material m,string property)=>m.HasProperty(property)?m.GetTexture(property) as Texture2D:null;
            foreach(var mat in AssetDatabase.FindAssets("t:Material",new[]{root}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>))
            {
                if(mat==null)continue;var color=Tex(mat,"_BaseMap") ?? Tex(mat,"_MainTex");if(color==null)continue;
                var combined=Tex(mat,"_MetallicGlossMap");
                result.Add(new ImportedTextureSet{name=mat.name,color=color,normal=Tex(mat,"_BumpMap"),metallic=combined,smoothness=combined,smoothnessChannel=3,
                    occlusion=Tex(mat,"_OcclusionMap"),occlusionChannel=1,height=Tex(mat,"_ParallaxMap"),heightChannel=1,
                    tint=mat.HasProperty("_BaseColor")?mat.GetColor("_BaseColor"):mat.HasProperty("_Color")?mat.GetColor("_Color"):Color.white,
                    defaultSmoothness=mat.HasProperty("_Smoothness")?mat.GetFloat("_Smoothness"):mat.HasProperty("_Glossiness")?mat.GetFloat("_Glossiness"):.3f});
            }
            var textures=AssetDatabase.FindAssets("t:Texture2D",new[]{root}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Where(t=>t!=null).ToArray();
            foreach(var color in textures)
            {
                string path=AssetDatabase.GetAssetPath(color);var kind=ImportedAssetModel.Map(color.name);
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                string words=ImportedAssetModel.Words(color.name);
                bool packed=words.Contains(" mask ") || words.Contains(" orm ") || words.Contains(" arm ") || words.Contains(" rma ") || words.Contains(" specular ");
                bool plain=kind==ImportedMap.Unknown && !packed && ImportedAssetModel.Ground(color.name).HasValue && importer!=null && importer.sRGBTexture && importer.textureType==TextureImporterType.Default;
                if(kind!=ImportedMap.Color && !plain || result.Any(s=>s.color==color))continue;
                result.Add(new ImportedTextureSet{name=color.name,color=color});
            }
            // Fill loose companion maps for material-backed sets as well. A
            // vendor material may reference albedo while its roughness is loose.
            foreach(var set in result)
            {
                string folder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(set.color)),key=ImportedAssetModel.TextureKey(set.color.name);
                var companions=textures.Where(t=>Path.GetDirectoryName(AssetDatabase.GetAssetPath(t))==folder && ImportedAssetModel.TextureKey(t.name)==key)
                    .OrderByDescending(t=>ImportedAssetModel.Words(t.name).Contains(" gl ") || ImportedAssetModel.Words(t.name).Contains(" opengl "));
                foreach(var map in companions)
                {
                    switch(ImportedAssetModel.Map(map.name))
                    {
                        case ImportedMap.Normal:
                            if(set.normal==null)set.normal=map;break;
                        case ImportedMap.Roughness:if(set.roughness==null)set.roughness=map;break;
                        case ImportedMap.Smoothness:if(set.smoothness==null)set.smoothness=map;break;
                        case ImportedMap.Occlusion:if(set.occlusion==null){set.occlusion=map;set.occlusionChannel=0;}break;
                        case ImportedMap.Height:if(set.height==null){set.height=map;set.heightChannel=0;}break;
                        case ImportedMap.Metallic:if(set.metallic==null)set.metallic=map;break;
                    }
                }
            }
            return result.OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
        }
    }
}
