using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class MatchController : MonoBehaviour
{
    public event Action StateChanged;
    public bool IsPlaying { get; private set; }
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
        RestartMatch();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SceneManager.LoadScene("Title");
        if (!IsPlaying)
        {
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) RestartMatch();
            return;
        }
        elapsed += Time.deltaTime;
        if (elapsed >= matchDuration) FinishByTime();
        StateChanged?.Invoke();
    }

    public void ReportHit(PlayerCombatant victim, PlayerCombatant attacker)
    {
        StateChanged?.Invoke();
        if (victim.HitsTaken >= 3) Finish(victim == playerOne ? playerTwo : playerOne);
    }

    public void RestartMatch()
    {
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
        IsPlaying = true;
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
