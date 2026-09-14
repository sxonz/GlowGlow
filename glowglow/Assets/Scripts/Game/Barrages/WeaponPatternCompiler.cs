using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds an independent firing plan. Assets and other players' plans are never mutated.</summary>
public static class WeaponPatternCompiler
{
    public static BarrageStep[] Build(BarrageStep[] source, WeaponEffects effects, float chainRoll, Vector2 chainPosition)
    {
        var steps = new List<BarrageStep>();
        foreach (var step in source) if (step != null) steps.Add(step.Snapshot());
        if (effects.Has(WeaponEffects.LaserQuickWarning | WeaponEffects.BombQuickWarning))
        {
            var warning = steps.Find(s => !s.dealsDamage && s.duration > 0 && s.delay == 0 &&
                (s.shape == BarrageShape.Line || s.shape == BarrageShape.Circle));
            if (warning != null)
            {
                float original = warning.duration;
                warning.duration *= .75f;
                warning.growTime *= .75f;
                warning.shrinkTime *= .75f;
                foreach (var step in steps) if (step != warning && step.delay >= original) step.delay -= original * .25f;
            }
        }
        if (effects.Has(WeaponEffects.SpreadInterceptor))
        {
            var center = steps.Find(s => s.shape == BarrageShape.Bullet && s.delay == 0 && s.offsetDegrees == Vector2.zero);
            if (center != null) { center.startSize *= 1.5f; center.interceptsBullet = true; }
        }
        if (effects.Has(WeaponEffects.SpreadExtra))
        {
            var template = steps.FindLast(s => s.shape == BarrageShape.Bullet);
            if (template != null)
                foreach (float angle in new[] { -30f, 30f })
                {
                    var extra = template.Snapshot();
                    extra.delay = .3f;
                    extra.offsetDegrees = new Vector2(angle, angle);
                    extra.interceptsBullet = false;
                    steps.Add(extra);
                }
        }
        if (effects.Has(WeaponEffects.BombFragments))
        {
            var fragments = steps.FindAll(s => s.shape == BarrageShape.Bullet);
            if (fragments.Count > 0)
            {
                int count = Mathf.CeilToInt(fragments.Count * 1.5f);
                steps.RemoveAll(s => s.shape == BarrageShape.Bullet);
                for (int i = 0; i < count; i++)
                {
                    var fragment = fragments[i % fragments.Count].Snapshot();
                    fragment.offsetDegrees = new Vector2(i * 360f / count, (i + 1) * 360f / count);
                    steps.Add(fragment);
                }
            }
        }
        if (effects.Has(WeaponEffects.BombChain) && chainRoll < .5f)
        {
            var explosion = steps.Find(s => s.shape == BarrageShape.Box && s.dealsDamage);
            if (explosion != null)
            {
                // Repeat the upgraded warning, explosion and fragments once at the same
                // new position. Snapshot before appending so the chain cannot recurse.
                foreach (var step in steps.ToArray())
                {
                    var extra = step.Snapshot();
                    extra.delay += .35f;
                    extra.overridePosition = true;
                    extra.worldPosition = chainPosition;
                    steps.Add(extra);
                }
            }
        }
        if (effects.Has(WeaponEffects.OrbLifetime))
            foreach (var step in steps) if (step.shape == BarrageShape.OrbitOrb) step.duration *= 1.5f;
        return steps.ToArray();
    }
}
