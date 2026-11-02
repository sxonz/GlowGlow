# 클래식 랭크 자동 매칭

체력 100과 탄막별 피해를 지원하는 빌드는 `glowglow-classic-5`를 사용한다. 상태 동기화의
피격 횟수를 체력으로 교체하여 서버 소스의 기본 허용 버전도 5로 맞췄다. 운영 중인 함수는 이 소스를 배포하거나 `RANKED_PROTOCOL=glowglow-classic-5`로
설정해야 한다. 클라이언트 양쪽도 같은 새 빌드여야 한다. 로컬/친구방은 이 서버 설정과 무관하다.

기존 클래식 모드의 **랭크 매칭** 버튼으로 비슷한 점수의 상대를 찾는다. 실제 이동·탄막은 기존 Steam P2P 연결을 사용하고, Supabase는 대기열·점수·경기 결과만 처리한다.

## App ID 480으로 무료 개발 테스트

**Steam 게임 등록비와 퍼블리셔 API 키 없이 테스트할 수 있다.** 현재 게임 설정은 `steamAppId: 480`, `developmentTest: true`이며 서버 주소도 아래 프로젝트로 입력되어 있다.

`https://mnnmfpattjescycllnxo.supabase.co/functions/v1/ranked`

2026-09-21: 이 프로젝트에 테스트 테이블과 ranked 함수를 배포했고, RANKED_TEST_MODE=true 및 함수 자체 인증 설정을 저장했다. 실제 배포 서버에서 테스트 인증·두 계정 매칭·방 정보 전달·경기 결과 합의·점수 +16/-16 및 중복 지급 방지 검증이 통과했다. 아래 1~4단계는 새 서버를 구성할 때 참고한다. 현재 빌드는 5단계부터 테스트하면 된다. 실제 Steam 두 PC 대전은 별도로 확인해야 한다.

1. Supabase 프로젝트의 **SQL Editor**에서 `supabase/migrations/202609210002_ranked_test.sql` 내용을 실행한다. 이전에 `202609210001_ranked.sql`을 실행했어도 이 테스트용 파일은 추가로 실행해야 한다. 각 파일은 한 번만 실행한다. 테스트용 파일만으로도 480 테스트는 가능하다.
2. **Edge Functions → Secrets**에 다음 두 값을 저장한다.
   - `RANKED_TEST_MODE` = `true`
   - `RANKED_SIGNING_SECRET` = 최소 32바이트의 무작위 비밀 값. 이미 저장했다면 그대로 사용한다.
3. `STEAM_APP_ID`와 `STEAM_WEB_API_KEY`는 **480 테스트에서는 필요 없다**. `STEAM_APP_ID`를 이미 넣었다면 `480`으로 설정한다. `SUPABASE_URL`과 `SUPABASE_SERVICE_ROLE_KEY`는 호스팅 환경에서 자동 제공된다.
4. 이 폴더에서 Supabase CLI로 `supabase functions deploy ranked --project-ref mnnmfpattjescycllnxo`를 실행한다. 또는 `npm run dashboard`로 만든 `dashboard/index.ts` 전체를 Supabase의 **Edge Functions → 새 함수 → 편집기**에 붙여넣고 함수 이름을 `ranked`로 배포한다. Supabase 기본 JWT 검사는 꺼야 한다. 함수 자체에서 로그인 후 발급한 서명 토큰을 검증한다.
5. PC 두 대에서 **같은 개발 빌드 폴더 전체**를 사용하고, Steam에는 **서로 다른 계정**으로 로그인한다. 실행 파일 옆 `steam_appid.txt`는 `480`이어야 한다.
6. 양쪽에서 **멀티플레이 → 클래식 → 랭크 매칭**을 누른다. 친구 등록이나 방 코드 입력 없이 자동 연결된다. 화면에 `[테스트]`가 표시된다.
7. 양쪽에서 초기 선택을 마치고 경기한다. 같은 점수에서 시작했다면 승자 +16, 패자 -16을 확인한다. 결과 확인 후 ESC로 나가서 다시 매칭한다.

개발 빌드에서는 실행 파일 옆 `RankedSettings.json`을 읽으므로 서버 주소를 바꾸기 위해 재빌드할 필요가 없다. 변경 후 게임을 재실행한다. Unity 에디터는 `Assets/Resources/Online/RankedSettings.json`을 읽는다.

```json
{
  "endpoint": "https://mnnmfpattjescycllnxo.supabase.co/functions/v1/ranked",
  "steamAppId": 480,
  "region": "asia",
  "developmentTest": true
}
```

테스트 인증은 게임이 전달한 Steam ID를 사용하며 **서버에서 Steam 소유권을 검증하지 않는다**. 따라서 개발 테스트에만 사용한다. 테스트 계정·대기열·경기·점수는 `ranked_test_*` 테이블에 저장하며, 정식 `ranked_*` 데이터와 토큰은 공유되지 않는다. 480 연결 자체는 기존 Steam 로비와 실제 상대 Steam ID 검사 경로를 사용한다.

## 정식 출시로 전환할 때

개발 테스트를 마친 뒤 진행하는 단계다. 지금 결제할 필요는 없다.

1. 게임 전용 Steam App ID와 해당 게임의 퍼블리셔 Web API 키를 준비한다.
2. 아직 적용하지 않았다면 `supabase/migrations/202609210001_ranked.sql`을 실행한다.
3. Secrets의 `RANKED_TEST_MODE`를 `false`로 바꾸고 `STEAM_APP_ID`, `STEAM_WEB_API_KEY`, `RANKED_SIGNING_SECRET`을 설정한다. 전환 전에 테스트 경기를 마친다.
4. 게임 설정에서 `developmentTest`를 `false`, `steamAppId`를 게임 전용 ID로 바꾸고 재빌드한다. 정식 점수는 별도로 1000점부터 시작한다.

API 키와 서명 비밀 값은 Unity 설정이나 배포 파일에 넣지 않는다. 테스트 모드는 에디터와 Development Build에서만 허용된다. 테스트 모드가 꺼진 서버는 `auth_test` 요청을 거부하고 Steam 인증 티켓을 검증한다.

## 매칭 규칙과 사용량

- 신규 점수 1000, Elo K=32. 점수 범위는 0~4000이다.
- 점수 차이 ±100에서 시작하고 대기 15초마다 50씩 넓혀 최대 ±600을 허용한다. 양쪽의 허용 범위를 모두 만족해야 하며, 가장 가까운 점수부터 매칭한다.
- 프로토콜과 지역이 같은 사람끼리 매칭한다. 지역은 `asia`, `europe`, `americas`, `oceania`를 지원한다. 실제 핑 기반 필터는 아직 없다.
- 대기열은 5초마다, 연결 준비는 3초마다 확인한다. 전투 중에는 매칭 HTTP 요청을 하지 않는다. 대기자가 분당 약 12회 요청하므로 Supabase 무료 사용량 한도 내에서 테스트한다.
- 두 참가자가 동일한 경기 결과를 제출하면 점수를 한 번만 반영한다. 시작 후 명시적으로 나가면 기권패다. 결과 불일치 또는 강제 종료로 결과가 미확정이면 점수를 임의 지급하지 않는다.
- 랭크는 나간 뒤 다시 매칭한다. 친구 방의 재대전은 유지한다.

## 남은 출시 작업

전투 판정은 여전히 호스트 PC에서 실행된다. 테스트 인증을 정식 인증으로 바꿔도 호스트 변조·공모·결과 제출 거부까지 막지는 못한다. 연결 끊김 자동 판정, 요청 제한, 시즌, 배치고사, 순위표, 핑 필터, 반복 상대 점수 획득 방지 및 지연 상황의 공정한 피격 판정은 별도 작업이다.

## 검증

이 폴더에서 `npm ci --ignore-scripts` 후 `npm test`를 실행한다. PGlite의 실제 PostgreSQL로 두 마이그레이션과 매칭 함수를 검사한다. 480 테스트는 인증부터 두 계정 매칭·로비 게시·시작·결과 합의·점수 반영까지 Edge Function 처리기와 DB를 함께 거친다. 정식 인증 테스트의 Steam 응답은 대역을 사용한다.

이 자동 검사는 실제 Steam 인터넷 연결을 대신하지 않는다. 배포 후 PC 두 대에서 취소·재검색·정상 경기 종료·기권·재매칭을 확인한다.

참고: [Supabase 비밀 값 설정](https://supabase.com/docs/guides/functions/secrets), [Steam 인증 티켓 검증](https://partner.steamgames.com/doc/webapi/ISteamUserAuth), [Supabase 무료 한도](https://supabase.com/pricing).
