using UnityEngine;

namespace Silence.Player
{
    // References must point into the actual imported model. Markers are empty transforms, not replacement meshes.
    [DisallowMultipleComponent]
    public sealed class FlashlightArtRig : MonoBehaviour
    {
        public Transform rightWristGrip, lensEmitter, leftPalmTap, leftWristCap, batterySocket;
        public Transform cap, oldCells, newCells;
        public Renderer[] emissiveLens;
        [Min(.001f)] public float capTravel = .035f;
        public Vector3 capLocalAxis = Vector3.back;
    }
}
