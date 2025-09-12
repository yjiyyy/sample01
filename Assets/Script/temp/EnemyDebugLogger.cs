using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Enemy))]
public class EnemyDebugLogger : MonoBehaviour
{
    private Enemy enemy;
    private EnemyAttackController attackCtrl;
    private EnemyAnimationController animCtrl;
    private EnemyHealth health;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        attackCtrl = GetComponent<EnemyAttackController>();
        animCtrl = GetComponent<EnemyAnimationController>();
        health = GetComponent<EnemyHealth>();
    }

    void Start()
    {
        StartCoroutine(LogRoutine());
    }

    private IEnumerator LogRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.2f); // 5È¸/ÃÊ

            string animName = "None";
            if (animCtrl != null && animCtrl.Animator != null)
            {
                var info = animCtrl.Animator.GetCurrentAnimatorStateInfo(0);
                var clips = animCtrl.Animator.GetCurrentAnimatorClipInfo(0);
                if (clips.Length > 0)
                    animName = clips[0].clip.name;
            }

            Debug.Log(
                $"[EnemyDebug] " +
                $"State={enemy.CurrentState} | " +
                $"Cooldown={attackCtrl?.IsCooldownActive()} | " +
                $"HP={health?.GetCurrentHP()} | " +
                $"Anim={animName}"
            );
        }
    }
}