# 프로젝트 진행 상황

> 최종 수정: 2026-08-21
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### 네트워크/UI
- [x] (2026-08-19 #2) 재전송 한도 폐기 + 수신 워치독 도입 (server-sync T2) — ACK 실패 횟수로 연결 생사를 판정하던 방식을 폐기. `MAX_RETRY`와 `CollectRetransmits`의 `out shouldDisconnect` 제거로 연결이 살아있다고 보는 동안 무한 재시도. 판정은 `PacketHandler.LastRecvSec`(서명 검증 통과분만 기록) 기준 `RECV_TIMEOUT_SEC`(10초) 무수신으로 단독 이관. 워치독 시드는 `SetSessionVariable`에서 — 0으로 두면 `Time.realtimeSinceStartup`과 비교되어 접속 즉시 오탐. RTO 백오프는 미도입(버퍼 재사용으로 CPU/GC 비용 0, 실사용 in-flight 0~2개). 재전송 한도가 겸하던 in-flight 상한이 사라져 슬롯 덮어쓰기 경고를 런타임 에러 로그로 승격
- [x] (2026-08-19 #4) D2CNotifyHealthChange.attacker_object_id 반영 (server-sync T11) — 핸들러 파싱 + `HandleHealthChange()` 시그니처 확장. `NO_ATTACKER_OBJECT_ID`를 `PLAYER_OBJECT_ID`와 **별도 상수로** 신설 — 값은 같아도 의미가 다르고(가해자 없음 vs 내 인벤토리), `0`은 실재 objectId라 미설정으로 읽으면 오귀속이 된다. `_currentHealthPoint`/`_currentShieldPoint` 신설로 서버 절대값 보관(T12가 이어받음), 교전 상대 추적은 `_lastAttackerObjectId` + `ATTACKER_TRACK_DURATION`(5초) 만료 창으로 `LastAttackerObjectId`/`HasRecentAttacker` 노출(T10 킬 피드·T15 킬러 표기에서 재사용). 피격 방향 각도 산출은 표시 UI가 없어 `OPTION:` 보류, `reason`은 `REASON_ITEM_HEAL` 발생 경로가 아직 없어 enum 승격 없이 `int` 유지
- [x] (2026-08-19 #5) 실드 재생 로컬 예측 (server-sync T12) — **구현 완료 / 런타임 미검증**. 전용 통보 패킷이 없어 클라가 서버 공식 그대로 예측: `UpdateShieldRegen()`이 `(재생량 × 경과ms)`를 누적해 1000마다 1 회복(실수 보간 아닌 정수 회복이라 서버와 어긋나지 않음), 중단 조건은 사망·방어구 미착용·상한 도달. 리셋 3곳 — 피격 수신(서버 절대값 + 누적기 0) / 방어구 착용·해제·교체 전부(`ResetShieldPrediction()`) / 스폰 시 필드 초기값. 계획 외 수정 2건: `_currentHealthPoint` 초기값을 `MAX_HEALTH_POINT`로(0이면 첫 피격 전까지 사망 오판으로 재생이 아예 안 돎, 이 과정에서 하드코딩 `100000f`를 상수화), `SyncHealthBarMax()`를 `SyncInventoryUI()`의 UI null 가드 앞으로 이동(전투 예측이 UI 오브젝트 존재 여부에 묶여 있었음). 이 시점엔 게이지가 미연결이라 실측 불가 → 아래 `(2026-08-20 #0)`에서 해소
- [x] (2026-08-20 #0) 체력 게이지 연결 + 표시 버그 2건 수정 (server-sync T12 마무리) — `IngameHealthBarUI.Init()`(`HealthBarBg/HealthBarFill`, `ArmorBarBg/ArmorBarFill`) 신설 후 `IngameScene.Init()`에서 호출해 영구 null 상태를 해소. 연결 과정에서 발견한 2건도 수정: ① **초기 게이지 값이 한 번도 안 밀렸다** — `SetHP`/`SetArmor` 호출부가 피격·재생 틱뿐이라 첫 피격 전까지 프리팹에 저장된 `fillAmount`가 그대로 보였다. `SyncHealthBarMax()`에서 최대치 직후 현재값도 밀도록 추가(최대치 → 현재값 순서여야 `SetArmor`가 최대 실드 0 가드에 안 걸림) ② **최대 실드가 0이 될 때 실드 바가 안 비워졌다** — `SetArmor()`의 `_maxShield <= 0f` 조기 반환 탓에 벗기 직전 `fillAmount`가 남았다. 반환 대신 `fillAmount = 0`으로 교체. 조건은 '방어구 교체'가 아니라 최대 실드가 0이 되는 것이며(해제 / 스펙 미등록 방어구 / equip 분기에 빈 슬롯이 소스), **정상 교체는 새 방어구 최대치가 들어와 원래부터 문제없다**
- [x] (2026-08-20 #1) 플레이어 디스폰 처리 + 유령 재스폰 차단 (server-sync T3 일부·T7) — `D2CDespawnPlayerObject`(35) 등록 + `Handle_D2CDespawnPlayerObject` + `IngameScene.DespawnPlayerObject()`. **지금까지 다른 플레이어가 사라지는 경로 자체가 없어** 죽거나 탈출해도 캐릭터가 맵에 영원히 남아 있었다. `DespawnReason`별 연출은 에셋이 없어 `TODO:`로 보류하고 사유와 무관하게 즉시 제거. **차단 지점이 두 곳이라는 것이 핵심** — 문서가 지목한 `UpdatePlayerStates()`의 요청 억제만으로는 부족하고, 이미 보낸 `C2DRequestSpawnByObjectId`의 응답이 디스폰보다 늦게 도착해 `SpawnPlayerObject()`가 되살리는 경로가 더 위험하다. `_despawnedObjectIds`(만료 없는 씬 수명 `HashSet<uint>`, 플레이어·비플레이어 공용)로 양쪽을 막았다. 만료 창을 쓰지 않은 이유는 창보다 늦게 오는 패킷이 뚫기 때문이며, 재전송 한도를 없앤 뒤로 그 경로가 넓어졌다. T3은 5개 일괄 등록 대신 **구현과 함께 하나씩 등록**하는 방침으로 변경 — 빈 스텁 등록은 패킷을 조용히 삼키지만 미등록은 경고 로그로 드러난다
- [x] (2026-08-20 #3) 비플레이어 오브젝트 스폰·디스폰 배선 (server-sync T4·T5·T6) — `_sceneObjects`(objectId → `GameObjectController`) 레지스트리 신설 + `D2CNotifySpawnObject`(36)·`D2CNotifyDespawnObject`(39) 처리. **T4의 실제 작업은 딕셔너리 추가가 아니라 스폰 경로 일원화였다** — `Handle_D2CResponseSpawnByObjectId`가 씬을 거치지 않고 `Managers.Resource.InstantiateFromObjectDataStruct()`를 직접 부르고 있어, 그대로 뒀다면 등록되지 않은 오브젝트가 생겨 레지스트리에 구멍이 났다. `IngameScene.SpawnObject()`를 유일한 스폰 경로로 삼고 호출부 3곳(정적·동적 초기 스폰, 지연 스폰 응답)을 전부 라우팅해 중복 검사·디스폰 차단·매핑 검사를 한곳에 모았다. `D2CNotifySpawnObject`는 `D2CResponseSpawnByObjectId`와 페이로드가 같아 변환부를 `PacketHandler.ToObjectData()`로 분리해 공용화. 매핑 없는 `object_type`은 빈 경로 로드 실패로 조용히 묻히던 것을 타입 번호·objectId를 찍는 에러 로그로 교체(`Undefined`는 키가 있어도 경로가 null이라 함께 걸러냄). **보류돼 있던 판단 확정**: 이미 파괴된 컨테이너에 `C2DCloseContainer`를 보내지 않는다 — 서버 통보를 분리해 `CloseContainerLocal()` 신설
- [x] (2026-08-21 #0) 체력/실드 게이지 Fill 이미지 Editor 설정 (server-sync T12 렌더 경로) — `HealthBarFill`/`ArmorBarFill`의 Image Type을 `Filled`로 맞추고 스프라이트를 신규 `Assets/Resources/White_Square.png`(Sprite, border 0)로 지정. **코드 변경 없음** — `(2026-08-20 #0)`에서 "값은 정상인데 그림만 안 바뀌던" 원인이 이것 하나였다. `fillAmount`는 Type이 `Filled`일 때만 동작하고 `Simple`/`Sliced`면 대입이 **에러 없이 무시된다**. 씬 오버라이드에는 두 Image 중 한쪽에만 `m_Type`이 실렸는데, 나머지 하나는 프리팹 기본값이 이미 `Filled`라 정상이다. 이 제약은 앞으로 추가될 게이지에도 그대로 적용되므로 `UI/CLAUDE.md`의 `IngameHealthBarUI` 항목에 상시 규칙으로 남겼다. **런타임 실측은 아직** — 아래 '확인 필요' 참조
- [x] (2026-08-21 #1) 무기 변경 통보 처리 + 지연 스폰 요청 1회 제한 (server-sync T8) — `D2CNotifyWeaponChanged`(37) 등록 + `IngameScene.HandleWeaponChanged()`. **도착 경로가 문서에 적힌 둘이 아니라 셋이었다** — 남의 무기 전환뿐 아니라 남의 장착·해제(`C2DRequestEquipItem` 슬롯 0/1 성공)도 이 패킷으로 오므로, T9(무기 전환 송신) 없이도 이 작업이 필요했다(지금까진 남이 무기를 주워도 스폰 시점 외형이 그대로였다). 분기는 디스폰 차단 → 본인 롤백 → 남 외형 갱신 → 미스폰 시 스폰 요청 순. **본인 objectId로 오는 것은 항상 거부다**(성공은 룸의 나머지에게만 간다) → 성공/실패 분기 불필요. `_spawnCompleted` 가드를 둔 이유는 `_myObjectId` 초기값 `0`이 실재 objectId라 스폰 전엔 비교가 성립하지 않기 때문. 롤백은 슬롯 정보가 없어 주/보조 `item_id` 중 **한쪽만 일치할 때만** 확정(동률이면 외형·스펙이 같아 유지) — T9에서 `target_slot` 대조로 승격할 자리. **함께 고친 것 2건**: ① `RequestSpawnIfUnknown()`·`_pendingSpawnRequests` 신설 — `UpdatePlayerStates()`가 미스폰 objectId를 볼 때마다 **10Hz로 새 reliable을 만들고 있었다**. 재전송이 아니라 매번 새 시퀀스라 응답이 3초만 늦어도 in-flight 32슬롯이 차고, 넘치면 아직 ACK되지 않은 **다른** 패킷(장착·귀환 요청)이 덮어써져 조용히 유실된다. 요청은 reliable이라 한 번이면 충분하므로 objectId당 1회로 제한하고 판정을 헬퍼 하나로 모았다(전송 지점도 이제 이 함수뿐) ② 무기 **해제** 시 `ApplyEquipItem`이 `EquipWeapon` 호출을 건너뛰어 손에 든 무기가 남던 버그 — 서버는 남들에게 맨손을 통보하므로 T8을 붙이는 순간 화면이 어긋난다. **Unity 컴파일·런타임 모두 미검증**

### 매니저/리소스
- [x] (2026-08-20 #2) 오브젝트 풀링 코드 전면 제거 — `PoolManager.cs`·`Poolable.cs`(+`.meta`) 삭제, `Managers.cs`의 `_pool`/`Pool`/`_pool.Init()`/`Pool.Clear()` 제거, `ResourceManager`의 `Instantiate()`·`Destroy()` 분기와 `Load<T>()`의 GameObject 분기 전체 제거(풀 조회가 유일한 목적이라 이름 추출까지 통째로 죽은 코드였음 → `Resources.Load<T>` 한 줄로 축약). **동작 변화 없음** — `Poolable`을 어디에도 부착하지 않아 세 분기 모두 항상 false로 흘렀고 `GetOriginal()`은 항상 null이었다. 풀링이 필요해지면 별도 방식으로 새로 만들 예정. `Util.GetOrAddComponent`는 `UIManager` 등이 쓰므로 유지

### 문서/설정
- [x] (2026-08-19 #3) 미완성 코드 주석 마커 카테고리화 — `TODO:`(미구현) / `TEMP:`(테스트용으로 막아둔 것) / `OPTION:`(없어도 무방한 개선) 3분류를 정의하고 기존 주석 23개를 재분류·표기 통일(`//TODO :` 등 혼재 → `// MARKER: `). 귀환 성공·취소 처리는 테스트 목적이 아닌 미구현이므로 TEMP→TODO로 이동. 코드와 어긋난 TODO 2건(`UI_Login`의 로그인 시도, `LobbyScene`의 게임 씬 로드 — 둘 다 이미 구현되어 있었음) 삭제. 규칙 표는 루트 `CLAUDE.md` 컨벤션 5번에 추가

---

## 다음 작업 우선순위 (제안)

> **서버 변경분(2026-08-12 이후) 반영 작업은 별도 문서로 분리** — `server-sync-todo.md` (T1~T17) 참조.
> proto의 `[작업사항]` 주석을 추적한 임시 문서이며, 전 항목 반영 후 이 파일로 이관하고 삭제할 것.
> 아래 목록과의 대응: 2번(귀환 최종 결과 실처리) = T15, 5번(워치독 제거 검토) = T15에 종속

1. **서버 변경분 반영 (`server-sync-todo.md`)** — T1·T2·T4~T8·T11 완료, T12는 코드·Editor 작업이 끝나고 **런타임 실측만 남음**(T3은 구현과 함께 하나씩 등록하는 방침으로 진행 중). 남은 것은 T9(무기 전환 송신 + 1/2 키 바인딩) → T10(킬 피드) → T15(귀환 후처리) → T16·T17. **T13은 시신 프리팹 제작 대기, T14는 서버 목록 대기**
2. **귀환 최종 결과 실처리 + 연결 끊김 처리** — `HandleRecallResult`의 TODO를 실제 처리로 교체. 성공 시 탈출 연출·씬 전환 + 잠금 유지(이미 맵을 떠나므로 해제하면 전환 지연 중 재요청 가능). 취소 시 `reason`별 분기(`OUT_OF_ZONE`·`SERVER_INTERNAL`은 재시도 허용, `PLAYER_DEAD`·`SESSION_LOST`는 각 흐름에 위임). **연결 끊김도 같은 출구가 필요하다** — 현재 `UDPManager.Disconnect()`는 소켓만 정리해 플레이어가 인게임 씬에 그대로 남는다. 매치 종료 화면을 만들 때 함께 처리할 것
3. **귀환 스팟 씬 배치** — 맵 씬에 귀환 단말기 오브젝트 배치, `RecallSpotController` 부착 후 인스펙터에서 `_recallSpotIndex`를 서버 테이블 값에 맞춤. 조준 레이가 맞아야 하므로 트리거가 아닌 일반 콜라이더 사용
4. **귀환 진행 중 UI 피드백** — 승인~결과 사이 5초 구간 표시(카운트다운 등). `InteractText`는 매 프레임 재조회되므로 `virtual` 프로퍼티화하면 동적 텍스트 전환 가능
5. **TEMP 귀환 워치독 제거 검토** — 서버 통지 신뢰성이 검증되면 `RECALL_TIMEOUT`/`_recallTimer` 및 `OnUpdate()`의 TEMP 블록 제거. 2번이 끝나기 전까지는 워치독이 정상 경로까지 떠받치므로 먼저 제거하지 말 것
6. **발사 이펙트 프리팹 준비 → 이펙트 구현** — 로컬 탄착 이펙트(`ProcessHit`), 수신 측 머즐 플래시/총성/탄착 이펙트(`HandleWeaponFireBroadcast`). 파티클·사운드 에셋이 `Resources/Prefabs/` 아래에 필요
7. **탄약 차감 주석 해제** — `Fire()` 내 `magazine.quantity--` 및 빈 탄창 가드. 테스트 완료 후 활성화
8. **EmptyAmmoFire() 구현** — 빈 탄창 사운드, 재장전 유도 UI
9. **인벤토리 열기/닫기 키바인딩** — Tab키로 MyInventory 토글 등 추가 입력 연결 (컨테이너 E/I키 닫기는 완료). 무기 전환 1/2 키는 T9에서 함께 처리
10. **실제 맵 씬에서 IngameScene 상속 완성** — `IngameScene`을 상속하는 맵별 씬 컴포넌트 구현
11. **설정값 실제 적용** — 해상도/창모드/FOV 변경이 `Screen.SetResolution()`, `Camera.fieldOfView` 등에 반영되도록 구현

---

## 메모

### 확인 필요
- **(2026-08-21) T8 컴파일 + 2인 접속 검증** — ① A가 무기를 장착하면 B 화면에서 A 손에 무기가 생기는지(경로 1, 이번 작업의 실질 검증) ② A가 해제하면 B 화면 맨손 + **A 본인 화면도 맨손인지**(해제 버그 수정 확인) ③ 미스폰 플레이어가 있을 때 `C2DRequestSpawnByObjectId`가 **한 번만** 나가는지. 본인 롤백(경로 3)은 `C2DRequestSwitchWeapon`을 보내야 발생하므로 T9 전까지 검증 불가
- **(2026-08-21) 체력/실드 게이지 런타임 실측** — Fill 이미지 설정이 끝나 이제 눈으로 확인만 남았다. 순서: 매치 진입 직후 HP 만피·실드 0 → 방어구 착용 후 초당 100(=1%) 상승 → 피격 시 서버값으로 점프(`[HealthChange]` 로그와 대조) → 방어구 해제 시 0. 통과하면 T11·T12를 함께 종료 처리한다. **어긋나면 원인은 이제 스크립트 쪽이다** — Editor 설정이 유일한 잔여 변수였으므로 남는 후보는 `SyncHealthBarMax()` 호출 시점과 실드 예측 누적 로직
- **(2026-08-20) T14용 HP 보유 비플레이어 오브젝트 목록** — 서버 답변 대기 중. 이게 와야 `ICombatTarget` 구현 대상이 정해진다
- **(2026-08-20) `Poolable` 삭제 후 Unity 콘솔 확인** — MonoBehaviour라 프리팹·씬 오브젝트에 수동으로 붙어 있었다면 "Missing (Mono Script)" 경고가 뜬다. 코드가 붙이는 경로는 없었으므로 가능성은 낮다

### 서버 계약 (2026-08-20 확인)
- **objectId는 한 게임 안에서 단조 증가하며 재사용되지 않는다. 죽은 오브젝트가 살아나지도 않는다.** proto에 문서화된 보장은 아니다. `_despawnedObjectIds`를 만료 없이 씬 수명 내내 들고 가는 설계의 근거이며, 서버 계약이 바뀌면 이 설계를 다시 봐야 한다(증상은 "특정 오브젝트가 끝까지 안 보임")

### 해소된 버그
- **무기 해제 시 손에 든 무기가 남던 문제** — (2026-08-21 #1)에서 해소. `ApplyEquipItem`이 `currentWeapon != null`일 때만 `EquipWeapon`을 호출했다. 서버는 남들에게 맨손을 통보하므로 본인 화면만 어긋난다
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
