using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DrawnWeaponBar : MonoBehaviour
{
    [Serializable]
    public sealed class Slot
    {
        public Button button;
        public Image icon;
        public Image cooldownClock;
        public Outline selection;
        public TMP_Text name;
        public TMP_Text remaining;
        [NonSerialized] public Image glow;
        [NonSerialized] internal Image frame;
        [NonSerialized] internal WeaponRuntime shownWeapon;
        [NonSerialized] internal bool cached, selected;
        [NonSerialized] internal int shownTenths = int.MinValue;
        [NonSerialized] internal float readyAt = float.NegativeInfinity;
    }

    [SerializeField] private PlayerCombatant player;
    [SerializeField] private TMP_Text status;
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private Slot[] slots;
    private int shownPlayer = -1, shownCount = -1;

    public void Configure(PlayerCombatant owner, TMP_Text statusLabel, Sprite defaultIcon, Slot[] views)
    {
        player = owner;
        status = statusLabel;
        fallbackIcon = defaultIcon;
        slots = views;
        shownPlayer = shownCount = -1;
        foreach (var slot in slots) { slot.cached = false; slot.shownTenths = int.MinValue; }
        Refresh();
    }

    private void Start()
    {
        NeonUI.Panel((RectTransform)transform,NeonUI.Violet);
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].frame=NeonUI.Button(slots[i].button,NeonUI.Violet);
            slots[i].cached=false;
            if (i < WeaponHand.SlotCount)
            {
                var rect = slots[i].button.GetComponent<RectTransform>();
                float width = .95f / WeaponHand.SlotCount;
                rect.anchorMin = new Vector2(.025f + i * width, .07f);
                rect.anchorMax = new Vector2(.025f + (i + 1) * width - .015f, .78f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var glow = new GameObject("Selection Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            glow.transform.SetParent(slots[i].button.transform, false);
            glow.transform.SetAsFirstSibling();
            glow.sprite = RuntimeShapes.SoftGlow;
            glow.raycastTarget = false;
            glow.rectTransform.anchorMin = Vector2.zero;
            glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.offsetMin = new Vector2(-18, -12);
            glow.rectTransform.offsetMax = new Vector2(18, 12);
            slots[i].glow = glow;
            int index = i;
            slots[i].button.onClick.AddListener(() => { player.SelectWeaponSlot(index); Refresh(); });
        }
        Refresh();
    }

    private void Update() => Refresh();

    public void Refresh()
    {
        var online = GlowGlow.Online.OnlineSession.Current;
        if (online != null && online.Match != null) player = online.LocalPlayer;
        var hand = player != null ? player.Hand : null;
        int count = hand?.Drawn.Count ?? 0;
        var basic = count == 0 && player != null ? player.CurrentWeapon : null;
        int playerIndex = player != null ? player.PlayerIndex : 1;
        if (shownPlayer != playerIndex || shownCount != count)
        {
            shownPlayer = playerIndex; shownCount = count;
            status.text = count > 0 ? $"P{playerIndex}  ·  장착 탄막 {count}/3   |   숫자 1–3 / 휠 / 클릭으로 선택"
                : $"P{playerIndex}  ·  시작 탄막 선택 대기";
        }
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.button.gameObject.activeSelf != (i < WeaponHand.SlotCount))
                slot.button.gameObject.SetActive(i < WeaponHand.SlotCount);
            var weapon = i < count ? hand.Drawn[i] : i == 0 ? basic : null;
            bool selected = weapon != null && (basic != null || hand.SelectedIndex == i);
            slot.button.interactable = weapon != null && player.CanSelectWeapon;
            if (!slot.cached || slot.selected != selected || slot.shownWeapon != weapon)
            {
                if(slot.frame!=null) NeonUI.Selection(slot.button,selected,weapon?.Definition.color??NeonUI.Violet);
                slot.selection.effectColor = selected ? new Color(1, .3f, .8f, 1) : new Color(.5f, .35f, .65f, .4f);
                slot.selection.effectDistance = selected ? new Vector2(3, -3) : new Vector2(1, -1);
                slot.icon.sprite = weapon != null ? weapon.Definition.icon != null ? weapon.Definition.icon : fallbackIcon : NeonUI.Asset("Deck");
                slot.icon.color = weapon != null ? weapon.Definition.icon != null ? Color.white : weapon.Definition.color
                    : new Color(.55f, .45f, .65f, .25f);
                slot.icon.preserveAspect = true;
                slot.name.text = weapon != null ? weapon.Definition.displayName : "빈 슬롯";
                slot.name.color = weapon != null ? NeonUI.White : NeonUI.Muted;
                if (slot.shownWeapon != weapon) { slot.shownTenths = int.MinValue; slot.readyAt = float.NegativeInfinity; }
                slot.cached = true; slot.selected = selected; slot.shownWeapon = weapon;
            }
            slot.cooldownClock.fillAmount = weapon?.CooldownFraction ?? 0;
            float remaining = weapon?.CooldownRemaining ?? 0;
            int tenths = weapon == null ? -1 : remaining > .01f ? Mathf.Max(1, Mathf.CeilToInt(remaining * 10)) : 0;
            if (slot.shownTenths != tenths)
            {
                if (tenths == 0 && slot.shownTenths > 0) slot.readyAt = Time.unscaledTime;
                slot.shownTenths = tenths;
                if (tenths > 0) slot.remaining.SetText("{0:1}s", tenths * .1f);
                else slot.remaining.text = tenths < 0 ? "—" : "READY";
                slot.remaining.color = tenths > 0 ? new Color(1, .65f, .85f) : new Color(.68f, .95f, .9f);
            }
            if (slot.glow != null)
            {
                float pulse = GraphicsTheme.IsClassic ? 0 : Mathf.Clamp01(1 - (Time.unscaledTime - slot.readyAt) / .32f);
                slot.glow.color = Color.Lerp(new Color(1, .3f, .8f, selected ? .22f : 0), new Color(.3f, 1, .9f, .5f), pulse * pulse);
            }
        }
    }
}
