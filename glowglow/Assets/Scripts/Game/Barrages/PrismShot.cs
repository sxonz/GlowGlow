using UnityEngine;

/// <summary>Seven staggered shots sharing one formation point in time and a radius-five fan.</summary>
public sealed class PrismShot : ProjectileBase
{
    public const float FormationDistance = 5f;
    public const float FormationTime = .8f;
    public const float GroupDelay = .15f;
    private float startedAt;
    private int group;
    private Vector2 anchor;
    private Vector2 coreLaunch;
    private static readonly int[][] Groups = {new[]{1,7},new[]{3,4,5},new[]{2,6}};
    private static readonly int[][] TailGroups = {new[]{1,7},new[]{3,4,5},new[]{2,6},new[]{3,4,5},new[]{4}};

    public static float LaunchDelay(int slot) => slot == 1 || slot == 7 ? 0 :
        slot == 3 || slot == 4 || slot == 5 ? GroupDelay : GroupDelay*2;

    protected override void OnSpawn()
    {
        startedAt=Time.time;
        group=0;
        anchor=transform.position;
        Owner.BeginPrismAim(Direction);
        EmitDue();
    }

    private void EmitDue()
        => EmitDueAt(Time.time-startedAt);

    private void EmitDueAt(float elapsed)
    {
        var groups=Effects.Has(WeaponEffects.PrismTail) ? TailGroups : Groups;
        while(group<groups.Length && elapsed >= group*GroupDelay)
        {
            // Each delayed group starts at the current muzzle; the formation stays anchored to the initial shot.
            Vector2 launch=group==0 ? anchor : Owner.MuzzlePosition;
            if(group==1) coreLaunch=launch;
            float shift=group>=3 ? (group-1)*GroupDelay : 0;
            Vector2 pathAnchor=group>=3 ? anchor+launch-coreLaunch : anchor;
            foreach(int slot in groups[group])
            {
                var stats=new WeaponStats(Stats.Cooldown,Stats.Speed,
                    float.PositiveInfinity,Stats.Radius,Stats.Range,Stats.Color);
                var pellet=SpawnBuiltin<PrismPellet>(Owner,launch,Direction,stats,Effects);
                pellet.Configure(slot,pathAnchor,launch,Direction,startedAt+shift);
                if(slot==3 || slot==5) pellet.BindCenter(launch,Owner,startedAt+shift);
            }
            group++;
        }
        if(group==groups.Length) Despawn();
    }

    protected override void Tick(float deltaTime) { }
    protected override void OnDespawn() { if(Owner!=null) Owner.EndPrismAim(); }
    protected override void LateUpdate() { EmitDue(); base.LateUpdate(); }
}
