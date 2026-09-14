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
            weapons = new[] {"Pulse Shot", "Basic Barrage 02", "Basic Barrage 03", "Basic Barrage 04", "Basic Barrage 05", "Basic Barrage 06", "Basic Barrage 07", "Basic Barrage 08", "Meteor Dive", "Octo Shot", "Boomerang", "Prism Shot"}
                .Select(name => AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/" + name + ".asset")).ToArray();
            upgrades = Resources.LoadAll<BarrageUpgradeDefinition>("WeaponUpgrades");
            Check(weapons.All(w => w != null), "12 weapon assets load");
            Check(upgrades.Length == 36 && Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades").Length == 36, "36 dedicated upgrades, no temporary entries");
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
            TestMeteorDive();
            TestMeteorUpgrades();
            TestOctoShot();
            TestBoomerang();
            TestPrismShot();
            TestPrismUpgrades();
            TestPrismWallVisual();
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
        Check(bomb.Count(s => s.shape == BarrageShape.Bullet) == 12, "6 fragments per explosion");
        Check(bomb.Count(s => s.shape == BarrageShape.Box) == 2, "one chain explosion maximum");
        var original = bomb.Where(s => !s.overridePosition).ToArray();
        var chained = bomb.Where(s => s.overridePosition).ToArray();
        Check(chained.Length == original.Length && chained.All(s => s.worldPosition == new Vector2(2,3)), "entire chain position snapshot");
        for (int i = 0; i < original.Length; i++)
        {
            Check(!ReferenceEquals(original[i], chained[i]) && original[i].shape == chained[i].shape, "independent chain steps");
            Near(chained[i].delay, original[i].delay + .35f, "chain timing offset");
            Near(chained[i].duration, original[i].duration, "chain retains upgraded duration");
        }
        var warning = chained.Single(s => s.shape == BarrageShape.Circle && !s.dealsDamage);
        var explosion = chained.Single(s => s.shape == BarrageShape.Box);
        Near(warning.delay + warning.duration, explosion.delay, "chain warning ends at explosion");
        Check(chained.Where(s => s.shape == BarrageShape.Bullet).All(s => Mathf.Approximately(s.delay, explosion.delay)), "chain fragments fire with explosion");
        var baseChain = WeaponPatternCompiler.Build(weapons[3].steps, WeaponEffects.BombChain, 0, Vector2.one);
        Check(baseChain.Count(s => s.shape == BarrageShape.Bullet) == 8 && baseChain.Count(s => s.shape == BarrageShape.Circle && !s.dealsDamage) == 2, "base chain has both warnings and four fragments each");
        Check(weapons[3].steps.Length == 6 && weapons[3].steps.All(s => !s.overridePosition), "bomb source asset untouched");
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
            draft.Select(0);
            var firstPick = draft.Offers[0];
            draft.Confirm();
            Check(draft.Choices.Count == 1 && ReferenceEquals(draft.Choices[0], firstPick), "draft choice history keeps actual first pick");
            for (int round = 0; round < 2; round++)
            {
                var offer = draft.Offers.First(o => o.IsUpgrade);
                Check(offer.Upgrade.CanApplyTo(offer.Weapon) && offer.Target.Upgrades.GetLevel(offer.Upgrade) == 0, "eligible unowned upgrade");
                draft.Select(draft.Offers.ToList().IndexOf(offer)); draft.Confirm();
                Check(ReferenceEquals(draft.Choices[draft.Choices.Count - 1], offer), "draft choice history retains upgrade target and order");
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
        Check(go.GetComponentsInChildren<UpgradeDiamondGraphic>().Length == 3, "three vector upgrade diamonds");
        var upgrade = upgrades.Single(u => u.targetWeapon == weapons[0] && u.displaySlot == 2);
        runtime.TryUpgrade(upgrade);
        NearColor(go.transform.Find("Upgrade Diamond 2").GetComponent<UpgradeDiamondGraphic>().color, weapons[0].RarityColor, "acquired shard colored");
        NearColor(go.transform.Find("Upgrade Diamond 0").GetComponent<UpgradeDiamondGraphic>().color, UpgradeShardDisplay.LockedColor, "other shard remains gray");
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
        Check(Get<Vector2>(owner,"laserRecoilRemaining").x < 0, "actual laser applies backwards recoil");
        Near(owner.MovementMultiplier,.65f,"beam still slows movement");
        Near(weapons[1].cooldown, 2f, "laser base cooldown nerf");
        Near(FullyUpgraded(1).Stats.Cooldown, 1.7f, "laser upgraded cooldown");
        foreach (int fps in new[] {30, 60, 120})
        {
            Clear();
            owner.ApplyLaserRecoil();
            Call(owner, "UpdateImpacts", 0f);
            Near(owner.transform.position.x, 0, "paused recoil does not move");
            for (int frame = 0; frame < Mathf.CeilToInt(fps / 12f); frame++)
            {
                Call(owner, "UpdateImpacts", 1f / fps);
                // Test against full forward speed, even without the beam's movement slow.
                Call(owner, "MoveControlled", Vector2.right, 5.8f, 1f / fps);
            }
            Check(owner.transform.position.x < -.25f, "laser overcomes forward movement at " + fps + " fps");
            owner.ApplyLaserRecoil();
            Reset(owner, Vector2.zero);
            Check(Get<Vector2>(owner, "laserRecoilRemaining") == Vector2.zero, "reset clears laser recoil");
        }
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
        Clear();
        Check(!owner.IsPostDashWindow,"pulse is not empowered before a dash");
        Set(owner,"dashing",true);
        Set(owner,"dashEndsAt",Time.time+.26f);
        Check(!owner.IsPostDashWindow,"pulse excludes early dash outside final 0.25 seconds");
        Set(owner,"dashEndsAt",Time.time+.24f);
        Check(owner.IsPostDashWindow,"pulse includes final 0.25 seconds of dash");
        Set(owner,"dashEndsAt",Time.time+.16f);
        Check(owner.IsPostDashWindow,"default short dash is included from its start");
        Set(owner,"dashing",false);
        Set(owner,"lastDashEndedAt",Time.time-.34f);
        Check(owner.IsPostDashWindow,"pulse includes 0.35 seconds after dash");
        Set(owner,"lastDashEndedAt",Time.time-.36f);
        Check(!owner.IsPostDashWindow,"pulse expires after post-dash window");
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
        Check(Get<OrbitOrb>(orbs[1], "orbitParent") == orbs[0], "second axis is centered on first orb");
        Near(Vector2.Distance(orbs[0].transform.position,orbs[1].transform.position),1.2f,"secondary orbital radius");
        Check(!orbs[1].IsSmallBullet,"small secondary orb is not interceptable bullet");
        Set(orbs[0],"startedAt",Time.time-1f); Call(orbs[0],"FollowOwner");
        Near(orbs[0].transform.position.y,-1.2f,"orb rotates 270 degrees per second",.001f);
        Set(orbs[1], "startedAt", Time.time - 1f); Call(orbs[1], "FollowOwner");
        Vector2 secondaryOffset = orbs[1].transform.position - orbs[0].transform.position;
        Near(secondaryOffset.x, -1.2f, "second axis rotates 540 degrees around first orb", .001f);
        Near(secondaryOffset.y, 0, "second axis independent phase", .001f);
        owner.transform.position = new Vector2(3, 0);
        Set(orbs[0], "updatedFrame", -1); Set(orbs[1], "updatedFrame", -1);
        Call(orbs[1], "UpdateOrbit", 1f / 60);
        Vector2 center = Get<Vector2>(orbs[0], "orbitCenter");
        Check(Vector2.Distance(center, owner.transform.position) > 2.8f, "dash leaves orbit center behind without leash snap");
        Near(Vector2.Distance(orbs[0].transform.position, orbs[1].transform.position), 1.2f, "child-first update keeps secondary centered");
        Call(orbs[0], "UpdateOrbit", 1f / 60);
        Check(Get<Vector2>(orbs[0], "orbitCenter") == center, "primary integrates once per frame");
        Set(orbs[0], "updatedFrame", -1); Set(orbs[1], "updatedFrame", -1);
        Call(orbs[1], "UpdateOrbit", 0f);
        Check(Get<Vector2>(orbs[0], "orbitCenter") == center, "paused follow preserves center");
        Call(orbs[0], "Despawn");
        Check(!orbs[1].IsSpawned, "primary despawn also removes satellite");
        foreach (int fps in new[] {30, 60, 120})
        {
            Clear();
            var orb = ProjectileBase.SpawnBuiltin<OrbitOrb>(owner, Vector2.zero, Vector2.right, runtime.Stats);
            Check(Get<OrbitOrb>(orb, "orbitParent") == null && Get<OrbitOrb>(orb, "satellite") == null, "pooled orb clears both axis links");
            for (int frame = 0; frame < fps; frame++)
            {
                owner.transform.position = Vector2.right * (5.8f * (frame + 1) / fps);
                Set(orb, "updatedFrame", -1);
                Call(orb, "UpdateOrbit", 1f / fps);
            }
            float lag = owner.transform.position.x - Get<Vector2>(orb, "orbitCenter").x;
            Check(lag > 2f && lag < 2.7f, "loose follow during sustained movement at " + fps + " fps");
            for (int frame = 0; frame < fps * 2; frame++)
            {
                Set(orb, "updatedFrame", -1);
                Call(orb, "UpdateOrbit", 1f / fps);
            }
            Check(Vector2.Distance(owner.transform.position, Get<Vector2>(orb, "orbitCenter")) < .02f, "orbit catches up after stopping");
        }
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

    private static void TestMeteorDive()
    {
        Clear();
        var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Meteor Dive.asset");
        Check(definition != null && definition.rarity == WeaponRarity.Unique,"meteor is unique");
        Near(definition.cooldown,10,"meteor cooldown");
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        Check(catalog.cards.Any(c => c.weapon == definition),"meteor available in catalog");
        Check(catalog.StarterCardIds().Count == 8 && !catalog.StarterCardIds().Contains("meteor-dive"),"existing starter deck unchanged");
        var deck = catalog.StarterCardIds(); deck[0] = "meteor-dive";
        Check(catalog.IsDeckValid("{\"cards\":["+string.Join(",",deck.Select(id=>"\""+id+"\""))+"]}"),"meteor can replace a starter card");
        var runtime = new WeaponRuntime(definition);
        runtime.Fire(owner,owner.MuzzlePosition,Vector2.right,new Vector2(2,-1));
        var dive = owner.MeteorDive;
        Check(dive != null && !owner.CanBeHit,"meteor enters invulnerable phase");
        Check(!owner.GetComponent<CircleCollider2D>().enabled,"hidden player has no collision");
        Check(owner.GetComponentsInChildren<SpriteRenderer>().All(r=>!r.enabled),"entire player disappears");
        Check(new WeaponRuntime(weapons[0]).Fire(owner,Vector2.zero,Vector2.right)==null,"other weapon cannot fire while diving");
        Check(runtime.CooldownRemaining>9.9f,"cooldown starts on activation");
        dive.SetTarget(new Vector2(100,-100));
        Check(dive.Target == new Vector2(0,-owner.ArenaHalfSize.y),"cursor does not teleport warning");
        Call(dive,"AdvanceTarget",.1f);
        Near(dive.Target.x,owner.BaseMoveSpeed*.9f*.1f,"horizontal tracking is slower than player");
        Call(dive,"AdvanceTarget",10f);
        Check(dive.Target == new Vector2(owner.ArenaHalfSize.x,-owner.ArenaHalfSize.y),"cursor x clamped and y fixed to floor");
        dive.SetTarget(new Vector2(2,-1));
        Call(dive,"AdvanceTarget",2f);
        Vector2 floorTarget = new Vector2(2,-owner.ArenaHalfSize.y);
        Reset(opponent,new Vector2(2,0));
        Set(dive,"startedAt",Time.time-1.99f); Call(dive,"Tick",0f);
        Check(!dive.IsDescending && !owner.GetComponent<SpriteRenderer>().enabled,"full two second disappearance");
        Set(dive,"startedAt",Time.time-2.1f); Call(dive,"Tick",0f);
        Check(dive.IsDescending && owner.GetComponent<SpriteRenderer>().enabled,"player visibly descends after warning");
        Near(owner.transform.position.x,2,"vertical descent follows target x");
        Check(owner.transform.position.y > -1,"descent begins above landing point");
        dive.SetTarget(Vector2.zero);
        Check(dive.Target == floorTarget,"target locks during descent");
        Set(dive,"startedAt",Time.time-2.21f); Call(dive,"Tick",0f);
        Check(!dive.IsSpawned && !owner.IsMeteorDiving,"landing restores player control");
        Check((Vector2)owner.transform.position == floorTarget,"player always lands at floor");
        Check(opponent.HitsTaken==1,"swept dive damages enemy on path even between frames");
        Check(owner.GetComponent<CircleCollider2D>().enabled && owner.CanBeHit,"landing restores collision and vulnerability");
        var fragments = UnityEngine.Object.FindObjectsByType<GravityFragment>(FindObjectsSortMode.None).Where(f=>f.IsSpawned).ToArray();
        Check(fragments.Length==MeteorDive.FragmentCount && fragments.All(f=>f.LinearVelocity.y>0),"eruption emits twelve upward fragments");
        var blast = UnityEngine.Object.FindObjectsByType<BarrageShapeProjectile>(FindObjectsSortMode.None).Single(b=>b.IsSpawned);
        Reset(opponent,floorTarget);
        Call(blast,"Hit",opponent.GetComponent<CircleCollider2D>());
        Check(opponent.HitsTaken==1,"landing explosion damages enemy");
        Clear();
        var damagingFragment=ProjectileBase.SpawnBuiltin<GravityFragment>(owner,new Vector2(-1,0),Vector2.right,new WeaponStats(1,12,3,.08f,1,Color.white));
        Call(damagingFragment,"Tick",.2f);
        Check(opponent.HitsTaken==1 && !damagingFragment.IsSpawned,"fragment sweep damages enemy and consumes fragment");
        Clear();
        foreach(int fps in new[]{30,60,120})
        {
            var fragment=ProjectileBase.SpawnBuiltin<GravityFragment>(owner,Vector2.zero,Vector2.up,new WeaponStats(1,8,3,.08f,1,Color.white));
            for(int frame=0;frame<fps;frame++) Call(fragment,"Tick",1f/fps);
            Near(fragment.transform.position.y,1,"ballistic displacement at "+fps+" fps",.001f);
            Near(fragment.LinearVelocity.y,-6,"gravity turns fragment downward at "+fps+" fps",.001f);
            Check(fragment.IsSmallBullet,"debris remains interceptable");
            Call(fragment,"RemoveProjectile");
        }
        runtime.ResetCooldown(); runtime.Fire(owner,owner.MuzzlePosition,Vector2.right,Vector2.one);
        owner.ResetCombatant(new Vector2(-2,0));
        Check(!owner.IsMeteorDiving && owner.GetComponent<CircleCollider2D>().enabled && owner.GetComponent<SpriteRenderer>().enabled,"reset during warning restores player");
        runtime.ResetCooldown(); runtime.Fire(owner,owner.MuzzlePosition,Vector2.right,Vector2.one);
        dive=owner.MeteorDive; Set(dive,"startedAt",Time.time-2.1f); Call(dive,"Tick",0f);
        dive.Cancel();
        Check((Vector2)owner.transform.position == new Vector2(-2,0) && !owner.IsMeteorDiving,"cancel during dive restores original position");
        Clear();
        runtime.ResetCooldown(); runtime.Fire(owner,owner.MuzzlePosition,Vector2.right,Vector2.one);
        dive = owner.MeteorDive;
        Check(!dive.RequestDive(),"activation frame cannot immediately confirm dive");
        Set(dive,"spawnedFrame",Time.frameCount-1);
        Set(dive,"startedAt",Time.time-.3f);
        Vector2 confirmedTarget = dive.Target;
        Check(dive.RequestDive() && dive.IsDescending,"new click confirms early descent");
        Check(!dive.RequestDive(),"repeated clicks cannot restart descent");
        dive.SetTarget(new Vector2(8,0));
        Check(dive.Target == confirmedTarget,"manual descent locks currently visible warning");
        Set(dive,"startedAt",Time.time-.51f); Call(dive,"Tick",0f);
        Check(!owner.IsMeteorDiving && (Vector2)owner.transform.position == confirmedTarget,"early descent lands after 0.2 seconds");
        Check(runtime.CooldownRemaining>9.9f,"early descent does not reset cooldown");
        Clear();
    }

    private static void TestMeteorUpgrades()
    {
        Clear();
        var runtime=FullyUpgraded(8);
        runtime.Fire(owner,owner.MuzzlePosition,Vector2.right,new Vector2(8,0));
        var dive=owner.MeteorDive;
        Check(owner.ShieldRemaining==0,"shield not granted during warning");
        Call(dive,"AdvanceTarget",.1f);
        Near(dive.Target.x,owner.BaseMoveSpeed*.9f*1.1f*.1f,"meteor tracking gains ten percent");
        Set(dive,"spawnedFrame",Time.frameCount-1);
        Check(dive.RequestDive(),"upgraded dive can confirm early");
        Check(owner.ShieldRemaining==1,"shield granted at descent start");
        var minis=UnityEngine.Object.FindObjectsByType<MeteorMiniTank>(FindObjectsSortMode.None).Where(m=>m.IsSpawned).OrderBy(m=>m.transform.position.x).ToArray();
        Check(minis.Length==2,"left and right mini tanks spawn");
        Near(minis[0].Stats.Radius,owner.BodyRadius*.5f,"mini tank body half size");
        Near(minis[1].transform.position.x-minis[0].transform.position.x,3,"mini tanks flank main dive");
        Set(dive,"startedAt",Time.time-.21f); Call(dive,"Tick",0f);
        foreach(var mini in minis) { Set(mini,"startedAt",Time.time-.21f); Call(mini,"Tick",0f); }
        var explosions=UnityEngine.Object.FindObjectsByType<BarrageShapeProjectile>(FindObjectsSortMode.None).Where(b=>b.IsSpawned).ToArray();
        Check(explosions.Length==3 && explosions.Count(b=>Mathf.Abs(b.Stats.Radius-.6f)<.001f)==2,"two half-radius mini explosions");
        Check(UnityEngine.Object.FindObjectsByType<GravityFragment>(FindObjectsSortMode.None).Count(f=>f.IsSpawned)==24,"12 main fragments plus 6 per mini tank");
        Check(owner.ShieldRemaining==1 && !owner.IsMeteorDiving,"shield persists after landing");
        Set(owner,"invulnerableUntil",Time.time-1);
        owner.ReceiveHit(opponent);
        Check(owner.ShieldRemaining==0 && owner.HitsTaken==0,"shield absorbs one hit without health spill");
        Set(owner,"invulnerableUntil",Time.time-1);
        owner.ReceiveHit(opponent);
        Check(owner.HitsTaken==1,"next unshielded hit damages health");
        owner.GrantLandingShield(); owner.GrantLandingShield();
        Check(owner.ShieldRemaining==1,"shield replenishes without stacking");
        Clear();
        Check(owner.ShieldRemaining==0,"reset clears landing shield");
        runtime.ResetCooldown(); runtime.Fire(owner,owner.MuzzlePosition,Vector2.right,Vector2.zero);
        dive=owner.MeteorDive; Set(dive,"spawnedFrame",Time.frameCount-1); dive.RequestDive(); dive.Cancel();
        Check(!UnityEngine.Object.FindObjectsByType<MeteorMiniTank>(FindObjectsSortMode.None).Any(m=>m.IsSpawned),"cancel cleans up mini tanks");
        Clear();
    }

    private static void TestOctoShot()
    {
        Clear();
        Check(weapons[9].rarity==WeaponRarity.Common,"octo common rarity");
        var catalog=AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        Check(catalog.cards.Any(c=>c.weapon==weapons[9]),"octo catalog registration");
        var runtime=new WeaponRuntime(weapons[9]);
        runtime.Fire(owner,owner.MuzzlePosition,Vector2.right);
        var shots=UnityEngine.Object.FindObjectsByType<OctoShot>(FindObjectsSortMode.None).Where(s=>s.IsSpawned).ToArray();
        Check(shots.Length==8,"octo fires eight bullets simultaneously");
        Check(shots.All(s=>(Vector2)s.transform.position==Vector2.zero),"octo fires around player center");
        var angles=shots.Select(s=>Mathf.Repeat(Mathf.Atan2(s.LinearVelocity.y,s.LinearVelocity.x)*Mathf.Rad2Deg+360,360)).OrderBy(a=>a).ToArray();
        for(int i=0;i<8;i++) Near(angles[i],i*45,"eight evenly spaced directions",.001f);
        Near(runtime.Stats.Speed,6,"slow octo speed");
        Near(shots[0].Stats.Lifetime,.8f,"short octo lifetime");
        Near(FullyUpgraded(9).Stats.Speed,7.5f,"octo rapid speed");
        Near(FullyUpgraded(9).Stats.Cooldown,1.19f,"octo rapid cooldown");
        Clear();
        Vector2? endpoint=null;
        foreach(int fps in new[]{30,60,120})
        {
            var curved=ProjectileBase.SpawnBuiltin<OctoShot>(owner,Vector2.zero,Vector2.right,runtime.Stats,WeaponEffects.OctoCurve);
            for(int i=0;i<fps/2;i++) Call(curved,"Tick",1f/fps);
            Check(curved.transform.position.y>.5f,"octo visibly curves");
            Near(curved.LinearVelocity.magnitude,6,"curve preserves speed",.001f);
            if(endpoint.HasValue) Check(Vector2.Distance(endpoint.Value,curved.transform.position)<.001f,"curve frame-rate independence");
            endpoint=curved.transform.position;
            Call(curved,"RemoveProjectile");
        }
        var upgraded=FullyUpgraded(9);
        upgraded.Fire(owner,owner.MuzzlePosition,Vector2.right);
        shots=UnityEngine.Object.FindObjectsByType<OctoShot>(FindObjectsSortMode.None).Where(s=>s.IsSpawned).ToArray();
        foreach(var shot in shots) { Set(shot,"despawnAt",Time.time-1); Call(shot,"Update"); }
        var children=UnityEngine.Object.FindObjectsByType<OctoShot>(FindObjectsSortMode.None).Where(s=>s.IsSpawned).ToArray();
        Check(children.Length==16,"eight parents split into sixteen children");
        Check(children.All(s=>!s.Effects.Has(WeaponEffects.OctoSplit) && s.Effects.Has(WeaponEffects.OctoCurve)),"children retain curve but cannot split again");
        Near(children[0].Stats.Radius,.065f,"split size halved");
        Near(children[0].Stats.Lifetime,.45f,"split children lifetime");
        foreach(var child in children) { Set(child,"despawnAt",Time.time-1); Call(child,"Update"); }
        Check(!UnityEngine.Object.FindObjectsByType<OctoShot>(FindObjectsSortMode.None).Any(s=>s.IsSpawned),"children expire without recursive splitting");
        upgraded.ResetCooldown(); upgraded.Fire(owner,owner.MuzzlePosition,Vector2.right);
        Clear();
        Check(!UnityEngine.Object.FindObjectsByType<OctoShot>(FindObjectsSortMode.None).Any(s=>s.IsSpawned),"cleanup never triggers splitting");
    }

    private static void TestBoomerang()
    {
        Clear();
        var definition=weapons[10];
        Check(definition.rarity==WeaponRarity.Rare,"boomerang rare rarity");
        Near(definition.cooldown,8,"boomerang base cooldown");
        Near(definition.projectileRadius,.432f,"boomerang base radius increased by twenty percent");
        Check(float.IsPositiveInfinity(definition.ResolveStats(null).Lifetime),"boomerang reports unlimited lifetime in stats");
        Near(FullyUpgraded(10).Stats.Radius,definition.projectileRadius*1.15f,"boomerang size upgrade");
        Check(FullyUpgraded(10).Upgrades.Effects.Has(WeaponEffects.BoomerangBounce),"bounce effect loads from existing high serialized word");
        var catalog=AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        Check(catalog.cards.Any(c=>c.weapon==definition),"boomerang catalog registration");
        Reset(opponent,new Vector2(0,-4));
        var runtime=FullyUpgraded(10);
        runtime.Fire(owner,owner.MuzzlePosition,Vector2.right);
        var boomerang=UnityEngine.Object.FindObjectsByType<Boomerang>(FindObjectsSortMode.None).Single(b=>b.IsSpawned);
        Check(float.IsPositiveInfinity(boomerang.Stats.Lifetime),"no boomerang lifetime timeout");
        Call(boomerang,"Tick",.2f);
        Check(boomerang.IsSpawned && !boomerang.IsReturning && boomerang.transform.position.x>1,"outbound cannot be caught at spawn");
        Check(Quaternion.Angle(boomerang.transform.rotation,Quaternion.identity)>1,"boomerang visibly spins");
        owner.EquipWeapon(weapons[0]);
        owner.CurrentWeapon.Fire(owner,owner.MuzzlePosition,Vector2.up);
        float otherCooldown=owner.CurrentWeapon.CooldownRemaining;
        for(int i=0;i<600 && boomerang.IsSpawned;i++) Call(boomerang,"Tick",1f/60);
        Check(!boomerang.IsSpawned,"boomerang returns to stationary owner and is caught");
        Near(runtime.CooldownRemaining,4,"upgraded catch refunds four seconds to original runtime",.01f);
        Near(owner.CurrentWeapon.CooldownRemaining,otherCooldown,"catch does not affect selected other weapon",.01f);
        Clear();
        runtime.ResetCooldown(); runtime.Fire(owner,owner.MuzzlePosition,Vector2.right);
        boomerang=UnityEngine.Object.FindObjectsByType<Boomerang>(FindObjectsSortMode.None).Single(b=>b.IsSpawned);
        boomerang.transform.position=Vector2.zero;
        owner.transform.position=new Vector2(0,3);
        Call(boomerang,"Tick",.275f);
        Near(boomerang.LinearVelocity.magnitude,10.5f,"outbound speed gradually drops after launch",.001f);
        Call(boomerang,"Tick",.275f);
        Near(boomerang.LinearVelocity.magnitude,7f,"outbound reaches half speed at halfway time",.001f);
        Call(boomerang,"Tick",.5f);
        Check(!boomerang.IsReturning && boomerang.LinearVelocity.magnitude<.7f,"outbound nearly stops before snap return");
        Call(boomerang,"Tick",.051f);
        Check(boomerang.IsReturning,"return begins at outbound duration");
        Near(boomerang.transform.position.x,7.7f,"deceleration preserves outbound reach",.025f);
        Near(boomerang.LinearVelocity.magnitude,21f,"return is faster than outbound",.001f);
        var returnVelocity=boomerang.LinearVelocity;
        Check(Vector2.Dot(returnVelocity.normalized,((Vector2)owner.transform.position-(Vector2)boomerang.transform.position).normalized)>.9999f,"return targets current owner position at turnaround");
        owner.transform.position=new Vector2(0,-3);
        Call(boomerang,"Tick",.45f);
        Check(boomerang.IsSpawned && boomerang.transform.position.x<0,"missed catch continues behind launch point");
        Near(Vector2.Distance(boomerang.LinearVelocity,returnVelocity),0,"missed catch keeps return heading without homing");
        Near(runtime.CooldownRemaining,8,"missed catch grants no cooldown refund",.01f);
        Clear();
        var baseline=new WeaponRuntime(definition);
        baseline.Fire(owner,owner.MuzzlePosition,Vector2.right);
        boomerang=UnityEngine.Object.FindObjectsByType<Boomerang>(FindObjectsSortMode.None).Single(b=>b.IsSpawned);
        Call(boomerang,"OnTriggerEnter2D",opponent.GetComponent<CircleCollider2D>());
        Check(boomerang.IsSpawned && opponent.HitsTaken==1,"basic boomerang persists on enemy hit");
        Call(boomerang,"OnTriggerStay2D",opponent.GetComponent<CircleCollider2D>());
        Check(opponent.HitsTaken==1,"basic boomerang respects hit invulnerability");
        Set(opponent,"invulnerableUntil",Time.time-1);
        Call(boomerang,"OnTriggerStay2D",opponent.GetComponent<CircleCollider2D>());
        Check(opponent.HitsTaken==2,"basic boomerang damages again after invulnerability");
        Near(baseline.CooldownRemaining,8,"enemy hit does not count as catch",.01f);
        Call(boomerang,"Tick",Boomerang.OutboundDuration);
        owner.transform.position=new Vector2(0,3);
        Call(boomerang,"Tick",20f);
        Check(!boomerang.IsSpawned && boomerang.transform.position.x < -owner.ArenaHalfSize.x,"basic boomerang exits without bouncing");
        Clear(); Reset(opponent,new Vector2(0,-4));
        baseline.ResetCooldown(); baseline.Fire(owner,owner.MuzzlePosition,Vector2.right);
        boomerang=UnityEngine.Object.FindObjectsByType<Boomerang>(FindObjectsSortMode.None).Single(b=>b.IsSpawned);
        Call(boomerang,"Tick",2f);
        Check(!boomerang.IsSpawned,"unupgraded boomerang is caught");
        Near(baseline.CooldownRemaining,6,"basic catch refunds two seconds",.01f);
        Clear(); runtime.ResetCooldown(); runtime.Fire(owner,owner.MuzzlePosition,Vector2.right);
        boomerang=UnityEngine.Object.FindObjectsByType<Boomerang>(FindObjectsSortMode.None).Single(b=>b.IsSpawned);
        Call(boomerang,"OnTriggerEnter2D",opponent.GetComponent<CircleCollider2D>());
        Check(boomerang.IsSpawned && opponent.HitsTaken==1,"bounce upgrade also persists after hit");
        Call(boomerang,"OnTriggerStay2D",opponent.GetComponent<CircleCollider2D>());
        Check(opponent.HitsTaken==1,"persistent boomerang respects hit invulnerability");
        Set(opponent,"invulnerableUntil",Time.time-1);
        Call(boomerang,"OnTriggerStay2D",opponent.GetComponent<CircleCollider2D>());
        Check(opponent.HitsTaken==2,"persistent boomerang can damage again after invulnerability");
        boomerang.transform.position=Vector2.zero;
        Set(boomerang,"returning",true);
        Set(boomerang,"velocity",Vector2.right*21f);
        owner.transform.position=new Vector2(0,3);
        Call(boomerang,"Tick",.4f);
        Check(boomerang.IsSpawned && boomerang.LinearVelocity.x<0 && Get<int>(boomerang,"bouncesRemaining")==1,"first wall reflects boomerang once");
        Near(boomerang.LinearVelocity.magnitude,21,"wall reflection preserves return speed");
        Call(boomerang,"Tick",.8f);
        Check(boomerang.IsSpawned && boomerang.LinearVelocity.x>0 && Get<int>(boomerang,"bouncesRemaining")==0,"second wall uses final reflection");
        Call(boomerang,"Tick",.8f);
        Check(!boomerang.IsSpawned && boomerang.transform.position.x>owner.ArenaHalfSize.x,"third wall is passed and missed boomerang is cleaned up");
        runtime.ReduceCooldown(100);
        Near(runtime.CooldownRemaining,0,"refund clamps at ready without negative cooldown");
        Clear();
        Check(!UnityEngine.Object.FindObjectsByType<Boomerang>(FindObjectsSortMode.None).Any(b=>b.IsSpawned),"reset cleanup removes boomerangs");
    }

    private static void TestPrismUpgrades()
    {
        Clear();
        Check(weapons[8].displayName=="Meteor Dive","meteor final name");
        var runtime=FullyUpgraded(11);
        Near(runtime.Stats.Cooldown,4.25f,"prism upgraded cooldown");
        Reset(opponent,new Vector2(-6,-3));
        runtime.Fire(owner,owner.MuzzlePosition,Vector2.right);
        var timeline=UnityEngine.Object.FindObjectsByType<PrismShot>(FindObjectsSortMode.None).Single(p=>p.IsSpawned);
        PrismPellet[] Shots()=>UnityEngine.Object.FindObjectsByType<PrismPellet>(FindObjectsSortMode.None).Where(p=>p.IsSpawned).ToArray();
        Check(owner.IsPrismAimLocked,"prism locks muzzle during emission");
        Near(Vector2.Angle((Vector2)Call(owner,"UpdatePrismAim",Vector2.up,.1f),Vector2.right),0,"locked aim ignores cursor");
        Call(timeline,"EmitDueAt",.151f);
        var original=Shots().Where(p=>p.Slot>=3 && p.Slot<=5).ToArray();
        Call(timeline,"EmitDueAt",.301f); Check(Shots().Length==7 && owner.IsPrismAimLocked,"tail keeps muzzle locked after base seven");
        Call(timeline,"EmitDueAt",.451f); Check(Shots().Length==10,"tail repeats three central shots");
        foreach(var first in original)
        {
            var tail=Shots().Single(p=>p.Slot==first.Slot && p!=first);
            float start=Get<float>(first,"firedAt");
            Near(Vector2.Distance(first.PositionAt(start+.7f),tail.PositionAt(start+1f)),0,"tail repeats time-shifted trajectory",.001f);
        }
        Call(timeline,"EmitDueAt",.601f);
        Check(Shots().Length==11 && Shots().Count(p=>p.Slot==4)==3,"tail ends with one extra central bullet");
        Check(!owner.IsPrismAimLocked,"muzzle releases after final tail shot");
        float recoveryAngle=Vector2.Angle((Vector2)Call(owner,"UpdatePrismAim",Vector2.up,.02f),Vector2.right);
        Check(recoveryAngle>1 && recoveryAngle<90,"muzzle recovers quickly without snapping");
        Near(Vector2.Angle((Vector2)Call(owner,"UpdatePrismAim",Vector2.up,.5f),Vector2.up),0,"muzzle recovery reaches cursor");
        Clear();
        var stats=new WeaponStats(5,6,2,.1f,5,Color.blue);
        var pellet=ProjectileBase.SpawnBuiltin<PrismPellet>(owner,Vector2.zero,Vector2.right,stats,WeaponEffects.PrismExplosion);
        pellet.Configure(4,Vector2.zero,Vector2.zero,Vector2.right,Time.time);
        Call(pellet,"OnTriggerEnter2D",opponent.GetComponent<CircleCollider2D>());
        var blasts=UnityEngine.Object.FindObjectsByType<BarrageShapeProjectile>(FindObjectsSortMode.None).Where(p=>p.IsSpawned).ToArray();
        Check(!pellet.IsSpawned && blasts.Length==1 && opponent.HitsTaken==1,"impact damages target and spawns one blast");
        Near(blasts[0].Stats.Radius,.6f,"impact blast radius");
        var neighbor=Player("Blast neighbor",blasts[0].transform.position+Vector3.right*.3f);
        Set(neighbor,"invulnerableUntil",Time.time-1);
        Call(blasts[0],"OnTriggerEnter2D",neighbor.GetComponent<CircleCollider2D>());
        Check(neighbor.HitsTaken==1,"impact blast damages nearby target");
        UnityEngine.Object.DestroyImmediate(neighbor.gameObject);
        Clear();
        runtime.ResetCooldown(); runtime.Fire(owner,owner.MuzzlePosition,Vector2.right);
        ProjectileBase.DespawnOwnedBy(owner);
        Check(!owner.IsPrismAimLocked,"cancellation releases muzzle");
        Check(!UnityEngine.Object.FindObjectsByType<BarrageShapeProjectile>(FindObjectsSortMode.None).Any(p=>p.IsSpawned),"cancellation creates no explosions");
        Clear();
    }

    private static void TestPrismShot()
    {
        Clear(); Reset(opponent,new Vector2(-6,-3));
        var definition=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Prism Shot.asset");
        Check(definition!=null && definition.rarity==WeaponRarity.Rare,"prism rare rarity");
        Near(definition.cooldown,5,"prism five second cooldown");
        Check(definition.displayName=="Prism Shot","prism final name");
        Check(definition.icon!=null && AssetDatabase.GetAssetPath(definition.icon)=="Assets/UI/BarrageIcons/PrismShot.png","prism orbital PNG icon");
        Near(definition.projectileRadius,.1f,"prism reduced size");
        Near(PrismShot.FormationTime,.8f,"prism earlier formation");
        var catalog=AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        Check(catalog.cards.Any(c=>c.weapon==definition),"prism catalog registration");
        var runtime=new WeaponRuntime(definition);
        Vector2 anchor=owner.MuzzlePosition;
        Check(float.IsPositiveInfinity(runtime.Stats.Lifetime),"prism weapon has no lifetime limit");
        runtime.Fire(owner,anchor,Vector2.right);
        var timeline=UnityEngine.Object.FindObjectsByType<PrismShot>(FindObjectsSortMode.None).Single(p=>p.IsSpawned);
        PrismPellet[] Shots()=>UnityEngine.Object.FindObjectsByType<PrismPellet>(FindObjectsSortMode.None).Where(p=>p.IsSpawned).OrderBy(p=>p.Slot).ToArray();
        Check(Shots().Select(p=>p.Slot).SequenceEqual(new[]{1,7}),"outer slow pair fires first");
        Call(timeline,"EmitDueAt",.149f); Check(Shots().Length==2,"no early orbit pair");
        owner.transform.position=new Vector2(.4f,0);
        Call(timeline,"EmitDueAt",.151f);
        Check(Shots().Select(p=>p.Slot).SequenceEqual(new[]{1,3,4,5,7}),"orbit pair and central bullet fire together");
        Check((Vector2)Shots().Single(p=>p.Slot==3).transform.position==owner.MuzzlePosition,"delayed pair uses current muzzle");
        Call(timeline,"EmitDueAt",.299f); Check(Shots().Length==5,"helix pair waits until third group");
        Call(timeline,"EmitDueAt",.301f);
        var shots=Shots(); Check(shots.Length==7 && !timeline.IsSpawned,"helix pair fires last and timeline ends");
        Check(shots.All(p=>float.IsPositiveInfinity(p.Stats.Lifetime)),"every prism pellet has unlimited lifetime");
        float start=Get<float>(shots[0],"firedAt");
        float lastY=float.PositiveInfinity;
        foreach(var pellet in shots)
        {
            Vector2 point=pellet.PositionAt(start+PrismShot.FormationTime);
            if(pellet.Slot==3 || pellet.Slot==5)
                Near(point.x,anchor.x+5,"orbit pair aligns with central bullet at fan",.001f);
            else Near(Vector2.Distance(anchor,point),5,"other pellets reach radius five",.001f);
            Check(point.y<lastY,"slots ordered left to right in fan"); lastY=point.y;
            Check(Vector2.Distance(anchor,pellet.PositionAt(start+PrismShot.FormationTime+.3f))>5,"pellet scatters beyond fan");
            float delay=PrismShot.LaunchDelay(pellet.Slot);
            Near(Vector2.Distance(pellet.PositionAt(start+delay),Get<Vector2>(pellet,"launch")),0,"all paths start at their own muzzle",.001f);
        }
        var helix=shots.Single(p=>p.Slot==2);
        var helixPeer=shots.Single(p=>p.Slot==6);
        float sample=start+PrismShot.FormationTime+.25f;
        Near(helix.PositionAt(sample).x,helixPeer.PositionAt(sample).x,"2 and 6 oscillate laterally at same forward position",.001f);
        var orbit=shots.Single(p=>p.Slot==3);
        float orbitHalf=start+PrismShot.GroupDelay+(PrismShot.FormationTime-PrismShot.GroupDelay)*.5f;
        Check(orbit.PositionAt(orbitHalf).y>0,"orbit approaches fan with slower revolution");
        var peer=shots.Single(p=>p.Slot==5);
        Check(Mathf.Abs(orbit.PositionAt(sample).x-peer.PositionAt(sample).x)>.5f,"3 and 5 orbit with opposite forward offsets");
        Vector2 midpoint=(orbit.PositionAt(orbitHalf)+peer.PositionAt(orbitHalf))*.5f;
        Near(midpoint.y,0,"orbital pair shares moving center",.001f);
        var core=shots.Single(p=>p.Slot==4);
        foreach(float offset in new[]{.16f,.25f,.5f,.8f,1.05f,1.55f})
            Near(Vector2.Distance((orbit.PositionAt(start+offset)+peer.PositionAt(start+offset))*.5f,core.PositionAt(start+offset)),0,"3 and 5 orbit the actual central trajectory",.001f);
        foreach(var pellet in shots)
        {
            float formation=start+PrismShot.FormationTime;
            Vector2 before=(pellet.PositionAt(formation)-pellet.PositionAt(formation-.0001f))/.0001f;
            Vector2 after=(pellet.PositionAt(formation+.0001f)-pellet.PositionAt(formation))/.0001f;
            Check(Vector2.Distance(before,after)<Mathf.Max(.2f,before.magnitude*.015f),"smooth motion through fan");
        }
        float helixAfter=start+PrismShot.FormationTime+.75f;
        Check(helix.PositionAt(helixAfter).y<0,"helix continues crossing after fan");
        float orbitAfter=start+PrismShot.FormationTime+.75f;
        Check(orbit.PositionAt(orbitAfter).y<0 && peer.PositionAt(orbitAfter).y>0,"orbit continues revolving after fan");
        Clear();
        var collision=ProjectileBase.SpawnBuiltin<PrismPellet>(owner,Vector2.zero,Vector2.right,new WeaponStats(5,4,2,.13f,5,Color.white));
        collision.Configure(4,Vector2.zero,Vector2.zero,Vector2.right,Time.time-.8f);
        Reset(opponent,new Vector2(1,0)); Call(collision,"Tick",0f);
        Check(opponent.HitsTaken==1 && !collision.IsSpawned,"prism pellet swept collision damage");
        Clear();
        foreach(var effect in new[]{WeaponEffects.None,WeaponEffects.PrismExplosion})
        {
            foreach(var direction in new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down})
            {
                Clear(); Reset(opponent,new Vector2(-6,-3));
                var wallShot=ProjectileBase.SpawnBuiltin<PrismPellet>(owner,Vector2.zero,direction,definition.ResolveStats(null),effect);
                wallShot.Configure(4,Vector2.zero,Vector2.zero,direction,Time.time-5f);
                Call(wallShot,"Tick",0f);
                Check(!wallShot.IsSpawned,"prism despawns at arena wall with or without explosion");
                Vector2 limit=owner.ArenaHalfSize-Vector2.one*definition.projectileRadius;
                float expected=direction.x!=0 ? limit.x : limit.y;
                Near(Vector2.Dot(wallShot.transform.position,direction),expected,"wall impact stops at radius-adjusted boundary",.001f);
                int blasts=UnityEngine.Object.FindObjectsByType<BarrageShapeProjectile>(FindObjectsSortMode.None).Count(p=>p.IsSpawned);
                Check(blasts==(effect==WeaponEffects.PrismExplosion ? 1 : 0),"wall explosion occurs exactly once only with upgrade");
                if(blasts==1)
                {
                    var blast=UnityEngine.Object.FindObjectsByType<BarrageShapeProjectile>(FindObjectsSortMode.None).Single(p=>p.IsSpawned);
                    Near(Vector2.Dot(blast.transform.position,direction),expected+definition.projectileRadius,"wall contact is explosion center",.001f);
                }
            }
            Clear(); Reset(opponent,new Vector2(-6,-3));
            var wall=new GameObject("Prism test wall",typeof(BoxCollider2D));
            wall.transform.position=Vector2.right*2;
            var terrainShot=ProjectileBase.SpawnBuiltin<PrismPellet>(owner,Vector2.zero,Vector2.right,definition.ResolveStats(null),effect);
            terrainShot.Configure(4,Vector2.zero,Vector2.zero,Vector2.right,Time.time-.8f);
            Call(terrainShot,"Tick",0f);
            Check(!terrainShot.IsSpawned,"prism swept collision despawns on solid terrain");
            int terrainBlasts=UnityEngine.Object.FindObjectsByType<BarrageShapeProjectile>(FindObjectsSortMode.None).Count(p=>p.IsSpawned);
            Check(terrainBlasts==(effect==WeaponEffects.PrismExplosion ? 1 : 0),"terrain explosion follows upgrade");
            UnityEngine.Object.DestroyImmediate(wall);
        }
        Clear();
    }

    private static void TestPrismWallVisual()
    {
        Clear(); Reset(opponent,new Vector2(-6,-3));
        var border=new GameObject("Arena Border",typeof(LineRenderer)).GetComponent<LineRenderer>();
        border.positionCount=4;
        border.SetPositions(new[]{new Vector3(-8.7f,-4.65f),new Vector3(-8.7f,4.65f),new Vector3(8.7f,4.65f),new Vector3(8.7f,-4.65f)});
        border.enabled=false;
        var pellet=ProjectileBase.SpawnBuiltin<PrismPellet>(owner,Vector2.zero,Vector2.right,weapons[11].ResolveStats(null),WeaponEffects.PrismExplosion);
        pellet.Configure(4,Vector2.zero,Vector2.zero,Vector2.right,Time.time-5);
        Call(pellet,"Tick",0f);
        var blast=UnityEngine.Object.FindObjectsByType<BarrageShapeProjectile>(FindObjectsSortMode.None).Single(p=>p.IsSpawned);
        Near(blast.transform.position.x,8.7f,"visible arena border defines explosion center instead of player movement limit");
        Call(blast,"LateUpdate");
        foreach(var renderer in blast.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Check(renderer.sharedMaterial.shader.name=="GlowGlow/ArenaClippedSprite","blast, flash and glow use world clipping");
            var properties=new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
            Near(properties.GetVector("_ClipRect").z,8.7f,"clip aligns with visible wall");
        }
        if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            var camera=new GameObject("Prism wall verification camera",typeof(Camera)).GetComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=1;
            camera.transform.position=new Vector3(8.7f,0,-10);
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            var target=new RenderTexture(128,128,24);
            var texture=new Texture2D(128,128,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,128,128),0,0); texture.Apply();
            Directory.CreateDirectory("ValidationOutput");
            File.WriteAllBytes("ValidationOutput/prism-wall-clipping.png",texture.EncodeToPNG());
            int inside=0,outside=0;
            for(int y=0;y<128;y++) for(int x=0;x<128;x++)
            {
                if(texture.GetPixel(x,y).maxColorComponent<.025f) continue;
                if(x<64) inside++; else outside++;
            }
            RenderTexture.active=previous; camera.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            Check(inside>100,"wall-centered blast renders inside arena");
            Check(outside==0,"blast including flash and glow is invisible outside wall");
        }
        ProjectileBase.DespawnAll();
        foreach(var renderer in blast.GetComponentsInChildren<SpriteRenderer>(true))
            Check(renderer.sharedMaterial.shader.name!="GlowGlow/ArenaClippedSprite","pooled shape restores unclipped material");
        UnityEngine.Object.DestroyImmediate(border.gameObject);
        Clear();
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
