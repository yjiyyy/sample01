using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 SO당 무기 1개. 본·Transform은 공통, 프리팹만 여러 개 등록해 스폰 시 균등 랜덤.
/// </summary>
[Serializable]
public class EnemyWeaponPartAttachment
{
    [Tooltip("붙일 본 이름. 비우면 R_Hand_Weapon.")]
    public string boneName = "";

    [Tooltip("후보 무기 프리팹. 1개면 고정, 2개 이상이면 스폰 시 그중 하나를 균등 랜덤.")]
    public GameObject[] partPrefabs = Array.Empty<GameObject>();

    [Tooltip("부착 후 로컬 위치 오프셋.")]
    public Vector3 localOffset = Vector3.zero;

    [Tooltip("부착 후 로컬 회전(오일러 각도).")]
    public Vector3 localRotationEuler = Vector3.zero;

    [Tooltip("부착 후 로컬 스케일.")]
    public Vector3 localScale = Vector3.one;

    [Tooltip("켜면 스폰 시에는 안 붙고, 이 공격(Prepare~Attack 전체) 실행 중에만 붙었다가 끝나면 제거됩니다.")]
    public bool spawnOnlyDuringAttack = false;

    public bool HasAnyPrefab()
    {
        if (partPrefabs == null) return false;
        for (int i = 0; i < partPrefabs.Length; i++)
        {
            if (partPrefabs[i] != null)
                return true;
        }
        return false;
    }

    /// <summary>null이 아닌 후보 중 균등 랜덤 1개. 없으면 null.</summary>
    public GameObject PickRandomPrefab()
    {
        if (partPrefabs == null || partPrefabs.Length == 0)
            return null;

        int nonNull = 0;
        for (int i = 0; i < partPrefabs.Length; i++)
        {
            if (partPrefabs[i] != null)
                nonNull++;
        }

        if (nonNull == 0)
            return null;

        int pick = UnityEngine.Random.Range(0, nonNull);
        for (int i = 0; i < partPrefabs.Length; i++)
        {
            if (partPrefabs[i] == null)
                continue;
            if (pick == 0)
                return partPrefabs[i];
            pick--;
        }

        return null;
    }
}

/// <summary>
/// 몬스터 공격 SO 공통 베이스. 무기 파츠 부착 설정을 담습니다.
/// </summary>
public abstract class EnemyAttackDataBase : ScriptableObject
{
    public const string DefaultWeaponBoneName = "R_Hand_Weapon";

    [Header("Weapon Parts")]
    [Tooltip("이 공격에서 쓸 무기 1개. 프리팹을 여러 개 넣으면 스폰 시 균등 랜덤.")]
    public EnemyWeaponPartAttachment weaponPart = new EnemyWeaponPartAttachment();

    // 구버전 슬롯 배열 → OnValidate에서 weaponPart로 이전
    [SerializeField, HideInInspector]
    private int weaponPartSlotCount = 0;

    [SerializeField, HideInInspector]
    private EnemyPartSlot[] weaponPartSlots = Array.Empty<EnemyPartSlot>();

    public static string ResolveWeaponBoneName(string boneName)
    {
        return string.IsNullOrEmpty(boneName) ? DefaultWeaponBoneName : boneName;
    }

    private void OnValidate()
    {
        if (weaponPart == null)
            weaponPart = new EnemyWeaponPartAttachment();

        if (weaponPart.localScale == Vector3.zero)
            weaponPart.localScale = Vector3.one;

        MigrateLegacyWeaponSlotsIfNeeded();
    }

    private void MigrateLegacyWeaponSlotsIfNeeded()
    {
        if (weaponPart.HasAnyPrefab())
            return;

        if (weaponPartSlots == null || weaponPartSlots.Length == 0)
            return;

        EnemyPartSlot first = null;
        var prefabs = new List<GameObject>();

        for (int i = 0; i < weaponPartSlots.Length; i++)
        {
            EnemyPartSlot slot = weaponPartSlots[i];
            if (slot == null)
                continue;

            if (first == null)
                first = slot;

            if (slot.partPrefab != null && !prefabs.Contains(slot.partPrefab))
                prefabs.Add(slot.partPrefab);
        }

        if (first == null && prefabs.Count == 0)
            return;

        if (first != null)
        {
            if (string.IsNullOrEmpty(weaponPart.boneName))
                weaponPart.boneName = first.boneName;
            weaponPart.localOffset = first.localOffset;
            weaponPart.localRotationEuler = first.localRotationEuler;
            weaponPart.localScale = first.localScale == Vector3.zero ? Vector3.one : first.localScale;
        }

        weaponPart.partPrefabs = prefabs.ToArray();
        weaponPartSlots = Array.Empty<EnemyPartSlot>();
        weaponPartSlotCount = 0;
    }
}
