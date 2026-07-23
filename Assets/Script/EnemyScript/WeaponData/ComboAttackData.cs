using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Attack/ComboAttackData", fileName = "ComboAttackData_SO")]
public class ComboAttackData : EnemyAttackDataBase
{
    [Header("�ĺ�")]
    public string attackName = "Combo_Attack";

    [Header("����(������� ���)")]
    [Tooltip("������� ������ MeleeAttackData���� ��������. �� ����(null)�� ���õ˴ϴ�.")]
    public MeleeAttackData[] slots = new MeleeAttackData[0];

    [Header("�޺� ��ü �ɼ�")]
    [Tooltip("�޺� ��ü�� ���� ����(�Ǵ� ���ͷ�Ʈ ��) ����Ǵ� ��ٿ�(��)")]
    public float cooldown = 1.5f;

    [Tooltip("�޺� ��ü�� ��ȿ �Ÿ�(EnemyAttackController.GetAttackRange���� ���)")]
    public float range = 2.5f;

    [Tooltip("���ͷ�Ʈ �߻� �� ��ü �޺� ��ٿ��� �������� ���� (����: true)")]
    public bool applyFullCooldownOnInterrupt = true;

#if UNITY_EDITOR
    private void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);
        range = Mathf.Max(0f, range);
     }
#endif
}