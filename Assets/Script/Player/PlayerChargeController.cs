using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 차지(홀드→성공→발사/유지/무적/스폰) 전담
/// - Tick()에서 InputManager를 읽어 동작
/// - 상태 조회/변경/무적 토글/스폰 포인트/애니 컨트롤러를 주입받아 동작
/// </summary>
[DisallowMultipleComponent]
public class PlayerChargeController : MonoBehaviour
{
    // 주입
    private Func<WeaponDataSO> getWeaponData;
    private Func<PlayerState> getState;
    private Action<PlayerState> changeState;
    private Action<bool> setInvincible; // 차지 무적 토글(컨트롤러로 전달)
    private Transform spawnPoint;
    private PlayerAnimationController anim;
    private bool enableChargeMessages;
    private bool debugMode;

    // 내부 상태
    private bool chargeHoldActive = false;
    private float chargeHoldStartTime = 0f;
    private bool chargeStartMsgDone = false;
    private bool chargeSuccessMsgDone = false;
    private bool chargeExecuted = false;
    private bool chargeReady = false;

    private Coroutine chargeSpawnRoutine;
    private Coroutine chargedMaintainRoutine;

    private WeaponDataSO chargeWeaponProxy;

    public void Setup(
        PlayerAnimationController animCtrl,
        Transform meleeSpawnPoint,
        Func<WeaponDataSO> getWeapon,
        Func<PlayerState> getCurrentState,
        Action<PlayerState> changeStateAction,
        Action<bool> setInvincibleAction,
        bool enableMessages,
        bool debug)
    {
        anim = animCtrl;
        spawnPoint = meleeSpawnPoint != null ? meleeSpawnPoint : transform;
        getWeaponData = getWeapon;
        getState = getCurrentState;
        changeState = changeStateAction;
        setInvincible = setInvincibleAction;
        enableChargeMessages = enableMessages;
        debugMode = debug;
    }

    public void Tick()
    {
        var data = getWeaponData != null ? getWeaponData() : null;
        // ---- 변경: AR 무기일 경우 차지 슬롯을 무시하도록 처리 ----
        PlayerChargeAttackSO slot = null;
        if (data != null && !(data is WeaponDataSO_AR))
            slot = data.chargeSlot;
        // ----------------------------------------------------

        // Down: 홀드 시작
        if (!chargeHoldActive && InputManager.Instance.GetAttackDown())
        {
            if (slot == null)
            {
                if (debugMode) Debug.Log("[Charge] 시작 불가: 현재 무기에 차지 슬롯 없음");
            }
            else
            {
                chargeHoldActive = true;
                chargeHoldStartTime = Time.time;
                chargeStartMsgDone = false;
                chargeSuccessMsgDone = false;
                chargeExecuted = false;
                chargeReady = false;
                if (debugMode) Debug.Log("[Charge] 홀드 시작");
            }
        }

        // Hold 유지: 메시지/성공 플래그
        if (chargeHoldActive && InputManager.Instance.GetAttack())
        {
            float held = Time.time - chargeHoldStartTime;

            if (enableChargeMessages && !chargeStartMsgDone && held >= 1.0f)
            {
                chargeStartMsgDone = true;
                Debug.Log("차지 시작");
            }

            if (slot != null && !chargeReady && held >= slot.holdSuccessTime)
            {
                chargeReady = true;
                if (enableChargeMessages && !chargeSuccessMsgDone)
                {
                    chargeSuccessMsgDone = true;
                    Debug.Log("차지 성공");
                }
            }
        }

        // Up: 발사 시도
        if (chargeHoldActive && InputManager.Instance.GetAttackUp())
        {
            bool fired = false;

            // ---- 변경: Up 시에도 current weapon이 AR이면 차지 슬롯 무시 ----
            var data2 = getWeaponData != null ? getWeaponData() : null;
            var slot2 = (data2 != null && !(data2 is WeaponDataSO_AR)) ? data2.chargeSlot : null;
            // ------------------------------------------------------------

            if (slot2 == null)
            {
                if (debugMode) Debug.Log("[Charge] 취소: 방출 시점에 차지 슬롯 없음");
            }
            else if (!chargeReady)
            {
                if (debugMode) Debug.Log("[Charge] 실패: 성공 시간 도달 전 방출");
            }
            else if (!IsChargeExecutionAllowedNow())
            {
                if (debugMode) Debug.Log("[Charge] 취소: 방출 시점 상태가 Idle/Move 아님");
            }
            else if (!chargeExecuted)
            {
                ExecuteChargeAttack(slot2);
                chargeExecuted = true;
                fired = true;
            }

            // 플래그 리셋
            chargeHoldActive = false;
            chargeStartMsgDone = false;
            chargeSuccessMsgDone = false;
            chargeReady = false;
            chargeExecuted = false;

            if (fired && debugMode) Debug.Log("[Charge] 릴리스 → 발사 완료");
        }
    }

    public void CancelAll()
    {
        // 홀드/플래그
        chargeHoldActive = false;
        chargeStartMsgDone = false;
        chargeSuccessMsgDone = false;
        chargeReady = false;
        chargeExecuted = false;

        // 스폰 대기 중단
        if (chargeSpawnRoutine != null)
        {
            StopCoroutine(chargeSpawnRoutine);
            chargeSpawnRoutine = null;
        }

        // 유지 코루틴 중단
        if (chargedMaintainRoutine != null)
        {
            StopCoroutine(chargedMaintainRoutine);
            chargedMaintainRoutine = null;
        }

        // 차지 무적 해제
        setInvincible?.Invoke(false);
    }

    private bool IsChargeExecutionAllowedNow()
    {
        var s = getState != null ? getState() : PlayerState.Idle;
        return s == PlayerState.Idle || s == PlayerState.Move;
    }

    private void ExecuteChargeAttack(PlayerChargeAttackSO slot)
    {
        if (slot == null) return;

        changeState?.Invoke(PlayerState.Attack);

        // 발동 무적
        if (slot.invincibilityDuration > 0f)
        {
            setInvincible?.Invoke(true);
            StartCoroutine(EndInvincibleLater(slot.invincibilityDuration));
        }

        // 애니메이션
        string animName = slot.chargedClip != null ? slot.chargedClip.name : slot.chargedStateName;
        if (string.IsNullOrEmpty(animName)) animName = "Attack_Charged01";
        anim?.PlayChargedAttack(animName);

        // 히트박스 스폰
        if (chargeSpawnRoutine != null)
        {
            StopCoroutine(chargeSpawnRoutine);
            chargeSpawnRoutine = null;
        }
        chargeSpawnRoutine = StartCoroutine(ChargeHitboxSpawnRoutine(slot));

        // 유지 시간
        float dur = (slot.duration > 0f) ? slot.duration : 0.8f;
        if (chargedMaintainRoutine != null) { StopCoroutine(chargedMaintainRoutine); chargedMaintainRoutine = null; }
        chargedMaintainRoutine = StartCoroutine(ChargedAttackMaintainRoutine(dur));
    }

    private IEnumerator ChargedAttackMaintainRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            var s = getState != null ? getState() : PlayerState.Idle;
            if (s == PlayerState.Knockback || s == PlayerState.Stun ||
                s == PlayerState.Dead || s == PlayerState.Evade)
            {
                chargedMaintainRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Idle/Move 복귀
        var move = GetComponent<PlayerMovement>();
        changeState?.Invoke(move != null && move.GetVelocityMagnitude() > 0.1f ? PlayerState.Move : PlayerState.Idle);
        chargedMaintainRoutine = null;
    }

    private IEnumerator ChargeHitboxSpawnRoutine(PlayerChargeAttackSO slot)
    {
        if (slot.hitBoxPrefab == null)
        {
            Debug.LogWarning("⚠ 차지 힛박스 프리팹이 비어 있습니다.");
            yield break;
        }

        if (slot.spawnDelay > 0f)
        {
            float waited = 0f;
            while (waited < slot.spawnDelay)
            {
                var s = getState != null ? getState() : PlayerState.Idle;
                if (s == PlayerState.Knockback || s == PlayerState.Stun ||
                    s == PlayerState.Dead || s == PlayerState.Evade)
                {
                    chargeSpawnRoutine = null;
                    yield break;
                }
                float step = Mathf.Min(Time.deltaTime, slot.spawnDelay - waited);
                waited += step;
                yield return null;
            }
        }

        Transform spawn = spawnPoint != null ? spawnPoint : transform;

        EnsureChargeWeaponProxy(slot);

        GameObject hb = Instantiate(slot.hitBoxPrefab, spawn.position, spawn.rotation);

        if (hb.TryGetComponent<HitBox_PC>(out var hitbox))
        {
            hitbox.SetWeapon(chargeWeaponProxy);

            if (slot.enableAreaDot)
            {
                float dmgPerTick = slot.dotDamagePerTick > 0f ? slot.dotDamagePerTick : slot.damage;
                float interval = Mathf.Max(0.01f, slot.dotTickInterval);

                hitbox.Initialize(
                    dmgPerTick,
                    slot.range,
                    slot.knockbackPower,
                    slot.hitBoxLifetime,
                    allowDup: true,
                    dupInterval: interval
                );
            }
            else
            {
                hitbox.Initialize(
                    slot.damage,
                    slot.range,
                    slot.knockbackPower,
                    slot.hitBoxLifetime
                );
            }
        }
        else
        {
            Debug.LogWarning("⚠ 차지 힛박스 프리팹에 HitBox_PC 컴포넌트가 없습니다.");
        }

#if UNITY_EDITOR
        if (debugMode)
        {
            Debug.Log($"[Charge] HB Spawn(Delay {slot.spawnDelay:F2}s) │ dmg:{slot.damage}, range:{slot.range}, kb:{slot.knockbackPower}, life:{slot.hitBoxLifetime}, dup:{slot.enableAreaDot}");
        }
#endif

        chargeSpawnRoutine = null;
    }

    private void EnsureChargeWeaponProxy(PlayerChargeAttackSO slot)
    {
        if (chargeWeaponProxy == null)
        {
            chargeWeaponProxy = ScriptableObject.CreateInstance<WeaponDataSO>();
            chargeWeaponProxy.weaponName = "ChargeAttack";
        }

        // 넉백/스턴
        chargeWeaponProxy.knockbackPower = slot.knockbackPower;
        chargeWeaponProxy.knockbackDuration = slot.knockbackDuration;
        chargeWeaponProxy.stunDuration = slot.stunDuration;

        // 처치 연출 파라미터
        chargeWeaponProxy.deathType = slot.deathType;
        chargeWeaponProxy.ragdollImpulse = slot.ragdollImpulse;
        chargeWeaponProxy.upwardImpulse = slot.upwardImpulse;
        chargeWeaponProxy.torqueImpulse = slot.torqueImpulse;
        chargeWeaponProxy.sliceForce = slot.sliceForce;

        // 리스트 복사
        if (slot.possibleSliceParts != null && slot.possibleSliceParts.Count > 0)
            chargeWeaponProxy.possibleSliceParts = new List<BodySliceType>(slot.possibleSliceParts);
        else
            chargeWeaponProxy.possibleSliceParts = new List<BodySliceType>();
    }

    private IEnumerator EndInvincibleLater(float duration)
    {
        yield return new WaitForSeconds(duration);
        setInvincible?.Invoke(false);
    }
}