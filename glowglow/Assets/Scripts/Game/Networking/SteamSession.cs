using System;
using Steamworks;
using UnityEngine;

namespace GlowGlow.Online
{
    public sealed class SteamSession : MonoBehaviour
    {
        public const string Protocol = "glowglow-classic-1";
        public static SteamSession Current { get; private set; }
        public bool Ready { get; private set; }
        public CSteamID Lobby { get; private set; }
        public event Action<ulong, bool> LobbyReady;
        public event Action<string> Failed;
        private CallResult<LobbyCreated_t> created;
        private CallResult<LobbyEnter_t> entered;
        private Callback<GameLobbyJoinRequested_t> invited;
        private bool hosting;
        private bool busy;
        private float deadline;

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
                    Environment.SetEnvironmentVariable("SteamAppId", "480");
                    Environment.SetEnvironmentVariable("SteamGameId", "480");
                }
                Ready = SteamAPI.Init();
                if (!Ready) { error = "Steam을 실행한 뒤 다시 시도하세요. 출시 빌드는 Steam 앱 등록이 필요합니다."; return false; }
                if (!Application.isEditor && !Debug.isDebugBuild && SteamUtils.GetAppID().m_AppId == 480)
                {
                    SteamAPI.Shutdown(); Ready = false;
                    error = "출시 빌드에서는 개발용 Steam App ID를 사용할 수 없습니다.";
                    return false;
                }
                created = CallResult<LobbyCreated_t>.Create(OnCreated);
                entered = CallResult<LobbyEnter_t>.Create(OnEntered);
                invited = Callback<GameLobbyJoinRequested_t>.Create(data => Join(data.m_steamIDLobby.m_SteamID));
                SteamNetworkingUtils.InitRelayNetworkAccess();
                return true;
            }
            catch (Exception exception)
            {
                error = "Steam 초기화 실패: " + exception.Message;
                return false;
            }
        }

        public void Host()
        {
            Leave(); hosting = true; busy = true; deadline = Time.unscaledTime + 20;
            created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 2));
        }
        public void Join(ulong lobby)
        {
            Leave(); hosting = false; busy = true; deadline = Time.unscaledTime + 20;
            entered.Set(SteamMatchmaking.JoinLobby(new CSteamID(lobby)));
        }
        private void OnCreated(LobbyCreated_t result, bool failed)
        {
            busy = false;
            if (failed || result.m_eResult != EResult.k_EResultOK) { Failed?.Invoke("방 생성에 실패했습니다: " + result.m_eResult); return; }
            Lobby = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(Lobby, "protocol", Protocol);
            SteamMatchmaking.SetLobbyData(Lobby, "host", SteamUser.GetSteamID().m_SteamID.ToString());
            LobbyReady?.Invoke(SteamUser.GetSteamID().m_SteamID, true);
        }
        private void OnEntered(LobbyEnter_t result, bool failed)
        {
            busy = false;
            if (failed || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            { Failed?.Invoke("방에 들어갈 수 없습니다. 방 코드와 인원을 확인하세요."); return; }
            Lobby = new CSteamID(result.m_ulSteamIDLobby);
            if (SteamMatchmaking.GetLobbyData(Lobby, "protocol") != Protocol ||
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
            created?.Cancel(); entered?.Cancel(); busy = false;
            if (Ready && Lobby.m_SteamID != 0) SteamMatchmaking.LeaveLobby(Lobby);
            Lobby = default; hosting = false;
        }
        private void Update()
        {
            if (Ready) SteamAPI.RunCallbacks();
            if (busy && Time.unscaledTime > deadline) { Leave(); Failed?.Invoke("Steam 응답 시간이 초과됐습니다. 다시 시도하세요."); }
        }
        private void OnApplicationQuit()
        {
            OnlineSession.Current?.Disconnect();
            Leave(); invited?.Dispose(); created?.Dispose(); entered?.Dispose();
            if (Ready) SteamAPI.Shutdown();
            Ready = false;
        }
    }
}
