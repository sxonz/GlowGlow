using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(LineRenderer))]
public sealed class SolarFlare : ProjectileBase
{
    private LineRenderer ring;
    private float startedAt, previousRadius;
    private readonly Collider2D[] overlaps=new Collider2D[32];
    private static Material material;
    protected override bool DespawnOnHit => false;
    protected override void OnSpawn()
    {
        startedAt=Time.time; previousRadius=0;
        if(ring==null)
        {
            ring=GetComponent<LineRenderer>();
            if(material==null) material=new Material(Shader.Find("Sprites/Default"));
            ring.sharedMaterial=material; ring.loop=true; ring.useWorldSpace=false;
            ring.positionCount=64; ring.sortingOrder=6;
            for(int i=0;i<64;i++) { float angle=i*Mathf.PI*2/64; ring.SetPosition(i,new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))); }
        }
        Tick(0);
    }
    protected override void Tick(float deltaTime)
    {
        float progress=Mathf.Clamp01((Time.time-startedAt)/.8f);
        float radius=Mathf.Max(.01f,Stats.Range*progress);
        transform.localScale=new Vector3(radius,radius,1);
        ring.startWidth=ring.endWidth=.09f;
        Color color=Stats.Color; color.a=1-progress*.7f;
        ring.startColor=ring.endColor=color;
        int count=gameObject.scene.GetPhysicsScene2D().OverlapCircle(transform.position,radius,
            new ContactFilter2D {useTriggers=true},overlaps);
        for(int i=0;i<count;i++)
        {
            var target=overlaps[i].GetComponent<PlayerCombatant>();
            if(target==null || Vector2.Distance(target.transform.position,transform.position)+target.BodyRadius<previousRadius) continue;
            TryHit(overlaps[i],0,.7f,1.5f,false);
        }
        previousRadius=radius;
    }
}
