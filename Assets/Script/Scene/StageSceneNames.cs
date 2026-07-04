/// <summary>
/// 스테이지 Additive 씬 이름 규칙.
/// </summary>
public static class StageSceneNames
{
    public const string Core = "Stage_Core";
    public const string Backup = "Stage_Backup";

    public static bool IsCoreScene(string sceneName)
    {
        return sceneName == Core;
    }

    /// <summary>배경(아트) 스테이지 씬인지. Stage01, Stage02 …</summary>
    public static bool IsStageEnvironmentScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        if (sceneName == Core || sceneName == Backup)
            return false;

        return sceneName.StartsWith("Stage");
    }
}
