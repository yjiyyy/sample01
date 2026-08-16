using System;
using UnityEngine;

/// <summary>
/// 한/영 문구 한 쌍. UI·자막 등에서 재사용합니다.
/// 비어 있는 쪽은 빈 문자열로 출력합니다(임시 글자 없음).
/// </summary>
[Serializable]
public struct LocalizedString
{
    [TextArea(1, 4)]
    public string korean;

    [TextArea(1, 4)]
    public string english;

    public string Get(GameLanguage language)
    {
        switch (language)
        {
            case GameLanguage.Korean:
                return korean ?? string.Empty;
            case GameLanguage.English:
                return english ?? string.Empty;
            default:
                return korean ?? string.Empty;
        }
    }
}
