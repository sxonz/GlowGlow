# Classic online multiplayer

## Entry point

Use **Title → Multiplayer → Classic**. The existing Classic button opens the connection panel.
There is no separate OnlineArena game. Both peers load the original `Arena` scene.

Each player uses their saved eight-card deck. The host validates both decks and sends the draft seeds.
Both players make the original three opening choices (new weapons or upgrades).
Combat begins after both submitted drafts are reconstructed and accepted.
The normal weapon bar, weapon patterns, effects, cooldowns, 100-HP defeat, match clock,
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

### Ranked matchmaking

The connection panel now includes **랭크 매칭** and cancellation alongside friend rooms.
It pairs by server-owned Elo (initial 1000, ±100 expanding by 50 every 15 seconds up to ±600),
with matching protocol and region. Ranked results require both authenticated players to agree;
explicitly leaving after combat starts concedes. Ranked rematches require a fresh search.

**App ID 480 development matchmaking is supported.** See `Backend/ranked/README.md` for setup.
The backend needs the test migration, `RANKED_TEST_MODE=true`, a signing secret and function deployment.
Publisher API keys are not required for this development path. Test tokens/data are isolated from production.
The client never stores an authoritative rating locally. Normal combat sends no HTTP to the backend.

### Friend rooms

Use two PCs with separate Steam accounts. In the Classic connection panel,
one player creates a Steam room and copies its room code; the other enters that code and joins.
The room is friends-only, so add each other as Steam friends first. Copy the entire
Builds/ClassicOnline folder to the other PC, not just the executable. The host's connection
panel remains visible while waiting for the guest so the room code can be clicked and copied.
Both must run the same build. Development uses App ID 480 when no app ID is configured.
The development build includes steam_appid.txt containing 480 next to the executable.
Only lobby members are accepted into that host connection.

The user has connected over Steam with another player; automated checks below use local transport.
A release must use the game's own Steam App ID. Release builds reject development App ID 480.

## Current architecture and limits

FishNet carries session messages over Tugboat locally or FishySteamworks for Steam P2P.
The host runs the **existing PlayerCombatant, MatchController, WeaponRuntime and ProjectileBase code**.
The guest sends input and renders authoritative player/weapon state and the original combat visuals.
The host supplies health, shields, cooldowns, time and winner; a client cannot submit health or positions.
The guest's weapon bar binds to P2.

This is a working Classic integration baseline, **not a release-ready latency/fairness implementation**.
The guest currently waits for host feedback: there is no Classic client prediction or rollback.
The previous sandbox's prediction tests do not establish Classic movement or hit fairness.
Player/weapon state runs at up to 30 Hz. Combat visuals are sampled at up to 20 Hz from the players
and active projectiles, without traversing the arena UI or inactive projectile pool.
Each object sample applies independently; missing chunks no longer freeze an entire visual frame.
Reliable removal messages and per-object sequence checks prevent lost or reordered movement packets
from leaving ghost projectiles. Sprite/line objects are pooled on the guest, and ordinary soft glows
are reconstructed locally instead of being transmitted as separate objects.
Sprite samples use 34 bytes (50 with clipping); moving visual samples have a 3000-byte capture budget
and 800-byte packet payload target. Removals bypass that budget. Dense scenes rotate through objects,
so individual update frequency can fall below 20 Hz. This is still renderer replication, not a
per-weapon prediction protocol. Lines are resampled to at most 64 points, trails to 32.
Input is capped at 60 sends/sec with button edges retained between sends. It is reliable and ordered,
so isolated button presses survive packet loss, but loss can add delay.
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
The opt-in player probe allows 180 seconds for two 100-HP matches. The PowerShell
runner allows 200 seconds by default (`-TimeoutSeconds` can raise this limit).
Latency values configure FishNet's simulator; the actual RTT appears in the HUD.
Automated draft choices bypass only clicking the existing draft screen.
After the initial movement probe, both automated players approach through normal
movement input so melee/orbit and shield-heavy random drafts can also finish.

`-Stress` spawns three bursts of 350 actual Classic bullets. It checks that guest visual counts return
to the idle baseline, pooled allocations stabilize, and estimated visual traffic stays bounded.
Reported frame timings are measurements from this PC, not a guarantee for the other player's PC.
`ClassicVisualChecks.Run` also tests missing/reordered samples, reliable removal, late resurrection,
20 bursts of 256 pooled visuals, and compact serialization roundtrips before the development build.
Current 100-HP builds use protocol `glowglow-classic-5`; both peers need the updated build folder.
The ranked function must also accept `glowglow-classic-5` (its default is updated in source;
deploy that function or update an existing `RANKED_PROTOCOL` setting before ranked testing).
Local/friend-room tests do not use that backend.

The graphics upgrade keeps sprite samples at 34 bytes (50 when clipped). Prism ribbons are
rebuilt locally from those samples instead of transmitting up to 32 trail positions. Each peer
caps prism ribbons at 48. Cosmetic flashes, floor light, rings and shards use a prewarmed
128-sprite pool, a 24-event/second budget and a 3-event/frame cap, with no extra network records.
See [GRAPHICS_UPGRADE.md](GRAPHICS_UPGRADE.md) for rendering and load validation.

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

## Visual synchronization validation — 2026-09-15

- Unity 6000.0.68f1 development build: missing/reordered packet and removal regression checks passed;
  20 x 256 visual spawn/removal cycles reused the pool, and sprite/clip/line codec roundtrips passed.
- Two actual Classic processes, three bursts of 350 real bullets: passed at configured 0 and 100 ms
  delay (100 ms run also includes 2% unreliable loss). Both peers reached 356 active visuals including
  the six idle player visuals, then returned to six. No projectile visuals remained.
- The guest created 356 replica objects total across all three bursts. Estimated peak visual traffic
  was 62,400 bytes/sec on the host; this excludes player state/input and transport framing overhead.
- Guest mean frame time was 2.01 ms in both local batch runs; maximums were 8.92 / 5.48 ms respectively.
  These local measurements are not another PC's performance and are not a before/after comparison.
- Passing stress run prefixes: 20260915-111851-889 and 20260915-111917-535 in Logs/ClassicOnlineSmoke.
- Regular Classic regression also passed two rounds on both peers at configured 0 and 100 ms delay:
  movement, real weapons, cooldowns, upgrades, hits, matching winner and rematch. Run prefixes:
  20260915-111958-571 and 20260915-112027-853. No runtime errors were detected in passing runs.
- The tested build was copied to Builds/ClassicOnline; the copied game assembly hash matches.
- Actual Steam transport and perceived motion under dense patterns still need validation on two PCs.

## Ranked integration validation — 2026-09-21

- Backend: 17 tests passed using PGlite PostgreSQL and the Edge Function handler, including Steam
  authentication stubs, forged/expired credentials, RPC permissions, matchmaking windows, nearest
  opponent, 100 requests producing 50 disjoint matches, cancellation and idempotent Elo updates.
- Unity 6000.0.68f1: runtime/editor compilation and the development player build passed; existing
  visual synchronization regression checks passed. Build log: Logs/ranked-build-20260921-final.log.
- Two local Classic players passed both rounds with real movement, weapons, upgrades, hits, agreed
  results and rematch. Run prefix: 20260921-082948-618 under Logs/ClassicOnlineSmoke.
- Tested integration sources match the build snapshot. The player is copied to Builds/ClassicOnline.
- Ranked Steam authentication, hosted Supabase and the full ranked client lifecycle are not verified
  live: no project endpoint or game App ID is configured yet. Rank is intentionally unavailable until
  those deployment settings are supplied. The existing 20260916 ZIP is a separate earlier artifact.

## App ID 480 automatic matchmaking — 2026-09-21

- Development builds now support `developmentTest=true` with Steam App ID 480.
- Test authentication does not request a Steam Web API ticket or require a publisher key.
- The Edge Function must explicitly enable `RANKED_TEST_MODE=true`; test JWT audiences and all test database tables are isolated from production.
- Backend validation: 21 checks pass, including two test accounts through authentication, pairing, lobby publication, start, result agreement and a single Elo award; production tables remain empty.
- Project endpoint is configured in the source settings. Development players also read `RankedSettings.json` next to the executable.
- Live Supabase deployment and two-account Steam verification are separate from local backend tests.

## Live development backend — 2026-09-21

- Deployed the 480 test schema and ranked function to the configured Supabase project; RANKED_TEST_MODE=true.
- User approved disabling the ranked function legacy JWT gateway check; the function validates its own signed tokens.
- Live HTTP smoke test passed: test authentication, pairing, lobby publication, start, result consensus, +16/-16 Elo exactly once, unauthenticated queue rejected. Synthetic accounts only; this does not establish two-PC Steam connectivity.


## Steam route connection investigation — 2026-09-21

Observed live Player.log: incoming connection accepted, remained FindingRoute, then a later client attempt ended with "Timed out attempting to negotiate rendezvous". No successful remote connection or combat start was observed. Friend-code and ranked connections share this transport path.

Changes: set Steam initial route timeout to 45 seconds before socket creation; allow 60 seconds for initial game connection; warm relay discovery up to 20 seconds before ranked queueing (still permit direct-route attempts if relay stays unavailable); ignore socket callbacks belonging to other/old sockets; preserve original connection errors and record Steam route/end codes and relay availability on transitions. No polling or additional traffic is added during combat.

These changes improve connection handling and diagnosis; actual two-PC Steam route recovery must be verified on the affected peers. No claim is made that the external network/rendezvous cause is resolved.

## 게스트 프레임 저하 조사 — 2026-09-29

- 현재 로컬 Player.log는 호스트 기록이므로 상대방 PC의 프레임 저하 원인을 확정하지 못했다.
- 수신한 스프라이트의 위치·회전만 변하면 재질, 색, 크기, 정렬, 글로우, 클리핑 속성을 다시 적용하지 않는다. 클리핑 해제 시 MaterialPropertyBlock도 제거한다. 풀에서 재사용할 때 달라진 스타일은 정상 적용한다.
- HUD의 체력·방어막·타이머·핑·결과 문자열은 표시 값이 변할 때만 갱신한다. 타이머를 기본 문자열과 온라인 문자열로 매 프레임 두 번 바꾸던 처리를 제거했다.
- 개발 빌드의 프레임별 실행 인자 배열 생성을 없앴다. 개발 빌드에는 5초 간격 평균 FPS 표시 및 CLASSIC_PERF 로그를 추가했다. 로그에는 최대 프레임 시간, 50ms 초과 횟수, 활성/생성 복제 수, 수신 패킷 수, 핑, 해상도가 포함된다. 추가 네트워크 전송은 없다.
- 에디터 회귀 검사 통과: 이동·회전 100회 수신 시 스타일 재적용 0회, 색 변경 반영, 풀 재사용 후 클리핑 적용·해제, 기존 패킷 손실·순서 역전·삭제·재사용 검사.
- 로컬 배치 350발 × 3회 기준: 수정 전 게스트 평균 2.00ms/최대 5.67ms, 수정 후 평균 2.00ms/최대 2.71ms. 큰 프레임 저하는 재현되지 않았으며 이 수치만으로 다른 PC에서의 개선을 주장하지 않는다.
- 수정 후 100ms 지연 및 2% 손실 조건: 게스트 평균 2.02ms/최대 10.17ms. 두 조건 모두 최대 복제 수 356, 종료 후 6, 생성 수 356으로 정리·재사용 통과. 시각 데이터 최대 추정 65,520 bytes/s (전송 프레이밍·입력·상태 제외).
- 기록: Logs/GuestPerfBaseline.log, Logs/GuestPerfFix.log 및 Logs/ClassicOnlineSmoke/20260929-113711-971, 20260929-113958-592, 20260929-114024-013 접두사.
- 실제 Steam 상대방에서 여전히 끊기면 그 PC의 %USERPROFILE%\AppData\LocalLow\DefaultCompany\dodevelopgame\Player.log에서 CLASSIC_PERF를 확인한다. FPS가 정상인데 탄막만 끊기는 경우 네트워크 시각 샘플 간격 문제와 구분하여 조사해야 한다.
- 실제 클래식 두 라운드 로컬 검증도 통과: 이동·피격·쿨다운·업그레이드·동일 승자·재대전 (20260929-114101-229). 일반 플레이어 모드의 추가 부하 검사도 통과 (20260929-114227-881). 자동 실행 창은 숨김이므로 이 테스트들은 상대방 화면의 GPU 성능을 보증하지 않는다.
- 배포 파일: Builds/GlowGlow-Test-20260929-GuestPerf.7z (33,538,400 bytes), 7-Zip 무결성 검사 통과. Steam App ID 480 및 기존 테스트 매칭 설정 포함.
