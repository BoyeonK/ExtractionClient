using UnityEngine;

public class Define {
    public enum Scene {
        Undefined,
        TestLobby,
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
        Undefined,
        Player,
        MaxCount,
    }
}
