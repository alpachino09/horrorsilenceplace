using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Silence.Multiplayer.Editor.Visual
{
    public sealed class VisualWorkshop : EditorWindow
    {
        private int seed=VisualFarmRebuilder.Seed;
        [MenuItem("SILENCE/13 Visual Workshop")]
        public static void Open()=>GetWindow<VisualWorkshop>("SILENCE Visual");
        private void OnGUI()
        {
            EditorGUILayout.LabelField("SILENCE — Visual Rebuild 2.0",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("One farm slice. Existing campaign and player prefab are copied and preserved. Original FBX/PBR assets are included.",MessageType.Info);
            seed=EditorGUILayout.IntField("Vegetation layout seed",seed);
            if(GUILayout.Button("Create visual copy (14)"))VisualFarmRebuilder.Generate(seed);
            if(GUILayout.Button("Validate scene and shaders (15)"))VisualSceneValidator.Validate();
            if(GUILayout.Button("Open visual scene"))
            {if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(VisualFarmRebuilder.ScenePath);}
            if(GUILayout.Button("Run native gameplay regression"))RunChecks();
            if(GUILayout.Button("Build visual Host + Client"))
            {if(VisualSceneValidator.Validate())NetworkFarmGenerator.BuildScene(VisualFarmRebuilder.ScenePath);}
            if(UnityEditor.PlayerSettings.colorSpace!=ColorSpace.Linear && GUILayout.Button("Use Linear color space (project setting)"))UnityEditor.PlayerSettings.colorSpace=ColorSpace.Linear;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lighting and review",EditorStyles.boldLabel);
            if(GUILayout.Button("Bake current visual scene lighting"))
            {
                if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before baking.");
                if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=VisualFarmRebuilder.ScenePath)throw new InvalidOperationException("Open the visual copy first.");
                if(!Lightmapping.BakeAsync())Debug.LogWarning("SILENCE: light bake did not start. Inspect Lighting window and project Lighting Settings.");
            }
            if(GUILayout.Button("Capture actual Game view + camera record"))Capture();
            if(GUILayout.Button("Record 15 seconds of actual performance"))VisualPerformanceRecorder.Start();
            EditorGUILayout.HelpBox("In Play: Esc → Graphics to choose Medium/High/Ultra. Film grain and vignette start disabled. The Blender previews are asset reviews, not Unity evidence.",MessageType.None);
        }
        private static void RunChecks()
        {
            if(!VisualSceneValidator.Validate())return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(VisualFarmRebuilder.ScenePath);
            Environment.SetEnvironmentVariable("SILENCE_GAMEPLAY_CHECKS",Path.GetFullPath("Library/Silence/VisualGameplayChecks.json"));EditorApplication.isPlaying=true;
        }
        [Serializable]private sealed class CaptureRecord{public string unity,utc,scene,image;public Vector3 cameraPosition,cameraEuler;public float fov;public string pipeline;}
        private static void Capture()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play with the visual scene, connect Host and frame the Game view first.");
            var camera=Camera.main;if(camera==null)throw new InvalidOperationException("No active main camera.");
            string folder=Path.GetFullPath("Library/Silence/VisualCaptures");Directory.CreateDirectory(folder);
            string stem=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"),image=Path.Combine(folder,stem+".png");
            File.WriteAllText(Path.Combine(folder,stem+".json"),JsonUtility.ToJson(new CaptureRecord{unity=Application.unityVersion,utc=DateTime.UtcNow.ToString("O"),
                scene=camera.gameObject.scene.path,image=image,cameraPosition=camera.transform.position,cameraEuler=camera.transform.eulerAngles,
                fov=camera.fieldOfView,pipeline=GraphicsSettings.currentRenderPipeline!=null?GraphicsSettings.currentRenderPipeline.name:"none"},true));
            ScreenCapture.CaptureScreenshot(image);Debug.Log("SILENCE: Game-view capture requested for end of frame: "+image+". Verify the PNG exists before treating it as evidence.");
        }
    }
}
