using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기/소품 소켓 본(R_Hand_Weapon 등)에 Rigidbody·CharacterJoint를 붙입니다.
/// Collider는 넣지 않으며, 충돌은 파츠 프리팹의 DieCollider를 사용합니다.
/// </summary>
public static class RagdollAttachmentBoneBuilder
{
    public static int Build(Transform modelRoot, Dictionary<Transform, Rigidbody> boneToRb, RagdollBuildSettings settings, string logPrefix = "[Ragdoll]")
    {
        if (modelRoot == null || boneToRb == null) return 0;

        var entries = settings != null && settings.attachmentBones != null && settings.attachmentBones.Length > 0
            ? settings.attachmentBones
            : RagdollBuildSettings.GetDefaultAttachmentBones();

        int built = 0;
        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.boneName)) continue;

            Transform bone = FindBoneInChildren(modelRoot, entry.boneName);
            if (bone == null)
            {
                Debug.LogWarning($"{logPrefix} Attachment bone '{entry.boneName}' not found. Skipping.");
                continue;
            }

            Rigidbody rb = bone.GetComponent<Rigidbody>();
            if (rb == null) rb = bone.gameObject.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.mass = Mathf.Max(0.0001f, entry.mass);
            rb.linearDamping = entry.drag;
            rb.angularDamping = entry.angularDrag;
            boneToRb[bone] = rb;

            Rigidbody connected = FindConnectedRigidbody(modelRoot, entry.jointConnectedBone, boneToRb);
            if (connected == null)
                Debug.LogWarning($"{logPrefix} Joint parent '{entry.jointConnectedBone}' Rigidbody not found for '{entry.boneName}'.");

            CharacterJoint joint = bone.GetComponent<CharacterJoint>();
            if (joint == null) joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = connected;
            joint.anchor = Vector3.zero;
            joint.axis = new Vector3(0, 0, 1);
            joint.swingAxis = new Vector3(1, 0, 0);
            joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
            joint.highTwistLimit = new SoftJointLimit { limit = 20f };
            joint.swing1Limit = new SoftJointLimit { limit = 30f };
            joint.swing2Limit = new SoftJointLimit { limit = 30f };
            joint.enableProjection = true;

            built++;
        }

        if (built > 0)
            Debug.Log($"{logPrefix} Attachment ragdoll bones built: {built}");

        return built;
    }

    private static Rigidbody FindConnectedRigidbody(Transform modelRoot, string connectedBoneName, Dictionary<Transform, Rigidbody> boneToRb)
    {
        if (string.IsNullOrEmpty(connectedBoneName)) return null;

        Transform tr = FindBoneInChildren(modelRoot, connectedBoneName);
        while (tr != null)
        {
            if (boneToRb.TryGetValue(tr, out Rigidbody rb)) return rb;
            Rigidbody direct = tr.GetComponent<Rigidbody>();
            if (direct != null) return direct;
            tr = tr.parent;
        }
        return null;
    }

    private static Transform FindBoneInChildren(Transform root, string boneName)
    {
        if (root == null || string.IsNullOrEmpty(boneName)) return null;
        if (root.name == boneName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindBoneInChildren(root.GetChild(i), boneName);
            if (found != null) return found;
        }
        return null;
    }
}
