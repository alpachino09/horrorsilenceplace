using UnityEngine;

namespace Silence.Player
{
    // An animation target may be unreachable or inside the torso. Presentation
    // consumers use the same constraint; gameplay positions are never changed.
    public interface IArmPoseConstraints
    {
        bool ConstrainArm(bool left, Vector3 requested, out Vector3 hand, out Vector3 elbow);
    }
}
