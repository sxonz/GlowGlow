using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class MeteorMiniTank : ProjectileBase
{
    private float startedAt;
    private Vector2 start, target, previous;
    private SpriteRenderer barrel;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    protected override bool DespawnOnHit => false;

    protected override void OnSpawn()
    {
        startedAt = Time.time;
        start = previous = transform.position;
        target = start;
        var visual = GetComponent<SpriteRenderer>();
        visual.sprite = RuntimeShapes.Circle;
        visual.color = Owner.GetComponent<SpriteRenderer>().color;
        visual.sortingOrder = 5;
        transform.localScale = Vector3.one * Stats.Radius * 2f;
        if (barrel == null)
        {
            barrel = new GameObject("Mini Barrel",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            barrel.transform.SetParent(transform,false);
            barrel.sprite = RuntimeShapes.Barrel;
            barrel.transform.localRotation = Quaternion.Euler(0,0,-90);
            barrel.transform.localPosition = Vector3.down*.46f;
            barrel.sortingOrder = 4;
        }
    }
    public void Configure(Vector2 destination) => target = destination;

    protected override void Tick(float deltaTime)
    {
        float progress = Mathf.Clamp01((Time.time-startedAt)/MeteorDive.DiveDuration);
        Vector2 position = Vector2.Lerp(start,target,progress*progress);
        Vector2 travel = position-previous;
        Physics2D.SyncTransforms();
        int count = gameObject.scene.GetPhysicsScene2D().CircleCast(previous,Stats.Radius,travel.normalized,
            travel.magnitude,new ContactFilter2D { useTriggers = true },hits);
        for(int i=0;i<count;i++)
        {
            if(hits[i].collider != null) TryHit(hits[i].collider);
            if(!IsSpawned) return;
        }
        transform.position = previous = position;
        if(progress < 1) return;
        MeteorDive.EmitEruption(Owner,target,Stats,.5f);
        Despawn();
    }
}
