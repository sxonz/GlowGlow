using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public sealed class BarrageSmokeRunner : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isBatchMode) new GameObject("Barrage smoke tests").AddComponent<BarrageSmokeRunner>().StartCoroutine(Run());
    }

    private static IEnumerator Run()
    {
        var test = Verify();
        while (true)
        {
            object current;
            try { if (!test.MoveNext()) break; current = test.Current; }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText("smoke-result.txt", e.ToString()); EditorApplication.Exit(1); yield break; }
            yield return current;
        }
        File.WriteAllText("smoke-result.txt", "PASS: assets/icons, cooldown/pause, basic travel, spread timing/scales, bomb cursor/4 shards, laser warning/hit/expiry, pool/reset/end/offscreen.");
        Debug.Log("BARRAGE_SMOKE_PASSED");
        EditorApplication.Exit(0);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static T[] Active<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
    private static void Playing(MatchController match, bool value) => typeof(MatchController).GetProperty("IsPlaying").SetValue(match, value);

    private static IEnumerator Verify()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        Check(catalog.cards.Length == 8 && catalog.StarterCardIds().Count == 8, "8-card deck broken");
        for (int i = 0; i < 4; i++) Check(catalog.cards[i].weapon.icon != null, "Missing icon");
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 5.4f;
        camera.transform.position = new Vector3(0, 0, -10);
        var match = new GameObject("Match").AddComponent<MatchController>(); match.enabled = false;
        var owner = new GameObject("Owner").AddComponent<PlayerCombatant>(); owner.enabled = false;
        var target = new GameObject("Target").AddComponent<PlayerCombatant>(); target.enabled = false;
        owner.Configure(1, match, catalog.cards[0].weapon, LocalPlayerInput.ControlScheme.MouseAndKeyboard, camera, target.transform);
        target.Configure(2, match, catalog.cards[0].weapon, LocalPlayerInput.ControlScheme.KeyboardTwo, camera, owner.transform);
        owner.transform.position = new Vector2(-6, 3); target.transform.position = new Vector2(6, 3);
        match.Configure(owner, target); Playing(match, true);
        var basic = new WeaponRuntime(catalog.cards[0].weapon);
        basic.Fire(owner, new Vector2(-4, 0), Vector2.right, Vector2.zero);
        Check(Active<Bullet>().Length == 1, "Basic must spawn immediately");
        var bullet = Active<Bullet>()[0];
        Check(bullet.Stats.Speed == 11 && float.IsPositiveInfinity(bullet.Stats.Lifetime), "Basic speed/offscreen lifetime");
        Check(basic.Fire(owner, Vector2.zero, Vector2.right) == null, "Cooldown not enforced");
        yield return new WaitForSeconds(.05f);
        Check(bullet.transform.position.x > -4, "Bullet did not travel");
        Time.timeScale = 0; float x = bullet.transform.position.x; float cooldown = basic.CooldownRemaining;
        yield return new WaitForSecondsRealtime(.08f);
        Check(bullet.transform.position.x == x && basic.CooldownRemaining == cooldown, "Pause changed bullets/cooldown"); Time.timeScale = 1;
        ProjectileBase.DespawnAll();
        var spread = new WeaponRuntime(catalog.cards[2].weapon);
        spread.Fire(owner, new Vector2(-4, 0), Vector2.right, Vector2.zero);
        Check(Active<Bullet>().Length == 1, "Spread first shot");
        owner.EquipWeapon(catalog.cards[0].weapon);
        yield return new WaitForSeconds(.12f);
        Check(Active<Bullet>().Length == 3, "Spread 0.1 pair");
        yield return new WaitForSeconds(.12f);
        Check(Active<Bullet>().Length == 5, "Spread 0.2 pair / weapon switching");
        Check(Active<Bullet>().Count(b => Mathf.Approximately(b.Stats.Radius, .065f)) == 2, "Spread small radii");
        ProjectileBase.DespawnAll();
        var bomb = new WeaponRuntime(catalog.cards[3].weapon);
        bomb.Fire(owner, new Vector2(-4, 0), Vector2.right, new Vector2(2, -1));
        var warning = Active<BarrageShapeProjectile>().Single();
        Check((Vector2)warning.transform.position == new Vector2(2, -1) && !warning.GetComponent<CircleCollider2D>().enabled, "Bomb warning cursor/damage");
        yield return new WaitForSeconds(1.04f);
        Check(Active<Bullet>().Length == 4, "Bomb needs four fragments");
        Check(Active<Bullet>().All(b => b.Stats.Speed >= 9.6f && b.Stats.Speed <= 14.4f), "Bomb speed range");
        var explosion = Active<BarrageShapeProjectile>().Single();
        Check((Vector2)explosion.transform.position == new Vector2(2, -1) && explosion.GetComponent<BoxCollider2D>().enabled, "Bomb explosion at cursor");
        yield return new WaitForSeconds(.85f);
        Check(Active<BarrageShapeProjectile>().Length == 0, "Bomb box did not expire");
        ProjectileBase.DespawnAll();
        target.transform.position = Vector2.zero;
        var laser = new WeaponRuntime(catalog.cards[1].weapon);
        laser.Fire(owner, new Vector2(-4, 0), Vector2.right, Vector2.zero);
        Check(!Active<BarrageShapeProjectile>().Single().GetComponent<BoxCollider2D>().enabled, "Laser warning damages");
        yield return new WaitForSeconds(.4f);
        Check(target.HitsTaken == 0, "Telegraph hit target");
        yield return new WaitForSeconds(.7f);
        Check(target.HitsTaken == 1, "Beam did not hit target");
        Check(Mathf.Approximately(Active<BarrageShapeProjectile>().Single().transform.localScale.y, .3f), "Beam thickness");
        yield return new WaitForSeconds(.5f);
        Check(Active<BarrageShapeProjectile>().Length == 0 && target.HitsTaken == 1, "Beam lifetime/repeated hit");
        new WeaponRuntime(catalog.cards[2].weapon).Fire(owner, Vector2.zero, Vector2.right);
        ProjectileBase.DespawnAll();
        yield return new WaitForSeconds(.3f);
        Check(Active<ProjectileBase>().Length == 0, "Reset emitted delayed shots");
        new WeaponRuntime(catalog.cards[3].weapon).Fire(owner, Vector2.zero, Vector2.right);
        Playing(match, false);
        yield return null; yield return null;
        Check(Active<ProjectileBase>().Length == 0, "Match end leaves attacks");
        Playing(match, true); target.transform.position = new Vector2(6, 3);
        new WeaponRuntime(catalog.cards[0].weapon).Fire(owner, Vector2.zero, Vector2.right);
        yield return new WaitForSeconds(2);
        Check(Active<Bullet>().Length == 0, "Offscreen bullet remains active");
    }
}
