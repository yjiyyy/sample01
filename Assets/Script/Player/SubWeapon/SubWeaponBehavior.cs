using UnityEngine;

/// <summary>
/// 보조무기 프리팹에 붙이는 베이스 컴포넌트.
/// 궤도 위치는 SubWeaponController가 설정하며, 이 컴포넌트는 자동 공격/회복 등 고유 동작을 담당합니다.
/// 상속하여 OrbitalDamageBehavior, AutoShootBehavior, AutoHealBehavior 등을 구현하세요.
/// </summary>
[DisallowMultipleComponent]
public class SubWeaponBehavior : MonoBehaviour
{
    [Header("런타임 데이터 (Controller가 주입)")]
    public SubWeaponDataSO data;

    /// <summary>
    /// Controller가 Spawn 시 호출. data를 주입합니다.
    /// </summary>
    public virtual void ApplyData(SubWeaponDataSO so)
    {
        data = so;
    }

    /// <summary>
    /// 매 프레임 호출. 자동 공격, 회복 등 동작을 여기서 구현하세요.
    /// </summary>
    protected virtual void OnTick()
    {
    }

    private void Update()
    {
        if (data != null)
            OnTick();
    }
}
