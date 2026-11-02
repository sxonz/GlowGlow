using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GlowGlow.Online
{
    public sealed class OnlineScreen : MonoBehaviour
    {
        private OnlineSession session;
        private RectTransform canvas, panel;
        private TMP_Text status, hud, room;
        private TMP_InputField code;
        private TMP_FontAsset font;
        private Button host, join, ranked, cancel, localHost, localJoin;
        private string shownStatus, shownRoom;
        private long shownPing = long.MinValue;

        private void Start()
        {
            session = GetComponent<OnlineSession>();
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var candidate in fonts)
                if (candidate.name.Contains("HiKR")) { font = candidate; break; }
            if (font == null) font = Resources.Load<TMP_FontAsset>("Online/Font");
            if (font == null) font = TMP_Settings.defaultFontAsset;
            canvas = new GameObject("Online UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
            canvas.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 100;
            canvas.gameObject.AddComponent<Image>().color = NeonUI.Ink;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Label(canvas, "GLOWGLOW / CLASSIC", .03f, .945f, .76f, .995f, 27);
            hud = Label(canvas, "", .25f, .885f, .75f, .94f, 25);
            status = Label(canvas, "", .04f, .02f, .84f, .1f, 21);
            Button(canvas, "나가기", .86f, .02f, .97f, .09f, session.Back);
            panel = Rect(canvas, "Connection", .12f, .17f, .88f, .83f);
            NeonUI.Panel(panel,NeonUI.Pink);
            NeonUI.Text(panel,"Online Eyebrow","MULTIPLAYER / ONLINE DUEL",font,16,NeonUI.Cyan,.06f,.87f,.46f,.96f);
            var title=Label(panel, "클래식 1대1", .06f, .73f, .47f, .87f, 44);
            title.alignment=TextAlignmentOptions.MidlineLeft;
            NeonUI.Art(panel,"Connection Orbit","Orbit Seal",new Color(1,.32f,.76f,.18f),.07f,.28f,.45f,.72f).preserveAspect=true;
            NeonUI.Emblem(panel,"Network",NeonUI.Pink,.18f,.40f,.34f,.61f);
            NeonUI.Text(panel,"Online Description","나만의 탄막으로 겨루는\n온라인 1대1 대전",font,24,NeonUI.Muted,.06f,.12f,.46f,.28f);
            ranked = Button(panel, "랭크 매칭", .52f, .66f, .94f, .80f, session.StartRanked);
            NeonUI.Button(ranked,NeonUI.Pink,true);
            cancel = Button(panel, "매칭 취소 / 나가기", .52f, .66f, .94f, .80f, session.Back);
            cancel.gameObject.SetActive(false);
            host = Button(panel, "방 만들기", .52f, .50f, .94f, .61f, session.HostSteam);
            var input = Rect(panel, "Room code", .52f, .33f, .79f, .44f);
            input.gameObject.AddComponent<Image>().color = new Color(.13f, .1f, .19f);
            code = input.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect(input, "Viewport", .03f, .06f, .97f, .94f);
            viewport.gameObject.AddComponent<RectMask2D>();
            code.textViewport = viewport;
            code.textComponent = (TextMeshProUGUI)Label(viewport, "", 0, 0, 1, 1, 23);
            code.placeholder = Label(viewport, "방 코드", 0, 0, 1, 1, 23);
            code.characterLimit = 20;
            code.contentType = TMP_InputField.ContentType.IntegerNumber;
            NeonUI.Input(code,NeonUI.Cyan);
            join = Button(panel, "참가", .81f, .33f, .94f, .44f, () => session.JoinSteam(code.text));
            room = Label(panel, "", .52f, .17f, .94f, .28f, 18);
            if (Application.isEditor || Debug.isDebugBuild)
            {
                localHost = Button(panel, "로컬 호스트", .52f, .055f, .72f, .145f, () => session.StartLocal(true));
                localJoin = Button(panel, "로컬 참가", .74f, .055f, .94f, .145f, () => session.StartLocal(false));
            }
            else Label(panel, "참가할 방의 코드를 입력하세요", .52f, .055f, .94f, .145f, 18);
        }
        private void Update()
        {
            if (session == null || status == null) return;
            var steam = SteamSession.Current;
            bool waitingForGuest = session.InArena && session.IsHost && session.WaitingForCombat
                && session.Match != null && !session.Match.IsDrafting && !session.Interrupted
                && steam != null && steam.Lobby.m_SteamID != 0;
            canvas.gameObject.SetActive(!session.InArena || waitingForGuest);
            if (session.InArena && !waitingForGuest) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) session.Back();
            if(shownStatus!=session.Status) { shownStatus=session.Status; status.text=shownStatus; }
            host.interactable = join.interactable = !session.HasSession;
            ranked.gameObject.SetActive(!session.IsRanked);
            ranked.interactable = !session.HasSession;
            cancel.gameObject.SetActive(session.IsRanked);
            code.interactable = !session.HasSession;
            if (localHost != null) localHost.interactable = localJoin.interactable = !session.HasSession;
            string roomText=session.IsRanked?"랭크 대전":steam!=null && steam.Lobby.m_SteamID!=0
                ? "방 코드: "+steam.Lobby.m_SteamID+"\n클릭해서 복사":session.HasSession?"상대방 연결 대기 중…":"친구와 플레이하려면 방 코드를 공유하세요";
            if(shownRoom!=roomText) { shownRoom=roomText; room.text=roomText; }
            if (!session.IsRanked && steam != null && steam.Lobby.m_SteamID != 0 && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                && RectTransformUtility.RectangleContainsScreenPoint(room.rectTransform, Mouse.current.position.ReadValue()))
                GUIUtility.systemCopyBuffer = steam.Lobby.m_SteamID.ToString();
            long ping=session.Connected?session.Ping:-1;
            if(shownPing!=ping) { shownPing=ping; hud.text=ping>=0?$"CLASSIC · {ping} ms":""; }
        }
        private static RectTransform Rect(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private TMP_Text Label(Transform parent, string text, float x0, float y0, float x1, float y1, float size)
        {
            var label = Rect(parent, "Label", x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = size * .7f; label.fontSizeMax = size;
            return label;
        }
        private Button Button(Transform parent, string text, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(parent, text, x0, y0, x1, y1);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.25f, .08f, .37f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(action);
            NeonUI.Button(button,NeonUI.Cyan);
            Label(rect, text, .03f, .05f, .97f, .95f, 23);
            return button;
        }
    }
}
