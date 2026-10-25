using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Silence.Graphics.Players;
using Silence.Player;
using Silence.Multiplayer.Editor.Visual;

namespace Silence.Tests.Animation
{
    // Native Unity tests supplied for Test Runner. They are not cloud pass counts.
    public sealed class CharacterReworkReferenceTests
    {
        [Test] public void OpaqueRepairPreservesTextureAndRgbWhileRestoringDepthAndAlpha()
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)Assert.Ignore("URP Lit is required.");
            var material=new Material(shader);var texture=new Texture2D(2,2);
            try
            {
                material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",new Color(.2f,.3f,.4f,.12f));material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);material.renderQueue=3000;material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                CharacterSurfaceFixer.MakeOpaque(material);
                Assert.That(material.GetTexture("_BaseMap"),Is.SameAs(texture));Assert.That(material.GetColor("_BaseColor"),Is.EqualTo(new Color(.2f,.3f,.4f,1)));
                Assert.That(material.GetFloat("_Surface"),Is.EqualTo(0));Assert.That(material.GetFloat("_ZWrite"),Is.EqualTo(1));Assert.That(material.renderQueue,Is.EqualTo(2000));
                Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),Is.False);Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"),Is.False);
            }
            finally{UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(texture);}
        }
        [Test] public void ATransparentLensOnTheBodyIsPreservedWhileItsJacketIsCopied()
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)Assert.Ignore("URP Lit is required.");
            string folder="Assets/_Silence/Generated/MaterialAudit-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var root=new GameObject("Body_Clothed");var renderer=root.AddComponent<SkinnedMeshRenderer>();
            var glass=new Material(shader){name="Glass_Lens"};glass.SetFloat("_Surface",1);
            var jacket=new Material(shader){name="Fabric_Olive"};jacket.SetFloat("_Surface",1);
            renderer.sharedMaterials=new[]{glass,jacket};
            try
            {
                CharacterSurfaceFixer.Prepare(root.transform,folder,new System.Collections.Generic.List<string>());
                Assert.That(renderer.sharedMaterials[0],Is.SameAs(glass));Assert.That(glass.GetFloat("_Surface"),Is.EqualTo(1));
                Assert.That(renderer.sharedMaterials[1],Is.Not.SameAs(jacket));Assert.That(jacket.GetFloat("_Surface"),Is.EqualTo(1));Assert.That(renderer.sharedMaterials[1].GetFloat("_Surface"),Is.EqualTo(0));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(glass);UnityEngine.Object.DestroyImmediate(jacket);AssetDatabase.DeleteAsset(folder);}
        }
        [Test] public void HandGoalsResetBeforeOwnersAndConstraintsRunAfterFlashlight()
        {
            int Order(Type t)=>((DefaultExecutionOrder)Attribute.GetCustomAttribute(t,typeof(DefaultExecutionOrder))).order;
            Assert.That(Order(typeof(CharacterArmIKReset)),Is.LessThan(Order(typeof(CharacterAnimationActions))));
            Assert.That(Order(typeof(CharacterAnimationActions)),Is.LessThan(Order(typeof(FlashlightAnimationController))));
            Assert.That(Order(typeof(FlashlightAnimationController)),Is.LessThan(Order(typeof(CharacterArmPoseSafety))));
        }
        [Test] public void GeneratedCharacterCopiesHaveSampledBoundsAndOpaqueSolidSurfaces()
        {
            const string root="Assets/_Silence/Generated/CharacterRework";
            if(!AssetDatabase.IsValidFolder(root))Assert.Ignore("Run menu 27 first.");
            var players=AssetDatabase.FindAssets("t:Prefab",new[]{root}).Select(g=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).Where(p=>p!=null && p.GetComponent<PlayerContext>()!=null).ToArray();
            if(players.Length==0)Assert.Ignore("No reworked player copies generated.");
            foreach(var player in players)
            {
                var model=player.GetComponent<PlayerAvatarPresentation>().Model;
                Assert.That(model.GetComponent<ICharacterIKCoordinator>(),Is.SameAs(model.GetComponent<CharacterArmPoseSafety>()));
                var report=JsonUtility.FromJson<CharacterReworkPreparation.Report>(File.ReadAllText(Path.GetDirectoryName(AssetDatabase.GetAssetPath(player))+"/character-audit.json"));
                Assert.That(report.nativePoseSampled);Assert.That(report.sampledClips,Is.GreaterThan(0));Assert.That(report.nativePlayTested,Is.False);
                foreach(var renderer in player.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
                    if(CharacterSurfaceFixer.IsSolid(renderer,material))
                    {Assert.That(material.GetFloat("_Surface"),Is.EqualTo(0));Assert.That(material.GetFloat("_ZWrite"),Is.EqualTo(1));Assert.That(material.GetColor("_BaseColor").a,Is.EqualTo(1));}
            }
        }
        [Test] public void ControllerBasedBoundsAuditPreservesSourceClipsAndActiveWorld()
        {
            const string root="Assets/_Silence/Generated/HumanMotion";
            if(!AssetDatabase.IsValidFolder(root))Assert.Ignore("Install animation player via 25 first.");
            var player=AssetDatabase.FindAssets("t:Prefab",new[]{root}).Select(g=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).LastOrDefault(p=>p!=null && p.GetComponent<PlayerContext>()!=null);
            if(player==null)Assert.Ignore("No installed animation player available.");
            var originals=player.GetComponent<PlayerAvatarPresentation>().Model.runtimeAnimatorController.animationClips.Select(AssetDatabase.GetAssetPath).Where(File.Exists).Distinct().ToDictionary(p=>p,File.ReadAllBytes);
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();int count=UnityEngine.SceneManagement.SceneManager.sceneCount;bool dirty=active.isDirty;
            string folder="Assets/_Silence/Generated/BoundsAudit-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            string path=folder+"/Player.prefab";Assert.That(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(player),path));
            var contents=PrefabUtility.LoadPrefabContents(path);
            try
            {
                PlayerViewWorkshop.ConfigurePlayer(contents,folder);
                var report=CharacterReworkPreparation.Prepare(contents,folder);
                Assert.That(report.nativePoseSampled);Assert.That(report.samplingMode,Does.Contain("AnimatorController"));Assert.That(report.skins.All(s=>s.maximumPoseActorSize.magnitude>0));
                Assert.That(AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(folder+"/__PoseSampling.controller"),Is.Null);
                foreach(var original in originals)Assert.That(File.ReadAllBytes(original.Key),Is.EqualTo(original.Value));
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Is.EqualTo(active));Assert.That(UnityEngine.SceneManagement.SceneManager.sceneCount,Is.EqualTo(count));Assert.That(active.isDirty,Is.EqualTo(dirty));
            }
            finally{PrefabUtility.UnloadPrefabContents(contents);AssetDatabase.DeleteAsset(folder);}
        }
    }
}
