using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

namespace GlowGlow.Online
{
    public sealed class SteamSession : MonoBehaviour
    {
        public const string Protocol = "glowglow-classic-5";
        public static SteamSession Current { get; private set; }
        public bool Ready { get; private set; }
        public bool IsBusy => busy;
        public string LastConnectionError { get; private set; }
        public CSteamID Lobby { get; private set; }
        public event Action<ulong, bool> LobbyReady;
        public event Action<string> Failed;
        private readonly List<CallResult<LobbyCreated_t>> creations = new();
        private readonly List<CallResult<LobbyEnter_t>> joins = new();
        private uint operation;
        private Callback<GameLobbyJoinRequested_t> invited;
        private Callback<SteamNetConnectionStatusChangedCallback_t> connectionStatus;
        private bool hosting;
        private bool busy;
        private float deadline;
        private string rankedMatch;

        public static SteamSession Get()
        {
            if (Current == null)
                Current = new GameObject("Steam Session").AddComponent<SteamSession>();
            return Current;
        }

        private void Awake()
        {
            if (Current != null && Current != this) { Destroy(gameObject); return; }
            Current = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool Initialize(out string error)
        {
            error = null;
            if (Ready) return true;
            try
            {
                // Development only. Release builds must be launched through their own Steam app.
                if ((Application.isEditor || Debug.isDebugBuild) &&
                    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SteamAppId")))
                {
                    uint configured = RankedMatchmaking.ConfiguredAppId;
                    string appId = configured == 0 ? "480" : configured.ToString();
                    Environment.SetEnvironmentVariable("SteamAppId", appId);
                    Environment.SetEnvironmentVariable("SteamGameId", appId);
                }
                Ready = SteamAPI.Init();
                if (!Ready) { error = "Steam을 실행한 뒤 다시 시도하세요."; return false; }
                if (!Application.isEditor && !Debug.isDebugBuild && SteamUtils.GetAppID().m_AppId == 480)
                {
                    SteamAPI.Shutdown(); Ready = false;
                    error = "출시 빌드에서는 개발용 Steam App ID를 사용할 수 없습니다.";
                    return false;
                }
                invited = Callback<GameLobbyJoinRequested_t>.Create(data =>
                {
                    if (OnlineSession.Current != null && !OnlineSession.Current.HasSession)
                        Join(data.m_steamIDLobby.m_SteamID);
                });
                // Set before sockets are created. Initial route discovery can outlast Steam's default timeout.
                IntPtr timeout = Marshal.AllocHGlobal(sizeof(int));
                try
                {
                    Marshal.WriteInt32(timeout, 45000);
                    SteamNetworkingUtils.SetConfigValue(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
                        ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
                        ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, timeout);
                }
                finally { Marshal.FreeHGlobal(timeout); }
                connectionStatus = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatus);
                SteamNetworkingUtils.InitRelayNetworkAccess();
                return true;
            }
            catch (Exception exception)
            {
                error = "초기화 실패: " + exception.Message;
                return false;
            }
        }

        public bool RelayReady => Ready && SteamNetworkingUtils.GetRelayNetworkStatus(out _) ==
            ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current;

        public void LogRelayStatus()
        {
            if (!Ready) return;
            SteamNetworkingUtils.GetRelayNetworkStatus(out var status);
            Debug.Log($"CLASSIC_STEAM_RELAY availability={status.m_eAvail} config={status.m_eAvailNetworkConfig} relay={status.m_eAvailAnyRelay} detail={status.m_debugMsg}");
        }

        private void OnConnectionStatus(SteamNetConnectionStatusChangedCallback_t data)
        {
            var state = data.m_info.m_eState;
            Debug.Log($"CLASSIC_STEAM_ROUTE state={state} reason={data.m_info.m_eEndReason} detail={data.m_info.m_szEndDebug}");
            if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                LastConnectionError = null;
            else if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally
                || state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer)
            {
                LastConnectionError = data.m_info.m_szEndDebug?.IndexOf("Timed out", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "경로를 찾지 못해 연결 시간이 초과됐습니다."
                    : $"연결이 종료됐습니다. (코드 {data.m_info.m_eEndReason})";
                LogRelayStatus();
            }
        }

        public void Host()
        {
            CreateRoom(ELobbyType.k_ELobbyTypeFriendsOnly, null);
        }
        public void HostRanked(string matchId)
        {
            CreateRoom(ELobbyType.k_ELobbyTypePublic, matchId);
        }
        public void JoinRanked(ulong lobby, string matchId)
        {
            JoinRoom(lobby, matchId);
        }
        public void Join(ulong lobby)
        {
            JoinRoom(lobby, null);
        }
        private void CreateRoom(ELobbyType type, string matchId)
        {
            if (!CanRequestRoom()) return;
            LastConnectionError = null;
            Leave(); rankedMatch = matchId; hosting = true; busy = true; deadline = Time.unscaledTime + 20;
            uint request = operation;
            CallResult<LobbyCreated_t> callback = null;
            callback = CallResult<LobbyCreated_t>.Create((result, failed) =>
            {
                creations.Remove(callback); callback.Dispose();
                if (request == operation) OnCreated(result, failed);
                else if (!failed && result.m_eResult == EResult.k_EResultOK)
                    SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby));
            });
            creations.Add(callback);
            callback.Set(SteamMatchmaking.CreateLobby(type, 2));
        }
        private void JoinRoom(ulong lobby, string matchId)
        {
            if (!CanRequestRoom()) return;
            LastConnectionError = null;
            Leave(); rankedMatch = matchId; hosting = false; busy = true; deadline = Time.unscaledTime + 20;
            uint request = operation;
            CallResult<LobbyEnter_t> callback = null;
            callback = CallResult<LobbyEnter_t>.Create((result, failed) =>
            {
                joins.Remove(callback); callback.Dispose();
                if (request == operation) OnEntered(result, failed);
                else if (!failed && result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess
                    && result.m_ulSteamIDLobby != Lobby.m_SteamID)
                    SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby));
            });
            joins.Add(callback);
            callback.Set(SteamMatchmaking.JoinLobby(new CSteamID(lobby)));
        }
        private bool CanRequestRoom()
        {
            if (creations.Count + joins.Count < 8) return true;
            Failed?.Invoke("요청이 지연되고 있습니다. 잠시 뒤 다시 시도하거나 게임을 재실행해 주세요.");
            return false;
        }
        private void OnCreated(LobbyCreated_t result, bool failed)
        {
            busy = false;
            if (failed || result.m_eResult != EResult.k_EResultOK) { Failed?.Invoke("방 생성에 실패했습니다: " + result.m_eResult); return; }
            Lobby = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(Lobby, "protocol", Protocol);
            SteamMatchmaking.SetLobbyData(Lobby, "host", SteamUser.GetSteamID().m_SteamID.ToString());
            SteamMatchmaking.SetLobbyData(Lobby, "ranked", rankedMatch ?? "");
            LobbyReady?.Invoke(SteamUser.GetSteamID().m_SteamID, true);
        }
        private void OnEntered(LobbyEnter_t result, bool failed)
        {
            busy = false;
            if (failed || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            { Failed?.Invoke("방에 들어갈 수 없습니다. 방 코드와 인원을 확인하세요."); return; }
            Lobby = new CSteamID(result.m_ulSteamIDLobby);
            if (SteamMatchmaking.GetLobbyData(Lobby, "protocol") != Protocol ||
                SteamMatchmaking.GetLobbyData(Lobby, "ranked") != (rankedMatch ?? "") ||
                !ulong.TryParse(SteamMatchmaking.GetLobbyData(Lobby, "host"), out var host) ||
                SteamMatchmaking.GetLobbyOwner(Lobby).m_SteamID != host || host == SteamUser.GetSteamID().m_SteamID)
            { Leave(); Failed?.Invoke("호환되는 상대방의 방이 아닙니다."); return; }
            LobbyReady?.Invoke(host, false);
        }
        public void SetJoinable(bool value)
        {
            if (Ready && hosting && Lobby.m_SteamID != 0) SteamMatchmaking.SetLobbyJoinable(Lobby, value);
        }
        public void Invite()
        {
            if (Ready && Lobby.m_SteamID != 0) SteamFriends.ActivateGameOverlayInviteDialog(Lobby);
        }
        public void Leave()
        {
            // Keep late callbacks alive so a cancelled successful request can leave its orphan lobby.
            operation++; busy = false;
            if (Ready && Lobby.m_SteamID != 0) SteamMatchmaking.LeaveLobby(Lobby);
            Lobby = default; hosting = false; rankedMatch = null;
        }
        private void Update()
        {
            if (Ready) SteamAPI.RunCallbacks();
            if (busy && Time.unscaledTime > deadline) { Leave(); Failed?.Invoke("응답 시간이 초과됐습니다. 다시 시도하세요."); }
        }
        private void OnApplicationQuit()
        {
            OnlineSession.Current?.Disconnect();
            Leave(); invited?.Dispose(); connectionStatus?.Dispose();
            foreach (var callback in creations) callback.Dispose();
            foreach (var callback in joins) callback.Dispose();
            creations.Clear(); joins.Clear();
            if (Ready) SteamAPI.Shutdown();
            Ready = false;
        }
    }
}
