using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Shared physics helpers for movement-related overlap queries and push/crowd evaluation.
/// - Provides a non-alloc wrapper around Physics.OverlapCapsuleNonAlloc with a safe fallback.
/// - Evaluates whether overlapping external colliders are pushable (by mass) and returns summary data.
/// - Designed to be called from PlayerMovement and Enemy without per-call allocations (uses caller-provided buffer).
/// </summary>
public static class MovementPhysics
{
    public struct OverlapSummary
    {
        public int externalCount;          // number of external colliders (excluding self)
        public bool anyUnpushable;         // true if any external is unpushable (no rigidbody or too heavy)
        public float totalPushableMass;    // total mass of pushable rigidbodies
        public int pushableCount;          // number of pushable rigidbodies
        public Collider[] fallbackHits;    // non-null if fallback allocating API was used (caller must not modify)
        public int rawCount;               // raw count returned by nonAlloc or fallback
    }

    /// <summary>
    /// Perform OverlapCapsuleNonAlloc and produce a summary useful for movement decisions.
    /// - tempBuffer: preallocated Collider[] passed by caller (e.g. overlapBuffer)
    /// - selfIds: set of instanceIDs that belong to the caller and must be ignored
    /// - playerRb: the player's Rigidbody used to compare mass; may be null
    /// - pushableMassMultiplier: threshold multiplier for considering otherRb.mass <= playerRb.mass * multiplier as pushable
    /// Returns OverlapSummary.
    /// </summary>
    public static OverlapSummary EvaluateCapsuleOverlapForMovement(
        Vector3 bottom,
        Vector3 top,
        float radius,
        LayerMask mask,
        Collider[] tempBuffer,
        HashSet<int> selfIds,
        Rigidbody playerRb,
        float pushableMassMultiplier = 1.0f)
    {
        OverlapSummary s = new OverlapSummary();
        s.externalCount = 0;
        s.anyUnpushable = false;
        s.totalPushableMass = 0f;
        s.pushableCount = 0;
        s.fallbackHits = null;
        s.rawCount = 0;

        if (mask == 0) return s;
        if (tempBuffer == null || tempBuffer.Length == 0)
        {
            // fallback straight to allocating API (rare)
            Collider[] hitsAlloc = Physics.OverlapCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
            s.rawCount = hitsAlloc != null ? hitsAlloc.Length : 0;
            if (hitsAlloc == null || hitsAlloc.Length == 0) return s;
            for (int i = 0; i < hitsAlloc.Length; ++i)
            {
                var c = hitsAlloc[i];
                if (c == null) continue;
                if (selfIds != null && selfIds.Contains(c.GetInstanceID())) continue;
                s.externalCount++;
                var otherRb = c.attachedRigidbody;
                if (otherRb == null || otherRb.isKinematic) s.anyUnpushable = true;
                else
                {
                    float m = otherRb.mass;
                    if (playerRb != null && m <= playerRb.mass * pushableMassMultiplier)
                    {
                        s.totalPushableMass += m;
                        s.pushableCount++;
                    }
                    else if (playerRb == null)
                    {
                        // if no playerRb given, treat dynamic rigidbodies as pushable by default
                        s.totalPushableMass += m;
                        s.pushableCount++;
                    }
                    else
                    {
                        s.anyUnpushable = true;
                    }
                }
            }
            s.fallbackHits = hitsAlloc;
            return s;
        }

        // Try non-alloc version first
        int cnt = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, tempBuffer, mask, QueryTriggerInteraction.Ignore);
        s.rawCount = cnt;
        if (cnt == tempBuffer.Length)
        {
            // buffer full -> fallback to allocating API to be safe
            Collider[] hitsAlloc = Physics.OverlapCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
            s.rawCount = hitsAlloc != null ? hitsAlloc.Length : 0;
            if (hitsAlloc == null || hitsAlloc.Length == 0) return s;
            for (int i = 0; i < hitsAlloc.Length; ++i)
            {
                var c = hitsAlloc[i];
                if (c == null) continue;
                if (selfIds != null && selfIds.Contains(c.GetInstanceID())) continue;
                s.externalCount++;
                var otherRb = c.attachedRigidbody;
                if (otherRb == null || otherRb.isKinematic) s.anyUnpushable = true;
                else
                {
                    float m = otherRb.mass;
                    if (playerRb != null && m <= playerRb.mass * pushableMassMultiplier)
                    {
                        s.totalPushableMass += m;
                        s.pushableCount++;
                    }
                    else if (playerRb == null)
                    {
                        s.totalPushableMass += m;
                        s.pushableCount++;
                    }
                    else
                    {
                        s.anyUnpushable = true;
                    }
                }
            }
            s.fallbackHits = hitsAlloc;
            return s;
        }

        // Non-alloc results available in tempBuffer[0..cnt-1]
        if (cnt == 0) return s;
        for (int i = 0; i < cnt; ++i)
        {
            var c = tempBuffer[i];
            if (c == null) continue;
            if (selfIds != null && selfIds.Contains(c.GetInstanceID())) continue;
            s.externalCount++;
            var otherRb = c.attachedRigidbody;
            if (otherRb == null || otherRb.isKinematic) s.anyUnpushable = true;
            else
            {
                float m = otherRb.mass;
                if (playerRb != null && m <= playerRb.mass * pushableMassMultiplier)
                {
                    s.totalPushableMass += m;
                    s.pushableCount++;
                }
                else if (playerRb == null)
                {
                    s.totalPushableMass += m;
                    s.pushableCount++;
                }
                else
                {
                    s.anyUnpushable = true;
                }
            }
        }

        return s;
    }

    /// <summary>
    /// Apply a small instantaneous velocity change (ForceMode.VelocityChange) to pushable rigidbodies found in the overlap.
    /// - tempBuffer & fallbackHits: results returned/used from EvaluateCapsuleOverlapForMovement.
    /// - impulseBase: base magnitude (caller may scale by disp magnitude).
    /// </summary>
    public static void ApplyPushImpulseToOverlap(
        Collider[] tempBuffer,
        int tempCount,
        Collider[] fallbackHits,
        HashSet<int> selfIds,
        Rigidbody playerRb,
        float pushableMassMultiplier,
        float impulseBase)
    {
        if ((tempBuffer == null || tempBuffer.Length == 0) && (fallbackHits == null || fallbackHits.Length == 0)) return;

        if (fallbackHits != null)
        {
            for (int i = 0; i < fallbackHits.Length; ++i)
            {
                var c = fallbackHits[i];
                if (c == null) continue;
                if (selfIds != null && selfIds.Contains(c.GetInstanceID())) continue;
                var otherRb = c.attachedRigidbody;
                if (otherRb == null || otherRb.isKinematic) continue;
                if (playerRb != null && otherRb.mass > playerRb.mass * pushableMassMultiplier) continue;

                Vector3 pushDir = (playerRb != null) ? (playerRb.position - otherRb.position) : Vector3.forward;
                pushDir.y = 0f;
                if (pushDir.sqrMagnitude <= 0.0001f) pushDir = Vector3.forward;
                pushDir.Normalize();

                float massFactor = 1f / Mathf.Max(0.001f, otherRb.mass);
                otherRb.AddForce(pushDir * impulseBase * massFactor, ForceMode.VelocityChange);
            }
            return;
        }

        // tempBuffer path (nonAlloc)
        for (int i = 0; i < tempCount; ++i)
        {
            var c = tempBuffer[i];
            if (c == null) continue;
            if (selfIds != null && selfIds.Contains(c.GetInstanceID())) continue;
            var otherRb = c.attachedRigidbody;
            if (otherRb == null || otherRb.isKinematic) continue;
            if (playerRb != null && otherRb.mass > playerRb.mass * pushableMassMultiplier) continue;

            Vector3 pushDir = (playerRb != null) ? (playerRb.position - otherRb.position) : Vector3.forward;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude <= 0.0001f) pushDir = Vector3.forward;
            pushDir.Normalize();

            float massFactor = 1f / Mathf.Max(0.001f, otherRb.mass);
            otherRb.AddForce(pushDir * impulseBase * massFactor, ForceMode.VelocityChange);
        }
    }
}