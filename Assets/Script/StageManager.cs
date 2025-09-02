using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("스테이지 설정")]
    public StageData stageData;       // ScriptableObject - 시간 설정
    public StageUI ui;                // UI 캔버스 스크립트
    public EnemySpawner spawner;     // 몬스터 스포너

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
        ui.ShowStartText(); // 1초간 START 텍스트 표시
    }

    void Update()
    {
        if (stageActive)
        {
            timer += Time.deltaTime;

            float timeRemaining = stageData.stageDuration - timer;
            timeRemaining = Mathf.Max(0, timeRemaining);

            ui.UpdateTimer(timeRemaining);

            // 스테이지 종료 조건 확인
            if (timer >= stageData.stageDuration)
            {
                EndStage(); // 종료 처리 분리
            }
        }
    }

    void EndStage()
    {
        if (stageEnded) return; // ✅ 이미 종료됐다면 중복 실행 방지
        stageEnded = true;
        stageActive = false;

        // UI 출력
        ui.ShowSuccessText(); // "MISSION\nSUCCESS!" 한 번만 출력

        // 스포너 멈춤
        if (spawner != null)
            spawner.enabled = false;

        // 현재 씬 내 모든 적 제거
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in allEnemies)
        {
            Destroy(enemy);
        }

        // 필요 시: 입력 비활성화, 버튼 활성화 등 추가 가능
    }
}
