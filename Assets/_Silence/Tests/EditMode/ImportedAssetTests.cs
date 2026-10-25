using System;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class ImportedAssetTests
    {
        [TestCase("HumanF@Idle01",ImportedMotion.Idle)]
        [TestCase("Female_Walking01_Forward",ImportedMotion.Walk)]
        [TestCase("Male@Run01",ImportedMotion.Run)]
        [TestCase("Imported_WalkBack",ImportedMotion.WalkBack)]
        [TestCase("Basic_Run Fast",ImportedMotion.Sprint)]
        [TestCase("HumanF@Walk01_Left",ImportedMotion.StrafeLeft)]
        [TestCase("HumanF@Walking_Right",ImportedMotion.StrafeRight)]
        [TestCase("Run Backward",ImportedMotion.RunBack)]
        [TestCase("Imported_RunLeft",ImportedMotion.RunLeft)]
        [TestCase("Run Right",ImportedMotion.RunRight)]
        [TestCase("Crouching_Idle01",ImportedMotion.CrouchIdle)]
        [TestCase("CrouchWalk",ImportedMotion.CrouchWalk)]
        public void LocomotionNamesMapToTheCorrectDirection(string name,ImportedMotion role)
            => Assert.That(ImportedAssetModel.Motion(name),Is.EqualTo(role));
        [TestCase("Walk Sad")] [TestCase("Run Up Stairs")] [TestCase("CarryWalk")] [TestCase("WoundedIdle")] [TestCase("Walking Turn 180")] [TestCase("Run Turn 180")] [TestCase("Rifle Idle")]
        public void SpecialActionsDoNotReplaceOrdinaryLocomotion(string name)=>Assert.That(ImportedAssetModel.Motion(name),Is.Null);
        [Test] public void FemaleAndInPlaceRecordingsHaveAnExplicitPreference()
        {
            int female=ImportedAssetModel.MotionScore("Walk01","Kevin Iglesias/Female/InPlace",ImportedMotion.Walk);
            int male=ImportedAssetModel.MotionScore("Walk01","Kevin Iglesias/Male/RootMotion",ImportedMotion.Walk);
            Assert.That(female,Is.GreaterThan(male));Assert.That(ImportedAssetModel.MotionScore("Run01","Female",ImportedMotion.Walk),Is.EqualTo(-1));
        }
        [TestCase("Grass_01_BaseColor",ImportedGround.Grass)] [TestCase("Forest_02",ImportedGround.Forest)]
        [TestCase("Dirt",ImportedGround.Soil)] [TestCase("Gravel",ImportedGround.Gravel)]
        public void GroundRolesRecognizeTheExistingWorldLayers(string name,ImportedGround role)=>Assert.That(ImportedAssetModel.Ground(name),Is.EqualTo(role));
        [TestCase("GrassCard")] [TestCase("Bark")] [TestCase("Roof")] [TestCase("Brick")] [TestCase("Face_Skin")]
        public void FoliageBuildingsAndSkinAreNotGround(string name)=>Assert.That(ImportedAssetModel.Ground(name),Is.Null);
        [TestCase("Ground01_BaseColor_4K",ImportedMap.Color)] [TestCase("Ground01_NormalGL_4K",ImportedMap.Normal)]
        [TestCase("Ground01_Roughness",ImportedMap.Roughness)] [TestCase("Ground01_AO",ImportedMap.Occlusion)]
        [TestCase("Ground01_Height",ImportedMap.Height)] [TestCase("Ground01_ORM",ImportedMap.Unknown)]
        public void DataMapNamesDoNotBecomeAlbedoOrInventPackedChannels(string name,ImportedMap role)=>Assert.That(ImportedAssetModel.Map(name),Is.EqualTo(role));
        [Test] public void MatchingUsesTheTextureIdentityWithoutMapAndResolutionSuffixes()
        {
            Assert.That(ImportedAssetModel.TextureKey("Ground01_BaseColor_4K"),Is.EqualTo(ImportedAssetModel.TextureKey("Ground01_NormalGL_4K")));
            Assert.That(ImportedAssetModel.TextureKey("Grass02_Roughness_2048"),Is.EqualTo("grass 02"));
        }
        [Test] public void NewCadencePreservesMeasuredMovementSpeed()
        {
            float oldStance=2,newStance=1.5f,target=1.65f;
            float newRate=target/oldStance*ImportedAssetModel.CadenceRatio(oldStance,newStance);
            Assert.That(newRate*newStance,Is.EqualTo(target).Within(.00001f));
        }
        [TestCase(0,1)] [TestCase(1,0)] [TestCase(100,1)] [TestCase(float.NaN,1)]
        public void InvalidCadenceIsRejected(float previous,float next)=>Assert.Throws<ArgumentException>(()=>ImportedAssetModel.CadenceRatio(previous,next));
        [Test] public void RoughnessConvertsToUrpSmoothnessWithoutReversingTheWrongChannel()
        {Assert.That(ImportedAssetModel.Smoothness(.8f),Is.EqualTo(.2f).Within(.00001f));Assert.That(ImportedAssetModel.Smoothness(2),Is.EqualTo(0));}
    }
}
