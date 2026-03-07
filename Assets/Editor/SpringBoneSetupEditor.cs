using UnityEngine;
using UnityEditor;

/// <summary>
/// 헤어본·차미본(의상)에 SpringBone을 한 번에 설치하는 에디터 메뉴.
/// 메뉴: Tools > SpringBone > Setup Selected Bone Chain
/// 
/// 사용법: Hierarchy에서 본 체인의 루트를 선택한 뒤 메뉴 실행.
/// </summary>
public static class SpringBoneSetupEditor
{
    private const string MenuPath = "Tools/SpringBone/Setup Selected Bone Chain";

    [MenuItem(MenuPath, true)]
    private static bool ValidateSetup()
    {
        return Selection.activeTransform != null;
    }

    [MenuItem(MenuPath, false, 100)]
    private static void Setup()
    {
        Transform root = Selection.activeTransform;
        if (root == null)
        {
            Debug.LogWarning("[SpringBoneSetup] 선택된 오브젝트가 없습니다.");
            return;
        }

        // 단일 체인: 루트 → 첫 번째 자식 → 그 자식의 첫 번째 자식 → ...
        var chain = new System.Collections.Generic.List<Transform>();
        Transform current = root;
        while (current != null)
        {
            chain.Add(current);
            current = current.childCount > 0 ? current.GetChild(0) : null;
        }

        if (chain.Count < 2)
        {
            Debug.LogWarning($"[SpringBoneSetup] '{root.name}'에 자식 본이 없어 체인이 형성되지 않습니다. 최소 2개 본이 필요합니다.");
            return;
        }

        const int SpringBoneLayer = 16;
        int boneCount = chain.Count - 1;

        int added = 0, updated = 0;
        for (int i = 0; i < boneCount; i++)
        {
            Transform bone = chain[i];
            Transform childTransform = chain[i + 1];

            // t: 첫 본 0 → 끝 본 1
            float t = boneCount > 1 ? (float)i / (boneCount - 1) : 0f;
            float stiffnessVal = 0.01f;
            float dragVal = 0.4f;
            float extForceY = Mathf.Lerp(0f, -0.01f, t);
            float blendVal = Mathf.Lerp(0.3f, 1f, t);

            var existing = bone.GetComponent<SpringBone>();
            if (existing != null)
            {
                Undo.RecordObject(existing, "SpringBone Setup");
                existing.child = childTransform;
                existing.stiffness = stiffnessVal;
                existing.drag = dragVal;
                existing.externalForce = new Vector3(0f, extForceY, 0f);
                existing.blend = blendVal;
                if (SpringBoneLayer >= 0 && SpringBoneLayer < 32 && bone.gameObject.layer != SpringBoneLayer)
                {
                    Undo.RecordObject(bone.gameObject, "SpringBone Setup");
                    bone.gameObject.layer = SpringBoneLayer;
                }
                updated++;
                continue;
            }

            var sb = Undo.AddComponent<SpringBone>(bone.gameObject);
            sb.child = childTransform;
            sb.stiffness = stiffnessVal;
            sb.drag = dragVal;
            sb.externalForce = new Vector3(0f, extForceY, 0f);
            sb.blend = blendVal;

            // 본→자식 방향을 로컬 기준으로 자동 설정 (초기 포즈 기준)
            Vector3 toChild = bone.InverseTransformPoint(childTransform.position);
            if (toChild.sqrMagnitude > 0.0001f)
                sb.boneAxis = toChild.normalized;

            // Layer 16 (SpringBone)
            if (SpringBoneLayer >= 0 && SpringBoneLayer < 32)
            {
                Undo.RecordObject(bone.gameObject, "SpringBone Setup");
                bone.gameObject.layer = SpringBoneLayer;
            }

            added++;
        }

        string msg = added > 0 ? $"{added}개 추가" : "";
        if (updated > 0) msg += (msg.Length > 0 ? ", " : "") + $"{updated}개 갱신";
        Debug.Log($"[SpringBoneSetup] '{root.name}' 체인: {msg} (총 {boneCount}개 본). Layer 16, 값 배분 적용.");
    }
}
