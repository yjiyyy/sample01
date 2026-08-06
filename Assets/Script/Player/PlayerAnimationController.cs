using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player 애니메이션 제어기
/// - UpperBody 레이어 토글/임시 비활성화(넉백 등 CC 시) 로직 포함
/// - Animator 파라미터가 없을 때 예외가 발생하지 않도록 안전하게 호출
/// - LateUpdate에서 Speed 파라미터 업데이트 (물리 이동과 동기화)
/// - 성능 개선: 파라미터 존재 여부 캐시 및 누적 로그 방지
/// - Death 애니메이션은 한 번만 재생되고, 재생이 끝나면 마지막 프레임에 멈추도록 처리
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    private PlayerMovement movement;

    [SerializeField] public WeaponBehavior weaponBehavior;

    // Animator 파라미터 해시
    private readonly int hashSpeed = Animator.StringToHash("Speed");
    private readonly int hashAttackIndex = Animator.StringToHash("AttackIndex");
    private readonly int hashIsAttacking = Animator.StringToHash("IsAttacking");
    private readonly int hashIsUpperAttacking = Animator.StringToHash("IsUpperAttacking");
    private readonly int hashIsBackStep = Animator.StringToHash("IsBackStep");
    private readonly int hashIsDead = Animator.StringToHash("IsDead");
    private readonly int hashKnockback = Animator.StringToHash("Knockback");
    private readonly int hashKnockbackIndex = Animator.StringToHash("KnockbackIndex");
    private readonly int hashStun = Animator.StringToHash("Stun");
    private readonly int hashIsEvading = Animator.StringToHash("IsEvading");
    private readonly int hashLowerBodySpeed = Animator.StringToHash("LowerBodySpeed");

    private bool upperBodyRequestedEnabled = false;
    private const string upperLayerName = "UpperBody";

    // 캐시된 파라미터 존재 여부
    private HashSet<int> existingParamHashes;
    // 이미 경고를 남긴 파라미터(중복 로그 방지)
    private HashSet<int> warnedMissingParams = new HashSet<int>();

    // Death 애니메이션이 이미 한 번 재생되었는지 표시
    private bool deathPlayed = false;

    // Death 재생을 관리하는 코루틴 레퍼런스 (so we can stop it if respawn/reset)
    private Coroutine deathRoutine;

    // 상태 관련 경고 캐시 (Play 시 상태가 없을 때 중복 로그 방지)
    private HashSet<string> warnedMissingStates = new HashSet<string>();

    // 스나이퍼 등: 상체 Attack 첫 프레임 고정(조준 홀드). animator.speed는 건드리지 않음(하체 이동 유지).
    private bool upperAttackHoldActive = false;
    private int upperAttackHoldLayer = -1;
    private int upperAttackHoldStateHash;

    void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement>();

        // 캐시: animator parameter 존재 여부 확인 (한 번만)
        existingParamHashes = new HashSet<int>();
        if (animator != null)
        {
            var pars = animator.parameters;
            for (int i = 0; i < pars.Length; ++i)
            {
                existingParamHashes.Add(pars[i].nameHash);
            }
        }
    }

    void LateUpdate()
    {
        if (animator == null) return;
        float speed = (movement != null) ? movement.GetAnimatorSpeedEstimate() : 0f;
        SafeSetFloat(hashSpeed, speed);
        KeepUpperAttackHoldFrozen();
    }

    /* ───────── 안전 호출 헬퍼 (캐시 사용) ───────── */

    private bool HasParameter(int hash)
    {
        if (animator == null) return false;
        return existingParamHashes != null && existingParamHashes.Contains(hash);
    }

    private void SafeSetBool(int hash, bool value)
    {
        if (animator == null) return;
        if (HasParameter(hash))
        {
            animator.SetBool(hash, value);
        }
        else
        {
            if (!warnedMissingParams.Contains(hash))
            {
                Debug.LogWarning($"[PlayerAnim] SetBool skipped — parameter not found (hash:{hash})");
                warnedMissingParams.Add(hash);
            }
        }
    }

    private void SafeResetTrigger(int hash)
    {
        if (animator == null) return;
        if (HasParameter(hash))
        {
            animator.ResetTrigger(hash);
        }
        else
        {
            if (!warnedMissingParams.Contains(hash))
            {
                Debug.LogWarning($"[PlayerAnim] ResetTrigger skipped — parameter not found (hash:{hash})");
                warnedMissingParams.Add(hash);
            }
        }
    }

    private void SafeSetTrigger(int hash)
    {
        if (animator == null) return;
        if (HasParameter(hash))
        {
            animator.SetTrigger(hash);
        }
        else
        {
            if (!warnedMissingParams.Contains(hash))
            {
                Debug.LogWarning($"[PlayerAnim] SetTrigger skipped — parameter not found (hash:{hash})");
                warnedMissingParams.Add(hash);
            }
        }
    }

    private void SafeSetFloat(int hash, float value)
    {
        if (animator == null) return;
        if (HasParameter(hash))
        {
            animator.SetFloat(hash, value);
        }
        else
        {
            if (!warnedMissingParams.Contains(hash))
            {
                Debug.LogWarning($"[PlayerAnim] SetFloat skipped — parameter not found (hash:{hash})");
                warnedMissingParams.Add(hash);
            }
        }
    }

    /* ───────── 상태별 강제 애니메이션 전환 ───────── */

    // PlayerState 변경에 따라 애니메이션을 강제로 재생/설정합니다.
    // Death는 한 번만 재생되고 재생이 끝나면 마지막 프레임에서 멈춥니다.
    public void ForceAnimationByState(PlayerState newState)
    {
        if (animator == null) return;

        // ResetAllAnimatorParams는 targetState을 인자로 받아 Dead일 때 IsDead를 끄지 않도록 구현되어 있음
        ResetAllAnimatorParams(newState);

        int upperLayer = GetUpperLayerIndex();
        if (upperLayer >= 0)
        {
            if (IsStateBlockingUpperBody(newState))
            {
                if (animator.GetLayerWeight(upperLayer) > 0f)
                {
                    animator.SetLayerWeight(upperLayer, 0f);
                    animator.Update(0f);
                    Debug.Log("[PlayerAnim] CC 진입 → UpperBody 레이어 임시 비활성화");
                }
            }
            else
            {
                float targetWeight = upperBodyRequestedEnabled ? 1f : 0f;
                if (!Mathf.Approximately(animator.GetLayerWeight(upperLayer), targetWeight))
                {
                    animator.SetLayerWeight(upperLayer, targetWeight);
                    animator.Update(0f);
                    Debug.Log($"[PlayerAnim] 상태 복귀 → UpperBody 레이어 weight 복구 target={targetWeight}");
                }
            }
        }

        switch (newState)
        {
            case PlayerState.Idle:
                SafeSetFloat(hashSpeed, 0f);
                TryPlaySafe("Idle/Run", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Idle");
                break;

            case PlayerState.Move:
                SafeSetFloat(hashSpeed, 1f);
                TryPlaySafe("Idle/Run", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Run");
                break;

            case PlayerState.Attack:
                Debug.Log("[PlayerAnim] Attack 상태 - PlayAttack() 별도 호출 필요");
                break;

            case PlayerState.Knockback:
                float randomKnockbackIndex = UnityEngine.Random.Range(0, 3);
                SafeSetFloat(hashKnockbackIndex, randomKnockbackIndex);
                SafeSetTrigger(hashKnockback);
                TryPlaySafe("Knockback_Blend Tree", 0, 0f);
                Debug.Log($"[PlayerAnim] 강제 전환 → Knockback (Index: {randomKnockbackIndex})");
                break;

            case PlayerState.Stun:
                SafeSetTrigger(hashStun);
                TryPlaySafe("Stun", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Stun");
                break;

            case PlayerState.Evade:
                SafeSetBool(hashIsEvading, true);
                animator.Update(0f);
                TryPlaySafe("Evade", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Evade");
                break;

            case PlayerState.Dead:
                // Death는 반드시 한 번만 재생하도록 합니다.
                if (!deathPlayed)
                {
                    deathPlayed = true;
                    SafeSetBool(hashIsDead, true);

                    // Stop any existing death coroutine just in case
                    if (deathRoutine != null)
                    {
                        try { StopCoroutine(deathRoutine); } catch { }
                        deathRoutine = null;
                    }

                    // Play death once and then freeze at last frame
                    // Log caller for debugging repeated requests
                    string caller = GetCallerSummary();
                    Debug.Log($"[PlayerAnim] Playing Death once. Caller: {caller}");
                    if (TryPlaySafe("Death", 0, 0f))
                        deathRoutine = StartCoroutine(PlayDeathAndFreezeRoutine());
                }
                else
                {
                    // Already played: ensure flag stays set and IsDead remains true
                    SafeSetBool(hashIsDead, true);
                }
                break;
        }
    }

    // Play Death clip once and freeze at its last frame.
    // This avoids looping that can be caused by transitions or external replays.
    private IEnumerator PlayDeathAndFreezeRoutine()
    {
        if (animator == null)
        {
            deathRoutine = null;
            yield break;
        }

        // Ensure animator is playing death; wait one frame to allow state to update.
        yield return null;

        // Grab current state info and clip length
        float clipLength = 0f;
        int stateHash = 0;
        try
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            stateHash = stateInfo.fullPathHash;

            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips != null && clips.Length > 0 && clips[0].clip != null)
            {
                clipLength = clips[0].clip.length;
            }
            else
            {
                // fallback: try to find a clip with "Death" in name among all clips on animator (less likely)
                var allClips = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips : null;
                if (allClips != null)
                {
                    foreach (var c in allClips)
                    {
                        if (c != null && c.name.IndexOf("death", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            clipLength = c.length;
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerAnim] PlayDeathAndFreezeRoutine: failed to get clip length: {ex.Message}");
        }

        // If we couldn't determine length, use a reasonable default (2s)
        if (clipLength <= 0f) clipLength = 2f;

        float elapsed = 0f;
        while (elapsed < clipLength)
        {
            // If death flag was reset externally (respawn), abort
            if (!deathPlayed)
            {
                deathRoutine = null;
                yield break;
            }

            // If animator state changed away from death unexpectedly, we still wait but won't restart
            elapsed += Time.deltaTime;
            yield return null;
        }

        // After full clip duration, freeze animator at last frame.
        try
        {
            // Set normalized time to 1 (end) for current state and then stop playback by setting speed = 0.
            // Use Play with state's hash to jump to end; prefer fullPathHash if available.
            var info = animator.GetCurrentAnimatorStateInfo(0);
            int playHash = info.fullPathHash != 0 ? info.fullPathHash : Animator.StringToHash("Death");
            animator.Play(playHash, 0, 1f);
            animator.Update(0f);
            animator.speed = 0f;
            Debug.Log("[PlayerAnim] Death clip finished — animator frozen at last frame.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerAnim] Failed to freeze animator at end of Death: {ex.Message}");
        }

        deathRoutine = null;
    }

    /// <summary>상태 재생 시도. 실제로 Play가 호출되면 true.</summary>
    private bool TryPlaySafe(string stateName, int layer = 0, float normalizedTime = 0f)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return false;

        // Normalize and prepare checks:
        // - Check animator.HasState for several common variants:
        //   1) exact provided name
        //   2) "Base Layer." + provided name (some states are referenced with layer prefix)
        //   3) last segment if stateName contains '/' or '.' (e.g. "Idle/Run")
        // If none found, do not call Play (to avoid Animator.GotoState: State could not be found).
        bool exists = false;
        try
        {
            int hashExact = Animator.StringToHash(stateName);
            if (animator.HasState(layer, hashExact)) exists = true;
            else
            {
                // Try Base Layer prefix
                string basePrefixed = "Base Layer." + stateName;
                if (animator.HasState(layer, Animator.StringToHash(basePrefixed))) exists = true;
                else
                {
                    // Try last segment after '/' or '.'
                    string lastSeg = stateName;
                    int slash = stateName.LastIndexOf('/');
                    if (slash >= 0) lastSeg = stateName.Substring(slash + 1);
                    int dot = lastSeg.LastIndexOf('.');
                    if (dot >= 0) lastSeg = lastSeg.Substring(dot + 1);

                    if (animator.HasState(layer, Animator.StringToHash(lastSeg))) exists = true;
                }
            }
        }
        catch { /* ignore any HasState exception, fallback to clip-name check */ }

        // If HasState failed or uncertain, also check runtimeAnimatorController's clips by name (best-effort).
        if (!exists)
        {
            try
            {
                var rac = animator.runtimeAnimatorController;
                if (rac != null)
                {
                    var clips = rac.animationClips;
                    if (clips != null)
                    {
                        // Compare last segment and full name case-insensitively
                        string lastSeg = stateName;
                        int slash = stateName.LastIndexOf('/');
                        if (slash >= 0) lastSeg = stateName.Substring(slash + 1);
                        int dot = lastSeg.LastIndexOf('.');
                        if (dot >= 0) lastSeg = lastSeg.Substring(dot + 1);

                        foreach (var c in clips)
                        {
                            if (c == null) continue;
                            if (string.Equals(c.name, stateName, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(c.name, lastSeg, StringComparison.OrdinalIgnoreCase))
                            {
                                // Note: clip exists on controller, but if there's no state referencing it, Play by name may still fail.
                                // We treat this conservatively: do NOT call Play unless animator.HasState returned true.
                                // So here we mark exists=false but we could log info for debugging.
                                // For safety (user requested "아예 안나오게"), we won't call Play solely because a clip is present.
                                break;
                            }
                        }
                    }
                }
            }
            catch { /* ignore */ }
        }

        if (!exists)
        {
            // Warn once per missing state
            if (!warnedMissingStates.Contains(stateName))
            {
                Debug.LogWarning($"[PlayerAnim] Play skipped — state not found on Animator: '{stateName}' (layer:{layer})");
                warnedMissingStates.Add(stateName);
            }
            return false;
        }

        // If we get here, state seems to exist; call Play wrapped in try/catch to be extra-safe.
        try
        {
            animator.Play(stateName, layer, normalizedTime);
            return true;
        }
        catch (Exception e)
        {
            // If Play still fails, log once
            if (!warnedMissingStates.Contains(stateName))
            {
                Debug.LogWarning($"[PlayerAnim] animator.Play('{stateName}') failed despite existence check: {e.Message}");
                warnedMissingStates.Add(stateName);
            }
            return false;
        }
    }

    // Detect if the current base-layer state or current clip corresponds to Death
    private bool IsCurrentStateDeath()
    {
        if (animator == null) return false;

        try
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Death") || stateInfo.IsName("Base Layer.Death") || stateInfo.IsName("Base Layer.Player_Death"))
                return true;

            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips != null && clips.Length > 0)
            {
                var clipName = clips[0].clip != null ? clips[0].clip.name : "";
                if (!string.IsNullOrEmpty(clipName) && clipName.IndexOf("Death", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        catch { /* ignore */ }

        return false;
    }

    // short stacktrace summarizer to identify caller (helpful for debugging repeated requests)
    private string GetCallerSummary()
    {
        try
        {
            var st = new System.Diagnostics.StackTrace(2, true);
            var frames = st.GetFrames();
            if (frames == null || frames.Length == 0) return "<unknown>";
            var f = frames[0];
            var m = f.GetMethod();
            if (m == null) return "<unknown>";
            return $"{m.DeclaringType?.Name}.{m.Name}()";
        }
        catch { return "<unknown>"; }
    }

    private bool IsStateBlockingUpperBody(PlayerState state)
    {
        return state == PlayerState.Knockback ||
               state == PlayerState.Stun ||
               state == PlayerState.Evade ||
               state == PlayerState.Dead;
    }

    private int GetUpperLayerIndex()
    {
        if (animator == null) return -1;
        return animator.GetLayerIndex(upperLayerName);
    }

    // Reset animator params in a safe manner.
    // If targetState == Dead, do NOT clear the IsDead flag.
    private void ResetAllAnimatorParams(PlayerState targetState = PlayerState.Idle)
    {
        if (animator == null) return;

        ClearUpperAttackHold();

        SafeResetTrigger(hashKnockback);
        SafeResetTrigger(hashStun);

        SafeSetBool(hashIsAttacking, false);
        SafeSetBool(hashIsUpperAttacking, false);
        SafeSetBool(hashIsBackStep, false);

        // IsDead는 Death 상태 재생 중일 때 끄지 않음
        if (targetState != PlayerState.Dead)
        {
            SafeSetBool(hashIsDead, false);
        }

        if (targetState != PlayerState.Evade)
        {
            SafeSetBool(hashIsEvading, false);
        }

        SafeSetFloat(hashAttackIndex, 0f);
        SafeSetFloat(hashKnockbackIndex, 0f);
        SafeSetFloat(hashLowerBodySpeed, 1f);

        Debug.Log("[PlayerAnim] 모든 애니메이터 파라미터 리셋 완료 (IsDead 보존 로직 포함)");
    }

    // Respawn or external logic can call this to allow Death to be played again.
    // Also un-freezes animator if it was paused.
    public void ResetDeathState()
    {
        deathPlayed = false;
        SafeSetBool(hashIsDead, false);

        // If we froze animator at the end of death, restore speed and play idle
        if (animator != null)
        {
            try
            {
                animator.speed = 1f;
                TryPlaySafe("Idle/Run", 0, 0f);
            }
            catch { /* ignore */ }
        }

        // Stop any death coroutine
        if (deathRoutine != null)
        {
            try { StopCoroutine(deathRoutine); } catch { }
            deathRoutine = null;
        }

        Debug.Log("[PlayerAnim] Death 상태 리셋: deathPlayed=false, animator resumed.");
    }

    public void EndEvade()
    {
        SafeSetBool(hashIsEvading, false);
        Debug.Log("[PlayerAnim] 회피 애니메이션 종료");
    }

    /* ───────── 공격 실행 ───────── */

    /// <summary>
    /// 상체 Attack을 시작하고 첫 프레임에 고정합니다. (스나이퍼 조준 등)
    /// ReleaseUpperAttackHold() 호출 시 같은 클립이 이어서 재생됩니다.
    /// </summary>
    public void BeginUpperAttackHold(WeaponDataSO weaponData)
    {
        if (animator == null) return;

        ClearUpperAttackHold();

        int variantCount = weaponData != null ? weaponData.attackAnimVariantCount : 3;
        if (variantCount < 1) variantCount = 3;
        float randomIndex = UnityEngine.Random.Range(0, variantCount);

        if (weaponBehavior != null)
        {
            if (weaponData != null)
                weaponBehavior.SetPendingAttackVariantHandMode(weaponData.GetAttackVariantHandMode((int)randomIndex));
            else
                weaponBehavior.ClearPendingAttackVariantHandMode();
        }

        SafeSetFloat(hashAttackIndex, randomIndex);
        SafeSetBool(hashIsUpperAttacking, true);

        int upperLayer = GetUpperLayerIndex();
        if (upperLayer >= 0)
        {
            if (animator.GetLayerWeight(upperLayer) <= 0f)
                animator.SetLayerWeight(upperLayer, 1f);

            TryPlaySafe("Attack_BlendTree", upperLayer, 0f);
            animator.Update(0f);

            upperAttackHoldActive = true;
            upperAttackHoldLayer = upperLayer;
            var info = animator.GetCurrentAnimatorStateInfo(upperLayer);
            upperAttackHoldStateHash = info.fullPathHash;
            KeepUpperAttackHoldFrozen();
            Debug.Log($"[PlayerAnim] Upper Attack Hold 시작 → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
        }
        else
        {
            // Upper 레이어 없으면 전체 Attack 시작 후 홀드 (하체까지 멈출 수 있음 — 폴백)
            ResetAllAnimatorParams();
            SafeSetFloat(hashAttackIndex, randomIndex);
            SafeSetBool(hashIsAttacking, true);
            TryPlaySafe("Attack_BlendTree", 0, 0f);
            animator.Update(0f);
            upperAttackHoldActive = true;
            upperAttackHoldLayer = 0;
            upperAttackHoldStateHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            KeepUpperAttackHoldFrozen();
            Debug.LogWarning($"[PlayerAnim] UpperLayer 없음 → Base Attack Hold 폴백 Index:{randomIndex}");
        }
    }

    /// <summary>조준 홀드를 해제하고 Attack 애니가 첫 프레임부터 재생되게 합니다.</summary>
    public void ReleaseUpperAttackHold()
    {
        if (!upperAttackHoldActive) return;
        upperAttackHoldActive = false;
        upperAttackHoldLayer = -1;
        upperAttackHoldStateHash = 0;
        Debug.Log("[PlayerAnim] Upper Attack Hold 해제 → Attack 재생");
    }

    private void ClearUpperAttackHold()
    {
        upperAttackHoldActive = false;
        upperAttackHoldLayer = -1;
        upperAttackHoldStateHash = 0;
    }

    private void KeepUpperAttackHoldFrozen()
    {
        if (!upperAttackHoldActive || animator == null) return;
        if (upperAttackHoldLayer < 0) return;
        if (upperAttackHoldStateHash == 0) return;

        animator.Play(upperAttackHoldStateHash, upperAttackHoldLayer, 0f);
    }

    public void PlayAttack(WeaponDataSO weaponData, bool upperBodyOnly = false)
    {
        if (animator == null) return;

        ClearUpperAttackHold();

        int variantCount = weaponData != null ? weaponData.attackAnimVariantCount : 3;
        if (variantCount < 1) variantCount = 3;
        float randomIndex = UnityEngine.Random.Range(0, variantCount);

        if (weaponBehavior != null)
        {
            if (weaponData != null)
                weaponBehavior.SetPendingAttackVariantHandMode(weaponData.GetAttackVariantHandMode((int)randomIndex));
            else
                weaponBehavior.ClearPendingAttackVariantHandMode();
        }

        if (upperBodyOnly)
        {
            SafeSetFloat(hashAttackIndex, randomIndex);
            SafeSetBool(hashIsUpperAttacking, true);

            int upperLayer = GetUpperLayerIndex();
            if (upperLayer >= 0)
            {
                bool currentlyUpper = false;
                if (HasParameter(hashIsUpperAttacking))
                    currentlyUpper = animator.GetBool(hashIsUpperAttacking);

                if (!currentlyUpper)
                {
                    TryPlaySafe("Attack_BlendTree", upperLayer, 0f);
                    Debug.Log($"[PlayerAnim] Upper-body Attack 시작(초기) → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
                }
                else
                {
                    try { animator.CrossFade("Attack_BlendTree", 0f, upperLayer, 0f); }
                    catch { TryPlaySafe("Attack_BlendTree", 0, 0f); }
                    Debug.Log($"[PlayerAnim] Upper-body Attack 재시작 → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
                }

                if (animator.GetLayerWeight(upperLayer) <= 0f)
                    animator.SetLayerWeight(upperLayer, 1f);

                animator.Update(0f);
            }
            else
            {
                ResetAllAnimatorParams();
                SafeSetFloat(hashAttackIndex, randomIndex);
                SafeSetBool(hashIsAttacking, true);
                TryPlaySafe("Attack_BlendTree", 0, 0f);
                Debug.Log($"[PlayerAnim] UpperLayer 없음 → 전체 Attack 시작(fallback) Index:{randomIndex}, 무기:{weaponData?.weaponName}");
            }
        }
        else
        {
            // 전신 Attack: UpperBody 레이어를 꺼서 Idle/Run(하체)과 상체 Attack이 섞이지 않게 함
            int upperLayer = GetUpperLayerIndex();
            if (upperLayer >= 0 && animator.GetLayerWeight(upperLayer) > 0f)
            {
                SafeSetBool(hashIsUpperAttacking, false);
                animator.SetLayerWeight(upperLayer, 0f);
                animator.Update(0f);
            }

            ResetAllAnimatorParams();
            SafeSetFloat(hashAttackIndex, randomIndex);
            SafeSetBool(hashIsAttacking, true);
            TryPlaySafe("Attack_BlendTree", 0, 0f);
            Debug.Log($"[PlayerAnim] Attack 시작(전신) → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
        }
    }

    public void EndAttack()
    {
        if (animator == null) return;
        ClearUpperAttackHold();
        SafeSetBool(hashIsAttacking, false);
        SafeSetBool(hashIsUpperAttacking, false);
        SafeSetFloat(hashLowerBodySpeed, 1f);
        Debug.Log("[PlayerAnim] Attack 종료 (쿨타임 종료)");

        var pwc = GetComponentInParent<PlayerWeaponController>();
        if (pwc != null)
        {
            pwc.ExecutePendingSwitchIfAnyImmediate();
        }
    }

    public void SetUpperBodyLayerEnabled(bool enabled)
    {
        if (animator == null) return;

        int layerIndex = GetUpperLayerIndex();
        if (layerIndex < 0)
        {
            Debug.LogWarning("[PlayerAnim] UpperBody 레이어가 없습니다. SetUpperBodyLayerEnabled 무시됨.");
            return;
        }

        upperBodyRequestedEnabled = enabled;

        if (enabled)
        {
            animator.SetLayerWeight(layerIndex, 1f);
            Debug.Log("[PlayerAnim] UpperBody 레이어 활성화");
        }
        else
        {
            ClearUpperAttackHold();
            SafeSetBool(hashIsAttacking, false);
            SafeSetBool(hashIsUpperAttacking, false);
            SafeSetBool(hashIsBackStep, false);
            SafeSetFloat(hashLowerBodySpeed, 1f);
            animator.SetLayerWeight(layerIndex, 0f);
            animator.Update(0f);
            Debug.Log("[PlayerAnim] UpperBody 레이어 비활성화 및 공격 파라미터 리셋");
        }
    }

    public void SetLowerBodyPlaybackSpeed(float speed)
    {
        SafeSetFloat(hashLowerBodySpeed, speed);
    }

    public void SetBackStep(bool enabled)
    {
        SafeSetBool(hashIsBackStep, enabled);
        Debug.Log($"[PlayerAnim] IsBackStep -> {enabled}");
    }

    public void PlayChargedAttack(string stateNameOrClip)
    {
        if (animator == null) return;

        ResetAllAnimatorParams();
        SafeSetBool(hashIsAttacking, true);

        string s = string.IsNullOrEmpty(stateNameOrClip) ? "Attack_Charged01" : stateNameOrClip;
        TryPlaySafe(s, 0, 0f);
        Debug.Log($"[PlayerAnim] ChargedAttack 시작 → {s}");
    }

    /// <summary>
    /// 근접 콤보 스텝 애니. None_Combo 상태 대신 Attack_BlendTree + AttackIndex 사용.
    /// AOC의 None_Attack01/02/03 오버라이드가 스텝별 클립이 됩니다.
    /// </summary>
    public void PlayComboStepAttack(int stepIndex)
    {
        if (animator == null) return;

        int index = Mathf.Max(0, stepIndex);

        ResetAllAnimatorParams();
        SafeSetFloat(hashAttackIndex, index);
        SafeSetBool(hashIsAttacking, true);
        TryPlaySafe("Attack_BlendTree", 0, 0f);
        Debug.Log($"[PlayerAnim] ComboStep Attack 시작 → Index:{index}");
    }

    /* 애니메이션 이벤트 */
    public void AttackHit()
    {
        Debug.Log("💥 [AnimEvent] AttackHit() 호출됨");
        weaponBehavior?.AttackHit();
    }

    public void OnAttackStart() => Debug.Log("🕒 [AnimEvent] OnAttackStart() 호출됨");
    public void OnAttackEnd() => Debug.Log("✅ [AnimEvent] OnAttackEnd() 호출됨");

    public Animator GetAnimator() => animator;
}