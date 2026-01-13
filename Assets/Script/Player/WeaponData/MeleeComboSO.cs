using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Melee Combo 전체 정의 (여러 스텝)
/// </summary>
[CreateAssetMenu(menuName = "Player/MeleeCombo", fileName = "MeleeCombo_SO")]
public class MeleeComboSO : ScriptableObject
{
    [Tooltip("콤보 스텝들 (순서대로 실행)")]
    public List<MeleeComboStepSO> steps = new List<MeleeComboStepSO>();

    [Tooltip("마지막 스텝 이후 루프 여부 (보통은 false)")]
    public bool loop = false;

    private void OnValidate()
    {
        if (steps == null) steps = new List<MeleeComboStepSO>();
    }
}