using UnityEngine;

namespace Silence.Player
{
    [CreateAssetMenu(menuName="SILENCE/Tactical Flashlight Profile")]
    public sealed class FlashlightProfile : ScriptableObject
    {
        public GameObject modelPrefab;
        public Texture2D cookie;
        [Min(0)] public float intensity = 1.8f;
        [Range(5,40)] public float range = 22;
        [Range(20,70)] public float outerAngle = 46;
        [Range(.2f,.9f)] public float innerRatio = .58f;
        public Color lightColor = new Color(1,.93f,.81f);
        public Color lensEmission = new Color(2.5f,2.3f,2);
        [Range(.02f,.5f)] public float lowChargeThreshold = .20f;
        [Range(.01f,.3f)] public float criticalChargeThreshold = .10f;
        public Vector3 readyPosition = new Vector3(.22f,-.19f,.46f);
        public Vector3 aimEuler;
        public Vector3 leftPalmWristToContact = new Vector3(0,.045f,.025f);
        [Range(0,1)] public float fingerGrip = .62f;
        public AudioClip buttonClick, palmTap, unscrewCap, removeCells, insertCells, screwCap, equip, holster;
        [Range(0,1)] public float soundVolume = .35f;
        [TextArea] public string calibrationNotes;
    }
}
