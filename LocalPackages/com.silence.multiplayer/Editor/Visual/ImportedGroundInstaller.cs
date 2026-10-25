using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Silence.Core;
using Object=UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class ImportedGroundInstaller
    {
        [Serializable] public sealed class Report
        {
            public string source=ImportedAssetCatalog.TerrainUrl;
            public string generatedFolder;
            public int terrains,terrainLayers,meshSlots;
            public bool originalAssetsModified,heightsOrPaintingModified;
            public List<string> bindings=new List<string>();
            public List<string> colorPaths=new List<string>();
        }
        private sealed class Built { public Material material;public Texture2D color,normal,mask;public ImportedTextureSet source; }
        public static Report Apply(Scene scene,ImportedTextureSet[] choices,float tileMetres,string folder)
        {
            if(choices.Length!=4 || !choices.Any(s=>s!=null && s.color!=null) || !(tileMetres>=.1f && tileMetres<=32))throw new InvalidOperationException("Выбери хотя бы одну текстуру земли и размер повтора 0.1–32 м.");
            VisualAssetLibrary.Folder(folder+"/Materials");VisualAssetLibrary.Folder(folder+"/Textures");
            var built=new Dictionary<ImportedGround,Built>();var report=new Report{generatedFolder=folder};
            string readFolder=folder+"/__ReadMaps";
            try
            {
                for(int i=0;i<choices.Length;i++)
                {
                    var set=choices[i];if(set==null || set.color==null)continue;
                    var normal=Normal(set.normal,folder+"/Textures/"+i+"_Normal");
                    var mask=Pack(set,readFolder+"/"+i,folder+"/Textures/"+i+"_Mask.png");
                    var mat=new Material(VisualAssetLibrary.RequireShader("Universal Render Pipeline/Lit")){name="Imported_"+(ImportedGround)i,enableInstancing=true};
                    mat.SetTexture("_BaseMap",set.color);mat.SetColor("_BaseColor",new Color(set.tint.r,set.tint.g,set.tint.b,1));
                    mat.SetFloat("_Metallic",mask!=null?1:0);mat.SetFloat("_Smoothness",mask!=null?1:set.defaultSmoothness);
                    if(normal!=null){mat.SetTexture("_BumpMap",normal);mat.SetFloat("_BumpScale",.65f);mat.EnableKeyword("_NORMALMAP");}
                    if(mask!=null){mat.SetTexture("_MetallicGlossMap",mask);mat.SetTexture("_OcclusionMap",mask);mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.EnableKeyword("_OCCLUSIONMAP");}
                    CharacterSurfaceFixer.MakeOpaque(mat);AssetDatabase.CreateAsset(mat,folder+"/Materials/"+i+"_Ground.mat");
                    built.Add((ImportedGround)i,new Built{material=mat,color=set.color,normal=normal,mask=mask,source=set});
                    report.colorPaths.Add(AssetDatabase.GetAssetPath(set.color));
                    report.bindings.Add((ImportedGround)i+": "+AssetDatabase.GetAssetPath(set.color)+" | normal="+AssetDatabase.GetAssetPath(set.normal)+" | roughness="+AssetDatabase.GetAssetPath(set.roughness));
                }
                var dataCopies=new Dictionary<TerrainData,TerrainData>();
                foreach(var terrain in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true)))
                {
                    var source=terrain.terrainData;if(source==null)continue;
                    if(!source.terrainLayers.Any(l=>l!=null && ImportedAssetModel.Ground(l.name).HasValue && built.ContainsKey(ImportedAssetModel.Ground(l.name).Value)))continue;
                    if(!dataCopies.TryGetValue(source,out var data))
                    {
                        data=Object.Instantiate(source);data.name=source.name+"_Imported";AssetDatabase.CreateAsset(data,folder+"/Terrain_"+dataCopies.Count+".asset");
                        var layers=data.terrainLayers;
                        for(int i=0;i<layers.Length;i++)
                        {
                            if(layers[i]==null)continue;var kind=ImportedAssetModel.Ground(layers[i].name);
                            if(!kind.HasValue || !built.TryGetValue(kind.Value,out var b))continue;
                            var layer=Object.Instantiate(layers[i]);layer.name=layers[i].name+"_Imported";
                            layer.diffuseTexture=b.color;layer.normalMapTexture=b.normal;layer.maskMapTexture=b.mask;layer.tileSize=Vector2.one*tileMetres;
                            layer.normalScale=.65f;layer.metallic=0;layer.smoothness=b.source.defaultSmoothness;
                            layer.diffuseRemapMin=Vector4.zero;layer.diffuseRemapMax=new Vector4(b.source.tint.r,b.source.tint.g,b.source.tint.b,1);
                            layer.maskMapRemapMin=Vector4.zero;layer.maskMapRemapMax=Vector4.one;
                            AssetDatabase.CreateAsset(layer,folder+"/Terrain_"+dataCopies.Count+"_Layer_"+i+".terrainlayer");layers[i]=layer;report.terrainLayers++;
                        }
                        data.terrainLayers=layers;EditorUtility.SetDirty(data);dataCopies.Add(source,data);
                    }
                    terrain.terrainData=data;var collider=terrain.GetComponent<TerrainCollider>();if(collider!=null){collider.terrainData=data;EditorUtility.SetDirty(collider);PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
                    var terrainMaterial=terrain.materialTemplate;
                    var material=terrainMaterial!=null && terrainMaterial.shader==VisualAssetLibrary.RequireShader("Universal Render Pipeline/Terrain/Lit")?Object.Instantiate(terrainMaterial):new Material(VisualAssetLibrary.RequireShader("Universal Render Pipeline/Terrain/Lit"));
                    material.name="Imported Terrain "+report.terrains;AssetDatabase.CreateAsset(material,folder+"/Materials/Terrain_"+report.terrains+".mat");terrain.materialTemplate=material;
                    EditorUtility.SetDirty(terrain);PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);terrain.Flush();report.terrains++;
                }
                var meshMaterials=new Dictionary<string,Material>();
                foreach(var renderer in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))
                {
                    // Only ground-like, mostly horizontal meshes; never skin,
                    // foliage alpha cards, walls or unrelated material slots.
                    var size=renderer.bounds.size;if(size.y>Mathf.Max(size.x,size.z)*.25f)continue;
                    var slots=renderer.sharedMaterials;bool changed=false;
                    for(int i=0;i<slots.Length;i++)
                    {
                        var original=slots[i];if(original==null || original.HasProperty("_AlphaClip") && original.GetFloat("_AlphaClip")>.5f)continue;
                        var kind=ImportedAssetModel.Ground(original.name);if(!kind.HasValue || !built.TryGetValue(kind.Value,out var b))continue;
                        Vector2 scale=Tiling(renderer,tileMetres,original);string key=(int)kind.Value+"_"+scale.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+scale.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
                        if(!meshMaterials.TryGetValue(key,out var mat))
                        {
                            mat=Object.Instantiate(b.material);mat.name=b.material.name+"_Mesh";mat.SetTextureScale("_BaseMap",scale);
                            AssetDatabase.CreateAsset(mat,folder+"/Materials/Mesh_"+meshMaterials.Count+".mat");meshMaterials.Add(key,mat);
                        }
                        slots[i]=mat;changed=true;report.meshSlots++;
                    }
                    if(changed){renderer.sharedMaterials=slots;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
                }
                if(report.terrainLayers+report.meshSlots==0)throw new InvalidOperationException("В этом мире не найдены слои/материалы выбранных категорий земли. Выбери подходящие Grass, Soil, Forest или Gravel в окне 28. Исходный мир сохранён.");
                return report;
            }
            finally{if(AssetDatabase.IsValidFolder(readFolder))AssetDatabase.DeleteAsset(readFolder);}
        }
        private static Vector2 Tiling(MeshRenderer renderer,float metres,Material original)
        {
            var filter=renderer.GetComponent<MeshFilter>();var mesh=filter!=null?filter.sharedMesh:null;
            if(mesh!=null && mesh.isReadable)
            {
                var uv=mesh.uv;
                if(uv.Length>0)
                {
                    Vector2 lo=uv[0],hi=uv[0];foreach(var p in uv){lo=Vector2.Min(lo,p);hi=Vector2.Max(hi,p);}
                    var span=hi-lo;var size=renderer.bounds.size;
                    if(span.x>.001f && span.y>.001f && size.x>.01f && size.z>.01f)return new Vector2(size.x/metres/span.x,size.z/metres/span.y);
                }
            }
            return original.HasProperty("_BaseMap")?original.GetTextureScale("_BaseMap"):original.HasProperty("_MainTex")?original.GetTextureScale("_MainTex"):Vector2.one;
        }
        private static Texture2D Normal(Texture2D source,string destination)
        {
            if(source==null)return null;string path=AssetDatabase.GetAssetPath(source);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer!=null && importer.textureType==TextureImporterType.NormalMap)return source;
            destination+=Path.GetExtension(path);if(!AssetDatabase.CopyAsset(path,destination))throw new IOException("Не скопирована normal map: "+path);
            importer=AssetImporter.GetAtPath(destination) as TextureImporter;if(importer==null)throw new InvalidOperationException("Нужна файловая normal texture: "+path);
            importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(destination);
        }
        private static Texture2D Pack(ImportedTextureSet set,string temporary,string destination)
        {
            if(!set.HasData)return null;VisualAssetLibrary.Folder(temporary);var cache=new Dictionary<Texture2D,Texture2D>();
            Texture2D Read(Texture2D source)
            {
                if(source==null)return null;if(cache.TryGetValue(source,out var ready))return ready;
                string path=AssetDatabase.GetAssetPath(source),copy=temporary+"/"+cache.Count+Path.GetExtension(path);
                if(!AssetDatabase.CopyAsset(path,copy))throw new IOException("Не скопирована data map: "+path);
                var importer=AssetImporter.GetAtPath(copy) as TextureImporter;if(importer==null)throw new InvalidOperationException("Нужна файловая data texture: "+path);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=false;importer.isReadable=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                ready=AssetDatabase.LoadAssetAtPath<Texture2D>(copy);cache.Add(source,ready);return ready;
            }
            var rough=Read(set.roughness);var smooth=Read(set.smoothness);var ao=Read(set.occlusion);var height=Read(set.height);var metal=Read(set.metallic);
            int width=Mathf.Min(1024,set.color.width),heightPixels=Mathf.Min(1024,set.color.height);var pixels=new Color[width*heightPixels];
            float Value(Texture2D map,float u,float v,int channel,float fallback)=>map!=null?map.GetPixelBilinear(u,v)[channel]:fallback;
            for(int y=0;y<heightPixels;y++)for(int x=0;x<width;x++)
            {
                float u=(x+.5f)/width,v=(y+.5f)/heightPixels;
                pixels[y*width+x]=new Color(Value(metal,u,v,0,0),Value(ao,u,v,set.occlusionChannel,1),Value(height,u,v,set.heightChannel,.5f),
                    rough!=null?ImportedAssetModel.Smoothness(Value(rough,u,v,0,.7f)):Value(smooth,u,v,set.smoothnessChannel,set.defaultSmoothness));
            }
            var texture=new Texture2D(width,heightPixels,TextureFormat.RGBA32,false,true);
            try{texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(destination,texture.EncodeToPNG());}
            finally{Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(destination);var output=AssetImporter.GetAtPath(destination) as TextureImporter;
            output.sRGBTexture=false;output.mipmapEnabled=true;output.alphaSource=TextureImporterAlphaSource.FromInput;output.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(destination);
        }
    }
}
