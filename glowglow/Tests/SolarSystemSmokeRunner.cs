// Copy into Assets only in an isolated validation project.
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class SolarSystemSmokeRunner : MonoBehaviour
{
    public static void Begin()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene,"Assets/SolarValidation.unity");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if(Application.isBatchMode) new GameObject("Solar smoke").AddComponent<SolarSystemSmokeRunner>().StartCoroutine(Run());
    }
    private static IEnumerator Run()
    {
        var camera=new GameObject("Solar camera",typeof(Camera)).GetComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=5.2f;
        camera.transform.position=new Vector3(0,0,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.008f,.055f);
        var weapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Solar System.asset");
        var owner=new GameObject("Solar caster").AddComponent<PlayerCombatant>();
        owner.ConfigurePreview(weapon); owner.ResetCombatant(Vector2.zero);
        foreach(var upgrade in Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades"))
            if(upgrade.CanApplyTo(weapon)) owner.CurrentWeapon.TryUpgrade(upgrade);
        var border=new GameObject("Arena reference",typeof(LineRenderer)).GetComponent<LineRenderer>();
        border.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
        border.loop=true; border.positionCount=4; border.startWidth=border.endWidth=.035f;
        border.startColor=border.endColor=new Color(.35f,.12f,.5f); border.sortingOrder=1;
        border.SetPositions(new[]{new Vector3(-8.7f,-4.65f),new Vector3(-8.7f,4.65f),new Vector3(8.7f,4.65f),new Vector3(8.7f,-4.65f)});
        yield return null;
        owner.CurrentWeapon.Fire(owner,owner.MuzzlePosition,Vector2.right);
        float castAt=Time.time;
        var sun=owner.SolarSystem;
        yield return new WaitForSeconds(.8f);
        yield return null;
        var target=new RenderTexture(768,768,24);
        var pixels=new Texture2D(768,768,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        pixels.ReadPixels(new Rect(0,0,768,768),0,0); pixels.Apply();
        Directory.CreateDirectory("ValidationOutput");
        File.WriteAllBytes("ValidationOutput/solar-system-play.png",pixels.EncodeToPNG());
        RenderTexture.active=previous; camera.targetTexture=null;
        Destroy(pixels); Destroy(target);
        bool sawFlare=false,sawComet=false;
        while(Time.time-castAt<6.15f)
        {
            sawFlare|=FindFirstObjectByType<SolarFlare>()!=null;
            sawComet|=FindFirstObjectByType<SolarComet>()!=null;
            yield return null;
        }
        bool released=FindFirstObjectByType<SolarPlanet>()!=null;
        bool ended=sun!=null && !sun.IsSpawned && owner.SolarSystem==null && owner.ShieldRemaining==0;
        yield return new WaitForSeconds(5f);
        bool cleaned=FindFirstObjectByType<SolarPlanet>()==null && FindFirstObjectByType<SolarComet>()==null && FindFirstObjectByType<SolarFlare>()==null;
        bool passed=ended && released && sawFlare && sawComet && cleaned;
        File.WriteAllText("ValidationOutput/solar-play-result.txt",passed ? "PASS: live flare, comet shower, post-expiry planet release and bounded cleanup." : $"FAIL: ended={ended}, released={released}, flare={sawFlare}, comet={sawComet}, cleaned={cleaned}");
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
