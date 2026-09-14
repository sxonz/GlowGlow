using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlowGlow.Online
{
    public struct ClassicWelcome : IBroadcast { public int Slot; public string Version; }
    public struct ClassicDeck : IBroadcast { public string Json; }
    public struct ClassicDraft : IBroadcast { public int Round, SeedOne, SeedTwo; public string DeckOne, DeckTwo; }
    public struct ClassicReady : IBroadcast { public int Round; public int[] Choices; }
    public struct ClassicStart : IBroadcast { public int Round; public uint StartTick; public int[] One, Two; }
    public struct ClassicRematch : IBroadcast { public int Round; }
    public struct ClassicInput : IBroadcast { public int Round; public uint Sequence; public PlayerCommand Command; }
    public struct ClassicNotice : IBroadcast { public string Message; public bool Interrupted; }
    public struct ClassicPlayerState
    {
        public Vector2 Position, Aim;
        public int Hits, Shield, Selected;
        public float[] Cooldowns, Durations;
    }
    public struct ClassicState : IBroadcast
    {
        public int Round; public uint Sequence;
        public bool Playing, Drafting; public float Remaining; public int Winner;
        public ClassicPlayerState One, Two;
    }

    // Owns connection, deck exchange and match lifecycle. Combat always uses the real Arena scene.
    [DefaultExecutionOrder(-200)]
    public sealed class OnlineSession : MonoBehaviour
    {
        public static OnlineSession Current { get; private set; }
        public MatchController Match { get; private set; }
        public bool InArena { get; private set; }
        public bool IsHost { get; private set; }
        public bool Interrupted { get; private set; }
        public bool WaitingForCombat => !combatStarted && !Interrupted;
        public int LocalSlot { get; private set; } = -1;
        public string Status { get; private set; } = "클래식 · 친구와 대전";
        public bool Connected => manager != null && manager.IsClientStarted;
        public bool HasSession => manager != null;
        public long Ping => manager == null ? 0 : manager.TimeManager.RoundTripTime;
        public PlayerCombatant LocalPlayer => Match == null ? null : LocalSlot == 0 ? Match.PlayerOne : Match.PlayerTwo;
        public int Round => round;
        public int ReceivedVisualFrames => presentation != null ? presentation.AppliedFrames : 0;
        public bool Smoke => (Application.isEditor || Debug.isDebugBuild) && Array.IndexOf(Environment.GetCommandLineArgs(), "-classic-smoke") >= 0;
        private NetworkManager manager;
        private SteamSession steam;
        private DeckCatalog catalog;
        private bool local, leaving, draftSent, startSent, combatStarted;
        private float deadline, inputDeadline, nextSend;
        private uint inputSequence, lastInput, snapshotSequence, lastState;
        private int round;
        private readonly Dictionary<int, int> participants = new();
        private readonly string[] decks = new string[2];
        private readonly int[][] ready = new int[2][];
        private readonly bool[] rematch = new bool[2];
        private ClassicDraft proposal;
        private RemoteInput remoteInput = new();
        private ClassicPresentation presentation;
        private int selectedSlot = -1;
        private bool startScheduled;
        private uint startTick;
        private OpeningDraft pendingOne, pendingTwo;
        private OnlineScreen screen;

        public static void Open(DeckCatalog selectedCatalog)
        {
            if (Current != null) return;
            var root = new GameObject("Classic Online Session");
            var session = root.AddComponent<OnlineSession>();
            session.catalog = selectedCatalog;
            session.screen = root.AddComponent<OnlineScreen>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void DevelopmentLaunch()
        {
            if (!(Application.isEditor || Debug.isDebugBuild)) return;
            var args = Environment.GetCommandLineArgs();
            bool host = Array.IndexOf(args, "-classic-host") >= 0;
            if (!host && Array.IndexOf(args, "-classic-client") < 0) return;
            var match = UnityEngine.Object.FindFirstObjectByType<MatchController>();
            // Title references the selected catalog, also available through its serialized field.
            DeckCatalog deck = match != null ? match.Catalog : Resources.FindObjectsOfTypeAll<DeckCatalog>().FirstOrDefault();
            if (deck == null) { Debug.LogError("CLASSIC: Missing deck catalog"); return; }
            Open(deck);
            if (Current.Smoke)
            {
                deck.EnsureStarterDeck();
                Current.gameObject.AddComponent<ClassicSmokeProbe>();
            }
            Current.StartLocal(host);
        }

        private void Awake()
        {
            Current = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            steam = SteamSession.Get();
            steam.LobbyReady += OnSteamLobby;
            steam.Failed += Fail;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        public void HostSteam()
        {
            if (HasSession) return;
            if (!steam.Initialize(out var error)) { Status = error; return; }
            Status = "Steam 방 생성 중…"; steam.Host();
        }
        public void JoinSteam(string code)
        {
            if (HasSession) return;
            if (!ulong.TryParse(code.Trim(), out var id) || id == 0) { Status = "올바른 방 코드를 입력하세요."; return; }
            if (!steam.Initialize(out var error)) { Status = error; return; }
            Status = "Steam 방 참가 중…"; steam.Join(id);
        }
        private void OnSteamLobby(ulong host, bool asHost)
        {
            if (HasSession) return;
            StartNetwork(false, asHost, host.ToString());
        }
        public void StartLocal(bool host)
        {
            if (!(Application.isEditor || Debug.isDebugBuild) || HasSession) return;
            StartNetwork(true, host, "127.0.0.1");
        }
        private void StartNetwork(bool useLocal, bool host, string address)
        {
            if (catalog == null || !catalog.IsSavedDeckValid()) { Status = "먼저 덱에 서로 다른 탄막 8개를 넣어 주세요."; return; }
            var prefab = Resources.Load<GameObject>("Online/NetworkManager");
            if (prefab == null) { Fail("네트워크 리소스가 없습니다."); return; }
            local = useLocal; IsHost = host; leaving = Interrupted = false;
            draftSent = startSent = combatStarted = startScheduled = false;
            decks[0] = decks[1] = null;
            ready[0] = ready[1] = null;
            round = 0; lastInput = inputSequence = lastState = snapshotSequence = 0;
            rematch[0] = rematch[1] = false;
            var instance = Instantiate(prefab);
            DontDestroyOnLoad(instance);
            manager = instance.GetComponent<NetworkManager>();
            var transport = useLocal ? (Transport)instance.GetComponent<Tugboat>() : instance.GetComponent<FishySteamworks.FishySteamworks>();
            instance.GetComponent<Tugboat>().enabled = useLocal;
            instance.GetComponent<FishySteamworks.FishySteamworks>().enabled = !useLocal;
            manager.GetComponent<FishNet.Managing.Transporting.TransportManager>().Transport = transport;
            instance.SetActive(true);
            manager.TimeManager.SetTickRate(60);
            transport.SetMaximumClients(2);
            transport.SetClientAddress(address);
            var args = Environment.GetCommandLineArgs();
            int latencyIndex = Array.IndexOf(args, "-online-latency");
            if ((Application.isEditor || Debug.isDebugBuild) && latencyIndex >= 0 && latencyIndex + 1 < args.Length
                && long.TryParse(args[latencyIndex + 1], out long latency))
            {
                var simulator = manager.TransportManager.LatencySimulator;
                simulator.SetLatency(Math.Max(0, Math.Min(250, latency)));
                simulator.SetPacketLoss(.02); simulator.SetEnabled(true);
            }
            manager.SceneManager.OnClientLoadedStartScenes += OnLoaded;
            manager.ServerManager.OnRemoteConnectionState += OnRemote;
            manager.ClientManager.OnClientConnectionState += OnClient;
            manager.ServerManager.RegisterBroadcast<ClassicDeck>(OnDeck);
            manager.ServerManager.RegisterBroadcast<ClassicReady>(OnReady);
            manager.ServerManager.RegisterBroadcast<ClassicInput>(OnInput);
            manager.ServerManager.RegisterBroadcast<ClassicRematch>(OnRematch);
            manager.ClientManager.RegisterBroadcast<ClassicWelcome>(OnWelcome);
            manager.ClientManager.RegisterBroadcast<ClassicDraft>(OnDraft);
            manager.ClientManager.RegisterBroadcast<ClassicStart>(OnStart);
            manager.ClientManager.RegisterBroadcast<ClassicState>(OnState);
            manager.ClientManager.RegisterBroadcast<ClassicNotice>(OnNotice);
            manager.ClientManager.RegisterBroadcast<ClassicVisualFrame>(OnVisuals);
            deadline = Time.unscaledTime + 25;
            Status = host ? "상대방을 기다리는 중…" : "호스트에 연결 중…";
            if (host && !manager.ServerManager.StartConnection()) { Fail("호스트를 시작하지 못했습니다."); return; }
            if (!manager.ClientManager.StartConnection()) Fail("연결을 시작하지 못했습니다.");
        }
        private void OnLoaded(NetworkConnection connection, bool asServer)
        {
            if (!asServer || leaving || participants.ContainsKey(connection.ClientId)) return;
            bool own = connection == manager.ClientManager.Connection;
            if (!local && !own)
            {
                string address = manager.TransportManager.Transport.GetConnectionAddress(connection.ClientId);
                bool member = false;
                for (int i = 0; i < Steamworks.SteamMatchmaking.GetNumLobbyMembers(steam.Lobby); i++)
                    member |= Steamworks.SteamMatchmaking.GetLobbyMemberByIndex(steam.Lobby, i).m_SteamID.ToString() == address;
                if (!member) { connection.Disconnect(true); return; }
            }
            int slot = own ? 0 : 1;
            if (participants.ContainsValue(slot) || draftSent || Interrupted) { connection.Disconnect(true); return; }
            participants.Add(connection.ClientId, slot);
            manager.ServerManager.Broadcast(connection, new ClassicWelcome { Slot = slot, Version = SteamSession.Protocol });
        }
        private void OnWelcome(ClassicWelcome data, Channel channel)
        {
            if (data.Version != SteamSession.Protocol || data.Slot < 0 || data.Slot > 1) { Fail("게임 버전이 다릅니다."); return; }
            LocalSlot = data.Slot;
            InArena = true;
            SceneManager.LoadScene("Arena");
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Title" && InArena) { Disconnect(); Destroy(gameObject); }
        }
        public void Attach(MatchController match)
        {
            Match = match;
            presentation = match.gameObject.AddComponent<ClassicPresentation>();
            presentation.Initialize(match, IsHost);
            if (!IsHost)
            {
                match.PlayerOne.SetNetworkReplica();
                match.PlayerTwo.SetNetworkReplica();
            }
            else match.PlayerTwo.SetInputSource(remoteInput);
            LocalPlayer.GetComponent<LocalPlayerInput>().Configure(LocalPlayerInput.ControlScheme.MouseAndKeyboard,
                Camera.main, LocalSlot == 0 ? match.PlayerTwo.transform : match.PlayerOne.transform);
            manager.ClientManager.Broadcast(new ClassicDeck { Json = PlayerPrefs.GetString(DeckCatalog.SaveKey, "{}") });
            deadline = 0;
            if (Smoke && IsHost) match.PlayerOne.SetInputSource(GetComponent<ClassicSmokeProbe>());
            Status = "상대방의 덱과 장면 로딩을 기다리는 중…";
        }
        private bool Participant(NetworkConnection connection, out int slot) => participants.TryGetValue(connection.ClientId, out slot);
        private void OnDeck(NetworkConnection connection, ClassicDeck data, Channel channel)
        {
            if (draftSent || !Participant(connection, out int slot)) return;
            if (data.Json == null || data.Json.Length > 4096 || !catalog.IsDeckValid(data.Json))
            { connection.Disconnect(true); return; }
            decks[slot] = data.Json;
            if (decks[0] != null && decks[1] != null) BeginDraft();
        }
        private void BeginDraft()
        {
            round++;
            draftSent = true; startSent = false;
            ready[0] = ready[1] = null; rematch[0] = rematch[1] = false;
            remoteInput.Clear();
            proposal = new ClassicDraft { Round = round, DeckOne = decks[0], DeckTwo = decks[1],
                SeedOne = UnityEngine.Random.Range(1, int.MaxValue), SeedTwo = UnityEngine.Random.Range(1, int.MaxValue) };
            Send(proposal);
            steam.SetJoinable(false);
        }
        private OpeningDraft MakeDraft(int slot)
        {
            return new OpeningDraft(catalog.LoadWeapons(slot == 0 ? proposal.DeckOne : proposal.DeckTwo),
                new System.Random(slot == 0 ? proposal.SeedOne : proposal.SeedTwo),
                Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades").OrderBy(x => x.name, StringComparer.Ordinal), true);
        }
        private OpeningDraft Reconstruct(int slot, int[] choices)
        {
            if (choices == null || choices.Length != OpeningDraft.RoundCount) return null;
            var draft = MakeDraft(slot);
            foreach (int choice in choices)
                if (!draft.Select(choice) || !draft.Confirm()) return null;
            return draft;
        }
        private void OnDraft(ClassicDraft data, Channel channel)
        {
            if (Match == null || data.Round < round || !catalog.IsDeckValid(data.DeckOne) || !catalog.IsDeckValid(data.DeckTwo)) return;
            round = data.Round; proposal = data; Interrupted = false;
            combatStarted = startScheduled = false;
            ready[0] = ready[1] = null; rematch[0] = rematch[1] = false;
            lastState = 0; selectedSlot = -1;
            presentation.ClearReplicas();
            Match.PrepareOnlineDraft(MakeDraft(LocalSlot), draft =>
            {
                Status = "준비 완료 · 상대방의 선택을 기다리는 중…";
                manager.ClientManager.Broadcast(new ClassicReady { Round = round, Choices = draft.SelectedIndices.ToArray() });
            });
            Status = "각자 덱에서 시작 탄막과 업그레이드를 선택하세요.";
            deadline = Time.unscaledTime + 65;
            if (Smoke)
            {
                var draft = MakeDraft(LocalSlot);
                while (!draft.IsComplete)
                {
                    int choice = 0;
                    for (int i = 0; i < draft.Offers.Count; i++)
                    {
                        var offer = draft.Offers[i];
                        bool pulse = offer.Weapon.steps != null && offer.Weapon.steps.Any(s => s != null && s.shape == BarrageShape.ElectricPulse);
                        if (draft.RoundNumber == 2 ? offer.IsUpgrade : !offer.IsUpgrade && !pulse)
                        { choice = i; break; }
                    }
                    draft.Select(choice); draft.Confirm();
                }
                Match.GetComponent<OpeningDraftScreen>().Hide();
                manager.ClientManager.Broadcast(new ClassicReady { Round = round, Choices = draft.SelectedIndices.ToArray() });
            }
        }
        private void OnReady(NetworkConnection connection, ClassicReady data, Channel channel)
        {
            if (Interrupted || !draftSent || startSent || data.Round != round || !Participant(connection, out int slot) || ready[slot] != null) return;
            if (Reconstruct(slot, data.Choices) == null) { connection.Disconnect(true); return; }
            ready[slot] = data.Choices;
            if (ready[0] != null && ready[1] != null)
            {
                startSent = true;
                Send(new ClassicStart { Round = round, StartTick = manager.TimeManager.Tick + 120, One = ready[0], Two = ready[1] });
            }
        }
        private void OnStart(ClassicStart data, Channel channel)
        {
            if (Match == null || data.Round != round || Interrupted) return;
            var one = Reconstruct(0, data.One); var two = Reconstruct(1, data.Two);
            if (one == null || two == null) { Fail("드래프트 정보를 확인할 수 없습니다."); return; }
            pendingOne = one; pendingTwo = two;
            startTick = data.StartTick; startScheduled = true;
            Status = "두 플레이어 준비 완료 · 곧 대전을 시작합니다.";
        }
        private void StartScheduledCombat()
        {
            startScheduled = false;
            Match.StartOnlineCombat(pendingOne, pendingTwo);
            combatStarted = true;
            Status = "대전 중";
            deadline = 0;
            inputDeadline = Time.unscaledTime + 5;
            Debug.Log($"CLASSIC_COMBAT_STARTED slot={LocalSlot} round={round} p1={pendingOne.Acquired.Count} p2={pendingTwo.Acquired.Count}");
        }
        public bool SelectSlot(PlayerCombatant player, int index)
        {
            if (player != LocalPlayer || Match == null || !Match.IsPlaying || index < 0 || index >= (player.Hand?.Drawn.Count ?? 0)) return false;
            selectedSlot = index;
            return true;
        }
        private void OnInput(NetworkConnection connection, ClassicInput data, Channel channel)
        {
            if (Match == null || !Participant(connection, out int slot) || slot != 1 || data.Round != round || !Match.IsPlaying || Interrupted) return;
            if ((int)(data.Sequence - lastInput) <= 0) return;
            var command = data.Command;
            if (!Finite(command.Move) || !Finite(command.Aim) || !Finite(command.AimPosition)) return;
            command.Move = Vector2.ClampMagnitude(command.Move, 1);
            command.Aim = command.Aim.sqrMagnitude > .001f ? command.Aim.normalized : Vector2.left;
            command.AimPosition = Match.PlayerTwo.ClampToArena(command.AimPosition);
            command.SelectedSlot = Mathf.Clamp(command.SelectedSlot, -1, 2);
            command.WeaponCycle = Mathf.Clamp(command.WeaponCycle, -1, 1);
            lastInput = data.Sequence;
            remoteInput.Push(command);
            inputDeadline = Time.unscaledTime + 5;
        }
        private static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y)
            && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
        private void Update()
        {
            if (manager == null || Interrupted) return;
            if (startScheduled && (int)(manager.TimeManager.Tick - startTick) >= 0) StartScheduledCombat();
            if (deadline > 0 && Time.unscaledTime > deadline && (!Connected || InArena && Match != null && !Match.IsPlaying))
            { Fail("연결 또는 드래프트 대기 시간이 초과됐습니다."); return; }
            if (Match == null || !Match.IsPlaying) return;
            if (!IsHost)
            {
                var command = LocalPlayer.GetComponent<LocalPlayerInput>().ReadCommand(LocalPlayer.transform.position);
                if (Smoke) command = GetComponent<ClassicSmokeProbe>().ReadCommand(LocalPlayer.transform.position);
                if (selectedSlot >= 0) { command.SelectedSlot = selectedSlot; selectedSlot = -1; }
                // Inputs use reliable ordered delivery: a one-frame dash, click or slot selection must not vanish.
                manager.ClientManager.Broadcast(new ClassicInput { Round = round, Sequence = ++inputSequence, Command = command });
            }
            else if (Time.unscaledTime > inputDeadline)
            {
                Interrupt("상대방의 입력이 끊겨 대전을 중단했습니다.");
            }
        }
        public void PublishState()
        {
            if (!IsHost || manager == null || Match == null || Interrupted || Time.unscaledTime < nextSend || !startSent) return;
            nextSend = Time.unscaledTime + 1f / 30;
            var state = new ClassicState { Round = round, Sequence = ++snapshotSequence,
                Playing = Match.IsPlaying, Drafting = Match.IsDrafting, Remaining = Match.RemainingTime,
                Winner = Match.Winner == Match.PlayerOne ? 0 : Match.Winner == Match.PlayerTwo ? 1 : -1,
                One = Match.PlayerOne.CaptureNetworkState(), Two = Match.PlayerTwo.CaptureNetworkState() };
            Send(state, Channel.Unreliable);
            // Visual chunks are independently sequenced; lost chunks never delete unrelated projectiles.
            foreach (var frame in presentation.Capture(round, snapshotSequence)) Send(frame, Channel.Unreliable, true);
        }
        private void OnState(ClassicState state, Channel channel)
        {
            if (IsHost || Match == null || !combatStarted || state.Round != round || Interrupted || (int)(state.Sequence - lastState) <= 0) return;
            lastState = state.Sequence;
            Match.PlayerOne.ApplyNetworkState(state.One);
            Match.PlayerTwo.ApplyNetworkState(state.Two);
            Match.ApplyOnlineClock(state.Playing, state.Drafting, state.Remaining, state.Winner);
            if (!state.Playing && !state.Drafting) Status = "상대방과 재대전에 동의하면 다시 덱 선택을 시작합니다.";
        }
        private void OnVisuals(ClassicVisualFrame frame, Channel channel)
        {
            if (IsHost || Match == null || frame.Round != round || Interrupted) return;
            presentation.Apply(frame);
        }
        private void Send<T>(T message, Channel channel = Channel.Reliable, bool guestOnly = false) where T : struct, IBroadcast
        {
            foreach (var participant in participants)
                if ((!guestOnly || participant.Value == 1) && manager.ServerManager.Clients.TryGetValue(participant.Key, out var connection) && connection.IsAuthenticated)
                    manager.ServerManager.Broadcast(connection, message, true, channel);
        }
        public void RequestRematch()
        {
            if (Match == null || Match.IsPlaying || Match.IsDrafting || Interrupted || !Connected) return;
            manager.ClientManager.Broadcast(new ClassicRematch { Round = round });
            Status = "재대전 동의 완료 · 상대방을 기다리는 중…";
        }
        private void OnRematch(NetworkConnection connection, ClassicRematch data, Channel channel)
        {
            if (Match == null || !combatStarted || !Participant(connection, out int slot) || data.Round != round || Match.IsPlaying || Match.IsDrafting || Interrupted) return;
            rematch[slot] = true;
            if (rematch[0] && rematch[1]) BeginDraft();
        }
        private void OnRemote(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped && participants.Remove(connection.ClientId) && !leaving)
                Interrupt("상대방의 연결이 종료됐습니다.");
        }
        private void OnClient(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped && !leaving) Fail("호스트와 연결이 종료됐습니다.");
        }
        private void OnNotice(ClassicNotice data, Channel channel)
        {
            Status = data.Message;
            if (data.Interrupted) { Interrupted = true; Match?.StopOnlineCombat(); }
        }
        private void Interrupt(string message)
        {
            if (Interrupted) return;
            Interrupted = true; Status = message;
            Match?.StopOnlineCombat();
            if (IsHost && manager != null) Send(new ClassicNotice { Message = message, Interrupted = true });
        }
        private void Fail(string message)
        {
            Interrupt(message);
            Disconnect();
            Status = message;
        }
        public void Disconnect()
        {
            if (leaving && manager == null) return;
            leaving = true;
            if (Match != null) Match.StopOnlineCombat();
            if (manager != null)
            {
                manager.SceneManager.OnClientLoadedStartScenes -= OnLoaded;
                manager.ServerManager.OnRemoteConnectionState -= OnRemote;
                manager.ClientManager.OnClientConnectionState -= OnClient;
                manager.ClientManager.StopConnection(); manager.ServerManager.StopConnection(true);
                manager.TransportManager.Transport.Shutdown();
                Destroy(manager.gameObject); manager = null;
            }
            steam?.Leave();
            participants.Clear();
        }
        public void Back()
        {
            Disconnect(); InArena = false;
            Destroy(gameObject);
            SceneManager.LoadScene("Title");
        }
        private void OnDestroy()
        {
            Disconnect();
            SceneManager.sceneLoaded -= SceneLoaded;
            if (steam != null) { steam.LobbyReady -= OnSteamLobby; steam.Failed -= Fail; }
            if (Current == this) Current = null;
        }
        private sealed class RemoteInput : IPlayerInputSource
        {
            private PlayerCommand command = new() { SelectedSlot = -1 };
            private float receivedAt;
            public void Push(PlayerCommand incoming)
            {
                incoming.DashPressed |= command.DashPressed;
                incoming.FirePressed |= command.FirePressed;
                if (incoming.SelectedSlot < 0) incoming.SelectedSlot = command.SelectedSlot;
                incoming.WeaponCycle = Mathf.Clamp(incoming.WeaponCycle + command.WeaponCycle, -1, 1);
                command = incoming; receivedAt = Time.unscaledTime;
            }
            public PlayerCommand ReadCommand(Vector2 position)
            {
                if (Time.unscaledTime - receivedAt > .25f) Clear();
                var result = command;
                command.DashPressed = command.FirePressed = false;
                command.SelectedSlot = -1; command.WeaponCycle = 0;
                return result;
            }
            public void Clear() => command = new PlayerCommand { SelectedSlot = -1, Aim = Vector2.left };
        }
    }
}

