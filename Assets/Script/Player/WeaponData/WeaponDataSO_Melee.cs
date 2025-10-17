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
    }
}