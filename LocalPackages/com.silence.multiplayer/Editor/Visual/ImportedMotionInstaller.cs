using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Silence.Core;
using Silence.Player;
using Silence.Graphics.Players;
using Object=UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class ImportedMotionInstaller
    {
        [Serializable] public sealed class Binding
        {
            public string role,source,clip,previous;
            public float oldStanceSpeed,newStanceSpeed,cadenceRatio;
        }
        [Serializable] public sealed class Report
        {
            public string source=ImportedAssetCatalog.MotionUrl;
            public bool originalClipsModified,nativePlayTested;
            public List<Binding> bindings=new List<Binding>();
            public string[] retainedClips;
        }
        public static void RequireChoices(AnimationClip[] choices)
        {
            if(choices==null || choices.Length!=Enum.GetValues(typeof(ImportedMotion)).Length)throw new InvalidOperationException("Нужен набор движений из окна 28.");
            foreach(var role in new[]{ImportedMotion.Idle,ImportedMotion.Walk,ImportedMotion.Run})
                if(choices[(int)role]==null)throw new InvalidOperationException("Назначь настоящий клип "+role+" из Kevin Iglesias в окне 28.");
            foreach(var clip in choices.Where(c=>c!=null))
                if(clip.legacy || !clip.isHumanMotion || clip.length<=.05f)throw new InvalidOperationException("Не Humanoid-клип: "+clip.name+". Проверь его Rig/Avatar.");
        }
        public static Report Apply(GameObject player,AnimationClip[] choices,string folder)
        {
            RequireChoices(choices);var actor=player.GetComponent<PlayerContext>();var visual=player.GetComponent<PlayerAvatarPresentation>();
            if(actor==null || visual==null || visual.Model==null || visual.AnimationProfile==null)throw new InvalidOperationException("В текущем мире нужен уже настроенный анимированный игрок (21/25/27).");
            var animator=visual.Model;var originalController=animator.runtimeAnimatorController as AnimatorController;
            if(originalController==null || !animator.isHuman || !animator.avatar.isValid)throw new InvalidOperationException("Нужен существующий Humanoid AnimatorController игрока.");
            string path=folder+"/ImportedMotion.controller";
            if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(originalController),path))throw new IOException("Не скопирован текущий AnimatorController.");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);VisualAssetLibrary.Folder(folder+"/Clips");
            void RequireOwnedStates(AnimatorStateMachine machine)
            {
                if(AssetDatabase.GetAssetPath(machine)!=path || machine.states.Any(s=>AssetDatabase.GetAssetPath(s.state)!=path))
                    throw new InvalidOperationException("В controller есть внешняя state machine. Требуется её явное копирование; исходные состояния не изменены.");
                foreach(var child in machine.stateMachines)RequireOwnedStates(child.stateMachine);
            }
            foreach(var layer in controller.layers)RequireOwnedStates(layer.stateMachine);
            var report=new Report();var replacements=new Dictionary<AnimationClip,AnimationClip>();var ratios=new Dictionary<AnimationClip,float>();
            var prepared=new Dictionary<ImportedMotion,AnimationClip>();var measurements=new Dictionary<ImportedMotion,AnimationClipPreparation.Measurements>();
            var usedRoles=new HashSet<ImportedMotion>();var retained=new List<string>();
            foreach(var previous in controller.animationClips.Distinct())
            {
                var role=ImportedAssetModel.Motion(previous.name);
                if(!role.HasValue || choices[(int)role.Value]==null){retained.Add(AssetDatabase.GetAssetPath(previous)+" | "+previous.name);continue;}
                bool moving=ImportedAssetModel.Moving(role.Value);
                if(!prepared.TryGetValue(role.Value,out var next))
                {
                    var selected=choices[(int)role.Value];
                    if(EditorUtility.DisplayCancelableProgressBar("SILENCE — Human Basic Motions",role.Value+": "+selected.name,(float)prepared.Count/choices.Length))throw new OperationCanceledException("Установка отменена; исходный мир сохранён.");
                    next=AnimationClipPreparation.Clone(selected,animator,folder+"/Clips/"+role.Value+".anim",true,moving,out var measured);
                    next.name="Imported_"+role.Value;EditorUtility.SetDirty(next);prepared.Add(role.Value,next);measurements.Add(role.Value,measured);
                }
                float oldSpeed=0,newSpeed=measurements[role.Value].measuredStanceSpeed,ratio=1;
                if(moving){oldSpeed=AnimationClipPreparation.MeasureCopy(previous,animator,true).measuredStanceSpeed;ratio=ImportedAssetModel.CadenceRatio(oldSpeed,newSpeed);}
                replacements.Add(previous,next);ratios.Add(previous,ratio);usedRoles.Add(role.Value);
                report.bindings.Add(new Binding{role=role.Value.ToString(),source=AssetDatabase.GetAssetPath(choices[(int)role.Value]),clip=choices[(int)role.Value].name,
                    previous=AssetDatabase.GetAssetPath(previous),oldStanceSpeed=oldSpeed,newStanceSpeed=newSpeed,cadenceRatio=ratio});
            }
            foreach(var role in new[]{ImportedMotion.Idle,ImportedMotion.Walk,ImportedMotion.Run})
                if(!usedRoles.Contains(role))throw new InvalidOperationException("В текущем controller не найдено заменяемое движение "+role+". Исходный мир сохранён.");
            var trees=new Dictionary<BlendTree,BlendTree>();
            Motion Rewrite(Motion motion,out float ratio)
            {
                ratio=1;
                if(motion is AnimationClip clip)
                {if(replacements.TryGetValue(clip,out var replacement)){ratio=ratios[clip];return replacement;}return clip;}
                if(!(motion is BlendTree tree))return motion;
                if(trees.TryGetValue(tree,out var cached))return cached;
                var copy=AssetDatabase.GetAssetPath(tree)==path?tree:Object.Instantiate(tree);
                if(copy!=tree)AssetDatabase.AddObjectToAsset(copy,controller);trees.Add(tree,copy);
                var children=copy.children;
                for(int i=0;i<children.Length;i++){children[i].motion=Rewrite(children[i].motion,out float rate);children[i].timeScale*=rate;}
                copy.children=children;EditorUtility.SetDirty(copy);return copy;
            }
            void RewriteStates(AnimatorStateMachine machine)
            {
                foreach(var state in machine.states)
                {var s=state.state;s.motion=Rewrite(s.motion,out float rate);s.speed*=rate;EditorUtility.SetDirty(s);}
                foreach(var child in machine.stateMachines)RewriteStates(child.stateMachine);
            }
            foreach(var layer in controller.layers)RewriteStates(layer.stateMachine);
            var profile=Object.Instantiate(visual.AnimationProfile);profile.name="Imported Human Basic Motions";profile.controller=controller;
            profile.provenance=ImportedAssetCatalog.MotionUrl+"; selected local Humanoid recordings. Existing controller states, parameters, fallback actions and motor kept. Cadence measured on this Avatar; Play/co-op acceptance pending.";
            AssetDatabase.CreateAsset(profile,folder+"/ImportedMotion.asset");animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            visual.ConfigureAnimation(profile,visual.FacingRoot);
            (animator.GetComponent<CharacterIKController>() ?? animator.gameObject.AddComponent<CharacterIKController>()).Configure(actor,profile);
            (animator.GetComponent<CharacterAnimationActions>() ?? animator.gameObject.AddComponent<CharacterAnimationActions>()).Configure(actor,profile);
            PlayerViewWorkshop.ConfigurePlayer(player,folder);CharacterReworkPreparation.Prepare(player,folder);
            report.retainedClips=retained.ToArray();EditorUtility.SetDirty(controller);EditorUtility.SetDirty(profile);
            File.WriteAllText(folder+"/imported-motion-report.json",JsonUtility.ToJson(report,true));return report;
        }
    }
}
