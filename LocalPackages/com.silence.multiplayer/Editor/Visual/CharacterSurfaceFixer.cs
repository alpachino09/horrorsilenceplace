using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class CharacterSurfaceFixer
    {
        public static bool IsSolid(Renderer renderer, Material material)
        {
            if (material == null) return false;
            string n=(renderer.name+" "+material.name).ToLowerInvariant();
            foreach(string transparent in new[]{"glass","cornea","lens","hair_cards","eyelash","lashes"}) if(n.Contains(transparent)) return false;
            foreach(string solid in new[]{"body_clothed","face","skin","fabric","leather","footwear","sole","belt","teeth","tongue","mouth","metal","brows","hair_dark"}) if(n.Contains(solid)) return true;
            return false;
        }
        public static void MakeOpaque(Material material)
        {
            // Property names verified against URP Lit/BaseShaderGUI. Unknown custom
            // pipelines fail explicitly rather than silently replacing their shader.
            if(material.shader==null || !material.shader.name.StartsWith("Universal Render Pipeline/",StringComparison.Ordinal) || !material.HasProperty("_Surface"))
                throw new InvalidOperationException("Нужен поддержанный URP материал: "+material.name);
            void Set(string name,float value) { if(material.HasProperty(name))material.SetFloat(name,value); }
            Set("_Surface",0);Set("_AlphaClip",0);Set("_ZWrite",1);Set("_ZWriteControl",0);Set("_AlphaToMask",0);
            Set("_SrcBlend",(float)BlendMode.One);Set("_DstBlend",(float)BlendMode.Zero);
            Set("_SrcBlendAlpha",(float)BlendMode.One);Set("_DstBlendAlpha",(float)BlendMode.Zero);
            Set("_QueueOffset",0);Set("_Cull",(float)CullMode.Back);Set("_ZTest",(float)CompareFunction.LessEqual);
            foreach(string keyword in new[]{"_SURFACE_TYPE_TRANSPARENT","_ALPHATEST_ON","_ALPHAPREMULTIPLY_ON","_ALPHAMODULATE_ON","_ALPHABLEND_ON"})material.DisableKeyword(keyword);
            if(material.HasProperty("_BaseColor")){Color color=material.GetColor("_BaseColor");color.a=1;material.SetColor("_BaseColor",color);}
            material.SetOverrideTag("RenderType","Opaque");material.renderQueue=(int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("DepthOnly",true);material.SetShaderPassEnabled("ShadowCaster",true);
        }
        public static int Prepare(Transform visual,string folder,List<string> preserved)
        {
            VisualAssetLibrary.Folder(folder+"/Materials");var copies=new Dictionary<Material,Material>();
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    var source=slots[i];if(source==null)throw new InvalidOperationException("Отсутствует материал: "+renderer.name);
                    if(!IsSolid(renderer,source)){preserved.Add(renderer.name+" / "+source.name);continue;}
                    if(!copies.TryGetValue(source,out var copy))
                    {
                        copy=new Material(source){name=source.name+"_Opaque"};MakeOpaque(copy);
                        AssetDatabase.CreateAsset(copy,folder+"/Materials/Solid_"+copies.Count+".mat");copies.Add(source,copy);
                    }
                    slots[i]=copy;
                }
                renderer.sharedMaterials=slots;
            }
            return copies.Count;
        }
    }
}
