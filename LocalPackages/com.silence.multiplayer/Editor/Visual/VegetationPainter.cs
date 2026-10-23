using System;
using UnityEngine;

namespace Silence.Multiplayer.Editor.Visual
{
    internal static class VegetationPainter
    {
        private static float Halton(int value,int radix)
        {float factor=1,result=0;while(value>0){factor/=radix;result+=factor*(value%radix);value/=radix;}return result;}
        public static void Paint(Transform parent,Terrain terrain,VisualAssetLibrary art,int seed)
        {
            var random=new System.Random(seed);
            // A fixed forest edge, with gaps at the road and no scattering through the playable rooms.
            int placed=0;
            for(int i=1;i<1000&&placed<52;i++)
            {
                float x=(Halton(i,2)-.5f)*102,z=(Halton(i,3)-.5f)*102;
                float radius=Mathf.Sqrt(x*x+z*z);
                if(radius<23||radius>53||Mathf.Abs(x)<5&&Mathf.Abs(z)<42)continue;
                float clustering=.55f+.45f*Mathf.Sin(x*.18f+Mathf.Cos(z*.13f));
                if(random.NextDouble()>clustering)continue;
                float y=terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y;
                int variant=new[]{31,47,83}[placed%3];float scale=.75f+(float)random.NextDouble()*.5f;
                var tree=art.LodModel("Pine_"+variant,parent,new Vector3(x,y,z),scale);
                tree.transform.localRotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);placed++;
            }
            for(int i=1;i<160;i++)
            {
                float x=(Halton(i+37,5)-.5f)*84,z=(Halton(i+37,7)-.5f)*84;
                if(Mathf.Abs(x)<21&&Mathf.Abs(z)<21)continue;
                float y=terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y;
                string asset=i%4==0?"Rock_"+new[]{5,12,29}[i%3]:"Fern";
                var item=art.Place(asset,parent,new Vector3(x,y,z),Quaternion.Euler(0,(float)random.NextDouble()*360,0));
                item.transform.localScale=Vector3.one*(.45f+(float)random.NextDouble()*.7f);
            }
        }
    }
}
