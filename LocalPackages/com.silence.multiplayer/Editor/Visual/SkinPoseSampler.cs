using System;
using System.Collections.Generic;
using Silence.Core;
using UnityEngine;

namespace Silence.Multiplayer.Editor.Visual
{
    // Editor-only linear skinning of the validated four-weight mesh. Bounds
    // come directly from current bones and imported bind poses, not from a
    // BakeMesh output whose scale would have to be interpreted a second time.
    public sealed class SkinPoseSampler
    {
        private readonly SkinnedMeshRenderer renderer;
        private readonly RigVector[] vertices;
        private readonly SkinWeights4[] weights;
        private readonly SkinTransform[] bind,pose;
        private readonly Transform[] bones;
        private readonly List<Shape> shapes=new List<Shape>();
        private readonly RigVector[] shaped;
        public Vector3[] ActorVertices { get; }
        public Bounds ActorBounds { get; private set; }
        public Bounds LocalBounds { get; private set; }

        private sealed class Frame
        {
            public float weight;
            public Vector3[] delta;
        }
        private sealed class Shape
        {
            public int index;
            public Frame[] frames;
        }
        public SkinPoseSampler(SkinnedMeshRenderer renderer)
        {
            this.renderer=renderer;
            var mesh=renderer.sharedMesh;
            if(mesh==null || mesh.vertexCount==0)throw new InvalidOperationException("Пустой skin: "+renderer.name);
            bones=renderer.bones;var binds=mesh.bindposes;var sourceWeights=mesh.boneWeights;
            if(binds.Length!=bones.Length || bones.Length==0 || sourceWeights.Length!=mesh.vertexCount)
                throw new InvalidOperationException("Несовместимые bones/bind poses/weights: "+renderer.name);
            bind=new SkinTransform[bones.Length];pose=new SkinTransform[bones.Length];
            for(int i=0;i<bones.Length;i++)
            { if(bones[i]==null)throw new InvalidOperationException("Нет skin bone: "+renderer.name);bind[i]=Convert(binds[i]); }
            var source=mesh.vertices;vertices=new RigVector[source.Length];shaped=new RigVector[source.Length];
            ActorVertices=new Vector3[source.Length];weights=new SkinWeights4[source.Length];
            for(int i=0;i<source.Length;i++)
            {
                vertices[i]=Convert(source[i]);var w=sourceWeights[i];
                weights[i]=new SkinWeights4(w.boneIndex0,w.weight0,w.boneIndex1,w.weight1,w.boneIndex2,w.weight2,w.boneIndex3,w.weight3);
            }
            for(int s=0;s<mesh.blendShapeCount;s++)
            {
                int count=mesh.GetBlendShapeFrameCount(s);var frames=new List<Frame>();bool zero=false;
                for(int f=0;f<count;f++)
                {
                    var frame=new Frame{weight=mesh.GetBlendShapeFrameWeight(s,f),delta=new Vector3[source.Length]};
                    mesh.GetBlendShapeFrameVertices(s,f,frame.delta,null,null);frames.Add(frame);zero|=frame.weight==0;
                }
                if(!zero)frames.Add(new Frame{weight=0});
                frames.Sort((a,b)=>a.weight.CompareTo(b.weight));
                for(int f=1;f<frames.Count;f++)if(frames[f].weight==frames[f-1].weight)
                    throw new InvalidOperationException("Повторный вес кадра blend shape: "+renderer.name);
                shapes.Add(new Shape{index=s,frames=frames.ToArray()});
            }
        }
        public void Sample(Matrix4x4 worldToActor)
        {
            // No additional renderer scale: bindpose = boneWorld^-1 * meshWorld
            // at bind time. Multiplying meshWorld again would double its scale.
            for(int i=0;i<bones.Length;i++)pose[i]=Convert(worldToActor*bones[i].localToWorldMatrix)*bind[i];
            Array.Copy(vertices,shaped,vertices.Length);
            foreach(var shape in shapes)
            {
                float weight=renderer.GetBlendShapeWeight(shape.index);
                if(float.IsNaN(weight) || float.IsInfinity(weight))throw new InvalidOperationException("Неконечный blend shape: "+renderer.name);
                if(weight==0)continue;
                var frames=shape.frames;if(frames.Length<2)throw new InvalidOperationException("Недостаточно кадров blend shape: "+renderer.name);
                int next=1;while(next<frames.Length-1 && weight>frames[next].weight)next++;
                var a=frames[next-1];var b=frames[next];float t=(weight-a.weight)/(b.weight-a.weight);
                for(int i=0;i<shaped.Length;i++)
                {
                    Vector3 da=a.delta==null?Vector3.zero:a.delta[i],db=b.delta==null?Vector3.zero:b.delta[i];
                    shaped[i]+=Convert(da*(1-t)+db*t);
                }
            }
            var actorToLocal=renderer.transform.worldToLocalMatrix*worldToActor.inverse;
            Bounds actor=default,local=default;
            for(int i=0;i<vertices.Length;i++)
            {
                var point=SkinPoseModel.Position(shaped[i],weights[i],pose);
                if(!point.Finite)throw new InvalidOperationException("Неконечная skin-вершина "+i+": "+renderer.name);
                var p=new Vector3(point.X,point.Y,point.Z);var q=actorToLocal.MultiplyPoint3x4(p);
                if(!Convert(q).Finite)throw new InvalidOperationException("Некорректный transform skin: "+renderer.name);
                ActorVertices[i]=p;
                if(i==0){actor=new Bounds(p,Vector3.zero);local=new Bounds(q,Vector3.zero);}
                else{actor.Encapsulate(p);local.Encapsulate(q);}
            }
            ActorBounds=actor;LocalBounds=local;
        }
        private static RigVector Convert(Vector3 p)=>new RigVector(p.x,p.y,p.z);
        private static SkinTransform Convert(Matrix4x4 m)=>new SkinTransform(Convert(m.GetColumn(0)),Convert(m.GetColumn(1)),Convert(m.GetColumn(2)),Convert(m.GetColumn(3)));
    }
}
