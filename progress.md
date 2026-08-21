# 프로젝트 진행 상황

> 최종 수정: 2026-08-21
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### 네트워크/UI
- [x] (2026-08-20 #0) 체력 게이지 연결 + 표시 버그 2건 수정 (server-sync T12 마무리) — `IngameHealthBarUI.Init()`(`HealthBarBg/HealthBarFill`, `ArmorBarBg/ArmorBarFill`) 신설 후 `IngameScene.Init()`에서 호출해 영구 null 상태를 해소. 연결 과정에서 발견한 2건도 수정: ① **초기 게이지 값이 한 번도 안 밀렸다** — `SetHP`/`SetArmor` 호출부가 피격·재생 틱뿐이라 첫 피격 전까지 프리팹에 저장된 `fillAmount`가 그대로 보였다. `SyncHealthBarMax()`에서 최대치 직후 현재값도 밀도록 추가(최대치 → 현재값 순서여야 `SetArmor`가 최대 실드 0 가드에 안 걸림) ② **최대 실드가 0이 될 때 실드 바가 안 비워졌다** — `SetArmor()`의 `_maxShield <= 0f` 조기 반환 탓에 벗기 직전 `fillAmount`가 남았다. 반환 대신 `fillAmount = 0`으로 교체. 조건은 '방어구 교체'가 아니라 최대 실드가 0이 되는 것이며(해제 / 스펙 미등록 방어구 / equip 분기에 빈 슬롯이 소스), **정상 교체는 새 방어구 최대치가 들어와 원래부터 문제없다**
- [x] (2026-08-20 #1) 플레이어 디스폰 처리 + 유령 재스폰 차단 (server-sync T3 일부·T7) — `D2CDespawnPlayerObject`(35) 등록 + `Handle_D2CDespawnPlayerObject` + `IngameScene.DespawnPlayerObject()`. **지금까지 다른 플레이어가 사라지는 경로 자체가 없어** 죽거나 탈출해도 캐릭터가 맵에 영원히 남아 있었다. `DespawnReason`별 연출은 에셋이 없어 `TODO:`로 보류하고 사유와 무관하게 즉시 제거. **차단 지점이 두 곳이라는 것이 핵심** — 문서가 지목한 `UpdatePlayerStates()`의 요청 억제만으로는 부족하고, 이미 보낸 `C2DRequestSpawnByObjectId`의 응답이 디스폰보다 늦게 도착해 `SpawnPlayerObject()`가 되살리는 경로가 더 위험하다. `_despawnedObjectIds`(만료 없는 씬 수명 `HashSet<uint>`, 플레이어·비플레이어 공용)로 양쪽을 막았다. 만료 창을 쓰지 않은 이유는 창보다 늦게 오는 패킷이 뚫기 때문이며, 재전송 한도를 없앤 뒤로 그 경로가 넓어졌다. T3은 5개 일괄 등록 대신 **구현과 함께 하나씩 등록**하는 방침으로 변경 — 빈 스텁 등록은 패킷을 조용히 삼키지만 미등록은 경고 로그로 드러난다
- [x] (2026-08-20 #3) 비플레이어 오브젝트 스폰·디스폰 배선 (server-sync T4·T5·T6) — `_sceneObjects`(objectId → `GameObjectController`) 레지스트리 신설 + `D2CNotifySpawnObject`(36)·`D2CNotifyDespawnObject`(39) 처리. **T4의 실제 작업은 딕셔너리 추가가 아니라 스폰 경로 일원화였다** — `Handle_D2CResponseSpawnByObjectId`가 씬을 거치지 않고 `Managers.Resource.InstantiateFromObjectDataStruct()`를 직접 부르고 있어, 그대로 뒀다면 등록되지 않은 오브젝트가 생겨 레지스트리에 구멍이 났다. `IngameScene.SpawnObject()`를 유일한 스폰 경로로 삼고 호출부 3곳(정적·동적 초기 스폰, 지연 스폰 응답)을 전부 라우팅해 중복 검사·디스폰 차단·매핑 검사를 한곳에 모았다. `D2CNotifySpawnObject`는 `D2CResponseSpawnByObjectId`와 페이로드가 같아 변환부를 `PacketHandler.ToObjectData()`로 분리해 공용화. 매핑 없는 `object_type`은 빈 경로 로드 실패로 조용히 묻히던 것을 타입 번호·objectId를 찍는 에러 로그로 교체(`Undefined`는 키가 있어도 경로가 null이라 함께 걸러냄). **보류돼 있던 판단 확정**: 이미 파괴된 컨테이너에 `C2DCloseContainer`를 보내지 않는다 — 서버 통보를 분리해 `CloseContainerLocal()` 신설
- [x] (2026-08-21 #0) 체력/실드 게이지 Fill 이미지 Editor 설정 (server-sync T12 렌더 경로) — `HealthBarFill`/`ArmorBarFill`의 Image Type을 `Filled`로 맞추고 스프라이트를 신규 `Assets/Resources/White_Square.png`(Sprite, border 0)로 지정. **코드 변경 없음** — `(2026-08-20 #0)`에서 "값은 정상인데 그림만 안 바뀌던" 원인이 이것 하나였다. `fillAmount`는 Type이 `Filled`일 때만 동작하고 `Simple`/`Sliced`면 대입이 **에러 없이 무시된다**. 씬 오버라이드에는 두 Image 중 한쪽에만 `m_Type`이 실렸는데, 나머지 하나는 프리팹 기본값이 이미 `Filled`라 정상이다. 이 제약은 앞으로 추가될 게이지에도 그대로 적용되므로 `UI/CLAUDE.md`의 `IngameHealthBarUI` 항목에 상시 규칙으로 남겼다. **런타임 실측은 아직** — 아래 '확인 필요' 참조
- [x] (2026-08-21 #1) 무기 변경 통보 처리 + 지연 스폰 요청 1회 제한 (server-sync T8) — `D2CNotifyWeaponChanged`(37) 등록 + `IngameScene.HandleWeaponChanged()`. **도착 경로가 문서에 적힌 둘이 아니라 셋이었다** — 남의 무기 전환뿐 아니라 남의 장착·해제(`C2DRequestEquipItem` 슬롯 0/1 성공)도 이 패킷으로 오므로, T9(무기 전환 송신) 없이도 이 작업이 필요했다(지금까진 남이 무기를 주워도 스폰 시점 외형이 그대로였다). 분기는 디스폰 차단 → 본인 롤백 → 남 외형 갱신 → 미스폰 시 스폰 요청 순. **본인 objectId로 오는 것은 항상 거부다**(성공은 룸의 나머지에게만 간다) → 성공/실패 분기 불필요. `_spawnCompleted` 가드를 둔 이유는 `_myObjectId` 초기값 `0`이 실재 objectId라 스폰 전엔 비교가 성립하지 않기 때문. 롤백은 슬롯 정보가 없어 주/보조 `item_id` 중 **한쪽만 일치할 때만** 확정(동률이면 외형·스펙이 같아 유지) — T9에서 `target_slot` 대조로 승격할 자리. **함께 고친 것 2건**: ① `RequestSpawnIfUnknown()`·`_pendingSpawnRequests` 신설 — `UpdatePlayerStates()`가 미스폰 objectId를 볼 때마다 **10Hz로 새 reliable을 만들고 있었다**. 재전송이 아니라 매번 새 시퀀스라 응답이 3초만 늦어도 in-flight 32슬롯이 차고, 넘치면 아직 ACK되지 않은 **다른** 패킷(장착·귀환 요청)이 덮어써져 조용히 유실된다. 요청은 reliable이라 한 번이면 충분하므로 objectId당 1회로 제한하고 판정을 헬퍼 하나로 모았다(전송 지점도 이제 이 함수뿐) ② 무기 **해제** 시 `ApplyEquipItem`이 `EquipWeapon` 호출을 건너뛰어 손에 든 무기가 남던 버그 — 서버는 남들에게 맨손을 통보하므로 T8을 붙이는 순간 화면이 어긋난다. **Unity 컴파일·런타임 모두 미검증**
- [x] (2026-08-21 #2) 무기 전환 구현 + 손에 든 무기 규칙 반영 (server-sync T9·T9-a) — **착수 직전 서버가 계약을 바꿨다**. `D2CNotifyWeaponChanged`에 `slot`·`inventory_version`이 추가되고 **성공도 요청자에게 오게 되면서**, T8이 세운 "본인 수신 = 거부" 전제와 `item_id` 추정 롤백이 통째로 폐기됐다. **로컬 예측은 쓰지 않는다** — 키 입력 시점엔 요청만 보내고 손의 무기는 통보 후에만 바꾼다(예측은 동작 검증 후 `OPTION:`으로). 확정 전 재요청을 막아 in-flight 요청이 항상 1개이므로 **통보 순서 역전이 정상 경로에서 발생하지 않는다**(잔여 경로는 `OPTION:`으로 기록 — 클라가 수신 reliable을 중복 제거하지 않아 재전송된 옛 통보가 뒤늦게 오면 낡은 슬롯이 재적용된다). 판정은 `slot == 보낸 target_slot`이지만 **상태 반영은 성공·거부가 같아**(도착값이 항상 권위값) `ApplyServerWeaponState()` 한 경로로 처리하고, 갈리는 것은 버전 불일치 시 재동기화 하나뿐이다. 자동 재요청은 하지 않는다(버전이 계속 움직이면 루프). **확정 전 발사 차단**은 reliable(교체)·unreliable(사격) 간 순서 보장이 없어 필수이며(`weapon_dbid` 불일치는 조용히 버려진다), `_fireBlocked`는 마우스 재클릭으로 풀려 재사용할 수 없어 `IsShooting`에 별도 게이트를 뒀다. 통보 유실 시 발사가 영구 차단되므로 **TEMP 워치독(3초)** — 잠금만 풀고 무기 상태는 서버에 맡긴다. **T9-a**: 장착·해제로 손에 든 슬롯이 바뀌는 경우는 본인에게 통보가 없어 클라가 서버 규칙("들고 있던 슬롯이 비면 반대쪽으로, 양쪽 다 비면 맨손")을 직접 반영해야 한다 → `SyncHeldWeapon()`. 그 외 `weapon_dbid`를 `PlayerController._equippedWeaponId`(서버 확정값)로 교체, `EquipWeapon` 재생성 가드·캐시 미스 로그 추가, 1/2 키 바인딩, 예측을 쓰지 않으면서 호출자가 사라진 `IngameInventory.ApplyWeapon()` 삭제. **Unity 컴파일·런타임 모두 미검증**
- [x] (2026-08-21 #3) 킬 피드 데이터 경로 (server-sync T10, UI 제외) — `D2CNotifyPlayerKilled`(40) 등록 + `HandlePlayerKilled()`. **판단 확정: UI는 붙이지 않는다**(프리팹이 `Assets/Scripts` 밖). 패킷 처리·킬러 무기 추적·표기 문자열(`DescribePlayer()`)까지 코드로 완성하고 출력만 `Util.Log`로 두어, 프리팹이 생기면 `TODO:` 자리에서 표시부만 갈아끼우면 되게 했다. **내 죽음에는 이 패킷이 오지 않으므로**(피해자 제외) 내 사망 줄은 `HandleHealthChange`에서 만든다 — T11이 남겨둔 `LastAttackerObjectId`를 여기서 처음 쓰며, 인게임 부활이 없어 `_deathReported`로 한 번만 남긴다. `killer_object_id`의 `0xFFFFFFFF`는 `NO_ATTACKER_OBJECT_ID`를 재사용했다(같은 전투 문맥이라 동일 값 상수를 세 번째로 만들지 않는다). 미스폰 **킬러는** `RequestSpawnIfUnknown()`으로 채우고 **피해자는 요청하지 않는다** — 같은 타이밍에 디스폰이 와서 어차피 차단 목록에 걸린다. 킬러 무기는 T8의 `EquippedWeaponId`를 쓰며 본인 경로를 위해 `PlayerController.EquippedWeaponId`를 공개. 이걸로 T3의 핸들러 5개가 전부 등록됐다. **Unity 컴파일·런타임 모두 미검증**
- [x] (2026-08-21 #4) 매치 이탈 출구 + 사망 유예 (server-sync T18·T15 일부·T10 수정) — **서버가 사망 후 5초 유예를 신설**(proto 공통 사항 6)하면서 사망 확정 신호가 `D2CNotifyPlayerKilled`로 바뀌고 **피해자에게도 오게 됐다**. 이에 맞춰 `HandleHealthChange`의 HP 0 감지를 걷어내고 사망 기점을 그쪽으로 일원화(두 패킷이 모두 도착해 기점이 이중화되던 문제). **사망·귀환 성공·연결 끊김 셋이 하나의 출구를 쓴다** — `BeginMatchExit(reason)` → `MATCH_EXIT_DELAY`(4초) → `CompleteMatchExit()`(UDP 종료). **4초는 서버 계약(5초)보다 짧게 잡은 값이며**(서버보다 먼저 정리 + ACK 시간 확보) 상수 주석에 근거를 남겼다. **ACK 함정 하나를 해소**: ACK는 다음 송신에 piggyback되는데 유예 중에는 상태 전송이 멈춰 송신이 하트비트뿐이고 주기가 3초라, 그냥 두면 통보 ACK가 최대 3초 늦어 4초 예산의 여유가 1초뿐이었다 → 이탈 시작 시 `SendHeartbeatNow()`로 강제 송신. 입력 차단은 `IsInputLocked` 하나로 모아 사격·시점·이동·조준/상호작용 판정과 `RequestXXX` 전부가 참조하게 했고, **이동은 입력만 끊고 중력은 유지**한다(공중에서 죽어도 시신이 뜨지 않게). **수신은 막지 않는다** — 브로드캐스트가 계속 오는 것이 관전 유지의 근거다. 연결 끊김 통보는 `UDPManager`의 수신 워치독 지점에 뒀다(`Disconnect()`는 재연결 직전 정리에서도 불려 접속마다 이탈 처리가 돌게 된다). 씬 정리는 `IngameScene.Clear()` 오버라이드가 맡아 `Managers.Clear()` 경로로 자동 실행된다. 귀환 취소는 `reason`별 분기 완료(`PLAYER_DEAD`는 사망 흐름에 양보, `SESSION_LOST`는 출구, 나머지는 재시도 허용). **결과 씬 전환과 사망 연출은 `TODO:`** — 지금은 4초 뒤 연결만 끊고 인게임에 머문다(테스트 종료는 당분간 강제 종료). **Unity 컴파일·런타임 모두 미검증**
- [x] (2026-08-21 #5) 사망 탑뷰 연출 — `DeathCameraController` 신설(`Assets/Scripts/Controller/`, **독립 MonoBehaviour·프리팹 없음**). `Play(sourceCamera, deadPlayer)` 정적 호출이 런타임에 `@DeathCamera` 리그를 만들어, 원본 카메라의 fov·클리핑·컬링 마스크를 복사한 카메라를 같은 시점에 생성 → 원본 비활성 → 시신 위 +3의 `TopViewPoint`로 2초간 상승하며 시선을 시신으로 회전(smoothstep). **기존 카메라를 옮기지 않고 새로 만드는 것이 핵심** — 기존 카메라는 `PlayerController` 하위라 `ApplyViewRotation()`이 매 프레임 시점을 덮어써 보간이 되돌려진다. 처리한 함정 2건: ① **`AudioListener`를 옮기지 않는다** — 새 카메라에 붙이면 씬에 둘이 되어 경고가 뜨고, 그대로 두면 소리가 시신 위치에서 들려 연출과도 맞는다 ② **도착 시점 시선이 정확히 수직**이라 `LookRotation`의 up 힌트를 원래 보던 수평 방향으로 잡았다(forward와 up이 평행하면 회전이 튄다). 튜닝값(높이 3, 시간 2초)은 파일 상단 상수이며 **연출 시간은 유예 4초보다 짧아야 한다**. **Unity 컴파일·런타임 모두 미검증**

### 매니저/리소스
- [x] (2026-08-20 #2) 오브젝트 풀링 코드 전면 제거 — `PoolManager.cs`·`Poolable.cs`(+`.meta`) 삭제, `Managers.cs`의 `_pool`/`Pool`/`_pool.Init()`/`Pool.Clear()` 제거, `ResourceManager`의 `Instantiate()`·`Destroy()` 분기와 `Load<T>()`의 GameObject 분기 전체 제거(풀 조회가 유일한 목적이라 이름 추출까지 통째로 죽은 코드였음 → `Resources.Load<T>` 한 줄로 축약). **동작 변화 없음** — `Poolable`을 어디에도 부착하지 않아 세 분기 모두 항상 false로 흘렀고 `GetOriginal()`은 항상 null이었다. 풀링이 필요해지면 별도 방식으로 새로 만들 예정. `Util.GetOrAddComponent`는 `UIManager` 등이 쓰므로 유지

---

## 다음 작업 우선순위 (제안)

> **서버 변경분(2026-08-12 이후) 반영 작업은 별도 문서로 분리** — `server-sync-todo.md` (T1~T17) 참조.
> proto의 `[작업사항]` 주석을 추적한 임시 문서이며, 전 항목 반영 후 이 파일로 이관하고 삭제할 것.
> 아래 목록과의 대응: 2번(게임 결과 씬) = T15·T18의 남은 부분, 5번(워치독 제거 검토) = 2번에 종속

1. **서버 변경분 반영 (`server-sync-todo.md`)** — T1~T9·T11 완료(T3의 핸들러 5개 전부 등록 완료), T10은 데이터 경로가 끝나고 **UI 프리팹 대기**, T12는 **런타임 실측만 남음**, T15·T18은 출구까지 끝나고 **결과 씬 대기**. 남은 것은 T16·T17. **T13은 시신 프리팹 제작 대기, T14는 서버 목록 대기**
2. **게임 결과 씬 제작 → 이탈 출구 연결** — 이탈 흐름(`BeginMatchExit` → 4초 → `CompleteMatchExit`)과 사망 연출은 완성됐고 **씬 전환만 `TODO:`로 비어 있다.** 지금은 4초 뒤 연결만 끊고 인게임에 머물러 **테스트 종료가 강제 종료다.** 씬이 생기면 `CompleteMatchExit()`의 TODO를 전환으로 교체하고, 로비 재진입 분기(`LobbyScene.cs:87`) + 진입 직후 `GetInventoryCall()` 재조회를 붙인다(**`OnLoginComplete()`이 캐시 배열을 읽으므로 재조회 완료 후 배치하도록 순서를 고정할 것**). 탈출(귀환) 연출도 `BeginMatchExit()`에 자리만 비어 있다
3. **귀환 스팟 씬 배치** — 맵 씬에 귀환 단말기 오브젝트 배치, `RecallSpotController` 부착 후 인스펙터에서 `_recallSpotIndex`를 서버 테이블 값에 맞춤. 조준 레이가 맞아야 하므로 트리거가 아닌 일반 콜라이더 사용
4. **귀환 진행 중 UI 피드백** — 승인~결과 사이 5초 구간 표시(카운트다운 등). `InteractText`는 매 프레임 재조회되므로 `virtual` 프로퍼티화하면 동적 텍스트 전환 가능
5. **TEMP 워치독 2건 제거 검토** — 귀환(`RECALL_TIMEOUT`/`_recallTimer`)과 무기 교체(`SWITCH_WEAPON_TIMEOUT`/`_switchWeaponTimer`). 서버 통지 신뢰성이 실측으로 확인되면 제거. **귀환 쪽은 실처리가 붙었으므로 이제 검토 가능**하고, 무기 교체 쪽은 통보 유실 시 발사가 영구 차단되는 것을 막는 안전장치다
6. **킬 피드 UI 프리팹 준비 → 표시부 연결** — 데이터 경로는 완료(T10). `HandlePlayerKilled()`의 `TODO:` 자리에서 `Util.Log`를 표시부로 교체하면 되며, **내 사망 줄은 `HandleHealthChange` 쪽 경로라 함께 봐야 한다**
7. **발사 이펙트 프리팹 준비 → 이펙트 구현** — 로컬 탄착 이펙트(`ProcessHit`), 수신 측 머즐 플래시/총성/탄착 이펙트(`HandleWeaponFireBroadcast`). 파티클·사운드 에셋이 `Resources/Prefabs/` 아래에 필요
8. **탄약 차감 주석 해제** — `Fire()` 내 `magazine.quantity--` 및 빈 탄창 가드. 테스트 완료 후 활성화
9. **EmptyAmmoFire() 구현** — 빈 탄창 사운드, 재장전 유도 UI
10. **인벤토리 열기/닫기 키바인딩** — Tab키로 MyInventory 토글 등 추가 입력 연결 (컨테이너 E/I키 닫기, 무기 전환 1/2 키는 완료)
11. **실제 맵 씬에서 IngameScene 상속 완성** — `IngameScene`을 상속하는 맵별 씬 컴포넌트 구현
12. **설정값 실제 적용** — 해상도/창모드/FOV 변경이 `Screen.SetResolution()`, `Camera.fieldOfView` 등에 반영되도록 구현

---

## 메모

### 확인 필요
- **(2026-08-21) 탑뷰 연출에서 시신이 보이는지** — 컬링 마스크를 원본 카메라에서 복사하므로, **1인칭에서 자기 몸을 숨기려고 레이어를 빼뒀다면 탑뷰에서도 시신이 안 보인다.** 그 경우 마스크에 해당 레이어를 더하는 한 줄이면 된다. URP 후처리·전용 렌더러를 원본 카메라에 지정해뒀다면 그것도 연출 카메라에는 적용되지 않는다(`UniversalAdditionalCameraData`는 복사하지 않음)
- **(2026-08-21) T18 검증** — 죽었을 때 ① 킬 피드 로그 뒤 `[MatchExit] 이탈 시작` ② **카메라가 떠오르며 시신을 내려다보는지** ③ 4초간 조작이 전부 막히고 **남들은 계속 움직이는지**(관전 유지) ④ `[MatchExit] 연결 종료` ⑤ 이후 인게임에 남는 것이 현재 의도된 동작(강제 종료로 끝낼 것)
- **(2026-08-21) T10 검증** — 2인 접속. A가 B를 죽였을 때 **A 화면**에 `[KillFeed] 나 objectId=A(weaponId=N) → objectId=B(...)`, **B 화면**에는 이 패킷이 오지 않으므로 `[HealthChange]` 뒤에 내 사망 줄이 찍혀야 한다. 제3자 화면에는 양쪽 objectId와 킬러 무기. `weaponId`가 0이나 `미스폰`으로 나오면 T8의 무기 추적부터 볼 것
- **(2026-08-21) T9 컴파일 + 검증** — ① 1/2 전환이 **한 박자 뒤에** 반영되는지(예측 없음이라 정상) ② **주무기 해제 시 보조무기가 손에 들리는지**(T9-a. 서버와 어긋나면 사격이 통째로 무시된다) ③ 전환 대기 중 클릭이 발사되지 않는지 ④ 빈 슬롯 키에 패킷이 안 나가는지 ⑤ 버전 불일치 거부는 자연 재현이 어려우니 낡은 버전을 싣는 `TEMP:` 코드로 재동기화 경로를 한 번
- **(2026-08-21) T8 컴파일 + 2인 접속 검증** — ① A가 무기를 장착하면 B 화면에서 A 손에 무기가 생기는지(경로 1, 이번 작업의 실질 검증) ② A가 해제하면 B 화면 맨손 + **A 본인 화면도 맨손인지**(해제 버그 수정 확인) ③ 미스폰 플레이어가 있을 때 `C2DRequestSpawnByObjectId`가 **한 번만** 나가는지. 본인 롤백(경로 3)은 `C2DRequestSwitchWeapon`을 보내야 발생하므로 T9 전까지 검증 불가
- **(2026-08-21) 체력/실드 게이지 런타임 실측** — Fill 이미지 설정이 끝나 이제 눈으로 확인만 남았다. 순서: 매치 진입 직후 HP 만피·실드 0 → 방어구 착용 후 초당 100(=1%) 상승 → 피격 시 서버값으로 점프(`[HealthChange]` 로그와 대조) → 방어구 해제 시 0. 통과하면 T11·T12를 함께 종료 처리한다. **어긋나면 원인은 이제 스크립트 쪽이다** — Editor 설정이 유일한 잔여 변수였으므로 남는 후보는 `SyncHealthBarMax()` 호출 시점과 실드 예측 누적 로직
- **(2026-08-20) T14용 HP 보유 비플레이어 오브젝트 목록** — 서버 답변 대기 중. 이게 와야 `ICombatTarget` 구현 대상이 정해진다
- **(2026-08-20) `Poolable` 삭제 후 Unity 콘솔 확인** — MonoBehaviour라 프리팹·씬 오브젝트에 수동으로 붙어 있었다면 "Missing (Mono Script)" 경고가 뜬다. 코드가 붙이는 경로는 없었으므로 가능성은 낮다

### 서버 계약 (2026-08-21 proto 변경 2차 — 사망 유예)
- **사망해도 세션이 즉시 끊기지 않고 5초의 유예가 있다.** 기점은 `D2CNotifyPlayerKilled.victim_object_id == 내 objectId`이며, 이 패킷은 **피해자에게도 온다**(구 계약에서 바뀜). 사망 판정을 HP 0으로 하면 기점이 이중화된다
- **유예 중 하향은 전부 계속 온다**(관전 유지 가능), **상향은 하트비트를 뺀 전부가 버려진다.** 유예 길이는 어떤 패킷으로도 오지 않아 클라 상수로 둘 수밖에 없다
- **자기 캐릭터의 디스폰 통보는 오지 않는다** — 유예 동안 남겨두고 스스로 치우라는 뜻
- 게임 결과·인벤토리 반영·매칭 락 해제는 **사망하는 순간 이미 시작**된다. 유예 중 연결을 끊어도 결과가 달라지지 않는다
- 귀환 성공 통보의 ACK는 **세션이 언제 끊기느냐만** 좌우한다. 인벤토리 반출은 성공 시점에 확정된다

### 서버 계약 (2026-08-21 proto 변경 1차 — 무기)
- **'장착한 무기'와 '손에 든 무기'는 다른 개념이다.** 무기 슬롯 2개는 장착이고 손에 든 것은 하나뿐이며, `C2DRequestWeaponFire.weapon_dbid`는 손에 든 쪽이어야 한다. 어긋나면 서버가 발사를 **조용히 버린다**(에러 응답 없음). 추적 출처가 넷이라 한 묶음으로 볼 것 — 상세는 `Scenes/CLAUDE.md`의 '손에 든 무기'
- **내 장착·해제로 손에 든 슬롯이 바뀌는 경우는 통보가 없다.** 클라가 서버 규칙을 직접 들고 있어야 하는 유일한 상태다
- `D2CNotifyWeaponChanged`의 `inventory_version`은 남의 통보에서 `0xFFFFFFFF`다. `0`은 실재하는 버전(세션 시작값)이라 미설정으로 읽으면 안 된다 — `attacker_object_id`와 같은 함정

### 서버 계약 (2026-08-20 확인)
- **objectId는 한 게임 안에서 단조 증가하며 재사용되지 않는다. 죽은 오브젝트가 살아나지도 않는다.** proto에 문서화된 보장은 아니다. `_despawnedObjectIds`를 만료 없이 씬 수명 내내 들고 가는 설계의 근거이며, 서버 계약이 바뀌면 이 설계를 다시 봐야 한다(증상은 "특정 오브젝트가 끝까지 안 보임")

### 해소된 버그
- **`RequestSpawnIfUnknown()`의 자기 objectId 가드 누락** — (2026-08-21 #3)에서 해소. 나는 `_oppoPlayers`에 들어가지 않아, 킬러가 나인 킬 피드마다 **내 스폰을 서버에 요청**하게 된다. 기존 호출부 2곳이 호출 전에 자기 id를 걸러내고 있어 드러나지 않다가 세 번째 호출자에서 노출됐다 — 가드는 호출부가 아니라 헬퍼 안쪽에 있어야 한다
- **장착·해제 시 손에 든 무기가 어긋나던 문제** — (2026-08-21 #2)에서 해소. 두 번 틀렸다: 원래는 `currentWeapon != null`일 때만 `EquipWeapon`을 호출해 **해제해도 이전 무기가 손에 남았고**, (#1)에서 맨손으로 고친 것은 **서버 규칙(반대쪽 슬롯으로 옮김)과 어긋났다**. 서버가 보조무기를 들려준 상태에서 클라만 맨손이면 `weapon_dbid` 불일치로 사격이 통째로 무시된다. 이 경로는 **본인에게 통보가 오지 않아** 클라가 규칙을 직접 들고 있어야 한다
- **미스폰 objectId에 대한 스폰 요청 폭주** — (2026-08-21 #1)에서 해소. `UpdatePlayerStates()`가 10Hz로 새 reliable을 만들어 in-flight 32슬롯을 채우면 **무관한 다른 패킷이 덮어써져 조용히 유실된다**. 재전송 한도를 없앤 뒤(T2) 슬롯 상한을 지키던 장치가 없어져 생긴 노출이다. 새 통보 패킷을 붙일 때 `RequestSpawnIfUnknown()`을 쓰지 않고 직접 전송하면 같은 문제가 재발한다
- **디스폰된 플레이어의 유령 재스폰** — (2026-08-20 #1)에서 해소. 최초 조사 때는 `UpdatePlayerStates()`의 요청 억제 한 곳만 지목했으나, 실제로는 이미 보낸 요청의 늦은 응답이 `SpawnPlayerObject()`로 들어가는 경로가 더 위험해 **두 곳 모두** 막아야 했다

### 서버 변경분 반영 관련 (2026-08-19 조사)
- **`ExternalProtocol.cs`는 이미 새 proto 기준으로 재생성되어 있다** — `.gitignore` 대상이라 git diff에 안 잡히지만 `PktId` 35~40, `D2CNotifyWeaponChanged`, `C2DRequestSwitchWeapon`, `D2CNotifyHealthChange.AttackerObjectId` 스텁이 전부 존재. protoc 재실행 불필요
- **`0xFFFFFFFF`가 '없음', `0`은 실재 objectId** — `D2CNotifyHealthChange.attacker_object_id`, `D2CNotifyPlayerKilled.killer_object_id` 모두 proto3 기본값 0을 미설정으로 해석하면 오귀속이 된다
- **UDP 송신 경로는 전부 메인 스레드다** — 하트비트·재전송 모두 `Managers.Update()` → `UDPManager.OnUpdate()`에서 나가고, 워커 스레드는 미리 만들어진 바이트만 큐에서 꺼내 보낸다. 따라서 메인 스레드가 멈추면 수신 측 상태를 아무리 최신으로 유지해도 내보낼 주체가 없다. 수신 상태 갱신을 워커 스레드로 옮기는 최적화는 이 이유로 이득이 없다(T1에서 검토 후 기각)
- **남의 방어구·실드·HP는 어떤 패킷으로도 오지 않는다 (의도된 설계)** — `blueprint_id` 하나가 `ArmorSpec` 전체와 동치라 수치 노출이 되기 때문. 구 `D2CNotifyEquipmentChanged`의 `armor_id`가 삭제된 이유
- **HP 최대치도 서버가 보내지 않는다** — `MAX_HEALTH_POINT`(100000) 상수가 유일한 출처다. 이 때문에 `_currentHealthPoint`를 0으로 초기화하면 스폰 직후부터 사망으로 오판되어 실드 재생이 멈춘다(T12에서 발견). HP를 다루는 로직을 추가할 때 같은 함정을 주의할 것
- **실드 재생은 정수 누적으로 구현할 것** — 서버가 `(재생량 × 경과ms)`를 누적해 1000마다 1 회복하는 방식이라, 초당 실수 보간으로 바꾸면 값이 미세하게 어긋난다. 검증 지점이 피격 시점뿐이라 어긋남이 눈에 잘 띄지 않는다

### 귀환(Recall) 설계 확정 사항
- **2단계 프로토콜** — ① `C2DRequestRecall` → `D2CResponseRecall`(승인/거부) ② 승인 시 서버가 1초 간격 5회 검사 후 `D2CNotifyRecallResult`(성공/취소 + 사유)
- **사유 전달 범위가 단계별로 다르다 (의도된 설계)** — **요청 거부**(`D2CResponseRecall`)는 사유를 알리지 않고 bool만. **진행 중 취소**(`D2CNotifyRecallResult`)는 `RecallResultReason`으로 사유를 알린다
- **복수 요청 허용 안 함** — 서버가 멱등성으로 처리, 클라도 `IngameScene._recallRequested`로 막는다. 스팟별이 아닌 씬 단위 플래그여야 다른 스팟 재요청 경로가 막힌다. 요청/응답 대조는 `recall_spot_index` 에코로 충분
- **응답 도달 시점에 씬은 항상 `IngameScene`** — 로딩 중 도달은 논리적으로 불가하므로 핸들러의 씬 가드는 방어 코드일 뿐, 별도 pending 처리 불필요
- **귀환 스팟은 작은 프롭** — 상호작용 판정이 `hit.collider.transform.position` 기준 2m 고정(`PlayerController.CheckInteractable`)이라 넓은 트리거 존은 성립하지 않는다. 영역형으로 바꾸려면 `InteractDistance` virtual화 + `hit.point` 기준 거리로 판정부 리팩터링 필요
- **귀환 스팟은 서버 스폰 대상이 아님** — `ObjectData`에 인덱스 필드가 없으므로 맵 씬에 직접 배치하고 `[SerializeField] _recallSpotIndex`로 부여
- **"실패 시 서버가 반드시 알린다"는 전제 + 워치독** — 전제 자체는 유지하되, 전제가 깨졌을 때 복구 불가가 되지 않도록 클라에 타임아웃을 둔다. `SESSION_LOST`·`SERVER_INTERNAL`은 사유 자체가 통지 경로의 불안정을 가리키고, 서버가 이미 멱등 처리하므로 안전장치 비용은 사실상 0. 반대로 없을 때의 손실은 "그 판 탈출 영구 불가"로 최대치라 비용이 비대칭
- **알려진 한계** — 타임아웃 후 재요청 시 1차 시도의 늦은 응답을 2차 결과로 오인할 수 있다(`recall_spot_index` 에코만으로는 시도 구분 불가). 타임아웃을 10초로 넉넉히 잡아 실무상 회피 중. 엄밀히 하려면 `C2DRequestWeaponFire`의 `FireSequence`처럼 요청 시퀀스를 proto에 추가해야 함
