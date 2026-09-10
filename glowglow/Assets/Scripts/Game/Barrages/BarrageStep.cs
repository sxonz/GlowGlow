using System;
using UnityEngine;

public enum BarrageShape { Bullet, Line, Circle, Box, ElectricPulse, Overdrive, Bouncer, OrbitOrb }
public enum BarragePosition { Gun, Cursor, Player }

/// <summary>One event on a firing timeline. Values are copied when firing.</summary>
[Serializable]
public sealed class BarrageStep
{
    public BarrageShape shape;
    public BarragePosition position;
    [Tooltip("Follow the live muzzle and limit its turning to 30 degrees/second for this attack.")]
    public bool followMuzzle;
    [Tooltip("Sample muzzle position and direction for each emission, then apply offsetDegrees. No following after launch or aim limits.")]
    public bool fireFromCurrentMuzzle;
    [Min(0)] public float delay;
    public Vector2 offsetDegrees;
    public Vector2 speedMultiplier = Vector2.one;
    public Vector2 dimensions = Vector2.one;
    public bool dealsDamage = true;
    [Range(0, 1)] public float opacity = 1;
    [Tooltip("Zero: bullets last until off screen. Stationary shapes need a duration.")]
    [Min(0)] public float duration;
    public float startSize = 1;
    public float peakSize = 1;
    public float endSize = 1;
    [Min(0)] public float growTime;
    [Min(0)] public float shrinkTime;
    public bool flash;
    [Tooltip("Total rotation over duration, with sine ease-in/out angular speed. Zero disables rotation.")]
    public float rotationDegrees;
    [Min(0)] public float knockbackDistance;
    [Range(0, 1)] public float slowMultiplier = 1;
    [Min(0)] public float slowDuration;
    [NonSerialized] public bool interceptsBullet;
    [NonSerialized] public bool overridePosition;
    [NonSerialized] public Vector2 worldPosition;

    public BarrageStep Snapshot() => (BarrageStep)MemberwiseClone();

    public float RotationAt(float age)
    {
        float progress = Mathf.Clamp01(age / Mathf.Max(.0001f, duration));
        return rotationDegrees * .5f * (1f - Mathf.Cos(Mathf.PI * progress));
    }

    public float SizeAt(float age)
    {
        if (growTime > 0 && age < growTime) return Mathf.Lerp(startSize, peakSize, age / growTime);
        if (shrinkTime > 0) return Mathf.Lerp(peakSize, endSize, Mathf.Clamp01((age - growTime) / shrinkTime));
        return growTime > 0 ? peakSize : startSize;
    }
}
