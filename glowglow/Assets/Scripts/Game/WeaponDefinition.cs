using UnityEngine;

[CreateAssetMenu(menuName = "GlowGlow/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    public string displayName = "Pulse Shot";
    [Min(.05f)] public float cooldown = .22f;
    [Min(1f)] public float projectileSpeed = 12f;
    [Min(.1f)] public float projectileLifetime = 3f;
    [Min(.02f)] public float projectileRadius = .13f;
    public Color color = new Color(1f, .15f, .8f, 1f);
}
