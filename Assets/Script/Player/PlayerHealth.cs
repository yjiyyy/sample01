using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 플레이어 전용 체력 관리 시스템
/// - 레벨업/경험 관련 로직은 PlayerStats로 이동하였습니다.
/// - 이 컴포넌트는 체력 관련만 담당합니다.
/// - PC 랙돌: 몬스터 웨폰 SO의 deathMode가 Ragdoll일 때만 활성화 (그 외는 기존 애니메이션 죽음).
/// - 슬라이스: 몬스터와 동일하게 SO의 sliceTargets/sliceImpulse로 본 분리 연출 지원.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("기본 체력")]
    public float maxHP = 100f;
    private float currentHP;

    [Header("피격 반응 (무게)")]
    [Tooltip("값이 클수록 넉백에 덜 밀림")]
    public float weight = 1f;

    // ✅ 사망 처리 중복 방지 플래그 (HP 0이 여러 번 들어오거나, 피격이 연속으로 들어와도 1회만 처리)
    private bool deadProcessed = false;

    // 랙돌 (몬스터 웨폰 SO가 Ragdoll일 때만 활성화)
    private List<Rigidbody> ragdollBodies = new List<Rigidbody>();
    private List<Collider> ragdollColliders = new List<Collider>();
    private Transform rootTransform;
    private Rigidbody rootRb;
    private Collider rootCollider;
    private Animator animator;
    private bool ragdollInitialized = false;

    private const string kHeadName = "Bip001 Head";
    private const string kLeftArmName = "Bip001 L UpperArm";
    private const string kRightArmName = "Bip001 R UpperArm";
    private const string kLeftLegName = "Bip001 L Thigh";
    private const string kRightLegName = "Bip001 R Thigh";
    private const float DESTROY_DELAY = 7f;

    void Awake()
    {
        currentHP = maxHP;
        deadProcessed = false;
        InputManager.SetPlayerDeathBlock(false); // 새 플레이어 생성 시 입력 차단 해제
        rootTransform = transform.root;
        rootRb = rootTransform.GetComponent<Rigidbody>();
        rootCollider = rootTransform.GetComponent<Collider>();
        animator = rootTransform.GetComponentInChildren<Animator>();
        CollectRagdollParts();
    }

    private void CollectRagdollParts()
    {
        ragdollBodies.Clear();
        ragdollColliders.Clear();
        if (rootTransform == null) return;
        foreach (var rb in rootTransform.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb != null && rb.transform != rootTransform) ragdollBodies.Add(rb);
        }
        foreach (var col in rootTransform.GetComponentsInChildren<Collider>(true))
        {
            if (col != null && col.transform != rootTransform && col != rootCollider) ragdollColliders.Add(col);
        }
        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            rb.isKinematic = true;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        foreach (var col in ragdollColliders)
        {
            if (col != null) col.enabled = false;
        }
        ragdollInitialized = true;
    }

    /* ───────── 피해 처리 ───────── */
    public void ApplyDamage(float amount)
    {
        ApplyDamage(amount, Vector3.zero, null, 1f);
    }

    public void ApplyDamage(float amount, WeaponDataSO weapon)
    {
        ApplyDamage(amount, Vector3.zero, weapon, 1f);
    }

    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon)
    {
        ApplyDamage(amount, hitDir, weapon, 1f);
    }

    public void ApplyDamage(float amount, Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        // 이미 죽었으면 추가 데미지/넉백/로그 등 모두 무시
        if (deadProcessed) return;

        currentHP -= amount;
        Debug.Log($"플레이어가 {amount:F1} 피해! scale:{impactScale:F2} | HP: {currentHP:F1}");

        if (currentHP <= 0f)
        {
            currentHP = 0f;
            Die(hitDir, weapon, impactScale);
        }
    }

    /* ───────── 회복 처리 ───────── */
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        if (deadProcessed) return; // 죽은 상태에서는 회복 금지(부활은 나중에 별도 처리)

        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        Debug.Log($"플레이어가 {amount:F1} 회복됨 → 현재 HP: {currentHP:F1}");
    }

    /* ───────── 사망 처리 ───────── */
    private void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (deadProcessed) return;
        deadProcessed = true;

        // 0) 즉시 입력 차단 — 죽는 순간 입력이 적용되지 않도록 (입력 중 랙돌 시 특히 중요)
        if (InputManager.Instance != null)
        {
            InputManager.SetPlayerDeathBlock(true);
            InputManager.Instance.ClearPlayerInput();
        }

        var weaponCtrl = GetComponent<PlayerWeaponController>();
        var move = GetComponent<PlayerMovement>();
        var evade = GetComponent<PlayerEvadeController>();
        var charge = GetComponent<PlayerChargeController>();
        var animTester = GetComponent<PlayerAnimationTester>();
        var recoil = GetComponent<PlayerRecoil>();

        // 1) 상태 Dead + 모든 행동 컴포넌트 종료 (죽음 이벤트만 유지)
        try
        {
            var m = weaponCtrl?.GetType().GetMethod("SetState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m != null && weaponCtrl != null) m.Invoke(weaponCtrl, new object[] { PlayerState.Dead });
        }
        catch (System.Exception ex) { Debug.LogWarning($"[PlayerHealth] weaponCtrl SetState 실패: {ex.Message}"); }
        if (move != null) move.enabled = false;
        if (weaponCtrl != null) weaponCtrl.enabled = false;
        if (evade != null) evade.enabled = false;
        if (charge != null) charge.enabled = false;
        if (animTester != null) animTester.enabled = false;
        if (recoil != null) recoil.enabled = false;

        // 2) SO에 따라 Ragdoll / Slice / 애니메이션 죽음 (몬스터와 동일)
        bool doRagdoll = (weapon != null && weapon.deathMode == DeathMode.Ragdoll && ragdollBodies.Count > 0);
        bool doSlice = (weapon != null && weapon.sliceTargets != null && weapon.sliceTargets.Count > 0);

        if (doRagdoll)
        {
            if (!ragdollInitialized && ragdollBodies.Count == 0) CollectRagdollParts();
            if (doSlice)
                PerformSliceWithSelectiveGlobalImpulse(hitDir, weapon, impactScale);
            else
            {
                if (animator != null) animator.enabled = false;
                if (rootRb != null) rootRb.isKinematic = true;
                if (rootCollider != null) rootCollider.enabled = false;
                foreach (var rb in ragdollBodies) { if (rb != null) rb.isKinematic = false; }
                foreach (var col in ragdollColliders) { if (col != null) col.enabled = true; }
                ApplyRagdollImpulse(hitDir, weapon, impactScale);
            }
            if (rootTransform != null) Destroy(rootTransform.gameObject, DESTROY_DELAY);
            else Destroy(gameObject, DESTROY_DELAY);
            return;
        }

        if (doSlice)
        {
            PerformSliceWithAnimationBody(hitDir, weapon, impactScale);
            if (rootTransform != null) Destroy(rootTransform.gameObject, DESTROY_DELAY);
            else Destroy(gameObject, DESTROY_DELAY);
            return;
        }

        // 3) 애니메이션 죽음: 애니메이터 IsDead, 전 Collider OFF, 루트 Kinematic
        Debug.Log("플레이어 사망 (HP 0) → 애니메이션 죽음");
        var animCtrl = GetComponent<PlayerAnimationController>();
        if (animCtrl != null) animCtrl.ForceAnimationByState(PlayerState.Dead);
        else if (animator != null) animator.SetBool("IsDead", true);

        var root = transform.root;
        if (root != null)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (c != null) c.enabled = false;
        }
        if (rootRb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rootRb.linearVelocity = Vector3.zero;
#else
            rootRb.velocity = Vector3.zero;
#endif
            rootRb.angularVelocity = Vector3.zero;
            rootRb.isKinematic = true;
        }
        if (root != null) Destroy(root.gameObject, 5f);
        else Destroy(gameObject, 5f);
    }

    private void PerformSliceWithAnimationBody(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (rootRb != null) rootRb.isKinematic = true;
        if (rootCollider != null) rootCollider.enabled = false;

        if (!ragdollInitialized && ragdollBodies.Count == 0) CollectRagdollParts();

        SliceTarget target = ChooseSliceTarget(weapon.sliceTargets);
        List<Transform> sliceRoots = CollectSliceRoots(target);
        if (sliceRoots.Count == 0)
        {
            PlayAnimationDeath();
            return;
        }

        HashSet<Rigidbody> slicedSet = new HashSet<Rigidbody>();
        foreach (var root in sliceRoots)
        {
            if (root == null) continue;
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) slicedSet.Add(rb);
        }

        Quaternion savedRotation = transform.rotation;
        Avatar originalAvatar = null;
        if (animator != null)
        {
            originalAvatar = animator.avatar;
            animator.avatar = null;
        }

        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            if (slicedSet.Contains(rb)) continue;
            DisconnectJointsPointingToSet(rb, slicedSet);
        }

        foreach (var root in sliceRoots)
        {
            if (root == null) continue;

            var spawner = rootTransform.GetComponentInChildren<SliceBloodEffectSpawner>();
            if (spawner != null) spawner.SpawnBloodAtSlice(root);

            Vector3 worldPos = root.position;
            Quaternion worldRot = root.rotation;

            var partBodies = root.GetComponentsInChildren<Rigidbody>(true);
            var partCols = root.GetComponentsInChildren<Collider>(true);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                foreach (var j in t.GetComponents<Joint>()) { if (j != null) { j.connectedBody = null; Destroy(j); } }
                foreach (var c in t.GetComponentsInChildren<ConfigurableJoint>(true)) { if (c != null) { c.connectedBody = null; Destroy(c); } }
                foreach (var cj in t.GetComponentsInChildren<CharacterJoint>(true)) { if (cj != null) { cj.connectedBody = null; Destroy(cj); } }
                foreach (var hj in t.GetComponentsInChildren<HingeJoint>(true)) { if (hj != null) { hj.connectedBody = null; Destroy(hj); } }
                foreach (var fj in t.GetComponentsInChildren<FixedJoint>(true)) { if (fj != null) { fj.connectedBody = null; Destroy(fj); } }
            }

            root.SetParent(null, worldPositionStays: true);
            root.gameObject.name = root.gameObject.name + "_Sliced";
            root.position = worldPos;
            root.rotation = worldRot;

            foreach (var col in partCols) { if (col != null) col.enabled = true; }
            foreach (var rb in partBodies)
            {
                if (rb == null) continue;
                rb.position = rb.transform.position;
                rb.rotation = rb.transform.rotation;
                rb.ResetInertiaTensor();
                rb.ResetCenterOfMass();
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.MovePosition(rb.transform.position);
                rb.MoveRotation(rb.transform.rotation);
            }

            float sImpulseBase = weapon != null ? weapon.sliceImpulse : 0f;
            float sImpulse = Randomize20Percent(sImpulseBase) * Mathf.Max(impactScale, 0f);
            if (sImpulse > 0f)
            {
                Vector3 dir = hitDir;
                if (dir.sqrMagnitude > 0.0001f) dir = new Vector3(dir.x, 0f, dir.z).normalized;
                else dir = Vector3.forward;
                Vector2 rnd2 = Random.insideUnitCircle;
                Vector3 randHoriz = (transform.right * rnd2.x + transform.forward * rnd2.y);
                Vector3 finalHoriz = (dir * 0.7f + randHoriz.normalized * 0.3f);
                finalHoriz.y = 0f;
                if (finalHoriz.sqrMagnitude > 0.0001f) finalHoriz = finalHoriz.normalized;
                Vector3 velChange = finalHoriz * sImpulse * 0.3f + Vector3.up * sImpulse * 0.7f;
                Vector3 spinAxis = MakeRandomSpinAxisAvoidPitch(dir);
                StartCoroutine(ApplySliceVelocityDelayed(partBodies, velChange, spinAxis, sImpulse));
            }
            Destroy(root.gameObject, DESTROY_DELAY);
        }

        if (animator != null && originalAvatar != null)
        {
            animator.avatar = originalAvatar;
            animator.Rebind();
            animator.Update(0f);
        }
        transform.rotation = savedRotation;
        if (rootRb != null) rootRb.MoveRotation(savedRotation);
        PlayAnimationDeath();
    }

    private void PerformSliceWithSelectiveGlobalImpulse(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (!ragdollInitialized && ragdollBodies.Count == 0) CollectRagdollParts();
        if (animator != null) animator.enabled = false;
        if (rootRb != null) rootRb.isKinematic = true;
        if (rootCollider != null) rootCollider.enabled = false;
        foreach (var rb in ragdollBodies) { if (rb != null) rb.isKinematic = false; }
        foreach (var col in ragdollColliders) { if (col != null) col.enabled = true; }

        SliceTarget target = ChooseSliceTarget(weapon.sliceTargets);
        List<Transform> sliceRoots = CollectSliceRoots(target);
        if (sliceRoots.Count == 0)
        {
            ApplyRagdollImpulse(hitDir, weapon, impactScale);
            if (rootTransform != null) Destroy(rootTransform.gameObject, DESTROY_DELAY);
            else Destroy(gameObject, DESTROY_DELAY);
            return;
        }

        HashSet<Rigidbody> slicedSet = new HashSet<Rigidbody>();
        foreach (var root in sliceRoots)
        {
            if (root == null) continue;
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) slicedSet.Add(rb);
        }

        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            if (slicedSet.Contains(rb)) continue;
            DisconnectJointsPointingToSet(rb, slicedSet);
        }

        foreach (var root in sliceRoots)
        {
            if (root == null) continue;

            var spawner = rootTransform.GetComponentInChildren<SliceBloodEffectSpawner>();
            if (spawner != null) spawner.SpawnBloodAtSlice(root);

            Vector3 worldPos = root.position;
            Quaternion worldRot = root.rotation;
            var partBodies = root.GetComponentsInChildren<Rigidbody>(true);
            var partCols = root.GetComponentsInChildren<Collider>(true);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                foreach (var j in t.GetComponents<Joint>()) { if (j != null) { j.connectedBody = null; Destroy(j); } }
                foreach (var c in t.GetComponentsInChildren<ConfigurableJoint>(true)) { if (c != null) { c.connectedBody = null; Destroy(c); } }
                foreach (var cj in t.GetComponentsInChildren<CharacterJoint>(true)) { if (cj != null) { cj.connectedBody = null; Destroy(cj); } }
                foreach (var hj in t.GetComponentsInChildren<HingeJoint>(true)) { if (hj != null) { hj.connectedBody = null; Destroy(hj); } }
                foreach (var fj in t.GetComponentsInChildren<FixedJoint>(true)) { if (fj != null) { fj.connectedBody = null; Destroy(fj); } }
            }

            foreach (var col in partCols) { if (col != null) col.enabled = true; }
            foreach (var rb in partBodies)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            Vector3 lateral = (transform.right * Random.Range(-1f, 1f) + transform.forward * Random.Range(-1f, 1f));
            if (lateral.sqrMagnitude > 0.0001f) lateral = lateral.normalized * 0.02f;
            root.position += lateral;

            float sImpulseBase = weapon != null ? weapon.sliceImpulse : 0f;
            float sImpulse = Randomize20Percent(sImpulseBase) * Mathf.Max(impactScale, 0f);
            if (sImpulse > 0f)
            {
                Vector3 dir = hitDir;
                if (dir.sqrMagnitude > 0.0001f) dir = new Vector3(dir.x, 0f, dir.z).normalized;
                else dir = Vector3.forward;
                Vector2 rnd2 = Random.insideUnitCircle;
                Vector3 randHoriz = (transform.right * rnd2.x + transform.forward * rnd2.y);
                Vector3 finalHoriz = (dir * 0.7f + randHoriz.normalized * 0.3f);
                finalHoriz.y = 0f;
                if (finalHoriz.sqrMagnitude > 0.0001f) finalHoriz = finalHoriz.normalized;
                Vector3 velChange = finalHoriz * sImpulse * 0.3f + Vector3.up * sImpulse * 0.7f;
                Vector3 spinAxis = MakeRandomSpinAxisAvoidPitch(dir);
                foreach (var rb in partBodies)
                {
                    if (rb == null) continue;
                    rb.AddForce(velChange, ForceMode.VelocityChange);
                    rb.AddTorque(spinAxis * sImpulse, ForceMode.VelocityChange);
                }
            }
            Destroy(root.gameObject, DESTROY_DELAY);
        }

        List<Rigidbody> nonSliced = new List<Rigidbody>();
        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            if (!slicedSet.Contains(rb)) nonSliced.Add(rb);
        }
        if (nonSliced.Count > 0)
            ApplyGlobalImpulseAndSpin(nonSliced, hitDir, weapon, impactScale);
    }

    private List<Transform> CollectSliceRoots(SliceTarget target)
    {
        var list = new List<Transform>();
        if (target == SliceTarget.All)
        {
            AddBoneIfFound(list, FindBoneByExactName(kHeadName));
            AddBoneIfFound(list, FindBoneByExactName(kLeftArmName));
            AddBoneIfFound(list, FindBoneByExactName(kRightArmName));
            AddBoneIfFound(list, FindBoneByExactName(kLeftLegName));
            AddBoneIfFound(list, FindBoneByExactName(kRightLegName));
        }
        else
        {
            Transform bone = null;
            switch (target)
            {
                case SliceTarget.Head: bone = FindBoneByExactName(kHeadName); break;
                case SliceTarget.LeftArm: bone = FindBoneByExactName(kLeftArmName); break;
                case SliceTarget.RightArm: bone = FindBoneByExactName(kRightArmName); break;
                case SliceTarget.LeftLeg: bone = FindBoneByExactName(kLeftLegName); break;
                case SliceTarget.RightLeg: bone = FindBoneByExactName(kRightLegName); break;
            }
            AddBoneIfFound(list, bone);
        }
        return list;
    }

    private void DisconnectJointsPointingToSet(Rigidbody owner, HashSet<Rigidbody> slicedSet)
    {
        foreach (var j in owner.GetComponents<Joint>())
        {
            if (j != null && j.connectedBody != null && slicedSet.Contains(j.connectedBody))
            { j.connectedBody = null; Destroy(j); }
        }
        foreach (var c in owner.GetComponents<ConfigurableJoint>())
        {
            if (c != null && c.connectedBody != null && slicedSet.Contains(c.connectedBody))
            { c.connectedBody = null; Destroy(c); }
        }
        foreach (var cj in owner.GetComponents<CharacterJoint>())
        {
            if (cj != null && cj.connectedBody != null && slicedSet.Contains(cj.connectedBody))
            { cj.connectedBody = null; Destroy(cj); }
        }
        foreach (var hj in owner.GetComponents<HingeJoint>())
        {
            if (hj != null && hj.connectedBody != null && slicedSet.Contains(hj.connectedBody))
            { hj.connectedBody = null; Destroy(hj); }
        }
        foreach (var fj in owner.GetComponents<FixedJoint>())
        {
            if (fj != null && fj.connectedBody != null && slicedSet.Contains(fj.connectedBody))
            { fj.connectedBody = null; Destroy(fj); }
        }
    }

    private SliceTarget ChooseSliceTarget(List<SliceTarget> list)
    {
        if (list == null || list.Count == 0) return SliceTarget.Head;
        return list[Random.Range(0, list.Count)];
    }

    private void AddBoneIfFound(List<Transform> list, Transform bone)
    {
        if (bone != null) list.Add(bone);
    }

    private Transform FindBoneByExactName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName) || rootTransform == null) return null;
        foreach (var tr in rootTransform.GetComponentsInChildren<Transform>(true))
        {
            if (tr != null && tr.name == exactName) return tr;
        }
        return null;
    }

    private Vector3 MakeRandomSpinAxisAvoidPitch(Vector3 hitDir)
    {
        Vector3 h = new Vector3(hitDir.x, 0f, hitDir.z);
        if (h.sqrMagnitude < 0.0001f) h = Vector3.forward;
        else h = h.normalized;
        Vector3 axis = Vector3.Cross(Vector3.up, h).normalized;
        Vector3 rnd = Random.onUnitSphere - Vector3.Project(Random.onUnitSphere, h);
        if (rnd.sqrMagnitude < 0.0001f) rnd = Vector3.up;
        else rnd = rnd.normalized;
        return (axis * 0.7f + rnd * 0.3f).normalized;
    }

    private float Randomize20Percent(float baseValue)
    {
        if (baseValue <= 0f) return 0f;
        return baseValue * Random.Range(0.8f, 1.2f);
    }

    private IEnumerator ApplySliceVelocityDelayed(Rigidbody[] bodies, Vector3 vel, Vector3 spinAxis, float spinMag)
    {
        yield return new WaitForFixedUpdate();
        foreach (var rb in bodies)
        {
            if (rb == null) continue;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = vel;
            rb.angularVelocity = spinAxis * spinMag;
#else
            rb.velocity = vel;
            rb.angularVelocity = spinAxis * spinMag;
#endif
        }
    }

    private void ApplyGlobalImpulseAndSpin(List<Rigidbody> targets, Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (targets == null || targets.Count == 0) return;
        Vector3 dir = hitDir;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) dir = dir.normalized;
        float horiz = Randomize20Percent(weapon != null ? weapon.ragdollImpulse : 0f) * Mathf.Max(impactScale, 0f);
        float up = Randomize20Percent(weapon != null ? weapon.ragdollUpImpulse : 0f) * Mathf.Max(impactScale, 0f);
        float spin = Randomize20Percent(weapon != null ? weapon.ragdollSpinTorque : 0f) * Mathf.Max(impactScale, 0f);
        Vector3 velChange = Vector3.zero;
        if (horiz > 0f && dir.sqrMagnitude > 0f) velChange += dir * horiz;
        if (up > 0f) velChange += Vector3.up * up;
        if (velChange.sqrMagnitude > 0f)
        {
            foreach (var rb in targets) { if (rb != null) rb.AddForce(velChange, ForceMode.VelocityChange); }
        }
        if (spin > 0f)
        {
            Vector3 axis = MakeRandomSpinAxisAvoidPitch(dir);
            foreach (var rb in targets) { if (rb != null) rb.AddTorque(axis * spin, ForceMode.VelocityChange); }
        }
    }

    private void PlayAnimationDeath()
    {
        var animCtrl = GetComponent<PlayerAnimationController>();
        if (animCtrl != null) animCtrl.ForceAnimationByState(PlayerState.Dead);
        else if (animator != null) animator.SetBool("IsDead", true);
        var root = transform.root;
        if (root != null)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (c != null) c.enabled = false;
        }
        if (rootRb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rootRb.linearVelocity = Vector3.zero;
#else
            rootRb.velocity = Vector3.zero;
#endif
            rootRb.angularVelocity = Vector3.zero;
            rootRb.isKinematic = true;
        }
    }

    private void ApplyRagdollImpulse(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        Vector3 dir = hitDir;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) dir = dir.normalized;
        else dir = Vector3.forward;

        float horiz = (weapon != null ? weapon.ragdollImpulse : 0f) * Mathf.Clamp(impactScale, 0f, 10f);
        float up = (weapon != null ? weapon.ragdollUpImpulse : 0f) * Mathf.Clamp(impactScale, 0f, 10f);
        float spin = (weapon != null ? weapon.ragdollSpinTorque : 0f) * Mathf.Clamp(impactScale, 0f, 10f);

        Vector3 vel = Vector3.zero;
        if (horiz > 0f && dir.sqrMagnitude > 0f) vel += dir * horiz;
        if (up > 0f) vel += Vector3.up * up;
        if (vel.sqrMagnitude > 0f)
        {
            foreach (var rb in ragdollBodies)
            { if (rb != null) rb.AddForce(vel, ForceMode.VelocityChange); }
        }
        if (spin > 0f)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, dir).normalized;
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;
            foreach (var rb in ragdollBodies)
            { if (rb != null) rb.AddTorque(axis * spin, ForceMode.VelocityChange); }
        }
    }

    /* ───────── 유틸 ───────── */
    public void SetHealth(float value)
    {
        currentHP = Mathf.Clamp(value, 0f, maxHP);

        // 에디터/디버그로 강제로 0을 넣는 경우도 있으니, 여기서도 사망 처리 보강
        if (!deadProcessed && currentHP <= 0f)
        {
            currentHP = 0f;
            Die(Vector3.zero, null, 1f);
        }
    }

    public float GetCurrentHP() => currentHP;
    public float GetMaxHP() => maxHP;
    public float GetWeight() => weight;
}
