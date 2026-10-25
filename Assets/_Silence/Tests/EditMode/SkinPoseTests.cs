using System;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class SkinPoseTests
    {
        private static SkinTransform Scale(float x,float y,float z,RigVector translation=default)
            => new SkinTransform(new RigVector(x,0,0),new RigVector(0,y,0),new RigVector(0,0,z),translation);
        private static void Equal(RigVector actual,RigVector expected,float tolerance=.00001f)
        {
            Assert.That(actual.X,Is.EqualTo(expected.X).Within(tolerance));
            Assert.That(actual.Y,Is.EqualTo(expected.Y).Within(tolerance));
            Assert.That(actual.Z,Is.EqualTo(expected.Z).Within(tolerance));
        }
        [TestCase(.01f)] [TestCase(1)] [TestCase(100)]
        public void FbxBoneAndBindScalesCancelWithoutApplyingRendererTwice(float fbxScale)
        {
            var bone=Scale(fbxScale,fbxScale,fbxScale,new RigVector(0,2,0));
            var bind=Scale(1/fbxScale,1/fbxScale,1/fbxScale,new RigVector(0,-2/fbxScale,0));
            var vertex=new RigVector(.4758f,1.3f,.3987f);
            Equal(SkinPoseModel.Position(vertex,new SkinWeights4(0,1),new[]{bone*bind}),vertex);
        }
        [Test] public void FourInfluencesBlendPositionsIncludingBindTranslations()
        {
            var transforms=new[]{Scale(1,1,1,new RigVector(1,0,0)),Scale(1,1,1,new RigVector(0,2,0)),
                Scale(1,1,1,new RigVector(0,0,3)),Scale(1,1,1,new RigVector(-2,0,0))};
            Equal(SkinPoseModel.Position(new RigVector(.5f,1,.25f),new SkinWeights4(0,.1f,1,.2f,2,.3f,3,.4f),transforms),new RigVector(-.2f,1.4f,1.15f));
        }
        [Test] public void UnusedInvalidBoneIndicesAreNotRead()
        {
            Equal(SkinPoseModel.Position(new RigVector(1,2,3),new SkinWeights4(0,1,-1,0,int.MaxValue,0,-50,0),new[]{Scale(1,1,1)}),new RigVector(1,2,3));
        }
        [Test] public void ActualGiantSkeletonIsNotSilentlyRescaled()
        {
            var giant=Scale(100,100,100)*Scale(1,1,1);
            var result=SkinPoseModel.Position(new RigVector(.4758f,1.3f,.3987f),new SkinWeights4(0,1),new[]{giant});
            Equal(result,new RigVector(47.58f,130,39.87f));
            Assert.That(result.Length,Is.GreaterThan(7.2f));
        }
        [Test] public void NonuniformReflectionAndQuarterTurnRespectColumnOrder()
        {
            var rotated=new SkinTransform(new RigVector(0,1,0),new RigVector(-1,0,0),new RigVector(0,0,1),new RigVector(2,3,4));
            var local=Scale(-2,.5f,3,new RigVector(1,0,0));
            Equal(SkinPoseModel.Position(new RigVector(1,2,3),new SkinWeights4(0,1),new[]{rotated*local}),new RigVector(1,2,13));
        }
        [Test] public void WorldToActorCancelsWorldPlacementWithoutChangingMetres()
        {
            var actor=Scale(2,2,2,new RigVector(10,20,-30));
            var inverseActor=Scale(.5f,.5f,.5f,new RigVector(-5,-10,15));
            var skin=inverseActor*actor*Scale(1,1,1,new RigVector(0,.3f,0));
            Equal(SkinPoseModel.Position(new RigVector(.2f,1,.4f),new SkinWeights4(0,1),new[]{skin}),new RigVector(.2f,1.3f,.4f));
        }
        [Test] public void AffineCompositionMatchesSequentialApplicationForRandomMatrices()
        {
            var random=new Random(47);
            RigVector V()=>new RigVector((float)random.NextDouble()*4-2,(float)random.NextDouble()*4-2,(float)random.NextDouble()*4-2);
            for(int i=0;i<100;i++)
            {
                var a=new SkinTransform(V(),V(),V(),V());var b=new SkinTransform(V(),V(),V(),V());var v=V();
                Equal((a*b).TransformPoint(v),a.TransformPoint(b.TransformPoint(v)),.0001f);
            }
        }
        [Test] public void NonfiniteBonesRemainDetectableByTheGuard()
        {
            var result=SkinPoseModel.Position(new RigVector(1,2,3),new SkinWeights4(0,1),new[]{Scale(float.NaN,1,1)});
            Assert.That(result.Finite,Is.False);
        }
    }
}
