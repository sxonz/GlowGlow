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
        private Button host, join;

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
            canvas.gameObject.AddComponent<Image>().color = new Color(.025f, .01f, .055f, .98f);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Label(canvas, "GLOWGLOW / CLASSIC", .03f, .945f, .76f, .995f, 27);
            hud = Label(canvas, "", .25f, .885f, .75f, .94f, 25);
            status = Label(canvas, "", .04f, .02f, .84f, .1f, 21);
            Button(canvas, "나가기", .86f, .02f, .97f, .09f, session.Back);
            panel = Rect(canvas, "Connection", .27f, .2f, .73f, .82f);
            panel.gameObject.AddComponent<Image>().color = new Color(.045f, .025f, .09f, .97f);
            Label(panel, "친구와 1대1", .06f, .83f, .94f, .97f, 35);
            Label(panel, "각자 덱에서 탄막·업그레이드 3회 선택\n기존 클래식 규칙으로 1대1 대전", .06f, .66f, .94f, .83f, 21);
            host = Button(panel, "Steam 방 만들기", .07f, .52f, .93f, .64f, session.HostSteam);
            var input = Rect(panel, "Room code", .07f, .36f, .65f, .48f);
            input.gameObject.AddComponent<Image>().color = new Color(.13f, .1f, .19f);
            code = input.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect(input, "Viewport", .03f, .06f, .97f, .94f);
            viewport.gameObject.AddComponent<RectMask2D>();
            code.textViewport = viewport;
            code.textComponent = (TextMeshProUGUI)Label(viewport, "", 0, 0, 1, 1, 23);
            code.placeholder = Label(viewport, "방 코드", 0, 0, 1, 1, 23);
            code.characterLimit = 20;
            code.contentType = TMP_InputField.ContentType.IntegerNumber;
            join = Button(panel, "참가", .68f, .36f, .93f, .48f, () => session.JoinSteam(code.text));
            room = Label(panel, "", .07f, .21f, .93f, .34f, 18);
            if (Application.isEditor || Debug.isDebugBuild)
            {
                Button(panel, "로컬 호스트", .07f, .07f, .48f, .18f, () => session.StartLocal(true));
                Button(panel, "로컬 참가", .52f, .07f, .93f, .18f, () => session.StartLocal(false));
            }
            else Label(panel, "친구가 만든 방의 코드를 입력하세요", .07f, .07f, .93f, .18f, 18);
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
            status.text = session.Status;
            host.interactable = join.interactable = !session.HasSession;
            if (steam != null && steam.Lobby.m_SteamID != 0)
                room.text = "방 코드: " + steam.Lobby.m_SteamID + "\n클릭해서 복사";
            else room.text = session.HasSession ? "상대방 연결 대기 중…" : "";
            if (steam != null && steam.Lobby.m_SteamID != 0 && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                && RectTransformUtility.RectangleContainsScreenPoint(room.rectTransform, Mouse.current.position.ReadValue()))
                GUIUtility.systemCopyBuffer = steam.Lobby.m_SteamID.ToString();
            hud.text = session.Connected ? $"CLASSIC · {session.Ping} ms" : "";
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
            Label(rect, text, .03f, .05f, .97f, .95f, 23);
            return button;
        }
    }
}
