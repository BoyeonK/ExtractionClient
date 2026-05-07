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
        Undefined = -1,
        Player,
        TestItemBox,
        MaxCount,
    }

    public static readonly Dictionary<int, string> ObjectPaths = new Dictionary<int, string>() {
        { (int)ObjectType.Undefined, null },
        { (int)ObjectType.Player, "GameObject/TestItemBox" },
        { (int)ObjectType.TestItemBox, "GameObject/TestItemBox" },
    };

    public enum Map {
        TestMap,
        Winchester,
        MaxCount
    }
}
