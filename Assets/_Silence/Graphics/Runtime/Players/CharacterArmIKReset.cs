using UnityEngine;
using Silence.Player;

namespace Silence.Graphics.Players
{
    // Clear legacy hand goals before their owners; never after the flashlight.
    [DisallowMultipleComponent, RequireComponent(typeof(Animator)), DefaultExecutionOrder(675)]
    public sealed class CharacterArmIKReset : MonoBehaviour
    {
        private Animator animator;
        private ICharacterIKCoordinator coordinator;
        private void Awake() { animator=GetComponent<Animator>();coordinator=GetComponent<ICharacterIKCoordinator>(); }
        private void OnAnimatorIK(int layer)
        {
            if (animator == null || !PlayerAnimatorIK.IsFinalPass(animator, layer)) return;
            if(coordinator!=null && coordinator.CoordinatesIK)return;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0); animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0); animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0); animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0);
        }
    }
}
