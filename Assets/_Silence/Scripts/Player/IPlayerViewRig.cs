using UnityEngine;

namespace Silence.Player
{
    public interface IPlayerViewRig
    {
        Camera ActiveCamera { get; }
        bool IsFirstPersonCamera(Camera camera);
        bool CameraOverride { get; set; }
        bool ThirdPerson { get; }
        void SetThirdPerson(bool value);
        void FirstPersonPose(out Vector3 position, out Quaternion rotation);
        void CopyCameraSettings(Camera destination);
        void DisableRendering();
    }
}
