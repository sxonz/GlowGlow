using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Run only in a disposable copy of the Unity project.
public sealed class BotMatchSmokeRunner : MonoBehaviour
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool booted;
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isBatchMode || booted) return;
        booted = true;
        var runner = new GameObject("Bot checks").AddComponent<BotMatchSmokeRunner>();
        DontDestroyOnLoad(runner); runner.StartCoroutine(runner.Run());
    }
    private IEnumerator Run()
    {
        var test = Verify();
        while (true)
        {
            object current;
            try { if (!test.MoveNext()) break; current = test.Current; }
            catch (Exception ex) { File.WriteAllText("bot-result.txt", ex.ToString()); Debug.LogException(ex); EditorApplication.Exit(1); yield break; }
            yield return current;
        }
        File.WriteAllText("bot-result.txt", "PASS: setup validation/presets, bot rating handoff, fair draft, no pre-draft actions, autonomous movement/fire/hits, difficulty scaling, cooldown rules, win/restart, mode isolation.");
        Debug.Log("BOT_MATCH_SMOKE_PASSED"); EditorApplication.Exit(0);
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static Button Button(string name) => FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == name);

    private static IEnumerator Verify()
    {
        yield return null;
        string saved = PlayerPrefs.GetString(DeckCatalog.SaveKey, "{}");
        var title = FindFirstObjectByType<TitleScreenController>();
        title.PlaySolo(); yield return new WaitForSeconds(.3f);
        Check(FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b => b.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "봇 대전")) == 1, "Exactly one bot mode entry");
        Check(Button("봇 대전").interactable, "Bot mode is unlocked");
        Button("봇 대전").onClick.Invoke(); yield return null;
        var setup = FindFirstObjectByType<BotMatchSetup>();
        var rating = setup.GetComponentInChildren<TMP_InputField>();
        var slider = setup.GetComponentInChildren<Slider>();
        Check(slider != null && slider.wholeNumbers && slider.minValue == 200 && slider.maxValue == 3000, "Integer rating slider range");
        Canvas.ForceUpdateCanvases();
        var sliderRect = (RectTransform)slider.transform;
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        pointer.button = UnityEngine.EventSystems.PointerEventData.InputButton.Left;
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, sliderRect.TransformPoint(new Vector2(sliderRect.rect.width * .25f, 0)));
        slider.OnPointerDown(pointer);
        Check(slider.value > 2000 && rating.text == Mathf.RoundToInt(slider.value).ToString(), "Pointer click adjusts slider and input");
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, sliderRect.TransformPoint(new Vector2(sliderRect.rect.width * .4f, 0)));
        slider.OnDrag(pointer); slider.OnPointerUp(pointer);
        Check(slider.value > 2600 && rating.text == Mathf.RoundToInt(slider.value).ToString(), "Pointer drag adjusts slider and input");
        slider.value = 1731;
        Check(rating.text == "1731", "Slider updates numeric input");
        Button("+").onClick.Invoke(); Check(rating.text == "1732" && slider.value == 1732, "Fine increment synchronizes slider");
        Button("−").onClick.Invoke(); Check(rating.text == "1731" && slider.value == 1731, "Fine decrement synchronizes slider");
        slider.value = 200; Button("−").onClick.Invoke(); Check(rating.text == "200", "Minimum clamp");
        slider.value = 3000; Button("+").onClick.Invoke(); Check(rating.text == "3000", "Maximum clamp");
        foreach (string invalid in new[] { "", "199", "3001" })
        {
            rating.text = invalid; Check(!Button("대전 시작").interactable, "Reject rating " + invalid);
        }
        Button("초신성 2400").onClick.Invoke(); Check(rating.text == "2400", "Rating preset");
        Check(slider.value == 2400, "Preset updates slider");
        rating.text = "1600"; Check(Button("대전 시작").interactable, "Valid custom rating");
        Check(slider.value == 1600, "Numeric input updates slider");
        rating.DeactivateInputField();
        yield return null;
        Capture(setup.GetComponentInParent<Canvas>(), "bot-setup.png");
        Button("대전 시작").onClick.Invoke(); yield return new WaitForSeconds(.6f);
        var match = FindFirstObjectByType<MatchController>();
        Check(match != null && match.IsBotMatch && match.BotRating == 1600 && !match.IsTraining, "Bot match rating handoff");
        var bot = match.PlayerTwo.GetComponent<BotPlayerInput>();
        Check(bot.Rating == 1600, "AI uses entered rating");
        Check(match.IsDrafting && !match.IsPlaying && match.PlayerTwo.Hand != null, "Bot has separate starting draft");
        Check(match.PlayerTwo.Hand.Drawn.Count + match.PlayerTwo.Hand.Drawn.Sum(w => w.Upgrades.Count) == 3, "Exactly three bot choices");
        Vector2 start = match.PlayerTwo.transform.position;
        yield return new WaitForSeconds(.5f);
        Check((Vector2)match.PlayerTwo.transform.position == start && !bot.ReadCommand(start).Fire, "No actions during draft");
        var screen = FindFirstObjectByType<OpeningDraftScreen>();
        var summary = GameObject.Find("Bot Selection Summary");
        Check(summary != null && summary.activeInHierarchy, "Bot selection shortcut visible");
        var picks = summary.GetComponentsInChildren<CanvasGroup>();
        Check(picks.Length == 3 && picks.All(p => p.alpha == 1 && !p.blocksRaycasts), "Three quick reveals never block player input");
        var validNames = match.PlayerTwo.Hand.Drawn.Select(w => w.Definition.displayName).ToList();
        foreach (var draftedWeapon in match.PlayerTwo.Hand.Drawn)
            foreach (var upgrade in Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades"))
                if (draftedWeapon.Upgrades.GetLevel(upgrade) > 0) validNames.Add(upgrade.displayName);
        Check(picks.All(p => validNames.Contains(p.transform.Find("Name").GetComponent<TMP_Text>().text)), "Summary shows actual acquired bot picks");
        Capture(screen.GetComponentInChildren<Canvas>(), "bot-draft-summary.png");
        for (int i = 0; i < 3; i++)
        {
            Set(screen, "deadline", Time.unscaledTime - 1);
            typeof(OpeningDraftScreen).GetMethod("FinishRound", Flags).Invoke(screen, new object[] { true });
        }
        Check(match.IsPlaying && !match.IsDrafting, "Human draft starts combat");
        float normalReaction = bot.ReactionInterval;
        bot.Configure(match.PlayerTwo, match.PlayerOne, match, BotPlayerInput.MinRating);
        Check(bot.ReactionInterval > normalReaction, "Low rating reacts slower");
        bot.Configure(match.PlayerTwo, match.PlayerOne, match, BotPlayerInput.MaxRating);
        Check(bot.ReactionInterval < normalReaction, "High rating reacts faster");
        Check(bot.ReactionInterval == 0, "Max rating evaluates every frame");
        VerifyContinuousRating(bot, match);
        VerifyPredictiveAvoidance(bot, match);
        VerifyTelegraphAvoidance(bot, match);
        var warningStats = new WeaponStats(2, 12, 1.2f, .5f, 20, Color.white);
        var liveLaser = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(match.PlayerOne, new Vector2(-5, 0), Vector2.right, warningStats);
        liveLaser.Configure(new BarrageStep { shape = BarrageShape.Line, dealsDamage = false, duration = 1.2f });
        yield return new WaitForSeconds(.5f);
        Check(Mathf.Abs(match.PlayerTwo.transform.position.y) > 1f, "Live bot moves sideways out of laser telegraph");
        ProjectileBase.DespawnAll();
        match.PlayerTwo.transform.position = start; bot.ResetBrain();
        var liveBomb = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(match.PlayerOne, start, Vector2.right, warningStats);
        liveBomb.Configure(new BarrageStep { shape = BarrageShape.Circle, dealsDamage = false, dimensions = new Vector2(3, 3), duration = 1.2f });
        yield return new WaitForSeconds(.65f);
        Check(Vector2.Distance(match.PlayerTwo.transform.position, start) > 2f, "Live bot exits bomb telegraph instead of approaching opponent");
        ProjectileBase.DespawnAll();
        match.PlayerTwo.transform.position = start; bot.ResetBrain();
        // Keep player alive while observing the real autonomous controller and physics.
        var player = match.PlayerOne;
        float until = Time.time + 14f;
        bool moved = false, fired = false, hit = false;
        while (Time.time < until && !hit)
        {
            moved |= Vector2.Distance(start, match.PlayerTwo.transform.position) > .5f;
            fired |= FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None).Any(p => p.IsSpawned && p.Owner == match.PlayerTwo);
            hit |= player.HitsTaken > 0;
            yield return null;
        }
        Check(moved && fired, "Bot moves and fires without keyboard input");
        // Ensure a known ranged loadout can score against a stationary player.
        if (!hit)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
            match.PlayerTwo.EquipWeapon(catalog.cards[0].weapon);
            typeof(PlayerCombatant).GetField("<Hand>k__BackingField", Flags).SetValue(match.PlayerTwo, null);
            bot.ResetBrain();
            until = Time.time + 10f;
            while (Time.time < until && player.HitsTaken == 0) yield return null;
            hit = player.HitsTaken > 0;
        }
        Check(hit, "Bot projectiles hit the player");
        var weapon = match.PlayerTwo.CurrentWeapon;
        weapon.ResetCooldown();
        weapon.Fire(match.PlayerTwo, match.PlayerTwo.MuzzlePosition, Vector2.left);
        Check(weapon.Fire(match.PlayerTwo, match.PlayerTwo.MuzzlePosition, Vector2.left) == null, "Bot respects weapon cooldown");
        while (player.HitsTaken < 3)
        {
            Set(player, "invulnerableUntil", Time.time - 1); player.ReceiveHit(match.PlayerTwo);
        }
        Check(!match.IsPlaying && match.Winner == match.PlayerTwo && !bot.ReadCommand(match.PlayerTwo.transform.position).Fire, "Bot victory stops actions");
        match.RestartMatch();
        Check(match.IsBotMatch && match.BotRating == 1600 && match.IsDrafting && player.HitsTaken == 0, "Restart retains mode and resets round");
        Check(GameObject.Find("Bot Selection Summary").GetComponentsInChildren<CanvasGroup>().Length == 3, "Restart reuses three summary tiles");
        Check(PlayerPrefs.GetString(DeckCatalog.SaveKey, "{}") == saved, "Saved deck unchanged");
        SceneManager.LoadScene("Title"); yield return new WaitForSeconds(.3f);
        MatchController.RequestTraining(); SceneManager.LoadScene("Arena"); yield return new WaitForSeconds(.5f);
        match = FindFirstObjectByType<MatchController>();
        Check(match.IsTraining && !match.IsBotMatch && FindFirstObjectByType<BotPlayerInput>() == null, "No bot leaks into training");
    }

    private static void Capture(Canvas canvas, string path)
    {
        var camera = Camera.main; var output = new RenderTexture(1600, 900, 24);
        camera.targetTexture = output; canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = output;
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Destroy(pixels); output.Release(); Destroy(output);
    }

    private static Vector2 Avoid(BotPlayerInput bot, Vector2 position, Vector2 preferred, bool expected)
    {
        object[] args = { position, preferred, Vector2.zero, false };
        bool avoiding = (bool)typeof(BotPlayerInput).GetMethod("TryAvoidAttacks", Flags).Invoke(bot, args);
        Check(avoiding == expected, "Visible warning detection");
        return (Vector2)args[2];
    }

    private static void VerifyContinuousRating(BotPlayerInput bot, MatchController match)
    {
        bot.Configure(match.PlayerTwo, match.PlayerOne, match, BotPlayerInput.MinRating);
        Check(bot.ReactionInterval >= .9f && bot.AimError >= 2.9f, "Low rating has slow, inaccurate decisions");
        Check(bot.FireProbability <= .21f && bot.DashProbability <= .02f && bot.AvoidanceResponse <= .16f,
            "Low rating hesitates to fire and only weakly corrects toward safe routes");
        for (int rating = BotPlayerInput.MinRating + 1; rating <= BotPlayerInput.MaxRating; rating++)
        {
            float reaction = bot.ReactionInterval, prediction = bot.PredictionHorizon;
            float margin = bot.AvoidanceMargin, commitment = bot.EscapeCommitment, dash = bot.DashProbability;
            float aim = bot.AimError, fire = bot.FireProbability, avoidance = bot.AvoidanceResponse;
            int directions = bot.EscapeDirections;
            bot.Configure(match.PlayerTwo, match.PlayerOne, match, rating);
            bool tierBoundary = rating == 600 || rating == 1200 || rating == 1800 || rating == 2400 || rating == 2800 || rating == 3000;
            float limit = tierBoundary ? .007f : .001f;
            Check(bot.ReactionInterval < reaction && reaction - bot.ReactionInterval < limit, "Bounded reaction at " + rating);
            Check(!tierBoundary || reaction - bot.ReactionInterval > .002f, "Small tier bonus at " + rating);
            Check(bot.PredictionHorizon > prediction && bot.PredictionHorizon - prediction < limit, "Bounded prediction at " + rating);
            Check(bot.AvoidanceMargin < margin && margin - bot.AvoidanceMargin < limit, "Bounded precision at " + rating);
            Check(bot.EscapeCommitment < commitment && commitment - bot.EscapeCommitment < limit, "Bounded commitment at " + rating);
            Check(bot.DashProbability > dash && bot.DashProbability - dash < limit, "Bounded dash at " + rating);
            Check(bot.AimError < aim && aim - bot.AimError < limit * 3, "Bounded aim improvement at " + rating);
            Check(bot.FireProbability > fire && bot.FireProbability - fire < limit, "Bounded firing improvement at " + rating);
            Check(bot.AvoidanceResponse > avoidance && bot.AvoidanceResponse - avoidance < limit, "Bounded avoidance improvement at " + rating);
            Check(bot.EscapeDirections >= directions && bot.EscapeDirections - directions <= 1, "Small integer search steps at " + rating);
        }
        Check(bot.ReactionInterval == 0 && Mathf.Approximately(bot.AimError, .06f) && bot.AvoidanceResponse == 1,
            "Top rating keeps full reaction, aim and evasion strength");
    }

    private static void VerifyPredictiveAvoidance(BotPlayerInput bot, MatchController match)
    {
        ProjectileBase.DespawnAll();
        var stats = new WeaponStats(1, 20, 2, .13f, 20, Color.white);
        var incoming = ProjectileBase.SpawnBuiltin<BulletProjectile>(match.PlayerOne, new Vector2(-4, 0), Vector2.right, stats);
        Vector2 escape = Avoid(bot, Vector2.zero, Vector2.left, true);
        Check(Mathf.Abs(escape.y) > .3f, "Superhuman anticipates fast incoming bullet before proximity detection");
        object[] args = { incoming, Vector2.zero, escape * 5.8f, match.PlayerTwo.BodyRadius + .08f, .6f };
        float clearance = (float)typeof(BotPlayerInput).GetMethod("BulletClearance", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        Check(clearance > 0, "Chosen route clears swept bullet path");
        bot.Configure(match.PlayerTwo, match.PlayerOne, match, 1200);
        Check(bot.ReactionInterval > .6f, "Normal rating has a larger reaction delay");
        Avoid(bot, Vector2.zero, Vector2.left, false);
        bot.Configure(match.PlayerTwo, match.PlayerOne, match, BotPlayerInput.MinRating);
        Avoid(bot, Vector2.zero, Vector2.left, false);
        bot.Configure(match.PlayerTwo, match.PlayerOne, match, BotPlayerInput.MaxRating);
        ProjectileBase.DespawnAll();
        ProjectileBase.SpawnBuiltin<BulletProjectile>(match.PlayerOne, new Vector2(-4, 0), Vector2.left, stats);
        Avoid(bot, Vector2.zero, Vector2.zero, false);
        ProjectileBase.DespawnAll(); bot.ResetBrain();
    }

    private static void VerifyTelegraphAvoidance(BotPlayerInput bot, MatchController match)
    {
        ProjectileBase.DespawnAll();
        var stats = new WeaponStats(2, 12, 1, .5f, 12, Color.white);
        var laser = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(match.PlayerOne, new Vector2(-5, 0), Vector2.right, stats);
        laser.Configure(new BarrageStep { shape = BarrageShape.Line, dealsDamage = false, duration = 1 });
        Check(laser.GetComponents<Collider2D>().All(c => !c.enabled), "Test uses non-colliding laser warning");
        var escape = Avoid(bot, Vector2.zero, Vector2.right, true);
        Check(Mathf.Abs(escape.y) > .7f, "Laser escape changes angle perpendicular to beam");
        Avoid(bot, new Vector2(0, 3), Vector2.right, false);
        ProjectileBase.DespawnAll(); bot.ResetBrain();
        var bomb = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(match.PlayerOne, Vector2.zero, Vector2.right, stats);
        bomb.Configure(new BarrageStep { shape = BarrageShape.Circle, dealsDamage = false, dimensions = new Vector2(3, 3), duration = 1 });
        Check(bomb.GetComponents<Collider2D>().All(c => !c.enabled), "Test uses non-colliding bomb warning");
        escape = Avoid(bot, new Vector2(2.8f, 0), Vector2.left, true);
        Check(escape.x > .7f, "Bomb escape overrides approach and leaves blast radius");
        float before = bomb.DangerClearance(new Vector2(2.8f, 0), match.PlayerTwo.BodyRadius);
        Check(bomb.DangerClearance(new Vector2(2.8f, 0) + escape * 2, match.PlayerTwo.BodyRadius) > before + 1, "Escape increases clearance");
        bomb.transform.position = new Vector2(7, 0); bot.ResetBrain();
        escape = Avoid(bot, new Vector2(8, 0), Vector2.right, true);
        Check(escape.x < .3f, "Bomb escape does not choose arena wall");
        ProjectileBase.DespawnAll(); bot.ResetBrain();
        var friendly = ProjectileBase.SpawnBuiltin<BarrageShapeProjectile>(match.PlayerTwo, Vector2.zero, Vector2.right, stats);
        friendly.Configure(new BarrageStep { shape = BarrageShape.Circle, dealsDamage = false, dimensions = new Vector2(3, 3), duration = 1 });
        Avoid(bot, Vector2.zero, Vector2.right, false);
        ProjectileBase.DespawnAll();
        UnityEngine.Random.InitState(41);
        var movements = new System.Collections.Generic.List<Vector2>();
        for (int i = 0; i < 20; i++)
        {
            Set(bot, "strafeUntil", Time.time - 1);
            typeof(BotPlayerInput).GetMethod("Think", Flags).Invoke(bot, new object[] { (Vector2)match.PlayerTwo.transform.position });
            movements.Add(Field<PlayerCommand>(bot, "command").Move);
        }
        Check(movements.Any(m => m.magnitude < .3f) && movements.Any(m => m.magnitude > .7f), "Feints vary pace with short pauses");
        Check(movements.Any(m => m.y > .2f) && movements.Any(m => m.y < -.2f), "Feints reverse lateral direction");
        bot.ResetBrain();
    }
}
