using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Three independent diamond nodes surrounding the equipped barrage.</summary>
public sealed class UpgradeShardDisplay : MonoBehaviour
{
    public const int ShardCount = 3;
    public static readonly Color LockedColor = new Color(.3f, .31f, .35f, 1f);
    public static readonly Color OfferedColor = new Color(.2f, 1f, .38f, 1f);
    private readonly UpgradeDiamondGraphic[] shards = new UpgradeDiamondGraphic[ShardCount];
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
            var part = new GameObject("Upgrade Diamond " + i, typeof(RectTransform), typeof(UpgradeDiamondGraphic))
                .GetComponent<UpgradeDiamondGraphic>();
            part.transform.SetParent(transform, false);
            part.rectTransform.sizeDelta = new Vector2(128, 128);
            part.NodePosition = position;
            part.raycastTarget = false;
            shards[i] = part;
        }
        var rim = MakeImage("Core Rim", Vector2.zero, Vector2.one * 41, RuntimeShapes.Circle);
        rim.color = new Color(.58f, .66f, .78f);
        var core = MakeImage("Core", Vector2.zero, Vector2.one * 38, RuntimeShapes.Circle);
        core.color = new Color(.045f, .025f, .085f);
        weaponIcon = MakeImage("Weapon Icon", Vector2.zero, Vector2.one * 26, null);
    }

    private Image MakeImage(string label, Vector2 position, Vector2 size, Sprite sprite)
    {
        var result = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        result.transform.SetParent(transform, false);
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
        weaponIcon.sprite = target.Definition.icon != null ? target.Definition.icon : RuntimeShapes.Circle;
        weaponIcon.color = target.Definition.color;
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
        shards[slot].SetState(tint, lit);
        glows[slot].enabled = lit;
        glows[slot].color = new Color(tint.r, tint.g, tint.b, .48f);
    }

    private void Unsubscribe()
    {
        if (target != null) target.Upgrades.Changed -= Refresh;
    }
    private void OnDestroy() => Unsubscribe();
}

/// <summary>Resolution-independent facets and neon strokes, rendered by the UI canvas.</summary>
public sealed class UpgradeDiamondGraphic : MaskableGraphic
{
    public Vector2 NodePosition { get; set; }
    private bool lit;

    public void SetState(Color tint, bool illuminated)
    {
        color = tint;
        lit = illuminated;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Vector2 p = NodePosition;
        Vector2[] corners = { p + new Vector2(0, 16), p + new Vector2(12, 0),
            p + new Vector2(0, -16), p + new Vector2(-12, 0) };
        Stroke(mesh, p.normalized * 20, p, 1.2f, color);
        for (int i = 0; i < 4; i++)
        {
            Color facet = color * (i == 0 ? .75f : i == 1 ? .32f : i == 2 ? .45f : .95f);
            facet.a = 1;
            Triangle(mesh, p, corners[i], corners[(i + 1) % 4], facet);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = corners[i], b = corners[(i + 1) % 4];
            Vector2 outerA = p + (a - p) * 1.2f, outerB = p + (b - p) * 1.2f;
            if (lit)
            {
                Stroke(mesh, outerA, outerB, 4.5f, new Color(color.r, color.g, color.b, .12f));
                Stroke(mesh, outerA, outerB, 2.8f, new Color(color.r, color.g, color.b, .24f));
            }
            Stroke(mesh, outerA, outerB, 1.1f, color);
            Color edge = Color.Lerp(color, Color.white, lit ? .55f : .12f);
            Stroke(mesh, a, b, .65f, edge);
            edge.a = .5f;
            Stroke(mesh, p, a, .55f, edge);
        }
    }

    private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int first = mesh.currentVertCount;
        mesh.AddVert(a, tint, Vector2.zero);
        mesh.AddVert(b, tint, Vector2.zero);
        mesh.AddVert(c, tint, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2);
    }

    private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * (width * .5f);
        int first = mesh.currentVertCount;
        mesh.AddVert(a - n, tint, Vector2.zero);
        mesh.AddVert(a + n, tint, Vector2.zero);
        mesh.AddVert(b + n, tint, Vector2.zero);
        mesh.AddVert(b - n, tint, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2);
        mesh.AddTriangle(first, first + 2, first + 3);
    }
}
