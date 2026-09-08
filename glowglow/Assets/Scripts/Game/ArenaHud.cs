using TMPro;
using UnityEngine;

public sealed class ArenaHud : MonoBehaviour
{
    [SerializeField] private MatchController match;
    [SerializeField] private TMP_Text playerOneLives;
    [SerializeField] private TMP_Text playerTwoLives;
    [SerializeField] private TMP_Text timer;
    [SerializeField] private TMP_Text result;

    public void Configure(MatchController controller, TMP_Text one, TMP_Text two, TMP_Text timerText, TMP_Text resultText)
    {
        match = controller;
        playerOneLives = one;
        playerTwoLives = two;
        timer = timerText;
        result = resultText;
    }

    private void Start()
    {
        match.StateChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (match != null) match.StateChanged -= Refresh;
    }

    private void Refresh()
    {
        playerOneLives.text = "P1  " + Hearts(match.PlayerOne.HitsRemaining) + Shield(match.PlayerOne);
        playerTwoLives.text = Shield(match.PlayerTwo) + Hearts(match.PlayerTwo.HitsRemaining) + "  P2";
        int seconds = Mathf.CeilToInt(match.RemainingTime);
        timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
        if (match.IsPlaying) result.text = string.Empty;
        else result.text = match.Winner == null ? "DRAW\nR  재시작" : $"PLAYER {match.Winner.PlayerIndex} WINS\nR  재시작";
    }

    private static string Hearts(int count) => count <= 0 ? "× × ×" : string.Join(" ", new string('◆', count).ToCharArray());
    private static string Shield(PlayerCombatant player) => player.ShieldRemaining > 0
        ? $"  <color=#75E8FF><size=65%>+{player.ShieldRemaining}</size></color>  " : "";
}
