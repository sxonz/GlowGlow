# 탄막 확장

## 구조

- `WeaponDefinition`: 공유하는 기본 설정 에셋. 이름, 동작 프리팹, 발사 간격, 속도, 수명, 반지름, 색상, 광선 사거리를 정의합니다.
- `WeaponRuntime`: 플레이어가 장착한 무기의 실행 상태. `Definition`, `Upgrades`, 최종 `Stats`를 제공합니다.
- `WeaponUpgradeState`: 무기 인스턴스마다 업그레이드 에셋별 레벨을 보관합니다. 고정된 강화 종류는 없습니다.
- `WeaponStats`: 발사 시 복사되는 최종 수치입니다. 효과와 상한은 WeaponUpgradeDefinition 파생 스크립트에서 직접 정의합니다. 발사 후 업그레이드해도 기존 탄은 변하지 않습니다.
- `ProjectileBase`: 소유자, 발사 수치, 방향, 수명, 중복 피격 방지, 프리팹별 풀을 관리하는 추상 클래스입니다.
- `Bullet : ProjectileBase`: 직진하고 명중 시 사라집니다. 사거리는 사용하지 않고 속도와 수명으로 이동 거리가 결정됩니다.
- `Laser : ProjectileBase`: 발사 지점과 방향에 고정되는 광선입니다. 사거리와 반지름으로 길이와 폭을 정하고, 수명 동안 유지하며 대상마다 한 번만 명중합니다. 이동 속도는 사용하지 않습니다. 무적 중 접촉한 대상은 무적이 끝나면 명중할 수 있습니다.
- `BulletProjectile : Bullet`: 기존 스크립트 GUID와 이전 `Spawn` 호출을 유지하는 호환 클래스입니다. 실제 플레이어 발사는 `WeaponRuntime.Fire`를 사용합니다.

기존 3회 피격 규칙을 유지하므로 공격력 업그레이드는 아직 없습니다. 재시작하면 활성 탄과 업그레이드를 초기화합니다. `EquipWeapon`도 새 상태를 생성하므로 무기 교체 시 이전 무기의 레벨을 유지하지 않습니다.

## 새 탄막 추가

1. `ProjectileBase`를 상속한 스크립트를 작성합니다. 기존 탄을 변형하려면 `Bullet`이나 `Laser`를 상속해도 됩니다.
2. `OnSpawn()`에서 시각 효과, 충돌체와 매번 재설정할 상태를 초기화하고, `Tick(float deltaTime)`에서 동작을 작성합니다. `Awake`/`OnEnable`에서는 발사 수치가 아직 설정되지 않았을 수 있으므로 발사 초기화에 사용하지 않습니다.
3. 충돌 콜백에서 `TryHit(other)`를 호출합니다. 기본적으로 명중하면 사라집니다. 관통형은 `DespawnOnHit`을 `false`로 재정의합니다. 추가 효과의 정리는 `OnDespawn()`에서 처리합니다.
4. 스크립트를 프리팹의 루트에 부착하고 필요한 렌더러/충돌체를 구성합니다. 루트 위치, 회전, 크기는 발사할 때 초기화됩니다. `Update`를 재정의할 때는 공통 수명 처리를 위해 `base.Update()`를 호출합니다.
5. `Create > GlowGlow > Weapon Definition`으로 설정을 만들고 `Projectile Prefab`에 연결한 뒤 플레이어의 `weapon`에 지정합니다. 비워 두면 기존 기본 탄이 발사됩니다.

Laser를 사용하려면 빈 GameObject에 `Laser`를 추가하여 프리팹으로 저장하고 연결합니다. 필요한 SpriteRenderer, BoxCollider2D, Rigidbody2D는 자동으로 추가됩니다. 먼저 수명을 0.2초, 발사 간격을 0.5초 정도로 설정해 확인할 수 있습니다.

```csharp
player.CurrentWeapon.TryUpgrade(upgradeAsset);
int level = player.CurrentWeapon.Upgrades.GetLevel(upgradeAsset);
float cooldown = player.CurrentWeapon.Stats.Cooldown;
```

## 검증

업그레이드 추가 시 레벨 상한, 상태 분리, 효과 적용 순서, 해제와 초기화를 검증하세요.

Unity Play Mode에서는 기본 탄 발사와 피격, Laser의 방향/길이/피격, 풀 재사용 시 상태 초기화, 재시작 후 남은 탄 제거를 확인해야 합니다. 업그레이드 선택 UI와 저장 기능은 이 구조에 포함하지 않습니다.

## 임의 업그레이드 연결

1. `WeaponUpgradeDefinition`을 상속하고 `ModifyStats(stats, level)`에서 원하는 최종 수치를 반환합니다. 구체적인 효과와 밸런스는 아직 정해두지 않았습니다.
2. 파생 클래스에 `CreateAssetMenu`를 선언해 설정 에셋을 만들고 이름, 설명, 아이콘, `maxLevel`을 지정합니다. 필요하면 `CanApplyTo(weapon)`으로 적용 대상을 제한합니다.
3. 해당 에셋을 `player.CurrentWeapon.TryUpgrade(upgradeAsset)`에 전달합니다. 실제 선택 UI, 비용, 획득 규칙은 별도로 연결합니다.
4. `Upgrades.GetLevel(asset)`, `Remove(asset)`, `Clear()`, `Changed`로 상태 조회, 해제, 초기화, 변경 알림을 사용합니다.

효과는 매번 기본 수치부터 획득 순서대로 계산됩니다. 같은 효과를 강화하면 그 위치에서 전체 레벨을 적용하며, 이전 계산 결과에 계속 누적하지 않습니다. 효과 계산은 부작용 없이 작성하고 공유 에셋에 플레이어 상태를 저장하지 마세요. 이미 발사한 탄과 진행 중인 쿨타임은 유지됩니다. 같은 손패에서 슬롯만 바꾸면 레벨이 유지되고, 새 무기 인스턴스 생성 및 경기 재시작 때 초기화됩니다. 저장 기능과 구체적인 업그레이드 에셋은 포함하지 않습니다.
