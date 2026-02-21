using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 사망 연출 전담 컴포넌트.  
/// - 애니메이션 죽음 / 랙돌 죽음 / 슬라이스(본 분리) 지원.  
/// - 랙돌 본(Rigidbody+Collider)은 Awake에서 자동 초기화(isKinematic=true, Collider. enabled=false).
/// - 슬라이스:  지정된 본을 찾아 해당 본과 모든 하위 본을 완전 독립 오브젝트로 분리(Transform.parent=null),
///   분리 트리 내/외부의 Joint 연결을 모두 끊고, 분리 파츠에만 sliceImpulse를 적용(±20% 랜덤).
/// - deathMode가 Animation이면:  몸통은 애니메이션 재생, 슬라이스 파츠만 랙돌 물리.  
/// - deathMode가 Ragdoll이면:  전신 랙돌, 슬라이스 파츠는 분리 + 더 강한 임펄스.  
/// - 7초 뒤 모든 오브젝트 파괴.
/// 
/// [Parts System]
/// - Awake/Die() 시점에 EnemyFacade.SpawnedParts에서 파츠 Rigidbody/Collider 수집. 
/// - 평소:  파츠는 "Parts" 레이어, Rigidbody kinematic, Collider disabled. 
/// - 죽을 때: 파츠를 부모에서 분리(SetParent(null)), "Ragdoll" 레이어로 변경, 
///   Rigidbody non-kinematic, Collider enabled, 타격 방향으로 sliceImpulse 적용.
/// </summary>
[DisallowMultipleComponent]
public class EnemyDie : MonoBehaviour
{
    [Header("참조")]
    public Animator animator;
    public Rigidbody rootRb;
    public Collider rootCollider;

    [Header("데스 애니메이션 파라미터 (권장:  BlendTree 사용)")]
    [Tooltip("Animator Trigger name to start death transition (default: 'Die')")]
    [SerializeField] private string deathTriggerName = "Die";
    [Tooltip("BlendTree parameter name (DeadMotionIndex)")]
    [SerializeField] private string deathIndexParam = "DeadMotionIndex";
    [Tooltip("Animator state name that holds the BlendTree (set this to your BlendTree state's name)")]
    [SerializeField] private string deathStateName = "DeadBlendTree";
    [Tooltip("Number of death variants (e.g.  3)")]
    [SerializeField] private int deathVariantCount = 3;

    [Header("랙돌 본 자동 수집")]
    public Transform excludeRoot;

    private List<Rigidbody> ragdollBodies = new List<Rigidbody>();
    private List<Collider> ragdollColliders = new List<Collider>();

    // Parts System:  파츠의 Rigidbody/Collider/GameObject를 보관
    private List<Rigidbody> partRigidbodies = new List<Rigidbody>();
    private List<Collider> partColliders = new List<Collider>();
    private List<GameObject> partGameObjects = new List<GameObject>();

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
        CollectPartRigidbodies(); // 첫 시도 (파츠가 없을 수 있음)
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

    /// <summary>
    /// Parts System: EnemyFacade.SpawnedParts에서 파츠의 Rigidbody/Collider/GameObject 수집. 
    /// - 파츠 프리펩 루트에 Rigidbody가 있으면 Ragdoll 파츠로 간주.
    /// </summary>
    private void CollectPartRigidbodies()
    {
        var facade = GetComponent<EnemyFacade>();
        if (facade == null || facade.SpawnedParts == null || facade.SpawnedParts.Count == 0)
        {
            Debug.Log("[EnemyDie] CollectPartRigidbodies: No parts found (facade or parts list empty).");
            return;
        }

        // 기존 리스트 초기화 (재수집 대비)
        partRigidbodies.Clear();
        partColliders.Clear();
        partGameObjects.Clear();

        foreach (var partObj in facade.SpawnedParts)
        {
            if (partObj == null) continue;

            // 파츠 루트에 Rigidbody가 있는지 확인
            Rigidbody partRb = partObj.GetComponent<Rigidbody>();
            if (partRb != null)
            {
                partRigidbodies.Add(partRb);
                partGameObjects.Add(partObj);

                // 파츠의 모든 Collider 수집 (루트 + 자식)
                Collider[] cols = partObj.GetComponentsInChildren<Collider>(true);
                foreach (var col in cols)
                {
                    if (col != null)
                    {
                        partColliders.Add(col);
                    }
                }

                Debug.Log($"[EnemyDie] Collected part:  '{partObj.name}' (Rigidbody: 1, Colliders: {cols.Length})");
            }
            else
            {
                Debug.LogWarning($"[EnemyDie] Part '{partObj.name}' has no Rigidbody on root.  Skipping.");
            }
        }

        Debug.Log($"[EnemyDie] Total parts collected: {partRigidbodies.Count}");
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

        // Parts System: 파츠 Rigidbody/Collider 초기화
        int disabledRbCount = 0;
        foreach (var partRb in partRigidbodies)
        {
            if (partRb == null) continue;
            partRb.isKinematic = true;
            partRb.useGravity = true;
            partRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            disabledRbCount++;
        }

        int disabledColCount = 0;
        foreach (var partCol in partColliders)
        {
            if (partCol == null) continue;
            partCol.enabled = false;
            disabledColCount++;
        }

        Debug.Log($"[EnemyDie] InitializeRagdollOff:  Part Rigidbodies set kinematic: {disabledRbCount}, Colliders disabled: {disabledColCount}");

        initialized = true;
    }

    public void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        // 파츠 재수집 (EnemyFacade.Start()가 Awake 이후 실행되므로 확실하게)
        if (partRigidbodies.Count == 0)
        {
            Debug.Log("[EnemyDie] Die(): Re-collecting parts...");
            CollectPartRigidbodies();

            // 재수집 후 초기화
            int disabledRbCount = 0;
            foreach (var partRb in partRigidbodies)
            {
                if (partRb == null) continue;
                partRb.isKinematic = true;
                partRb.useGravity = true;
                partRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                disabledRbCount++;
            }

            int disabledColCount = 0;
            foreach (var partCol in partColliders)
            {
                if (partCol == null) continue;
                partCol.enabled = false;
                disabledColCount++;
            }

            Debug.Log($"[EnemyDie] Die(): Part Rigidbodies set kinematic: {disabledRbCount}, Colliders disabled: {disabledColCount}");
        }

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

        // Parts System: 파츠 분리 + Ragdoll 활성화
        SeparateAndActivateParts(hitDir, weapon, impactScale);

        ApplyGlobalImpulseAndSpin(ragdollBodies, hitDir, weapon, impactScale);
    }

    /// <summary>
    /// Parts System: 파츠를 부모에서 분리하고 Ragdoll 활성화. 
    /// - 부모에서 분리 (SetParent(null))
    /// - 레이어를 "Ragdoll"로 변경
    /// - Rigidbody non-kinematic, Collider enabled
    /// - 타격 방향으로 sliceImpulse 적용
    /// </summary>
    private void SeparateAndActivateParts(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (partRigidbodies.Count == 0)
        {
            Debug.Log("[EnemyDie] SeparateAndActivateParts:  No parts to separate.");
            return;
        }

        int ragdollLayer = LayerMask.NameToLayer("Ragdoll");
        if (ragdollLayer == -1)
        {
            Debug.LogWarning("[EnemyDie] 'Ragdoll' layer not found!  Parts will keep their original layer.");
            ragdollLayer = 0;
        }

        // ★ weapon이 null이면 기본값 5.0f 사용 ★
        float sImpulseBase = (weapon != null ? weapon.sliceImpulse : 5.0f);
        float sImpulse = Randomize20Percent(sImpulseBase) * Mathf.Max(impactScale, 0f);

        Vector3 dir = hitDir;
        if (dir.sqrMagnitude > 0.0001f)
            dir = new Vector3(dir.x, 0f, dir.z).normalized;
        else
            dir = Vector3.forward;  // hitDir이 zero면 앞 방향

        for (int i = 0; i < partGameObjects.Count; i++)
        {
            GameObject partObj = partGameObjects[i];
            if (partObj == null) continue;

            Rigidbody partRb = partRigidbodies[i];
            if (partRb == null) continue;

            // 1. 월드 포지션/회전 저장
            Vector3 worldPos = partObj.transform.position;
            Quaternion worldRot = partObj.transform.rotation;

            // 2. 부모에서 분리
            partObj.transform.SetParent(null, worldPositionStays: true);
            partObj.name = partObj.name + "_Separated";

            // 3. 월드 포지션/회전 복원
            partObj.transform.position = worldPos;
            partObj.transform.rotation = worldRot;

            // 4. 레이어 변경 (재귀적으로 자식도)
            SetLayerRecursively(partObj, ragdollLayer);

            // 5. Rigidbody 활성화
            partRb.isKinematic = false;
            partRb.useGravity = true;
            partRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 6. Collider 활성화
            Collider[] cols = partObj.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                if (col != null) col.enabled = true;
            }

            // 7. Impulse 적용
            if (sImpulse > 0f && dir.sqrMagnitude > 0f)
            {
                Vector3 velChange = dir * sImpulse * 0.3f + Vector3.up * sImpulse * 0.7f;
                partRb.AddForce(velChange, ForceMode.VelocityChange);

                Vector3 spinAxis = MakeRandomSpinAxisAvoidPitch(dir);
                partRb.AddTorque(spinAxis * sImpulse, ForceMode.VelocityChange);
            }

            // 8. 7초 후 파괴
            Destroy(partObj, DESTROY_DELAY);

            Debug.Log($"[EnemyDie] Separated part:  '{partObj.name}' (Layer: {LayerMask.LayerToName(ragdollLayer)}, Impulse: {sImpulse:F2})");
        }

        Debug.Log($"[EnemyDie] Total parts separated: {partGameObjects.Count}");
    }

    /// <summary>
    /// GameObject와 모든 자식의 레이어를 재귀적으로 변경. 
    /// </summary>
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            if (child != null)
                SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void PerformSliceWithSelectiveGlobalImpulse(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        if (animator != null) animator.enabled = false;
        if (rootRb != null) rootRb.isKinematic = true;
        if (rootCollider != null) rootCollider.enabled = false;
        foreach (var rb in ragdollBodies) { if (rb != null) rb.isKinematic = false; }
        foreach (var col in ragdollColliders) { if (col != null) col.enabled = true; }

        // Parts System: 파츠 분리 + Ragdoll 활성화
        SeparateAndActivateParts(hitDir, weapon, impactScale);

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

            var spawner = transform.root.GetComponentInChildren<SliceBloodEffectSpawner>();
            if (spawner != null) spawner.SpawnBloodAtSlice(root);

            Vector3 worldPos = root.position;
            Quaternion worldRot = root.rotation;

            var partBodies = root.GetComponentsInChildren<Rigidbody>(true);
            var partCols = root.GetComponentsInChildren<Collider>(true);

            DisconnectJointsFromSliceToBody(root, slicedSet);

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

        // Parts System: 파츠 분리 + Ragdoll 활성화
        SeparateAndActivateParts(hitDir, weapon, impactScale);

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

            var spawner = transform.root.GetComponentInChildren<SliceBloodEffectSpawner>();
            if (spawner != null) spawner.SpawnBloodAtSlice(root);

            Vector3 worldPos = root.position;
            Quaternion worldRot = root.rotation;

            var partBodies = root.GetComponentsInChildren<Rigidbody>(true);
            var partCols = root.GetComponentsInChildren<Collider>(true);

            DisconnectJointsFromSliceToBody(root, slicedSet);

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

        Vector3 dir = hitDir;
        dir.y = 0f;
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
            if (Random.value > 0.5f) axis = -axis;

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

    /// <summary>
    /// 슬라이스 파츠 내에서, 몸통과 연결된 Joint만 끊음. 파츠 내부 Joint(슬라이스↔슬라이스)는 유지하여 트리 구조 보존.
    /// </summary>
    private void DisconnectJointsFromSliceToBody(Transform sliceRoot, HashSet<Rigidbody> slicedSet)
    {
        foreach (var t in sliceRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            var joints = t.GetComponents<Joint>();
            foreach (var j in joints)
            {
                if (j == null) continue;
                if (j.connectedBody != null && !slicedSet.Contains(j.connectedBody))
                {
                    j.connectedBody = null;
                    Object.Destroy(j);
                }
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
        Vector3 horizontalHitDir = new Vector3(hitDir.x, 0f, hitDir.z);

        if (horizontalHitDir.sqrMagnitude < 0.0001f)
        {
            horizontalHitDir = Vector3.forward;
        }
        else
        {
            horizontalHitDir = horizontalHitDir.normalized;
        }

        Vector3 spinAxis = Vector3.Cross(Vector3.up, horizontalHitDir).normalized;

        Vector3 randomOffset = Random.onUnitSphere;
        randomOffset -= Vector3.Project(randomOffset, horizontalHitDir);

        if (randomOffset.sqrMagnitude < 0.0001f)
        {
            randomOffset = Vector3.up;
        }
        else
        {
            randomOffset = randomOffset.normalized;
        }

        Vector3 finalAxis = (spinAxis * 0.7f + randomOffset * 0.3f).normalized;

        return finalAxis;
    }

    private float Randomize20Percent(float baseValue)
    {
        if (baseValue <= 0f) return 0f;
        float factor = Random.Range(0.8f, 1.2f);
        return baseValue * factor;
    }

    private void DisableNonRagdollPhysics()
    {
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            if (col == null) continue;
            if (ragdollColliders.Contains(col)) continue;
            col.enabled = false;
        }

        var allBodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in allBodies)
        {
            if (rb == null) continue;
            if (ragdollBodies.Contains(rb)) continue;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        if (rootCollider != null && !ragdollColliders.Contains(rootCollider))
            rootCollider.enabled = false;

        if (rootRb != null && !ragdollBodies.Contains(rootRb))
        {
            rootRb.isKinematic = true;
            rootRb.detectCollisions = false;
        }
    }

    private void PlayAnimationDeath()
    {
        // ★★★ Parts System:  애니메이션 죽음에서도 파츠 분리 ★★★
        SeparateAndActivateParts(Vector3.forward, null, 1f);  // 앞 방향으로 기본값 impulse

        if (animator != null)
        {
            DisableNonRagdollPhysics();

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

            if (paramFound && paramType == AnimatorControllerParameterType.Int)
            {
                animator.SetInteger(deathIndexParam, idx);
            }
            else
            {
                animator.SetFloat(deathIndexParam, (float)idx);
            }

            Debug.Log($"[EnemyDie] PlayAnimationDeath idx={idx} paramFound={paramFound} paramType={paramType} deathStateName='{deathStateName}'");

            if (!string.IsNullOrEmpty(deathStateName))
            {
                int stateHash = Animator.StringToHash(deathStateName);
                if (animator.HasState(0, stateHash))
                {
                    animator.Play(stateHash, 0, 0f);
                    animator.Update(0f);
                    return;
                }
                else
                {
                    Debug.Log($"[EnemyDie] Animator. HasState returned false for '{deathStateName}' on layer 0.");
                }
            }
            else
            {
                Debug.Log("[EnemyDie] deathStateName is empty; falling back to trigger.");
            }

            animator.SetTrigger(deathTriggerName);
            animator.Update(0f);
        }
    }
}