using UnityEngine;

[CreateAssetMenu(menuName = "Player/Melee")]
public class WeaponDataSO_Melee : WeaponDataSO
{
    private void OnValidate()
    {
        range = Mathf.Max(0f, range);
        hitBoxLifetime = Mathf.Max(0f, hitBoxLifetime);
        knockbackDuration = Mathf.Max(0f, knockbackDuration);
        stunDuration = Mathf.Max(0f, stunDuration);

#if UNITY_EDITOR
        if (meleeHitboxMode == MeleeHitboxMode.SpawnPrefab && meleeHitboxPrefab == null)
            Debug.LogWarning($"WeaponDataSO_Melee '{name}': meleeHitboxMode=SpawnPrefab인데 meleeHitboxPrefab이 비어 있습니다.");
#endif
    }
}