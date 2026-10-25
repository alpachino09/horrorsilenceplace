using NUnit.Framework;
using UnityEngine;
using Silence.Multiplayer.Editor.Visual;

namespace Silence.Tests.Animation
{
    // Native Test Runner fixtures. Supplied for Unity; not executed by .NET.
    public sealed class SkinPoseReferenceTests
    {
        private sealed class Fixture : System.IDisposable
        {
            public readonly GameObject root=new GameObject("Skin fixture");
            public readonly Mesh mesh=new Mesh();
            public readonly SkinnedMeshRenderer renderer;
            public readonly Transform a,b;
            public Fixture(float rendererScale=1,float boneScale=1)
            {
                a=new GameObject("A").transform;a.SetParent(root.transform,false);a.localScale=Vector3.one*boneScale;
                b=new GameObject("B").transform;b.SetParent(root.transform,false);b.localPosition=Vector3.up*.5f;
                renderer=new GameObject("Skin").AddComponent<SkinnedMeshRenderer>();renderer.transform.SetParent(root.transform,false);
                renderer.transform.localScale=Vector3.one*rendererScale;
                mesh.vertices=new[]{Vector3.zero,new Vector3(.4758f,0,0)/rendererScale,new Vector3(0,1.3f,.3987f)/rendererScale};
                mesh.triangles=new[]{0,1,2};
                mesh.boneWeights=new[]{new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=1,weight0=1},new BoneWeight{boneIndex0=0,weight0=.25f,boneIndex1=1,weight1=.75f}};
                mesh.bindposes=new[]{a.worldToLocalMatrix*renderer.transform.localToWorldMatrix,b.worldToLocalMatrix*renderer.transform.localToWorldMatrix};
                renderer.bones=new[]{a,b};renderer.rootBone=a;renderer.sharedMesh=mesh;renderer.quality=SkinQuality.Bone4;
            }
            public void Dispose(){Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }
        private static void Equal(Vector3 actual,Vector3 expected)
        {Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.0001f),actual+" != "+expected);}
        [TestCase(.01f,100)] [TestCase(100,.01f)] [TestCase(1,1)]
        public void FbxScaleIsAppliedOnceForRestAndAnimatedVertices(float rendererScale,float boneScale)
        {
            using(var f=new Fixture(rendererScale,boneScale))
            {
                var sampler=new SkinPoseSampler(f.renderer);sampler.Sample(f.root.transform.worldToLocalMatrix);
                Equal(sampler.ActorBounds.size,new Vector3(.4758f,1.3f,.3987f));
                var before=(Vector3[])sampler.ActorVertices.Clone();
                f.a.localPosition+=Vector3.right*.2f;f.b.localPosition+=Vector3.forward*.4f;
                sampler.Sample(f.root.transform.worldToLocalMatrix);
                Equal(sampler.ActorVertices[0],before[0]+Vector3.right*.2f);
                Equal(sampler.ActorVertices[1],before[1]+Vector3.forward*.4f);
                Equal(sampler.ActorVertices[2],before[2]+Vector3.right*.05f+Vector3.forward*.3f);
                for(int i=0;i<3;i++)
                {
                    var p=f.renderer.transform.InverseTransformPoint(sampler.ActorVertices[i]);
                    Assert.That(Vector3.Distance(p,sampler.LocalBounds.ClosestPoint(p)),Is.LessThan(.0001f));
                }
            }
        }
        [Test] public void NonuniformWorldPlacementDoesNotChangeActorBounds()
        {
            using(var f=new Fixture(.01f,100))
            {
                f.root.transform.SetPositionAndRotation(new Vector3(10,20,-30),Quaternion.Euler(15,30,45));
                f.root.transform.localScale=new Vector3(2,3,-4);
                var sampler=new SkinPoseSampler(f.renderer);sampler.Sample(f.root.transform.worldToLocalMatrix);
                Equal(sampler.ActorBounds.size,new Vector3(.4758f,1.3f,.3987f));
                Equal(sampler.LocalBounds.size,new Vector3(47.58f,130,39.87f));
            }
        }
        [TestCase(-50)] [TestCase(50)] [TestCase(150)]
        public void SingleFrameBlendShapeIsAppliedBeforeSkinning(float weight)
        {
            using(var f=new Fixture(.01f,100))
            {
                var delta=new[]{new Vector3(20,0,0),Vector3.zero,Vector3.zero};
                f.mesh.AddBlendShapeFrame("BlinkTest",100,delta,null,null);f.renderer.SetBlendShapeWeight(0,weight);
                var sampler=new SkinPoseSampler(f.renderer);sampler.Sample(f.root.transform.worldToLocalMatrix);
                Equal(sampler.ActorVertices[0],Vector3.right*(.2f*weight/100));
            }
        }
        [TestCase(25,.05f)] [TestCase(75,.25f)] [TestCase(150,.7f)]
        public void MultipleBlendFramesInterpolateWithoutDroppingMorphs(float weight,float expected)
        {
            using(var f=new Fixture())
            {
                f.mesh.AddBlendShapeFrame("Test",50,new[]{Vector3.right*.1f,Vector3.zero,Vector3.zero},null,null);
                f.mesh.AddBlendShapeFrame("Test",100,new[]{Vector3.right*.4f,Vector3.zero,Vector3.zero},null,null);
                f.renderer.SetBlendShapeWeight(0,weight);var sampler=new SkinPoseSampler(f.renderer);sampler.Sample(Matrix4x4.identity);
                Equal(sampler.ActorVertices[0],Vector3.right*expected);
            }
        }
        [Test] public void UnscaledFixtureAgreesWithNativeBakeAfterRotatingBones()
        {
            using(var f=new Fixture())
            {
                f.a.localRotation=Quaternion.Euler(10,0,40);f.b.localRotation=Quaternion.Euler(-30,20,0);
                var sampler=new SkinPoseSampler(f.renderer);sampler.Sample(Matrix4x4.identity);var baked=new Mesh();
                try
                {
                    f.renderer.BakeMesh(baked,false);var vertices=baked.vertices;
                    for(int i=0;i<vertices.Length;i++)Equal(sampler.ActorVertices[i],f.renderer.transform.TransformPoint(vertices[i]));
                }
                finally{Object.DestroyImmediate(baked);}
            }
        }
        [Test] public void ActualHundredfoldBoneGrowthIsStillReportedAsGiant()
        {
            using(var f=new Fixture())
            {
                f.a.localScale=f.b.localScale=Vector3.one*100;
                var sampler=new SkinPoseSampler(f.renderer);sampler.Sample(Matrix4x4.identity);
                Assert.That(sampler.ActorBounds.size.magnitude,Is.GreaterThan(7.2f));
            }
        }
    }
}
