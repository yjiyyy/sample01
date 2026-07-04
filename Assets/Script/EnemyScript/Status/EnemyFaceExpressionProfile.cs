using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 헤드 프리팹(M_Face plane)에 붙입니다.
/// 텍스처 그룹별로 담당 상황을 지정하고, 지정되지 않은 상황은 isDefaultForAll 그룹을 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyFaceExpressionProfile : MonoBehaviour
{
    public const string FaceMeshName = "M_Face";

    [Serializable]
    public class ExpressionGroup
    {
        [Tooltip("이 그룹에 사용할 표정 텍스처.")]
        public Texture2D texture;

        [Tooltip("켜면 지정되지 않은 모든 상황의 기본 표정이 됩니다. 그룹당 하나만 켜세요.")]
        public bool isDefaultForAll;

        [Tooltip("이 텍스처를 사용할 상황 (isDefaultForAll이면 비워도 됨).")]
        public List<EnemyFaceSituation> situations = new List<EnemyFaceSituation>();
    }

    [SerializeField]
    private List<ExpressionGroup> groups = new List<ExpressionGroup>
    {
        new ExpressionGroup { isDefaultForAll = true },
    };

    [SerializeField]
    private MeshRenderer faceRenderer;

    private MaterialPropertyBlock _propertyBlock;
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    public IReadOnlyList<ExpressionGroup> Groups => groups;

    private void Awake()
    {
        EnsureFaceRenderer();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureFaceRenderer();
    }
#endif

    public void EnsureFaceRenderer()
    {
        if (faceRenderer != null) return;

        foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null) continue;
            if (renderer.gameObject.name.IndexOf(FaceMeshName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                faceRenderer = renderer;
                return;
            }
        }
    }

    /// <summary>상황에 맞는 텍스처를 M_Face에 적용합니다.</summary>
    public bool ApplySituation(EnemyFaceSituation situation)
    {
        Texture2D texture = ResolveTexture(situation);
        if (texture == null || faceRenderer == null)
            return false;

        _propertyBlock ??= new MaterialPropertyBlock();
        faceRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetTexture(MainTexId, texture);
        faceRenderer.SetPropertyBlock(_propertyBlock);
        return true;
    }

    public Texture2D ResolveTexture(EnemyFaceSituation situation)
    {
        if (groups == null || groups.Count == 0)
            return null;

        ExpressionGroup fallback = null;

        for (int i = 0; i < groups.Count; i++)
        {
            ExpressionGroup group = groups[i];
            if (group == null) continue;

            if (group.isDefaultForAll)
            {
                if (fallback == null && group.texture != null)
                    fallback = group;
                continue;
            }

            if (group.texture == null || group.situations == null)
                continue;

            for (int s = 0; s < group.situations.Count; s++)
            {
                if (group.situations[s] == situation)
                    return group.texture;
            }
        }

        return fallback != null ? fallback.texture : null;
    }
}
