using UnityEngine;

public abstract class BossAttackPattern
{
    public float cooldown = 1.0f;
    private float lastUsedTime = -Mathf.Infinity;

    public bool IsOffCooldown()
    {
        return Time.time >= lastUsedTime + cooldown;
    }

    public void MarkUsed()
    {
        lastUsedTime = Time.time;
    }

    public abstract void Execute(Boss boss);
}
