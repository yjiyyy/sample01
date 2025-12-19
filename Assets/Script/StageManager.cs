using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("스테이지 설정")]
    public StageData stageData;
    public StageUI ui;
    public EnemySpawner spawner;

    [Header("낙하 처리")]
    [Tooltip("플레이어 y가 이 값 이하로 내려가면 낙사 처리")]
    public float killY = 0f; // NEW

    private float timer;
    private int currentLevel = 0;
    private bool stageActive = false;
    private bool stageEnded = false;

    void Start()
    {
        timer = 0f;
        currentLevel = 0;
        stageActive = true;
        stageEnded = false;

        spawner.SetSpawnLevel(currentLevel);
        ui.ShowStartText();
    }

    void Update()
    {
        if (stageActive)
        {
            timer += Time.deltaTime;
            float timeRemaining = stageData.stageDuration - timer;
            timeRemaining = Mathf.Max(0, timeRemaining);
            ui.UpdateTimer(timeRemaining);

            if (timer >= stageData.stageDuration)
            {
                EndStage();
            }
        }
    }

    void EndStage()
    {
        if (stageEnded) return;
        stageEnded = true;
        stageActive = false;

        ui.ShowSuccessText();

        if (spawner != null)
            spawner.enabled = false;

        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in allEnemies)
        {
            Destroy(enemy);
        }
    }

    // 낙사 처리 훅 (플레이어가 killY 이하로 내려갔을 때 호출)
    public void HandlePlayerFall(GameObject player)
    {
        // 실제 게임 디자인에 맞는 처리: 사망 UI, 재시작, HP 0 등
        //Debug.Log($"[StageManager] Player fell below killY ({killY}).");
        // TODO: 플레이어 Health 컴포넌트가 있다면 Die() 호출 등으로 연결
        // 임시: 위치 리셋 또는 씬 리로드 등 (여기서는 로그만)
    }
}