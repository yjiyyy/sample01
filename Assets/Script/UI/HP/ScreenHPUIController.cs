using UnityEngine;

// 화면에 미리 배치된 HP UI 컨트롤러
public class ScreenHPUIController : HPUIControllerBase
{
    protected override void Start()
    {
        base.Start();
    }

    void LateUpdate()
    {
        RefreshValues();
    }
}
