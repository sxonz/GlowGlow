# Graphics themes

Title → Settings → 그래픽 테마 offers 클래식 · 이전 and 네온 · 현재.
The default is Neon; selection is saved locally as `GraphicsTheme.v1`.
Neon keeps the player's central white core removed.

Classic restores flat player/projectile shading, the original arena grid/border,
soft sprite glow and simple prism trails. It suppresses the added impact particles,
ready flash, laser arcs and solar surface animation. Existing gameplay warnings,
attack shapes and arena clipping remain visible.

Theme selection changes a global shader value, the shared cosmetic glow material,
and arena renderer visibility. It does not modify colliders, object scales, weapon
stats, health, timers, RNG or networking messages. Material identities and compact
visual style IDs are retained so each peer can choose its own presentation.
No per-frame scene search or runtime sprite creation is involved.

Validation in a disposable Unity project:

1. Execute `GraphicsThemeChecks.RunBatch` for Neon graphics/protocol checks and
   the existing 899 combat assertions under each theme.
2. Copy only `GraphicsThemeSmokeRunner.cs` into the disposable `Assets` folder,
   then execute `GraphicsThemeSmokeRunner.Begin` with graphics enabled.
   It clicks the real settings buttons, verifies saving/navigation/scene changes,
   renders identical arena states under both themes, compares collider states
   through repeated switching and compares the exact serialized network bytes.
   It also applies each theme's snapshots while the other theme is selected.
3. Remove the copied runner and its meta before building the game.

The checks restore the pre-test theme preference. Play-mode output is
`theme-smoke-result.txt`, `theme-settings.png`, `theme-classic.png`, `theme-neon.png`.

Validated 2026-10-15 in Unity 6000.0.68f1. Settings clicks, persistence, keyboard
navigation, scene transition, eight live theme switches with identical colliders,
unchanged gameplay values/RNG, byte-identical visual snapshots and cross-theme
guest rendering passed. Saved reports/screenshots are in `Logs/GraphicsThemes/`.
At a 120 FPS cap, 350 bullets + 12 mines measured Classic mean/p95/max
8.34/8.84/11.01 ms and Neon 8.33/8.70/10.84 ms in the editor after warmup.

The rendered-snapshot check also exposed an existing capture issue: Unity adds
sprite texture/color property blocks after rendering, which were mistaken for
arena clipping. Capture now accepts only a nonempty clipping rectangle. This
changes presentation metadata correctness, not collision geometry or wire format.
