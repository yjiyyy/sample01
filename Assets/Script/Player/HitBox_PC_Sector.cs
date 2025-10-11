using UnityEngine;

/// <summary>
/// 샷건(섹터·부채꼴) 전용 히트박스
/// - 스폰 시 Initialize로 dmg/radius/knockbackPower/lifetime 주입
/// - SetWeapon으로 WeaponDataSO 주입
/// - 스폰 즉시 1회 판정(근접과 동일한 즉시형)
/// - 거리감쇠는 Shotgun 전용 옵션 사용
/// - 시각화: (요청) 초록 실제선 제거, WeaponBehavior의 주황 프리뷰만 사용
/// </summary>
public class HitBox_PC_Sector : MonoBehaviour
{
    /* ─────────── 런타임 파라미터(스폰 시 주입) ─────────── */
    private float damage;
    private float radius;
    private float knockbackPower;
    private float lifetime;

    /// <summary>무기 SO 주입용</summary>
    private WeaponDataSO weapon;

    public void Initialize(float dmg, float rad, float kbPower, float life)
    {
        damage = dmg;
        radius = rad;
        knockbackPower = kbPower;
        lifetime = life;

        Debug.Log($"[HitBox] Init(Shotgun Sector) │ dmg:{damage}, kb:{knockbackPower}, r:{radius}");
        Destroy(gameObject, lifetime);

        // 근접과 동일하게 "스폰 즉시" 1회 판정
        DoHit();
    }

    public void SetWeapon(WeaponDataSO w) => weapon = w;

    private void DoHit()
    {
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;

        var sg = weapon as WeaponDataSO_Shotgun;
        float angle = sg != null ? sg.shotgunAngle : 90f;
        float halfAngle = angle * 0.5f;

        // 반경 내 후보 수집(모든 레이어 → Tag로 필터)
        Collider[] cols = Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Ignore);

        foreach (var col in cols)
        {
            if (!col.CompareTag("Enemy")) continue;

            Vector3 toTarget = col.bounds.center - origin;
            float dist = toTarget.magnitude;
            if (dist <= Mathf.Epsilon || dist > radius) continue;

            Vector3 dir = toTarget.normalized;
            float ang = Vector3.Angle(forward, dir);
            if (ang > halfAngle) continue;

            // 거리감쇠 가중치
            float weight = 1f;
            if (sg != null && sg.shotgunUseDistanceFalloff && radius > 0.01f)
            {
                float norm = Mathf.Clamp01(1f - (dist / radius)); // 가까울수록 1
                weight = Mathf.Lerp(sg.shotgunFalloffMin, 1f, norm);
            }

            float finalKb = knockbackPower * weight;
            float finalDmg = damage * weight;

            if (col.GetComponentInParent<Enemy>() is Enemy enemy)
            {
                Vector3 knockDir = dir; knockDir.y = 0f;

                // ✅ 넉백/스턴 시간·세기 모두 weight 적용 (impactScale=weight)
                enemy.ApplyKnockback(knockDir, weapon, weight);
            }

            // ✅ 데미지 1회만(여기서만) 적용
            if (col.GetComponentInParent<EnemyHealth>() is EnemyHealth hp)
            {
                Vector3 hitDir = dir;
                hp.ApplyDamage(finalDmg, hitDir, weapon, weight);
                Debug.Log($"✅ [Shotgun] EnemyHealth에 {finalDmg} 데미지 적용!(w={weight:F2})");
            }
            else
            {
                Debug.LogWarning($"❌ [Shotgun] {col.name}에서 EnemyHealth를 찾을 수 없습니다!");
            }
        }
    }
}