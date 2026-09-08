# Barrage smoke test

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
