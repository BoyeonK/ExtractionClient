# 프로젝트 진행 상황

> 최종 수정: 2026-05-12
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI

### 네트워크
- [x] (2026-05-02 #3) Network CLAUDE.md에 Protobuf 타입 격리 규칙 추가
- [x] (2026-05-02 #4) `GameSceneContext.OnStaticObjectsSpawned()` 추가 — 정적 오브젝트 스폰 완료 후 SpawnPoint·StaticObjects·인덱스 추적 상태 전체 초기화
- [x] (2026-05-02 #5) Heartbeat 구현 — `UDPManager.OnUpdate()`에서 1초 간격으로 `C2DHeartBeat`(Unreliable) 전송, `Handle_D2CHeartBeat` 핸들러 등록
- [x] (2026-05-06 #0) 프로토콜 변경사항 적용 — `External_Protocol.proto` / `External_Unity_Object.proto` 수정, `Define.cs` 업데이트
- [x] (2026-05-10 #0) 매치메이킹 `/start` 요청에 `characterType` 추가 — `GameReadyRequest` 스펙 반영, `MatchStartRequest`·`StartMatchCall`·`TryMatchMake` 수정, `_selectedCharacterType`을 `LobbyScene`으로 이동 및 `SetCharacterType()` 추가
- [x] (2026-05-12 #0) 매치 상태 응답 `mapId` 필드 추가 — `MatchStatusData`에 `mapId` 추가, `HTTPManager`에 `MapId` 프로퍼티 추가 및 SUCCESS 분기에서 저장

### 기타
- [x] (2026-05-06 #1) 압축된 Quaternion값 복원 로직 — `PacketHandler.cs`에 Quaternion 역직렬화·복원 구현
- [x] (2026-05-06 #2) `ObjectData` struct 기반 gameObject 생성 메서드 정의 — `ResourceManager`에 생성 메서드 추가, `TestIngame.cs` → `TestIngameScene.cs` 리네임, `TestIngameScene` 프리팹 추가
- [x] (2026-05-04 #0) StaticObject 로딩·동기화 테스트 환경 구성 — `TestItemBoxController` 추가, TestIngame 씬에 오브젝트 배치
- [x] (2026-05-07 #0) `PlayerObject` 모든 Animation 등록 및 적용 — 애니메이션 클립 연결 및 상태 머신 설정 완료

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
5. 교체된 Scene의 Init() 함수에서 C2DRequestBluePrint에서 받아온 친구들 까지 포함해서 그려냄 ← **진행 중** (ObjectData 기반 생성 메서드 정의 완료, PlayerObject 애니메이션 완료, 실제 Init() 연결 필요)
6. Init함수가 실행된 이후, 서버에 Scene 로딩 완료됬음을 알려줌과 동시에 동적인 정보를 다시 요청.
    - C2DRequestSpawnMe

---

## 다음 작업 우선순위 (제안)

1. **GameScene Init() 완성** — ObjectData 생성 메서드를 Init()에 연결, Blueprint 데이터 기반 정적 오브젝트 실제 렌더링
2. **C2DRequestSpawnMe 전송** — Init() 완료 후 서버에 로딩 완료 알림 및 동적 정보 요청
3. **설정 UI 콘텐츠 채우기** — General / Graphic / Audio 탭 실제 항목 구현
