using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class OverdriveSmokeRunner : MonoBehaviour
{
    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isBatchMode) new GameObject("Overdrive checks").AddComponent<OverdriveSmokeRunner>().StartCoroutine(Run());
    }
    private static IEnumerator Run()
    {
        var test = Verify();
        while (true)
        {
            object current;
            try { if (!test.MoveNext()) break; current = test.Current; }
            catch (Exception ex) { Debug.LogException(ex); File.WriteAllText("overdrive-result.txt", ex.ToString()); EditorApplication.Exit(1); yield break; }
            yield return current;
        }
        File.WriteAllText("overdrive-result.txt", "PASS: shield 3, no health spill, pulse does not consume shield, 150% speed/inertia, firing lock, attached rotating spike contacts, break/5s/pause/reset/match-end cleanup, pooled reuse.");
        Debug.Log("OVERDRIVE_SMOKE_PASSED"); EditorApplication.Exit(0);
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Barrel(PlayerCombatant player) => player.transform.Find("Barrel Pivot/Barrel").GetComponent<SpriteRenderer>().enabled;
    private static PlayerCombatant Player(string name, WeaponDefinition weapon, Vector2 position)
    {
        var player = new GameObject(name).AddComponent<PlayerCombatant>();
        player.transform.localScale = Vector3.one * .72f;
        player.ConfigurePreview(weapon);
        player.ResetCombatant(position);
        return player;
    }
    private static IEnumerator Verify()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        var definition = catalog.cards[5].weapon;
        Check(catalog.cards.Length == 8 && definition.displayName == "Overdrive", "Deck registration");
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 3;
        camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = new Color(.025f, .01f, .06f);
        var owner = Player("Owner", definition, new Vector2(-2, 0));
        var target = Player("Target", catalog.cards[0].weapon, new Vector2(4, 0));
        owner.GetComponent<SpriteRenderer>().color = Color.magenta;
        target.GetComponent<SpriteRenderer>().color = new Color(.5f, .4f, 1);
        yield return new WaitForSeconds(.6f);
        var runtime = owner.CurrentWeapon;
        runtime.Fire(owner, owner.transform.position, Vector2.right);
        Check(owner.IsOverdriving && owner.ShieldRemaining == 3, "Activation/shield");
        Check(Mathf.Approximately(owner.MovementMultiplier, 1.5f) && !Barrel(owner), "Speed/gunless state");
        var effect = owner.Overdrive;
        Check(effect.transform.parent == owner.transform && effect.GetComponentsInChildren<PolygonCollider2D>().Length == 6, "Attached spikes");
        var basic = new WeaponRuntime(catalog.cards[0].weapon);
        Check(basic.Fire(owner, owner.transform.position, Vector2.right) == null && basic.CooldownRemaining == 0, "Other weapons must be blocked without spending cooldown");
        float start = Time.time;
        while (Time.time - start < .25f) { owner.MovePreview(Vector2.right, 5.2f * Time.deltaTime); yield return null; }
        float x = owner.transform.position.x;
        start = Time.time;
        while (Time.time - start < .1f) { owner.MovePreview(Vector2.zero, 5.2f * Time.deltaTime); yield return null; }
        Check(owner.transform.position.x > x + .05f, "Releasing input should coast");
        Quaternion rotation = effect.transform.rotation;
        yield return new WaitForSeconds(.1f);
        Check(Quaternion.Angle(rotation, effect.transform.rotation) > 5, "Spikes not rotating");
        owner.ReceiveHit(target);
        Check(owner.ShieldRemaining == 2 && owner.HitsTaken == 0, "Shield first hit");
        yield return new WaitForSeconds(.85f); owner.ReceiveHit(target);
        Check(owner.ShieldRemaining == 1 && owner.HitsTaken == 0, "Shield second hit");
        yield return new WaitForSeconds(.85f); owner.ReceiveHit(target);
        Check(!owner.IsOverdriving && owner.ShieldRemaining == 0 && owner.HitsTaken == 0, "Shield break must not spill into health");
        Check(Barrel(owner) && owner.MovementMultiplier == 1 && !effect.gameObject.activeSelf, "Shield break restoration");
        Check(runtime.CooldownRemaining > 0 && runtime.Fire(owner, Vector2.zero, Vector2.right) == null, "Cooldown must survive shield break");
        yield return new WaitForSeconds(.85f); owner.ReceiveHit(target);
        Check(owner.HitsTaken == 1, "Normal damage must resume after shield break");

        owner.ResetCombatant(Vector2.zero);
        target.ResetCombatant(new Vector2(1.5f, 0));
        yield return new WaitForSeconds(.6f);
        owner.CurrentWeapon.Fire(owner, Vector2.zero, Vector2.right);
        Check(owner.Overdrive == effect && owner.ShieldRemaining == 3, "Pooled state reset");
        new WeaponRuntime(catalog.cards[4].weapon).Fire(target, target.transform.position, Vector2.left);
        yield return new WaitForSeconds(.3f);
        Check(owner.ShieldRemaining == 3 && owner.HitsTaken == 0, "Zero-damage pulse must not consume shield");
        Check(owner.MovementMultiplier <= 1.5f, "Slow/boost composition");
        Time.timeScale = 0; rotation = effect.transform.rotation;
        yield return new WaitForSecondsRealtime(.15f);
        Check(owner.IsOverdriving && Quaternion.Angle(rotation, effect.transform.rotation) < .001f, "Pause changed Overdrive");
        Time.timeScale = 1;
        target.transform.position = new Vector2(5, 0);
        yield return new WaitForSeconds(4.8f);
        Check(!owner.IsOverdriving && owner.MovementMultiplier == 1 && Barrel(owner), "Five-second expiry");

        owner.ResetCombatant(Vector2.zero); target.ResetCombatant(new Vector2(.72f, 0));
        yield return new WaitForSeconds(.6f);
        owner.CurrentWeapon.Fire(owner, Vector2.zero, Vector2.right);
        yield return new WaitForSeconds(.15f);
        Check(target.HitsTaken == 1 && owner.ShieldRemaining == 3, "Spike collision/owner filtering");
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            var rt = new RenderTexture(960, 480, 24); camera.targetTexture = rt; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(960, 480, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 960, 480), 0, 0); texture.Apply();
            File.WriteAllBytes("overdrive.png", texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Destroy(rt); Destroy(texture);
        }
        yield return new WaitForSeconds(.9f);
        Check(target.HitsTaken >= 2, "Persistent contact must respect and resume after hit invulnerability");
        owner.ResetCombatant(Vector2.zero);
        Check(!owner.IsOverdriving && owner.MovementMultiplier == 1 && Barrel(owner), "Restart cleanup");
        float resetX = owner.transform.position.x;
        owner.MovePreview(Vector2.zero, 5.2f * Time.deltaTime);
        Check(owner.transform.position.x == resetX, "Restart retained inertia");
        target.transform.position = new Vector2(5, 0);

        var match = new GameObject("Match").AddComponent<MatchController>(); match.enabled = false;
        var realPlayer = new GameObject("Real Player").AddComponent<PlayerCombatant>();
        realPlayer.Configure(1, match, definition, LocalPlayerInput.ControlScheme.KeyboardTwo, camera, target.transform);
        typeof(MatchController).GetProperty("IsPlaying").SetValue(match, true);
        realPlayer.CurrentWeapon.Fire(realPlayer, new Vector2(-4, 2), Vector2.right);
        Check(realPlayer.IsOverdriving, "Real match activation");
        typeof(MatchController).GetProperty("IsPlaying").SetValue(match, false);
        yield return null; yield return null;
        Check(!realPlayer.IsOverdriving && Barrel(realPlayer), "Match-end cleanup");
        ProjectileBase.DespawnAll();
    }
}
