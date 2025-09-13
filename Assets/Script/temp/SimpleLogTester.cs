using UnityEngine;

public class SimpleLogTester : MonoBehaviour
{
    [Header("로그 출력 주기 (초)")]
    public float interval = 0.5f;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;

            string logMessage = $"[SimpleLogTester] 오브젝트:{gameObject.name} | 위치:{transform.position} | 상태:{enabled}";

            // Enemy 상태 추가
            if (TryGetComponent<Enemy>(out var enemy))
            {
                logMessage += $" | Enemy상태:{enemy.CurrentState}";
            }

            // 실행 중인 코루틴 정보 추가
            string coroutineInfo = GetActiveCoroutineInfo();
            if (!string.IsNullOrEmpty(coroutineInfo))
            {
                logMessage += $" | 코루틴:{coroutineInfo}";
            }

            Debug.Log(logMessage);
        }
    }

    private string GetActiveCoroutineInfo()
    {
        var coroutineInfo = "";

        // EnemyAttackController 코루틴 체크
        if (TryGetComponent<EnemyAttackController>(out var attackCtrl))
        {
            if (attackCtrl.IsCooldownActive())
            {
                coroutineInfo += "Cooldown,";
            }
        }

        // EnemyRushAttack 코루틴 체크
        if (TryGetComponent<EnemyRushAttack>(out var rushAttack))
        {
            // Reflection으로 private 필드 확인 (선택적)
            var rushCoroutineField = typeof(EnemyRushAttack).GetField("rushCoroutine",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (rushCoroutineField?.GetValue(rushAttack) != null)
            {
                coroutineInfo += "Rush,";
            }
        }

        return coroutineInfo.TrimEnd(',');
    }
}