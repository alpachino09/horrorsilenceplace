using UnityEngine;

namespace Silence.Player
{
    public static class PlayerAnimatorIK
    {
        // Solve after the last contributing animation layer so upper-body overrides
        // cannot undo the flashlight/interaction hands or the ground contact solution.
        public static bool IsFinalPass(Animator animator, int layer)
        {
            int reaction = animator.GetLayerIndex("Reactions");
            if (reaction >= 0 && animator.GetLayerWeight(reaction) > .001f) return layer == reaction;
            int upper = animator.GetLayerIndex("UpperBody");
            if (upper >= 0 && animator.GetLayerWeight(upper) > .001f) return layer == upper;
            return layer == 0;
        }
    }
}
