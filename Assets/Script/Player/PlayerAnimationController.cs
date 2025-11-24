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
/// </summary>
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

    public void ForceAnimationByState(PlayerState newState)
    {
        if (animator == null) return;

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
                animator.Play("Idle/Run", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Idle");
                break;

            case PlayerState.Move:
                SafeSetFloat(hashSpeed, 1f);
                animator.Play("Idle/Run", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Run");
                break;

            case PlayerState.Attack:
                Debug.Log("[PlayerAnim] Attack 상태 - PlayAttack() 별도 호출 필요");
                break;

            case PlayerState.Knockback:
                float randomKnockbackIndex = UnityEngine.Random.Range(0, 3);
                SafeSetFloat(hashKnockbackIndex, randomKnockbackIndex);
                SafeSetTrigger(hashKnockback);
                animator.Play("Knockback_Blend Tree", 0, 0f);
                Debug.Log($"[PlayerAnim] 강제 전환 → Knockback (Index: {randomKnockbackIndex})");
                break;

            case PlayerState.Stun:
                SafeSetTrigger(hashStun);
                animator.Play("Stun", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Stun");
                break;

            case PlayerState.Evade:
                SafeSetBool(hashIsEvading, true);
                animator.Update(0f);
                animator.Play("Evade", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Evade");
                break;

            case PlayerState.Dead:
                SafeSetBool(hashIsDead, true);
                animator.Play("Death", 0, 0f);
                Debug.Log("[PlayerAnim] 강제 전환 → Death");
                break;
        }
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

    private void ResetAllAnimatorParams(PlayerState targetState = PlayerState.Idle)
    {
        if (animator == null) return;

        SafeResetTrigger(hashKnockback);
        SafeResetTrigger(hashStun);

        SafeSetBool(hashIsAttacking, false);
        SafeSetBool(hashIsUpperAttacking, false);
        SafeSetBool(hashIsBackStep, false);
        SafeSetBool(hashIsDead, false);

        if (targetState != PlayerState.Evade)
        {
            SafeSetBool(hashIsEvading, false);
        }

        SafeSetFloat(hashAttackIndex, 0f);
        SafeSetFloat(hashKnockbackIndex, 0f);
        SafeSetFloat(hashLowerBodySpeed, 1f);

        Debug.Log("[PlayerAnim] 모든 애니메이터 파라미터 리셋 완료");
    }

    public void EndEvade()
    {
        SafeSetBool(hashIsEvading, false);
        Debug.Log("[PlayerAnim] 회피 애니메이션 종료");
    }

    /* ───────── 공격 실행 ───────── */

    public void PlayAttack(WeaponDataSO weaponData, bool upperBodyOnly = false)
    {
        if (animator == null) return;

        float randomIndex = UnityEngine.Random.Range(0, 3);

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
                    animator.Play("Attack_BlendTree", upperLayer, 0f);
                    Debug.Log($"[PlayerAnim] Upper-body Attack 시작(초기) → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
                }
                else
                {
                    animator.CrossFade("Attack_BlendTree", 0f, upperLayer, 0f);
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
                animator.Play("Attack_BlendTree", 0, 0f);
                Debug.Log($"[PlayerAnim] UpperLayer 없음 → 전체 Attack 시작(fallback) Index:{randomIndex}, 무기:{weaponData?.weaponName}");
            }
        }
        else
        {
            ResetAllAnimatorParams();
            SafeSetFloat(hashAttackIndex, randomIndex);
            SafeSetBool(hashIsAttacking, true);
            animator.Play("Attack_BlendTree", 0, 0f);
            Debug.Log($"[PlayerAnim] Attack 시작 → Index:{randomIndex}, 무기:{weaponData?.weaponName}");
        }
    }

    public void EndAttack()
    {
        if (animator == null) return;
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
        animator.Play(s, 0, 0f);

        Debug.Log($"[PlayerAnim] ChargedAttack 시작 → {s}");
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