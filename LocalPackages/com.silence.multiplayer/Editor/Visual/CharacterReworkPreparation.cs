using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Animations;
using UnityEngine.SceneManagement;
using Silence.Player;
using Silence.Core;
using Silence.Graphics.Players;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class CharacterReworkPreparation
    {
        [Serializable] public sealed class Report
        {
            public string kind="Unity Editor geometry/pose measurements; native Play acceptance is separate";
            public string samplingMode="Isolated AnimatorController, applyRootMotion=false, reset between clips; bind-pose CPU skinning in actor space";
            public int opaqueMaterials, normalizedMeshes, sampledClips, sampledPoses;
            public bool nativePoseSampled, envelopeSampled, nativePlayTested;
            public float torsoWidth, torsoDepth;
            public List<string> preservedMaterials=new List<string>();
            public List<Skin> skins=new List<Skin>();
            public List<Pose> poses=new List<Pose>();
        }
        [Serializable] public sealed class Skin { public string renderer, mesh; public int vertices, repairedVertices; public Vector3 sampledBoundsSize, maximumPoseActorSize, restActorSize; }
        [Serializable] public sealed class Pose { public string clip; public float seconds, leftWristFromHips, rightWristFromHips; public Vector3 leftWrist, rightWrist; }
        [Serializable] public sealed class Failure
        {
            public string version="0.4.7", scene, clip, clipPath, renderer, message, avatarPath, nativeBakeError;
            public string measurement="sum(weight * bone.localToWorld * bindpose * shapedVertex); preview world = actor space";
            public float seconds, expectedHeight, planarHipTravel, humanScale;
            public Vector3 localBoundsSize, actorBoundsSize, restActorBoundsSize, rendererScale, nativeBakeLocalSize, nativeBakeTransformedSize;
            public List<BoneMeasure> bones=new List<BoneMeasure>();
        }
        [Serializable] public sealed class BoneMeasure { public string bone;public float restParentDistance,poseParentDistance;public Vector3 restScale,poseScale; }
        private sealed class Rest
        {
            public Transform transform;public Vector3 position,scale;public Quaternion rotation;
            public Rest(Transform t){transform=t;position=t.localPosition;rotation=t.localRotation;scale=t.localScale;}
            public void Restore(){transform.localPosition=position;transform.localRotation=rotation;transform.localScale=scale;}
        }
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
            Sample(actor,model,visual,renderers,report,folder);
            model.GetComponent<CharacterArmPoseSafety>().ConfigureEnvelope(report.torsoWidth,report.torsoDepth);
            File.WriteAllText(folder+"/character-audit.json",JsonUtility.ToJson(report,true));
            return report;
        }
        private static bool Invalid(int index,float weight,int count)=>weight>0 && (index<0 || index>=count);
        public static Bounds ToActorBounds(Bounds bounds,Matrix4x4 matrix)
        {
            RigVector V(Vector4 v)=>new RigVector(v.x,v.y,v.z);
            var e=SkinBoundsModel.TransformExtents(new RigVector(bounds.extents.x,bounds.extents.y,bounds.extents.z),V(matrix.GetColumn(0)),V(matrix.GetColumn(1)),V(matrix.GetColumn(2)));
            return new Bounds(matrix.MultiplyPoint3x4(bounds.center),new Vector3(e.X,e.Y,e.Z)*2);
        }
        private static void Sample(PlayerContext actor,Animator source,Transform visual,SkinnedMeshRenderer[] originals,Report report,string folder)
        {
            var clips=source.runtimeAnimatorController.animationClips.Where(c=>c!=null && c.isHumanMotion && c.name!="BreathGame").Distinct().ToArray();
            if(clips.Length==0)throw new InvalidOperationException("Нет Humanoid-клипов для проверки деформаций.");
            Scene preview=EditorSceneManager.NewPreviewScene();GameObject clone=null;
            string controllerPath=folder+"/__PoseSampling.controller";
            var measured=new Bounds[originals.Length];var hasBounds=new bool[originals.Length];float width=.08f,depth=.07f;
            var context=new Failure{scene=SceneManager.GetActiveScene().path,expectedHeight=actor.Settings.standingHeight};
            try
            {
#if UNITY_6000_6_OR_NEWER
                clone=Object.Instantiate(visual.gameObject,new InstantiateParameters{scene=preview,worldSpace=false});
#else
                clone=Object.Instantiate(visual.gameObject);SceneManager.MoveGameObjectToScene(clone,preview);
#endif
                foreach(var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
                Matrix4x4 local=actor.transform.worldToLocalMatrix*visual.localToWorldMatrix;
                clone.transform.SetPositionAndRotation(local.GetColumn(3),local.rotation);clone.transform.localScale=local.lossyScale;
                var animator=clone.GetComponentInChildren<Animator>(true);animator.runtimeAnimatorController=null;animator.applyRootMotion=false;
                animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                var rest=clone.GetComponentsInChildren<Transform>(true).Select(t=>new Rest(t)).ToArray();
                var copies=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r=>AnimationUtility.CalculateTransformPath(r.transform,clone.transform));
                var samplers=report.skins.Select(s=>new SkinPoseSampler(copies[s.renderer])).ToArray();
                for(int r=0;r<samplers.Length;r++){context.renderer=report.skins[r].renderer;samplers[r].Sample(Matrix4x4.identity);report.skins[r].restActorSize=samplers[r].ActorBounds.size;}
                context.humanScale=animator.humanScale;context.avatarPath=AssetDatabase.GetAssetPath(animator.avatar);
                var tracked=new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,
                    HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
                    HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
                    HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot};
                var trackedBones=tracked.Select(animator.GetBoneTransform).Where(t=>t!=null && t.parent!=null).ToArray();
                foreach(var bone in trackedBones)context.bones.Add(new BoneMeasure{bone=bone.name,restParentDistance=Vector3.Distance(bone.position,bone.parent.position),restScale=bone.lossyScale});
                // Match gameplay's controller root-motion processing. Raw clip
                // Playables and state retained from a preceding clip are not used.
                var controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var originalController=source.runtimeAnimatorController as AnimatorController;
                if(originalController==null)throw new InvalidOperationException("Нужен сохранённый AnimatorController для проверки поз.");
                foreach(var parameter in originalController.parameters)controller.AddParameter(parameter.name,parameter.type);
                var machine=controller.layers[0].stateMachine;var state=machine.AddState("AuditPose");machine.defaultState=state;state.iKOnFeet=false;
                int stateHash=Animator.StringToHash("Base Layer.AuditPose");
                for(int c=0;c<clips.Length;c++)
                {
                    context.clip=clips[c].name;context.clipPath=AssetDatabase.GetAssetPath(clips[c]);context.renderer=null;context.planarHipTravel=0;
                    if(EditorUtility.DisplayCancelableProgressBar("SILENCE — проверка персонажа",clips[c].name,(float)c/clips.Length))throw new OperationCanceledException("Проверка отменена; исходный мир сохранён.");
                    animator.runtimeAnimatorController=null;
                    foreach(var pose in rest)pose.Restore();
                    state.motion=clips[c];EditorUtility.SetDirty(state);EditorUtility.SetDirty(controller);animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.Rebind();
                    Vector3 firstHip=Vector3.zero;bool loop=AnimationUtility.GetAnimationClipSettings(clips[c]).loopTime;
                    for(int f=0;f<9;f++)
                    {
                        context.seconds=clips[c].length*f/8;context.renderer=null;context.localBoundsSize=context.actorBoundsSize=Vector3.zero;
                        animator.Play(stateHash,0,f/8f);animator.Update(0);
                        if(animator.GetCurrentAnimatorStateInfo(0).fullPathHash!=stateHash)throw new InvalidOperationException("Не запущено изолированное состояние проверки: "+clips[c].name);
                        for(int b=0;b<trackedBones.Length;b++){var bone=trackedBones[b];context.bones[b].poseParentDistance=Vector3.Distance(bone.position,bone.parent.position);context.bones[b].poseScale=bone.lossyScale;}
                        var hip=animator.GetBoneTransform(HumanBodyBones.Hips);var upper=animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
                        var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);var right=animator.GetBoneTransform(HumanBodyBones.RightHand);
                        if(!Finite(hip.position))throw new InvalidOperationException("Некорректная позиция Hips: "+clips[c].name);
                        if(f==0)firstHip=hip.position;
                        context.planarHipTravel=Mathf.Max(context.planarHipTravel,Vector3.ProjectOnPlane(hip.position-firstHip,Vector3.up).magnitude);
                        if(loop && context.planarHipTravel>.5f)throw new InvalidOperationException("Измерено движение Hips в цикле "+clips[c].name+": "+context.planarHipTravel.ToString("F3")+" м. Нужна проверка root motion именно этого клипа; исходный мир сохранён.");
                        report.poses.Add(new Pose{clip=clips[c].name,seconds=context.seconds,leftWrist=left.position,rightWrist=right.position,leftWristFromHips=Vector3.Distance(left.position,hip.position),rightWristFromHips=Vector3.Distance(right.position,hip.position)});
                        for(int r=0;r<originals.Length;r++)
                        {
                            var copy=copies[report.skins[r].renderer];context.renderer=report.skins[r].renderer;
                            context.restActorBoundsSize=report.skins[r].restActorSize;
                            var sampler=samplers[r];sampler.Sample(Matrix4x4.identity);
                            var bounds=sampler.LocalBounds;var actorBounds=sampler.ActorBounds;
                            context.localBoundsSize=bounds.size;context.actorBoundsSize=actorBounds.size;context.rendererScale=copy.transform.lossyScale;
                            if(!Finite(bounds.center) || !Finite(bounds.size) || !Finite(actorBounds.center) || !Finite(actorBounds.size))throw new InvalidOperationException("Неконечная геометрия в "+clips[c].name+", "+copy.name);
                            if(actorBounds.size.magnitude>actor.Settings.standingHeight*4)
                            {
                                CompareNativeBake(copy,context);
                                throw new InvalidOperationException("Большая поза подтверждена по костям и bind poses: "+clips[c].name+", t="+context.seconds.ToString("F3")+", "+copy.name+", размер в метрах "+actorBounds.size+", Avatar humanScale="+context.humanScale.ToString("F4")+". Исходный мир сохранён.");
                            }
                            report.skins[r].maximumPoseActorSize=Vector3.Max(report.skins[r].maximumPoseActorSize,actorBounds.size);
                            if(!hasBounds[r]){measured[r]=bounds;hasBounds[r]=true;}else measured[r].Encapsulate(bounds);
                            bool neutral=clips[c].name=="Idle" || clips[c].name=="Basic_Idle" || clips[c].name.EndsWith("|Idle",StringComparison.Ordinal);
                            if(f!=0 || !neutral || !copy.name.StartsWith("Body_Clothed",StringComparison.Ordinal))continue;
                            var vertices=sampler.ActorVertices;var weights=copy.sharedMesh.boneWeights;
                            var torso=new HashSet<int>(copy.bones.Select((b,i)=>new{b,i}).Where(b=>b.b==hip || b.b==upper || b.b==animator.GetBoneTransform(HumanBodyBones.Spine) || b.b==animator.GetBoneTransform(HumanBodyBones.Chest)).Select(b=>b.i));
                            Vector3 up=(upper.position-hip.position).normalized;Quaternion basis=Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward,up),up);Quaternion inverse=Quaternion.Inverse(basis);
                            for(int i=0;i<vertices.Length;i++)
                            {
                                var w=weights[i];float t=(torso.Contains(w.boneIndex0)?w.weight0:0)+(torso.Contains(w.boneIndex1)?w.weight1:0)+(torso.Contains(w.boneIndex2)?w.weight2:0)+(torso.Contains(w.boneIndex3)?w.weight3:0);
                                Vector3 p=inverse*(vertices[i]-hip.position);
                                if(t>.7f && p.y>.05f && p.y<(upper.position-hip.position).magnitude-.025f){width=Mathf.Max(width,Mathf.Abs(p.x));depth=Mathf.Max(depth,Mathf.Abs(p.z));report.envelopeSampled=true;}
                            }
                        }
                        report.sampledPoses++;
                    }
                    report.sampledClips++;
                }
                for(int r=0;r<originals.Length;r++)
                {
                    if(!hasBounds[r])throw new InvalidOperationException("Не получены bounds: "+originals[r].name);
                    // This union is for renderer culling only. Its size is not a
                    // measurement of planar body travel or a single deformed pose.
                    var bounds=measured[r];
                    var padding=ToActorBounds(new Bounds(Vector3.zero,Vector3.one*.10f),originals[r].transform.worldToLocalMatrix*actor.transform.localToWorldMatrix);
                    bounds.Expand(padding.size);originals[r].localBounds=bounds;report.skins[r].sampledBoundsSize=bounds.size;
                }
                report.torsoWidth=report.envelopeSampled?Mathf.Clamp(width+.01f,.08f,.30f):.18f;report.torsoDepth=report.envelopeSampled?Mathf.Clamp(depth+.01f,.07f,.25f):.13f;report.nativePoseSampled=true;
            }
            catch(Exception error)
            {
                context.message=error.Message;
                try{Directory.CreateDirectory("Library/Silence");string json=JsonUtility.ToJson(context,true);File.WriteAllText("Library/Silence/CharacterReworkFailure.json",json);Debug.Log("SILENCE CharacterRework diagnostics — скопируй эту запись целиком:\n"+json);}
                catch(Exception io){Debug.LogWarning("Не записана диагностика: "+io.Message);}
                throw new InvalidOperationException(error.Message+" Диагностика: Library/Silence/CharacterReworkFailure.json",error);
            }
            finally
            {
                EditorUtility.ClearProgressBar();if(clone!=null)Object.DestroyImmediate(clone);
                AssetDatabase.DeleteAsset(controllerPath);EditorSceneManager.ClosePreviewScene(preview);
            }
        }
        private static void CompareNativeBake(SkinnedMeshRenderer renderer,Failure failure)
        {
            // A comparison only: native BakeMesh output never controls bounds
            // or bypasses a genuine deformation detected by bind-pose skinning.
            var mesh=new Mesh();
            try{renderer.BakeMesh(mesh,false);mesh.RecalculateBounds();failure.nativeBakeLocalSize=mesh.bounds.size;failure.nativeBakeTransformedSize=ToActorBounds(mesh.bounds,renderer.transform.localToWorldMatrix).size;}
            catch(Exception e){failure.nativeBakeError=e.Message;}
            finally{Object.DestroyImmediate(mesh);}
        }
        private static bool Finite(Vector3 p)=>!float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    }
}
