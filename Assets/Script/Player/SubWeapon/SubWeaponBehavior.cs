using UnityEngine;

/// <summary>
/// 보조무기 프리팹에 붙이는 베이스 컴포넌트.
/// 궤도 위치는 SubWeaponController가 설정하며, 이 컴포넌트는 자동 공격 등 고유 동작을 담당합니다.
/// </summary>
[DisallowMultipleComponent]
public class SubWeaponBehavior : MonoBehaviour
{
    /// <summary>SubWeaponController가 인스턴스를 붙인 직후 호출됩니다.</summary>
    public virtual void NotifyAttached()
    {
    }

    /// <summary>매 프레임 호출. 자동 공격 등 동작을 여기서 구현하세요.</summary>
    protected virtual void OnTick()
    {
    }

    private void Update()
    {
        OnTick();
    }
}
