/// <summary>
/// 전신 실루엣 잔상을 남길지 여부. 대시·버프 등에서 별도 컴포넌트로 구현해 끼울 수 있습니다.
/// </summary>
public interface ISilhouetteGhostSpawnSource
{
    bool ShouldSpawnSilhouettes { get; }
}
