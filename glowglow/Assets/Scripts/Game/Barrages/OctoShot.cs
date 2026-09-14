using UnityEngine;

/// <summary>One spoke of an eight-way burst, optionally following a curved trajectory.</summary>
public sealed class OctoShot : Bullet
{
    public const float CurveDegreesPerSecond = 60f;
    private float age, initialAngle;
    private Vector2 origin;
    private float AngularSpeed => Effects.Has(WeaponEffects.OctoCurve) ? CurveDegreesPerSecond*Mathf.Deg2Rad : 0;
    public override Vector2 LinearVelocity => new Vector2(Mathf.Cos(initialAngle+AngularSpeed*age),Mathf.Sin(initialAngle+AngularSpeed*age))*Stats.Speed;

    protected override void OnSpawn()
    {
        base.OnSpawn();
        age=0;
        origin=transform.position;
        initialAngle=Mathf.Atan2(Direction.y,Direction.x);
    }

    protected override void Tick(float deltaTime)
    {
        age+=Mathf.Max(0,deltaTime);
        float omega=AngularSpeed;
        // Analytic integration keeps the curve and travel distance consistent across frame rates.
        Vector2 offset = omega == 0 ? Direction*Stats.Speed*age : new Vector2(
            Mathf.Sin(initialAngle+omega*age)-Mathf.Sin(initialAngle),
            Mathf.Cos(initialAngle)-Mathf.Cos(initialAngle+omega*age))*(Stats.Speed/omega);
        transform.position=origin+offset;
        Color color=Stats.Color;
        color.a*=Mathf.Clamp01(RemainingLifetime/.15f);
        GetComponent<SpriteRenderer>().color=color;
    }

    protected override void OnLifetimeExpired()
    {
        if(!Effects.Has(WeaponEffects.OctoSplit)) return;
        Tick(Mathf.Max(0,Stats.Lifetime-age));
        var stats=new WeaponStats(Stats.Cooldown,Stats.Speed,.45f,Stats.Radius*.5f,Stats.Range,Stats.Color);
        foreach(float angle in new[]{-20f,20f})
            SpawnBuiltin<OctoShot>(Owner,transform.position,Quaternion.Euler(0,0,angle)*LinearVelocity.normalized,
                stats,Effects & ~WeaponEffects.OctoSplit);
    }
}
