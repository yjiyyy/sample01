using UnityEngine;

/// <summary>
/// 적 루트에 붙입니다. 몸 상태·AI 페이즈를 표정 상황으로 변환해 헤드의 EnemyFaceExpressionProfile에 전달합니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyFaceController : MonoBehaviour
{
    private Enemy _enemy;
    private EnemyAI _ai;
    private EnemyFaceExpressionProfile _profile;
    private EnemyFaceSituation _lastApplied = (EnemyFaceSituation)(-1);

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _ai = GetComponent<EnemyAI>();
    }

    /// <summary>EnemyBodyPartSlots에서 헤드 파츠 생성 후 호출.</summary>
    public void BindHead(GameObject headInstance)
    {
        _profile = headInstance != null
            ? headInstance.GetComponentInChildren<EnemyFaceExpressionProfile>(true)
            : null;

        if (_profile == null && headInstance != null)
        {
            Debug.LogWarning(
                $"[EnemyFaceController] '{headInstance.name}'에 EnemyFaceExpressionProfile이 없습니다. " +
                "헤드 프리팹에 컴포넌트를 추가하세요.", headInstance);
        }

        _lastApplied = (EnemyFaceSituation)(-1);
        RefreshFace(force: true);
    }

    /// <summary>상태 변경 직후 즉시 갱신할 때 호출.</summary>
    public void RefreshFace(bool force = false)
    {
        if (_profile == null) return;

        EnemyFaceSituation situation = ResolveCurrentSituation();
        if (!force && situation == _lastApplied)
            return;

        if (_profile.ApplySituation(situation))
            _lastApplied = situation;
    }

    private EnemyFaceSituation ResolveCurrentSituation()
    {
        if (_enemy == null)
            return EnemyFaceSituation.Combat;

        switch (_enemy.CurrentState)
        {
            case Enemy.EnemyState.Dead:
                return EnemyFaceSituation.Dead;
            case Enemy.EnemyState.Stunned:
                return EnemyFaceSituation.Stun;
            case Enemy.EnemyState.Knockback:
                return EnemyFaceSituation.Knockback;
            case Enemy.EnemyState.ShieldBreak:
                return EnemyFaceSituation.ShieldBreak;
            case Enemy.EnemyState.Attack:
                return EnemyFaceSituation.Attack;
        }

        if (_ai != null)
        {
            switch (_ai.CurrentFacePhase)
            {
                case EnemyAI.FacePhase.Peace:
                    return EnemyFaceSituation.Peace;
                case EnemyAI.FacePhase.Finding:
                    return EnemyFaceSituation.Find;
            }
        }

        return EnemyFaceSituation.Combat;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || _profile == null)
            return;

        RefreshFace();
    }
}
