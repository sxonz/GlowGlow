using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One pooled planet; prepared body and glow sprites only, with no per-frame allocation.</summary>
public sealed class SolarPlanet : Bullet
{
    public override int Damage => released ? BarrageDamage.ReleasedPlanet : base.Damage;
    private SolarSystemProjectile sun;
    private float radius, angularSpeed, phase, startedAt;
    private float previousElapsed;
    private bool released, launched;
    private float launchAt;
    private Vector2 flightVelocity;
    private readonly RaycastHit2D[] hits=new RaycastHit2D[16];
    protected override bool DespawnOnHit => launched;
    protected override bool AllowRepeatedHits => !released;
    public override bool IsSmallBullet => false;
    protected override bool CanReflect => launched && AttackReady;
    protected override float ArmingDuration => 1f;
    protected override void OnBeginDisappear() => sun=null;
    protected override void OnReflected(Vector2 velocity) => flightVelocity = velocity;
    public override Vector2 LinearVelocity
    {
        get
        {
            if(released) return launched ? flightVelocity : Vector2.zero;
            Vector2 offset=sun!=null ? (Vector2)(transform.position-sun.transform.position) : Vector2.zero;
            return new Vector2(-offset.y,offset.x)*(angularSpeed*Mathf.Deg2Rad);
        }
    }
    protected override void OnSpawn()
    {
        base.OnSpawn();
        GetComponent<SpriteRenderer>().sharedMaterial=NeonVisualStyle.Get(NeonVisualStyle.PlanetMode);
        sun=null; startedAt=Time.time; previousElapsed=0; released=launched=false; flightVelocity=Vector2.zero;
        UpdateSpawnGrowth();
    }
    public void Release(float delay) { sun=null; released=true; launchAt=Time.time+delay; }
    public void Configure(SolarSystemProjectile center,float orbitRadius,float speed,float startAngle)
    {
        sun=center; radius=orbitRadius; angularSpeed=speed; phase=startAngle;
        transform.position=PositionAt(0);
    }
    protected override void Tick(float deltaTime)
    {
        UpdateSpawnGrowth();
        // A planet may update before the sun on the expiry frame. Resolve the
        // release first, rather than despawning because IsRunning is already false.
        if(sun!=null) sun.FinishIfExpired();
        if(!IsSpawned || IsDisappearing) return;
        if(released) { TickFlight(deltaTime); return; }
        if(sun==null || !sun.IsRunning) { Despawn(); return; }
        float elapsed=Time.time-startedAt;
        if(!AttackReady)
        {
            transform.position=PositionAt(elapsed);
            previousElapsed=elapsed;
            return;
        }
        int steps=Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(elapsed-previousElapsed)*angularSpeed/6f),1,32);
        var physics=gameObject.scene.GetPhysicsScene2D();
        var filter=new ContactFilter2D {useTriggers=true};
        for(int i=1;i<=steps;i++)
        {
            Vector2 from=transform.position;
            Vector2 to=PositionAt(Mathf.Lerp(previousElapsed,elapsed,i/(float)steps));
            Vector2 travel=to-from;
            int count=physics.CircleCast(from,Stats.Radius,travel.normalized,travel.magnitude,filter,hits);
            for(int j=0;j<count;j++)
            {
                if(hits[j].collider!=null) TryHit(hits[j].collider);
                if(!IsSpawned) return;
            }
            transform.position=to;
        }
        previousElapsed=elapsed;
    }
    private void TickFlight(float deltaTime)
    {
        if(Time.time<launchAt) return;
        if(Time.time>=launchAt+4) { Despawn(); return; }
        if(!launched)
        {
            Vector2 aim=Owner.AimPosition-(Vector2)transform.position;
            flightVelocity=(aim.sqrMagnitude>.0001f ? aim.normalized : Owner.AimDirection)*18f;
            launched=true;
            deltaTime=Mathf.Min(deltaTime,Mathf.Max(0,Time.time-launchAt));
        }
        Vector2 from=transform.position, travel=flightVelocity*deltaTime;
        int count=gameObject.scene.GetPhysicsScene2D().CircleCast(from,Stats.Radius,travel.normalized,travel.magnitude,new ContactFilter2D {useTriggers=true},hits);
        for(int i=0;i<count;i++) { if(hits[i].collider!=null) TryHit(hits[i].collider); if(!IsSpawned) return; }
        transform.position=from+travel;
        Vector2 bounds=Owner.ArenaHalfSize+Vector2.one*(Stats.Radius+2);
        Vector2 next=transform.position;
        if((Mathf.Abs(next.x)>bounds.x || Mathf.Abs(next.y)>bounds.y) && Vector2.Dot(next,flightVelocity)>0) Despawn();
    }
    private Vector3 PositionAt(float elapsed)
    {
        float angle=(phase+elapsed*angularSpeed)*Mathf.Deg2Rad;
        return sun.transform.position+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
    }
    private void OnTriggerStay2D(Collider2D other) => TryHit(other);
    protected override void OnDespawn() => sun=null;
}
