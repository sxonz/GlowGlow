using TMPro;
using UnityEngine;

public sealed class ArenaHud : MonoBehaviour
{
    [SerializeField] private MatchController match;
    [SerializeField] private TMP_Text playerOneLives;
    [SerializeField] private TMP_Text playerTwoLives;
    [SerializeField] private TMP_Text timer;
    [SerializeField] private TMP_Text result;
    private bool botHelpShown;

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

    private void Update()
    {
        if (match != null && match.IsOnline) Refresh();
    }

    private void Refresh()
    {
        if ((match.IsBotMatch || match.IsOnline) && !botHelpShown)
        {
            var controls = transform.Find("Controls")?.GetComponent<TMP_Text>();
            if (controls != null) controls.text = "WASD 이동 · MOUSE 조준 / 발사 · SPACE 대시 · 1–3 / 휠 탄막 선택 · ESC 나가기";
            botHelpShown = true;
        }
        playerOneLives.text = "P1  " + Hearts(match.PlayerOne.HitsRemaining) + Shield(match.PlayerOne);
        playerTwoLives.text = Shield(match.PlayerTwo) + Hearts(match.PlayerTwo.HitsRemaining) +
            (match.IsBotMatch ? $"  <size=65%>BOT · {match.BotRating}</size>" : "  P2");
        int seconds = Mathf.CeilToInt(match.RemainingTime);
        timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
        if (match.IsPlaying || match.IsDrafting) result.text = string.Empty;
        else result.text = match.Winner == null ? "DRAW\nR  재시작" :
            match.IsBotMatch ? (match.Winner == match.PlayerOne ? "VICTORY" : "BOT WINS") + "\nR  재시작" :
            $"PLAYER {match.Winner.PlayerIndex} WINS\nR  재시작";
        if (match.IsOnline)
        {
            var session = GlowGlow.Online.OnlineSession.Current;
            timer.text += $"  <size=55%>{session.Ping} ms · 나 P{session.LocalSlot + 1}</size>";
            if (session.WaitingForCombat) result.text = "<size=60%>" + session.Status + "</size>";
            if (!session.WaitingForCombat && !match.IsPlaying && !match.IsDrafting)
                result.text = session.Interrupted ? session.Status + "\nESC 나가기"
                    : result.text.Replace("R  재시작", "R  재대전 동의 · ESC 나가기") + "\n<size=60%>" + session.Status + "</size>";
        }
    }

    private static string Hearts(int count) => count <= 0 ? "× × ×" : string.Join(" ", new string('◆', count).ToCharArray());
    private static string Shield(PlayerCombatant player) => player.ShieldRemaining > 0
        ? $"  <color=#75E8FF><size=65%>+{player.ShieldRemaining}</size></color>  " : "";
}
