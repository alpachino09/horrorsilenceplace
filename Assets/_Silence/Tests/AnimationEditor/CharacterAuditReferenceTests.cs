using System;
using NUnit.Framework;
using UnityEngine;
using Silence.Core;
using Silence.Multiplayer.Editor.Visual;

namespace Silence.Tests.Animation
{
    // Native Unity fixtures supplied for Test Runner; not cloud pass counts.
    public sealed class CharacterAuditReferenceTests
    {
        [Test] public void ClothOnAMixedGlassRendererStillGetsOpaqueDepthWithoutChangingTheGlass()
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)Assert.Ignore("URP is required.");
            var go=new GameObject("Face_with_Glass");var renderer=go.AddComponent<MeshRenderer>();
            var cloth=new Material(shader){name="Fabric_Hood"};var glass=new Material(shader){name="Glass_Lens"};
            try
            {
                cloth.SetFloat("_Surface",1);cloth.SetFloat("_ZWrite",0);cloth.SetShaderPassEnabled("DepthNormals",false);glass.SetFloat("_Surface",1);
                Assert.That(CharacterSurfaceFixer.IsSolid(renderer,cloth));Assert.That(CharacterSurfaceFixer.IsSolid(renderer,glass),Is.False);
                CharacterSurfaceFixer.MakeOpaque(cloth);Assert.That(cloth.GetFloat("_ZWrite"),Is.EqualTo(1));Assert.That(cloth.GetShaderPassEnabled("DepthNormals"));
                Assert.That(glass.GetFloat("_Surface"),Is.EqualTo(1));
            }
            finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(cloth);UnityEngine.Object.DestroyImmediate(glass);}
        }
        [Test] public void FingerAxisFollowsAReorientedImportedJoint()
        {
            var go=new GameObject("FingerAxisFixture");
            try
            {
                go.transform.rotation=Quaternion.Euler(73,-48,16);
                var worldDirection=go.transform.TransformDirection(Vector3.forward);var worldInward=go.transform.TransformDirection(Vector3.down);
                var axis=FingerCurlModel.BendAxis(new RigVector(worldDirection.x,worldDirection.y,worldDirection.z),new RigVector(worldInward.x,worldInward.y,worldInward.z));
                var local=go.transform.InverseTransformDirection(new Vector3(axis.X,axis.Y,axis.Z));Assert.That(Vector3.Distance(local,Vector3.right),Is.LessThan(.00001f));
                var bent=Quaternion.AngleAxis(FingerCurlModel.Degrees(false,0,1),local)*Vector3.forward;
                Assert.That(bent.y,Is.LessThan(0));
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
