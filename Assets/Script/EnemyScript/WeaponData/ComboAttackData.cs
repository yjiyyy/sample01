using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Attack/ComboAttackData", fileName = "ComboAttackData_SO")]
public class ComboAttackData : ScriptableObject
{
    [Header("식별")]
    public string attackName = "Combo_Attack";

    [Header("슬롯(순서대로 재생)")]
    [Tooltip("순서대로 실행할 MeleeAttackData들을 넣으세요. 빈 슬롯(null)은 무시됩니다.")]
    public MeleeAttackData[] slots = new MeleeAttackData[0];

    [Header("콤보 전체 옵션")]
    [Tooltip("콤보 전체가 끝난 이후(또는 인터럽트 시) 적용되는 쿨다운(초)")]
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