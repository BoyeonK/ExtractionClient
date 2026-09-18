using UnityEngine;

// 첫 HostileNPC 파생. 지금은 계층과 스폰 배선을 세우는 자리이고 전투 동작은 비어 있다 —
// MinAggro/MaxAggro는 기본값(4/8)을 쓰며, 다른 값이 필요해지면 여기서 override한다
public class TurretNPC : HostileNPC {
    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.Turret;
    }

    // TODO: 사거리·시야 판정 — 대상은 언제나 주도권자이므로 후보 탐색이 아니라 가시성 확인이다
    protected override void FindTarget() { }

    // TODO: 연사 간격과 피격 보고 — 보고 경로는 프로토콜 설계 후에 붙는다
    protected override void TryAttack() { }
}
