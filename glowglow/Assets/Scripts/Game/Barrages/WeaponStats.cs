using UnityEngine;

/// <summary>Immutable snapshot; later upgrades do not change existing projectiles.</summary>
public readonly struct WeaponStats
{
    public float Cooldown { get; }
    public float Speed { get; }
    public float Lifetime { get; }
    public float Radius { get; }
    public float Range { get; }
    public Color Color { get; }
    public int Damage { get; }

    public WeaponStats(float cooldown, float speed, float lifetime, float radius, float range, Color color, int damage = BarrageDamage.Basic)
    {
        Cooldown = Mathf.Max(.05f, cooldown);
        Speed = Mathf.Max(1f, speed);
        Lifetime = Mathf.Max(.1f, lifetime);
        Radius = Mathf.Max(.02f, radius);
        Range = Mathf.Max(.1f, range);
        Color = color;
        Damage = Mathf.Max(0, damage);
    }

    public WeaponStats WithDamage(int damage) => new(Cooldown, Speed, Lifetime, Radius, Range, Color, damage);
}
