using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 사망 연출 전담 컴포넌트. 
/// - 애니메이션 죽음 / 랙돌 죽음 / 슬라이스(본 분리) 지원. 
/// - 랙돌 본(Rigidbody+Collider)은 Awake에서 자동 초기화(isKinematic=true, Collider.enabled=false).
/// - 슬라이스:  지정된 본을 찾아 해당 본과 모든 하위 본을 완전 독립 오브젝트로 분리(Transform.parent=null),
///   분리 트리 내/외부의 Joint 연결을 모두 끊고, 분리 파츠에만 sliceImpulse를 적용(±20% 랜덤).
/// - deathMode가 Animation이면:  몸통은 애니메이션 재생, 슬라이스 파츠만 랙돌 물리. 
/// - deathMode가 Ragdoll이면: 전신 랙돌, 슬라이스 파츠는 분리 + 더 강한 임펄스. 
/// - 7초 뒤 모든 오브젝트 파괴. 
/// </summary>
[DisallowMultipleComponent]
public class EnemyDie : MonoBehaviour
{
    [Header("참조")]
    public Animator animator;
    public Rigidbody rootRb;
    public Collider rootCollider;

    [Header("데스 애니메이션 파라미터 (권장: BlendTree 사용)")]
    [Tooltip("Animator Trigger name to start death transition (default: 'Die')")]
    [SerializeField] private string deathTriggerName = "Die";
    [Tooltip("BlendTree parameter name (DeadMotionIndex)")]
    [SerializeField] private string deathIndexParam = "DeadMotionIndex";
    [Tooltip("Animator state name that holds the BlendTree (set this to your BlendTree state's name)")]
    [SerializeField] private string deathStateName = "DeadBlendTree";
    [Tooltip("Number of death variants (e.g. 3)")]
    [SerializeField] private int deathVariantCount = 3;

    [Header("랙돌 본 자동 수집")]
    public Transform excludeRoot;

    private List<Rigidbody> ragdollBodies = new List<Rigidbody>();
    private List<Collider> ragdollColliders = new List<Collider>();

    [Header("중심 본(힙/골반) 지정")]
    [SerializeField] private Rigidbody hipsBody;

    private bool initialized;
    private const float DESTROY_DELAY = 7f;

    private const string kHeadName = "Bip001 Head";
    private const string kLeftArmName = "Bip001 L UpperArm";
    private const string kRightArmName = "Bip001 R UpperArm";
    private const string kLeftLegName = "Bip001 L Thigh";
    private const string kRightLegName = "Bip001 R Thigh";

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (rootRb == null) rootRb = GetComponent<Rigidbody>();
        if (excludeRoot == null) excludeRoot = this.transform;

        if (rootCollider == null)
        {
            rootCollider = GetComponent<Collider>();
            if (rootCollider == null)
            {
                var caps = GetComponentsInChildren<CapsuleCollider>(true);
                if (caps.Length > 0) rootCollider = caps[0];
            }
        }

        CollectRagdollParts();
        InitializeRagdollOff();
    }

    private void CollectRagdollParts()
    {
        ragdollBodies.Clear();
        ragdollColliders.Clear();

        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null) continue;
            if (excludeRoot != null && rb.transform == excludeRoot) continue;
            ragdollBodies.Add(rb);
        }

        if (hipsBody == null)
        {
            foreach (var rb in ragdollBodies)
            {
                string n = rb.name.ToLowerInvariant();
                if (n.Contains("hip") || n.Contains("pelvis"))
                {
                    hipsBody = rb;
                    break;
                }
            }
            if (hipsBody == null)
            {
                int maxChildren = -1;
                foreach (var rb in ragdollBodies)
                {
                    int childCount = rb.GetComponentsInChildren<Transform>(true).Length;
                    if (childCount > maxChildren)
                    {
                        maxChildren = childCount;
                        hipsBody = rb;
                    }
                }
            }
        }

        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col == null) continue;
            if (excludeRoot != null && col.transform == excludeRoot) continue;
            if (col == rootCollider) continue;
            ragdollColliders.Add(col);
        }
    }

    private void InitializeRagdollOff()
    {
        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;

            rb.position = rb.transform.position;
            rb.rotation = rb.transform.rotation;

            rb.isKinematic = true;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            col.enabled = false;
        }
        initialized = true;
    }

    public void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (!initialized) { CollectRagdollParts(); InitializeRagdollOff(); }

        var mode = weapon != null ? weapon.deathMode : DeathMode.Animation;
        bool doSlice = weapon != null && weapon.sliceTargets != null && weapon.sliceTargets.Count > 0;

        if (mode == DeathMode.Animation)
        {
            if (doSlice)
            {
                PerformSliceWithAnimationBody(hitDir, weapon, impactScale);
            }
            else
            {
                PlayAnimationDeath();
            }
        }
        else
        {
            if (doSlice)
            {
                PerformSliceWithSelectiveGlobalImpulse(hitDir, weapon, impactScale);
            }
            else
            {
                ActivateRagdollAll(hitDir, weapon, impactScale);
            }
        }

        Destroy(gameObject, DESTROY_DELAY);
    }

    private void ActivateRagdollAll(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (animator != null) animator.enabled = false;

        if (rootRb != null) rootRb.isKinematic = true;
        if (rootCollider != null) rootCollider.enabled = false;

        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            rb.isKinematic = false;
        }
        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            col.enabled = true;
        }

        ApplyGlobalImpulseAndSpin(ragdollBodies, hitDir, weapon, impactScale);
    }

    private void PerformSliceWithSelectiveGlobalImpulse(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (animator != null) animator.enabled = false;
        if (rootRb != null) rootRb.isKinematic = true;
        if (rootCollider != null) rootCollider.enabled = false;
        foreach (var rb in ragdollBodies) { if (rb != null) rb.isKinematic = false; }
        foreach (var col in ragdollColliders) { if (col != null) col.enabled = true; }

        SliceTarget target = ChooseSliceTarget(weapon.sliceTargets);

        List<Transform> sliceRoots = new List<Transform>();
        if (target == SliceTarget.All)
        {
            AddBoneIfFound(sliceRoots, FindBoneByExactName(kHeadName));
            AddBoneIfFound(sliceRoots, FindBoneByExactName(kLeftArmName));
            AddBoneIfFound(sliceRoots, FindBoneByExactName(kRightArmName));
            AddBoneIfFound(sliceRoots, FindBoneByExactName(kLeftLegName));
            AddBoneIfFound(sliceRoots, FindBoneByExactName(kRightLegName));
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
            AddBoneIfFound(sliceRoots, bone);
        }

        if (sliceRoots.Count == 0)
        {
            ApplyGlobalImpulseAndSpin(ragdollBodies, hitDir, weapon, impactScale);
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

            Vector3 worldPos = root.position;
            Quaternion worldRot = root.rotation;

            var partBodies = root.GetComponentsInChildren<Rigidbody>(true);
            var partCols = root.GetComponentsInChildren<Collider>(true);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                var joints = t.GetComponents<Joint>();
                foreach (var j in joints) { if (j == null) continue; j.connectedBody = null; Object.Destroy(j); }
                var cfgs = t.GetComponentsInChildren<ConfigurableJoint>(true);
                foreach (var c in cfgs) { if (c == null) continue; c.connectedBody = null; Object.Destroy(c); }
                var chars = t.GetComponentsInChildren<CharacterJoint>(true);
                foreach (var cj in chars) { if (cj == null) continue; cj.connectedBody = null; Object.Destroy(cj); }
                var hinges = t.GetComponentsInChildren<HingeJoint>(true);
                foreach (var hj in hinges) { if (hj == null) continue; hj.connectedBody = null; Object.Destroy(hj); }
                var fixeds = t.GetComponentsInChildren<FixedJoint>(true);
                foreach (var fj in fixeds) { if (fj == null) continue; fj.connectedBody = null; Object.Destroy(fj); }
            }

            root.SetParent(null, worldPositionStays: true);

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

            float sImpulseBase = (weapon != null ? weapon.sliceImpulse : 0f);
            float sImpulse = Randomize20Percent(sImpulseBase) * Mathf.Max(impactScale, 0f);

            if (sImpulse > 0f)
            {
                Vector3 dir = hitDir;
                if (dir.sqrMagnitude > 0.0001f) dir = new Vector3(dir.x, 0f, dir.z).normalized;

                Vector2 rnd2 = Random.insideUnitCircle;
                Vector3 randHoriz = (transform.right * rnd2.x + transform.forward * rnd2.y);
                Vector3 finalHoriz = (dir * 0.7f + randHoriz.normalized * 0.3f);
                finalHoriz.y = 0f;
                if (finalHoriz.sqrMagnitude > 0.0001f) finalHoriz = finalHoriz.normalized;

                Vector3 velChange = finalHoriz * sImpulse * 0.3f + Vector3.up * sImpulse * 0.7f;

                foreach (var rb in partBodies)
                {
                    if (rb == null) continue;
                    rb.AddForce(velChange, ForceMode.VelocityChange);
                }

                Vector3 spinAxis = MakeRandomSpinAxisAvoidPitch(dir);
                foreach (var rb in partBodies)
                {
                    if (rb == null) continue;
                    rb.AddTorque(spinAxis * sImpulse, ForceMode.VelocityChange);
                }
            }

            Destroy(root.gameObject, DESTROY_DELAY);
        }

        List<Rigidbody> nonSliced = new List<Rigidbody>(ragdollBodies.Count);
        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            if (!slicedSet.Contains(rb))
                nonSliced.Add(rb);
        }

        if (nonSliced.Count > 0)
        {
            ApplyGlobalImpulseAndSpin(nonSliced, hitDir, weapon, impactScale);
        }
    }

    private void PerformSliceWithAnimationBody(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (rootRb != null) rootRb.isKinematic = true;
        if (rootCollider != null) rootCollider.enabled = false;

        SliceTarget target = ChooseSliceTarget(weapon.sliceTargets);

        List<Transform> sliceRoots = new List<Transform>();
        if (target == SliceTarget.All)
        {
            AddBoneIfFound(sliceRoots, FindBoneByExactName(kHeadName));
            AddBoneIfFound(sliceRoots, FindBoneByExactName(kLeftArmName));
            AddBoneIfFound(sliceRoots, FindBoneByExactName(kRightArmName));
            AddBoneIfFound(sliceRoots, FindBoneByExactName(kLeftLegName));
            AddBoneIfFound(sliceRoots, FindBoneByExactName(kRightLegName));
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
            AddBoneIfFound(sliceRoots, bone);
        }

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

            Vector3 worldPos = root.position;
            Quaternion worldRot = root.rotation;

            var partBodies = root.GetComponentsInChildren<Rigidbody>(true);
            var partCols = root.GetComponentsInChildren<Collider>(true);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                var joints = t.GetComponents<Joint>();
                foreach (var j in joints) { if (j == null) continue; j.connectedBody = null; Object.Destroy(j); }
                var cfgs = t.GetComponentsInChildren<ConfigurableJoint>(true);
                foreach (var c in cfgs) { if (c == null) continue; c.connectedBody = null; Object.Destroy(c); }
                var chars = t.GetComponentsInChildren<CharacterJoint>(true);
                foreach (var cj in chars) { if (cj == null) continue; cj.connectedBody = null; Object.Destroy(cj); }
                var hinges = t.GetComponentsInChildren<HingeJoint>(true);
                foreach (var hj in hinges) { if (hj == null) continue; hj.connectedBody = null; Object.Destroy(hj); }
                var fixeds = t.GetComponentsInChildren<FixedJoint>(true);
                foreach (var fj in fixeds) { if (fj == null) continue; fj.connectedBody = null; Object.Destroy(fj); }
            }

            root.SetParent(null, worldPositionStays: true);
            root.gameObject.name = root.gameObject.name + "_Sliced";

            root.position = worldPos;
            root.rotation = worldRot;

            foreach (var col in partCols)
            {
                if (col != null) col.enabled = true;
            }

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

            float sImpulseBase = (weapon != null ? weapon.sliceImpulse : 0f);
            float sImpulse = Randomize20Percent(sImpulseBase) * Mathf.Max(impactScale, 0f);

            if (sImpulse > 0f)
            {
                Vector3 dir = hitDir;
                if (dir.sqrMagnitude > 0.0001f) dir = new Vector3(dir.x, 0f, dir.z).normalized;

                Vector2 rnd2 = Random.insideUnitCircle;
                Vector3 randHoriz = (transform.right * rnd2.x + transform.forward * rnd2.y);
                Vector3 finalHoriz = (dir * 0.7f + randHoriz.normalized * 0.3f);
                finalHoriz.y = 0f;
                if (finalHoriz.sqrMagnitude > 0.0001f) finalHoriz = finalHoriz.normalized;

                Vector3 velChange = finalHoriz * sImpulse * 0.3f + Vector3.up * sImpulse * 0.7f;
                Vector3 spinAxis = MakeRandomSpinAxisAvoidPitch(dir);

                StartCoroutine(ApplySliceVelocityDelayed(partBodies, velChange, spinAxis, sImpulse, root.name));
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
        if (rootRb != null)
            rootRb.MoveRotation(savedRotation);

        PlayAnimationDeath();
    }

    private IEnumerator ApplySliceVelocityDelayed(Rigidbody[] bodies, Vector3 vel, Vector3 spinAxis, float spinMag, string rootName)
    {
        yield return new WaitForFixedUpdate();

        foreach (var rb in bodies)
        {
            if (rb == null) continue;

            rb.linearVelocity = vel;
            rb.angularVelocity = spinAxis * spinMag;
        }
    }

    private void ApplyGlobalImpulseAndSpin(List<Rigidbody> targets, Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (targets == null || targets.Count == 0) return;

        Vector3 dir = hitDir; dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) dir = dir.normalized;

        float horizBase = (weapon != null ? weapon.ragdollImpulse : 0f);
        float upBase = (weapon != null ? weapon.ragdollUpImpulse : 0f);
        float spinBase = (weapon != null ? weapon.ragdollSpinTorque : 0f);

        float horizImpulse = Randomize20Percent(horizBase) * Mathf.Max(impactScale, 0f);
        float upImpulse = Randomize20Percent(upBase) * Mathf.Max(impactScale, 0f);
        float spin = Randomize20Percent(spinBase) * Mathf.Max(impactScale, 0f);

        Vector3 velChange = Vector3.zero;
        if (horizImpulse > 0f && dir.sqrMagnitude > 0f)
            velChange += dir * horizImpulse;
        if (upImpulse > 0f)
            velChange += Vector3.up * upImpulse;

        if (velChange.sqrMagnitude > 0f)
        {
            foreach (var rb in targets)
            {
                if (rb == null) continue;
                rb.AddForce(velChange, ForceMode.VelocityChange);
            }
        }

        if (spin > 0f)
        {
            Vector3 axis = MakeRandomSpinAxisAvoidPitch(dir);

            foreach (var rb in targets)
            {
                if (rb == null) continue;

                float factor = 0.5f;
                if (rb == hipsBody)
                {
                    factor = 1.0f;
                }
                else
                {
                    string n = rb.name;
                    if (!string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains("head"))
                        factor = 0.8f;
                }

                rb.AddTorque(axis * (spin * factor), ForceMode.VelocityChange);
            }
        }
    }

    private void DisconnectJointsPointingToSet(Rigidbody owner, HashSet<Rigidbody> slicedSet)
    {
        var joints = owner.GetComponents<Joint>();
        foreach (var j in joints)
        {
            if (j == null) continue;
            if (j.connectedBody != null && slicedSet.Contains(j.connectedBody))
            {
                j.connectedBody = null;
                Object.Destroy(j);
            }
        }
        var cfgs = owner.GetComponents<ConfigurableJoint>();
        foreach (var c in cfgs)
        {
            if (c == null) continue;
            if (c.connectedBody != null && slicedSet.Contains(c.connectedBody))
            {
                c.connectedBody = null;
                Object.Destroy(c);
            }
        }
        var chars = owner.GetComponents<CharacterJoint>();
        foreach (var cj in chars)
        {
            if (cj == null) continue;
            if (cj.connectedBody != null && slicedSet.Contains(cj.connectedBody))
            {
                cj.connectedBody = null;
                Object.Destroy(cj);
            }
        }
        var hinges = owner.GetComponents<HingeJoint>();
        foreach (var hj in hinges)
        {
            if (hj == null) continue;
            if (hj.connectedBody != null && slicedSet.Contains(hj.connectedBody))
            {
                hj.connectedBody = null;
                Object.Destroy(hj);
            }
        }
        var fixeds = owner.GetComponents<FixedJoint>();
        foreach (var fj in fixeds)
        {
            if (fj == null) continue;
            if (fj.connectedBody != null && slicedSet.Contains(fj.connectedBody))
            {
                fj.connectedBody = null;
                Object.Destroy(fj);
            }
        }
    }

    private SliceTarget ChooseSliceTarget(List<SliceTarget> list)
    {
        if (list == null || list.Count == 0) return SliceTarget.Head;
        int idx = Random.Range(0, list.Count);
        return list[idx];
    }

    private void AddBoneIfFound(List<Transform> list, Transform bone)
    {
        if (bone != null) list.Add(bone);
    }

    private Transform FindBoneByExactName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;
        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            if (tr != null && tr.name == exactName)
                return tr;
        }
        return null;
    }

    private Vector3 MakeRandomSpinAxisAvoidPitch(Vector3 hitDir)
    {
        Vector3 rand = Random.onUnitSphere;

        if (hitDir.sqrMagnitude > 0.0001f)
        {
            Vector3 h = new Vector3(hitDir.x, 0f, hitDir.z).normalized;
            rand -= Vector3.Project(rand, h);
        }

        if (rand.sqrMagnitude < 0.0001f)
        {
            rand = Vector3.up;
        }
        else
        {
            rand = (rand.normalized * 0.8f) + (Vector3.up * 0.2f);
        }

        return rand.normalized;
    }

    private float Randomize20Percent(float baseValue)
    {
        if (baseValue <= 0f) return 0f;
        float factor = Random.Range(0.8f, 1.2f);
        return baseValue * factor;
    }

    private void PlayAnimationDeath()
    {
        if (animator != null)
        {
            // Set a random DeadMotionIndex (0..deathVariantCount-1) and handle float/int param types.
            int idx = Random.Range(0, Mathf.Max(1, deathVariantCount));

            bool paramFound = false;
            AnimatorControllerParameterType paramType = AnimatorControllerParameterType.Float;
            foreach (var p in animator.parameters)
            {
                if (p.name == deathIndexParam)
                {
                    paramFound = true;
                    paramType = p.type;
                    break;
                }
            }

            // Set parameter according to detected type (Int or Float). Default to float if not found.
            if (paramFound && paramType == AnimatorControllerParameterType.Int)
            {
                animator.SetInteger(deathIndexParam, idx);
            }
            else
            {
                // use SetFloat for BlendTree float parameter compatibility
                animator.SetFloat(deathIndexParam, (float)idx);
            }

            Debug.Log($"[EnemyDie] PlayAnimationDeath idx={idx} paramFound={paramFound} paramType={paramType} deathStateName='{deathStateName}'");

            // Try to directly play the BlendTree state so the parameter is immediately used.
            if (!string.IsNullOrEmpty(deathStateName))
            {
                int stateHash = Animator.StringToHash(deathStateName);
                if (animator.HasState(0, stateHash))
                {
                    animator.Play(stateHash, 0, 0f);
                    // Force immediate evaluation so the selected clip in BlendTree is applied this frame.
                    animator.Update(0f);
                    return;
                }
                else
                {
                    Debug.Log($"[EnemyDie] Animator.HasState returned false for '{deathStateName}' on layer 0.");
                }
            }
            else
            {
                Debug.Log("[EnemyDie] deathStateName is empty; falling back to trigger.");
            }

            // Fallback: trigger the Die trigger (keep compatibility)
            animator.SetTrigger(deathTriggerName);
            animator.Update(0f);
        }
    }
}