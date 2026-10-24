using UnityEngine;
using Silence.Core;

namespace Silence.Graphics.Players
{
    public static class PlayerFootContacts
    {
        public static float Read(Animator animator, bool left)
        {
            var current = animator.GetCurrentAnimatorStateInfo(0);
            bool currentIdle = current.IsTag("PlantedIdle");
            // State mirroring swaps Mecanim's legs, but arbitrary custom float curves
            // are not Humanoid muscles. Swap their contact roles explicitly for pivots.
            if ((current.IsName("Base Layer.PivotWalk") || current.IsName("Base Layer.PivotRun")) && animator.GetBool("TurnMirrored")) left = !left;
            float contribution = 1;
            if (animator.IsInTransition(0))
            {
                bool nextIdle = animator.GetNextAnimatorStateInfo(0).IsTag("PlantedIdle");
                if (currentIdle != nextIdle)
                { float phase = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime); contribution = currentIdle ? phase : 1 - phase; }
            }
            return FootContactModel.RemoveIdleBlend(animator.GetFloat(left ? "LeftFootPlant" : "RightFootPlant"), contribution);
        }
    }
}
