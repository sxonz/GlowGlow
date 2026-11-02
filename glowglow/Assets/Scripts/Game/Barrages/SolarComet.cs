using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(LineRenderer))]
public sealed class SolarComet : Bullet
{
    private LineRenderer tail;
    private static Material material;
    private readonly RaycastHit2D[] hits=new RaycastHit2D[32];
    protected override void OnReflected(Vector2 velocity)
        => tail.SetPosition(1, -(Vector3)Direction * (2.125f / (Stats.Radius * 2)));
    protected override void OnSpawn()
    {
        base.OnSpawn();
        if(tail==null) tail=GetComponent<LineRenderer>();
        if(material==null) material=new Material(Shader.Find("Sprites/Default"));
        tail.sharedMaterial=material; tail.useWorldSpace=false; tail.positionCount=2; tail.sortingOrder=3;
        tail.SetPosition(0,Vector3.zero); tail.SetPosition(1,-(Vector3)Direction*(2.125f/(Stats.Radius*2)));
        tail.startWidth=.2975f; tail.endWidth=0;
        tail.startColor=Stats.Color; var transparent=Stats.Color; transparent.a=0; tail.endColor=transparent;
    }
    protected override void Tick(float deltaTime)
    {
        Vector2 from=transform.position, travel=Direction*(Stats.Speed*deltaTime);
        int count=gameObject.scene.GetPhysicsScene2D().CircleCast(from,Stats.Radius,Direction,travel.magnitude,new ContactFilter2D {useTriggers=true},hits);
        for(int i=0;i<count;i++) { if(hits[i].collider!=null) TryHit(hits[i].collider); if(!IsSpawned) return; }
        transform.position=from+travel;
        Vector2 bounds=Owner.ArenaHalfSize+Vector2.one*3;
        Vector2 next = transform.position;
        if ((Mathf.Abs(next.y) > bounds.y || Mathf.Abs(next.x) > bounds.x) && Vector2.Dot(next, Direction) > 0) Despawn();
    }
}
