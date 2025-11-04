using UnityEngine;

// 스크린(캔버스)에 미리 배치된 UI용 컨트롤러
// 위치(anchoredPosition, anchor, pivot 등)는 에디터에서 RectTransform으로 직접 설정하세요.
public class ScreenHPUIController : HPUIControllerBase
{
    protected override void Start()
    {
        base.Start();
        // 위치는 에디터에서 직접 설정합니다. 특별한 초기화 없음.
    }

    void LateUpdate()
    {
        // 값 갱신만 수행. 위치는 에디터에서 고정.
        RefreshValues();
    }
}