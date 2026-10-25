using System;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class SkinBoundsTests
    {
        [TestCase(.001f)] [TestCase(.00001f)] [TestCase(.00000001f)]
        public void SmallFacialPartsAreValidInLocalMeshUnits(float scale)
        {
            var extents=new RigVector(.02f*scale,.01f*scale,.005f*scale);
            Assert.That(SkinBoundsModel.IsUsable(new RigVector(0,0,0),extents),Is.True);
            Assert.That(RigVector.Dot(extents,extents),Is.LessThan(.0001f));
        }
        [Test] public void ZeroBoundsAreStillRejected()
            => Assert.That(SkinBoundsModel.IsUsable(new RigVector(0,1,0),new RigVector(0,0,0)),Is.False);
        [Test] public void FlatSurfaceWithFiniteNonzeroSizeIsValid()
            => Assert.That(SkinBoundsModel.IsUsable(new RigVector(0,1,0),new RigVector(.01f,0,.02f)),Is.True);
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void NonfiniteBoundsAreRejected(float value)
        {
            Assert.That(SkinBoundsModel.IsUsable(new RigVector(value,0,0),new RigVector(1,1,1)),Is.False);
            Assert.That(SkinBoundsModel.IsUsable(new RigVector(0,0,0),new RigVector(1,value,1)),Is.False);
        }
        [Test] public void NegativeExtentIsRejected()
            => Assert.That(SkinBoundsModel.IsUsable(new RigVector(0,0,0),new RigVector(.01f,-.01f,.01f)),Is.False);

        [Test] public void FbxNonuniformScaleDoesNotInventAnOversizedCharacter()
        {
            var e=new RigVector(.005f,100,25);
            var corrected=SkinBoundsModel.TransformExtents(e,new RigVector(100,0,0),new RigVector(0,.01f,0),new RigVector(0,0,.01f));
            Assert.That(corrected.X,Is.EqualTo(.5f).Within(.00001f));Assert.That(corrected.Y,Is.EqualTo(1));Assert.That(corrected.Z,Is.EqualTo(.25f));
            float oldAverage=(float)Math.Sqrt((100*100+.01*.01+.01*.01)/3);
            Assert.That(e.Length*2*oldAverage,Is.GreaterThan(7.2f));Assert.That(corrected.Length*2,Is.LessThan(7.2f));
        }
        [Test] public void QuarterTurnPermutesTheBoundsAxes()
        {
            var e=SkinBoundsModel.TransformExtents(new RigVector(1,2,3),new RigVector(0,1,0),new RigVector(-1,0,0),new RigVector(0,0,1));
            Assert.That(e.X,Is.EqualTo(2));Assert.That(e.Y,Is.EqualTo(1));Assert.That(e.Z,Is.EqualTo(3));
        }
        [Test] public void ReflectionAndShearKeepPositiveExtents()
        {
            var e=SkinBoundsModel.TransformExtents(new RigVector(1,2,3),new RigVector(-2,0,0),new RigVector(1,3,0),new RigVector(0,-1,.5f));
            Assert.That(e.X,Is.EqualTo(4));Assert.That(e.Y,Is.EqualTo(9));Assert.That(e.Z,Is.EqualTo(1.5f));
        }
        [Test] public void AffineResultMatchesExtremaOfAllEightCorners()
        {
            var random=new Random(46);
            for(int n=0;n<100;n++)
            {
                RigVector Column()=>new RigVector((float)random.NextDouble()*4-2,(float)random.NextDouble()*4-2,(float)random.NextDouble()*4-2);
                var x=Column();var y=Column();var z=Column();var extents=new RigVector(.3f,.8f,.15f);
                var expected=SkinBoundsModel.TransformExtents(extents,x,y,z);float mx=0,my=0,mz=0;
                for(int i=0;i<8;i++)
                {
                    var point=x*((i&1)==0?-extents.X:extents.X)+y*((i&2)==0?-extents.Y:extents.Y)+z*((i&4)==0?-extents.Z:extents.Z);
                    mx=Math.Max(mx,Math.Abs(point.X));my=Math.Max(my,Math.Abs(point.Y));mz=Math.Max(mz,Math.Abs(point.Z));
                }
                Assert.That(expected.X,Is.EqualTo(mx).Within(.00001f));Assert.That(expected.Y,Is.EqualTo(my).Within(.00001f));Assert.That(expected.Z,Is.EqualTo(mz).Within(.00001f));
            }
        }
    }
}
