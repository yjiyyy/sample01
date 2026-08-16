using UnityEngine;

/// <summary>
/// 스프링본이 막을 몸체 콜라이더 표시.
/// 이름에 SpringCollider가 있거나 이 컴포넌트가 있으면 자동 수집됩니다.
/// </summary>
[DisallowMultipleComponent]
public class SpringBoneCollider : MonoBehaviour
{
    public const string NamePrefix = "SpringCollider";

    public static bool IsSpringColliderObject(Transform t)
    {
        if (t == null) return false;
        if (t.GetComponent<SpringBoneCollider>() != null) return true;
        return t.name != null && t.name.StartsWith(NamePrefix, System.StringComparison.Ordinal);
    }
}
