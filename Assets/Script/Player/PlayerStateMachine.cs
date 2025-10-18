using System;
using UnityEngine;

/// <summary>
/// 얇은 상태머신: 현재는 상태 값/이벤트만 제공
/// </summary>
[DisallowMultipleComponent]
public class PlayerStateMachine : MonoBehaviour
{
    public PlayerState Current { get; private set; } = PlayerState.Idle;
    public PlayerState Previous { get; private set; } = PlayerState.Idle;

    public event Action<PlayerState, PlayerState> OnStateChanged;

    public void Init(PlayerState initial)
    {
        Previous = initial;
        Current = initial;
    }

    public void Set(PlayerState newState)
    {
        if (newState == Current) return;
        var old = Current;
        Previous = old;
        Current = newState;
        OnStateChanged?.Invoke(old, newState);
    }
}