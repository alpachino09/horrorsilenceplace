using System.Collections;
using UnityEngine;
using Silence.Core;
using Silence.Player;
using Silence.Gameplay;

namespace Silence.Audio
{
    public sealed class MicrophoneNoiseMeter : MonoBehaviour
    {
        private PlayerContext actor;
        private AudioClip clip;
        private string device;
        private readonly float[] samples = new float[512];
        private readonly MicrophoneEnvelope envelope = new MicrophoneEnvelope();
        public bool Capturing => clip != null;
        public bool Muted { get; set; }
        public float Sensitivity { get; set; } = 12;
        public float Level => envelope.Level;
        public string Status { get; private set; } = "";
        private bool pending;
        private int generation;
        private void Awake() => actor = GetComponent<PlayerContext>();
        public void EnableWithConsent() { if (!pending && actor.IsLocalOwner && clip == null) StartCoroutine(StartCapture()); }
        private IEnumerator StartCapture()
        {
            pending = true; int request = ++generation;
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (request != generation || !isActiveAndEnabled || !actor.IsLocalOwner) { pending = false; yield break; }
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            { Status = "Microphone permission denied / Нет разрешения на микрофон"; pending = false; yield break; }
            var devices = Microphone.devices;
            if (devices.Length == 0) { Status = "No microphone / Микрофон не найден"; pending = false; yield break; }
            device = devices[0]; clip = Microphone.Start(device, true, 1, 16000);
            Status = clip != null ? device : "Microphone unavailable"; pending = false;
        }
        public void DisableCapture()
        {
            generation++; pending = false;
            if (device != null) Microphone.End(device);
            if (clip != null) Destroy(clip); clip = null; device = null;
        }
        public void Calibrate() { if (clip != null) envelope.Calibrate(MicrophoneEnvelope.Rms(samples)); }
        private void Update()
        {
            if (!actor.IsLocalOwner || clip == null || !Microphone.IsRecording(device)) return;
            int position = Microphone.GetPosition(device);
            if (position < samples.Length || !clip.GetData(samples, position - samples.Length)) return;
            if (Muted || actor.Game == null || !actor.Game.CanAct(actor)) return;
            if (envelope.Sample(MicrophoneEnvelope.Rms(samples), Time.unscaledTimeAsDouble, Sensitivity, out byte metric) &&
                actor.Authority is IGameplayCommands commands)
                commands.RequestAction(actor, ActorAction.Microphone, 0, metric / 255f);
        }
        private void OnDisable() => DisableCapture();
    }
}
