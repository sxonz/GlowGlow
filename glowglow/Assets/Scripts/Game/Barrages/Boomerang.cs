using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Slows to a stop outbound, then snaps back toward the owner's position at turnaround.</summary>
public sealed class Boomerang : Bullet
{
    public const float OutboundDuration = 1.1f;
    public const float ReturnSpeedMultiplier = 1.5f;
    private float age;
    private bool returning;
    private int bouncesRemaining;
    private Vector2 velocity;
    private WeaponRuntime firingWeapon;
    private readonly SpriteRenderer[] arms = new SpriteRenderer[2];
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    public bool IsReturning => returning;
    public override Vector2 LinearVelocity => velocity;
    public override bool IsSmallBullet => false;
    protected override bool DespawnOnHit => false;
    protected override bool AllowRepeatedHits => true;
    protected override bool GlowEnabled => false;

    public void BindWeapon(WeaponRuntime runtime) => firingWeapon = runtime;

    protected override void OnSpawn()
    {
        base.OnSpawn();
        age=0;
        returning=false;
        bouncesRemaining=Effects.Has(WeaponEffects.BoomerangBounce) ? 2 : 0;
        firingWeapon=null;
        velocity=Direction*Stats.Speed;
        GetComponent<SpriteRenderer>().color=Color.clear;
        for(int i=0;i<2;i++)
        {
            if(arms[i]==null)
            {
                var arm=new GameObject("Boomerang Arm "+i,typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                arm.transform.SetParent(transform,false);
                arm.sprite=RuntimeShapes.Barrel;
                arm.transform.localPosition=new Vector3(i==0?-.19f:.19f,.06f,0);
                arm.transform.localRotation=Quaternion.Euler(0,0,i==0?135:45);
                Vector2 size=arm.sprite.bounds.size;
                arm.transform.localScale=new Vector3(.65f/size.x,.16f/size.y,1);
                arm.sortingOrder=5;
                arms[i]=arm;
            }
            arms[i].color=Stats.Color;
        }
    }

    protected override void Tick(float deltaTime)
    {
        // Substeps and swept collisions keep catches and damage reliable during slow frames.
        float remaining=Mathf.Max(0,deltaTime);
        while(remaining>0 && IsSpawned)
        {
            float dt=Mathf.Min(remaining,1f/60);
            if(!returning) dt=Mathf.Min(dt,OutboundDuration-age);
            Vector2 from=transform.position;
            Vector2 heading=velocity.normalized;
            float deceleration=returning ? 0 : Stats.Speed/OutboundDuration;
            float speed=returning ? velocity.magnitude : Stats.Speed*Mathf.Clamp01(1-age/OutboundDuration);
            float distance=speed*dt-.5f*deceleration*dt*dt;
            bool bounceX=false, bounceY=false;
            if(bouncesRemaining>0)
            {
                Vector2 limits=Vector2.Max(Vector2.one*.01f,Owner.ArenaHalfSize-Vector2.one*Stats.Radius);
                float tx=Mathf.Abs(heading.x)>.00001f ? (Mathf.Sign(heading.x)*limits.x-from.x)/heading.x : float.PositiveInfinity;
                float ty=Mathf.Abs(heading.y)>.00001f ? (Mathf.Sign(heading.y)*limits.y-from.y)/heading.y : float.PositiveInfinity;
                float wallDistance=Mathf.Max(0,Mathf.Min(tx,ty));
                if(wallDistance<=distance)
                {
                    float impactSpeed=Mathf.Sqrt(Mathf.Max(0,speed*speed-2*deceleration*wallDistance));
                    dt=2*wallDistance/Mathf.Max(.000001f,speed+impactSpeed);
                    distance=wallDistance;
                    bounceX=tx<=ty+.000001f;
                    bounceY=ty<=tx+.000001f;
                }
            }
            remaining-=dt;
            age+=dt;
            Vector2 next=from+heading*distance;
            velocity=heading*Mathf.Max(0,speed-deceleration*dt);
            Vector2 travel=next-from;
            Physics2D.SyncTransforms();
            int count=gameObject.scene.GetPhysicsScene2D().CircleCast(from,Stats.Radius,travel.normalized,travel.magnitude,
                new ContactFilter2D {useTriggers=true},hits);
            for(int i=0;i<count;i++)
            {
                if(hits[i].collider!=null) TryHit(hits[i].collider);
                if(!IsSpawned) return;
            }
            transform.position=next;
            transform.rotation=Quaternion.Euler(0,0,age*720f);
            if(bounceX || bounceY)
            {
                if(bounceX) velocity.x=-velocity.x;
                if(bounceY) velocity.y=-velocity.y;
                bouncesRemaining--;
            }
            if(!returning && age>=OutboundDuration)
            {
                returning=true;
                Vector2 toOwner=(Vector2)Owner.transform.position-next;
                velocity=(toOwner.sqrMagnitude>.00001f ? toOwner.normalized : -Direction)
                    *(Stats.Speed*ReturnSpeedMultiplier);
            }
            if(IsReturning && !Owner.IsMeteorDiving)
            {
                float t=travel.sqrMagnitude>0 ? Mathf.Clamp01(Vector2.Dot((Vector2)Owner.transform.position-from,travel)/travel.sqrMagnitude) : 0;
                if(Vector2.Distance(from+travel*t,Owner.transform.position)<=Owner.BodyRadius+Stats.Radius)
                {
                    firingWeapon?.ReduceCooldown(Effects.Has(WeaponEffects.BoomerangRefund) ? 4f : 2f);
                    Despawn();
                    return;
                }
            }
            Vector2 bounds=Owner.ArenaHalfSize+Vector2.one*(Stats.Radius+2f);
            if(IsReturning && (Mathf.Abs(next.x)>bounds.x || Mathf.Abs(next.y)>bounds.y)
                && Vector2.Dot(next,velocity)>0)
                Despawn();
        }
    }

    private void OnTriggerStay2D(Collider2D other) => TryHit(other);
    protected override void OnDespawn() => firingWeapon=null;
}
