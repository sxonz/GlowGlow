# Classic online multiplayer

## Entry point

Use **Title → Multiplayer → Classic**. The existing Classic button opens the connection panel.
There is no separate OnlineArena game. Both peers load the original `Arena` scene.

Each player uses their saved eight-card deck. The host validates both decks and sends the draft seeds.
Both players make the original three opening choices (new weapons or upgrades).
Combat begins after both submitted drafts are reconstructed and accepted.
The normal weapon bar, weapon patterns, effects, cooldowns, three-hit defeat, match clock,
and result screen are used. Press R on both peers to agree to a rematch; Escape leaves the session.

## Local test on one PC

Build a development player using `OnlineSetup.BuildTestBatch` (or build Title and Arena in that order).
Output: `Builds/ClassicOnline/GlowGlow.exe`.

1. Run two copies, or run Unity Play Mode plus one built player.
2. Select Multiplayer → Classic in both.
3. Click **로컬 호스트** in one and **로컬 참가** in the other.
4. Make three draft selections in each window.
5. Each window controls its own player using WASD, Space, mouse, and 1–3 / wheel / weapon-bar clicks.
   The HUD indicates **나 P1** or **나 P2**.
6. Check selected weapons, upgrades, cooldowns, warning shapes, shields, damage, results, rematch, and leaving.

These local buttons are available only in the editor and development builds.
Local tests do not need Steam or a registered Steam app.

## Steam connection

Use two PCs with separate Steam accounts. In the Classic connection panel,
one player creates a Steam room and copies its room code; the other enters that code and joins.
The room is friends-only, so add each other as Steam friends first. Copy the entire
Builds/ClassicOnline folder to the other PC, not just the executable. The host's connection
panel remains visible while waiting for the guest so the room code can be clicked and copied.
Both must run the same build. Development uses App ID 480 when no app ID is configured.
The development build includes steam_appid.txt containing 480 next to the executable.
Only lobby members are accepted into that host connection.

Steam P2P/internet play has not yet been verified with two real accounts.
A release must use the game's own Steam App ID. Release builds reject development App ID 480.

## Current architecture and limits

FishNet carries session messages over Tugboat locally or FishySteamworks for Steam P2P.
The host runs the **existing PlayerCombatant, MatchController, WeaponRuntime and ProjectileBase code**.
The guest sends input and renders authoritative player/weapon state and the original combat visuals.
The host supplies hit counts, shields, cooldowns, time and winner; a client cannot submit hit counts or positions.
The guest's weapon bar binds to P2.

This is a working Classic integration baseline, **not a release-ready latency/fairness implementation**.
The guest currently waits for host feedback: there is no Classic client prediction or rollback.
The previous sandbox's prediction tests do not establish Classic movement or hit fairness.
Visual snapshots run at 30 Hz and include the active dynamic renderers; this is not yet an optimized
per-weapon replication protocol. Long cosmetic trails are resampled to at most 64 points.
Input is reliable and ordered, so isolated button presses survive packet loss, but loss can add delay.
A slow/stalled input stream stops guest movement after 250 ms; after five seconds the match is interrupted.
No host migration is implemented.

Before release: implement and validate Classic-specific prediction/reconciliation and a fair latency policy;
measure bandwidth with dense weapon combinations; test actual Steam connections, disconnects and long sessions.
A simulation delay test only establishes synchronization under that test configuration, not fairness.

## Automated checks

`Tests/RunOnlineSmoke.ps1` runs two **actual Classic** players using development flags
`-classic-host/-classic-client -classic-smoke`. It exercises seeded drafts including an upgrade,
real equipped weapons, movement, cooldowns, real damage, agreed results and a second match.
It writes results, runtime logs and screenshots to `Logs/ClassicOnlineSmoke`.
The runner checks both peers' result and winner, and rejects runtime exceptions.
Latency values configure FishNet's simulator; the actual RTT appears in the HUD.
Automated draft choices bypass only clicking the existing draft screen.

`CombatUpgradeChecks.RunBatch` retains the existing offline weapon and upgrade regression suite.

Obsolete circle/bullet sandbox source was archived under ignored `Temp/LegacyOnlineSandbox`.
Its old `Builds/OnlineTest` player and old `Logs/OnlineSmoke` results are not Classic validation.


## Validation record — 2026-09-14

- Final build: Unity 6000.0.68f1, Title + original Arena, Windows development player.
- Two-process Classic: both peers passed two rounds and agreed on winners at configured 0 / 50 / 100 ms delay; positive-delay runs also simulated 2% unreliable packet loss. Runtime exception checks passed.
- Existing combat and upgrade regression: 637 assertions passed.
- Seeded network draft reconstruction: 256 drafts passed, including mixed manual and timeout selections.
- Final batch run prefixes: 20260914-204602-690, 20260914-204625-061, 20260914-204650-232 under Logs/ClassicOnlineSmoke.
- Normal player mode (-RenderWindow, launched hidden) also passed both rounds in run 20260914-204747-950. Native screen captures were black in both batch and hidden normal runs on this host. Offscreen camera captures show replicated combat visuals but can omit cached static UI geometry, so full-screen visual parity still requires a visible manual run.
- These results do not establish Steam internet connectivity, optimized bandwidth, input prediction, or release-level hit fairness.

