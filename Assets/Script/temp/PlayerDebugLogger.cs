using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PlayerWeaponController))]
[RequireComponent(typeof(PlayerAnimationController))]
public class PlayerDebugLogger : MonoBehaviour
{
    private PlayerWeaponController weaponController;
    private PlayerAnimationController animationController;

    private Coroutine logRoutine;

    private void Awake()
    {
        weaponController = GetComponent<PlayerWeaponController>();
        animationController = GetComponent<PlayerAnimationController>();
    }

    private void OnEnable()
    {
        if (logRoutine == null)
            logRoutine = StartCoroutine(LogRoutine());
    }

    // ❌ OnDisable 제거 (StopAllCoroutines() 하지 않음)

    private IEnumerator LogRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f);

            if (weaponController == null || animationController == null)
            {
                Debug.LogWarning("[PlayerDebugLogger] 컨트롤러 참조 없음");
                continue;
            }

            var anim = animationController.GetAnimator();
            var stateInfo = anim.GetCurrentAnimatorStateInfo(0);

            string animName = "Unknown";
            var clips = anim.GetCurrentAnimatorClipInfo(0);
            if (clips.Length > 0)
                animName = clips[0].clip.name;

            Debug.Log(
                $"[Player Debug] State={weaponController.CurrentState} | " +
                $"Anim={animName} | " +
                $"Normalized={stateInfo.normalizedTime:F2} | " +
                $"Animator.speed={anim.speed:F2}"
            );
        }
    }
}
