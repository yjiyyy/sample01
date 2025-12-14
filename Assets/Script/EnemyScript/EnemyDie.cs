using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 사망 연출 전담 컴포넌트.
/// - 애니메이션 죽음 / 랙돌 죽음 / 슬라이스(본 분리) 지원.
/// - 랙돌 본(Rigidbody+Collider)은 Awake에서 자동 초기화(isKinematic=true, Collider.enabled=false).
/// - 슬라이스: 지정된 본을 찾아 해당 본과 모든 하위 본을 완전 독립 오브젝트로 분리(Transform.parent=null),
///   분리 트리 내/외부의 Joint 연결을 모두 끊고, 분리 파츠에만 sliceImpulse를 적용(±20% 랜덤).
/// - 비슬라이스 본들에는 기존 랙돌 임펄스/업 임펄스/토크(전체 분배: 힙=1.0, 머리=0.8, 기타=0.5) 적용(각 임펄스/토크에 ±20% 랜덤).
/// - 7초 뒤 모든 오브젝트 파괴.
/// </summary>
[DisallowMultipleComponent]
public class EnemyDie : MonoBehaviour
{
    [Header("참조")]
    public Animator animator;
    public Rigidbody rootRb;
    public Collider rootCollider;

    [Header("랙돌 본 자동 수집")]
    public Transform excludeRoot;

    private List<Rigidbody> ragdollBodies = new List<Rigidbody>();
    private List<Collider> ragdollColliders = new List<Collider>();

    [Header("중심 본(힙/골반) 지정")]
    [SerializeField] private Rigidbody hipsBody;

    private bool initialized;
    private const float DESTROY_DELAY = 7f;

    // 본 이름 매핑(정확 문자열 매칭)
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

        // 힙 자동 추정(없으면)
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

        bool doSlice = weapon != null && weapon.sliceTargets != null && weapon.sliceTargets.Count > 0;

        if (doSlice)
        {
            PerformSliceWithSelectiveGlobalImpulse(hitDir, weapon, impactScale);
        }
        else
        {
            var mode = weapon != null ? weapon.deathMode : DeathMode.Animation;
            if (mode == DeathMode.Ragdoll)
            {
                ActivateRagdollAll(hitDir, weapon, impactScale);
            }
            else
            {
                PlayAnimationDeath();
            }
        }

        Destroy(gameObject, DESTROY_DELAY);
    }

    // 랙돌 활성화: 모든 본 활성화 + 전역 임펄스/토크 적용(각 값에 ±20% 랜덤)
    private void ActivateRagdollAll(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        // 애니메이터 비활성
        if (animator != null) animator.enabled = false;

        // 루트 물리/충돌 비활성(중복 방지)
        if (rootRb != null) rootRb.isKinematic = true;
        if (rootCollider != null) rootCollider.enabled = false;

        // 랙돌 본 활성화
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

    // 슬라이스: 파츠 분리 + 파츠에 sliceImpulse(±20%) 적용 + 나머지 본에 전역 임펄스/토크(±20%) 적용
    private void PerformSliceWithSelectiveGlobalImpulse(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        // 랙돌 활성화(전역 임펄스는 지금은 적용하지 않음)
        if (animator != null) animator.enabled = false;
        if (rootRb != null) rootRb.isKinematic = true;
        if (rootCollider != null) rootCollider.enabled = false;
        foreach (var rb in ragdollBodies) { if (rb != null) rb.isKinematic = false; }
        foreach (var col in ragdollColliders) { if (col != null) col.enabled = true; }

        // 타겟 선택
        SliceTarget target = ChooseSliceTarget(weapon.sliceTargets);

        // 슬라이스 루트들 수집
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

        // 슬라이스 집합 수집
        HashSet<Rigidbody> slicedSet = new HashSet<Rigidbody>();
        foreach (var root in sliceRoots)
        {
            if (root == null) continue;
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) slicedSet.Add(rb);
        }

        // 본체 쪽 Joint에서 slicedSet을 향하는 연결 끊기
        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            if (slicedSet.Contains(rb)) continue;
            DisconnectJointsPointingToSet(rb, slicedSet);
        }

        // 파츠 독립 + 파츠 내부 Joint 제거 + sliceImpulse(±20%) 적용 + 파츠 파괴 예약
        foreach (var root in sliceRoots)
        {
            if (root == null) continue;

            // 월드로 승격(독립)
            root.SetParent(null, worldPositionStays: true);

            var partBodies = root.GetComponentsInChildren<Rigidbody>(true);
            var partCols = root.GetComponentsInChildren<Collider>(true);

            // 파츠 내부 Joint 제거
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                var joints = t.GetComponents<Joint>();
                foreach (var j in joints) { if (j == null) continue; j.connectedBody = null; Object.Destroy(j); }
                var cfgs = t.GetComponents<ConfigurableJoint>();
                foreach (var c in cfgs) { if (c == null) continue; c.connectedBody = null; Object.Destroy(c); }
                var chars = t.GetComponents<CharacterJoint>();
                foreach (var cj in chars) { if (cj == null) continue; cj.connectedBody = null; Object.Destroy(cj); }
                var hinges = t.GetComponents<HingeJoint>();
                foreach (var hj in hinges) { if (hj == null) continue; hj.connectedBody = null; Object.Destroy(hj); }
                var fixeds = t.GetComponents<FixedJoint>();
                foreach (var fj in fixeds) { if (fj == null) continue; fj.connectedBody = null; Object.Destroy(fj); }
            }

            // 활성화
            foreach (var col in partCols) { if (col != null) col.enabled = true; }
            foreach (var rb in partBodies)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // 겹침 완화 오프셋
            Vector3 lateral = (transform.right * Random.Range(-1f, 1f) + transform.forward * Random.Range(-1f, 1f));
            if (lateral.sqrMagnitude > 0.0001f) lateral = lateral.normalized * 0.02f;
            root.position += lateral;

            // sliceImpulse 적용(파츠에만) — ±20% 랜덤, 수평/위 분배(수직 비중 강화: 70%)
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

                // 수평 30% + 위 70% (요청대로 위로 더 강하게)
                Vector3 velChange = finalHoriz * sImpulse * 0.3f + Vector3.up * sImpulse * 0.7f;

                foreach (var rb in partBodies)
                {
                    if (rb == null) continue;
                    rb.AddForce(velChange, ForceMode.VelocityChange);
                }

                // 추가: 슬라이스 파츠에 랜덤 회전(토크) 부여 — ragdollSpinTorque는 쓰지 않고 sliceImpulse로 크기 결정
                // 축은 '앞으로 고꾸라짐' 억제 함수 사용
                Vector3 spinAxis = MakeRandomSpinAxisAvoidPitch(dir);
                foreach (var rb in partBodies)
                {
                    if (rb == null) continue;
                    rb.AddTorque(spinAxis * sImpulse, ForceMode.VelocityChange);
                }
            }

            Destroy(root.gameObject, DESTROY_DELAY);
        }

        // 이제 non-sliced 본들에 “전역 랙돌 임펄스/업 임펄스/토크(±20%)” 적용
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

    // 주어진 목록의 본들에 전역 임펄스(수평+업)와 토크를 적용(각 값 ±20% 랜덤)
    private void ApplyGlobalImpulseAndSpin(List<Rigidbody> targets, Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (targets == null || targets.Count == 0) return;

        Vector3 dir = hitDir; dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) dir = dir.normalized;

        // ±20% 랜덤을 각각의 입력 값에 적용
        float horizBase = (weapon != null ? weapon.ragdollImpulse : 0f);
        float upBase = (weapon != null ? weapon.ragdollUpImpulse : 0f);
        float spinBase = (weapon != null ? weapon.ragdollSpinTorque : 0f);

        float horizImpulse = Randomize20Percent(horizBase) * Mathf.Max(impactScale, 0f);
        float upImpulse = Randomize20Percent(upBase) * Mathf.Max(impactScale, 0f);
        float spin = Randomize20Percent(spinBase) * Mathf.Max(impactScale, 0f);

        // 임펄스 적용
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

        // 회전 토크(전체 분배: 힙=1.0, 머리=0.8, 기타=0.5)
        if (spin > 0f)
        {
            Vector3 axis = MakeRandomSpinAxisAvoidPitch(dir);

            foreach (var rb in targets)
            {
                if (rb == null) continue;

                float factor = 0.5f; // 기본 0.5
                if (rb == hipsBody)
                {
                    factor = 1.0f; // 힙
                }
                else
                {
                    string n = rb.name;
                    if (!string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains("head"))
                        factor = 0.8f; // 머리
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

    // 입력값에 ±20% 랜덤 계수를 적용(0.8 ~ 1.2 배)
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
            animator.SetTrigger("Die");
            animator.SetBool("IsDead", true);
        }
    }
}