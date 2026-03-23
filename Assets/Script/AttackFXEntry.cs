using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 FX 1개 항목. 플레이어 무기 SO, 콤보 스텝, 몬스터 공격 SO에서 공통 사용.
/// </summary>
[Serializable]
public class AttackFXEntry
{
    [Tooltip("AttackerRoot=캐릭터 루트, FirePoint=무기 Fire_Point(없으면 루트), Custom=attachPathOrName로 본/소켓 검색(비어 있으면 루트)")]
    public AttackFXAttachRoot attachRoot = AttackFXAttachRoot.AttackerRoot;

    [Tooltip("Custom일 때만 사용. 캐릭터 루트 기준 이름 또는 경로(예: R_Hand_Weapon, Root_dummy/Bip001 Spine). 비어 있으면 캐릭터 루트.")]
    public string attachPathOrName = "";

    [Tooltip("스폰할 FX 프리팹")]
    public GameObject prefab;

    [Tooltip("공격 시작 후 스폰까지 지연(초)")]
    public float startDelay;

    [Tooltip("체크 시 소켓/본의 자식으로 붙어서 따라감. 해제 시 스폰 순간 위치·회전만 맞추고 월드에 고정.")]
    public bool parentToAttachPoint = false;

    /// <summary>
    /// attackFX 리스트를 스케줄. 공격 시작 시 호출.
    /// resolveEntry: 항목별 Transform. isTimeHoldActive: 홀드 중이면 delay 정지 (nullable, null이면 항상 진행)
    /// </summary>
    public static void ScheduleAttackFX(
        MonoBehaviour mb,
        IReadOnlyList<AttackFXEntry> list,
        Func<AttackFXEntry, Transform> resolveEntry,
        Func<bool> isTimeHoldActive = null)
    {
        if (mb == null || list == null || list.Count == 0) return;
        foreach (var entry in list)
        {
            if (entry == null || entry.prefab == null) continue;
            mb.StartCoroutine(SpawnFXAfterDelay(entry, resolveEntry, isTimeHoldActive));
        }
    }

    private static IEnumerator SpawnFXAfterDelay(
        AttackFXEntry entry,
        Func<AttackFXEntry, Transform> resolveEntry,
        Func<bool> isTimeHoldActive)
    {
        float delay = Mathf.Max(0f, entry.startDelay);
        float elapsed = 0f;

        while (elapsed < delay)
        {
            if (isTimeHoldActive != null && isTimeHoldActive())
            {
                yield return null;
                continue;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        var root = resolveEntry != null ? resolveEntry(entry) : null;
        if (root != null)
        {
            if (entry.parentToAttachPoint)
                UnityEngine.Object.Instantiate(entry.prefab, root.position, root.rotation, root);
            else
                UnityEngine.Object.Instantiate(entry.prefab, root.position, root.rotation);
        }
    }
}

/// <summary>FX가 붙을 루트.</summary>
public enum AttackFXAttachRoot
{
    [Tooltip("캐릭터(공격자) 루트")]
    AttackerRoot,

    [Tooltip("무기 Fire_Point (플레이어=무기 하위, 몬스터=하위 Fire_Point 검색, 없으면 루트)")]
    FirePoint,

    [Tooltip("attachPathOrName으로 본/소켓 검색(비어 있으면 캐릭터 루트)")]
    Custom
}
