# Weapon icons

All 19 catalog weapons use distinct authored silhouettes in
`Assets/UI/BarrageIcons/WeaponIcons.png`. Each sprite occupies its entire centered
192×192 cell, with a bright core, colored rim, restrained baked glow and a
transparent gutter. Colors follow the weapon definitions at bake time. Original
vector contours are in `Assets/Editor/WeaponIconAuthoring.cs`; no external art
or runtime texture/sprite generation is involved.

The glyphs depict the attack: a focused beam, five-shot fan, ricochet, returning
blade, four linked orbital stages, pressure plate, converging lights, paired
satellites, opening flower and sequential star route. Sharing the texture does
not share an icon or silhouette between weapons.

Use **GlowGlow → Bake Weapon Icons** in Unity to regenerate the atlas and update
the catalog weapon references. Sprite identities are preserved by name across
rebakes. The existing basic/celestial/mine authoring tools now use this atlas;
`Tests/BakePrismIcon.ps1` is a compatibility entry point for the same bake.
Legacy loose PNGs remain available for old references but are not used by the
19 weapon definitions.

The atlas is a single 1024×1024 RGBA32 texture (4 MiB of texels), with mipmaps,
lossy compression and runtime CPU readability disabled. Glow needs no extra
UI objects, materials, render passes, shaders, animation or network data.
Collection, draft, bot summary, HUD and upgrade-core Images display the prepared
colors with a white tint; monochrome fallback and custom upgrade icons retain
their tint behavior. HUD updates remain cached.

`WeaponIconSmokeRunner` runs in the isolated validation project. It checks
unique geometry and references, full sprite bounds, bright opaque pixels,
translucent halos, transparent gutters, stable sprite IDs after rebaking,
texture settings, all collection pages, real draft and bot-summary Images,
every HUD/upgrade icon and warmed HUD allocations. Its contact sheet uses the
actual Unity UI Image renderer at 90, 48 and 26 pixels. Captures and reports are
written to `Builds/ConstellationValidation/ValidationOutput/weapon-icons-*`.

Validated on 2026-10-22 with Unity 6000.0.68f1: 142 checks passed. All four
collection pages, the opening draft/bot summary and the 90/48/26px contact sheet
were visually inspected. Warmed HUD refresh allocated zero bytes over 100
updates. Unity's Editor profiler reported 8,389,504 bytes for the shared atlas
(including Editor storage); the uncompressed GPU texel payload is 4 MiB.
The 19 sprite identities survived rebaking. `Tests/weapon-icons-preview.png`
contains the rendered contact sheet. Log: `Builds/weapon-icons-smoke.log`.
