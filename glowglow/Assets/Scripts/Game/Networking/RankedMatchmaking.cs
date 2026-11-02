using System;
using System.Collections;
using System.IO;
using System.Text;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

namespace GlowGlow.Online
{
    // HTTP is used only for matchmaking and results, never for combat replication.
    public sealed class RankedMatchmaking : MonoBehaviour
    {
        [Serializable] private sealed class Settings
        {
            public string endpoint;
            public uint steamAppId;
            public string region = "asia";
            public bool developmentTest;
        }
        [Serializable] private sealed class Request
        {
            public string action, ticket, protocol, region, matchId, lobby, winner, steamId;
            public uint appId;
        }
        [Serializable] private sealed class Reply
        {
            public string status, token, steamId, matchId, host, guest, lobby, winner, error;
            public int rating, games, range;
            public bool testMode;
        }
        public bool Active { get; private set; }
        public bool Searching { get; private set; }
        public bool CombatStarted { get; private set; }
        public string MatchId { get; private set; }
        public string HostId { get; private set; }
        public string GuestId { get; private set; }
        public string OpponentId => HostId == ownId ? GuestId : HostId;
        public string Summary { get; private set; } = "랭크 대전";
        private OnlineSession session;
        private SteamSession steam;
        private Settings settings;
        private string token, ownId, ticket;
        private int initialRating;
        private bool leaveRequested, ticketFinished, requestFailed;
        private Reply reply;
        private HAuthTicket ticketHandle = HAuthTicket.Invalid;
        private Callback<GetTicketForWebApiResponse_t> ticketCallback;
        private UnityWebRequest currentRequest;

        public static uint ConfiguredAppId
        {
            get
            {
                try { return LoadSettings()?.steamAppId ?? 0; }
                catch { return 0; }
            }
        }

        private static Settings LoadSettings()
        {
            // Test builds can be pointed at a backend without rebuilding the game.
            if (!Application.isEditor && Debug.isDebugBuild)
            {
                string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "RankedSettings.json");
                if (File.Exists(path)) return JsonUtility.FromJson<Settings>(File.ReadAllText(path));
            }
            var asset = Resources.Load<TextAsset>("Online/RankedSettings");
            return asset == null ? null : JsonUtility.FromJson<Settings>(asset.text);
        }

        public void Begin(OnlineSession owner, SteamSession connection)
        {
            if (Active) return;
            session = owner; steam = connection;
            try { settings = LoadSettings(); }
            catch { settings = null; }
            if (settings == null || !Uri.TryCreate(settings.endpoint, UriKind.Absolute, out var uri)
                || uri.Scheme != "https" || settings.steamAppId == 0)
            { SetStatus("자동 매칭 서버 주소를 RankedSettings.json에 설정해 주세요."); return; }
            if (settings.developmentTest && (!(Application.isEditor || Debug.isDebugBuild) || settings.steamAppId != 480)
                || !settings.developmentTest && settings.steamAppId == 480)
            { SetStatus("480 자동 매칭 테스트는 개발 빌드에서 developmentTest를 켜 주세요."); return; }
            if (!steam.Initialize(out var error)) { SetStatus(error); return; }
            if (SteamUtils.GetAppID().m_AppId != settings.steamAppId)
            { SetStatus("랭크 대전을 지원하는 게임 버전으로 실행해 주세요."); return; }
            ownId = SteamUser.GetSteamID().m_SteamID.ToString();
            Active = Searching = true; leaveRequested = CombatStarted = false;
            ticketFinished = false; ticket = token = MatchId = HostId = GuestId = null;
            if (!settings.developmentTest)
            {
                ticketCallback = Callback<GetTicketForWebApiResponse_t>.Create(OnTicket);
                ticketHandle = SteamUser.GetAuthTicketForWebApi("glowglow-ranked");
            }
            StartCoroutine(Run());
        }
        public void NotifyCombatStarted() => CombatStarted = true;
        public void Leave() { if (Active) { leaveRequested = true; SetStatus("랭크 대전 종료 중…"); } }
        private void SetStatus(string text)
        {
            Summary = settings != null && settings.developmentTest ? "[테스트] " + text : text;
            session.SetRankedStatus(Summary);
        }
        private void OnTicket(GetTicketForWebApiResponse_t response)
        {
            if (response.m_hAuthTicket != ticketHandle) return;
            ticketFinished = true;
            if (response.m_eResult == EResult.k_EResultOK && response.m_cubTicket > 0
                && response.m_rgubTicket != null && response.m_cubTicket <= response.m_rgubTicket.Length)
                ticket = BitConverter.ToString(response.m_rgubTicket, 0, response.m_cubTicket).Replace("-", "");
        }
        private IEnumerator Run()
        {
            SetStatus("연결 경로 준비 중…");
            float relayUntil = Time.unscaledTime + 20;
            var relayWait = new WaitForSecondsRealtime(.5f);
            while (!steam.RelayReady && !leaveRequested && Time.unscaledTime < relayUntil) yield return relayWait;
            steam.LogRelayStatus();
            // A direct route may still be available if a relay is unavailable.
            SetStatus("랭크 프로필 확인 중…");
            float until = Time.unscaledTime + 15;
            while (!settings.developmentTest && !ticketFinished && !leaveRequested && Time.unscaledTime < until) yield return null;
            if (leaveRequested) { Close(); yield break; }
            if (!settings.developmentTest && ticket == null) { yield return Failure("인증을 완료하지 못했습니다."); yield break; }
            yield return Post(settings.developmentTest
                ? new Request { action = "auth_test", steamId = ownId, appId = 480 }
                : new Request { action = "auth", ticket = ticket });
            ReleaseTicket(); ticket = null;
            if (requestFailed || reply.steamId != ownId || string.IsNullOrEmpty(reply.token)
                || reply.testMode != settings.developmentTest)
            { yield return Failure("랭크 서버에 연결하지 못했습니다. 잠시 뒤 다시 시도하세요."); yield break; }
            token = reply.token; initialRating = reply.rating;
            while (!leaveRequested)
            {
                yield return Post(new Request { action = "queue", protocol = SteamSession.Protocol, region = settings.region });
                if (requestFailed) { yield return Failure("매칭 서버 응답이 끊겼습니다. 다시 시도해 주세요."); yield break; }
                if (reply.status == "reserved" || reply.status == "running") break;
                SetStatus($"상대를 찾는 중 · 점수 {reply.rating} · ±{reply.range}");
                yield return Pause(5);
            }
            if (leaveRequested) { yield return CancelAndClose(); yield break; }
            MatchId = reply.matchId; HostId = reply.host; GuestId = reply.guest;
            if (!Guid.TryParse(MatchId, out _) || !ulong.TryParse(HostId, out _) || !ulong.TryParse(GuestId, out _)
                || HostId == GuestId || ownId != HostId && ownId != GuestId)
            { yield return Failure("매칭 정보를 확인하지 못했습니다."); yield break; }
            Searching = false;
            bool asHost = ownId == HostId;
            bool lobbyPublished = false, joined = false;
            SetStatus("상대를 찾았습니다. 연결 중…");
            if (asHost) steam.HostRanked(MatchId);
            until = Time.unscaledTime + 150;
            while (!CombatStarted && !leaveRequested && !session.Interrupted && Time.unscaledTime < until)
            {
                if (asHost && !lobbyPublished && steam.Lobby.m_SteamID != 0)
                {
                    yield return Post(new Request { action = "lobby", matchId = MatchId, lobby = steam.Lobby.m_SteamID.ToString() });
                    if (requestFailed) { yield return Failure("매칭 방을 등록하지 못했습니다."); yield break; }
                    lobbyPublished = true;
                }
                else yield return Post(new Request { action = "status", matchId = MatchId });
                if (requestFailed) { yield return Failure("매칭 상태를 확인하지 못했습니다."); yield break; }
                if (reply.status != "reserved" && reply.status != "running")
                { yield return Failure("상대방이 매칭을 취소했거나 연결 시간이 초과됐습니다."); yield break; }
                if (!asHost && !joined && ulong.TryParse(reply.lobby, out ulong lobbyId) && lobbyId != 0)
                { joined = true; steam.JoinRanked(lobbyId, MatchId); }
                yield return Pause(3);
            }
            if (leaveRequested) { yield return CancelAndClose(); yield break; }
            if (!CombatStarted || session.Interrupted)
            {
                string reason = session.Interrupted ? session.Status :
                    "대전 준비 시간이 초과됐습니다. 양쪽의 연결 및 탄막 선택 상태를 확인해 주세요.";
                yield return Failure(reason); yield break;
            }
            yield return Post(new Request { action = "start", matchId = MatchId });
            if (requestFailed) { yield return Failure("랭크 대전 시작을 확인하지 못했습니다."); yield break; }
            SetStatus("랭크 대전 중");
            // No polling or web traffic while normal combat is in progress.
            var combatWait = new WaitForSecondsRealtime(.1f);
            while (!leaveRequested && !session.Interrupted && session.Match != null && session.Match.IsPlaying)
                yield return combatWait;
            if (leaveRequested && session.Match != null && session.Match.IsPlaying)
            { yield return CancelAndClose(); yield break; }
            if (session.Interrupted || session.Match == null)
            { yield return Failure("연결이 끊겨 랭크 결과를 확인하지 못했습니다."); yield break; }
            string winner = session.Match.Winner == null ? "draw" : session.Match.Winner == session.Match.PlayerOne ? HostId : GuestId;
            SetStatus("경기 결과 확인 중…");
            // Retry the same result safely; the backend awards it only once after agreement.
            bool sent = false;
            for (int attempt = 0; attempt < 3 && !sent; attempt++)
            {
                yield return Post(new Request { action = "report", matchId = MatchId, winner = winner });
                sent = !requestFailed;
                if (!sent) yield return new WaitForSecondsRealtime(2);
            }
            until = Time.unscaledTime + 60;
            while (sent && reply.status == "running" && !leaveRequested && Time.unscaledTime < until)
            {
                yield return Pause(5);
                yield return Post(new Request { action = "status", matchId = MatchId });
                if (requestFailed) break;
            }
            if (!requestFailed && reply.status == "complete")
                SetStatus($"랭크 점수 {reply.rating} ({reply.rating - initialRating:+0;-0;0}) · ESC 나가기");
            else SetStatus("랭크 결과 확인이 보류됐습니다. · ESC 나가기");
            while (!leaveRequested) yield return null;
            // A finished game's submitted result must not turn into a concession on exit.
            Close();
        }
        private IEnumerator Failure(string message)
        {
            Searching = false;
            session.FailRanked(message);
            SetStatus(message + " · ESC 나가기");
            if (token != null && !string.IsNullOrEmpty(MatchId))
            {
                yield return Post(new Request { action = "status", matchId = MatchId });
                if (!requestFailed && reply.status == "complete")
                    SetStatus($"랭크 점수 {reply.rating} ({reply.rating - initialRating:+0;-0;0}) · ESC 나가기");
            }
            while (!leaveRequested) yield return null;
            // Preserve any started match for review/expiry instead of trusting disconnect claims.
            if (!CombatStarted && token != null) yield return Post(new Request { action = "cancel", matchId = MatchId });
            Close();
        }
        private IEnumerator CancelAndClose()
        {
            if (token != null) yield return Post(new Request { action = "cancel", matchId = MatchId ?? reply?.matchId });
            Close();
        }
        private void Close()
        {
            Active = Searching = false; ReleaseTicket();
            session.Back();
        }
        private IEnumerator Pause(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (!leaveRequested && Time.unscaledTime < until) yield return null;
        }
        private IEnumerator Post(Request body)
        {
            requestFailed = true; reply = null;
            using (var request = new UnityWebRequest(settings.endpoint, "POST"))
            {
                currentRequest = request;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 12;
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try { reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); }
                    catch { reply = null; }
                    requestFailed = reply == null || !string.IsNullOrEmpty(reply.error);
                }
                currentRequest = null;
            }
        }
        private void ReleaseTicket()
        {
            if (ticketHandle != HAuthTicket.Invalid && steam != null && steam.Ready)
                SteamUser.CancelAuthTicket(ticketHandle);
            ticketHandle = HAuthTicket.Invalid;
            ticketCallback?.Dispose(); ticketCallback = null;
        }
        private void OnDestroy() { currentRequest?.Abort(); currentRequest?.Dispose(); currentRequest = null; ReleaseTicket(); }
    }
}
