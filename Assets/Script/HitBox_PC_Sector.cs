using UnityEngine;

/// <summary>
/// 샷건(섹터·부채꼴) 전용 히트박스
/// - 스폰 시 Initialize로 dmg/radius/knockbackPower/lifetime 주입
/// - SetWeapon으로 WeaponDataSO 주입
/// - 스폰 즉시 1회 판정(근접과 동일한 즉시형)
/// - 거리감쇠는 WeaponDataSO의 옵션 사용
/// - 게임뷰 표시: LineRenderer로 "실제 섹터"를 일정 시간 표시
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

    /* ─────────── 게임뷰 시각화용 ─────────── */
    private LineRenderer actualLR;
    private Material actualMat;
    private const int kSegments = 36;

    public void Initialize(float dmg, float rad, float kbPower, float life)
    {
        damage = dmg;
        radius = rad;
        knockbackPower = kbPower;
        lifetime = life;

        Debug.Log($"[HitBox] Init(Shotgun Sector) │ dmg:{damage}, kb:{knockbackPower}, r:{radius}");
        Destroy(gameObject, lifetime);

        // 시각화 라인 준비
        EnsureActualLine();

        // 근접과 동일하게 "스폰 즉시" 1회 판정
        DoHit();
    }

    public void SetWeapon(WeaponDataSO w) => weapon = w;

    private void DoHit()
    {
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;
        float halfAngle = (weapon != null ? weapon.shotgunAngle : 90f) * 0.5f;

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

            Debug.Log($"[HitBox] collide:{col.name} | weapon:{weapon?.name}, impulse:{weapon?.ragdollImpulse}");

            // 거리감쇠 가중치
            float weight = 1f;
            if (weapon != null && weapon.shotgunUseDistanceFalloff && radius > 0.01f)
            {
                float norm = Mathf.Clamp01(1f - (dist / radius)); // 가까울수록 1
                weight = Mathf.Lerp(weapon.shotgunFalloffMin, 1f, norm);
            }

            float finalKb = knockbackPower * weight;
            float finalDmg = damage * weight;

            if (col.GetComponentInParent<Enemy>() is Enemy enemy)
            {
                Vector3 knockDir = dir; knockDir.y = 0f;
                enemy.ApplyKnockback(knockDir * finalKb, weapon);
            }

            if (col.GetComponentInParent<Health>() is Health hp)
            {
                Vector3 hitDir = dir;
                // 기존
                // hp.ApplyDamage(finalDmg, hitDir, weapon);

                // 변경
                hp.ApplyDamage(finalDmg, hitDir, weapon, weight);   // ← 거리감쇠(weight) 전달

            }
        }

        // 공격 시 "실제 섹터"를 다른 색으로 잠깐 보여줌
        if (actualLR != null && weapon != null && weapon.shotgunDebugVisualize)
        {
            UpdateActualSector(origin, forward, radius, weapon.shotgunAngle, weapon.shotgunDebugActualColor);
            // LineRenderer는 이 오브젝트의 lifetime 동안 보였다가 함께 제거됨
        }
        else if (actualLR != null)
        {
            actualLR.enabled = false;
        }
    }

    /* ─────────── LineRenderer 유틸 ─────────── */

    private void EnsureActualLine()
    {
        if (actualLR != null) return;

        var go = new GameObject("ShotgunActual_Line");
        go.transform.SetParent(transform, false);
        actualLR = go.AddComponent<LineRenderer>();
        actualLR.useWorldSpace = true;
        actualLR.loop = false;
        actualLR.widthMultiplier = 0.04f; // 미리보기보다 약간 두껍게
        actualLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        actualLR.receiveShadows = false;

        var shader = Shader.Find("Sprites/Default");
        actualMat = new Material(shader);
        actualLR.material = actualMat;

        actualLR.positionCount = kSegments + 3;
        actualLR.enabled = false;
    }

    private void UpdateActualSector(Vector3 center, Vector3 forward, float r, float angle, Color color)
    {
        if (actualLR == null) return;

        actualLR.enabled = true;
        actualLR.startColor = color;
        actualLR.endColor = color;

        int idx = 0;
        float half = angle * 0.5f;

        Vector3 leftDir = Quaternion.AngleAxis(-half, Vector3.up) * forward;
        Vector3 leftEnd = center + leftDir.normalized * r;
        actualLR.SetPosition(idx++, center);
        actualLR.SetPosition(idx++, leftEnd);

        for (int i = 1; i <= kSegments; i++)
        {
            float t = i / (float)kSegments;
            float yaw = Mathf.Lerp(-half, half, t);
            Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
            Vector3 cur = center + dir.normalized * r;
            actualLR.SetPosition(idx++, cur);
        }

        Vector3 rightDir = Quaternion.AngleAxis(half, Vector3.up) * forward;
        Vector3 rightEnd = center + rightDir.normalized * r;
        actualLR.SetPosition(idx++, rightEnd);
    }
}
