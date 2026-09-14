using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Straight, double-helix or mutually orbiting paths that continue through the shared fan.</summary>
public sealed class PrismPellet : Bullet
{
    public int Slot { get; private set; }
    private Vector2 anchor, launch, axis, lateral, endpoint;
    private float firedAt, launchDelay;
    private bool configured;
    private bool hasCenter;
    private Vector2 centerLaunch;
    private Rect arenaRect;
    private TrailRenderer trail;
    private static Material trailMaterial;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    public override Vector2 LinearVelocity => configured
        ? (PositionAt(Time.time+.001f)-PositionAt(Time.time))/.001f : base.LinearVelocity;

    protected override void OnSpawn()
    {
        base.OnSpawn();
        configured=false;
        hasCenter=false;
        Vector2 half=Owner.ArenaHalfSize;
        arenaRect=new Rect(-half,half*2);
        foreach(var root in gameObject.scene.GetRootGameObjects())
        foreach(var line in root.GetComponentsInChildren<LineRenderer>())
        {
            if(line.name!="Arena Border" || line.positionCount<4) continue;
            Vector2 min=Vector2.positiveInfinity, max=Vector2.negativeInfinity;
            for(int i=0;i<line.positionCount;i++)
            {
                Vector2 point=line.useWorldSpace ? line.GetPosition(i) : line.transform.TransformPoint(line.GetPosition(i));
                min=Vector2.Min(min,point); max=Vector2.Max(max,point);
            }
            arenaRect=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        if(trail==null)
        {
            trail=gameObject.AddComponent<TrailRenderer>();
            if(trailMaterial==null) trailMaterial=new Material(Shader.Find("Sprites/Default"));
            trail.sharedMaterial=trailMaterial;
            trail.time=.16f;
            trail.minVertexDistance=.025f;
            trail.sortingOrder=3;
            trail.endWidth=0;
        }
        trail.Clear();
        trail.startWidth=Stats.Radius;
        trail.startColor=new Color(Stats.Color.r,Stats.Color.g,Stats.Color.b,.45f);
        trail.endColor=new Color(Stats.Color.r,Stats.Color.g,Stats.Color.b,0);
        trail.emitting=true;
    }

    public void Configure(int slot,Vector2 formationAnchor,Vector2 origin,Vector2 direction,float startTime)
    {
        Slot=slot;
        anchor=formationAnchor;
        launch=origin;
        axis=direction.normalized;
        lateral=new Vector2(-axis.y,axis.x);
        firedAt=startTime;
        launchDelay=PrismShot.LaunchDelay(slot);
        float angle=(4-slot)*10f*Mathf.Deg2Rad;
        endpoint=anchor+(axis*Mathf.Cos(angle)+lateral*Mathf.Sin(angle))*PrismShot.FormationDistance;
        configured=true;
    }

    public void BindCenter(Vector2 origin,PlayerCombatant owner,float startTime)
    {
        if(!IsSpawned || Owner!=owner || firedAt!=startTime) return;
        centerLaunch=origin; hasCenter=true;
    }

    public Vector2 PositionAt(float time)
    {
        float t=Mathf.Max(0,(time-firedAt-launchDelay)/(PrismShot.FormationTime-launchDelay));
        if(Slot==1 || Slot==4 || Slot==7) return Vector2.LerpUnclamped(launch,endpoint,t);
        int sign=Slot<4?1:-1;
        float halfAngle=(Slot==3 || Slot==5 ? 10f : 20f)*Mathf.Deg2Rad;
        Vector2 center=Vector2.LerpUnclamped(launch,anchor+axis*(PrismShot.FormationDistance*Mathf.Cos(halfAngle)),t);
        float expansion=t<=1 ? t : 2-Mathf.Exp(-(t-1));
        float radius=PrismShot.FormationDistance*Mathf.Sin(halfAngle)*expansion;
        float phase=(time-firedAt-PrismShot.FormationTime)*Mathf.PI;
        if(Slot==2 || Slot==6)
            return center+lateral*(sign*radius*Mathf.Cos(phase));
        if(hasCenter)
        {
            float elapsed=time-firedAt-PrismShot.LaunchDelay(4);
            float centerProgress=Mathf.Max(0,elapsed/(PrismShot.FormationTime-PrismShot.LaunchDelay(4)));
            Vector2 core=Vector2.LerpUnclamped(centerLaunch,anchor+axis*PrismShot.FormationDistance,centerProgress);
            // All three fire together and share the central bullet trajectory from launch.
            center=core;
        }
        // A trajectory snapshot keeps the pair moving even if the central bullet hits or is pooled.
        return center+sign*radius*(lateral*Mathf.Cos(phase)+axis*Mathf.Sin(phase));
    }

    protected override void Tick(float deltaTime)
    {
        if(!configured) return;
        Vector2 from=transform.position;
        Vector2 to=PositionAt(Time.time);
        bool boundary=false;
        {
            Vector2 min=arenaRect.min+Vector2.one*Stats.Radius;
            Vector2 max=arenaRect.max-Vector2.one*Stats.Radius;
            Vector2 delta=to-from;
            float fraction=1;
            if(to.x>=max.x && delta.x>0) { boundary=true; fraction=Mathf.Min(fraction,(max.x-from.x)/delta.x); }
            if(to.x<=min.x && delta.x<0) { boundary=true; fraction=Mathf.Min(fraction,(min.x-from.x)/delta.x); }
            if(to.y>=max.y && delta.y>0) { boundary=true; fraction=Mathf.Min(fraction,(max.y-from.y)/delta.y); }
            if(to.y<=min.y && delta.y<0) { boundary=true; fraction=Mathf.Min(fraction,(min.y-from.y)/delta.y); }
            to=Vector2.Lerp(from,to,Mathf.Clamp01(fraction));
        }
        Vector2 travel=to-from;
        Physics2D.SyncTransforms();
        int count=gameObject.scene.GetPhysicsScene2D().CircleCast(from,Stats.Radius,travel.normalized,travel.magnitude,
            new ContactFilter2D {useTriggers=true},hits);
        for(int i=0;i<count;i++)
        {
            if(hits[i].collider!=null) Impact(hits[i].collider,hits[i].point);
            if(!IsSpawned) return;
        }
        transform.position=to;
        if(boundary)
        {
            Vector2 contact=to;
            if(to.x>=arenaRect.xMax-Stats.Radius-.0001f && travel.x>0) contact.x=arenaRect.xMax;
            if(to.x<=arenaRect.xMin+Stats.Radius+.0001f && travel.x<0) contact.x=arenaRect.xMin;
            if(to.y>=arenaRect.yMax-Stats.Radius-.0001f && travel.y>0) contact.y=arenaRect.yMax;
            if(to.y<=arenaRect.yMin+Stats.Radius+.0001f && travel.y<0) contact.y=arenaRect.yMin;
            Explode(contact); Despawn(); return;
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
        => Impact(other,other.ClosestPoint(transform.position));

    private void Impact(Collider2D other,Vector2 point)
    {
        if(!IsSpawned) return;
        if(TryRegisterHit(other,out var target))
        {
            target.ReceiveHit(Owner);
            if(!IsSpawned) return;
            Explode(point);
            Despawn();
        }
        else if(!other.isTrigger &&
                other.GetComponentInParent<PlayerCombatant>()==null && other.GetComponentInParent<ProjectileBase>()==null)
        {
            Explode(point); Despawn();
        }
    }

    private void Explode(Vector2 position)
    {
        if(!Effects.Has(WeaponEffects.PrismExplosion)) return;
        var stats=new WeaponStats(Stats.Cooldown,0,.25f,.6f,Stats.Range,Stats.Color);
        var explosion=SpawnBuiltin<BarrageShapeProjectile>(Owner,position,Direction,stats);
        explosion.Configure(new BarrageStep
        {
            shape=BarrageShape.Circle, position=BarragePosition.Cursor, duration=.25f,
            dimensions=Vector2.one, startSize=1, peakSize=1, endSize=0,
            shrinkTime=.25f, flash=true, dealsDamage=true
        });
        explosion.ClipToArena(arenaRect);
    }

    protected override void OnDespawn()
    {
        configured=false;
        trail.emitting=false;
        trail.Clear();
    }
}
