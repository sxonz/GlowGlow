using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy into Assets only in an isolated validation project. Never writes player preferences.
public sealed class MeteorDiveSmokeRunner : MonoBehaviour
{
    private Camera view;
    public static void Begin()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/MeteorValidation.unity");
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isBatchMode) return;
        new GameObject("Meteor smoke checks").AddComponent<MeteorDiveSmokeRunner>().StartCoroutine(Run());
    }

    private static IEnumerator Run()
    {
        var runner = FindFirstObjectByType<MeteorDiveSmokeRunner>();
        var checks = runner.Verify();
        while (true)
        {
            object next;
            try { if (!checks.MoveNext()) break; next = checks.Current; }
            catch (Exception ex)
            {
                Debug.LogException(ex); File.WriteAllText("meteor-smoke-result.txt",ex.ToString());
                EditorApplication.Exit(1); yield break;
            }
            yield return next;
        }
        File.WriteAllText("meteor-smoke-result.txt","PASS: disappearance, horizontal tracking, pause, floor landing, plunge damage, explosion damage, fragment damage, gravity, reset, PNG captures.");
        Debug.Log("METEOR_SMOKE_PASSED");
        EditorApplication.Exit(0);
    }

    private IEnumerator Verify()
    {
        view = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        view.orthographic = true; view.orthographicSize = 5.2f;
        view.transform.position = new Vector3(0,0,-10);
        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.025f,.012f,.07f);
        var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Meteor Dive.asset");
        var owner = Player("Caster",new Vector2(-4,0),definition,new Color(1,.25f,.8f));
        var target = Player("Target",new Vector2(2,-1),definition,new Color(.3f,.8f,1));
        var floorTarget = Player("Floor target",new Vector2(2.8f,-owner.ArenaHalfSize.y),definition,Color.cyan);
        yield return new WaitForSeconds(.6f);
        var runtime = owner.CurrentWeapon;
        runtime.Fire(owner,owner.MuzzlePosition,Vector2.right,Vector2.zero);
        float firedAt = Time.time;
        var dive = owner.MeteorDive;
        Check(dive != null && !owner.CanBeHit,"hidden invulnerability");
        yield return new WaitForSeconds(.4f);
        dive.SetTarget(new Vector2(2,-1));
        float previousX = dive.Target.x;
        yield return null;
        Check(dive.Target.x < 2 && dive.Target.x >= previousX,"warning follows horizontally without teleporting");
        Check(owner.GetComponentsInChildren<SpriteRenderer>().All(r=>!r.enabled),"hidden renderers remain disabled across frames");
        Capture("meteor-warning.png");
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(.25f);
        Check(!dive.IsDescending && owner.IsMeteorDiving,"pause preserves warning phase");
        Time.timeScale = 1;
        while (Time.time-firedAt < 1.9f) yield return null;
        Check(!dive.IsDescending,"no early descent");
        while (!dive.IsDescending && Time.time-firedAt < 2.4f) yield return null;
        Check(dive.IsDescending,"descent starts after two seconds");
        while (Time.time-firedAt < 2.12f) yield return null;
        Capture("meteor-descent.png");
        while (owner.IsMeteorDiving && Time.time-firedAt < 2.6f) yield return null;
        Check(!owner.IsMeteorDiving,"landing restores controls");
        Check(Vector2.Distance(owner.transform.position,new Vector2(2,-owner.ArenaHalfSize.y))<.01f,"floor landing position ignores cursor y");
        yield return new WaitForSeconds(.12f);
        Check(target.HitsTaken==1,"real swept plunge damage above explosion range");
        Check(floorTarget.HitsTaken==1,"real Physics2D floor explosion damage");
        var fragments = FindObjectsByType<GravityFragment>(FindObjectsSortMode.None).Where(f=>f.IsSpawned).ToArray();
        Check(fragments.Length>0 && fragments.All(f=>f.LinearVelocity.y>0),"debris initially erupts upward");
        Capture("meteor-eruption.png");
        yield return new WaitForSeconds(.65f);
        Check(fragments.Any(f=>f.IsSpawned && f.LinearVelocity.y<0),"gravity bends debris downward");
        ProjectileBase.DespawnAll();
        target.ResetCombatant(new Vector2(2,0));
        yield return new WaitForSeconds(.6f);
        ProjectileBase.SpawnBuiltin<GravityFragment>(owner,new Vector2(0,.2f),Vector2.right,new WeaponStats(1,12,2,.08f,1,Color.white));
        yield return new WaitForSeconds(.25f);
        Check(target.HitsTaken==1,"real fragment collision damage");
        runtime.ResetCooldown(); runtime.Fire(owner,owner.MuzzlePosition,Vector2.right,Vector2.zero);
        yield return null;
        owner.ResetCombatant(Vector2.left);
        yield return null;
        Check(!owner.IsMeteorDiving && owner.GetComponent<SpriteRenderer>().enabled && owner.GetComponent<CircleCollider2D>().enabled,"reset restores hidden player");
        ProjectileBase.DespawnAll();
        var upgraded = new WeaponRuntime(definition);
        foreach(var upgrade in Resources.LoadAll<BarrageUpgradeDefinition>("WeaponUpgrades"))
            if(upgrade.CanApplyTo(definition)) upgraded.TryUpgrade(upgrade);
        upgraded.Fire(owner,owner.MuzzlePosition,Vector2.right,Vector2.zero);
        yield return new WaitForSeconds(2.1f);
        Check(owner.ShieldRemaining==1,"shield granted during real descent");
        Check(FindObjectsByType<MeteorMiniTank>(FindObjectsSortMode.None).Count(m=>m.IsSpawned)==2,"two mini tanks during real descent");
        Capture("meteor-mini-tanks.png");
        yield return new WaitForSeconds(.22f);
        Check(!owner.IsMeteorDiving && owner.ShieldRemaining==1,"shield persists after real landing");
        Capture("meteor-landing-shield.png");
        owner.ReceiveHit(target);
        Check(owner.ShieldRemaining==0 && owner.HitsTaken==0,"landing shield absorbs real hit");
        ProjectileBase.DespawnAll();
    }

    private static PlayerCombatant Player(string name,Vector2 position,WeaponDefinition weapon,Color color)
    {
        var player = new GameObject(name).AddComponent<PlayerCombatant>();
        player.ConfigurePreview(weapon); player.ResetCombatant(position);
        player.transform.localScale = Vector3.one*.66f;
        player.GetComponent<SpriteRenderer>().color=color;
        player.GetComponent<SpriteRenderer>().sortingOrder=5;
        return player;
    }

    private void Capture(string path)
    {
        var rt = new RenderTexture(1000,560,16);
        var previous = RenderTexture.active;
        view.targetTexture=rt; view.Render(); RenderTexture.active=rt;
        var pixels = new Texture2D(1000,560,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,1000,560),0,0); pixels.Apply();
        File.WriteAllBytes(path,pixels.EncodeToPNG());
        view.targetTexture=null; RenderTexture.active=previous;
        Destroy(pixels); rt.Release(); Destroy(rt);
    }
    private static void Check(bool condition,string label) {if(!condition)throw new Exception("FAILED: "+label);}
}
