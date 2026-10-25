using UnityEngine;
using Silence.Core;

namespace Silence.Player
{
    public static class PlayerAnimatorIK
    {
        // Solve after the last contributing animation layer so upper-body overrides
        // cannot undo the flashlight/interaction hands or the ground contact solution.
        public static int FinalLayer(Animator animator)
        {
            int reaction = animator.GetLayerIndex("Reactions");
            int upper = animator.GetLayerIndex("UpperBody");
            return CharacterIKPassRules.FinalLayer(upper,upper>=0?animator.GetLayerWeight(upper):0,reaction,reaction>=0?animator.GetLayerWeight(reaction):0);
        }
        public static bool IsFinalPass(Animator animator,int layer)=>layer==FinalLayer(animator);
    }
}
