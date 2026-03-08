using UnityEngine;

/// <summary>
/// 보조무기 데이터 ScriptableObject.
/// 각 보조무기 타입(자동 공격, 자동 회복 등)이 필요한 공통 필드 + 프리팹 참조.
/// 실제 동작은 SubWeaponBehavior를 상속한 프리팹 컴포넌트가 정의합니다.
/// </summary>
[CreateAssetMenu(menuName = "SubWeapon/SubWeaponDataSO", fileName = "SubWeapon_")]
public class SubWeaponDataSO : ScriptableObject
{
    [Header("식별/표시")]
    public string id;
    public string displayName = "New SubWeapon";
    public Sprite icon;

    [Header("장착 프리팹")]
    [Tooltip("이 SO를 장착할 때 인스턴스화할 보조무기 프리팹. SubWeaponBehavior 컴포넌트 필요.")]
    public GameObject prefab;

    [Header("공통 수치 (각 Behavior가 선택적으로 사용)")]
    public float damage = 10f;
    public float cooldown = 1f;
    public float range = 5f;
    [Tooltip("회복용 보조무기일 때 회복량")]
    public float healAmount = 5f;
}
