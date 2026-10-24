namespace Silence.Player
{
    // Allows a body interaction to share the Animator with the existing flashlight IK.
    public interface ILeftHandIKClaim { bool OwnsLeftHand { get; } }
}
