using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyDeath : MonoBehaviour
{
    [Header("사망 랙돌 체급(무게)")]
    public float weight = 1f;

    private readonly Dictionary<BodySliceType, string[]> sliceBones = new()
    {
        { BodySliceType.Head,    new[] { "Bip001 Head" } },
        { BodySliceType.LeftArm, new[] { "Bip001 L UpperArm" } },
        { BodySliceType.RightArm,new[] { "Bip001 R UpperArm" } },
        { BodySliceType.LeftLeg, new[] { "Bip001 L Thigh" } },
        { BodySliceType.RightLeg,new[] { "Bip001 R Thigh" } },
        { BodySliceType.All,     new[] { "Bip001 Head", "Bip001 L UpperArm", "Bip001 R UpperArm", "Bip001 L Thigh", "Bip001 R Thigh" } }
    };

    public void PlayDeath(Enemy ctx, Vector3 hitDir, WeaponDataSO weapon, float scale)
    {
        // 공통 비활성
        if (ctx.agent) ctx.agent.enabled = false;
        if (ctx.TryGetComponent(out Collider rootCol)) rootCol.enabled = false;
        if (ctx.TryGetComponent(out Rigidbody rootRb)) rootRb.isKinematic = true;
        if (ctx.TryGetComponent(out Animator rootAnim)) rootAnim.enabled = false;

        switch (weapon?.deathType ?? EnemyDeathType.Default)
        {
            case EnemyDeathType.Ragdoll:
                PlayRagdoll(hitDir, weapon, scale, ctx);
                break;
            case EnemyDeathType.Slice:
                SliceBody(ChooseRandomSlicePart(weapon), hitDir, weapon, scale, ctx);
                break;
            default:
                if (ctx.animator) ctx.animator.SetTrigger("Die");
                Object.Destroy(ctx.gameObject, 3f);
                break;
        }
    }

    private void PlayRagdoll(Vector3 hitDir, WeaponDataSO weapon, float impactScale, Enemy ctx)
    {
        float horizBase = weapon ? weapon.ragdollImpulse * impactScale : 0f;
        float upwardBase = weapon ? weapon.upwardImpulse * impactScale : 0f;
        float torqueBase = weapon ? weapon.torqueImpulse * impactScale : horizBase;

        float rand = Random.Range(0.9f, 1.1f);
        float horiz = horizBase * rand / Mathf.Max(weight, 0.01f);
        float up = upwardBase * rand / Mathf.Max(weight, 0.01f);
        float torque = torqueBase * rand;

        Vector3 force = hitDir.normalized * horiz; force.y += up;

        Rigidbody pelvisRB = ctx.GetComponentsInChildren<Rigidbody>()
                                .OrderByDescending(rb => rb.mass).FirstOrDefault();

        foreach (var rb in ctx.GetComponentsInChildren<Rigidbody>())
        {
            if (rb.transform == ctx.transform) continue;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(force * Random.Range(0.95f, 1.05f), ForceMode.Impulse);

            float partTorque = (rb == pelvisRB) ? torque : torque * 0.25f;
            rb.AddTorque(Random.onUnitSphere * partTorque, ForceMode.Impulse);
        }

        foreach (var t in ctx.GetComponentsInChildren<Transform>())
        {
            if (t == ctx.transform) continue;
            if (t.TryGetComponent(out Collider col)) col.enabled = true;
            t.gameObject.layer = LayerMask.NameToLayer("Ragdoll");
        }

        Object.Destroy(ctx.gameObject, 5f);
    }

    private void SliceBody(BodySliceType sliceType, Vector3 hitDir, WeaponDataSO weapon, float impactScale, Enemy ctx)
    {
        if (ctx.animator) ctx.animator.enabled = false;

        float horizBase = weapon ? weapon.ragdollImpulse * impactScale : 0f;
        float upwardBase = weapon ? weapon.upwardImpulse * impactScale : 0f;
        float torqueBase = weapon ? weapon.torqueImpulse * impactScale : horizBase;

        float rand = Random.Range(0.9f, 1.1f);
        float horiz = horizBase * rand / Mathf.Max(weight, 0.01f);
        float up = upwardBase * rand / Mathf.Max(weight, 0.01f);
        float torque = torqueBase * rand;

        Vector3 force = hitDir.normalized * horiz; force.y += up;

        var excluded = new HashSet<Transform>(GetSliceTransforms(ctx, sliceType));

        foreach (var rb in ctx.GetComponentsInChildren<Rigidbody>())
        {
            if (rb.transform == ctx.transform) continue;
            if (excluded.Contains(rb.transform)) continue;

            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(force * Random.Range(0.95f, 1.05f), ForceMode.Impulse);
            rb.AddTorque(Random.onUnitSphere * torque, ForceMode.Impulse);
        }

        foreach (var t in ctx.GetComponentsInChildren<Transform>())
        {
            if (t == ctx.transform) continue;
            if (t.TryGetComponent(out Collider col)) col.enabled = true;
            t.gameObject.layer = LayerMask.NameToLayer("Ragdoll");
        }

        foreach (Transform bone in excluded)
        {
            if (bone == null) continue;
            if (bone.TryGetComponent(out Rigidbody rb))
            {
                if (bone.TryGetComponent(out CharacterJoint joint)) Object.Destroy(joint);
                rb.isKinematic = false;
                rb.AddForce((hitDir + Random.insideUnitSphere).normalized * (weapon ? weapon.sliceForce : 8f), ForceMode.Impulse);
            }
            bone.SetParent(null);
            Object.Destroy(bone.gameObject, 5f);
        }

        Object.Destroy(ctx.gameObject, 5f);
    }

    private System.Collections.Generic.IEnumerable<Transform> GetSliceTransforms(Enemy ctx, BodySliceType type)
    {
        if (!sliceBones.TryGetValue(type, out var names)) yield break;
        var all = ctx.GetComponentsInChildren<Transform>();
        foreach (var n in names)
        {
            var t = all.FirstOrDefault(x => x.name == n);
            if (t != null) yield return t;
        }
    }

    private BodySliceType ChooseRandomSlicePart(WeaponDataSO weapon)
    {
        if (weapon == null || weapon.possibleSliceParts == null || weapon.possibleSliceParts.Count == 0)
            return BodySliceType.None;
        return weapon.possibleSliceParts[Random.Range(0, weapon.possibleSliceParts.Count)];
    }
}