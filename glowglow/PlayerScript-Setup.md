# 비전투 PlayerScript

Unity 6 / Input System 기반. NPC 상호작용과 전투용 Player는 포함하지 않습니다.
기존 씬과 프로젝트 설정은 변경하지 않았습니다.
키 배치는 `Assets/InputSystem_Actions.inputactions`의 `NonCombat` 맵에서 관리합니다.
기존 `Player` 및 `UI` 맵은 변경하지 않았습니다.

## 씬에 붙이기

1. `GameObject > 2D Object > Sprites > Square`로 플레이어를 만듭니다.
2. `Assets/Scripts/PlayerScript.cs`를 플레이어에 붙입니다.
   `Rigidbody2D`와 `BoxCollider2D`가 자동으로 추가됩니다.
3. PlayerScript의 `Move Action`에 `InputSystem_Actions > NonCombat > Move`,
   `Jump Action`에 `InputSystem_Actions > NonCombat > Jump`를 지정합니다.
   각 필드 오른쪽 선택 버튼에서 고르거나 에셋을 펼쳐 해당 액션을 드래그하세요.
   기존 `Player/Move`는 Vector2이므로 여기에는 지정하지 마세요.
4. Rigidbody2D는 `Body Type: Dynamic`, `Gravity Scale: 1` 이상으로 둡니다.
   X/Y 위치는 고정하지 마세요. Z 회전은 스크립트가 고정합니다.
5. 바닥용 Square를 만들고 가로로 늘린 뒤 `BoxCollider2D`를 붙입니다.
   플레이어와 바닥의 `Is Trigger`는 꺼두세요.
6. 플레이어를 바닥 위, 카메라 안에 배치하고 Play를 누릅니다.
   Game 뷰를 클릭한 뒤 ←/→로 이동하고 Space로 점프합니다.

## Inspector 설정

- `Move Action`: `NonCombat/Move` (Value / Axis). 좌우를 -1~1로 읽습니다.
- `Jump Action`: `NonCombat/Jump` (Button). 새로 누른 프레임에만 점프를 요청합니다.
- `Move Speed`: 좌우 이동 속도. 기본값 5.
- `Jump Speed`: 점프 시 위쪽 속도. 기본값 8. 중력에 따라 점프 높이가 달라집니다.
- `Ground Layers`: 착지 가능한 레이어. 기본값 Everything.
  NPC 등이 추가되면 바닥용 레이어를 만들어 이것만 선택하세요.

## 키 변경과 추후 설정 UI

에디터에서는 InputSystem_Actions를 열고 NonCombat의 바인딩을 변경한 뒤 저장합니다.
Move는 1D Axis Composite이며 Negative가 왼쪽, Positive가 오른쪽입니다.
기본 바인딩은 ← / → / Space이고, 같은 액션에 게임패드 등의 바인딩을 추가할 수도 있습니다.
물리 이동 코드는 어떤 키나 장치를 썼는지 알 필요가 없습니다.

게임 안에서 키를 바꾸는 UI와 설정 저장 기능은 아직 구현하지 않았습니다.
추후 같은 액션 인스턴스에 `PerformInteractiveRebinding()`을 적용하고,
`SaveBindingOverridesAsJson()` / `LoadBindingOverridesFromJson()`으로 설정을 저장·복원할 수 있습니다.
Move의 좌우는 Composite 전체가 아니라 각 파트 바인딩을 따로 리바인딩해야 합니다.
키를 받는 동안 PlayerScript를 비활성화하고, 완료/취소 후 다시 활성화하면
설정 입력으로 캐릭터가 움직이거나 점프하는 것을 피할 수 있습니다.

PlayerScript는 지정한 두 액션의 Enable/Disable을 관리합니다.
단일 비전투 플레이어용이며, 다른 컴포넌트에서 이 두 액션의 활성화를 중복 관리하지 마세요.
PlayerInput 컴포넌트나 생성된 C# 래퍼는 필요하지 않습니다.

## 동작 확인

- ←/→ 각각 이동하고 키를 놓으면 수평 이동이 멈춥니다.
- 두 방향을 동시에 누르면 수평 이동하지 않습니다.
- 착지한 상태에서 Space를 새로 눌러야 점프합니다.
- 공중에서 다시 누르거나 벽/천장에 닿아도 추가 점프하지 않습니다.
- Space를 누른 채 착지해도 자동으로 다시 점프하지 않습니다.
- PlayerScript를 끄면 입력과 수평 이동이 멈추며 중력은 유지됩니다.
- Move의 좌우 바인딩과 Jump 바인딩을 다른 키로 바꿔도 코드 수정 없이 동작합니다.

경사면 이동, 이동 발판 따라가기, 애니메이션, NPC 대화 중 제어는 이번 범위에 포함하지 않습니다.
