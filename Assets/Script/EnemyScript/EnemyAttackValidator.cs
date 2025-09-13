using UnityEngine;

/// <summary>
/// Runtime validator to help debug and validate the enemy attack fix
/// Attach this to any enemy GameObject to monitor its attack behavior
/// </summary>
public class EnemyAttackValidator : MonoBehaviour
{
    [Header("Validation Settings")]
    public bool enableValidation = true;
    public float logInterval = 1f; // Log status every second
    
    private Enemy enemy;
    private EnemyAttackController attackController;
    private float lastLogTime;
    private Enemy.EnemyState lastState;
    private bool lastCooldownStatus;
    
    private void Start()
    {
        enemy = GetComponent<Enemy>();
        attackController = GetComponent<EnemyAttackController>();
        
        if (enemy == null)
        {
            Debug.LogError("[EnemyAttackValidator] No Enemy component found!");
            enabled = false;
            return;
        }
        
        if (attackController == null)
        {
            Debug.LogError("[EnemyAttackValidator] No EnemyAttackController component found!");
            enabled = false;
            return;
        }
        
        Debug.Log($"[EnemyAttackValidator] Validation started for {gameObject.name} with {attackController.AttackCount} attack patterns");
        lastState = enemy.CurrentState;
        lastCooldownStatus = attackController.IsCooldownActive();
    }
    
    private void Update()
    {
        if (!enableValidation || enemy == null || attackController == null)
            return;
        
        // Check for state changes
        if (enemy.CurrentState != lastState)
        {
            Debug.Log($"[EnemyAttackValidator] {gameObject.name} state changed: {lastState} → {enemy.CurrentState}");
            lastState = enemy.CurrentState;
        }
        
        // Check for cooldown status changes
        bool currentCooldownStatus = attackController.IsCooldownActive();
        if (currentCooldownStatus != lastCooldownStatus)
        {
            Debug.Log($"[EnemyAttackValidator] {gameObject.name} cooldown status changed: {lastCooldownStatus} → {currentCooldownStatus}");
            lastCooldownStatus = currentCooldownStatus;
        }
        
        // Periodic status logging
        if (Time.time - lastLogTime >= logInterval)
        {
            LogCurrentStatus();
            lastLogTime = Time.time;
        }
    }
    
    private void LogCurrentStatus()
    {
        if (enemy == null || attackController == null) return;
        
        string status = $"[EnemyAttackValidator] {gameObject.name} Status:";
        status += $" State={enemy.CurrentState}";
        status += $" Cooldown={attackController.IsCooldownActive()}";
        
        if (attackController.AttackCount > 0)
        {
            status += $" Attacks=[";
            for (int i = 0; i < attackController.AttackCount; i++)
            {
                bool available = attackController.IsOffCooldown(i);
                float remaining = attackController.CooldownRemaining(i);
                status += $"{i}:{(available ? "Ready" : $"{remaining:F1}s")}";
                if (i < attackController.AttackCount - 1) status += ",";
            }
            status += "]";
        }
        
        Debug.Log(status);
    }
    
    /// <summary>
    /// Force test an attack selection at the current distance to player
    /// </summary>
    [ContextMenu("Test Attack Selection")]
    public void TestAttackSelection()
    {
        if (attackController == null) return;
        
        Transform player = GameObject.FindWithTag("Player")?.transform;
        if (player == null)
        {
            Debug.LogWarning("[EnemyAttackValidator] No player found for testing");
            return;
        }
        
        float distance = Vector3.Distance(transform.position, player.position);
        int selectedAttack = attackController.SelectAttackIndex(distance);
        
        Debug.Log($"[EnemyAttackValidator] Attack selection test at distance {distance:F2}: attack {selectedAttack} selected");
    }
}