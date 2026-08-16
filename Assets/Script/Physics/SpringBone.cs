using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 본 끝점(Verlet) 스프링. Unity 충돌 매트릭스와 무관하게,
/// 캐릭터 아래 SpringCollider(이름/마커)와 직접 충돌 판정합니다.
/// </summary>
[DefaultExecutionOrder(200)]
public class SpringBone : MonoBehaviour
{
    private const int CollisionIterations = 3;

    [Header("Tip 자식 본")]
    public Transform child;

    [Header("본 방향 (local axis 기준)")]
    public Vector3 boneAxis = new Vector3(-1, 0, 0);

    [Header("힘 설정")]
    [Tooltip("끝점을 기본 방향으로 당기는 힘. 높을수록 딱딱, 낮을수록 부드럽게 늘어남")]
    public float stiffness = 0.02f;
    [Tooltip("움직임 감쇠. 높을수록 빨리 멈추고, 낮을수록 오래 흔들림")]
    public float drag = 0.25f;
    [Tooltip("헤어/의상: (0,0,0) 권장. Y 음수면 아래로 당기는 힘(축 처짐 느낌)")]
    public Vector3 externalForce = new Vector3(0f, -0.005f, 0f);

    [Header("회전 혼합 비율")]
    [Tooltip("흔들림 반영 비율. 1=100%, 0=흔들림 없음. 충돌한 프레임은 무조건 100% 적용.")]
    [Range(0f, 1f)] public float blend = 1f;

    [Header("회전 축 락 (로컬, 초기 포즈 기준)")]
    [Tooltip("켜면 로컬 X 회전을 초기값에 고정합니다.")]
    public bool lockRotationX = false;
    [Tooltip("켜면 로컬 Y 회전을 초기값에 고정합니다.")]
    public bool lockRotationY = false;
    [Tooltip("켜면 로컬 Z 회전을 초기값에 고정합니다.")]
    public bool lockRotationZ = false;

    [Header("충돌 (SpringCollider)")]
    [Tooltip("켜면 캐릭터의 SpringCollider와 끝점이 겹치지 않게 밀어냅니다.")]
    public bool useCollision = true;

    [Tooltip("끝점 충돌 구 반경. 0이면 이 본의 SphereCollider.radius를 쓰고, 그것도 없으면 0.04.")]
    public float collisionRadius = 0f;

    [Tooltip("비우면 캐릭터에서 SpringCollider를 자동으로 모읍니다. 직접 넣어도 됩니다.")]
    public Collider[] colliders;

    [Tooltip("켜면 Awake/Start에서 SpringCollider를 다시 수집합니다.")]
    public bool autoCollectColliders = true;

#if UNITY_EDITOR
    [Tooltip("Play 중 충돌 콜라이더를 못 찾으면 경고를 한 번 출력합니다.")]
    public bool debugMissingColliders = true;
#endif

    private float boneLength;
    private float resolvedRadius;
    private Quaternion initialLocalRotation;
    private Transform trs;
    private Vector3 prevTipPos, currTipPos;
    private Collider[] cachedColliders = System.Array.Empty<Collider>();
    private bool _warnedMissingColliders;

    private void Awake()
    {
        trs = transform;
        initialLocalRotation = trs.localRotation;

        if (child == null)
        {
            Debug.LogWarning($"[SpringBone] '{name}'에 child가 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        boneLength = Vector3.Distance(trs.position, child.position);
        if (boneLength < 1e-6f)
            boneLength = 0.01f;

        prevTipPos = currTipPos = child.position;
        resolvedRadius = ResolveCollisionRadius();

        if (autoCollectColliders)
            RefreshColliders();
        else if (HasManualColliders())
            cachedColliders = colliders;
    }

    private void Start()
    {
        if (autoCollectColliders)
            RefreshColliders();
    }

    private bool HasManualColliders()
    {
        return colliders != null && colliders.Length > 0;
    }

    /// <summary>캐릭터에서 SpringCollider를 다시 모읍니다.</summary>
    public void RefreshColliders()
    {
        if (HasManualColliders())
        {
            cachedColliders = colliders;
            return;
        }

        Transform searchRoot = FindCollisionSearchRoot();
        if (searchRoot == null)
        {
            cachedColliders = System.Array.Empty<Collider>();
            return;
        }

        var list = new List<Collider>(8);
        var all = searchRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var col = all[i];
            if (col == null) continue;

            // 자기 스프링 체인(치마 Sphere 등)은 제외
            if (col.transform == transform || col.transform.IsChildOf(transform))
                continue;

            if (!SpringBoneCollider.IsSpringColliderObject(col.transform))
                continue;

            list.Add(col);
        }

        cachedColliders = list.ToArray();
    }

    /// <summary>
    /// 파츠가 붙인 뒤에도 몸 SpringCollider를 찾도록,
    /// Animator / PlayerBodyPartSlots가 있는 조상을 우선 루트로 씁니다.
    /// </summary>
    private Transform FindCollisionSearchRoot()
    {
        Transform t = trs != null ? trs : transform;

        var parts = t.GetComponentInParent<PlayerBodyPartSlots>();
        if (parts != null)
            return parts.transform;

        var animator = t.GetComponentInParent<Animator>();
        if (animator != null)
            return animator.transform;

        return t.root;
    }

    private float ResolveCollisionRadius()
    {
        if (collisionRadius > 0f)
            return collisionRadius;

        var sphere = GetComponent<SphereCollider>();
        if (sphere != null && sphere.radius > 0f)
            return sphere.radius * GetUniformScale(sphere.transform);

        return 0.04f;
    }

    private static float GetUniformScale(Transform t)
    {
        Vector3 s = t.lossyScale;
        return Mathf.Max(0.0001f, (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float sqrDt = dt * dt;

        if (useCollision && autoCollectColliders && !HasManualColliders() &&
            (cachedColliders == null || cachedColliders.Length == 0))
        {
            RefreshColliders();
#if UNITY_EDITOR
            if (debugMissingColliders && !_warnedMissingColliders &&
                (cachedColliders == null || cachedColliders.Length == 0))
            {
                _warnedMissingColliders = true;
                Debug.LogWarning(
                    $"[SpringBone] '{name}'이 SpringCollider를 찾지 못했습니다. " +
                    "몸 프리팹 허벅지 아래 이름이 SpringCollider인 콜라이더가 있는지, Play 모드인지 확인하세요.",
                    this);
            }
#endif
        }

        trs.localRotation = initialLocalRotation;

        Vector3 force = trs.rotation * (boneAxis * stiffness) / sqrDt;
        force += (prevTipPos - currTipPos) * drag / sqrDt;
        force += externalForce / sqrDt;

        Vector3 temp = currTipPos;
        currTipPos = currTipPos + (currTipPos - prevTipPos) + force * sqrDt;
        ConstrainedToBoneLength(ref currTipPos);

        bool hit = false;
        if (useCollision)
        {
            for (int i = 0; i < CollisionIterations; i++)
            {
                if (ResolveCollisions(ref currTipPos))
                    hit = true;
                ConstrainedToBoneLength(ref currTipPos);
            }
        }

        prevTipPos = temp;

        Vector3 aimVector = trs.TransformDirection(boneAxis);
        Vector3 tipDir = currTipPos - trs.position;
        if (tipDir.sqrMagnitude > 1e-12f)
        {
            Quaternion aimRot = Quaternion.FromToRotation(aimVector, tipDir);
            // 충돌 시에는 관통이 남기지 않도록 회전을 전부 적용
            float applyBlend = hit ? 1f : blend;
            trs.rotation = Quaternion.Lerp(trs.rotation, aimRot * trs.rotation, applyBlend);
        }

        if (lockRotationX || lockRotationY || lockRotationZ)
        {
            ApplyRotationAxisLocks();
            // 락된 회전에 tip을 맞춰 다음 프레임 Verlet이 축을 무시하지 않게 함
            currTipPos = trs.position + trs.TransformDirection(boneAxis).normalized * boneLength;
        }
    }

    /// <summary>
    /// 초기 로컬 회전 대비 변화량에서 잠근 축만 0으로 만듭니다.
    /// 예: X·Y 락, Z만 허용 → 힌지처럼 Z로만 흔들림.
    /// </summary>
    private void ApplyRotationAxisLocks()
    {
        Quaternion delta = Quaternion.Inverse(initialLocalRotation) * trs.localRotation;
        Vector3 euler = delta.eulerAngles;
        if (lockRotationX) euler.x = 0f;
        if (lockRotationY) euler.y = 0f;
        if (lockRotationZ) euler.z = 0f;
        trs.localRotation = initialLocalRotation * Quaternion.Euler(euler);
    }

    private void ConstrainedToBoneLength(ref Vector3 tip)
    {
        Vector3 from = tip - trs.position;
        if (from.sqrMagnitude < 1e-12f)
            tip = trs.position + trs.TransformDirection(boneAxis).normalized * boneLength;
        else
            tip = trs.position + from.normalized * boneLength;
    }

    /// <returns>하나라도 밀어냈으면 true</returns>
    private bool ResolveCollisions(ref Vector3 tip)
    {
        if (cachedColliders == null || cachedColliders.Length == 0)
            return false;

        bool hit = false;
        float radius = resolvedRadius;
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            var col = cachedColliders[i];
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
                continue;

            if (col is CapsuleCollider capsule)
                hit |= PushOutFromCapsule(capsule, ref tip, radius);
            else if (col is SphereCollider sphere)
                hit |= PushOutFromSphere(sphere, ref tip, radius);
            else
                hit |= PushOutFromClosestPoint(col, ref tip, radius);
        }

        return hit;
    }

    private static bool PushOutFromSphere(SphereCollider sphere, ref Vector3 tip, float radius)
    {
        Vector3 center = sphere.transform.TransformPoint(sphere.center);
        float worldRadius = sphere.radius * GetUniformScale(sphere.transform) + radius;
        Vector3 delta = tip - center;
        float dist = delta.magnitude;
        if (dist < 1e-6f)
        {
            tip = center + Vector3.up * worldRadius;
            return true;
        }

        if (dist < worldRadius)
        {
            tip = center + delta * (worldRadius / dist);
            return true;
        }

        return false;
    }

    private static bool PushOutFromCapsule(CapsuleCollider capsule, ref Vector3 tip, float radius)
    {
        GetCapsuleWorldSegment(capsule, out Vector3 a, out Vector3 b, out float capsuleRadius);
        float keepOut = capsuleRadius + radius;

        Vector3 closest = ClosestPointOnSegment(tip, a, b);
        Vector3 delta = tip - closest;
        float dist = delta.magnitude;

        if (dist < 1e-6f)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < 1e-8f)
                axis = Vector3.up;
            else
                axis.Normalize();

            Vector3 fallback = Vector3.Cross(axis, Vector3.up);
            if (fallback.sqrMagnitude < 1e-6f)
                fallback = Vector3.Cross(axis, Vector3.right);
            tip = closest + fallback.normalized * keepOut;
            return true;
        }

        if (dist < keepOut)
        {
            tip = closest + delta * (keepOut / dist);
            return true;
        }

        return false;
    }

    private static bool PushOutFromClosestPoint(Collider col, ref Vector3 tip, float radius)
    {
        Vector3 closest = col.ClosestPoint(tip);
        Vector3 delta = tip - closest;
        float dist = delta.magnitude;

        if (dist < 1e-6f)
        {
            Vector3 fromCenter = tip - col.bounds.center;
            if (fromCenter.sqrMagnitude < 1e-8f)
                fromCenter = Vector3.up;
            tip = closest + fromCenter.normalized * radius;
            return true;
        }

        if (dist < radius)
        {
            tip = closest + delta * (radius / dist);
            return true;
        }

        return false;
    }

    /// <summary>
    /// TransformPoint로 양 끝을 구해, BIP 본의 음수 스케일에도 맞춰집니다.
    /// </summary>
    private static void GetCapsuleWorldSegment(
        CapsuleCollider capsule, out Vector3 pointA, out Vector3 pointB, out float worldRadius)
    {
        Transform t = capsule.transform;
        Vector3 lossy = t.lossyScale;
        Vector3 localDir;

        switch (capsule.direction)
        {
            case 0:
                localDir = Vector3.right;
                worldRadius = capsule.radius * Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
                break;
            case 2:
                localDir = Vector3.forward;
                worldRadius = capsule.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
                break;
            default:
                localDir = Vector3.up;
                worldRadius = capsule.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
                break;
        }

        worldRadius = Mathf.Max(0.0001f, worldRadius);

        // height/radius는 로컬 값, TransformPoint가 스케일 반영
        float half = capsule.height * 0.5f - capsule.radius;
        if (half < 0f) half = 0f;

        Vector3 localCenter = capsule.center;
        pointA = t.TransformPoint(localCenter + localDir * half);
        pointB = t.TransformPoint(localCenter - localDir * half);
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float abSqr = ab.sqrMagnitude;
        if (abSqr < 1e-12f)
            return a;
        float t = Vector3.Dot(point - a, ab) / abSqr;
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (child == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, child.position);
        float r = collisionRadius > 0f
            ? collisionRadius
            : (GetComponent<SphereCollider>() != null ? GetComponent<SphereCollider>().radius : 0.04f);
        Gizmos.DrawWireSphere(Application.isPlaying ? currTipPos : child.position, r);

        if (Application.isPlaying && cachedColliders != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] == null) continue;
                Gizmos.DrawWireSphere(cachedColliders[i].bounds.center, 0.02f);
            }
        }
    }
#endif
}
