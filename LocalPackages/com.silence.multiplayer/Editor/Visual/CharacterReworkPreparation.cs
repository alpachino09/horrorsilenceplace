using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Silence.Player;
using Silence.Graphics.Players;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class CharacterReworkPreparation
    {
        [Serializable] public sealed class Report
        {
            public string kind="Unity Editor geometry/pose measurements; native Play acceptance is separate";
            public int opaqueMaterials, normalizedMeshes, sampledClips, sampledPoses;
            public bool nativePoseSampled, envelopeSampled, nativePlayTested;
            public float torsoWidth, torsoDepth;
            public List<string> preservedMaterials=new List<string>();
            public List<Skin> skins=new List<Skin>();
            public List<Pose> poses=new List<Pose>();
        }
        [Serializable] public sealed class Skin { public string renderer, mesh; public int vertices, repairedVertices; public Vector3 sampledBoundsSize; }
        [Serializable] public sealed class Pose { public string clip; public float seconds, leftWristFromHips, rightWristFromHips; public Vector3 leftWrist, rightWrist; }
        private static Transform VisualRoot(PlayerContext actor,Animator animator)
        {
            var t=animator.transform;while(t.parent!=null && t.parent!=actor.transform)t=t.parent;
            if(t.parent!=actor.transform)throw new InvalidOperationException("Нужен отдельный visual root игрока.");return t;
        }
        public static Report Prepare(GameObject player,string folder)
        {
            var actor=player.GetComponent<PlayerContext>();var model=player.GetComponent<PlayerAvatarPresentation>().Model;
            if(model==null || model.avatar==null || !model.avatar.isValid || !model.isHuman)throw new InvalidOperationException("Нужен действительный Humanoid Avatar.");
            foreach(var side in new[]{new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand},new[]{HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand}})
            {
                var chain=side.Select(model.GetBoneTransform).ToArray();
                if(chain.Any(t=>t==null) || !chain[1].IsChildOf(chain[0]) || !chain[2].IsChildOf(chain[1]))throw new InvalidOperationException("Неверная иерархия плечо/локоть/кисть: "+side[0]);
            }
            var visual=VisualRoot(actor,model);var report=new Report();
            report.opaqueMaterials=CharacterSurfaceFixer.Prepare(visual,folder,report.preservedMaterials);
            var renderers=visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var repaired=new Dictionary<Mesh,Mesh>();
            foreach(var renderer in renderers)
            {
                var mesh=renderer.sharedMesh;if(mesh==null)throw new InvalidOperationException("Нет меша: "+renderer.name);
                if(renderer.bones.Any(b=>b==null) || mesh.bindposes.Length!=renderer.bones.Length)throw new InvalidOperationException("Повреждены bind poses/ссылки костей: "+renderer.name);
                var record=new Skin{renderer=AnimationUtility.CalculateTransformPath(renderer.transform,visual),mesh=AssetDatabase.GetAssetPath(mesh),vertices=mesh.vertexCount};
                var weights=mesh.boneWeights;
                if(weights.Length!=mesh.vertexCount)throw new InvalidOperationException("Нужен доступный четырёхвесовой skin: "+renderer.name);
                for(int i=0;i<weights.Length;i++)
                {
                    var w=weights[i];float sum=w.weight0+w.weight1+w.weight2+w.weight3;
                    if(!(sum>.0001f) || float.IsInfinity(sum) || w.weight0<0 || w.weight1<0 || w.weight2<0 || w.weight3<0 ||
                        Invalid(w.boneIndex0,w.weight0,renderer.bones.Length) || Invalid(w.boneIndex1,w.weight1,renderer.bones.Length) || Invalid(w.boneIndex2,w.weight2,renderer.bones.Length) || Invalid(w.boneIndex3,w.weight3,renderer.bones.Length))
                        throw new InvalidOperationException("Некорректный skin vertex "+i+" в "+renderer.name+". Автоматически угадывать кость нельзя.");
                    if(Mathf.Abs(sum-1)>.0001f)
                    { w.weight0/=sum;w.weight1/=sum;w.weight2/=sum;w.weight3/=sum;weights[i]=w;record.repairedVertices++; }
                }
                if(record.repairedVertices>0)
                {
                    if(!repaired.TryGetValue(mesh,out var copy))
                    { copy=Object.Instantiate(mesh);copy.name=mesh.name+"_Normalized";copy.boneWeights=weights;VisualAssetLibrary.Folder(folder+"/Meshes");AssetDatabase.CreateAsset(copy,folder+"/Meshes/Skin_"+repaired.Count+".asset");repaired.Add(mesh,copy); }
                    renderer.sharedMesh=copy;
                }
                renderer.quality=SkinQuality.Bone4;report.skins.Add(record);
            }
            report.normalizedMeshes=repaired.Count;
            Sample(actor,model,visual,renderers,report);
            model.GetComponent<CharacterArmPoseSafety>().ConfigureEnvelope(report.torsoWidth,report.torsoDepth);
            File.WriteAllText(folder+"/character-audit.json",JsonUtility.ToJson(report,true));
            return report;
        }
        private static bool Invalid(int index,float weight,int count)=>weight>0 && (index<0 || index>=count);
        private static void Sample(PlayerContext actor,Animator source,Transform visual,SkinnedMeshRenderer[] originals,Report report)
        {
            var clips=source.runtimeAnimatorController.animationClips.Where(c=>c!=null && c.isHumanMotion && c.name!="BreathGame").Distinct().ToArray();
            if(clips.Length==0)throw new InvalidOperationException("Нет Humanoid-клипов для проверки деформаций.");
            Scene preview=EditorSceneManager.NewPreviewScene();GameObject clone=null;PlayableGraph graph=default;Mesh baked=null;
            var measured=new Bounds[originals.Length];var hasBounds=new bool[originals.Length];float width=.08f,depth=.07f;
            try
            {
                clone=Object.Instantiate(visual.gameObject);SceneManager.MoveGameObjectToScene(clone,preview);
                foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
                Matrix4x4 local=actor.transform.worldToLocalMatrix*visual.localToWorldMatrix;
                clone.transform.SetPositionAndRotation(local.GetColumn(3),local.rotation);clone.transform.localScale=local.lossyScale;
                var animator=clone.GetComponentInChildren<Animator>(true);animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                var copies=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r=>AnimationUtility.CalculateTransformPath(r.transform,clone.transform));
                baked=new Mesh{name="SILENCE temporary pose bounds"};
                graph=PlayableGraph.Create("SILENCE character deformation audit");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output=AnimationPlayableOutput.Create(graph,"Avatar",animator);graph.Play();
                for(int c=0;c<clips.Length;c++)
                {
                    if(EditorUtility.DisplayCancelableProgressBar("SILENCE — проверка персонажа",clips[c].name,(float)c/clips.Length))throw new OperationCanceledException("Проверка отменена; исходный мир сохранён.");
                    var playable=AnimationClipPlayable.Create(graph,clips[c]);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);output.SetSourcePlayable(playable);
                    for(int f=0;f<9;f++)
                    {
                        float seconds=clips[c].length*f/8;playable.SetTime(seconds);graph.Evaluate(0);
                        var hip=animator.GetBoneTransform(HumanBodyBones.Hips);var upper=animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
                        var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);var right=animator.GetBoneTransform(HumanBodyBones.RightHand);
                        report.poses.Add(new Pose{clip=clips[c].name,seconds=seconds,leftWrist=left.position,rightWrist=right.position,leftWristFromHips=Vector3.Distance(left.position,hip.position),rightWristFromHips=Vector3.Distance(right.position,hip.position)});
                        for(int r=0;r<originals.Length;r++)
                        {
                            var copy=copies[report.skins[r].renderer];copy.BakeMesh(baked);baked.RecalculateBounds();
                            if(!hasBounds[r]){measured[r]=baked.bounds;hasBounds[r]=true;}else measured[r].Encapsulate(baked.bounds);
                            // Calibrate the envelope only from neutral clothed torso
                            // vertices influenced by actual spine/hips bones, not arms.
                            bool neutral=clips[c].name=="Idle" || clips[c].name.EndsWith("|Idle",StringComparison.Ordinal);
                            if(f!=0 || !neutral || !copy.name.StartsWith("Body_Clothed",StringComparison.Ordinal))continue;
                            var vertices=baked.vertices;var weights=copy.sharedMesh.boneWeights;
                            var torso=new HashSet<int>(copy.bones.Select((b,i)=>new{b,i}).Where(b=>b.b==hip || b.b==upper || b.b==animator.GetBoneTransform(HumanBodyBones.Spine) || b.b==animator.GetBoneTransform(HumanBodyBones.Chest)).Select(b=>b.i));
                            Vector3 up=(upper.position-hip.position).normalized;Quaternion basis=Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward,up),up);Quaternion inverse=Quaternion.Inverse(basis);
                            for(int i=0;i<vertices.Length;i++)
                            {
                                var w=weights[i];float t=(torso.Contains(w.boneIndex0)?w.weight0:0)+(torso.Contains(w.boneIndex1)?w.weight1:0)+(torso.Contains(w.boneIndex2)?w.weight2:0)+(torso.Contains(w.boneIndex3)?w.weight3:0);
                                Vector3 p=inverse*(copy.transform.TransformPoint(vertices[i])-hip.position);
                                if(t>.7f && p.y>.05f && p.y<(upper.position-hip.position).magnitude-.025f){width=Mathf.Max(width,Mathf.Abs(p.x));depth=Mathf.Max(depth,Mathf.Abs(p.z));report.envelopeSampled=true;}
                            }
                        }
                        report.sampledPoses++;
                    }
                    playable.Destroy();report.sampledClips++;
                }
                for(int r=0;r<originals.Length;r++)
                {
                    if(!hasBounds[r])throw new InvalidOperationException("Не получены bounds: "+originals[r].name);
                    var b=measured[r];
                    if(!Finite(b.center) || !Finite(b.size))throw new InvalidOperationException("Неконечные координаты меша/позы: "+originals[r].name);
                    float modelScale=Mathf.Max(.001f,originals[r].transform.lossyScale.magnitude/Mathf.Sqrt(3));
                    if(b.size.magnitude*modelScale>actor.Settings.standingHeight*4)
                        throw new InvalidOperationException("Анимация уводит меш далеко от капсулы: "+originals[r].name+". Сначала подготовь in-place клипы через 25. Исходный мир сохранён.");
                    b.Expand(.10f);originals[r].localBounds=b;report.skins[r].sampledBoundsSize=b.size;
                }
                report.torsoWidth=report.envelopeSampled?Mathf.Clamp(width+.01f,.08f,.30f):.18f;report.torsoDepth=report.envelopeSampled?Mathf.Clamp(depth+.01f,.07f,.25f):.13f;report.nativePoseSampled=true;
            }
            finally
            { EditorUtility.ClearProgressBar();if(graph.IsValid())graph.Destroy();if(baked!=null)Object.DestroyImmediate(baked);if(clone!=null)Object.DestroyImmediate(clone);EditorSceneManager.ClosePreviewScene(preview); }
        }
        private static bool Finite(Vector3 p)=>!float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    }
}
