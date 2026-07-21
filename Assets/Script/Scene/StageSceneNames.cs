/// <summary>
/// 스테이지 씬 이름 규칙.
/// </summary>
public static class StageSceneNames
{
    public const string Backup = "Stage_Backup";

    /// <summary>플레이 가능한 스테이지 씬인지. Stage00, Stage01 …</summary>
    public static bool IsStageEnvironmentScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        if (sceneName == Backup)
            return false;

        return sceneName.StartsWith("Stage");
    }
}
