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
    }

    [SerializeField] private PlayerCombatant player;
    [SerializeField] private TMP_Text status;
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private Slot[] slots;

    public void Configure(PlayerCombatant owner, TMP_Text statusLabel, Sprite defaultIcon, Slot[] views)
    {
        player = owner;
        status = statusLabel;
        fallbackIcon = defaultIcon;
        slots = views;
        Refresh();
    }

    private void Start()
    {
        for (int i = 0; i < slots.Length; i++)
        {
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
        string ownerLabel = $"P{(player != null ? player.PlayerIndex : 1)}";
        status.text = count > 0 ? $"{ownerLabel}  ·  장착 탄막 {count}/3   |   숫자 1–3 / 휠 / 클릭으로 선택" :
            ownerLabel + "  ·  시작 탄막 선택 대기";
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            slot.button.gameObject.SetActive(i < WeaponHand.SlotCount);
            var weapon = i < count ? hand.Drawn[i] : i == 0 ? basic : null;
            bool selected = weapon != null && (basic != null || hand.SelectedIndex == i);
            if (slot.glow != null) slot.glow.color = new Color(1, .3f, .8f, selected ? .22f : 0);
            slot.button.interactable = weapon != null && player.CanSelectWeapon;
            slot.selection.effectColor = selected ? new Color(1, .3f, .8f, 1) : new Color(.5f, .35f, .65f, .4f);
            slot.selection.effectDistance = selected ? new Vector2(3, -3) : new Vector2(1, -1);
            slot.icon.sprite = weapon?.Definition.icon != null ? weapon.Definition.icon : fallbackIcon;
            slot.icon.color = weapon != null ? weapon.Definition.color : new Color(.55f, .45f, .65f, .25f);
            slot.name.text = weapon != null ? weapon.Definition.displayName : "빈 슬롯";
            slot.name.color = weapon != null ? weapon.Definition.RarityColor : new Color(.6f, .53f, .68f);
            slot.cooldownClock.fillAmount = weapon?.CooldownFraction ?? 0;
            float remaining = weapon?.CooldownRemaining ?? 0;
            slot.remaining.text = weapon == null ? "—" : remaining > .01f ? $"{remaining:0.0}s" : "READY";
            slot.remaining.color = remaining > .01f ? new Color(1, .65f, .85f) : new Color(.68f, .95f, .9f);
        }
    }
}
