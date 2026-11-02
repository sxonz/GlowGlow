using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Three independent diamond nodes surrounding the equipped barrage.</summary>
public sealed class UpgradeShardDisplay : MonoBehaviour
{
    public const int ShardCount = 3;
    public static readonly Color LockedColor = new Color(.3f, .31f, .35f, 1f);
    public static readonly Color OfferedColor = new Color(.2f, 1f, .38f, 1f);
    private readonly Image[] shards = new Image[ShardCount];
    private readonly Image[] glows = new Image[ShardCount];
    private Image weaponIcon;
    private WeaponRuntime target;
    private WeaponUpgradeDefinition offered;
    private IReadOnlyList<WeaponUpgradeDefinition> definitions;

    public void Show(WeaponRuntime weapon, IReadOnlyList<WeaponUpgradeDefinition> upgrades,
        WeaponUpgradeDefinition offeredUpgrade = null)
    {
        Unsubscribe();
        target = weapon;
        definitions = upgrades;
        offered = offeredUpgrade;
        if (shards[0] == null) Build();
        if (target != null) target.Upgrades.Changed += Refresh;
        Refresh();
    }

    private void Build()
    {
        for (int i = 0; i < ShardCount; i++)
        {
            float angle = (90 + 120 * i) * Mathf.Deg2Rad;
            var position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 41;
            glows[i] = MakeImage("Node Glow " + i, position, new Vector2(49, 55), RuntimeShapes.SoftGlow);
            shards[i] = MakeImage("Upgrade Diamond " + i, position, new Vector2(32, 40), RuntimeShapes.UpgradeDiamond);
        }
        var rim = MakeImage("Core Rim", Vector2.zero, Vector2.one * 41, RuntimeShapes.UICircle);
        rim.color = new Color(.58f, .66f, .78f);
        var core = MakeImage("Core", Vector2.zero, Vector2.one * 38, RuntimeShapes.UICircle);
        core.color = new Color(.045f, .025f, .085f);
        weaponIcon = MakeImage("Weapon Icon", Vector2.zero, Vector2.one * 26, null);
    }

    private Image MakeImage(string label, Vector2 position, Vector2 size, Sprite sprite)
    {
        var result = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        result.transform.SetParent(transform, false);
        result.rectTransform.anchorMin = result.rectTransform.anchorMax = new Vector2(.5f, .5f);
        result.rectTransform.anchoredPosition = position;
        result.rectTransform.sizeDelta = size;
        result.sprite = sprite;
        result.preserveAspect = true;
        result.raycastTarget = false;
        return result;
    }

    private void Refresh()
    {
        for (int i = 0; i < ShardCount; i++) SetState(i, LockedColor, false);
        weaponIcon.enabled = target != null;
        if (target == null) return;
        weaponIcon.sprite = target.Definition.icon != null ? target.Definition.icon : RuntimeShapes.UICircle;
        weaponIcon.color = target.Definition.icon != null ? Color.white : target.Definition.color;
        if (definitions == null) return;
        foreach (var upgrade in definitions)
        {
            if (upgrade == null || !upgrade.CanApplyTo(target.Definition) || upgrade.displaySlot < 0 || upgrade.displaySlot >= ShardCount) continue;
            bool acquired = target.Upgrades.GetLevel(upgrade) > 0;
            bool highlighted = upgrade == offered;
            SetState(upgrade.displaySlot, acquired ? target.Definition.RarityColor : highlighted ? OfferedColor : LockedColor,
                acquired || highlighted);
        }
    }

    private void SetState(int slot, Color tint, bool lit)
    {
        shards[slot].sprite = lit ? RuntimeShapes.UpgradeDiamondLit : RuntimeShapes.UpgradeDiamond;
        shards[slot].color = tint;
        glows[slot].enabled = lit;
        glows[slot].color = new Color(tint.r, tint.g, tint.b, .48f);
    }

    private void Unsubscribe()
    {
        if (target != null) target.Upgrades.Changed -= Refresh;
    }
    private void OnDestroy() => Unsubscribe();
}
