# 체력 100과 탄막별 피해

플레이어 최대 체력은 100이며 0에서 패배한다. 시간 종료는 남은 체력이 높은 쪽이 승리한다.
피격 무적은 기존 0.8초를 유지한다. 같은 프레임의 피해 판정은 가장 큰 값 하나만 적용한다.
작은 피해가 먼저 들어온 경우 차액만 추가하며 피격 횟수와 무적 시간을 추가하지 않는다.
보호막은 피해량에 관계없이 1회 공격을 전부 막으며, 같은 프레임에 중복 소모하지 않는다.

| 탄막 | 피해 |
| --- | --- |
| Basic Shot | 6 |
| Laser Shot | 22 |
| Spread Shot | 중앙 10, ±10도 8, ±20도 및 강화 ±30도 6 |
| Bomb | 폭발 30, 파편 6; 강화 연쇄도 동일 |
| Electric Pulse | 기본 0, 강화 벽 충돌/당기기 접촉 18 |
| Overdrive | 접촉 20 |
| Bouncer | 10 |
| Orbit Orb | 접촉 16 |
| 4-axis | 오브마다 10 |
| Meteor Dive | 낙하 32, 폭발 20, 파편 4 |
| Meteor 미니탱크 | 낙하 16, 폭발 10, 파편 2 |
| Octo Shot | 원본 8, 분열탄 4 |
| Boomerang | 왕복 각각 24 |
| Prism Shot | 기본/꼬리 탄환 10, 강화 폭발 8 |
| Solar System | 태양 24, 공전 행성 12, 방출 행성 16, 혜성 8, 플레어 0 |
| Mine | 폭발 32 |
| Firefly Convergence | 반딧불마다 8 |
| Twin Satellites | 위성 접촉 12, 발사 탄환 5 |
| Blossom Burst | 봉오리 0, 꽃잎 6 |
| Constellation Ignition | 경로 14, 완성된 외곽선 24 |

기본 피해는 각 WeaponDefinition의 damage, 개별 패턴은 BarrageStep.damage에서 조정한다.
step의 -1은 무기 피해 상속이며 0은 무피해다. 파생 공격 수치는 BarrageDamage에 모았다.
피해는 WeaponStats에 값으로 저장하며 강화, 슬롯 전환, 지연 발사, 반사와 풀 재사용에 따른
값 유실을 방지한다. 런타임 스프라이트나 텍스처를 추가 생성하지 않는다.

훈련장 표적은 체력 0에서도 계속 맞을 수 있고 누적 피해와 명중 횟수를 표시한다.
전장 초기화/무기 전환 시 기존 규칙대로 체력과 통계를 초기화한다.

온라인은 ClassicPlayerState의 피격 횟수 필드를 체력 byte로 교체한다. 체력 0~100은
항상 1바이트로 전송하여 상태 메시지 크기를 늘리지 않는다. 두 클라이언트와 랭크 대기열은 glowglow-classic-5를 사용한다.
서버 소스의 기본값을 갱신했으며 운영 서버 배포는 별도다.

## 검증 실행

현재 Assets/Scripts, Editor, Resources, Settings, Scenes, UI를 격리 Unity 6000.0.68f1
프로젝트에 복사하고 HealthDamageSmokeRunner.cs만 그 프로젝트의 Assets에 추가한다.
`-batchmode -executeMethod HealthDamageSmokeRunner.Begin -logFile <log>`로 실행한다.
그래픽을 켜고 -quit는 사용하지 않는다. 실제 피해/무적/보호막/파생 공격/반사/풀링,
FishNet 체력 직렬화와 게스트 적용, 실제 Arena의 승패와 HUD, 무제한 훈련 피해,
중첩 판정의 할당량을 검증한다. 결과는 ValidationOutput/health-result.txt에 저장한다.

## 검증 결과 — 2026-10-28

- HealthDamageSmokeRunner: 115개 검사 통과. 저장 보고서는 health-validation-result.txt.
- CombatUpgradeChecks: 기존 전투 회귀 897개 검사 통과.
- BotMatchSmokeRunner: 실제 봇 대전의 자동 행동, 승패, 재시작, 훈련장 분리 통과.
- Backend/ranked: 21개 테스트 통과.
- 워밍업 이후 같은 프레임의 중첩 피해 처리 10,000회에서 추가 할당 0바이트.
- 빈 손패의 FishNet 플레이어 상태는 체력 0/1/63/64/78/100 모두 기존과 같은 21바이트.
- 1280×720 실제 Arena HP 표시와 훈련장 누적 피해 화면을 렌더링하고 확인했다.
  캡처는 Builds/ConstellationValidation/ValidationOutput/health-arena.png 및 health-training.png.

격리된 로컬 Unity 에디터 검증이며, 온라인 상태는 같은 프로세스에서 직렬화와 게스트
적용을 확인했다. 두 PC의 실제 Steam 통신이나 운영 랭크 서버 배포를 포함하지 않는다.
