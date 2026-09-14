# Barrage smoke test

## Meteor Dive (temporary name)

Copy current Assets/Scripts, Assets/Resources, Assets/Settings, their metadata, and the
project's package dependencies into an isolated Unity project. Copy only
`MeteorDiveSmokeRunner.cs` into that project's Assets and run
`-batchmode -executeMethod MeteorDiveSmokeRunner.Begin` without `-quit` or `-nographics`.
The runner checks real-time disappearance, target changes, pause, descent, actual
Physics2D explosion damage, ballistic debris, and reset cleanup. It writes
`meteor-smoke-result.txt` and PNGs of the warning, descent, and eruption.
`CombatUpgradeChecks` also covers catalog registration, deck compatibility, firing
lock, pooling, cancellation, and gravity at 30/60/120 FPS.

## Bot match

In Singleplayer → 봇 대전, enter a rating from 200 to 3000 (default 1200),
drag the integer slider, use +/- for one-point steps, or select a preset.
All controls stay synchronized.
The setup displays space-themed bands: 200 우주 먼지, 600 유성, 1200 행성,
1800 항성, 2400 초신성, 2800 블랙홀, and 3000 특이점.
The rating controls reaction interval, aim error, prediction, and avoidance/dash likelihood;
it is not a calibrated competitive Elo score.
The decision skill curve is 97% continuous plus six small tier bonuses of 0.5% each.
These percentages describe the tuning curve, not measured win rates. Every rating point reduces reaction delay, increases straight-bullet prediction
horizon, improves clearance precision, reduces escape commitment, and raises dash probability.
At 200, decisions take 0.95 seconds, aim error reaches 3 world units, firing probability
per decision is 20%, urgent dash probability is 1%, and only 15% of the safe-route
correction is followed. All improve toward the existing maximum-rating performance.
There is no ability switch at 2400. Search directions grow in small integer steps from 8
to 64, with stopping always considered. At 3000, reaction delay reaches zero and urgent
dashes have no intentional random failures. Health, movement speed and cooldown rules remain unchanged.
Both combatants use the normal three-choice opening draft and three-hit match rules.
During the player's draft, the bot's three actual picks appear in a compact bottom
strip over 0.6 seconds, including upgrade names and their target weapons. This adds
no extra wait or confirmation. The strip stays visible through the three rounds.
The bot uses normal movement, weapons, cooldowns, and hitboxes. R after the result
starts another match at the same rating; ESC returns to Title.
Bots read visible beam/bomb warnings even though telegraph colliders are disabled.
They compare escape paths against overlapping attack areas and arena walls, prioritize
leaving warnings over approaching, and dash when time is short. Movement mixes
brief pauses, lateral reversals, and diagonal feints. Rating still gates reaction time.

For validation, copy the current Assets/Packages/ProjectSettings to an isolated project
and copy only `BotMatchSmokeRunner.cs` into its Assets (remove other smoke runners).
Run Unity with `-batchmode -executeMethod BotMatchSmokeRunner.Begin`, without `-quit`
or `-nographics`. It writes `bot-result.txt` and `bot-setup.png`, checking rating input,
draft gating, autonomous movement/fire/hits, difficulty scaling, cooldowns, results,
restart and training-mode isolation. No PlayerPrefs are written by the runner.

## Training ground

Copy Assets, Packages and ProjectSettings into an isolated Unity 6000.0.68f1 project.
Copy only `TrainingSmokeRunner.cs` into that project's Assets and run Unity with
`-batchmode -projectPath <isolated-project> -executeMethod TrainingSmokeRunner.Begin -logFile <log>`.
Omit `-quit` and `-nographics`. The runner exits with 0/1, writes `training-result.txt`
and renders the training UI at 1600×900, 1280×720 and 1200×900.
It checks entry with an invalid deck catalog, every catalog weapon and applicable
upgrade, retained builds, reset cleanup, unlimited hits, moving targets, text overflow,
saved deck preservation, and subsequent classic-mode isolation. It never writes PlayerPrefs.

In-game: Singleplayer → 훈련장. Q/E switches weapons, +/- changes upgrade levels,
기본/최대 clears or maximizes the current weapon, R resets the arena while keeping
upgrades, and ESC returns to Title. Weapon and upgrade changes preserve both combatants'
positions and the moving target's phase while clearing effects and cooldowns.
Settings are retained only within the current visit.

`BarrageSmokeRunner.cs` runs real Play Mode timing and Physics2D checks. It is outside
Assets so it cannot be included in the game or run in the user's editor.

Run in an isolated Unity 6000.0.68f1 project:

1. Copy Assets/Scripts, Assets/Editor, Assets/Settings, ProjectSettings and the project's
   package dependencies into the isolated project.
2. Copy BarrageSmokeRunner.cs into its Assets folder.
3. Start Unity with `-batchmode -nographics -projectPath <isolated-project> -executeMethod BarrageSetup.ValidateBatch -logFile <log>`.
   Do not pass `-quit`: the runner exits with code 0 or 1 after Play Mode checks.
4. Inspect `smoke-result.txt` in the isolated project and `BARRAGE_SMOKE_PASSED` in the log.

The setup creates fresh icons and installs the first four patterns in that isolated
project before entering Play Mode. It does not change PlayerPrefs or open the title scene.
Checks cover eight-card catalog integrity, immediate/delayed spawning, travel, cooldown,
pause, switching weapons mid-sequence, bomb cursor placement/fragment speed,
laser telegraph immunity/actual hits/lifetime, pooled state, reset cancellation,
match-end cancellation and offscreen cleanup.

## Electric Pulse and deck preview

Copy the full Assets tree into an isolated project so that the title scene and fonts are available.
Run `BarrageSetup.BakePreviewBatch` to install the fifth weapon and bake the deck preview.
Then copy `PulsePreviewSmokeRunner.cs` into its Assets folder and run
`-batchmode -executeMethod PulsePreviewSmokeRunner.Begin` with the same project and a log path.
Graphics must be enabled for image validation (omit `-nographics`).
The runner writes `pulse-preview-result.txt`, `preview-world.png`, and `deck-preview.png`.
It checks pulse zero damage/no damage invulnerability/owner filtering, knockback, 75% speed for 0.3 seconds, pause and reset,
private Physics2D scenes, camera output, all eight weapon selections, and scene/texture disposal.

## Overdrive

Run `BarrageSetup.InstallOverdriveBatch` in the isolated project to register the sixth card.
Only one smoke runner may exist under its Assets folder at a time (they start automatically in batch Play Mode).
Replace the pulse preview runner with `OverdriveSmokeRunner.cs` and execute `OverdriveSmokeRunner.Begin`.
The runner checks three shield charges without health spill, zero-damage pulse interaction, 150% speed/coasting,
weapon firing lock, actual rotating spike contacts and repeated-hit cooldown, pause, five-second expiration,
shield-break/reset/match-end restoration, and pool reuse. Outputs: `overdrive-result.txt` and `overdrive.png`.

## Prism Shot

`CombatUpgradeChecks.RunBatch` includes the Rare card's five-second cooldown, staggered group order,
current-muzzle origins, synchronized radius-five formation, helix/orbit paths, smooth scattering and swept damage.
For a real-frame visual check, copy `PrismShotSmokeRunner.cs` into the isolated project's Assets folder
(with no other smoke runners present) and execute `-batchmode -executeMethod PrismShotSmokeRunner.Begin`.
Keep graphics enabled. It verifies actual delayed emission, formation, scattering, wall cleanup and cancellation.
Outputs: `prism-smoke-result.txt`, `prism-helix.png`, `prism-orbit.png`, and `prism-formation.png`.
The upgraded smoke checks eleven shots, muzzle release and actual nearby explosion damage, with `prism-tail.png` and `prism-impact.png` captures.
The formation capture samples the exact 0.8-second position to avoid frame-rate-dependent screenshots.

## Classic online

See [ONLINE_MULTIPLAYER.md](ONLINE_MULTIPLAYER.md) for the existing Classic mode's two-player test, validation scope and remaining latency limitations.
