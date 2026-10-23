using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Silence.Graphics;
using Silence.Gameplay;

namespace Silence.Multiplayer.Editor.Visual
{
    internal static class LightingConfigurator
    {
        public static void Build(GameObject root,GameplayDirector game,Terrain terrain,VisualAssetLibrary art,string output)
        {
            var lighting=Lightmapping.TryGetLightingSettings(out var previousLighting)?Object.Instantiate(previousLighting):new LightingSettings();
            lighting.name="SILENCE visual copy lighting";lighting.bakedGI=true;lighting.realtimeGI=false;
            AssetDatabase.CreateAsset(lighting,output+"/LightingSettings.asset");Lightmapping.lightingSettings=lighting;
            Lightmapping.lightingDataAsset=null; // A future bake must not write into the source campaign's lighting data.
            var atmosphere=root.AddComponent<VisualAtmosphere>();atmosphere.game=game;
            atmosphere.sun=RenderSettings.sun;atmosphere.timeOfDay=17.3f;
            if(atmosphere.sun!=null)
            {
                atmosphere.sun.intensity=.55f;atmosphere.sun.color=new Color(1,.78f,.55f);atmosphere.sun.shadows=LightShadows.Soft;
                atmosphere.sun.shadowBias=.025f;atmosphere.sun.shadowNormalBias=.15f;atmosphere.sun.lightmapBakeType=LightmapBakeType.Mixed;
                atmosphere.sun.transform.rotation=Quaternion.Euler(8,-38,0);
            }
            var sky=new Material(VisualAssetLibrary.RequireShader("Skybox/Procedural")){name="Farm physical sky"};
            sky.SetFloat("_SunDisk",2);sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",1.25f);
            sky.SetColor("_SkyTint",new Color(.38f,.42f,.46f));sky.SetColor("_GroundColor",new Color(.1f,.1f,.09f));sky.SetFloat("_Exposure",.65f);
            AssetDatabase.CreateAsset(sky,output+"/Materials/Skybox.mat");RenderSettings.skybox=sky;
            var volume=root.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,output+"/PostProcessing.asset");volume.sharedProfile=profile;
            var tone=profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
            var colors=profile.Add<ColorAdjustments>();colors.postExposure.Override(.25f);colors.saturation.Override(-9);colors.contrast.Override(7);
            var bloom=profile.Add<Bloom>();bloom.intensity.Override(.08f);bloom.threshold.Override(1.4f);bloom.scatter.Override(.45f);
            var grain=profile.Add<FilmGrain>();grain.intensity.Override(.035f);grain.active=false;
            var vignette=profile.Add<Vignette>();vignette.intensity.Override(.1f);vignette.active=false;
            var blur=profile.Add<MotionBlur>();blur.active=false;var aberration=profile.Add<ChromaticAberration>();aberration.active=false;
            var fogObject=new GameObject("Local single-scattering height fog");fogObject.transform.SetParent(root.transform,false);fogObject.transform.localPosition=new Vector3(0,5,0);fogObject.transform.localScale=new Vector3(110,20,110);fogObject.layer=31;
            // A proxy volume is rendering infrastructure, not a visible cube used as final environment art.
            var mesh=Resources.GetBuiltinResource<Mesh>("Cube.fbx");fogObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var fogMaterial=new Material(VisualAssetLibrary.RequireShader("SILENCE/HeightFog")){name="Farm height scattering"};
            AssetDatabase.CreateAsset(fogMaterial,output+"/Materials/HeightFog.mat");var renderer=fogObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=fogMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var fog=fogObject.AddComponent<FarmFogVolume>();fog.game=game;fog.fogMaterial=fogMaterial;
            var profiles=new GraphicsQualityProfile[3];
            for(int i=0;i<3;i++)
            {
                var q=ScriptableObject.CreateInstance<GraphicsQualityProfile>();q.preset=(GraphicsPreset)i;q.renderScale=1;
                q.shadowDistance=new[]{40,65,90}[i];q.shadowResolution=i==2?4096:2048;q.msaaSamples=2;
                q.lodBias=new[]{.8f,1.15f,1.6f}[i];q.vegetationDensity=new[]{.4f,.7f,1}[i];q.viewDistance=new[]{125,155,190}[i];
                q.fogSteps=new[]{8,16,24}[i];q.particles=i>0;profiles[i]=q;AssetDatabase.CreateAsset(q,output+"/"+q.preset+".asset");
            }
            var manager=root.AddComponent<GraphicsQualityManager>();manager.Configure(profiles,volume,terrain,fog);root.AddComponent<VisualSettingsUI>().quality=manager;
            Probe(root.transform,"House interior",new Vector3(-10,1.3f,0),new Vector3(7.5f,3,7.5f));
            Probe(root.transform,"Farm exterior",new Vector3(0,2,-2),new Vector3(38,10,38));
            var probes=root.AddComponent<LightProbeGroup>();var positions=new System.Collections.Generic.List<Vector3>();
            for(int z=-12;z<=15;z+=6)for(int x=-15;x<=15;x+=6)positions.Add(new Vector3(x,1.3f,z));probes.probePositions=positions.ToArray();
            EditorUtility.SetDirty(profile);
        }
        private static void Probe(Transform root,string name,Vector3 position,Vector3 size)
        {
            var go=new GameObject(name+" reflection");go.transform.SetParent(root,false);go.transform.localPosition=position;
            var probe=go.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=128;probe.size=size;
            probe.boxProjection=true;probe.blendDistance=2;probe.cullingMask=~(1<<31);probe.shadowDistance=18;
        }
    }
}
