using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy into Assets only in an isolated validation project.
public sealed class PrismShotSmokeRunner : MonoBehaviour
{
    private Camera view;
    public static void Begin()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene,"Assets/PrismValidation.unity");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(Application.isBatchMode)
            new GameObject("Prism smoke checks").AddComponent<PrismShotSmokeRunner>().StartCoroutine(Run());
    }
    private static IEnumerator Run()
    {
        var checks=FindFirstObjectByType<PrismShotSmokeRunner>().Verify();
        while(true)
        {
            object next;
            try { if(!checks.MoveNext()) break; next=checks.Current; }
            catch(Exception ex)
            {
                Debug.LogException(ex); File.WriteAllText("prism-smoke-result.txt",ex.ToString());
                EditorApplication.Exit(1); yield break;
            }
            yield return next;
        }
        File.WriteAllText("prism-smoke-result.txt","PASS: real delayed emission, seven-slot fan, scattering, wall cleanup, cancellation, PNG captures.");
        Debug.Log("PRISM_SMOKE_PASSED"); EditorApplication.Exit(0);
    }
    private IEnumerator Verify()
    {
        view=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();
        view.orthographic=true; view.orthographicSize=3.7f;
        view.transform.position=new Vector3(0,0,-10);
        view.clearFlags=CameraClearFlags.SolidColor; view.backgroundColor=new Color(.025f,.012f,.07f);
        var definition=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Prism Shot.asset");
        var icon=new GameObject("Prism card icon").AddComponent<SpriteRenderer>();
        icon.sprite=definition.icon; icon.color=new Color(.25f,.65f,1);
        icon.transform.position=new Vector3(-4,2,0);
        icon.transform.localScale=Vector3.one*1.5f;
        var owner=new GameObject("Caster").AddComponent<PlayerCombatant>();
        owner.ConfigurePreview(definition); owner.ResetCombatant(new Vector2(-4,0));
        owner.transform.localScale=Vector3.one*.66f;
        yield return new WaitForSeconds(.6f);
        var origin=owner.MuzzlePosition;
        owner.CurrentWeapon.Fire(owner,origin,Vector2.right,Vector2.zero);
        float started=Time.time;
        Check(Shots().Length==2,"first pair emitted immediately");
        while(Time.time-started<.35f) yield return null;
        Capture("prism-helix.png");
        while(Time.time-started<.6f) yield return null;
        Check(Shots().Length==7,"all seven emitted over real frames");
        Capture("prism-orbit.png");
        while(Time.time-started<PrismShot.FormationTime) yield return null;
        var shots=Shots();
        Check(shots.Length==7,"all seven survive to formation");
        var positions=shots.Select(p=>p.transform.position).ToArray();
        for(int i=0;i<shots.Length;i++)
        {
            Vector2 exact=shots[i].PositionAt(started+PrismShot.FormationTime);
            Check(shots[i].Slot==3 || shots[i].Slot==5 ? Mathf.Abs(exact.x-origin.x-5)<.001f : Mathf.Abs(Vector2.Distance(origin,exact)-5)<.001f,"formation alignment");
            Check(Vector2.Distance(shots[i].transform.position,exact)<.3f,"live position near formation");
            shots[i].transform.position=exact;
        }
        Capture("prism-formation.png");
        for(int i=0;i<shots.Length;i++) shots[i].transform.position=positions[i];
        while(Time.time-started<1.2f) yield return null;
        Check(Shots().All(p=>Vector2.Distance(origin,p.transform.position)>5),"scatter beyond fan");
        Capture("prism-after-fan.png");
        while(Time.time-started<2.2f) yield return null;
        Check(Shots().Length==0,"wall cleanup");
        var runtime=new WeaponRuntime(definition);
        runtime.Fire(owner,owner.MuzzlePosition,Vector2.right,Vector2.zero);
        ProjectileBase.DespawnOwnedBy(owner);
        yield return new WaitForSeconds(.6f);
        Check(Shots().Length==0,"cancellation prevents delayed shots");
        Check(!owner.IsPrismAimLocked,"cancellation releases muzzle");
        var upgraded=new WeaponRuntime(definition);
        foreach(var upgrade in Resources.LoadAll<BarrageUpgradeDefinition>("WeaponUpgrades"))
            if(upgrade.CanApplyTo(definition)) upgraded.TryUpgrade(upgrade);
        upgraded.Fire(owner,owner.MuzzlePosition,Vector2.right,Vector2.zero);
        Check(owner.IsPrismAimLocked,"upgraded emission locks muzzle");
        yield return new WaitForSeconds(.65f);
        Check(Shots().Length==11 && !owner.IsPrismAimLocked,"eleven real shots then muzzle release");
        Capture("prism-tail.png");
        ProjectileBase.DespawnAll();
        var target=new GameObject("Impact target").AddComponent<PlayerCombatant>();
        target.ConfigurePreview(definition); target.ResetCombatant(Vector2.zero);
        var neighbor=new GameObject("Blast target").AddComponent<PlayerCombatant>();
        neighbor.ConfigurePreview(definition); neighbor.ResetCombatant(new Vector2(0,.65f));
        yield return new WaitForSeconds(.6f);
        var projectile=ProjectileBase.SpawnBuiltin<PrismPellet>(owner,new Vector2(-2,0),Vector2.right,
            new WeaponStats(5,6,2,.1f,5,Color.cyan),WeaponEffects.PrismExplosion);
        projectile.Configure(4,new Vector2(-2,0),new Vector2(-2,0),Vector2.right,Time.time-PrismShot.LaunchDelay(4));
        yield return new WaitForSeconds(.27f);
        Check(target.HitsTaken==1 && neighbor.HitsTaken==1,"real impact and nearby explosion damage");
        Capture("prism-impact.png");
        ProjectileBase.DespawnAll();
    }
    private static PrismPellet[] Shots()=>FindObjectsByType<PrismPellet>(FindObjectsSortMode.None)
        .Where(p=>p.IsSpawned).OrderBy(p=>p.Slot).ToArray();
    private void Capture(string path)
    {
        var rt=new RenderTexture(1000,560,16);
        var previous=RenderTexture.active;
        view.targetTexture=rt; view.Render(); RenderTexture.active=rt;
        var pixels=new Texture2D(1000,560,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,1000,560),0,0); pixels.Apply();
        File.WriteAllBytes(path,pixels.EncodeToPNG());
        view.targetTexture=null; RenderTexture.active=previous;
        Destroy(pixels); rt.Release(); Destroy(rt);
    }
    private static void Check(bool condition,string label)
    { if(!condition) throw new Exception("FAILED: "+label); }
}
