using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runs the real weapon in a private physics scene and renders it into the deck UI.</summary>
public sealed class BarragePreview : MonoBehaviour
{
    [SerializeField] private RawImage image;
    private WeaponDefinition weapon;
    private Scene scene;
    private Camera cameraView;
    private Camera titleCamera;
    private int titleMask;
    private RenderTexture texture;
    private PlayerCombatant shooter;
    private PlayerCombatant target;
    private float restartAt;
    private float firedAt;
    private float simulateAccumulator;
    private bool overdrivePreview;
    private bool bouncerPreview;
    private float demoStart;
    private float nextDemoShot;
    private const int PreviewLayer = 31;

    public void Configure(RawImage view) => image = view;

    public void Select(WeaponDefinition definition)
    {
        weapon = definition;
        if (Application.isPlaying && isActiveAndEnabled) Restart();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (weapon != null) Restart();
    }

    private PlayerCombatant CreatePlayer(string name, Color color)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        var player = go.AddComponent<PlayerCombatant>();
        player.ConfigurePreview(weapon);
        player.GetComponent<SpriteRenderer>().color = color;
        player.GetComponent<SpriteRenderer>().sortingOrder = 5;
        player.transform.localScale = Vector3.one * .65f;
        foreach (var child in go.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
        return player;
    }

    private void CreateWorld()
    {
        scene = SceneManager.CreateScene("Barrage Preview " + GetInstanceID(), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var go = new GameObject("Preview Camera");
        SceneManager.MoveGameObjectToScene(go, scene);
        cameraView = go.AddComponent<Camera>();
        cameraView.orthographic = true;
        cameraView.orthographicSize = 3.7f;
        cameraView.transform.position = new Vector3(0, 0, -10);
        cameraView.clearFlags = CameraClearFlags.SolidColor;
        cameraView.backgroundColor = new Color(.025f, .012f, .07f);
        cameraView.cullingMask = 1 << PreviewLayer;
        cameraView.allowHDR = false;
        texture = new RenderTexture(768, 320, 16) { name = "Deck Barrage Preview" };
        texture.Create();
        cameraView.targetTexture = texture;
        image.texture = texture;
        image.color = Color.white;
        titleCamera = Camera.main;
        if (titleCamera != null) { titleMask = titleCamera.cullingMask; titleCamera.cullingMask &= ~(1 << PreviewLayer); }
        shooter = CreatePlayer("Preview Shooter", new Color(1, .25f, .8f));
        target = CreatePlayer("Preview Target", new Color(.55f, .4f, 1));
    }

    public void Restart()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || weapon == null || image == null) return;
        if (shooter == null) CreateWorld();
        ProjectileBase.DespawnOwnedBy(shooter);
        ProjectileBase.DespawnOwnedBy(target);
        bool pulse = System.Array.Exists(weapon.steps ?? System.Array.Empty<BarrageStep>(), s => s != null && s.shape == BarrageShape.ElectricPulse);
        bool orb = System.Array.Exists(weapon.steps ?? System.Array.Empty<BarrageStep>(), s => s != null && s.shape == BarrageShape.OrbitOrb);
        overdrivePreview = System.Array.Exists(weapon.steps ?? System.Array.Empty<BarrageStep>(), s => s != null && s.shape == BarrageShape.Overdrive);
        bouncerPreview = System.Array.Exists(weapon.steps ?? System.Array.Empty<BarrageStep>(), s => s != null && s.shape == BarrageShape.Bouncer);
        shooter.ConfigurePreview(weapon);
        // Keep ranged attacks well separated; the radial pulse demonstrates its outer reach.
        shooter.ResetCombatant(new Vector2(orb ? -.7f : pulse ? -1.4f : -4, 0));
        target.ResetCombatant(new Vector2(orb ? .7f : pulse ? 1.4f : 4, 0));
        // Give reset invulnerability time to expire before firing the demonstration.
        firedAt = Time.time + .6f;
        demoStart = firedAt;
        nextDemoShot = firedAt + .8f;
        float length = overdrivePreview || orb ? 5 : Mathf.Max(1.8f, weapon.cooldown);
        foreach (var step in weapon.steps ?? System.Array.Empty<BarrageStep>())
            if (step != null) length = Mathf.Max(length, step.delay + step.duration);
        restartAt = firedAt + length + .7f;
        simulateAccumulator = 0;
    }

    private void Update()
    {
        if (shooter == null || weapon == null) return;
        if (Time.time >= restartAt) { Restart(); return; }
        if (Time.time >= firedAt)
        {
            // Fire diagonally so Bouncer demonstrates reflection before hitting the target.
            Vector2 direction = bouncerPreview ? new Vector2(1, .65f).normalized : Vector2.right;
            shooter.CurrentWeapon.Fire(shooter, (Vector2)shooter.transform.position + direction * .56f, direction, target.transform.position);
            firedAt = float.PositiveInfinity;
        }
        // A moving target makes the short slow visible.
        target.MovePreview(Vector2.up * Mathf.Cos(Time.time * 2), Time.deltaTime * .18f);
        if (overdrivePreview && Time.time >= demoStart)
        {
            float phase = (Time.time - demoStart) % 2.4f;
            Vector2 input = phase < .8f ? Vector2.right : phase < 1.3f ? Vector2.zero : Vector2.left;
            shooter.MovePreview(input, Time.deltaTime * 5.2f);
            if (Time.time >= nextDemoShot && shooter.IsOverdriving)
            {
                Vector2 direction = ((Vector2)shooter.transform.position - (Vector2)target.transform.position).normalized;
                var stats = new WeaponStats(1, 8, 2, .13f, 12, new Color(.65f, .5f, 1));
                ProjectileBase.Spawn(null, target, (Vector2)target.transform.position + direction * .5f, direction, stats);
                nextDemoShot = Time.time + 1;
            }
        }
    }

    private void LateUpdate()
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        simulateAccumulator += Time.deltaTime;
        Physics2D.SyncTransforms();
        while (simulateAccumulator >= Time.fixedDeltaTime)
        {
            scene.GetPhysicsScene2D().Simulate(Time.fixedDeltaTime);
            simulateAccumulator -= Time.fixedDeltaTime;
        }
    }

    private void OnDisable()
    {
        if (shooter != null) ProjectileBase.DespawnOwnedBy(shooter);
        if (target != null) ProjectileBase.DespawnOwnedBy(target);
        if (titleCamera != null) titleCamera.cullingMask = titleMask;
        if (image != null) { image.texture = null; image.color = new Color(.025f, .012f, .07f); }
        if (cameraView != null) { cameraView.enabled = false; cameraView.targetTexture = null; }
        if (texture != null) { texture.Release(); Destroy(texture); }
        if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        shooter = target = null;
        texture = null;
    }
}
