using UnityEngine;

/// <summary>
/// 샷건(섹터·부채꼴) 전용 히트박스
/// - 스폰 시 Initialize로 dmg/radius/knockbackPower/lifetime 주입
/// - SetWeapon으로 WeaponDataSO 주입
/// - 스폰 즉시 1회 판정(근접과 동일한 즉시형)
/// - 거리감쇠는 Shotgun 전용 옵션 사용
/// </summary>
public class HitBox_PC_Sector : MonoBehaviour
{
    private float damage;
    private float radius;
    private float knockbackPower;
    private float lifetime;
    private WeaponDataSO weapon;

    // 🆕 forward 스냅샷 오버라이드
    private bool hasForwardOverride = false;
    private Vector3 forwardOverride;

    public void Initialize(float dmg, float rad, float kbPower, float life)
    {
        damage = dmg;
        radius = rad;
        knockbackPower = kbPower;
        lifetime = life;

        Debug.Log($"[HitBox] Init(Shotgun Sector) │ dmg:{damage}, kb:{knockbackPower}, r:{radius}, hasFwdOverride:{hasForwardOverride}");
        Destroy(gameObject, lifetime);

        // 근접과 동일하게 "스폰 즉시" 1회 판정
        DoHit();
    }

    public void SetWeapon(WeaponDataSO w) => weapon = w;

    public void SetForwardOverride(Vector3 fwd)
    {
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.0001f)
        {
            hasForwardOverride = true;
            forwardOverride = fwd.normalized;
        }
        else
        {
            hasForwardOverride = false;
        }
    }

    private void DoHit()
    {
        Vector3 origin = transform.position;

        Vector3 baseForward = hasForwardOverride && forwardOverride.sqrMagnitude > 0.0001f
            ? forwardOverride
            : transform.forward;

        baseForward.y = 0f;
        if (baseForward.sqrMagnitude < 0.0001f) baseForward = Vector3.forward;

        var sg = weapon as WeaponDataSO_Shotgun;
        float angle = sg != null ? sg.shotgunAngle : 90f;
        float halfAngle = angle * 0.5f;

        // 반경 내 후보 수집(모든 레이어 → Tag로 필터)
        Collider[] cols = Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Ignore);

        foreach (var col in cols)
        {
            if (!col || !col.CompareTag("Enemy")) continue;

            Vector3 toTarget = col.bounds.center - origin;
            float dist = toTarget.magnitude;
            if (dist <= Mathf.Epsilon || dist > radius) continue;

            Vector3 dir = toTarget;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;
            dir.Normalize();

            float ang = Vector3.Angle(baseForward, dir);
            if (ang > halfAngle) continue;

            // 거리감쇠 가중치
            float weight = 1f;
            if (sg != null && sg.shotgunUseDistanceFalloff && radius > 0.01f)
            {
                float norm = Mathf.Clamp01(1f - (dist / radius)); // 가까울수록 1
                weight = Mathf.Lerp(sg.shotgunFalloffMin, 1f, norm);
            }

            float finalDmg = damage * weight;

            // 먼저 EnemyHealth에 데미지 적용(중복 방지 등 내부 처리)
            var hp = col.GetComponentInParent<EnemyHealth>();
            if (hp == null)
            {
                Debug.LogWarning($"❌ [Shotgun] {col.name}에서 EnemyHealth를 찾을 수 없습니다!");
                continue;
            }

            // 데미지
            hp.ApplyDamage(finalDmg, dir, weapon, weight);
            Debug.Log($"✅ [Shotgun] EnemyHealth에 {finalDmg} 데미지 적용!(w={weight:F2})");

            // 넉백/푸시 분기
            var enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                if (weapon != null && weapon.usePushInsteadOfKnockback)
                {
                    // Push: 상태 변화 없음
                    enemy.ApplyPush(dir, weapon, weight);
                }
                else
                {
                    // 기존 넉백(상태 변화)
                    Vector3 knockDir = dir; knockDir.y = 0f;
                    enemy.ApplyKnockback(knockDir, weapon, weight);
                }
            }
        }
    }
}