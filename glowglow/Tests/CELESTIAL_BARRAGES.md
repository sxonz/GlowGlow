# Celestial base barrages

The four cards are registered in `Assets/Settings/Deck Catalog.asset`, so the
existing deck collection, training selection, preview, draft, and online catalog
can use them. Card names are English and descriptions are Korean. Upgrades are intentionally
undefined; no upgrade assets or effects were added.

Card rarities: Firefly Convergence — Common; Twin Satellites — Epic;
Blossom Burst — Rare; Constellation Ignition — Legendary. The editor installer
preserves this mapping. Epic retains the existing purple tier's serialized value 2.

Muzzle-origin correction (2026-10-22): all four descriptions and their editor
installer defaults are Korean. Constellation Ignition uses the firing muzzle
instead of the cursor as its first star. `CelestialBarrageSmokeRunner` passed
727 checks, including all three routes at eight aim angles from the center and
two arena corners, cursor-distance independence, movement after firing, pooled
reuse, prepared-figure/collision alignment, the actual deck preview, and existing
damage/replication coverage. All three completed figures were visually checked.
Warmed trace animation, reveal animation and outline collision allocated zero
bytes. The eight-cast local Editor fixture peaked at 117 projectiles, 8.35 ms
mean / 12.38 ms maximum frame time at the 120 FPS cap, and 3,272 capture bytes.
Network coverage remains an in-process host/guest fixture. Report:
`Builds/ConstellationValidation/ValidationOutput/celestial-result.txt`; log:
`Builds/constellation-muzzle-smoke.log`.

The settings window now includes **MY BARRAGE OPACITY** (0% hidden, 100% visible,
100% by default). The saved preference affects only the local player's projectile
bodies, lines, trails, halos, and attack effects. Damage, warning timing, original
colors, and the opponent's visibility remain unchanged. Ownership is assigned on
spawn/reuse/reflection; opacity changes update a shader global without scanning
projectiles each frame. Network visuals carry the owner in unused sprite-byte
bits, preserving packet sizes. Both clients require protocol `glowglow-classic-4`;
ranked server source defaults were updated, but the live server was not deployed.

Validation (2026-10-21): `FireflyOpacitySmokeRunner` passed 123 checks, including
caster absorption after movement, swept absorption during a dash, unchanged
damage at 0% opacity, actual pixels in both themes, local/guest ownership, pooled
reuse, clipping, trails, saved settings, and codec round trips. Existing celestial
regression passed 637 checks. Eight simultaneous casts peaked at 103 projectiles,
8.36 ms mean / 16.60 ms maximum frame time at 120 FPS, and 3,184 capture bytes.
Warmed homing and visibility assignment allocated zero bytes. Backend tests: 21
passed. Network checks used an in-process host/guest fixture, not two Steam PCs.

| Card | Base behavior | Cooldown |
| --- | --- | --- |
| Firefly Convergence | Nine lights start beyond the active camera and continuously home toward their caster, swaying less near arrival and being absorbed on body contact. Enemies along the approach still take damage. Eight-second maximum lifetime. | 8.5s |
| Twin Satellites | Two opposite lights follow a fixed 1.6-unit circular orbit around the aimed point, completing one revolution every 2.4 seconds for 4.8 seconds. After a 0.7-second warning, each emits six evenly spaced three-shot fans. | 8s |
| Blossom Burst | Three buds travel for 0.85 seconds, stop and open, then release eight petals each at 1.55 seconds. | 5s |
| Constellation Ignition | The first star and comet start on the spawn frame, with no opening delay. Only the next segment is warned. Travel accelerates from 0.11 to 0.05 seconds per segment (twice the original speed). Each segment burns for 0.10 seconds, followed by 0.4 seconds of harmless afterglow. The traced path remains faintly visible. Completion reveals a hollow figure for 1.25 seconds: a 0.16-second reveal, damaging outline until 0.85 seconds, then 0.4 seconds of harmless fading light. | 6s |

Hydra, Eridanus, and Draco use stylized, authored silhouettes rather than a
randomly generated zigzag. The astronomical references are the
[Hydra](https://iauarchive.eso.org/static/public/constellations/pdf/HYA.pdf),
[Eridanus](https://iauarchive.eso.org/static/public/constellations/pdf/ERI.pdf),
and [Draco](https://iauarchive.eso.org/static/public/constellations/pdf/DRA.pdf)
IAU charts. The game shapes simplify those figures; they are not star-coordinate
projections. The first star and comet start exactly at the firing muzzle. The
authored route and prepared figure share that anchor and aim rotation; cursor
distance never relocates them. Arena-sized scaling is retained, but the route
is not shifted away from the muzzle to fit inside walls. Orientation, placement,
and the selected silhouette remain fixed after firing. The comet and its two
echoes travel along that fixed route.
Only the next line and its endpoint are revealed; future star positions remain
hidden. When the last star is reached, the approved abstract sea serpent,
celestial river, or dragon appears. Its exterior consists of crossing cyan and
lavender filaments; its interior is transparent. The actual traversed path and
arrived stars supply the interior light, using two pooled line renderers and
the existing star renderers. There is no painted star map or interior fill.
The figure uses one reusable sprite renderer and aligns with the trace at every
rotation and arena fit.
Each star pulses on arrival; revisiting a closed head reuses the same star.
The collider grows from the segment's start to its visible front. The undrawn
remainder stays harmless, and the trailing afterglow disables both collision and
bot danger. A stalled frame skips expired damage instead of replaying it.

The completed figure's luminous outer strands now damage opponents. Its
prepared distance field follows the image rather than filling the enclosed
body or its bounding rectangle. Soft halo pixels are excluded. Contacts are
swept against scene-local player circles after movement, honoring body radius,
owner exclusion, disabled hitboxes and invulnerability. Crossing multiple
strands deals at most one hit per target per reveal. The 0.16-second emergence
warns before activation, and the final fade has no damage or bot danger.
Bots use the same distance field to choose an escape through open space.

Runtime visuals load the prepared Circle, Square, SoftGlow, and three
Constellation figure assets. The figures are baked by
**GlowGlow > Bake Constellation Figures** (`ConstellationFigureAuthoring.Bake`),
which does not reset card tuning. All figure textures are 768 by 448 pixels,
about 3.94 MiB total in RGBA32, with no mipmaps or runtime texture creation.
The approved hollow source artwork lives in `Assets/Editor/ConstellationArt/`
and is excluded from player builds. The editor bake preserves transparency,
reduces it to gameplay resolution, and writes three matching collision fields.
It explicitly imports a single centered full-canvas sprite, replacing stale
automatic slices so the visible image and collision coordinates stay identical.
Each field is 416 by 256 quantized distance samples, with an empty border,
occupying 212,996 bytes (about 0.61 MiB total). They are loaded and cached once;
no runtime texture reads, image generation or contour construction are needed.
The built-in image generator was used to edit the earlier hollow sprites into
the approved crossing-filament shapes. Earlier design references included
[the celestial-serpent presentation](https://www.wow-petopia.com/npc.php?id=60410)
and Chandra's [Draco](https://chandra.si.edu/photo/constellations/draco.html)
and [Eridanus](https://chandra.si.edu/photo/constellations/eridanus.html) atlas
illustrations. Final generation prompts, reference purposes, and asset paths
are recorded in `Tests/CONSTELLATION_ART.json`.
The existing network presentation uses three sprite IDs and sends a single
ordinary sprite record per figure, plus existing line records for actual
afterglow paths and ordinary sprite records for arrived stars. Its packet
format and capture budget are unchanged. Card
icons are baked by `CelestialBarrageAuthoring.Install`, available from
**GlowGlow > Install Celestial Barrages**. That command is idempotent and
reapplies the base tuning above. No runtime textures or sprites are generated.
All movement projectiles and constellation segments reuse existing pools.
Scene-local swept collision prevents fast petals from skipping players.
Constellation segments use the existing beam replication and bot telegraph
avoidance path; decorative star nodes do not have damage colliders.

## Validation

Create an isolated Unity 6000.0.68f1 project (for example,
`Builds/ConstellationValidation`) with copies of Assets/Scripts,
Assets/Resources, Assets/Settings, Assets/UI, Packages, and ProjectSettings.
Copy `Assets/Editor/CelestialBarrageAuthoring.cs` and
`Assets/Editor/ConstellationFigureAuthoring.cs` into its Assets/Editor.
Also copy `Assets/Editor/ConstellationArt/` to reproduce the prepared artwork.
If recreating the prepared figures, run
`-batchmode -nographics -executeMethod ConstellationFigureAuthoring.BakeBatch`.
`CelestialBarrageAuthoring.InstallBatch` also installs all four cards, so only
use it when intentionally restoring the base tuning.
Copy `Tests/CelestialBarrageSmokeRunner.cs` into the isolated Assets folder,
with no other auto-start smoke runners present. Then run
`-batchmode -executeMethod CelestialBarrageSmokeRunner.Begin`.
Omit `-quit` and `-nographics` for this Play Mode run.

The runner writes `ValidationOutput/celestial-result.txt` and screenshots of
each pattern. Checks cover catalog/icon registration, absence of upgrades,
offscreen ingress, pause, zero-allocation warmed firefly movement, pool reuse,
satellite pairing and fans, stopped buds and 24 petals, all three constellations,
immediate first-star ignition, single-segment warning and ignition order,
exactly doubled travel speed, growing damage geometry,
pause, harmless future segments/afterglow, zero-allocation warmed constellation
and completed-figure animation, prepared-figure alignment, delayed reveal/fade,
figure replication, ordinary-beam reuse, frame-hitch catch-up, real damage and owner exclusion, swept hits,
bounded cleanup, simultaneous casts with the existing network capture budget,
and the real deck preview's private physics scene.
Outline checks additionally read the prepared pixels independently, find
bright and hollow probes, exercise real damage, immunity and disabled hitboxes,
verify once-per-reveal hits and sweeps across empty endpoints, and measure
warmed collision allocations.

The initial simultaneous-ignition version was validated on 2026-10-20 with
Unity 6000.0.68f1. That run passed 227
assertions (the count includes repeated checks while sampling all three random
constellations). Warmed firefly movement, including swept queries, allocated
0 bytes across 100 updates. Eight simultaneous casts peaked at 136 active
projectiles; the 120-FPS-capped Editor run observed 8.54 ms mean / 57.55 ms
maximum frame time and 3,284 maximum capture bytes including packet overhead.
These are local Editor observations, not a standalone-build performance
guarantee. Network coverage uses the real capture/replica code in one process;
it does not simulate transport latency or test two remote machines.

The sequential-ignition runner writes its current report and captures to
`Builds/ConstellationValidation/ValidationOutput/`.

The sequential version passed 137 Play Mode checks on 2026-10-20. All three
routes were captured and visually inspected. Warmed constellation animation
allocated 0 bytes over 100 updates for each route. Eight concurrent casts
peaked at 134 projectiles; the same 120-FPS-capped Editor fixture measured
8.43 ms mean / 30.99 ms maximum frame time and 3,264 maximum capture bytes
including packet overhead. Final source, card assets, and runner hashes match
the validation copy. These measurements retain the local Editor and
single-process network limitations described above.

The faster, single-warning version with cosmic completion figures passed
351 Play Mode checks on 2026-10-20. Warning, tracing, and completed-figure
captures were checked for all three routes. Both warmed tracing and completed
figure animation allocated 0 bytes over 100 updates. All three prepared
figures reached the real remote presentation with their artwork and opacity
intact. Eight concurrent casts peaked at 122 projectiles; the local
120-FPS-capped Editor fixture measured 8.73 ms mean / 39.74 ms maximum frame
time and 3,264 maximum capture bytes including overhead. The load window
includes the completed figures. These figures are local observations rather
than a standalone performance guarantee. The new captures use the suffix
`-complete.png` in the same validation output folder.

The immediate-start version with the repainted figures passed 663 Play Mode
checks on 2026-10-20 (including repeated random selections to cover all three
routes). The first star and comet were visible on the spawn frame in every
selection, with the opening delay removed. All three final in-game figures
were visually inspected, and their exact inner star maps remained aligned.
Warmed tracing and completion animation still allocated 0 bytes over 100
updates. The eight-cast fixture peaked at 118 projectiles and observed
8.36 ms mean / 16.30 ms maximum frame time at the 120 FPS cap, with 3,264
maximum capture bytes including overhead. These remain local Editor and
single-process network observations. The tested source, prepared images,
card assets, and smoke runner match the workspace copies.

The approved hollow crossing-filament version with damaging outlines passed
394 Play Mode checks on 2026-10-20, including repeated route selections.
All three full-canvas figures were visually inspected after removing obsolete
sprite slices. Prepared-pixel probes, real outline hits, safe hollow probes,
owner/immunity/disabled-hitbox filtering, single-hit registration, swept
crossings, fade expiry and pool resets passed. Warmed trace animation,
completion animation and outline collision each allocated zero bytes across
100 updates. Eight concurrent casts peaked at 118 projectiles; the capped
local Editor fixture observed 8.38 ms mean / 16.79 ms maximum frame time and
3,312 maximum capture bytes including overhead. The existing capture budget
was unchanged. This retains the local Editor and single-process network
limitations above. Logs: `Builds/constellation-hollow-full-bake.log` and
`Builds/constellation-hollow-full-smoke.log`.
