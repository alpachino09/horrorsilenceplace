using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Silence.Multiplayer.Editor.Visual
{
    internal static class TerrainBuilder
    {
        public static Terrain Build(Transform parent,string output,VisualAssetLibrary art)
        {
            var source=AssetDatabase.LoadAssetAtPath<TextAsset>(VisualAssetLibrary.Art+"/Textures/FarmTerrain.bytes");
            if(source==null)throw new InvalidOperationException("FarmTerrain.bytes was not imported.");
            using var reader=new BinaryReader(new MemoryStream(source.bytes));
            if(new string(reader.ReadChars(4))!="SVR2")throw new InvalidDataException("Unknown terrain version.");
            int n=reader.ReadInt32();reader.ReadInt32();if(n!=129)throw new InvalidDataException("Expected 129×129 terrain.");
            var heights=new float[n,n];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {float value=reader.ReadSingle();if(float.IsNaN(value)||value<0||value>1)throw new InvalidDataException("Invalid height.");heights[z,x]=value;}
            var weights=new float[n,n,4];for(int z=0;z<n;z++)for(int x=0;x<n;x++)for(int layer=0;layer<4;layer++)weights[z,x,layer]=reader.ReadSingle();
            var data=new TerrainData{heightmapResolution=n,size=new Vector3(128,10,128),alphamapResolution=128,baseMapResolution=1024};
            data.SetHeights(0,0,heights);
            var layers=new TerrainLayer[4];string[] names={"Forest","Grass","Mud","Gravel"};
            for(int i=0;i<4;i++)
            {
                layers[i]=new TerrainLayer{diffuseTexture=art.Texture(names[i]+"_BaseColor"),normalMapTexture=art.Texture(names[i]+"_Normal"),
                    maskMapTexture=art.Texture(names[i]+"_Mask"),tileSize=new Vector2(4,4),normalScale=.5f};
                AssetDatabase.CreateAsset(layers[i],output+"/"+names[i]+".terrainlayer");
            }
            data.terrainLayers=layers;var alpha=new float[128,128,4];
            for(int z=0;z<128;z++)for(int x=0;x<128;x++)for(int i=0;i<4;i++)alpha[z,x,i]=weights[Mathf.RoundToInt(z*128f/127),Mathf.RoundToInt(x*128f/127),i];
            data.SetAlphamaps(0,0,alpha);
            data.SetDetailResolution(256,16);
            data.detailPrototypes=new[]{new DetailPrototype{prototypeTexture=art.Texture("GrassAtlas"),renderMode=DetailRenderMode.GrassBillboard,
                minWidth=.35f,maxWidth=.65f,minHeight=.25f,maxHeight=.6f,healthyColor=Color.white,dryColor=new Color(.85f,.85f,.7f),noiseSpread=.35f}};
            var density=new int[256,256];
            for(int z=0;z<256;z++)for(int x=0;x<256;x++)
            {
                float wx=x*.5f-64,wz=z*.5f-64;
                bool yard=Mathf.Abs(wx)<21&&Mathf.Abs(wz)<21;
                bool house=Mathf.Abs(wx+10)<5&&Mathf.Abs(wz)<5, barn=Mathf.Abs(wx-9)<5&&Mathf.Abs(wz-6)<7;
                int ix=Mathf.Clamp(x/2,0,127),iz=Mathf.Clamp(z/2,0,127);
                if(!house&&!barn&&alpha[iz,ix,3]<.15f&&alpha[iz,ix,2]<.3f)density[z,x]=yard?1:3;
            }
            data.SetDetailLayer(0,0,0,density);AssetDatabase.CreateAsset(data,output+"/FarmTerrain.asset");
            var go=Terrain.CreateTerrainGameObject(data);go.name="Farm sculpted terrain";go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(-64,-3,-64);
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI);
            var terrain=go.GetComponent<Terrain>();terrain.drawInstanced=true;terrain.heightmapPixelError=4;terrain.basemapDistance=65;
            terrain.detailObjectDistance=32;terrain.treeDistance=120;terrain.detailObjectDensity=.55f;
            var mat=new Material(VisualAssetLibrary.RequireShader("Universal Render Pipeline/Terrain/Lit")){name="SILENCE terrain PBR"};
            mat.EnableKeyword("_TERRAIN_BLEND_HEIGHT");mat.SetFloat("_EnableHeightBlend",1);mat.SetFloat("_HeightTransition",.12f);
            AssetDatabase.CreateAsset(mat,output+"/Materials/Terrain.mat");terrain.materialTemplate=mat;terrain.Flush();
            // Preserve audible ground categories when rays hit the new TerrainCollider.
            go.AddComponent<Silence.SoundSimulation.SurfaceMaterial>().kind=Silence.Core.SurfaceKind.Grass;
            return terrain;
        }
    }
}
