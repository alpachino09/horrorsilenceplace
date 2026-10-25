using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Core;
using Silence.Graphics.Players;
using Silence.Multiplayer.Editor.Visual;

namespace Silence.Tests.Animation
{
    // Native fixtures provided for the user's Unity Test Runner; not cloud passes.
    public sealed class ImportedAssetsReferenceTests
    {
        [Test] public void KevinIglesiasScanFindsRealHumanoidRecordingsWithoutPolyOne()
        {
            const string root="Assets/Kevin Iglesias";if(!AssetDatabase.IsValidFolder(root))Assert.Ignore("Import Human Basic Motions FREE first.");
            var clips=ImportedAssetCatalog.Motions(root);Assert.That(clips,Is.Not.Empty);
            foreach(var role in new[]{ImportedMotion.Idle,ImportedMotion.Walk,ImportedMotion.Run})
                Assert.That(clips.Any(c=>ImportedAssetCatalog.Role(c)==role),Is.True,role.ToString());
        }
        [Test] public void GroundInstallationCopiesTerrainDataAndPreservesHeightsPaintingAndSourceLayer()
        {
            if(Shader.Find("Universal Render Pipeline/Lit")==null || Shader.Find("Universal Render Pipeline/Terrain/Lit")==null)Assert.Ignore("URP is required.");
            var active=SceneManager.GetActiveScene();if(string.IsNullOrEmpty(active.path))Assert.Ignore("Save the open scene before this additive fixture.");
            string folder="Assets/_Silence/Generated/ImportedTerrainTest-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            Scene scene=default;
            try
            {
                scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var color=new Texture2D(2,2);AssetDatabase.CreateAsset(color,folder+"/Color.asset");
                var layer=new TerrainLayer{name="Grass"};AssetDatabase.CreateAsset(layer,folder+"/Grass.terrainlayer");
                var data=new TerrainData{heightmapResolution=33,alphamapResolution=16,size=new Vector3(8,2,8)};
                var heights=new float[33,33];heights[12,16]=.25f;data.SetHeights(0,0,heights);data.terrainLayers=new[]{layer};
                var paint=new float[16,16,1];for(int y=0;y<16;y++)for(int x=0;x<16;x++)paint[y,x,0]=1;data.SetAlphamaps(0,0,paint);AssetDatabase.CreateAsset(data,folder+"/Original.asset");
                var terrain=Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();var choices=new ImportedTextureSet[4];choices[(int)ImportedGround.Grass]=new ImportedTextureSet{name="TestGrass",color=color};
                var report=ImportedGroundInstaller.Apply(scene,choices,4,folder+"/Output");
                Assert.That(report.terrainLayers,Is.EqualTo(1));Assert.That(terrain.terrainData,Is.Not.SameAs(data));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(terrain.terrainData));
                Assert.That(terrain.terrainData.GetHeights(0,0,33,33),Is.EqualTo(data.GetHeights(0,0,33,33)));
                Assert.That(terrain.terrainData.GetAlphamaps(0,0,16,16),Is.EqualTo(data.GetAlphamaps(0,0,16,16)));
                Assert.That(layer.diffuseTexture,Is.Null);Assert.That(terrain.terrainData.terrainLayers[0].diffuseTexture,Is.SameAs(color));
            }
            finally{if(scene.IsValid() && scene.isLoaded)EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);AssetDatabase.DeleteAsset(folder);}
        }
        [Test] public void ImportedProfilesActuallyReferenceNewIdleWalkRun()
        {
            const string root="Assets/_Silence/Generated/ImportedAssets";if(!AssetDatabase.IsValidFolder(root))Assert.Ignore("Install via 28 first.");
            var profiles=AssetDatabase.FindAssets("t:PlayerAnimationProfile",new[]{root}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<PlayerAnimationProfile>).ToArray();
            if(profiles.Length==0)Assert.Ignore("Install motions via 28 first.");
            foreach(var profile in profiles)
            {
                Assert.That(profile.controller,Is.Not.Null);
                foreach(string name in new[]{"Imported_Idle","Imported_Walk","Imported_Run"})Assert.That(profile.controller.animationClips.Any(c=>c.name==name),Is.True,name);
                foreach(var clip in profile.controller.animationClips.Where(c=>c.name.StartsWith("Imported_",StringComparison.Ordinal)))Assert.That(AnimationUtility.GetAnimationEvents(clip),Is.Empty);
            }
        }
    }
}
