// Copy into Assets only in the isolated validation project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GlowGlow.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class CelestialBarrageSmokeRunner : MonoBehaviour
{
    private static readonly string[] Names =
        { "Firefly Convergence", "Twin Satellites", "Blossom Burst", "Constellation Ignition" };
    private static readonly List<string> Results = new();
    private static PlayerCombatant owner, target;
    private static Camera cameraView;
    private static WeaponDefinition[] weapons;
    private static int checks;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Begin()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/CelestialValidation.unity");
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CelestialValidation")
            new GameObject("Celestial smoke").AddComponent<CelestialBarrageSmokeRunner>().StartCoroutine(Run());
    }

    private static IEnumerator Run()
    {
        Directory.CreateDirectory("ValidationOutput");
        var iterator = Verify();
        while (true)
        {
            object next;
            try { if (!iterator.MoveNext()) break; next = iterator.Current; }
            catch (Exception ex)
            {
                Time.timeScale = 1;
                File.WriteAllText("ValidationOutput/celestial-result.txt", string.Join("\n", Results) + "\nFAIL: " + ex);
                Debug.LogException(ex);
                EditorApplication.Exit(1);
                yield break;
            }
            yield return next;
        }
        Results.Add("PASS: " + checks + " checks.");
        File.WriteAllText("ValidationOutput/celestial-result.txt", string.Join("\n", Results));
        Debug.Log("CELESTIAL_SMOKE_PASSED: " + checks);
        EditorApplication.Exit(0);
    }

    private static IEnumerator Verify()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
        weapons = Names.Select(n => AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/" + n + ".asset")).ToArray();
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        var upgrades = Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades");
        foreach (var weapon in weapons)
        {
            Check(weapon != null && weapon.icon != null, "Prepared card and icon: " + weapon?.displayName);
            Check(catalog.cards.Count(c => c.weapon == weapon) == 1, "Catalog registration: " + weapon.name);
            Check(!upgrades.Any(u => u.CanApplyTo(weapon)), "No upgrades: " + weapon.name);
        }
        Check(catalog.StarterCardIds().Count == 8, "Starter deck remains valid");
        cameraView = new GameObject("Validation Camera", typeof(Camera)).GetComponent<Camera>();
        cameraView.tag = "MainCamera";
        cameraView.orthographic = true;
        cameraView.orthographicSize = 5.2f;
        cameraView.aspect = 16f / 9;
        cameraView.transform.position = new Vector3(0, 0, -10);
        cameraView.clearFlags = CameraClearFlags.SolidColor;
        cameraView.backgroundColor = new Color(.025f, .012f, .06f);
        owner = Player("Caster", new Vector2(-4, 0));
        target = Player("Opponent", new Vector2(3, 0));
        target.GetComponent<SpriteRenderer>().color = Color.cyan;
        yield return new WaitForSeconds(.65f);

        Fire(0);
        var lights = Active<FireflyLight>();
        Check(lights.Length == 9, "Nine fireflies");
        Rect view = CelestialBarrageGeometry.ViewBounds(owner);
        Check(lights.All(l => !view.Contains(l.transform.position)), "All lights originate outside the actual camera");
        float before = lights.Sum(l => Vector2.Distance(l.transform.position, owner.transform.position));
        yield return new WaitForSeconds(1.25f);
        lights = Active<FireflyLight>();
        Check(lights.Length > 0 && lights.Any(l => view.Contains(l.transform.position)), "Offscreen lights survive and enter view");
        Check(lights.Sum(l => Vector2.Distance(l.transform.position, owner.transform.position)) < before, "Lights converge on their caster");
        Capture("firefly-convergence.png");
        var pausePositions = lights.Select(l => l.transform.position).ToArray();
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(.15f);
        Check(lights.Select((l, i) => l.transform.position == pausePositions[i]).All(v => v), "Pause freezes lights");
        Time.timeScale = 1;
        var tick = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), lights[0], typeof(FireflyLight).GetMethod("Tick", Private));
        tick(.001f);
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) tick(.001f);
        allocation = GC.GetAllocatedBytesForCurrentThread() - allocation;
        Check(allocation == 0, "Warmed firefly movement allocates zero bytes: " + allocation);
        var originalIds = new HashSet<int>(lights.Select(l => l.GetInstanceID()));
        Clear();
        Fire(0);
        Check(Active<FireflyLight>().Any(l => originalIds.Contains(l.GetInstanceID())), "Firefly objects reuse the pool");
        Clear();

        Fire(1);
        var twins = Active<TwinSatellite>();
        Check(twins.Length == 2, "Two satellites");
        Vector2 orbitCenter = (twins[0].transform.position + twins[1].transform.position) * .5f;
        float initialRadius = Vector2.Distance(twins[0].transform.position, orbitCenter);
        yield return new WaitForSeconds(.85f);
        Check(Active<CelestialBullet>().Count(p => p is not TwinSatellite) == 6, "First paired volley emits six bullets");
        Check(Mathf.Abs(Vector2.Distance(twins[0].transform.position, orbitCenter) - initialRadius) < .001f, "Circular orbit keeps its radius");
        Check(Vector2.Distance((twins[0].transform.position + twins[1].transform.position) * .5f, orbitCenter) < .04f, "Satellites remain opposite");
        Capture("twin-satellites.png");
        Clear();

        Fire(2);
        Check(Active<BlossomBud>().Length == 3, "Three flower buds");
        yield return new WaitForSeconds(1.1f);
        Check(Active<CelestialBullet>().Length == 0, "Buds do not release petals during warning");
        var buds = Active<BlossomBud>();
        var stopped = buds.Select(b => b.transform.position).ToArray();
        Capture("blossom-warning.png");
        yield return new WaitForSeconds(.2f);
        Check(buds.Select((b, i) => b.transform.position == stopped[i]).All(v => v), "Buds stop before blooming");
        yield return new WaitForSeconds(.31f);
        Check(Active<CelestialBullet>().Length == 24, "Three blooms release 24 petals");
        Capture("blossom-burst.png");
        Clear();

        VerifyMuzzleAnchoring();
        var patterns = new HashSet<int>();
        for (int attempt = 0; attempt < 40 && patterns.Count < 3; attempt++)
        {
            Vector2 muzzle = owner.MuzzlePosition;
            Fire(3);
            float castAt = Time.time;
            var constellation = Active<ConstellationIgnition>().Single();
            patterns.Add(constellation.PatternIndex);
            Check(Vector2.Distance(constellation.StarPosition(0), muzzle) < .00001f,
                "First star starts at the actual firing muzzle");
            Rect arena = CelestialBarrageGeometry.ArenaBounds(owner);
            Vector2 min = muzzle, max = muzzle;
            for (int i = 0; i < constellation.StarCount; i++)
            {
                min = Vector2.Min(min, constellation.StarPosition(i));
                max = Vector2.Max(max, constellation.StarPosition(i));
            }
            Check(max.x - min.x <= arena.width - 1.199f && max.y - min.y <= arena.height - 1.199f &&
                Mathf.Max(max.x - min.x, max.y - min.y) <= weapons[3].range + .001f,
                "Route retains bounded dimensions without relocating the firing muzzle");
            Check(constellation.IgnitionTime(0) == 0 && constellation.IgnitedSegments == 1 &&
                constellation.transform.Find("Constellation Comet").GetComponent<SpriteRenderer>().enabled,
                "First star and comet ignite on the spawn frame with no opening delay");
            Check(Active<BarrageShapeProjectile>().Count(s => s.IsTelegraph) == 1 &&
                Active<BarrageShapeProjectile>().Count(s => !s.IsTelegraph) == 1,
                "Immediate first beam warns only the following segment");
            Check(constellation.TraceTime(0) > constellation.TraceTime(constellation.StarCount - 2), "Trace accelerates toward the final star");
            for (int i = 0; i < constellation.StarCount - 1; i++)
                Check(Mathf.Abs(constellation.TraceTime(i) * 2 - Mathf.Lerp(.22f, .1f,
                    (float)i / (constellation.StarCount - 2))) < .00001f, "Every segment travels at exactly twice its former speed");
            var figure = constellation.transform.Find("Constellation Revelation").GetComponent<SpriteRenderer>();
            Check(figure.sprite.rect == new Rect(0, 0, 768, 448) &&
                Vector2.Distance(figure.sprite.bounds.size, new Vector2(12, 7)) < .001f &&
                figure.sprite.bounds.center.sqrMagnitude < .000001f,
                "Prepared figure uses the full centered canvas without an obsolete sprite slice");
            Check(!figure.enabled && figure.sprite == RuntimeShapes.ConstellationFigure(constellation.PatternIndex),
                "Correct prepared figure stays hidden before completion, including on pool reuse");
            Check(!constellation.FigureDealsDamage &&
                float.IsPositiveInfinity(constellation.DangerClearance(constellation.StarPosition(0), .3f)),
                "Hidden outline has neither damage nor bot danger on spawn or reuse");
            for (int i = 0; i < constellation.StarCount; i++)
                Check(Vector2.Distance(figure.transform.TransformPoint(ConstellationIgnition.AuthoredStarPosition(
                    constellation.PatternIndex, i)), constellation.StarPosition(i)) < .001f,
                    "Prepared figure aligns with the rotated and fitted star map");
            Check(constellation.GetComponentsInChildren<SpriteRenderer>().Count(s => s.name == "Constellation Star" && s.enabled) == 3,
                "Unrevealed stars do not disclose the future route");
            Vector2 firstCenter = (constellation.StarPosition(0) + constellation.StarPosition(1)) * .5f;
            target.transform.position = firstCenter;
            owner.transform.position = firstCenter;
            Vulnerable(target); Vulnerable(owner);
            int hits = target.HitsTaken;
            Check(target.HitsTaken == hits, "Zero-length beam does not hit before its front moves");
            Time.timeScale = 0;
            Capture("constellation-" + ConstellationIgnition.PatternNames[constellation.PatternIndex].ToLowerInvariant() + "-warning.png");
            // Do not let screenshot encoding consume the entire fast first segment.
            yield return null;
            Time.timeScale = 1;
            yield return new WaitForSeconds(Mathf.Max(0, castAt + ConstellationIgnition.WarningTime + .055f - Time.time));
            var beams = Active<BarrageShapeProjectile>().Where(b => !b.IsTelegraph && !b.IsDisappearing).ToArray();
            Check(beams.Length == 1 && constellation.IgnitedSegments == 1, "Only the first segment ignites initially");
            var first = beams[0];
            float fullLength = Vector2.Distance(constellation.StarPosition(0), constellation.StarPosition(1));
            Check(first.transform.localScale.x > 0 && first.transform.localScale.x < fullLength, "Beam visibly draws from star to star");
            foreach (var beam in beams)
            {
                Vector2 rear = beam.transform.position - beam.transform.right * (beam.transform.localScale.x * .5f);
                Check(Vector2.Distance(rear, constellation.StarPosition(0)) < .001f &&
                    Mathf.Abs(beam.transform.localScale.y - ConstellationIgnition.BeamWidth) < .001f,
                    "Growing damage remains anchored to the warned segment and thickness");
            }
            Check(target.HitsTaken > hits && owner.HitsTaken == 0, "Beam damages opponent and excludes owner");
            var nextWarning = Active<BarrageShapeProjectile>().Single(s => s.IsTelegraph);
            Vector2 warningRear = nextWarning.transform.position - nextWarning.transform.right * nextWarning.transform.localScale.x * .5f;
            Check(Vector2.Distance(warningRear, constellation.StarPosition(1)) < .001f,
                "Warning advances to the immediately following segment");
            var pausePosition = first.transform.position;
            var pauseScale = first.transform.localScale;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.12f);
            Check(first.transform.position == pausePosition && first.transform.localScale == pauseScale, "Pause freezes the drawing beam");
            Time.timeScale = 1;
            int last = constellation.StarCount - 1;
            target.transform.position = (constellation.StarPosition(last - 1) + constellation.StarPosition(last)) * .5f;
            Vulnerable(target);
            int futureHits = target.HitsTaken;
            yield return new WaitForSeconds(.3f);
            Check(target.HitsTaken == futureHits, "Future segments remain harmless while the head advances");
            Check(first.IsSpawned && first.IsDisappearing && !first.GetComponent<BoxCollider2D>().enabled &&
                float.IsPositiveInfinity(first.DangerClearance(firstCenter, .3f)), "Afterglow has no collider or bot danger");
            Check(constellation.IgnitedSegments > 1 && constellation.IgnitedSegments < last, "Ignition advances sequentially");
            var animate = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), constellation, typeof(ConstellationIgnition).GetMethod("Tick", Private));
            animate(0);
            long animationBytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) animate(0);
            animationBytes = GC.GetAllocatedBytesForCurrentThread() - animationBytes;
            Check(animationBytes == 0, "Warmed constellation animation allocates zero bytes: " + animationBytes);
            Capture("constellation-" + ConstellationIgnition.PatternNames[constellation.PatternIndex].ToLowerInvariant() + "-ignition.png");
            bool singleWarning = true;
            while (Time.time < castAt + constellation.CompletionTime + .24f)
            {
                singleWarning &= Active<BarrageShapeProjectile>().Count(s => s.IsTelegraph) <= 1;
                yield return null;
            }
            Check(singleWarning, "At most one upcoming warning throughout the chain");
            Check(figure.enabled && figure.color.a > .2f && figure.color.a <= .62f, "Completed figure emerges with a faint glow");
            Check(constellation.FigureDealsDamage &&
                !Active<BarrageShapeProjectile>().Any(s => !s.IsTelegraph && !s.IsDisappearing),
                "Completed outline damages independently after the inner beams have expired");
            Check(constellation.transform.Find("Constellation Trail").GetComponent<LineRenderer>().positionCount == constellation.StarCount,
                "Actual traced path remains as the completed interior afterglow");
            var checkFigureHits = (Action)Delegate.CreateDelegate(typeof(Action), constellation,
                typeof(ConstellationIgnition).GetMethod("CheckFigureHits", Private));
            VerifyOutlineCollision(constellation, figure, checkFigureHits);
            animate(0);
            long figureBytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) animate(0);
            Check(GC.GetAllocatedBytesForCurrentThread() - figureBytes == 0, "Completed figure animation allocates zero bytes");
            checkFigureHits();
            long hitBytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) checkFigureHits();
            Check(GC.GetAllocatedBytesForCurrentThread() - hitBytes == 0, "Warmed outline collision allocates zero bytes");
            CheckFigureReplication(figure);
            Capture("constellation-" + ConstellationIgnition.PatternNames[constellation.PatternIndex].ToLowerInvariant() + "-complete.png");
            float figureAlpha = figure.color.a;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.1f);
            Check(figure.color.a == figureAlpha, "Pause freezes the cosmic reveal");
            Time.timeScale = 1;
            yield return new WaitForSeconds(Mathf.Max(0, castAt + constellation.CompletionTime + .97f - Time.time));
            Check(figure.enabled && figure.color.a < figureAlpha, "Cosmic glow fades out after completion");
            Check(!constellation.FigureDealsDamage &&
                float.IsPositiveInfinity(constellation.DangerClearance(target.transform.position, .3f)),
                "Fading outline has no damage or bot danger");
            int fadeHits = target.HitsTaken;
            Vulnerable(target);
            checkFigureHits();
            Check(target.HitsTaken == fadeHits, "Faded outline cannot hit again");
            yield return new WaitForSeconds(Mathf.Max(0, castAt + constellation.EndTime + .12f - Time.time));
            Check(!ProjectileBase.ActiveProjectiles.Any(), "Constellation, segments and nodes expire");
            Clear();
        }
        Check(patterns.Count == 3, "All three authored constellations are selectable");

        // The damaging front must not extend into the undrawn part of a long segment.
        var traceStep = new BarrageStep { shape = BarrageShape.Box, position = BarragePosition.Gun,
            duration = .8f, dimensions = new Vector2(1, ConstellationIgnition.BeamWidth), dealsDamage = true };
        var traceStats = new WeaponStats(1, 0, .8f, .5f, 6, Color.cyan);
        target.transform.position = new Vector2(2.4f, 0);
        Vulnerable(target);
        var trace = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(owner, new Vector2(-3, 0), Vector2.right, traceStats);
        trace.ConfigureTrace(traceStep, .6f);
        yield return new WaitForSeconds(.15f);
        Check(target.HitsTaken == 0, "Undrawn beam endpoint does not deal damage");
        yield return new WaitForSeconds(.48f);
        Check(target.HitsTaken == 1, "Drawing front deals damage when it reaches the target");
        yield return new WaitForSeconds(.25f);
        Check(trace.IsDisappearing && !trace.GetComponent<BoxCollider2D>().enabled, "Trace expires into harmless afterglow");
        Vulnerable(target);
        yield return new WaitForSeconds(.08f);
        Check(target.HitsTaken == 1, "Afterglow cannot hit again");
        Clear();
        var normal = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(owner, new Vector2(-3, 0), Vector2.right, traceStats);
        normal.Configure(traceStep);
        Check(Mathf.Abs(normal.transform.localScale.x - 6) < .001f, "Ordinary beams still appear at full length after pool reuse");
        Clear();
        Fire(3);
        var skipped = Active<ConstellationIgnition>().Single();
        typeof(ProjectileBase).GetField("<SpawnedAt>k__BackingField", Private).SetValue(skipped, Time.time - (skipped.EndTime - .2f));
        var catchUp = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), skipped, typeof(ConstellationIgnition).GetMethod("Tick", Private));
        catchUp(0);
        Check(!Active<BarrageShapeProjectile>().Any(b => !b.IsTelegraph && !b.IsDisappearing), "Hitch does not replay expired damaging segments");
        Check(!skipped.FigureDealsDamage, "Hitch does not replay expired outline damage");
        Clear();

        // Actual swept contact: cross the opponent in one update, without relying on trigger overlap.
        Vulnerable(target);
        var shot = ProjectileBase.SpawnBuiltin<CelestialBullet>(owner, new Vector2(1, 0), Vector2.right,
            new WeaponStats(1, 20, 1, .1f, 12, Color.white));
        var move = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), shot, typeof(CelestialBullet).GetMethod("Tick", Private));
        move(.2f);
        Check(target.HitsTaken == 1 && !shot.IsSpawned, "Swept bullet catches a crossed target");
        Clear();

        var match = new GameObject("Capture fixture", typeof(MatchController)).GetComponent<MatchController>();
        match.enabled = false;
        match.Configure(owner, target);
        var host = new GameObject("Capture host").AddComponent<ClassicPresentation>();
        host.enabled = false;
        host.Initialize(match, true);
        var guest = new GameObject("Capture guest").AddComponent<ClassicPresentation>();
        guest.enabled = false;
        guest.Initialize(null, false);
        for (int i = 0; i < 4; i++) { Fire(i); Fire(i, target); }
        int peak = 0, maxBytes = 0, frames = 0;
        float until = Time.time + 3.8f, frameTotal = 0, maxFrame = 0;
        uint sequence = 1;
        while (Time.time < until)
        {
            peak = Mathf.Max(peak, ProjectileBase.ActiveProjectiles.Count());
            var messages = host.Capture(1, sequence++);
            foreach (var message in messages) guest.Apply(message);
            foreach (var removal in host.Removals) guest.ApplyRemoval(removal);
            maxBytes = Mathf.Max(maxBytes, host.LastCaptureBytes);
            frameTotal += Time.unscaledDeltaTime * 1000;
            maxFrame = Mathf.Max(maxFrame, Time.unscaledDeltaTime * 1000);
            frames++;
            yield return null;
        }
        Check(peak <= 180, "Simultaneous eight-cast projectile count stays bounded: " + peak);
        Check(guest.ActiveReplicaCount > 0 && maxBytes < 4200, "Existing replication displays new sprites within capture budget: " + maxBytes);
        Results.Add($"Load observation: {peak} peak projectiles, {frameTotal / frames:F2}ms mean frame, {maxFrame:F2}ms max frame, {maxBytes} max capture bytes.");
        yield return new WaitForSeconds(6.2f);
        Check(!ProjectileBase.ActiveProjectiles.Any(), "All four patterns and emitted children have bounded lifetimes");
        Clear();
        foreach (var message in host.Capture(1, sequence++)) guest.Apply(message);
        foreach (var removal in host.Removals) guest.ApplyRemoval(removal);
        Check(guest.ActiveReplicaCount <= 6, "Reset removes projectile replicas");
        Destroy(host.gameObject); Destroy(guest.gameObject); Destroy(match.gameObject);

        // Exercise the actual isolated preview, including its camera and local physics scene.
        var previewObject = new GameObject("Deck preview fixture", typeof(RectTransform), typeof(RawImage), typeof(BarragePreview));
        var preview = previewObject.GetComponent<BarragePreview>();
        preview.Configure(previewObject.GetComponent<RawImage>());
        foreach (var weapon in weapons)
        {
            preview.Select(weapon);
            yield return new WaitForSeconds(1.4f);
            Check(ProjectileBase.ActiveProjectiles.Any(p => p.gameObject.scene != owner.gameObject.scene), "Isolated preview: " + weapon.name);
            if (weapon == weapons[3])
            {
                var constellation = Active<ConstellationIgnition>().Single();
                Check(Vector2.Distance(constellation.StarPosition(0), constellation.Owner.MuzzlePosition) < .002f,
                    "Deck preview also begins at the displayed muzzle");
            }
            preview.Restart();
        }
        previewObject.SetActive(false);
        yield return null;
        Check(!ProjectileBase.ActiveProjectiles.Any(), "Closing preview cleans all owned projectiles");
    }

    private static void VerifyMuzzleAnchoring()
    {
        var randomState = UnityEngine.Random.state;
        var aimField = typeof(PlayerCombatant).GetField("aim", Private);
        Rect arena = CelestialBarrageGeometry.ArenaBounds(owner);
        Vector2[] positions = { Vector2.zero, arena.min + Vector2.one * .4f, arena.max - Vector2.one * .4f };
        foreach (var position in positions)
        for (int directionIndex = 0; directionIndex < 8; directionIndex++)
        {
            var patterns = new HashSet<int>();
            Vector2 direction = CelestialBarrageGeometry.Unit(directionIndex * Mathf.PI / 4);
            for (int attempt = 0; attempt < 40 && patterns.Count < 3; attempt++)
            {
                Clear();
                owner.transform.position = position;
                aimField.SetValue(owner, direction);
                Vector2 muzzle = owner.MuzzlePosition;
                UnityEngine.Random.InitState(6173 + attempt);
                new WeaponRuntime(weapons[3]).Fire(owner, muzzle, direction, muzzle + direction * 2);
                var constellation = Active<ConstellationIgnition>().Single();
                if (!patterns.Add(constellation.PatternIndex)) continue;
                Check(Vector2.Distance(constellation.StarPosition(0), muzzle) < .00001f &&
                    Vector2.Distance(constellation.TracePosition(0), muzzle) < .00001f &&
                    Vector2.Distance(constellation.transform.Find("Constellation Comet").position, muzzle) < .00001f,
                    "Muzzle stays exact for every route, aim direction, center and arena corner");
                var figure = constellation.transform.Find("Constellation Revelation");
                bool aligned = true;
                var stars = new Vector2[constellation.StarCount];
                for (int i = 0; i < stars.Length; i++)
                {
                    stars[i] = constellation.StarPosition(i);
                    aligned &= Vector2.Distance(figure.TransformPoint(ConstellationIgnition.AuthoredStarPosition(
                        constellation.PatternIndex, i)), stars[i]) < .001f;
                }
                Check(aligned, "Whole figure and route retain alignment around the muzzle anchor");
                Vector2 localProbe = ConstellationIgnition.AuthoredStarPosition(constellation.PatternIndex, 1);
                float expected = ConstellationFigureMask.Load(constellation.PatternIndex).Distance(localProbe) * figure.lossyScale.x - .1f;
                Check(Mathf.Abs(constellation.FigureClearance(figure.TransformPoint(localProbe), .1f) - expected) < .001f,
                    "Outline collision shares the translated and rotated figure transform");
                owner.transform.position += new Vector3(.7f, -.5f);
                aimField.SetValue(owner, -direction);
                Check(Vector2.Distance(constellation.StarPosition(0), muzzle) < .00001f &&
                    Vector2.Distance(figure.TransformPoint(ConstellationIgnition.AuthoredStarPosition(
                        constellation.PatternIndex, 0)), muzzle) < .001f,
                    "Movement and re-aiming after firing do not drag the route");
                Clear();
                owner.transform.position = position;
                aimField.SetValue(owner, direction);
                UnityEngine.Random.InitState(6173 + attempt);
                new WeaponRuntime(weapons[3]).Fire(owner, muzzle, direction, muzzle + direction * 20);
                constellation = Active<ConstellationIgnition>().Single();
                Check(stars.Length == constellation.StarCount && stars.Select((p, i) =>
                    Vector2.Distance(p, constellation.StarPosition(i)) < .00001f).All(v => v),
                    "Cursor distance cannot move or resize the muzzle-anchored route on pool reuse");
            }
            Check(patterns.Count == 3, "All three routes checked for this muzzle and aim direction");
        }
        Clear();
        aimField.SetValue(owner, Vector2.right);
        UnityEngine.Random.state = randomState;
    }

    private static PlayerCombatant Player(string name, Vector2 position)
    {
        var player = new GameObject(name).AddComponent<PlayerCombatant>();
        player.ConfigurePreview(weapons[0]);
        player.ResetCombatant(position);
        return player;
    }

    private static void VerifyOutlineCollision(ConstellationIgnition constellation, SpriteRenderer figure, Action checkHits)
    {
        target.ResetCombatant(target.transform.position);
        var mask = ConstellationFigureMask.Load(constellation.PatternIndex);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes("Assets/Resources/GameplaySprites/Constellation" +
            ConstellationIgnition.PatternNames[constellation.PatternIndex] + ".png"));
        Color[] pixels = texture.GetPixels();
        Vector2 solid = Vector2.zero, hollow = Vector2.zero;
        bool foundSolid = false, foundHollow = false;
        // Read the actual prepared artwork independently of the distance field.
        for (int y = 20; y < texture.height - 20 && (!foundSolid || !foundHollow); y += 3)
        {
            int run = 0, previousEnd = -1;
            bool inBand = false;
            for (int x = 10; x < texture.width - 10; x++)
            {
                Color p = pixels[y * texture.width + x];
                bool band = p.a > .18f;
                Vector2 local = new((x + .5f) / 64 - 6, (y + .5f) / 64 - 3.5f);
                Vector2 world = figure.transform.TransformPoint(local);
                if (!foundSolid && p.a * Mathf.Max(p.r, Mathf.Max(p.g, p.b)) > .6f &&
                    CelestialBarrageGeometry.ArenaBounds(owner).Contains(world))
                {
                    solid = local;
                    foundSolid = true;
                    Check(mask.Distance(local) < .06f, "Bright approved artwork has matching collision");
                }
                if (band && !inBand)
                {
                    // Alternating border bands enclose the hollow channel.
                    if (!foundHollow && run % 2 == 1 && previousEnd >= 0 && x - previousEnd > 32)
                    {
                        Vector2 candidate = new((x + previousEnd) * .5f / 64 - 6, (y + .5f) / 64 - 3.5f);
                        if (mask.Distance(candidate) > .13f)
                        {
                            hollow = candidate;
                            foundHollow = true;
                        }
                    }
                    run++;
                }
                if (!band && inBand) previousEnd = x;
                inBand = band;
            }
        }
        Destroy(texture);
        Check(foundSolid && foundHollow, "Prepared outline contains luminous strands and a hollow interior");
        Vector2 solidWorld = figure.transform.TransformPoint(solid);
        Vector2 hollowWorld = figure.transform.TransformPoint(hollow);
        Check(constellation.DangerClearance(solidWorld, .08f) < 0 &&
            constellation.DangerClearance(hollowWorld, .08f) > 0, "Bot danger follows the outline and preserves its hole");

        var collider = target.GetComponent<CircleCollider2D>();
        float oldRadius = collider.radius;
        collider.radius = .07f;
        target.transform.position = hollowWorld;
        owner.transform.position = solidWorld;
        typeof(PlayerCombatant).GetField("invulnerableUntil", Private).SetValue(target, float.MaxValue);
        checkHits(); // Reset the movement sample after test teleportation.
        Vulnerable(target); Vulnerable(owner);
        int before = target.HitsTaken, ownerBefore = owner.HitsTaken;
        checkHits();
        Check(target.HitsTaken == before && owner.HitsTaken == ownerBefore, "Hollow interior stays safe and the outline excludes its owner");

        target.transform.position = solidWorld;
        typeof(PlayerCombatant).GetField("invulnerableUntil", Private).SetValue(target, float.MaxValue);
        checkHits();
        Check(target.HitsTaken == before, "Outline respects invulnerability");
        collider.enabled = false;
        Vulnerable(target);
        checkHits();
        Check(target.HitsTaken == before, "Outline respects a disabled player hitbox");
        collider.enabled = true;
        checkHits();
        Check(target.HitsTaken == before + 1 && constellation.IsSpawned, "Luminous outline deals real damage and persists after a hit");
        Vulnerable(target);
        checkHits();
        Check(target.HitsTaken == before + 1, "Multiple crossing strands hit a target only once per reveal");
        collider.radius = oldRadius;

        // Find two safe endpoints on opposite sides of a strand; the sweep must hit between them.
        bool crossed = false;
        for (int i = 0; i < 16 && !crossed; i++)
        {
            Vector2 delta = CelestialBarrageGeometry.Unit(i * Mathf.PI / 8) * .5f;
            if (mask.Distance(solid - delta) < .08f || mask.Distance(solid + delta) < .08f) continue;
            crossed = mask.IntersectsSweep(solid - delta, solid + delta, .07f);
        }
        Check(crossed, "Swept outline collision catches a crossing with both endpoints in empty space");
    }
    private static void CheckFigureReplication(SpriteRenderer figure)
    {
        var fixture = new GameObject("Figure capture fixture", typeof(MatchController)).GetComponent<MatchController>();
        fixture.enabled = false;
        fixture.Configure(owner, target);
        var sender = new GameObject("Figure capture sender").AddComponent<ClassicPresentation>();
        sender.enabled = false;
        sender.Initialize(fixture, true);
        var receiver = new GameObject("Figure capture receiver").AddComponent<ClassicPresentation>();
        receiver.enabled = false;
        receiver.Initialize(null, false);
        foreach (var message in sender.Capture(1, 1)) receiver.Apply(message);
        Check(receiver.ContainsReplica(figure.GetInstanceID()), "Completed figure reaches the remote presentation");
        Check(receiver.GetComponentsInChildren<SpriteRenderer>().Any(s => s.sprite == figure.sprite && s.color == figure.color),
            "Remote figure preserves the prepared artwork and faint opacity");
        Destroy(sender.gameObject); Destroy(receiver.gameObject); Destroy(fixture.gameObject);
    }
    private static void Fire(int index, PlayerCombatant caster = null)
    {
        caster = caster != null ? caster : owner;
        Vector2 aim = caster == owner ? (Vector2)target.transform.position : (Vector2)owner.transform.position;
        new WeaponRuntime(weapons[index]).Fire(caster, caster.MuzzlePosition, (aim - (Vector2)caster.transform.position).normalized, aim);
    }
    private static T[] Active<T>() where T : ProjectileBase => ProjectileBase.ActiveProjectiles.OfType<T>().Where(p => p.IsSpawned).ToArray();
    private static void Vulnerable(PlayerCombatant player) => typeof(PlayerCombatant).GetField("invulnerableUntil", Private).SetValue(player, 0f);
    private static void Clear()
    {
        ProjectileBase.DespawnAll();
        owner.ResetCombatant(new Vector2(-4, 0));
        target.ResetCombatant(new Vector2(3, 0));
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
        Results.Add("PASS: " + message);
    }
    private static void Capture(string name)
    {
        var texture = new RenderTexture(960, 540, 24);
        var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        cameraView.targetTexture = texture;
        cameraView.Render();
        RenderTexture.active = texture;
        pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        pixels.Apply();
        File.WriteAllBytes("ValidationOutput/" + name, pixels.EncodeToPNG());
        cameraView.targetTexture = null;
        RenderTexture.active = previous;
        Destroy(pixels); Destroy(texture);
    }
}
