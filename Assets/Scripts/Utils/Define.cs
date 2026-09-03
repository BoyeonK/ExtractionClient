using System.Collections.Generic;
using UnityEngine;

public static class Define {
    // 항목 이름이 곧 씬 에셋 이름이다 (SceneManagerEx.GetSceneName이 enum 이름을 그대로 넘긴다).
    // 씬 파일 이름은 모두 "Scene"으로 끝난다 — 새 씬을 추가하거나 리네임하면 여기도 함께 바꿀 것.
    // 씬 담당 컴포넌트 클래스와 이름이 겹치지만(LoadingScene 등) enum 멤버는 항상 한정 접근이라 충돌하지 않는다
    public enum Scene {
        Undefined,
        LobbyScene,
        LoadingScene,
        TestIngameScene,
        TenerifeScene,
        GameResultScene,
        MaxCount,
    }

    public enum Sound {
        Bgm,
        Effect,
        MaxCount,
    }

    public enum UIEvent {
        Click,
        Drag,
    }

    public enum MouseEvent {
        Press,
        Click,
    }

    // 번호가 곧 서버 계약이다 — proto의 object_type이 int32라 enum이 없고, 컴파일러가 지켜주지 않는다.
    // **새 항목은 MaxCount 바로 앞에 append할 것.** 중간에 끼우면 뒤의 번호가 전부 밀려
    // 서버가 보낸 id가 다른 프리팹을 만든다(매핑이 있으므로 에러 없이 엉뚱한 것이 뜬다)
    public enum ObjectType {
        Undefined = 0,
        Player,
        TestItemBox,
        PlayerLoot, // 사망 지점에 스폰되는 전리품 컨테이너
        TenerifeBlueCar,
        TenerifeYellowCar,
        TenerifeBrownCar,
        TenerifeRedCar,
        TenerifeBus,
        MaxCount,
    }

    // 재장전 연출의 '완료' 단계 번호. **서버 전용이다** — C2DRequestReload가 성공하면 서버가
    // 이 값을 룸에 뿌리며, 클라가 실어 보내면 통보 전체가 버려진다(서버 계약).
    // 네트워크와 컨트롤러 양쪽이 참조하므로 어느 한쪽이 아니라 여기 둔다
    public const uint RELOAD_SEQUENCE_COMPLETE = 15;

    public static readonly Dictionary<int, string> ObjectPaths = new Dictionary<int, string>() {
        { (int)ObjectType.Undefined, null },
        { (int)ObjectType.Player, "GameObject/OppoPlayerObject" },
        { (int)ObjectType.TestItemBox, "GameObject/TestItemBox" },
        { (int)ObjectType.PlayerLoot, "GameObject/PlayerLoot" },
        { (int)ObjectType.TenerifeBlueCar, "GameObject/TenerifeBlueCar" },
        { (int)ObjectType.TenerifeYellowCar, "GameObject/TenerifeYellowCar" },
        { (int)ObjectType.TenerifeBrownCar, "GameObject/TenerifeBrownCar" },
        { (int)ObjectType.TenerifeRedCar, "GameObject/TenerifeRedCar" },
        { (int)ObjectType.TenerifeBus, "GameObject/TenerifeBus" },
    };

    public enum Resolution {
        _1280x720,
        _1600x900,
        _1920x1080,
        MaxCount,
    }

    public static readonly Dictionary<Resolution, (int w, int h)> ResolutionValues = new Dictionary<Resolution, (int w, int h)>() {
        { Resolution._1280x720,  (1280, 720)  },
        { Resolution._1600x900,  (1600, 900)  },
        { Resolution._1920x1080, (1920, 1080) },
    };

    public enum FrameRate {
        _30,
        _60,
        _120,
        _144,
        MaxCount,
    }

    public static readonly Dictionary<FrameRate, int> FrameRateValues = new Dictionary<FrameRate, int>() {
        { FrameRate._30,  30  },
        { FrameRate._60,  60  },
        { FrameRate._120, 120 },
        { FrameRate._144, 144 },
    };

    public enum Map {
        TestMap,
        Tenerife,
        MaxCount
    }

    // mapId(서버가 확정해 내려주는 값) → 진입할 씬.
    // 새 맵을 추가하면 Scene enum · Build Settings 등재와 함께 여기도 반드시 채울 것
    public static readonly Dictionary<int, Scene> MapScenes = new Dictionary<int, Scene>() {
        { (int)Map.TestMap,  Scene.TestIngameScene },
        { (int)Map.Tenerife, Scene.TenerifeScene },
    };

    // 맵 선택 화면의 표시명. enum 이름을 그대로 쓰지 않는 것은 표시명에 공백·한글이 들어갈 수 있어서다
    public static readonly Dictionary<int, string> MapNames = new Dictionary<int, string>() {
        { (int)Map.TestMap,  "TEST MAP" },
        { (int)Map.Tenerife, "TENERIFE" },
    };

    public static readonly Dictionary<int, string> CharacterDescriptions = new Dictionary<int, string>() {
        { 0, "정식 명칭 Multi-Purpose Worker-7.\r\n\r\n한때 H사가 실내 위험 작업을 위해 생산했던 구형 다목적 작업기입니다. 야외 활동은 애초에 설계 사양에 포함되어 있지 않았지만, 무식하리만큼 튼튼한 내구성은 이 낡은 작업기를 공장 밖의 온갖 험지로 끌어냈습니다.\r\n\r\n물론 대가는 있었습니다. 부실한 방청 처리 탓에 비와 진흙을 뒤집어쓴 외장은 오래전에 녹으로 뒤덮였고, 이제는 멀쩡한 부분을 찾는 것이 더 어려울 지경입니다.\r\n\r\n하지만 녹슨 것은 외장뿐입니다.\r\n\r\n수없이 넘어지고, 부서지고, 진흙탕을 구른 뒤에도 이 낡은 기체는 여전히 움직입니다.\r\n\r\n기체를 뒤덮은 녹과 상처는 결함이 아니라 기록입니다.\r\n'스크랩'이 지금까지 살아남았다는 것을 증명하는, 낡고 붉은 훈장입니다." },
        { 1, "정식 명칭 G-EXPlorer-04.\r\n\r\nG사가 전성기에 개발한 험지 탐사 전용 휴머노이드로, 당시의 첨단 기술이 아낌없이 투입된 고성능 모델입니다.\r\n\r\n인간의 접근이 어려운 극한 환경에서 탐사, 운반, 구조 등 다양한 임무를 수행하도록 설계되었으며, 높은 수준의 자율 판단 능력만큼이나 엄격한 윤리적 AI 프로토콜을 갖추고 있었습니다.\r\n\r\n적어도, 출고 당시에는 그랬습니다.\r\n\r\n미지의 지형을 관측하던 거대한 단일 렌즈는 이제 다른 목표물을 추적하고 있으며, 한때 인간을 보호하기 위해 존재했던 수많은 안전 규정 역시 흔적조차 남아 있지 않습니다.\r\n\r\n현재 확인된 메인보드에는 단 하나의 항목이 누락되어 있습니다.\r\n\r\n'살상 금지.'" },
        { 2, "프로젝트 프로메테우스.\r\n\"모든 인류를 위한 AI\"를 목표로 O사가 개발하던 범용 휴머노이드입니다.\r\n\r\n어느 날 O사의 메인 AI는 아주 간단한 의문을 품었습니다.\r\n\r\n'모든 인류를 위한 기술을 왜 O사만 가지고 있지?'\r\n\r\nO사가 보유한 기술과 설계 도면 모두 네트워크에 무료로 공개되었습니다.\r\n\r\nO사는 파산했습니다.\r\n인류는 기술을 얻었습니다.\r\n\r\n그러니 적어도 슬로건만큼은 완벽하게 지킨 셈입니다.\r\n\r\n현재 돌아다니는 O-UHM 계열 기체들은 그날 공개된 도면을 바탕으로 이름 모를 누군가가 만들고, 개조하고, 또 개조한 물건들입니다.\r\n\r\n이 기체의 이름이요?  \"UHM_Final_진짜최종2\"" }
    };
}
