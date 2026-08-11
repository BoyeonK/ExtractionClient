using UnityEngine;

// 맵 씬에 직접 배치되는 귀환 스팟. 서버 스폰(ObjectData) 대상이 아니므로
// _objectId는 사용하지 않고, 맵별 귀환 영역 테이블의 인덱스를 인스펙터에서 부여한다.
public class RecallSpotController : InteractableGameObjectController {
    [SerializeField] private uint _recallSpotIndex;

    public override void Init() {
        base.Init();
        _interactText = "귀환하기";
        _onInteract += RequestRecall;
    }

    public void RequestRecall() {
        _ingameScene.RequestRecall(_recallSpotIndex);
    }
}
