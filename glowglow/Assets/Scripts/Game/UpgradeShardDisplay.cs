using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Three independent upgrade shards. Artwork is a pre-imported PNG.</summary>
public sealed class UpgradeShardDisplay : MonoBehaviour
{
    public const int ShardCount = 3;
    public static readonly Color LockedColor = new Color(.3f, .31f, .35f, 1f);
    private readonly Image[] shards = new Image[ShardCount];
    private Image weaponIcon;
    private WeaponRuntime target;
    private IReadOnlyList<WeaponUpgradeDefinition> definitions;

    public void Show(WeaponRuntime weapon, IReadOnlyList<WeaponUpgradeDefinition> upgrades)
    {
        Unsubscribe();
        target = weapon;
        definitions = upgrades;
        if (shards[0] == null) Build();
        if (target != null) target.Upgrades.Changed += Refresh;
        Refresh();
    }

    private void Build()
    {
        var sprite = Resources.Load<Sprite>("GameplaySprites/UpgradeShard");
        if (sprite == null) throw new System.InvalidOperationException("Missing UpgradeShard PNG sprite.");
        for (int i = 0; i < ShardCount; i++)
        {
            var part = new GameObject("Upgrade Shard " + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            part.transform.SetParent(transform, false);
            part.rectTransform.sizeDelta = new Vector2(128, 128);
            part.rectTransform.localRotation = Quaternion.Euler(0, 0, -120f * i);
            part.sprite = sprite;
            part.raycastTarget = false;
            shards[i] = part;
        }
        weaponIcon = new GameObject("Weapon Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        weaponIcon.transform.SetParent(transform, false);
        weaponIcon.rectTransform.sizeDelta = new Vector2(28, 28);
        weaponIcon.preserveAspect = true;
        weaponIcon.raycastTarget = false;
    }

    private void Refresh()
    {
        for (int i = 0; i < ShardCount; i++) shards[i].color = LockedColor;
        if (target == null) return;
        weaponIcon.sprite = target.Definition.icon != null ? target.Definition.icon : RuntimeShapes.Circle;
        weaponIcon.color = target.Definition.color;
        if (definitions == null) return;
        foreach (var upgrade in definitions)
        {
            if (upgrade == null || !upgrade.CanApplyTo(target.Definition) || upgrade.displaySlot < 0 || upgrade.displaySlot >= ShardCount) continue;
            if (target.Upgrades.GetLevel(upgrade) > 0)
                shards[upgrade.displaySlot].color = target.Definition.RarityColor;
        }
    }

    private void Unsubscribe()
    {
        if (target != null) target.Upgrades.Changed -= Refresh;
    }
    private void OnDestroy() => Unsubscribe();
}
