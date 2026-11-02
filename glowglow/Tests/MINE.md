# Mine validation

Mine is the `mine` catalog card (common, cooldown 4 seconds). It is placed at the
owner's feet, with a 0.34-unit contact radius and a 1.5-unit explosion radius.
An opponent stepping on it, its 20-second scaled-time fuse, or three enemy attack
hits detonates it. Health has no on-screen indicator. Friendly attacks pass through.
Each persistent attack counts once per mine deployment; pooled deployments have
distinct IDs. Electric Pulse affects either player's mines without damaging them;
the pull upgrade also applies. Pulse movement is clamped to the arena.

The host runs all collision, health, movement and explosion logic. Guests reuse
the existing compact sprite protocol: two sprite records per mine and no separate
health messages. Body/core sprites are prepared assets with a shared neon shader,
and the deck icon is baked only by `MineAuthoring.BakeIcon` in the editor.

Run `GraphicsUpgradeChecks.RunBatch` in an isolated Unity project for combat and
presentation regression checks. Copy only `Tests/MineSmokeRunner.cs` into that
project's `Assets`, along with the current game source/assets, and execute
`MineSmokeRunner.Begin` in Unity batch mode. It exercises real physics and a real
20-second fuse, verifies guest snapshot/removal behavior, and records load with
12 mines and 350 bullets. Output: `mine-smoke-result.txt`, `mine-arena.png`.
Remove this copied runner and its meta before building a player.

For two-process load validation, build with `OnlineSetup.BuildTestBatch` and run:

```powershell
./Tests/RunOnlineSmoke.ps1 -PlayerPath <built GlowGlow.exe> -Latency @(0,100) -MineStress
```

The opt-in development probe adds 12 mines across three 350-bullet waves and checks
bounded replica creation, bandwidth and complete cleanup on both peers.

Validated 2026-10-12 with Unity 6000.0.68f1:

- Existing combat regression: 899 assertions; graphics/protocol checks passed.
- Real arena: all mine contact, three-hit, repeated-contact, pooling, pulse,
  arena-boundary, 20-second expiry and guest removal checks passed.
- Two-process 12-mine + 350-bullet load passed at configured 0/100 ms delay.
  Host mean 2.01/2.02 ms, maximum 22.59/33.82 ms; guest mean 2.00/2.02 ms,
  maximum 3.74/19.99 ms. Peak visual traffic 62,472/65,424 bytes/s;
  both peers returned to their six baseline visuals.
- The separate editor screenshot/load run includes a 480.73 ms frame containing
  synchronous capture/cold spawning. It is not a steady gameplay benchmark; the
  standalone two-process numbers above do not perform screenshot capture.

Saved reports and screenshot: `Logs/Mine/`. Tested standalone player:
`Builds/GlowGlow-Mine-20261012/GlowGlow.exe`. Actual Steam/internet latency is not
represented by the local two-process test.
