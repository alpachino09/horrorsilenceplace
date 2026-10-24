using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using NUnit.Framework;
using Silence.HoodedGirl.Editor;

namespace Silence.Tests
{
    // Executes the actual resolver in the replacement file, without stubbing any Unity APIs.
    public sealed class HumanoidMappingTests
    {
        private static readonly string[] Sides={"Left","Right"},Fingers={"Thumb","Index","Middle","Ring","Little"},Segments={"Proximal","Intermediate","Distal"};
        private static List<string> Names(bool spacedFingers)
        {
            var result=new List<string>{"Hips","Spine","Chest","UpperChest","Neck","Head","Jaw","LeftEye","RightEye"};
            foreach(string side in Sides)
            {
                foreach(string name in new[]{"Shoulder","UpperArm","LowerArm","Hand","UpperLeg","LowerLeg","Foot","Toes"})result.Add(side+name);
                foreach(string finger in Fingers)foreach(string segment in Segments)result.Add(spacedFingers?side+" "+finger+" "+segment:side+finger+segment);
            }
            return result;
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DeliveredFbxHierarchyMapsAll55IncludingSpacedFingerLabels(int level)
        {
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,"HoodedGirl-fbx-hierarchy.json"))))
            {
                var bones=doc.RootElement.GetProperty("lods")[level].GetProperty("bones").EnumerateArray().Select(n=>n.GetString()).ToArray();
                var labels=Names(true);Assert.That(labels.Count,Is.EqualTo(55));
                // This reproduces the old exact-match guard: 25 matched, so menu 22 stopped.
                Assert.That(labels.Count(bones.Contains),Is.EqualTo(25));
                var bindings=HoodedGirlBoneMapping.Resolve(labels,bones);
                Assert.That(bindings.Count,Is.EqualTo(55));Assert.That(bindings.Select(b=>b.BoneName).Distinct().Count(),Is.EqualTo(55));
                Assert.That(bindings.Single(b=>b.BoneName=="LeftThumbProximal").HumanName,Is.EqualTo("Left Thumb Proximal"));
            }
        }
        [Test] public void CompactApiLabelsRemainSupported()
        { var names=Names(false);Assert.That(HoodedGirlBoneMapping.Resolve(names,names).Count,Is.EqualTo(55)); }
        [Test] public void WhitespaceIsRemovedOnlyForLookupAndOriginalApiLabelIsRetained()
        { var binding=HoodedGirlBoneMapping.Resolve(new[]{"Left\tThumb Proximal"},new[]{"LeftThumbProximal"}).Single();Assert.That(binding.HumanName,Is.EqualTo("Left\tThumb Proximal"));Assert.That(binding.BoneName,Is.EqualTo("LeftThumbProximal")); }
        [Test] public void MissingActualBoneIsReportedInsteadOfAcceptingPartialAvatar()
        { var names=Names(false);var error=Assert.Throws<InvalidOperationException>(()=>HoodedGirlBoneMapping.Resolve(Names(true),names.Where(n=>n!="RightHand")));Assert.That(error.Message,Does.Contain("RightHand"));Assert.That(error.Message,Does.Contain("54/55")); }
        [Test] public void DuplicateHeadTransformIsRejectedInsteadOfMappingMeshAsBone()
        { var names=Names(false);var error=Assert.Throws<InvalidOperationException>(()=>HoodedGirlBoneMapping.Resolve(names,names.Concat(new[]{"Head"})));Assert.That(error.Message,Does.Contain("Duplicate FBX transform")); }
        [Test] public void DuplicateHumanLabelsAreRejected()
        { Assert.Throws<InvalidOperationException>(()=>HoodedGirlBoneMapping.Resolve(new[]{"LeftHand","Left Hand"},new[]{"LeftHand"})); }
        [Test] public void NonHumanoidExtraTransformsDoNotAffectMapping()
        { var names=Names(false);Assert.That(HoodedGirlBoneMapping.Resolve(Names(true),names.Concat(new[]{"Root","Face","Body_Clothed"})).Count,Is.EqualTo(55)); }
        [Test] public void NullOrEmptyInputsAreRejected()
        { Assert.Throws<ArgumentException>(()=>HoodedGirlBoneMapping.Resolve(Array.Empty<string>(),Array.Empty<string>()));Assert.Throws<ArgumentNullException>(()=>HoodedGirlBoneMapping.Resolve(new[]{"Head"},null)); }
    }
}
