using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Attack/ComboAttackData", fileName = "ComboAttackData_SO")]
public class ComboAttackData : EnemyAttackDataBase
{
    [Header("식별")]
    public string attackName = "Combo_Attack";

    [Header("슬롯(근접공격 목록)")]
    [Tooltip("콤보에서 사용할 MeleeAttackData를 순서대로. 빈 슬롯(null)은 건너뜁니다.")]
    public MeleeAttackData[] slots = new MeleeAttackData[0];

    [Header("콤보 전체 옵션")]
    [Tooltip("콤보 전체에 적용되는 쿨다운(또는 인터럽트 시 적용되는 쿨다운)(초)")]
    public float cooldown = 1.5f;

    [Tooltip("콤보 전체의 유효 거리(EnemyAttackController.GetAttackRange에서 사용)")]
    public float range = 2.5f;

    [Tooltip("인터럽트 발생 시 전체 콤보 쿨다운을 적용할지 여부 (권장: true)")]
    public bool applyFullCooldownOnInterrupt = true;

#if UNITY_EDITOR
    private void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);
        range = Mathf.Max(0f, range);
     }
#endif
}
