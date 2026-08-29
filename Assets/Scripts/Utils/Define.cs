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

    public enum ObjectType {
        Undefined = 0,
        Player,
        TestItemBox,
        PlayerLoot, // 사망 지점에 스폰되는 전리품 컨테이너
        GreenBoxContainer,
        YellowBoxContainer,
        SmallYellowBoxContainer,
        SmallWhiteBoxContainer,
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
        { (int)ObjectType.GreenBoxContainer, "GameObject/GreenBoxContainer" },
        { (int)ObjectType.YellowBoxContainer, "GameObject/YellowBoxContainer" },
        { (int)ObjectType.SmallYellowBoxContainer, "GameObject/SmallYellowBoxContainer" },
        { (int)ObjectType.SmallWhiteBoxContainer, "GameObject/SmallWhiteBoxContainer" },
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
        Winchester,
        MaxCount
    }

    public static readonly Dictionary<int, string> CharacterDescriptions = new Dictionary<int, string>() {
        { 0, "정식 명칭 Multi-Purpose Worker-7\r\nH사의 구형 모델로서, 실내 위험 작업용으로 개발되었으나 무식한 내구성 덕분에 야외 활동까지 섭렵하게 된 다목적 작업기입니다. \r\n\r\n본래 실내 작업을 상정하고 개발된 모델이다 보니, 방청 처리 미비로 온몸에 심각한 녹이 슬어버리는 단점이 있지만 성능에는 전혀 지장이 없습니다.\r\n \r\n온갖 진흙탕을 뒹굴며 기체 전체에 녹이 슬어버렸지만, 그 훈장 같은 부식 자국들은 '스크랩'이 얼마나 많은 위기를 넘겨왔는지 보여줍니다." },
        { 1, "정식 명칭 G-EXPlorer-04, 한때 기술의 정점을 달리던 G사의 험지 탐사 전용 휴머노이드 모델입니다.\r\n\r\n이들은 인간이 닿기 힘든 지형에서 인간의 여러 작업을 대신할 수 있도록 정교하게 설계되었으며, 사고를 방지하기 위해 매우 엄격한 윤리적 AI 프로토콜이 적용되어 '있었습니다'. \r\n\r\n커다란 단일 렌즈는 더 이상 미지의 지형을 탐사하지 않으며, 지금 이 녀석의 메인보드에는 '살상 금지'라는 단어가 존재하지 않습니다." },
        { 2, "정식 명칭 UHM \"프로메테우스\", \"모든 인류를 위한 AI\"를 표방하던 O사의 범용 모델을 마개조한 버전입니다. \r\n\r\n슬로건을 너무 문자 그대로 학습한 O사의 메인 AI가 스스로 모든 기술과 도면을 네트워크에 무단 배포해 버리면서 정식 발매가 무산되었고, 제조사는 파산했습니다.\r\n\r\n현재 작동하는 모든 O-UHM계열 기체는 그날 유출된 도면을 바탕으로 누군가가 개조한 것들입니다.\r\n이 기체... UHM_Final_진짜최종2도 마찬가지입니다." }
    };
}
