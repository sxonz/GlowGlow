using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public sealed class ArenaHud : MonoBehaviour
{
    [SerializeField] private MatchController match;
    [SerializeField] private TMP_Text playerOneLives;
    [SerializeField] private TMP_Text playerTwoLives;
    [SerializeField] private TMP_Text timer;
    [SerializeField] private TMP_Text result;
    private bool botHelpShown;
    private int shownOneHealth = -1, shownTwoHealth = -1, shownOneShield = -1, shownTwoShield = -1;
    private int shownSeconds = -1, shownSlot = -2, shownBotRating = -1;
    private long shownPing = -1;
    private int shownFps = -1;
    private bool shownOnline, hasResult;
    private bool shownPlaying, shownDrafting, shownWaiting, shownInterrupted, shownRanked;
    private PlayerCombatant shownWinner;
    private string shownStatus;
    private Image oneHealthFill, twoHealthFill;
    private GameObject resultCard;
    private Button rematchButton;

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
        ApplyVisualTheme();
        match.StateChanged += Refresh;
        Refresh();
    }

    private void ApplyVisualTheme()
    {
        var top=transform.Find("Top Bar") as RectTransform;
        if(top!=null)
        {
            top.GetComponent<Image>().color=NeonUI.Ink;
            oneHealthFill=HealthPlate(top,"P1 Health Plate",playerOneLives,NeonUI.Pink,.025f,.385f,false);
            twoHealthFill=HealthPlate(top,"P2 Health Plate",playerTwoLives,NeonUI.Cyan,.615f,.975f,true);
            NeonUI.Place(timer.rectTransform,.425f,.20f,.575f,.92f);
            timer.fontSize=timer.fontSizeMax=36;
            NeonUI.Emblem(top,"Clock",NeonUI.Muted,.395f,.30f,.425f,.80f);
        }
        var rect=NeonUI.Rect(transform,"Match Result Card",.29f,.29f,.71f,.73f);
        resultCard=rect.gameObject;
        NeonUI.Panel(rect,NeonUI.Violet);
        NeonUI.Text(rect,"Result Eyebrow","MATCH / RESULT",result.font,17,NeonUI.Cyan,.10f,.85f,.90f,.94f,TextAlignmentOptions.Center);
        NeonUI.Emblem(rect,"Star",NeonUI.Violet,.42f,.61f,.58f,.84f);
        result.transform.SetParent(rect,false);
        NeonUI.Place(result.rectTransform,.065f,.26f,.935f,.65f);
        result.fontSize=result.fontSizeMax=52;
        result.enableAutoSizing=true; result.fontSizeMin=22;
        var outline=result.GetComponent<Outline>(); if(outline!=null) outline.enabled=false;
        rematchButton=ResultButton(rect,"다시 대전 · R",.08f,.11f,.52f,.24f,()=>match.RestartMatch());
        ResultButton(rect,"나가기 · ESC",.55f,.11f,.92f,.24f,()=>
        {
            if(match.IsOnline) GlowGlow.Online.OnlineSession.Current.Back(); else SceneManager.LoadScene("Title");
        });
        resultCard.SetActive(false);
    }

    private static Image HealthPlate(RectTransform top,string name,TMP_Text label,Color accent,float left,float right,bool reverse)
    {
        var plate=NeonUI.Rect(top,name,left,.04f,right,.96f);
        NeonUI.Panel(plate,accent);
        plate.SetAsFirstSibling();
        label.transform.SetParent(plate,false);
        NeonUI.Place(label.rectTransform,reverse ? .06f : .15f,.32f,reverse ? .85f : .94f,.94f);
        label.fontSize=label.fontSizeMax=27; label.color=NeonUI.White;
        NeonUI.Emblem(plate,"Health",accent,reverse ? .86f : .035f,.40f,reverse ? .97f : .14f,.88f);
        NeonUI.Art(plate,"Health Rail","Rule",new Color(.26f,.29f,.4f,1),.08f,.12f,.92f,.25f);
        var fill=NeonUI.Art(plate,"Health Fill","Rule",accent,.08f,.12f,.92f,.25f);
        fill.type=Image.Type.Filled; fill.fillMethod=Image.FillMethod.Horizontal;
        fill.fillOrigin=reverse?1:0;
        return fill;
    }

    private Button ResultButton(Transform parent,string text,float x0,float y0,float x1,float y1,UnityEngine.Events.UnityAction action)
    {
        var rect=NeonUI.Rect(parent,text,x0,y0,x1,y1);
        var button=rect.gameObject.AddComponent<Button>();
        NeonUI.Button(button,NeonUI.Violet,text.StartsWith("다시"));
        NeonUI.Text(rect,"Label",text,result.font,21,NeonUI.White,.05f,.05f,.95f,.95f,TextAlignmentOptions.Center);
        button.onClick.AddListener(action);
        return button;
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
        int oneHealth = match.PlayerOne.Health, twoHealth = match.PlayerTwo.Health;
        int oneShield = match.PlayerOne.ShieldRemaining, twoShield = match.PlayerTwo.ShieldRemaining;
        if (shownOneHealth != oneHealth || shownOneShield != oneShield)
        {
            shownOneHealth = oneHealth; shownOneShield = oneShield;
            if(oneHealthFill!=null) oneHealthFill.fillAmount=oneHealth/(float)PlayerCombatant.MaxHealth;
            playerOneLives.text = $"P1  HP {oneHealth}/{PlayerCombatant.MaxHealth}" + Shield(match.PlayerOne);
        }
        if (shownTwoHealth != twoHealth || shownTwoShield != twoShield || shownBotRating != match.BotRating)
        {
            shownTwoHealth = twoHealth; shownTwoShield = twoShield; shownBotRating = match.BotRating;
            if(twoHealthFill!=null) twoHealthFill.fillAmount=twoHealth/(float)PlayerCombatant.MaxHealth;
            playerTwoLives.text = Shield(match.PlayerTwo) + $"HP {twoHealth}/{PlayerCombatant.MaxHealth}" +
                (match.IsBotMatch ? $"  <size=65%>BOT · {match.BotRating}</size>" : "  P2");
        }
        var session = match.IsOnline ? GlowGlow.Online.OnlineSession.Current : null;
        int seconds = Mathf.CeilToInt(match.RemainingTime);
        long ping = session?.Ping ?? 0;
        int slot = session?.LocalSlot ?? -1;
        int fps = session?.FramesPerSecond ?? 0;
        if (shownSeconds != seconds || shownPing != ping || shownSlot != slot || shownOnline != match.IsOnline || shownFps != fps)
        {
            shownFps = fps;
            shownSeconds = seconds; shownPing = ping; shownSlot = slot; shownOnline = match.IsOnline;
            string clock = $"{seconds / 60:00}:{seconds % 60:00}";
            if (session != null) clock += $"  <size=55%>{ping} ms · 나 P{slot + 1}</size>";
            if (session != null && Debug.isDebugBuild && fps > 0) clock += $"  <size=55%>{fps} FPS</size>";
            timer.text = clock;
        }
        bool waiting = session?.WaitingForCombat ?? false, interrupted = session?.Interrupted ?? false;
        bool ranked = session?.IsRanked ?? false;
        string status = session?.Status;
        if (hasResult && shownPlaying == match.IsPlaying && shownDrafting == match.IsDrafting
            && shownWinner == match.Winner && shownWaiting == waiting && shownInterrupted == interrupted
            && shownRanked == ranked && shownStatus == status) return;
        hasResult = true; shownPlaying = match.IsPlaying; shownDrafting = match.IsDrafting;
        shownWinner = match.Winner; shownWaiting = waiting; shownInterrupted = interrupted;
        shownRanked = ranked; shownStatus = status;
        string message;
        if (match.IsPlaying || match.IsDrafting) message = string.Empty;
        else message = match.Winner == null ? "DRAW" :
            match.IsBotMatch ? (match.Winner == match.PlayerOne ? "VICTORY" : "DEFEAT") :
            $"PLAYER {match.Winner.PlayerIndex} WINS";
        if (session != null)
        {
            if (session.WaitingForCombat) message = "<size=60%>" + session.Status + "</size>";
            if (!session.WaitingForCombat && !match.IsPlaying && !match.IsDrafting)
                message = session.Interrupted ? session.Status
                    : message + "\n<size=60%>" + session.Status + "</size>";
        }
        result.text = message;
        if(resultCard!=null)
        {
            resultCard.SetActive(!string.IsNullOrEmpty(message));
            rematchButton.gameObject.SetActive(!ranked && !interrupted && !waiting);
        }
    }

    private static string Shield(PlayerCombatant player) => player.ShieldRemaining > 0
        ? $"  <color=#75E8FF><size=65%>+{player.ShieldRemaining}</size></color>  " : "";
}
