# 프로젝트 진행 상황

> 최종 수정: 2026-05-22
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI

### 네트워크
- [x] (2026-05-20 #1) `D2CFullInventorySync` 패킷 핸들러 — `PacketHandler`에 핸들러 등록·구현. Protobuf `InventorySlot` → `InventoryItem` 변환 후 `IngameScene.Inventory.ApplyFullSync()` 호출
- [x] (2026-05-21 #0) `PlayerController.EquipWeapon(int weaponId)` 구현 — weaponId로 `Resources/Prefabs/Weapons/Weapon_{id}_{name}` 프리팹을 캐시 딕셔너리에서 조회·장착. `_equippedWeaponGo`로 무기만 추적하여 WeaponSocket 내 LeftHandIKTarget 등 기존 자식에 영향 없이 교체
- [x] (2026-05-21 #1) `IngameInventory` 무기 전환 기능 — `_emptySlotIdx`+`FindEmptySlotIdx()` 빈 슬롯 추적, `_isPrimaryWeaponApplyed` 현재 적용 무기 관리, `GetIngameScene()` 늦은 참조, `InitWeapon()` 초기 무기 장착(주무기 우선→보조무기 폴백), `ApplyWeapon(bool)` 주/보조무기 전환·`EquipWeapon` 연동. `IngameScene.PlayerController` 프로퍼티 추가
- [x] (2026-05-21 #2) 인게임 초기 무기 장착 자동화 — `IngameScene`에 `_itemLoaded`·`_weaponInitialized` 플래그 + `TryInitWeapon()` 추가. `_spawnCompleted`와 `_itemLoaded` 양쪽 모두 true인 시점에 1회만 `InitWeapon()` 실행. `PacketHandler.Handle_D2CFullInventorySync`에서 `_itemLoaded = true` + `TryInitWeapon()` 호출
- [x] (2026-05-21 #3) WeaponPrefabCache 공유화 — `_weaponPrefabCache`를 `PlayerController`에서 `IngameScene`으로 이동. lazy init getter(`WeaponPrefabCache`) 제공. `PlayerController`·`OppoPlayerController` 모두 공유 캐시 참조
- [x] (2026-05-21 #4) OppoPlayer 무기 장착 — `OppoPlayerController.EquipWeapon()` 추가, `PlayerSpawnData`에 `WeaponId` 필드 추가, `PacketHandler`에서 `pkt.WeaponId` 추출, `IngameScene.SpawnPlayerObject()`에서 스폰 시 `EquipWeapon()` 호출

### 기타

- [x] (2026-05-19 #2) `LobbySettingUI` Audio 탭 — 마스터/이펙트/음악 볼륨 슬라이더(0~100) 구현 + `SettingManager.SetVolume()`에 마스터 볼륨 곱 적용, `SetMasterVolume()` 호출 시 Effect/Bgm 즉시 재적용
- [x] (2026-05-19 #3) `LobbySettingUI` 세팅 변경 감지 — `HasChanges()` 메서드 추가(8개 설정값을 SettingManager와 비교), `OnClickApply`/`OnClickCancel`에 적용하여 변경 없으면 팝업 없이 바로 닫기
- [x] (2026-05-22 #0) `ItemType.Equipment` → `ItemType.Armor` 리네이밍 — enum 정의(`ItemTypeHelper.cs`) 및 참조 4개 파일(`UI_Shop`, `UI_Inventory`, `ISlot`) 일괄 변경

### 버그 수정

---

## 진행 중 / 미완성

### 매칭 성공시 씬 전환
/connect요청을 통해서 ip와 port를 받았을 경우
1. ~~workerThread를 살려내고 루프 작동. (ping 작동)~~ ← **완료**
    - workerThread내에서 ReliableFlag로 C2DHeartBeat전송, D2CHeartBeat로 응답 받음.
2. ~~Scene을 LoadingScene으로 변경하고, GameScene의 비동기 로딩 시작.~~ ← **완료**
3. ~~비동기 로딩 완료되었을 경우, C2DRequestBluePrint 전송~~ ← **완료**
4. ~~3의 패킷의 응답을 받았을 경우, 해당 내용을 역직렬화해서 보관하고 Scene교체 진행.~~ ← **완료**
    - `D2CResponseBlueprintSpawnPoint` / `D2CResponseBlueprintStaticObjects` 핸들러 구현, `SceneManagerEx.NextSceneContext`(`GameSceneContext`)에 누적 저장
5. 교체된 Scene의 Init() 함수에서 C2DRequestBluePrint에서 받아온 친구들 까지 포함해서 그려냄 ← **진행 중** (`IngameScene.Init()`에서 정적 오브젝트 스폰 연결 완료, 실제 맵 씬에서 `IngameScene` 상속 후 `RequestSpawnMe()` 호출 필요)
6. ~~Init함수가 실행된 이후, 서버에 Scene 로딩 완료됬음을 알려줌과 동시에 동적인 정보를 다시 요청.~~ ← **완료**
    - C2DRequestSpawnMe (`TestIngameScene.Start()`에서 호출, 응답 핸들러·`TryCompleteSpawnMe` 흐름·`SpawnMeAndStartGame()` 모두 구현 완료)

---

## 다음 작업 우선순위 (제안)

1. **실제 맵 씬에서 IngameScene 상속 완성** — `IngameScene`을 상속하는 맵별 씬 컴포넌트 구현
2. **설정값 실제 적용** — 해상도/창모드/FOV 변경이 `Screen.SetResolution()`, `Camera.fieldOfView` 등에 반영되도록 구현
