using System;
using System.Collections;
using System.Net;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Silence.Core;
using Silence.Player;
using Silence.UI;

namespace Silence.Multiplayer
{
    [DefaultExecutionOrder(-800)]
    public sealed class NetworkSession : MonoBehaviour
    {
        [SerializeField] private NetworkManager manager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private NetworkWorldAuthority world;
        [SerializeField] private Camera lobbyCamera;
        public static NetworkSession Instance { get; private set; }
        public SessionAdmission Admission { get; private set; } = new SessionAdmission();
        public NetworkWorldAuthority World => world;
        public NetworkPlayer LocalPlayer { get; private set; }
        public NetworkManager Manager => manager;
        public string Status { get; private set; } = "";
        public bool Returning { get; private set; }
        private string address = "127.0.0.1", portText = "7777";
        private bool attempted, listenLan;
        private float attemptAt;
        private static string lastStatus;

        public void Configure(NetworkManager network, UnityTransport utp, NetworkWorldAuthority authority, Camera camera)
        { manager = network; transport = utp; world = authority; lobbyCamera = camera; }

        private void Awake()
        {
            if (Instance != null && Instance != this) throw new InvalidOperationException("One NetworkSession is allowed per scene.");
            Instance = this;
            Application.runInBackground = true;
            Status = lastStatus ?? "";
            lastStatus = null;
            manager.ConnectionApprovalCallback = Approve;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.OnTransportFailure += TransportFailed;
            manager.OnClientStopped += ClientStopped;
            PlayerLook.Capture(false);
        }

        public static Vector3 SpawnPosition(int slot) => new Vector3(-2.25f + slot * 1.5f, .12f, -11f);

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string protocol = request.Payload != null && request.Payload.Length <= 64
                ? Encoding.UTF8.GetString(request.Payload) : "";
            response.Approved = Admission.TryJoin(request.ClientNetworkId, protocol, out int slot, out string reason);
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            response.Reason = reason;
            if (response.Approved)
            {
                response.Position = SpawnPosition(slot);
                response.Rotation = Quaternion.identity;
            }
        }

        public bool StartConnection(bool host, string ip, ushort port, bool lan = false)
        {
            if (attempted || manager.IsListening || manager.ShutdownInProgress || Returning) return false;
            if (!IPAddress.TryParse(ip, out var parsed) ||
                parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || port == 0)
            { Status = Text("Укажите IP-адрес и порт от 1 до 65535.", "Enter an IP address and port 1–65535."); return false; }
            address = ip;
            manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(SessionAdmission.Protocol);
            transport.SetConnectionData(ip, port, host ? (lan ? "0.0.0.0" : "127.0.0.1") : null);
            attempted = true;
            attemptAt = Time.unscaledTime;
            Status = Text("Подключение…", "Connecting…");
            bool started = host ? manager.StartHost() : manager.StartClient();
            if (!started) ReturnToLobby(Text("Не удалось запустить соединение.", "Connection could not start."));
            return started;
        }

        internal void SetLocalPlayer(NetworkPlayer player)
        {
            LocalPlayer = player;
            lobbyCamera.enabled = false;
            var listener = lobbyCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            Status = "";
        }
        private void Update()
        {
            if (attempted && LocalPlayer == null && !Returning && Time.unscaledTime - attemptAt > 20f)
                ReturnToLobby(Text("Время подключения истекло.", "Connection timed out."));
        }
        private void Disconnected(ulong id)
        {
            if (manager.IsServer) Admission.Remove(id);
            if (!manager.IsServer && id == manager.LocalClientId)
                ReturnToLobby(string.IsNullOrEmpty(manager.DisconnectReason)
                    ? Text("Соединение закрыто.", "Connection closed.") : manager.DisconnectReason);
        }
        private void TransportFailed() => ReturnToLobby(Text("Ошибка сетевого транспорта.", "Network transport failed."));
        private void ClientStopped(bool wasHost)
        {
            if (attempted && !Returning) ReturnToLobby(string.IsNullOrEmpty(manager.DisconnectReason)
                ? Text("Соединение закрыто.", "Connection closed.") : manager.DisconnectReason);
        }
        public void ReturnToLobby(string reason)
        {
            if (Returning) return;
            Returning = true;
            lastStatus = reason;
            PlayerLook.Capture(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GetComponent<NetworkSmokeProbe>()?.Abort(reason);
#endif
            StartCoroutine(ShutdownAndReload());
        }
        private IEnumerator ShutdownAndReload()
        {
            if (manager.IsListening || manager.IsClient || manager.IsServer) manager.Shutdown();
            while (manager.ShutdownInProgress) yield return null;
            // NetworkManager persists across scenes; remove it before loading a clean lobby.
            if (manager != null) Destroy(manager.gameObject);
            yield return null;
            SceneManager.LoadSceneAsync(gameObject.scene.path, LoadSceneMode.Single);
        }
        private string Text(string ru, string en) => PrototypePreferences.English ? en : ru;

        private void OnGUI()
        {
            if (Returning) { GUI.Box(new Rect(20, 20, 430, 55), Text("Возвращение в лобби…", "Returning to lobby…")); return; }
            if (world.Running && LocalPlayer != null)
            {
                if (!LocalPlayer.Context.Input.GameplayActive && GUI.Button(new Rect(20, Screen.height - 65, 240, 40),
                    Text("Выйти в сетевое лобби", "Leave to network lobby")))
                    ReturnToLobby(Text("Вы вышли из сессии.", "You left the session."));
                return;
            }
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 260, Screen.height / 2f - 190, 520, 380), GUI.skin.box);
            GUILayout.Label("SILENCE — " + Text("кооператив · до 4 игроков", "co-op · up to 4 players"));
            if (!attempted)
            {
                GUILayout.Label(Text("IP хоста (для одного компьютера: 127.0.0.1)", "Host IP (same computer: 127.0.0.1)"));
                address = GUILayout.TextField(address, 64);
                GUILayout.Label(Text("Порт", "Port"));
                portText = GUILayout.TextField(portText, 5);
                listenLan = GUILayout.Toggle(listenLan, Text("Разрешить подключение из локальной сети", "Allow LAN connections"));
                if (GUILayout.Button(Text("Создать сессию", "Host"))) ConnectFromMenu(true);
                if (GUILayout.Button(Text("Подключиться", "Join"))) ConnectFromMenu(false);
            }
            else if (LocalPlayer == null) GUILayout.Label(Text("Ожидание подключения…", "Waiting for connection…"));
            else
            {
                GUILayout.Label(Text("Игроков: ", "Players: ") + world.PlayerCount + "/4");
                GUILayout.Label(Text("Соберите группу до начала. Во время раунда вход закрыт.",
                    "Gather your group first. Joining is closed during a round."));
                if (manager.IsHost && GUILayout.Button(Text("Начать раунд", "Start round"))) world.BeginRound();
                else if (!manager.IsHost) GUILayout.Label(Text("Ожидаем старта от хоста.", "Waiting for host to start."));
            }
            if (attempted && GUILayout.Button(Text("Отменить / выйти", "Cancel / leave")))
                ReturnToLobby(Text("Вы вышли из сессии.", "You left the session."));
            if (GUILayout.Button(Text("Язык: русский", "Language: English"))) PrototypePreferences.English = !PrototypePreferences.English;
            GUILayout.Label(Status);
            GUILayout.EndArea();
        }
        private void ConnectFromMenu(bool host)
        {
            if (!ushort.TryParse(portText, out ushort port) || port == 0)
            { Status = Text("Неверный порт.", "Invalid port."); return; }
            StartConnection(host, address.Trim(), port, listenLan);
        }
        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.ConnectionApprovalCallback = null;
                manager.OnClientDisconnectCallback -= Disconnected;
                manager.OnTransportFailure -= TransportFailed;
                manager.OnClientStopped -= ClientStopped;
            }
            if (Instance == this) Instance = null;
        }
    }
}
