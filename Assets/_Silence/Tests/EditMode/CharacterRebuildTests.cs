using System;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class CharacterRebuildTests
    {
        [TestCase(0f,0f,0)] [TestCase(1f,0f,1)] [TestCase(0f,1f,2)] [TestCase(.5f,.5f,2)]
        public void OnlyTheLastContributingIkLayerSolves(float upper,float reaction,int expected)
        {
            int final=CharacterIKPassRules.FinalLayer(1,upper,2,reaction);Assert.That(final,Is.EqualTo(expected));
            int solves=0;float handGoal=0;
            for(int layer=0;layer<3;layer++)
            {
                if(CharacterIKPassRules.ClearBeforeSolve(layer,final))handGoal=0;
                else if(layer==final){handGoal=1;solves++;}
            }
            Assert.That(solves,Is.EqualTo(1));Assert.That(handGoal,Is.EqualTo(1),"Later inactive callbacks must preserve the computed hand/foot goals.");
        }
        [Test] public void NamedLayersCanBeReorderedWithoutChoosingAnEarlierSolve()
            => Assert.That(CharacterIKPassRules.FinalLayer(3,1,1,1),Is.EqualTo(3));
        [Test] public void UnrelatedCameraEndCannotUnwindAnActiveFirstPersonView()
        {
            var stack=new CharacterRenderScopes<int>();var a=new object();var b=new object();stack.Push(a,1);
            Assert.That(stack.TryPopFor(b,out _,out _),Is.False);Assert.That(stack.Count,Is.EqualTo(1));
            Assert.That(stack.TryPopFor(a,out int state,out bool matched));Assert.That(state,Is.EqualTo(1));Assert.That(matched);
        }
        [Test] public void NestedAndMissingCameraEndsRestoreOnlyTheMatchingScope()
        {
            var stack=new CharacterRenderScopes<int>();var a=new object();var b=new object();stack.Push(a,10);stack.Push(b,20);stack.Push(b,30);
            Assert.That(stack.TryPopFor(b,out int value,out bool done));Assert.That(done);Assert.That(value,Is.EqualTo(30));Assert.That(stack.Count,Is.EqualTo(2));
            Assert.That(stack.TryPopFor(a,out value,out done));Assert.That(done,Is.False);Assert.That(value,Is.EqualTo(20));
            Assert.That(stack.TryPopFor(a,out value,out done));Assert.That(done);Assert.That(value,Is.EqualTo(10));Assert.That(stack.Count,Is.EqualTo(0));
        }
        [TestCase("Body_with_Glass","Fabric_Olive",CharacterSurfaceRole.Solid)]
        [TestCase("Face_Lashes","Skin",CharacterSurfaceRole.Solid)]
        [TestCase("Body_Clothed","Glass_Lens",CharacterSurfaceRole.Transparent)]
        [TestCase("Face","Eye_Cornea",CharacterSurfaceRole.Transparent)]
        [TestCase("Body_Clothed","M_Unknown",CharacterSurfaceRole.Solid)]
        [TestCase("Unclassified","Custom",CharacterSurfaceRole.Unknown)]
        public void SubmeshMaterialRoleTakesPriorityOverTheRendererName(string renderer,string material,CharacterSurfaceRole role)
            => Assert.That(CharacterSurfaceRules.Classify(renderer,material),Is.EqualTo(role));
        [TestCase(15)] [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ArmRelaxationHasTheSameResponseAtDifferentFrameRates(int fps)
        {
            float value=0;for(int i=0;i<fps;i++)value=NaturalArmPostureModel.Blend(value,true,1f/fps);
            Assert.That(value,Is.EqualTo(1-(float)Math.Exp(-10)).Within(.000001f));
        }
        [Test] public void ArmCorrectionHysteresisDoesNotChatterAtTheEntryAngle()
        {
            var s=new RigVector(.166f,.473f,0);float angle=17*(float)Math.PI/180;
            var e=s+new RigVector(.25f*(float)Math.Sin(angle),-.25f*(float)Math.Cos(angle),0);var h=new RigVector(.27f,0,.02f);
            var entering=NaturalArmPostureModel.Plan(s,e,h,.25f,.228f,1,0,.184f,.045f,.035f,false);
            var staying=NaturalArmPostureModel.Plan(s,e,h,.25f,.228f,1,0,.184f,.045f,.035f,true);
            Assert.That(entering.Required,Is.False);Assert.That(staying.Required,Is.True);
        }
        [Test] public void FingerCurlAxisFollowsAnatomyAndDoesNotInventXForDegenerateHands()
        {
            var axis=FingerCurlModel.BendAxis(new RigVector(0,0,1),new RigVector(0,-1,0));
            Assert.That(axis.X,Is.EqualTo(1));Assert.That(axis.Y,Is.EqualTo(0));Assert.That(axis.Z,Is.EqualTo(0));
            var rotated=FingerCurlModel.BendAxis(new RigVector(1,0,0),new RigVector(0,-1,0));Assert.That(rotated.Z,Is.EqualTo(-1));
            Assert.That(FingerCurlModel.BendAxis(default,new RigVector(0,1,0)).Length,Is.EqualTo(0));
        }
        [Test] public void ThumbAndFingerAnglesRemainBounded()
        {
            Assert.That(FingerCurlModel.Degrees(false,1,1),Is.EqualTo(75));Assert.That(FingerCurlModel.Degrees(true,0,2),Is.EqualTo(25));
            Assert.That(FingerCurlModel.Degrees(false,0,-1),Is.EqualTo(0));Assert.That(FingerCurlModel.Degrees(false,0,float.NaN),Is.EqualTo(0));
        }
        [Test] public void RecordingCannotAutomaticallyBecomeVisualAcceptance()
        {
            var review=new CharacterReviewSession();review.Begin(0,true);review.cases[0].measurements.Add(.02f,.01f,.02f,20,0,0);
            Assert.That(review.Mark(0,true,""),Is.False);review.Finish();Assert.That(review.cases[0].status,Is.EqualTo(CharacterReviewStatus.NeedsVisualReview));
            Assert.That(review.cases[0].manuallyConfirmed,Is.False);Assert.That(review.Mark(0,true,"Observed in Unity"));
            Assert.That(review.cases[0].status,Is.EqualTo(CharacterReviewStatus.Passed));Assert.That(review.cases[0].manuallyConfirmed);
        }
        [Test] public void UnavailableAndUnrecordedCasesCannotBePassed()
        {
            var review=new CharacterReviewSession();Assert.That(review.cases.Length,Is.EqualTo(15));
            review.Begin(13,false,"offline lab");Assert.That(review.Mark(13,true,""),Is.False);Assert.That(review.Mark(1,true,""),Is.False);
        }
        [Test] public void ReviewRejectsNonfiniteMeasurementsInsteadOfMarkingThemSuccessful()
        {
            var review=new CharacterReviewSession();review.Begin(2,true);review.cases[2].measurements.Add(float.NaN,0,0,0,0,0);review.Finish();
            Assert.That(review.cases[2].status,Is.EqualTo(CharacterReviewStatus.NotRun));Assert.That(review.cases[2].measurements.invalidSamples,Is.EqualTo(1));
        }
    }
}
