using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R_Hand_Weapon / L_Hand_Weapon / Bip001 Prop3 — 모든 사망 연출에서 본+무기 분리.
/// EnemyDie·PlayerHealth 공용.
/// </summary>
public static class AttachmentBoneDeathSlice
{
    public static readonly string[] BoneNames =
    {
        "R_Hand_Weapon",
        "L_Hand_Weapon",
        "Bip001 Prop3",
    };

    public sealed class Result
    {
        public readonly HashSet<Rigidbody> SlicedBodies = new HashSet<Rigidbody>();
        public readonly List<GameObject> SlicedRoots = new List<GameObject>();
    }

    public static bool IsUnderAttachmentBone(Transform t, Transform ownerRoot)
    {
        if (t == null) return false;
        while (t != null && t != ownerRoot)
        {
            for (int i = 0; i < BoneNames.Length; i++)
            {
                if (t.name == BoneNames[i])
                    return true;
            }
            t = t.parent;
        }
        return false;
    }

    public static Transform FindBone(Transform searchRoot, string exactName)
    {
        if (searchRoot == null || string.IsNullOrEmpty(exactName)) return null;
        foreach (var tr in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (tr != null && tr.name == exactName)
                return tr;
        }
        return null;
    }

    public static Result Perform(
        Transform ownerTransform,
        Transform searchRoot,
        Animator animator,
        Rigidbody rootRb,
        IList<Rigidbody> ragdollBodies,
        Vector3 hitDir,
        WeaponDataSO weapon,
        float impactScale,
        bool keepAnimator,
        MonoBehaviour coroutineHost,
        float destroyDelay,
        string logTag,
        bool scheduleSlicedAutoDestroy = true)
    {
        var result = new Result();
        if (searchRoot == null) return result;

        var sliceRoots = new List<Transform>(BoneNames.Length);
        foreach (string boneName in BoneNames)
        {
            Transform bone = FindBone(searchRoot, boneName);
            if (bone != null)
                sliceRoots.Add(bone);
        }
        if (sliceRoots.Count == 0) return result;

        var slicedSet = new HashSet<Rigidbody>();
        foreach (var root in sliceRoots)
        {
            if (root == null) continue;
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                if (rb != null) slicedSet.Add(rb);
        }

        Avatar originalAvatar = null;
        Quaternion savedRotation = ownerTransform != null ? ownerTransform.rotation : Quaternion.identity;
        if (keepAnimator && animator != null)
        {
            originalAvatar = animator.avatar;
            animator.avatar = null;
        }

        if (ragdollBodies != null)
        {
            foreach (var rb in ragdollBodies)
            {
                if (rb == null || slicedSet.Contains(rb)) continue;
                DisconnectJointsPointingToSet(rb, slicedSet);
            }
        }

        float sImpulseBase = weapon != null ? weapon.sliceImpulse : 0f;
        float sImpulse = Randomize20Percent(sImpulseBase) * Mathf.Max(impactScale, 0f);

        Vector3 dir = hitDir;
        if (dir.sqrMagnitude > 0.0001f) dir = new Vector3(dir.x, 0f, dir.z).normalized;

        foreach (var root in sliceRoots)
        {
            if (root == null) continue;

            Vector3 worldPos = root.position;
            Quaternion worldRot = root.rotation;

            var partBodies = root.GetComponentsInChildren<Rigidbody>(true);
            DisconnectJointsFromSliceToBody(root, slicedSet);

            root.SetParent(null, worldPositionStays: true);
            root.gameObject.name = root.gameObject.name + "_Sliced";
            root.position = worldPos;
            root.rotation = worldRot;

            DieColliderUtility.ApplyPartsLayer(root);
            DieColliderUtility.SetDieCollidersEnabled(root, true);

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
                result.SlicedBodies.Add(rb);
            }

            if (sImpulse > 0f)
            {
                Vector2 rnd2 = Random.insideUnitCircle;
                Vector3 right = ownerTransform != null ? ownerTransform.right : Vector3.right;
                Vector3 forward = ownerTransform != null ? ownerTransform.forward : Vector3.forward;
                Vector3 randHoriz = (right * rnd2.x + forward * rnd2.y);
                Vector3 finalHoriz = (dir * 0.7f + randHoriz.normalized * 0.3f);
                finalHoriz.y = 0f;
                if (finalHoriz.sqrMagnitude > 0.0001f) finalHoriz = finalHoriz.normalized;

                Vector3 velChange = finalHoriz * sImpulse * 0.3f + Vector3.up * sImpulse * 0.7f;
                Vector3 spinAxis = MakeRandomSpinAxisAvoidPitch(dir, right, forward);

                if (keepAnimator && coroutineHost != null)
                    coroutineHost.StartCoroutine(ApplySliceVelocityDelayed(partBodies, velChange, spinAxis, sImpulse));
                else
                {
                    foreach (var rb in partBodies)
                    {
                        if (rb == null) continue;
                        rb.AddForce(velChange, ForceMode.VelocityChange);
                        rb.AddTorque(spinAxis * sImpulse, ForceMode.VelocityChange);
                    }
                }
            }

            if (scheduleSlicedAutoDestroy)
                Object.Destroy(root.gameObject, destroyDelay);
            else
                result.SlicedRoots.Add(root.gameObject);

            Debug.Log($"[{logTag}] Always-sliced attachment bone: '{root.name}' (sliceImpulse: {sImpulse:F2})");
        }

        if (keepAnimator && animator != null && originalAvatar != null)
        {
            animator.avatar = originalAvatar;
            animator.Rebind();
            animator.Update(0f);
        }

        if (ownerTransform != null)
        {
            ownerTransform.rotation = savedRotation;
            if (rootRb != null)
                rootRb.MoveRotation(savedRotation);
        }

        return result;
    }

    public static List<Rigidbody> FilterForGlobalImpulse(IList<Rigidbody> source, HashSet<Rigidbody> exclude)
    {
        if (source == null || source.Count == 0 || exclude == null || exclude.Count == 0)
            return source as List<Rigidbody> ?? new List<Rigidbody>(source ?? new Rigidbody[0]);

        var filtered = new List<Rigidbody>(source.Count);
        foreach (var rb in source)
        {
            if (rb != null && !exclude.Contains(rb))
                filtered.Add(rb);
        }
        return filtered;
    }

    private static IEnumerator ApplySliceVelocityDelayed(Rigidbody[] bodies, Vector3 vel, Vector3 spinAxis, float spinMag)
    {
        yield return new WaitForFixedUpdate();
        foreach (var rb in bodies)
        {
            if (rb == null) continue;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = vel;
#else
            rb.velocity = vel;
#endif
            rb.angularVelocity = spinAxis * spinMag;
        }
    }

    private static void DisconnectJointsFromSliceToBody(Transform sliceRoot, HashSet<Rigidbody> slicedSet)
    {
        foreach (var t in sliceRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            foreach (var j in t.GetComponents<Joint>())
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

    private static void DisconnectJointsPointingToSet(Rigidbody owner, HashSet<Rigidbody> slicedSet)
    {
        foreach (var j in owner.GetComponents<Joint>())
        {
            if (j != null && j.connectedBody != null && slicedSet.Contains(j.connectedBody))
            { j.connectedBody = null; Object.Destroy(j); }
        }
        foreach (var c in owner.GetComponents<ConfigurableJoint>())
        {
            if (c != null && c.connectedBody != null && slicedSet.Contains(c.connectedBody))
            { c.connectedBody = null; Object.Destroy(c); }
        }
        foreach (var cj in owner.GetComponents<CharacterJoint>())
        {
            if (cj != null && cj.connectedBody != null && slicedSet.Contains(cj.connectedBody))
            { cj.connectedBody = null; Object.Destroy(cj); }
        }
        foreach (var hj in owner.GetComponents<HingeJoint>())
        {
            if (hj != null && hj.connectedBody != null && slicedSet.Contains(hj.connectedBody))
            { hj.connectedBody = null; Object.Destroy(hj); }
        }
        foreach (var fj in owner.GetComponents<FixedJoint>())
        {
            if (fj != null && fj.connectedBody != null && slicedSet.Contains(fj.connectedBody))
            { fj.connectedBody = null; Object.Destroy(fj); }
        }
    }

    private static Vector3 MakeRandomSpinAxisAvoidPitch(Vector3 hitDir, Vector3 right, Vector3 forward)
    {
        Vector3 horizontalHitDir = new Vector3(hitDir.x, 0f, hitDir.z);
        if (horizontalHitDir.sqrMagnitude < 0.0001f) horizontalHitDir = forward.sqrMagnitude > 0.0001f ? forward : Vector3.forward;
        else horizontalHitDir = horizontalHitDir.normalized;

        Vector3 spinAxis = Vector3.Cross(Vector3.up, horizontalHitDir).normalized;
        Vector3 randomOffset = Random.onUnitSphere;
        randomOffset -= Vector3.Project(randomOffset, horizontalHitDir);
        if (randomOffset.sqrMagnitude < 0.0001f) randomOffset = Vector3.up;
        else randomOffset = randomOffset.normalized;
        return (spinAxis * 0.7f + randomOffset * 0.3f).normalized;
    }

    private static float Randomize20Percent(float baseValue)
    {
        if (baseValue <= 0f) return 0f;
        return baseValue * Random.Range(0.8f, 1.2f);
    }
}
