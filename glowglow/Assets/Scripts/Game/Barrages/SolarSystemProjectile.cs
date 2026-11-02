using UnityEngine;

/// <summary>A stationary sun with eight pooled planets and three temporary shield charges.</summary>
public sealed class SolarSystemProjectile : Bullet
{
    public int ShieldRemaining { get; private set; }
    public bool IsRunning => IsSpawned && !IsDisappearing && ShieldRemaining > 0 && Time.time < expiresAt;
    public float OuterOrbitRadius { get; private set; }
    private float expiresAt;
    private float startedAt, firstFlareAt, secondFlareAt, nextCometAt;
    private int flaresEmitted;
    private bool releaseOnEnd;
    private readonly SolarPlanet[] planets = new SolarPlanet[8];
    private readonly SolarFlare[] flares=new SolarFlare[2];
    private readonly SolarComet[] comets=new SolarComet[18];
    private int cometCount;
    private static Material orbitMaterial;
    private readonly LineRenderer[] orbits = new LineRenderer[8];
    private static readonly float[] PlanetSizes = { .13f, .17f, .18f, .15f, .26f, .23f, .20f, .19f };
    private static readonly Color[] PlanetColors = {
        new(.7f,.65f,.6f), new(1,.65f,.3f), new(.25f,.65f,1), new(1,.3f,.15f),
        new(1,.8f,.5f), new(.9f,.8f,.45f), new(.35f,.9f,.9f), new(.3f,.4f,1)
    };
    protected override bool DespawnOnHit => false;
    protected override bool AllowRepeatedHits => true;
    public override bool IsSmallBullet => false;
    public override Vector2 LinearVelocity => Vector2.zero;
    protected override bool CanReflect => false;
    protected override float ArmingDuration => 1f;

    protected override void OnSpawn()
    {
        base.OnSpawn();
        GetComponent<SpriteRenderer>().sharedMaterial = NeonVisualStyle.Get(NeonVisualStyle.SunMode);
        if (orbitMaterial == null) orbitMaterial = new Material(Shader.Find("Sprites/Default"));
        ShieldRemaining = 3;
        ExtendLifetime(ArmingDuration);
        expiresAt = Time.time + Stats.Lifetime + ArmingDuration;
        startedAt=Time.time;
        firstFlareAt=startedAt+ArmingDuration+Random.Range(0f,3f);
        secondFlareAt=startedAt+ArmingDuration+Random.Range(4f,5f);
        nextCometAt=startedAt+1f+Random.Range(.15f,.45f);
        flaresEmitted=0;
        cometCount=0;
        releaseOnEnd=false;
        transform.position = Owner.transform.position;
        Owner.BeginSolarSystem(this);
        // Scale every compact orbit by 1.5, independently of the cast position.
        float uranusRadius=Mathf.Max(1.8f,Mathf.Min(Owner.ArenaHalfSize.x,Owner.ArenaHalfSize.y)*.9f);
        OuterOrbitRadius=(1.2f+(uranusRadius-1.2f)*7f/6f)*1.8f;
        for(int i=0;i<planets.Length;i++)
        {
            float size=PlanetSizes[i];
            var stats=new WeaponStats(Stats.Cooldown,0,float.PositiveInfinity,size,Stats.Range,PlanetColors[i],BarrageDamage.Planet);
            planets[i]=SpawnBuiltin<SolarPlanet>(Owner,transform.position,Vector2.right,stats);
            float orbitRadius=Mathf.Lerp(2.16f,OuterOrbitRadius,i/7f);
            planets[i].Configure(this,orbitRadius,240f/(1+i*.3f),i*137.5f);
            if(orbits[i]==null)
            {
                orbits[i]=new GameObject("Solar Orbit "+i,typeof(LineRenderer)).GetComponent<LineRenderer>();
                orbits[i].transform.SetParent(transform,false);
                orbits[i].sharedMaterial=orbitMaterial;
                orbits[i].useWorldSpace=false;
                orbits[i].loop=true;
                orbits[i].positionCount=96;
                orbits[i].startWidth=orbits[i].endWidth=.012f;
                orbits[i].sortingOrder=1;
            }
            orbits[i].startColor=orbits[i].endColor=new Color(.5f,.65f,.85f,.09f);
            float localRadius=orbitRadius/transform.localScale.x;
            for(int point=0;point<96;point++)
            {
                float angle=point*(Mathf.PI*2/96);
                orbits[i].SetPosition(point,new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*localRadius);
            }
        }
        UpdateSpawnGrowth();
    }
    protected override void Tick(float deltaTime)
    {
        if(!IsRunning) { Finish(); return; }
        UpdateSpawnGrowth();
        if(!AttackReady) return;
        if(Effects.Has(WeaponEffects.SolarFlare))
        {
            while(flaresEmitted<2 && Time.time >= (flaresEmitted==0 ? firstFlareAt : secondFlareAt))
            {
                flares[flaresEmitted]=SpawnBuiltin<SolarFlare>(Owner,transform.position,Vector2.right,
                    new WeaponStats(Stats.Cooldown,0,.85f,.1f,OuterOrbitRadius+1f,new Color(1,.65f,.12f),0));
                flaresEmitted++;
            }
        }
        if(Effects.Has(WeaponEffects.SolarComets) && Time.time>=nextCometAt && cometCount<comets.Length)
        {
            Vector2 bounds=Owner.ArenaHalfSize;
            Vector2 origin=Random.value<.5f
                ? new Vector2(Random.Range(-bounds.x,bounds.x+2),bounds.y+2)
                : new Vector2(bounds.x+2,Random.Range(0,bounds.y+2));
            Vector2 target=new Vector2(Random.Range(-bounds.x-2,Mathf.Min(origin.x-1,bounds.x)), -bounds.y-2);
            comets[cometCount++]=SpawnBuiltin<SolarComet>(Owner,origin,(target-origin).normalized,
                new WeaponStats(Stats.Cooldown,16,4,Random.Range(.187f,.34f),Stats.Range,new Color(.55f,.85f,1),BarrageDamage.Comet));
            nextCometAt=Time.time+Random.Range(.3f,.6f);
        }
    }
    public bool AbsorbHit()
    {
        if(!IsRunning) return false;
        ShieldRemaining--;
        if(ShieldRemaining==0) Finish();
        return true;
    }
    private void Finish()
    {
        if(IsDisappearing) return;
        releaseOnEnd=true;
        if(Effects.Has(WeaponEffects.SolarRelease)) Despawn();
        else BeginDisappear();
    }
    protected override void OnBeginDisappear()
    {
        ShieldRemaining=0;
        if(Owner!=null) Owner.EndSolarSystem(this);
        foreach(var planet in planets)
            if(planet!=null && planet.IsSpawned) planet.BeginDisappear();
    }
    protected override void OnDisappearVisual(float remaining)
    {
        foreach(var orbit in orbits)
            if(orbit!=null) orbit.startColor=orbit.endColor=new Color(.5f,.65f,.85f,.09f*remaining);
    }
    internal void FinishIfExpired()
    {
        if(IsSpawned && Time.time>=expiresAt) Finish();
    }
    protected override void OnLifetimeExpired() => Finish();
    public void Cancel() { releaseOnEnd=false; Despawn(); }
    private void OnTriggerStay2D(Collider2D other) { if(IsRunning) TryHit(other); }
    protected override void OnDespawn()
    {
        for(int i=0;i<flares.Length;i++)
        {
            if(!releaseOnEnd && flares[i]!=null && flares[i].IsSpawned && flares[i].Owner==Owner) flares[i].RemoveProjectile();
            flares[i]=null;
        }
        for(int i=0;i<comets.Length;i++)
        {
            if(!releaseOnEnd && comets[i]!=null && comets[i].IsSpawned && comets[i].Owner==Owner) comets[i].RemoveProjectile();
            comets[i]=null;
        }
        ShieldRemaining=0;
        for(int i=0;i<planets.Length;i++)
        {
            if(planets[i]!=null && planets[i].IsSpawned)
            {
                if(releaseOnEnd && Owner!=null && Owner.CanSelectWeapon && Effects.Has(WeaponEffects.SolarRelease))
                    planets[i].Release((7-i)*.1f);
                else planets[i].RemoveProjectile();
            }
            planets[i]=null;
        }
        if(Owner!=null) Owner.EndSolarSystem(this);
    }
}
