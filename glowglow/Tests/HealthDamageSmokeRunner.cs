// Copy into Assets only in the isolated validation project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Serializing;
using GlowGlow.Online;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HealthDamageSmokeRunner : MonoBehaviour
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<string> Results = new();
    private static PlayerCombatant owner, target;
    private static DeckCatalog catalog;
    private static int checks;

    public static void Begin()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/HealthValidation.unity");
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isBatchMode && Environment.CommandLine.Contains("HealthDamageSmokeRunner.Begin"))
            new GameObject("Health damage validation").AddComponent<HealthDamageSmokeRunner>().StartCoroutine(Run());
    }

    private static IEnumerator Run()
    {
        DontDestroyOnLoad(GameObject.Find("Health damage validation"));
        Directory.CreateDirectory("ValidationOutput");
        var iterator = Verify();
        while (true)
        {
            object next;
            try { if (!iterator.MoveNext()) break; next = iterator.Current; }
            catch (Exception ex)
            {
                Time.timeScale = 1;
                File.WriteAllText("ValidationOutput/health-result.txt", string.Join("\n", Results) + "\nFAIL: " + ex);
                Debug.LogException(ex);
                EditorApplication.Exit(1);
                yield break;
            }
            yield return next;
        }
        Results.Add("PASS: " + checks + " health/damage checks.");
        File.WriteAllText("ValidationOutput/health-result.txt", string.Join("\n", Results));
        Debug.Log("HEALTH_DAMAGE_PASSED: " + checks);
        EditorApplication.Exit(0);
    }

    private static IEnumerator Verify()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
        catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        owner = Player("Caster", new Vector2(-4, 0));
        target = Player("Target", new Vector2(4, 0));
        yield return null;
        var expected = new Dictionary<string, int>
        {
            {"Basic Shot",6},{"Laser Shot",22},{"Spread Shot",10},{"Bomb",30},{"Electric Pulse",0},
            {"Overdrive",20},{"Bouncer",10},{"Orbit Orb",16},{"4-axis",10},{"Meteor Dive",32},
            {"Octo Shot",8},{"Boomerang",24},{"Prism Shot",10},{"Solar System",24},{"Mine",32},
            {"Firefly Convergence",8},{"Twin Satellites",12},{"Blossom Burst",0},{"Constellation Ignition",24}
        };
        var upgrades = Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades");
        Check(catalog.cards.Length == expected.Count, "All 19 damage profiles are covered");
        foreach (var card in catalog.cards)
        {
            var runtime = new WeaponRuntime(card.weapon);
            Check(runtime.Stats.Damage == expected[card.weapon.displayName], "Saved base damage: " + card.weapon.displayName);
            foreach (var upgrade in upgrades.Where(u => u.CanApplyTo(card.weapon))) runtime.TryUpgrade(upgrade);
            Check(runtime.Stats.Damage == expected[card.weapon.displayName], "Upgrades retain base damage: " + card.weapon.displayName);
            Clear();
            var fired = runtime.Fire(owner, owner.MuzzlePosition, Vector2.right, target.transform.position);
            Check(fired != null && fired.Stats.Damage == expected[card.weapon.displayName], "Firing snapshot: " + card.weapon.displayName);
        }

        Clear();
        var spread = Weapon("Spread Shot");
        Check(spread.steps.Select(s => s.damage).OrderBy(n => n).SequenceEqual(new[] {6,6,8,8,10}), "Spread central, inner and outer damage");
        var extra = WeaponPatternCompiler.Build(spread.steps, WeaponEffects.SpreadExtra, 1, Vector2.zero);
        Check(extra.Skip(spread.steps.Length).All(s => s.damage == 6), "Extra spread spokes use outer damage");
        var bomb = Weapon("Bomb");
        Check(bomb.steps.Where(s => s.shape == BarrageShape.Bullet).All(s => s.damage == 6), "Bomb fragments are distinct from 30-damage explosion");
        var chain = WeaponPatternCompiler.Build(bomb.steps, WeaponEffects.BombChain | WeaponEffects.BombFragments, 0, Vector2.zero);
        Check(chain.Count(s => s.shape == BarrageShape.Bullet && s.damage == 6) == 12, "Both upgraded bomb waves retain fragment damage");

        Clear();
        Vulnerable(target);
        var basic = ProjectileBase.SpawnBuiltin<BulletProjectile>(owner, new Vector2(-1, 0), Vector2.right, Weapon("Basic Shot").ResolveStats(null));
        target.transform.position = Vector2.zero;
        yield return new WaitForSeconds(.16f);
        Check(target.Health == 94 && target.HitsTaken == 1 && !basic.IsSpawned, "Real Physics2D bullet subtracts 6 HP and despawns");
        float immuneUntil = (float)Field(target, "invulnerableUntil");
        yield return null;
        target.ReceiveHit(owner, 32);
        Check(target.Health == 94 && target.HitsTaken == 1, "Larger attack on later frame respects 0.8-second immunity");
        Check(immuneUntil > Time.time + .5f, "Existing hit immunity duration retained");

        Clear(); Vulnerable(target);
        Hit(ProjectileBase.SpawnBuiltin<BulletProjectile>(owner, Vector2.zero, Vector2.right, Stats(6)), target);
        Hit(ProjectileBase.SpawnBuiltin<BulletProjectile>(owner, Vector2.zero, Vector2.right, Stats(32)), target);
        target.ReceiveHit(owner, 20);
        Check(target.Health == 68 && target.DamageTaken == 32 && target.HitsTaken == 1, "Same-frame 6/32/20 overlap applies maximum 32 once");
        Clear(); Vulnerable(target);
        target.ReceiveHit(owner, 32); target.ReceiveHit(owner, 20); target.ReceiveHit(owner, 6);
        Check(target.Health == 68 && target.HitsTaken == 1, "Maximum damage is independent of collision order");
        Clear(); Vulnerable(target); target.GrantLandingShield();
        target.ReceiveHit(owner, 6); target.ReceiveHit(owner, 32);
        Check(target.Health == 100 && target.HitsTaken == 0 && target.ShieldRemaining == 0, "Landing shield absorbs one full overlapping hit without spill");
        Clear(); Vulnerable(target);
        var drive = ProjectileBase.SpawnBuiltin<OverdriveProjectile>(target, target.transform.position, Vector2.right, Stats(20));
        target.ReceiveHit(owner, 32); target.ReceiveHit(owner, 20);
        Check(target.Health == 100 && drive.ShieldRemaining == 2, "Overdrive consumes only one shield for overlap");
        Clear(); Vulnerable(target);
        var sun = ProjectileBase.SpawnBuiltin<SolarSystemProjectile>(target, target.transform.position, Vector2.right, Stats(24));
        target.ReceiveHit(owner, 32); target.ReceiveHit(owner, 6);
        Check(target.Health == 100 && sun.ShieldRemaining == 2, "Solar shield absorbs full damage once");
        Clear(); Vulnerable(target); target.ReceiveHit(owner, 0); target.ReceiveHit(owner, -8);
        Check(target.Health == 100 && target.CanBeHit && target.HitsTaken == 0, "Zero/negative damage cannot consume immunity or health");
        for (int i = 0; i < 17; i++) { Vulnerable(target); target.ReceiveHit(owner, 6); }
        Check(target.Health == 0 && target.HitsTaken == 17, "100 HP survives 16 basic hits and reaches zero on hit 17");
        target.ResetCombatant(Vector2.zero);
        Check(target.Health == 100 && target.HitsTaken == 0 && target.DamageTaken == 0, "Reset clears health, damage and hit telemetry");

        Clear();
        MeteorDive.EmitEruption(owner, Vector2.zero, Stats(32), 1);
        Check(Active<BarrageShapeProjectile>().Single().Damage == 20 && Active<GravityFragment>().All(p => p.Damage == 4), "Meteor blast 20 and 12 fragments at 4 each");
        Check(Active<GravityFragment>().Length == 12, "Full meteor fragment count retained");
        Clear(); MeteorDive.EmitEruption(owner, Vector2.zero, Stats(16), .5f);
        Check(Active<BarrageShapeProjectile>().Single().Damage == 10 && Active<GravityFragment>().All(p => p.Damage == 2), "Mini meteor blast 10 and fragments 2");
        Clear();
        var meteor = ProjectileBase.SpawnBuiltin<MeteorDive>(owner, Vector2.zero, Vector2.down, Stats(32), WeaponEffects.MeteorMiniTanks);
        Call(meteor, "BeginDescent", 0f);
        Check(Active<MeteorMiniTank>().Length == 2 && Active<MeteorMiniTank>().All(p => p.Damage == 16), "Mini meteor direct-hit damage 16");
        Clear();
        var octo = ProjectileBase.SpawnBuiltin<OctoShot>(owner, Vector2.zero, Vector2.right, Stats(8), WeaponEffects.OctoSplit);
        Call(octo, "OnLifetimeExpired");
        Check(Active<OctoShot>().Where(p => p != octo).Count(p => p.Damage == 4) == 2, "Octo secondary spokes deal 4");
        Clear();
        var prism = ProjectileBase.SpawnBuiltin<PrismPellet>(owner, Vector2.zero, Vector2.right, Stats(10), WeaponEffects.PrismExplosion);
        Call(prism, "Explode", Vector2.zero);
        Check(prism.Damage == 10 && Active<BarrageShapeProjectile>().Single().Damage == 8, "Prism direct 10 and blast 8");
        Clear();
        var mine = ProjectileBase.SpawnBuiltin<MineProjectile>(owner, Vector2.zero, Vector2.right, Stats(32));
        Call(mine, "Detonate");
        Check(Active<BarrageShapeProjectile>().Single().Damage == 32, "Mine explosion inherits 32");
        Clear();
        TwinSatellite.Cast(owner, Vector2.zero, Stats(12));
        foreach (var satellite in Active<TwinSatellite>()) { SetBase(satellite, "<SpawnedAt>k__BackingField", Time.time - 1); Call(satellite, "Tick", 0f); }
        Check(Active<TwinSatellite>().All(p => p.Damage == 12) && Active<CelestialBullet>().Where(p => !(p is TwinSatellite)).All(p => p.Damage == 5)
            && Active<CelestialBullet>().Length > 2, "Satellites 12 and emitted fan bullets 5");
        Clear();
        BlossomBud.Cast(owner, Vector2.zero, Vector2.right, new Vector2(3,0), Stats(0));
        foreach (var bud in Active<BlossomBud>()) Call(bud, "Bloom");
        Check(Active<BlossomBud>().All(p => p.Damage == 0) && Active<CelestialBullet>().Count(p => p.Damage == 6) == 24, "Harmless buds emit 24 petals with 6 damage");
        Clear();
        sun = ProjectileBase.SpawnBuiltin<SolarSystemProjectile>(owner, Vector2.zero, Vector2.right, Stats(24), WeaponEffects.SolarRelease | WeaponEffects.SolarComets);
        Check(Active<SolarPlanet>().All(p => p.Damage == 12) && Active<SolarPlanet>().Length == 8, "Eight orbiting planets deal 12");
        SetBase(sun, "<SpawnedAt>k__BackingField", Time.time - 2); Set(sun, "nextCometAt", Time.time - 1); Call(sun, "Tick", 0f);
        Check(Active<SolarComet>().Single().Damage == 8, "Solar comet damage 8");
        Call(sun, "Finish");
        Check(Active<SolarPlanet>().All(p => p.Damage == 16), "Released planets deal 16");
        Clear();
        var constellation = ProjectileBase.SpawnBuiltin<ConstellationIgnition>(owner, owner.MuzzlePosition, Vector2.right, Stats(24));
        Check(constellation.Damage == 24 && Active<BarrageShapeProjectile>().Any(p => !p.IsTelegraph && p.Damage == 14), "Constellation outline 24 and tracing segments 14");

        Clear();
        var temporary = Instantiate(Weapon("Laser Shot"));
        var sequence = BarrageSequence.Fire(owner, owner.MuzzlePosition, Vector2.right, Vector2.zero, temporary.ResolveStats(null), temporary.steps);
        temporary.damage = 99;
        owner.EquipWeapon(Weapon("Basic Shot"));
        Set(sequence, "startedAt", Time.time - 1.1f); Call(sequence, "EmitDueSteps");
        Check(Active<BarrageShapeProjectile>().Any(p => !p.IsTelegraph && p.Damage == 22), "Delayed beam retains 22 after asset/loadout changes");
        Destroy(temporary);
        Clear(); Vulnerable(target);
        var reflected = ProjectileBase.SpawnBuiltin<BulletProjectile>(owner, Vector2.zero, Vector2.right, Stats(30));
        Check(reflected.Reflect(target) && reflected.Damage == 30, "Reflection preserves source damage");
        Vulnerable(owner); Hit(reflected, owner);
        Check(owner.Health == 70 && target.Health == 100, "Reflected damage hits original caster");
        Clear();
        var reused = ProjectileBase.SpawnBuiltin<BulletProjectile>(owner, Vector2.zero, Vector2.right, Stats(6));
        Check(reused.Damage == 6, "Pooled projectile receives fresh damage on reuse");

        Clear(); Vulnerable(target); target.ReceiveHit(owner, 22);
        var state = target.CaptureNetworkState();
        var writer = WriterPool.Retrieve(); writer.Write(state);
        var reader = ReaderPool.Retrieve(writer.GetArraySegment(), null);
        var decoded = reader.Read<ClassicPlayerState>(); reader.Store();
        Check(decoded.Health == 78, "FishNet player-state serializer round-trips HP");
        int stateBytes = writer.Length; writer.Store();
        Check(stateBytes == 21, "Player-state payload retains legacy 21 bytes with an empty hand");
        foreach (byte hp in new byte[] {0, 1, 63, 64, 78, 100})
        {
            state.Health = hp;
            writer = WriterPool.Retrieve(); writer.Write(state);
            Check(writer.Length == stateBytes, "HP value does not increase packet size: " + hp);
            writer.Store();
        }
        var replica = Player("Guest", new Vector2(7,3)); replica.SetNetworkReplica(); replica.ApplyNetworkState(decoded);
        Check(replica.Health == 78, "Guest applies authoritative HP");
        replica.ReceiveHit(owner, 32); Check(replica.Health == 78, "Guest cannot apply local damage");
        decoded.Health = 255; replica.ApplyNetworkState(decoded); Check(replica.Health == 100, "Guest clamps malformed high HP");
        decoded.Health = 0; replica.ApplyNetworkState(decoded); Check(replica.Health == 0, "Guest applies zero HP");
        Destroy(replica.gameObject);

        Clear(); Vulnerable(target); target.ReceiveHit(owner, 1);
        for (int i = 2; i < 100; i++) target.ReceiveHit(owner, i);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 100; i < 10100; i++) target.ReceiveHit(owner, i);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "Warmed overlapping-damage resolution allocates zero bytes across 10,000 contacts");
        Clear();

        // Exercise real match, HUD, defeat and training scenes in the same run.
        SceneManager.LoadScene("Arena"); yield return new WaitForSeconds(.5f);
        var match = FindFirstObjectByType<MatchController>();
        Check(match != null, "Real Arena scene loads");
        match.StopOnlineCombat();
        match.ApplyOnlineClock(true, false, 100, -1);
        var one = match.PlayerOne; var two = match.PlayerTwo;
        one.ResetCombatant(new Vector2(-4,0)); two.ResetCombatant(new Vector2(4,0));
        Vulnerable(one); one.ReceiveHit(two, 6); Vulnerable(one); one.ReceiveHit(two, 6);
        Vulnerable(two); two.ReceiveHit(one, 30);
        Check(one.Health == 88 && two.Health == 70 && match.IsPlaying, "Match survives three total contacts with health remaining");
        Call(match, "FinishByTime");
        Check(match.Winner == one, "Time limit uses remaining HP rather than fewer hits");
        match.ApplyOnlineClock(true, false, 100, -1);
        Vulnerable(two); two.ReceiveHit(one, 100);
        Check(two.Health == 0 && !match.IsPlaying && match.Winner == one, "Zero HP ends real match and selects winner");
        var labels = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
        Check(labels.Any(t => t.text.Contains("HP 88/100")) && labels.Any(t => t.text.Contains("HP 0/100")), "HUD displays actual HP for both players");
        Capture("health-arena.png");
        yield return new WaitForSeconds(.15f);
        MatchController.RequestTraining(); SceneManager.LoadScene("Arena"); yield return new WaitForSeconds(.5f);
        match = FindFirstObjectByType<MatchController>();
        Check(match.IsTraining && match.IsPlaying, "Training mode remains active");
        for (int i = 0; i < 20; i++) { Vulnerable(match.PlayerTwo); match.PlayerTwo.ReceiveHit(match.PlayerOne, 10); }
        yield return null;
        Check(match.IsPlaying && match.PlayerTwo.Health == 0 && match.PlayerTwo.DamageTaken == 200, "Training permits unlimited hits and accumulates damage past zero HP");
        Check(FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Any(t => t.text.Contains("누적 피해 200")), "Training displays accumulated damage");
        Capture("health-training.png");
        yield return new WaitForSeconds(.15f);
        FindFirstObjectByType<TrainingGround>().ResetArena();
        Check(match.PlayerOne.Health == 100 && match.PlayerTwo.Health == 100 && match.PlayerTwo.DamageTaken == 0, "Training reset restores both combatants");
    }

    private static WeaponDefinition Weapon(string name) => catalog.cards.First(c => c.weapon.displayName == name).weapon;
    private static WeaponStats Stats(int damage) => new(10, 11, 5, .13f, 12, Color.cyan, damage);
    private static PlayerCombatant Player(string name, Vector2 position)
    {
        var player = new GameObject(name).AddComponent<PlayerCombatant>();
        player.ConfigurePreview(Weapon("Basic Shot")); player.ResetCombatant(position); return player;
    }
    private static T[] Active<T>() where T : ProjectileBase => ProjectileBase.ActiveProjectiles.OfType<T>().Where(p => p.IsSpawned).ToArray();
    private static void Clear() { ProjectileBase.DespawnAll(); owner.ResetCombatant(new Vector2(-4,0)); target.ResetCombatant(new Vector2(4,0)); }
    private static void Vulnerable(PlayerCombatant p) => Set(p, "invulnerableUntil", Time.time - 1);
    private static object Field(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o,value);
    private static void SetBase(object o, string name, object value) => typeof(ProjectileBase).GetField(name,Flags).SetValue(o,value);
    private static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name,Flags).Invoke(o,args);
    private static void Hit(ProjectileBase p, PlayerCombatant victim) => typeof(ProjectileBase).GetMethod("TryHit",Flags).Invoke(p,new object[]{victim.GetComponent<CircleCollider2D>(),0f,1f,0f,true});
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Results.Add("PASS: " + message); }
    private static void Capture(string name)
    {
        var camera = Camera.main;
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var output = new RenderTexture(1280, 720, 24);
        var oldTarget = camera.targetTexture;
        var previous = RenderTexture.active;
        camera.targetTexture = output;
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        camera.GetComponent<ArenaCameraLayout>()?.Apply();
        Canvas.ForceUpdateCanvases();
        var training = FindFirstObjectByType<TrainingGround>();
        if (training != null) Call(training, "LateUpdate");
        Canvas.ForceUpdateCanvases();
        foreach (var label in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t => t.text.Contains("HP ") || t.text.Contains("누적 피해")))
        {
            label.ForceMeshUpdate();
            Check(!label.isTextOverflowing, "HP/damage label fits: " + label.text.Replace('\n', ' '));
        }
        camera.Render(); RenderTexture.active = output;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
        File.WriteAllBytes("ValidationOutput/" + name,pixels.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = oldTarget;
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        Destroy(pixels); output.Release(); Destroy(output);
    }
}
