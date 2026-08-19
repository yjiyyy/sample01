using UnityEngine;

/// <summary>
/// 게임플레이 시간(일시정지·Time.timeScale 대응). 무적 타이머 등은 이 값으로만 감소시키면 규칙이 한곳에 모입니다.
/// </summary>
public static class GameplayTime
{
    private static float timeScaleBeforePause = 1f;

    /// <summary>UI만 켜진 일시정지(게임 로직 정지) — true이면 <see cref="DeltaTime"/> 이 0입니다.</summary>
    public static bool IsGameplayPaused { get; private set; }

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

    /// <summary>전투를 멈추고 옵션 같은 UI만 동작하게 합니다.</summary>
    public static void Pause()
    {
        if (IsGameplayPaused)
            return;

        IsGameplayPaused = true;
        timeScaleBeforePause = Time.timeScale;
        if (timeScaleBeforePause <= 0f)
            timeScaleBeforePause = 1f;
        Time.timeScale = 0f;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.ClearPlayerInput();
            InputManager.Instance.SetOverlayInputBlocked(true);
        }
    }

    /// <summary>일시정지를 풀고 전투를 다시 진행합니다.</summary>
    public static void Resume()
    {
        if (!IsGameplayPaused)
            return;

        IsGameplayPaused = false;
        Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;

        if (InputManager.Instance != null)
            InputManager.Instance.SetOverlayInputBlocked(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsGameplayPaused = false;
        timeScaleBeforePause = 1f;
        Time.timeScale = 1f;
    }
}
