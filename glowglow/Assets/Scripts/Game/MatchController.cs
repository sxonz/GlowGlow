using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class MatchController : MonoBehaviour
{
    private static bool trainingRequested;
    private static int requestedBotRating;
    public bool IsTraining { get; private set; }
    public bool IsOnline => GlowGlow.Online.OnlineSession.Current?.Match == this;
    public DeckCatalog Catalog => deckCatalog;
    public bool IsBotMatch { get; private set; }
    public int BotRating { get; private set; }
    public static void RequestTraining() { trainingRequested = true; requestedBotRating = 0; }
    public static void RequestBotMatch(int rating)
    {
        trainingRequested = false;
        requestedBotRating = Mathf.Clamp(rating, BotPlayerInput.MinRating, BotPlayerInput.MaxRating);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRequest() { trainingRequested = false; requestedBotRating = 0; }
    public event Action StateChanged;
    public bool IsPlaying { get; private set; }
    public bool IsDrafting { get; private set; }
    private OpeningDraftScreen draftScreen;
    public float RemainingTime => Mathf.Max(0, matchDuration - elapsed);
    public PlayerCombatant Winner { get; private set; }

    [SerializeField] private PlayerCombatant playerOne;
    [SerializeField] private PlayerCombatant playerTwo;
    [SerializeField] private float matchDuration = 480f;
    [SerializeField] private DeckCatalog deckCatalog;
    public void ConfigureDeckRules(DeckCatalog catalog) => deckCatalog = catalog;
    private float elapsed;

    public PlayerCombatant PlayerOne => playerOne;
    public PlayerCombatant PlayerTwo => playerTwo;

    public void Configure(PlayerCombatant one, PlayerCombatant two)
    {
        playerOne = one;
        playerTwo = two;
    }

    private void Start()
    {
        RuntimeShapes.AddArenaGlow(gameObject.scene);
        playerOne.AttachMatch(this);
        playerTwo.AttachMatch(this);
        IsTraining = trainingRequested;
        BotRating = requestedBotRating;
        IsBotMatch = BotRating > 0;
        trainingRequested = false;
        requestedBotRating = 0;
        if (GlowGlow.Online.OnlineSession.Current?.InArena == true)
        {
            IsTraining = IsBotMatch = false;
            GlowGlow.Online.OnlineSession.Current.Attach(this);
            return;
        }
        if (IsBotMatch)
        {
            var bot = playerTwo.gameObject.AddComponent<BotPlayerInput>();
            bot.Configure(playerTwo, playerOne, this, BotRating);
            playerTwo.SetInputSource(bot);
        }
        if (IsTraining)
        {
            IsPlaying = true;
            gameObject.AddComponent<TrainingGround>().Initialize(this, deckCatalog);
            return;
        }
        RestartMatch();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (IsOnline) GlowGlow.Online.OnlineSession.Current.Back();
            else SceneManager.LoadScene("Title");
            return;
        }
        if (IsOnline)
        {
            if (!IsPlaying && !IsDrafting && keyboard != null && keyboard.rKey.wasPressedThisFrame)
                GlowGlow.Online.OnlineSession.Current.RequestRematch();
            if (!GlowGlow.Online.OnlineSession.Current.IsHost) return;
        }
        if (IsTraining) return;
        if (!IsPlaying)
        {
            if (!IsOnline && !IsDrafting && keyboard != null && keyboard.rKey.wasPressedThisFrame) RestartMatch();
            return;
        }
        elapsed += Time.deltaTime;
        if (elapsed >= matchDuration) FinishByTime();
        StateChanged?.Invoke();
    }

    public void ReportHit(PlayerCombatant victim, PlayerCombatant attacker)
    {
        StateChanged?.Invoke();
        if (!IsTraining && victim.HitsTaken >= 3) Finish(victim == playerOne ? playerTwo : playerOne);
    }

    public void RestartMatch()
    {
        if (IsOnline) { GlowGlow.Online.OnlineSession.Current.RequestRematch(); return; }
        if (IsTraining) { GetComponent<TrainingGround>().ResetArena(); return; }
        IsPlaying = false;
        if (deckCatalog == null || !deckCatalog.IsSavedDeckValid())
        {
            IsPlaying = false;
            SceneManager.LoadScene("Title");
            return;
        }
        ProjectileBase.DespawnAll();
        elapsed = 0f;
        Winner = null;
        playerOne.ResetCombatant(new Vector2(-5.8f, 0));
        playerTwo.ResetCombatant(new Vector2(5.8f, 0));
        OpeningDraft botDraft = null;
        if (IsBotMatch)
        {
            playerTwo.GetComponent<BotPlayerInput>().ResetBrain();
            botDraft = new OpeningDraft(deckCatalog.LoadSelectedWeapons(), new System.Random(),
                Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades"));
            // Start with a damaging weapon so a pulse-only build cannot stall the match.
            for (int i = 0; i < botDraft.Offers.Count; i++)
            {
                bool pulseOnly = false;
                foreach (var step in botDraft.Offers[i].Weapon.steps)
                    if (step != null && step.shape == BarrageShape.ElectricPulse) pulseOnly = true;
                if (!pulseOnly) { botDraft.Select(i); break; }
            }
            while (!botDraft.IsComplete) botDraft.Confirm(true);
            playerTwo.EquipDraft(botDraft, DeckCatalog.RequiredDeckSize);
        }
        IsDrafting = true;
        if (draftScreen == null) draftScreen = gameObject.AddComponent<OpeningDraftScreen>();
        draftScreen.Show(new OpeningDraft(deckCatalog.LoadSelectedWeapons(), new System.Random(),
            Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades")), StartCombat, botDraft?.Choices, BotRating);
        StateChanged?.Invoke();
    }

    private void StartCombat(OpeningDraft draft)
    {
        if (!IsDrafting || !draft.IsComplete) return;
        playerOne.EquipDraft(draft, DeckCatalog.RequiredDeckSize);
        if (IsBotMatch) playerTwo.GetComponent<BotPlayerInput>().ResetBrain();
        IsDrafting = false;
        IsPlaying = true;
        StateChanged?.Invoke();
    }

    public void PrepareOnlineDraft(OpeningDraft draft, Action<OpeningDraft> complete)
    {
        IsPlaying = false;
        IsDrafting = true;
        Winner = null;
        elapsed = 0;
        ProjectileBase.DespawnAll();
        playerOne.ResetCombatant(new Vector2(-5.8f, 0));
        playerTwo.ResetCombatant(new Vector2(5.8f, 0));
        if (draftScreen == null) draftScreen = gameObject.AddComponent<OpeningDraftScreen>();
        draftScreen.Show(draft, complete);
        StateChanged?.Invoke();
    }

    public void StartOnlineCombat(OpeningDraft one, OpeningDraft two)
    {
        draftScreen?.Hide();
        playerOne.EquipDraft(one, DeckCatalog.RequiredDeckSize);
        playerTwo.EquipDraft(two, DeckCatalog.RequiredDeckSize);
        IsDrafting = false;
        IsPlaying = true;
        elapsed = 0;
        StateChanged?.Invoke();
    }

    public void ApplyOnlineClock(bool playing, bool drafting, float remaining, int winner)
    {
        IsPlaying = playing;
        IsDrafting = drafting;
        elapsed = matchDuration - Mathf.Clamp(remaining, 0, matchDuration);
        Winner = winner == 0 ? playerOne : winner == 1 ? playerTwo : null;
        StateChanged?.Invoke();
    }

    public void StopOnlineCombat()
    {
        draftScreen?.Hide();
        IsPlaying = IsDrafting = false;
        if (playerOne != null) playerOne.StopAllCoroutines();
        if (playerTwo != null) playerTwo.StopAllCoroutines();
        ProjectileBase.DespawnAll();
        StateChanged?.Invoke();
    }

    private void FinishByTime()
    {
        if (playerOne.HitsTaken == playerTwo.HitsTaken) Finish(null);
        else Finish(playerOne.HitsTaken < playerTwo.HitsTaken ? playerOne : playerTwo);
    }

    private void Finish(PlayerCombatant winner)
    {
        Winner = winner;
        IsPlaying = false;
        StateChanged?.Invoke();
    }
}
