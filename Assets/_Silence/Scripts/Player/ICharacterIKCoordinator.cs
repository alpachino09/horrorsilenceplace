namespace Silence.Player
{
    // One Animator callback owns the explicit order of all IK contributors.
    public interface ICharacterIKCoordinator
    {
        bool CoordinatesIK { get; }
    }
}
