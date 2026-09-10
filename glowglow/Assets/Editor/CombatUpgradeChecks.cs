using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CombatUpgradeChecks
{
    private static int assertions;
    private static WeaponDefinition[] weapons;
    private static BarrageUpgradeDefinition[] upgrades;
    private static PlayerCombatant owner, opponent;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("GlowGlow/Validate Combat Upgrades")]
    public static void Run()
    {
        assertions = 0;
        var previousScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            weapons = new[] {"Pulse Shot", "Basic Barrage 02", "Basic Barrage 03", "Basic Barrage 04", "Basic Barrage 05", "Basic Barrage 06", "Basic Barrage 07", "Basic Barrage 08"}
                .Select(name => AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/" + name + ".asset")).ToArray();
            upgrades = Resources.LoadAll<BarrageUpgradeDefinition>("WeaponUpgrades");
            Check(weapons.All(w => w != null), "8 weapon assets load");
            Check(upgrades.Length == 24 && Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades").Length == 24, "24 dedicated upgrades, no temporary entries");
            foreach (var weapon in weapons)
            {
                var set = upgrades.Where(u => u.CanApplyTo(weapon)).OrderBy(u => u.displaySlot).ToArray();
                Check(set.Length == 3 && set.Select(u => u.displaySlot).SequenceEqual(new[] {0,1,2}), weapon.name + " three shards");
                Check(set.All(u => u.maxLevel == 1 && u.effect != WeaponEffects.None), weapon.name + " one-time unlocks");
                var runtime = new WeaponRuntime(weapon);
                foreach (var upgrade in set.Reverse()) Check(runtime.TryUpgrade(upgrade), "out-of-order acquisition " + upgrade.name);
                Check(set.All(u => !runtime.TryUpgrade(u)), "duplicates rejected");
                Check(new WeaponRuntime(weapon).Upgrades.Count == 0, "other runtime is isolated");
                Check(!runtime.TryUpgrade(upgrades.First(u => !u.CanApplyTo(weapon))), "foreign upgrade rejected");
            }
            var basic = FullyUpgraded(0).Stats;
            Near(basic.Speed, weapons[0].projectileSpeed * 1.2f, "basic speed");
            Near(basic.Radius, weapons[0].projectileRadius * 1.2f, "basic size");
            Near(basic.Cooldown, weapons[0].cooldown * .9f, "basic cooldown");
            Near(FullyUpgraded(1).Stats.Cooldown, weapons[1].cooldown * .85f, "laser cooldown");
            Near(FullyUpgraded(2).Stats.Speed, weapons[2].projectileSpeed * 1.25f, "spread speed");
            Near(FullyUpgraded(4).Stats.Cooldown, weapons[4].cooldown * .85f, "pulse cooldown");
            Near(FullyUpgraded(6).Stats.Cooldown, weapons[6].cooldown * .9f, "bouncer cooldown");
            TestPatterns();
            TestDraft();
            owner = Player("Validation Owner", Vector2.zero);
            opponent = Player("Validation Opponent", Vector2.right);
            TestShards();
            TestLaser();
            TestInterception();
            TestOverdrive();
            TestPulse();
            TestOrbAndBounce();
            Directory.CreateDirectory("Temp");
            string result = "PASS: " + assertions + " Unity combat upgrade assertions.";
            File.WriteAllText("Temp/combat-upgrade-validation.txt", result);
            Debug.Log(result);
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/combat-upgrade-validation.txt", ex.ToString());
            throw;
        }
        finally
        {
            ProjectileBase.DespawnAll();
            foreach (var player in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerCombatant>(true)).ToArray())
            {
                var root = Get<Transform>(player, "afterimageRoot");
                if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
                Set(player, "afterimageRoot", null);
            }
            SceneManager.SetActiveScene(previousScene);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    public static void RunBatch()
    {
        try
        {
            // Batch validation runs in a disposable project with an initially untitled scene.
            var initialScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(initialScene.path))
            {
                Directory.CreateDirectory("Temp");
                EditorSceneManager.SaveScene(initialScene, "Temp/ValidationHost.unity");
            }
            Run(); EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    private static WeaponRuntime FullyUpgraded(int index)
    {
        var runtime = new WeaponRuntime(weapons[index]);
        foreach (var upgrade in upgrades.Where(u => u.CanApplyTo(weapons[index]))) runtime.TryUpgrade(upgrade);
        return runtime;
    }

    private static void TestPatterns()
    {
        var laser = WeaponPatternCompiler.Build(weapons[1].steps, WeaponEffects.LaserQuickWarning, 1, Vector2.zero);
        Near(laser[0].duration, .75f, "laser warning reduced");
        Near(laser[1].delay, .75f, "laser fire follows warning");
        Near(weapons[1].steps[0].duration, 1, "asset warning untouched");
        var spread = WeaponPatternCompiler.Build(weapons[2].steps, WeaponEffects.SpreadExtra | WeaponEffects.SpreadInterceptor, 1, Vector2.zero);
        Check(spread.Length == 7, "7 spread bullets");
        Check(spread.Count(s => s.delay == .3f && Mathf.Abs(s.offsetDegrees.x) == 30) == 2, "extra pair timing/angles");
        Check(spread.Count(s => s.interceptsBullet) == 1, "only central bullet intercepts");
        Near(spread.First(s => s.interceptsBullet).startSize, 1.5f, "central size");
        var bomb = WeaponPatternCompiler.Build(weapons[3].steps, WeaponEffects.BombChain | WeaponEffects.BombFragments | WeaponEffects.BombQuickWarning, .49f, new Vector2(2,3));
        Check(bomb.Count(s => s.shape == BarrageShape.Bullet) == 6, "6 bomb fragments");
        Check(bomb.Count(s => s.shape == BarrageShape.Box) == 2, "one chain explosion maximum");
        Check(bomb.Single(s => s.overridePosition).worldPosition == new Vector2(2,3), "chain position snapshot");
        Near(bomb.First(s => s.shape == BarrageShape.Box).delay, .75f, "bomb warning reduction");
        Check(WeaponPatternCompiler.Build(weapons[3].steps, WeaponEffects.BombChain, .5f, Vector2.zero).Count(s => s.shape == BarrageShape.Box) == 1, "50 percent threshold");
        Near(WeaponPatternCompiler.Build(weapons[7].steps, WeaponEffects.OrbLifetime, 1, Vector2.zero)[0].duration, 7.5f, "orb lifetime");
    }

    private static void TestDraft()
    {
        for (int seed = 0; seed < 24; seed++)
        {
            var draft = new OpeningDraft(weapons, new System.Random(seed), upgrades);
            Check(draft.Offers.All(o => !o.IsUpgrade), "first round weapon-only");
            draft.Select(0); draft.Confirm();
            for (int round = 0; round < 2; round++)
            {
                var offer = draft.Offers.First(o => o.IsUpgrade);
                Check(offer.Upgrade.CanApplyTo(offer.Weapon) && offer.Target.Upgrades.GetLevel(offer.Upgrade) == 0, "eligible unowned upgrade");
                draft.Select(draft.Offers.ToList().IndexOf(offer)); draft.Confirm();
            }
            var hand = WeaponHand.FromDraft(draft, 8);
            Check(draft.IsComplete && hand.Drawn.Count == 1 && hand.Selected.Upgrades.Count == 2, "upgrades survive hand transfer");
        }
    }

    private static PlayerCombatant Player(string name, Vector2 position)
    {
        var go = new GameObject(name);
        var player = go.AddComponent<PlayerCombatant>();
        if (Get<SpriteRenderer>(player, "visuals") == null) Call(player, "Awake");
        player.ConfigurePreview(weapons[0]);
        Reset(player, position);
        return player;
    }
    private static void Reset(PlayerCombatant player, Vector2 position)
    {
        player.ResetCombatant(position);
        Set(player, "invulnerableUntil", Time.time - 1);
        Set(player, "arenaExtents", new Vector2(8.2f,4.35f));
    }
    private static void Clear()
    {
        ProjectileBase.DespawnAll();
        Reset(owner, Vector2.zero); Reset(opponent, Vector2.right);
    }

    private static void TestShards()
    {
        var runtime = new WeaponRuntime(weapons[0]);
        var go = new GameObject("Shard test", typeof(RectTransform));
        var display = go.AddComponent<UpgradeShardDisplay>();
        display.Show(runtime, upgrades);
        Check(go.GetComponentsInChildren<Image>().Count(i => i.name.StartsWith("Upgrade Shard")) == 3, "three PNG shards");
        var upgrade = upgrades.Single(u => u.targetWeapon == weapons[0] && u.displaySlot == 2);
        runtime.TryUpgrade(upgrade);
        NearColor(go.transform.Find("Upgrade Shard 2").GetComponent<Image>().color, weapons[0].RarityColor, "acquired shard colored");
        NearColor(go.transform.Find("Upgrade Shard 0").GetComponent<Image>().color, UpgradeShardDisplay.LockedColor, "other shard remains gray");
        UnityEngine.Object.DestroyImmediate(go);
    }

    private static void TestLaser()
    {
        Clear();
        var warning = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(owner, owner.MuzzlePosition, Vector2.right, new WeaponStats(1,12,.75f,.13f,20,Color.white), WeaponEffects.LaserFreeWarning);
        warning.Configure(new BarrageStep {shape=BarrageShape.Line, followMuzzle=true, dealsDamage=false,duration=.75f});
        Near(owner.MovementMultiplier, 1, "free warning has no movement slow");
        var beam = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(owner, owner.MuzzlePosition, Vector2.right, new WeaponStats(1,12,.5f,.13f,20,Color.white), WeaponEffects.LaserFreeWarning);
        beam.Configure(new BarrageStep {shape=BarrageShape.Box,followMuzzle=true,dealsDamage=true,dimensions=new Vector2(20,.3f),duration=.5f});
        Check(Get<Vector2>(owner,"knockbackRemaining").x < 0, "actual laser applies backwards recoil");
        Near(owner.MovementMultiplier,.65f,"beam still slows movement");
        var recover = typeof(PlayerCombatant).GetMethod("RecoverAim",BindingFlags.Static|BindingFlags.NonPublic);
        object[] args = { Vector2.right,Vector2.up,0f,1f/60 };
        Vector2 first = (Vector2)recover.Invoke(null,args);
        Check(first.y>0 && first.x>.1f,"recovery is fast but not instant");
        for(int i=0;i<10;i++) {args[0]=first; first=(Vector2)recover.Invoke(null,args);}
        Check(Vector2.Distance(first,Vector2.up)<.01f,"recovery settles quickly");
    }

    private static void TestInterception()
    {
        Clear();
        var interceptor=ProjectileBase.SpawnBuiltin<BulletProjectile>(owner,Vector2.zero,Vector2.right,new WeaponStats(1,12,3,.195f,12,Color.white));
        interceptor.SetInterception(1);
        var enemy=ProjectileBase.SpawnBuiltin<BulletProjectile>(opponent,Vector2.right,Vector2.left,new WeaponStats(1,12,3,.13f,12,Color.white));
        Call(interceptor,"OnTriggerEnter2D",enemy.GetComponent<CircleCollider2D>());
        Check(!enemy.IsSpawned && interceptor.IsSpawned,"intercepts one enemy small bullet");
        var next=ProjectileBase.SpawnBuiltin<BulletProjectile>(opponent,Vector2.right,Vector2.left,new WeaponStats(1,12,3,.13f,12,Color.white));
        Call(interceptor,"OnTriggerEnter2D",next.GetComponent<CircleCollider2D>());
        Check(next.IsSpawned,"cannot intercept a second bullet");
        Call(interceptor,"RemoveProjectile");
        var reused=ProjectileBase.SpawnBuiltin<BulletProjectile>(owner,Vector2.zero,Vector2.right,new WeaponStats(1,12,3,.13f,12,Color.white));
        Call(reused,"OnTriggerEnter2D",next.GetComponent<CircleCollider2D>());
        Check(next.IsSpawned,"pool reuse clears interception charge");
    }

    private static void TestOverdrive()
    {
        Clear();
        Set(owner,"arenaExtents",new Vector2(100,100));
        var effect=ProjectileBase.SpawnBuiltin<OverdriveProjectile>(owner,Vector2.zero,Vector2.right,new WeaponStats(10,12,5,.13f,12,Color.white),WeaponEffects.OverdriveShield|WeaponEffects.OverdriveAcceleration|WeaponEffects.OverdriveHandling);
        Check(effect.ShieldRemaining==6,"double shield");
        for(int i=0;i<25;i++) Call(owner,"MoveControlled",Vector2.right,5.8f,.05f);
        Check(Get<Vector2>(owner,"inertiaVelocity").magnitude>11,"straight acceleration upgrade");
        float moving=Get<Vector2>(owner,"inertiaVelocity").magnitude;
        Call(owner,"MoveControlled",Vector2.zero,5.8f,.1f);
        Check(Get<Vector2>(owner,"inertiaVelocity").magnitude<moving*.6f,"handling reduces stopping inertia");
        for(int i=0;i<6;i++) Check(effect.AbsorbHit(),"shield absorbs hit");
        Check(!effect.IsSpawned && !owner.IsOverdriving,"sixth hit ends overdrive");
    }

    private static void Pulse(WeaponEffects effects, Vector2 target, bool postDash, Vector2? targetBounds=null)
    {
        Clear(); Reset(opponent,target);
        if(targetBounds.HasValue) Set(opponent,"arenaExtents",targetBounds.Value);
        if(postDash) Set(owner,"lastDashEndedAt",Time.time);
        var pulse=ProjectileBase.SpawnBuiltin<ElectricPulseProjectile>(owner,Vector2.zero,Vector2.right,new WeaponStats(2,12,.4f,.13f,3,Color.white),effects);
        pulse.Configure(weapons[4].steps[0].Snapshot());
        Call(pulse,"Hit",opponent.GetComponent<CircleCollider2D>());
        for(int i=0;i<120;i++) Call(opponent,"UpdatePulseImpulse",.02f);
    }

    private static void TestPulse()
    {
        Pulse(WeaponEffects.None,Vector2.right,false);
        Near(opponent.transform.position.x,3,"normal pulse reaches range edge",.02f);
        Check(opponent.HitsTaken==0,"normal pulse is harmless");
        Pulse(WeaponEffects.PulseDash,new Vector2(.5f,0),true,new Vector2(1.4f,4));
        Check(opponent.HitsTaken==1,"dash pulse wall collision damages once");
        Pulse(WeaponEffects.PulseDash,new Vector2(2,0),true);
        Near(opponent.transform.position.x,2,"empowered radius is halved");
        Pulse(WeaponEffects.PulsePull,new Vector2(2,0),false);
        Near(Vector2.Distance(owner.transform.position,opponent.transform.position),owner.BodyRadius+opponent.BodyRadius,"pull reaches caster",.02f);
        Check(opponent.HitsTaken==0,"ordinary pull cannot damage");
        Pulse(WeaponEffects.PulsePull|WeaponEffects.PulseDash,Vector2.right,true);
        Check(opponent.HitsTaken==1,"dash/pull combination contact damage once");
        Clear();
        var pull=ProjectileBase.SpawnBuiltin<ElectricPulseProjectile>(owner,Vector2.zero,Vector2.right,new WeaponStats(2,12,.4f,.13f,3,Color.white),WeaponEffects.PulsePull);
        pull.Configure(weapons[4].steps[0]);
        float initial=pull.GetComponent<LineRenderer>().GetPosition(0).magnitude;
        Set(pull,"startedAt",Time.time-.2f); Call(pull,"Tick",0f);
        Check(pull.GetComponent<LineRenderer>().GetPosition(0).magnitude<initial,"pull effect contracts");
    }

    private static void TestOrbAndBounce()
    {
        Clear();
        var runtime=FullyUpgraded(7);
        runtime.Fire(owner,owner.MuzzlePosition,Vector2.right);
        var orbs=UnityEngine.Object.FindObjectsByType<OrbitOrb>(FindObjectsSortMode.None).Where(o=>o.IsSpawned).OrderByDescending(o=>o.Stats.Radius).ToArray();
        Check(orbs.Length==2,"two axis orbs");
        Near(orbs[1].Stats.Radius,orbs[0].Stats.Radius*.5f,"secondary orb half size");
        Near(orbs[0].Stats.Lifetime,7.5f,"orb lifetime snapshot");
        Check(Vector2.Distance(orbs[0].transform.position,orbs[1].transform.position)>2.3f,"opposite orbital positions");
        Check(!orbs[1].IsSmallBullet,"small secondary orb is not interceptable bullet");
        Set(orbs[0],"startedAt",Time.time-1f); Call(orbs[0],"FollowOwner");
        Near(orbs[0].transform.position.y,-1.2f,"orb rotates 270 degrees per second",.001f);
        Clear();
        var bouncer=ProjectileBase.SpawnBuiltin<Bouncer>(owner,Vector2.zero,Vector2.right,new WeaponStats(1,15,3,.13f,12,Color.white),WeaponEffects.BouncerLifetime|WeaponEffects.BouncerRandom);
        float before=bouncer.RemainingLifetime;
        Call(bouncer,"MoveBouncing",9f);
        Near(bouncer.RemainingLifetime,before+.3f,"boundary adds 0.3 seconds",.01f);
        Vector2 heading=Get<Vector2>(bouncer,"heading");
        Check(heading.x<0 && Mathf.Abs(Mathf.Atan2(heading.y,-heading.x)*Mathf.Rad2Deg)<=20.01f,"random bounce stays within 20 degrees and inward");
        Clear();
        var wall=new GameObject("Validation Terrain",typeof(BoxCollider2D)); wall.transform.position=new Vector3(2,0); wall.transform.localScale=new Vector3(.2f,3,1);
        Physics2D.SyncTransforms();
        bouncer=ProjectileBase.SpawnBuiltin<Bouncer>(owner,Vector2.zero,Vector2.right,new WeaponStats(1,15,3,.13f,12,Color.white),WeaponEffects.BouncerLifetime);
        before=bouncer.RemainingLifetime; Call(bouncer,"MoveBouncing",3f);
        Check(Get<Vector2>(bouncer,"heading").x<0,"solid terrain reflection");
        Near(bouncer.RemainingLifetime,before+.3f,"terrain adds 0.3 seconds",.01f);
        UnityEngine.Object.DestroyImmediate(wall);
        var integral=typeof(Bouncer).GetMethod("IntegratedSpeed",BindingFlags.Static|BindingFlags.NonPublic);
        Check((float)integral.Invoke(null,new object[]{3f,4f,3f,.85f})>.8f,"extended bouncer keeps moving past original lifetime");
    }

    private static void Check(bool condition,string label) {assertions++;if(!condition)throw new Exception("FAILED: "+label);}
    private static void Near(float actual,float expected,string label,float tolerance=.0001f) => Check(Mathf.Abs(actual-expected)<=tolerance,label+" ("+actual+" / "+expected+")");
    private static void NearColor(Color actual,Color expected,string label)=>Check(Vector4.Distance(actual,expected)<.001f,label);
    private static FieldInfo Field(Type type,string name)
    {
        while(type!=null) {var field=type.GetField(name,Flags);if(field!=null)return field;type=type.BaseType;}
        throw new Exception("Missing field "+name);
    }
    private static T Get<T>(object instance,string name)=>(T)Field(instance.GetType(),name).GetValue(instance);
    private static void Set(object instance,string name,object value)=>Field(instance.GetType(),name).SetValue(instance,value);
    private static object Call(object instance,string name,params object[] args)
    {
        Type type=instance.GetType();
        while(type!=null)
        {
            var method=type.GetMethods(Flags|BindingFlags.DeclaredOnly).FirstOrDefault(m=>m.Name==name && m.GetParameters().Length==args.Length);
            if(method!=null)return method.Invoke(instance,args);
            type=type.BaseType;
        }
        throw new Exception("Missing method "+name);
    }
}
