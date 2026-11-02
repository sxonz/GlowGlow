// Copy into Assets only in the isolated validation project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SingleplayerTwinSmokeRunner : MonoBehaviour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Results = new();
    private static int checks;

    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isBatchMode && Environment.CommandLine.Contains("SingleplayerTwinSmokeRunner.Begin") && SceneManager.GetActiveScene().name == "Title")
            new GameObject("Menu and twin validation").AddComponent<SingleplayerTwinSmokeRunner>().StartCoroutine(Run());
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
                File.WriteAllText("ValidationOutput/singleplayer-twin-result.txt", string.Join("\n", Results) + "\nFAIL: " + ex);
                Debug.LogException(ex);
                EditorApplication.Exit(1);
                yield break;
            }
            yield return next;
        }
        Results.Add("PASS: " + checks + " checks.");
        File.WriteAllText("ValidationOutput/singleplayer-twin-result.txt", string.Join("\n", Results));
        Debug.Log("SINGLEPLAYER_TWIN_PASSED: " + checks);
        EditorApplication.Exit(0);
    }

    private static IEnumerator Verify()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
        yield return null;
        var title = FindFirstObjectByType<TitleScreenController>();
        Check(title != null, "Real title scene loads");
        title.PlaySolo();
        yield return new WaitForSecondsRealtime(.3f);
        var panel = (CanvasGroup)typeof(TitleScreenController).GetField("soloPanel", Private).GetValue(title);
        var back = (Button)typeof(TitleScreenController).GetField("soloBackButton", Private).GetValue(title);
        var scroll = panel.GetComponentInChildren<ScrollRect>();
        Check(scroll != null && !scroll.horizontal && scroll.vertical, "Singleplayer has a vertical scroll list");
        Check(back.transform.parent == panel.transform.Find("Content"), "BACK remains outside the scrolling viewport");
        Check(scroll.viewport.GetComponent<RectMask2D>() != null, "Overflow is clipped");
        Check(EventSystem.current.currentSelectedGameObject.name == "훈련장", "Opening singleplayer selects training");
        Check(scroll.content.GetChild(0).name == "훈련장" && scroll.content.GetChild(1).name == "봇 대전", "Mode order preserved");
        Check(scroll.content.GetComponentsInChildren<Button>().Count(b => b.name == "봇 대전") == 1, "One live bot match button");
        Capture("singleplayer-menu.png");
        yield return null;

        var create = typeof(TitleScreenController).GetMethod("CreateModeButton", Private);
        var configure = typeof(TitleScreenController).GetMethod("ConfigureSoloScrolling", Private);
        for (int i = 0; i < 10; i++)
            create.Invoke(title, new object[] { panel, "MODE " + (i + 1), "SCROLL VALIDATION", false });
        configure.Invoke(title, null);
        configure.Invoke(title, null);
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null; // ScrollRect updates auto-hidden scrollbar visibility in LateUpdate.
        Check(panel.GetComponentsInChildren<ScrollRect>().Length == 1, "Repeated configuration does not duplicate the menu");
        Check(scroll.content.rect.height > scroll.viewport.rect.height, "Extra modes overflow the larger panel rather than shrink rows");
        Check(scroll.verticalScrollbar.gameObject.activeSelf, "Overflow shows scrollbar");
        Check(scroll.content.Cast<Transform>().All(t => Mathf.Abs(((RectTransform)t).rect.height - 88) < .1f), "Every mode retains its readable height");
        Vector3 backPosition = back.transform.position;
        scroll.verticalNormalizedPosition = 1;
        scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -3) });
        Check(scroll.content.anchoredPosition.y > 40, "Mouse wheel scrolls the list");
        scroll.verticalNormalizedPosition = 1;
        Vector2 pointer = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.position);
        var drag = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = pointer };
        scroll.OnInitializePotentialDrag(drag);
        scroll.OnBeginDrag(drag);
        drag.position += Vector2.up * 100;
        scroll.OnDrag(drag);
        scroll.OnEndDrag(drag);
        Check(scroll.content.anchoredPosition.y > 40, "Pointer drag scrolls the list");
        scroll.StopMovement();
        var last = scroll.content.GetChild(scroll.content.childCount - 1).GetComponent<Button>();
        EventSystem.current.SetSelectedGameObject(last.gameObject);
        Check(Visible(scroll, last), "Selecting an offscreen mode scrolls it fully into view");
        Check(back.transform.position == backPosition, "BACK does not move during scrolling");
        var move = new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down };
        last.OnMove(move);
        Check(EventSystem.current.currentSelectedGameObject == back.gameObject, "Last mode navigates to BACK");
        back.OnMove(move);
        var first = scroll.content.GetChild(0).GetComponent<Button>();
        Check(EventSystem.current.currentSelectedGameObject == first.gameObject && Visible(scroll, first), "BACK wraps to the visible first mode");
        var panelRect = (RectTransform)panel.transform;
        Vector2 originalSize = panelRect.sizeDelta;
        panelRect.sizeDelta -= new Vector2(0, 130);
        Canvas.ForceUpdateCanvases();
        EventSystem.current.SetSelectedGameObject(last.gameObject);
        Check(Visible(scroll, last), "Shorter panels still reveal the selected row");
        panelRect.sizeDelta = originalSize;
        Canvas.ForceUpdateCanvases();
        EventSystem.current.SetSelectedGameObject(first.gameObject);
        Capture("singleplayer-overflow.png");
        yield return null;
        back.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.3f);
        Check(!panel.gameObject.activeSelf, "BACK returns to the main menu");
        title.PlaySolo();
        yield return new WaitForSecondsRealtime(.3f);
        Check(EventSystem.current.currentSelectedGameObject == first.gameObject && Visible(scroll, first), "Reopening starts at training");

        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Twin Satellites.asset");
        var owner = new GameObject("Orbit caster").AddComponent<PlayerCombatant>();
        owner.ConfigurePreview(weapon);
        owner.ResetCombatant(new Vector2(-6, -3));
        var stats = new WeaponStats(8, 3.6f, 4.8f, .23f, 11, Color.cyan);
        TwinSatellite.Cast(owner, Vector2.zero, stats);
        var twins = ProjectileBase.ActiveProjectiles.OfType<TwinSatellite>().ToArray();
        Check(twins.Length == 2, "Twin cast spawns two satellites");
        Vector2 center = (twins[0].transform.position + twins[1].transform.position) * .5f;
        Vector2 start = twins[0].PositionAt(0);
        float expectedSpeed = Mathf.PI * 2 * TwinSatellite.OrbitRadius / TwinSatellite.OrbitPeriod;
        Check(Mathf.Abs(twins[0].LinearVelocity.magnitude - expectedSpeed) < .001f, "Initial velocity is already the orbital tangent");
        for (int i = 0; i <= 480; i++)
        {
            float age = i * .01f;
            Vector2 a = twins[0].PositionAt(age), b = twins[1].PositionAt(age);
            if (Mathf.Abs(Vector2.Distance(a, center) - 1.6f) > .0001f || (a + b - 2 * center).sqrMagnitude > .000001f)
                throw new Exception("Orbit radius/opposition changed at " + age);
            float speed = Vector2.Distance(a, twins[0].PositionAt(age + .001f)) / .001f;
            if (Mathf.Abs(speed - expectedSpeed) > .005f) throw new Exception("Nonuniform orbit speed at " + age);
        }
        Check(Vector2.Distance(twins[0].PositionAt(2.4f), start) < .0001f, "Full lifetime: circular, opposite, constant speed; one orbit takes 2.4 seconds");
        var initial = twins.Select(t => t.transform.position).ToArray();
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(.1f);
        Check(twins.Select((t, i) => t.transform.position == initial[i]).All(v => v), "Pause freezes both satellites");
        Time.timeScale = 1;
        yield return new WaitForSeconds(.85f);
        Check(ProjectileBase.ActiveProjectiles.OfType<CelestialBullet>().Count(p => p is not TwinSatellite) == 6, "First fan still releases six bullets after warning");
        Check(twins.All(t => Mathf.Abs(t.LinearVelocity.magnitude - expectedSpeed) < .001f), "Live movement keeps a constant speed");
        var tick = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), twins[0], typeof(TwinSatellite).GetMethod("Tick", Private));
        tick(.016f);
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) tick(.016f);
        allocation = GC.GetAllocatedBytesForCurrentThread() - allocation;
        Check(allocation == 0, "Warmed orbit update allocates zero bytes: " + allocation);
        var ids = twins.Select(t => t.GetInstanceID()).ToArray();
        ProjectileBase.DespawnOwnedBy(owner);
        TwinSatellite.Cast(owner, Vector2.zero, stats);
        Check(ProjectileBase.ActiveProjectiles.OfType<TwinSatellite>().All(t => ids.Contains(t.GetInstanceID())), "Satellites reuse pooled objects");
        ProjectileBase.DespawnOwnedBy(owner);

        var previewParent = new GameObject("Preview bounds", typeof(RectTransform)).GetComponent<RectTransform>();
        var previewObject = new GameObject("Preview", typeof(RectTransform), typeof(RawImage), typeof(BarragePreview));
        previewObject.transform.SetParent(previewParent, false);
        var view = previewObject.GetComponent<RawImage>();
        var preview = previewObject.GetComponent<BarragePreview>();
        preview.Configure(view);
        preview.Select(weapon);
        foreach (Vector2 size in new[] { new Vector2(600, 180), new Vector2(300, 300), new Vector2(850, 350) })
        {
            previewParent.sizeDelta = size;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rendered = view.rectTransform.rect;
            Check(Mathf.Abs(rendered.width / rendered.height - (float)view.texture.width / view.texture.height) < .001f,
                "Preview preserves circular geometry at " + size);
            Check(rendered.width <= size.x + .1f && rendered.height <= size.y + .1f, "Preview fits its panel");
        }
        previewObject.SetActive(false);
    }

    private static bool Visible(ScrollRect scroll, Button button)
    {
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)button.transform);
        return bounds.min.y >= scroll.viewport.rect.yMin - .1f && bounds.max.y <= scroll.viewport.rect.yMax + .1f;
    }

    private static void Capture(string name)
    {
        var camera = Camera.main;
        var canvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas);
        var previousMode = canvas.renderMode;
        var previousCamera = canvas.worldCamera;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        File.WriteAllBytes("ValidationOutput/" + name, pixels.EncodeToPNG());
        Destroy(pixels);
        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        canvas.renderMode = previousMode;
        canvas.worldCamera = previousCamera;
        RenderTexture.ReleaseTemporary(target);
        Canvas.ForceUpdateCanvases();
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
        Results.Add("OK: " + message);
    }
}
