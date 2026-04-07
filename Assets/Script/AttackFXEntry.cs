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

    [Tooltip("듀얼 무기에서 FirePoint FX를 왼손 Fire_Point에도 자동 스폰할지 여부")]
    public bool applyToOffHandWhenDual = false;

    [Tooltip("왼손(Fire_Point2) FX 지연(초). 메인 startDelay와 독립적으로 적용됩니다.")]
    public float offHandStartDelay = 0f;

    [Tooltip("FirePoint 사용 시 어느 손의 Fire_Point를 쓸지. 일반 설정에서는 Main 유지 권장.")]
    public AttackFXFirePointHand firePointHand = AttackFXFirePointHand.Main;

    [Tooltip("체크 시 소켓/본의 자식으로 붙어서 따라감. 해제 시 스폰 순간 위치·회전만 맞추고 월드에 고정.")]
    public bool parentToAttachPoint = false;

    /// <summary>
    /// 현재 항목을 기반으로 왼손 FirePoint 스폰용 복사본을 만듭니다.
    /// </summary>
    public AttackFXEntry CreateOffHandClone()
    {
        return new AttackFXEntry
        {
            attachRoot = attachRoot,
            attachPathOrName = attachPathOrName,
            prefab = prefab,
            startDelay = offHandStartDelay,
            applyToOffHandWhenDual = false,
            offHandStartDelay = offHandStartDelay,
            firePointHand = AttackFXFirePointHand.OffHand,
            parentToAttachPoint = parentToAttachPoint
        };
    }

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
            {
                var inst = UnityEngine.Object.Instantiate(entry.prefab, root.position, root.rotation, root);
                StartAutoCleanupIfNeeded(inst);
            }
            else
            {
                var inst = UnityEngine.Object.Instantiate(entry.prefab, root.position, root.rotation);
                StartAutoCleanupIfNeeded(inst);
            }
        }
    }

    private static void StartAutoCleanupIfNeeded(GameObject inst)
    {
        if (inst == null) return;

        var runner = inst.GetComponent<AttackFXAutoCleanupRunner>();
        if (runner == null)
            runner = inst.AddComponent<AttackFXAutoCleanupRunner>();
        runner.Begin();
    }
}

[Serializable]
public class AttackFXPhaseSet
{
    [Tooltip("이 FX 묶음이 발동할 공격 페이즈")]
    public AttackFXPhase phase = AttackFXPhase.Attack;

    [Tooltip("해당 페이즈에서 실행할 FX 목록")]
    public List<AttackFXEntry> entries = new List<AttackFXEntry>();
}

public enum AttackFXPhase
{
    Attack,
    Prepare,
    Windup,
    Active,
    Recovery,
    Finish,
    ChargeStart,
    ChargeLoop,
    ChargeRelease,
    ReloadStart,
    ReloadLoop,
    ReloadEnd,
    Custom1,
    Custom2,
}

public static class AttackFXPhaseResolver
{
    /// <summary>
    /// phase 목록에서 해당 페이즈의 FX를 찾습니다.
    /// </summary>
    public static IReadOnlyList<AttackFXEntry> Resolve(
        List<AttackFXPhaseSet> phases,
        AttackFXPhase phase)
    {
        if (phases != null)
        {
            for (int i = 0; i < phases.Count; i++)
            {
                var set = phases[i];
                if (set == null) continue;
                if (set.phase != phase) continue;
                if (set.entries != null && set.entries.Count > 0)
                    return set.entries;
            }
        }
        return null;
    }
}

/// <summary>
/// 스폰된 FX 인스턴스의 파티클 재생이 끝나면 자동 삭제.
/// 루프 파티클은 삭제하지 않으며, 외부 수명/삭제 정책을 따른다.
/// </summary>
public class AttackFXAutoCleanupRunner : MonoBehaviour
{
    private bool started;

    public void Begin()
    {
        if (started) return;
        started = true;
        StartCoroutine(CleanupRoutine());
    }

    private IEnumerator CleanupRoutine()
    {
        var systems = GetComponentsInChildren<ParticleSystem>(true);
        if (systems == null || systems.Length == 0)
            yield break;

        // 루프 파티클이 있으면 자동 삭제하지 않음
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null && systems[i].main.loop)
                yield break;
        }

        while (true)
        {
            bool anyAlive = false;
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null) continue;
                if (ps.IsAlive(true))
                {
                    anyAlive = true;
                    break;
                }
            }

            if (!anyAlive) break;
            yield return null;
        }

        if (this != null && gameObject != null)
            Destroy(gameObject);
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

/// <summary>FirePoint 사용 시 적용 손 선택.</summary>
public enum AttackFXFirePointHand
{
    Main,
    OffHand
}
