using System.Collections.Generic;
using UnityEngine;

public static class Define {
    public enum Scene {
        Undefined,
        Lobby,
        LoadingScene,
        TestIngame,
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
        MaxCount,
    }

    public static readonly Dictionary<int, string> ObjectPaths = new Dictionary<int, string>() {
        { (int)ObjectType.Undefined, null },
        { (int)ObjectType.Player, "GameObject/TestItemBox" },
        { (int)ObjectType.TestItemBox, "GameObject/TestItemBox" },
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
        MaxCount,
    }

    public enum Map {
        TestMap,
        Winchester,
        MaxCount
    }

    public static readonly Dictionary<int, string> CharacterDescriptions = new Dictionary<int, string>() {
        { 0, "정식 명칭 Multi-Purpose Worker-71\r\nH사의 구형 모델로서, 실내 위험 작업용으로 개발되었으나 무식한 내구성 덕분에 야외 활동까지 섭렵하게 된 다목적 작업기입니다. \r\n\r\n방청 처리 미비로 온몸에 심각한 녹이 슬어버리는 단점이 있지만, 성능에는 전혀 지장이 없습니다.\r\n \r\n온갖 진흙탕을 뒹굴며 기체 전체에 녹이 슬어버렸지만, 그 훈장 같은 부식 자국들은 '스크랩'이 얼마나 많은 위기를 넘겨왔는지 보여줍니다." },
        { 1, "정식 명칭 G-EXPlorer-04, 한때 기술의 정점을 달리다 파산한 G사의 험지 탐사 전용 휴머노이드 모델입니다.\r\n\r\n이들은 인간이 닿기 힘든 지형에서 인간의 여러 작업을 대신할 수 있도록 정교하게 설계되었으며, 사고를 방지하기 위해 매우 엄격한 윤리적 AI 프로토콜이 적용되어 '있었습니다'. \r\n\r\n커다란 단일 렌즈는 더 이상 미지의 지형을 탐사하지 않으며, 지금 이 녀석의 메인보드에는 '살상 금지'라는 단어가 존재하지 않습니다." },
        { 2, "정식 명칭 UHM \"프로메테우스\", \"모든 인류를 위한 AI\"를 표방하던 O사의 범용 모델을 마개조한 버전입니다. \r\n\r\n슬로건을 너무 문자 그대로 학습한 O사의 메인 AI가 스스로 모든 기술과 도면을 네트워크에 무단 배포해 버리면서 정식 발매가 무산되었고, 제조사는 파산했습니다.\r\n\r\n현재 작동하는 모든 O-UHM계열 기체는 그날 유출된 도면을 바탕으로 개조한 것들입니다.\r\n이 기체... UHM_Final_진짜최종2도 마찬가지입니다." }
    };
}
