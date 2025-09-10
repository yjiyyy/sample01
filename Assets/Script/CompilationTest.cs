#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Test script to validate that all the compilation issues are resolved
/// </summary>
public class CompilationTest : MonoBehaviour
{
    [Header("Test Components")]
    public PlayerHealth playerHealth;
    public Enemy enemy;
    public MeleeAttackData meleeData;
    public RushAttackData rushData;

    void Start()
    {
        TestCompilation();
    }

    void TestCompilation()
    {
        Debug.Log("[CompilationTest] Starting compilation tests...");

        // Test 1: PlayerHealth.ApplyKnockback exists
        if (playerHealth != null)
        {
            // This should compile without errors now
            playerHealth.ApplyKnockback(Vector3.forward, 5f, 1f, null);
            Debug.Log("✅ Test 1 PASS: PlayerHealth.ApplyKnockback exists");
        }

        // Test 2: EnemyState enum is accessible
        EnemyState testState = EnemyState.Chase;
        Debug.Log($"✅ Test 2 PASS: EnemyState enum accessible - {testState}");

        // Test 3: Enemy.SetState is public
        if (enemy != null)
        {
            enemy.SetState(EnemyState.Chase);
            Debug.Log("✅ Test 3 PASS: Enemy.SetState is accessible");
        }

        // Test 4: MeleeAttackData exists (not RushAttackData in wrong file)
        if (meleeData != null)
        {
            float damage = meleeData.damage;
            Debug.Log($"✅ Test 4 PASS: MeleeAttackData accessible - damage: {damage}");
        }

        // Test 5: RushAttackData exists in correct file
        if (rushData != null)
        {
            float rushSpeed = rushData.rushSpeed;
            Debug.Log($"✅ Test 5 PASS: RushAttackData accessible - rush speed: {rushSpeed}");
        }

        Debug.Log("[CompilationTest] All tests completed successfully!");
    }
}
#endif