using UnityEngine;

/// <summary>
/// 게임플레이 시간(일시정지·Time.timeScale 대응). 무적 타이머 등은 이 값으로만 감소시키면 규칙이 한곳에 모입니다.
/// </summary>
public static class GameplayTime
{
    /// <summary>UI만 켜진 일시정지(게임 로직 정지) — true이면 <see cref="DeltaTime"/> 이 0입니다.</summary>
    public static bool IsGameplayPaused { get; set; }

    /// <summary>일시정지이거나 timeScale이 0이면 0, 아니면 unscaled 프레임 간격.</summary>
    public static float DeltaTime
    {
        get
        {
            if (IsGameplayPaused)
                return 0f;
            if (Time.timeScale <= 0f)
                return 0f;
            return Time.unscaledDeltaTime;
        }
    }
}
