using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AngelCursePoisonField : MonoBehaviour
{
    // "중첩 금지"를 위해 대상별 다음 허용 틱 시각을 전역 공유한다.
    private static readonly Dictionary<int, float> NextAllowedTickTimeByTarget = new Dictionary<int, float>();

    private readonly HashSet<EnemyHealth> targetsInField = new HashSet<EnemyHealth>();
    private readonly HashSet<PlayerHealth> playersInField = new HashSet<PlayerHealth>();

    private Upgrade_05_03_AngelCurse config;
    private CapsuleCollider triggerCollider;
    private WeaponDataSO poisonWeaponProxy;
    private GameObject visualObject;

    private float elapsed;
    private float tickTimer;

    public void Initialize(Upgrade_05_03_AngelCurse sourceConfig)
    {
        config = sourceConfig;
        if (config == null)
        {
            Destroy(gameObject);
            return;
        }

        SetupTriggerCollider();
        BuildPoisonWeaponProxy();
        TryBuildInGameVisual();

        elapsed = 0f;
        tickTimer = 0f;
    }

    private void SetupTriggerCollider()
    {
        triggerCollider = GetComponent<CapsuleCollider>();
        if (triggerCollider == null)
            triggerCollider = gameObject.AddComponent<CapsuleCollider>();

        triggerCollider.isTrigger = true;
        triggerCollider.direction = 1; // y-axis
        triggerCollider.radius = Mathf.Max(0.1f, config.poisonFieldRadius);
        triggerCollider.height = Mathf.Max(config.poisonFieldHeight, triggerCollider.radius * 2f + 0.01f);
        triggerCollider.center = config.poisonFieldCenterOffset;
    }

    private void BuildPoisonWeaponProxy()
    {
        poisonWeaponProxy = ScriptableObject.CreateInstance<WeaponDataSO>();
        poisonWeaponProxy.hideFlags = HideFlags.HideAndDontSave;
        poisonWeaponProxy.name = "AngelCurse_PoisonDoTProxy";

        poisonWeaponProxy.id = config.id;
        poisonWeaponProxy.weaponName = config.upgradeName;
        poisonWeaponProxy.category = WeaponCategory.Secondary;
        poisonWeaponProxy.damageType = AttackDamageType.ProjectileGun;
        poisonWeaponProxy.damage = Mathf.Max(0f, config.poisonDamagePerTick);

        // 순수 DoT 요구사항: 넉백/푸시/스턴 없음.
        poisonWeaponProxy.knockbackDuration = 0f;
        poisonWeaponProxy.knockbackPower = 0f;
        poisonWeaponProxy.jerkIntensity = 0f;
        poisonWeaponProxy.jerkDuration = 0f;
        poisonWeaponProxy.usePushInsteadOfKnockback = false;
        poisonWeaponProxy.stunDuration = 0f;

        poisonWeaponProxy.targetHoldDuration = 0f;
        poisonWeaponProxy.attackerHoldDuration = 0f;
        poisonWeaponProxy.deathMode = config.deathMode;
        poisonWeaponProxy.ragdollImpulse = Mathf.Max(0f, config.ragdollImpulse);
        poisonWeaponProxy.ragdollUpImpulse = config.ragdollUpImpulse;
        poisonWeaponProxy.ragdollSpinTorque = Mathf.Max(0f, config.ragdollSpinTorque);
    }

    private void TryBuildInGameVisual()
    {
        if (!config.showFieldInGame)
            return;

        visualObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visualObject.name = "FieldVisual";
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = config.poisonFieldCenterOffset;
        visualObject.transform.localRotation = Quaternion.identity;

        float radius = Mathf.Max(0.1f, config.poisonFieldRadius + config.fieldVisualPadding);
        float height = Mathf.Max(0.1f, config.poisonFieldHeight + config.fieldVisualPadding);

        // Cylinder 기본 크기: 반경 0.5 / 높이 2.0
        visualObject.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

        var col = visualObject.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        var renderer = visualObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateTransparentMaterial(config.fieldVisualColor);
        }
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        // URP -> Built-in 순서로 찾는다. 미지원 셰이더를 잡으면 게임뷰에서 마젠타가 나온다.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        var mat = new Material(shader);
        mat.hideFlags = HideFlags.HideAndDontSave;
        ConfigureTransparency(mat, color);
        return mat;
    }

    private static void ConfigureTransparency(Material mat, Color color)
    {
        // URP Lit/Unlit 공통 프로퍼티
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f); // Alpha

        // Built-in Standard 투명 설정
        if (mat.HasProperty("_Mode"))
            mat.SetFloat("_Mode", 3f); // Transparent
        if (mat.HasProperty("_SrcBlend"))
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend"))
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", 0f);

        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    private void Update()
    {
        if (config == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        elapsed += dt;
        if (elapsed >= Mathf.Max(0.1f, config.poisonFieldLifetime))
        {
            Destroy(gameObject);
            return;
        }

        tickTimer += dt;
        float tick = Mathf.Max(0.05f, config.poisonTickInterval);
        if (tickTimer < tick)
            return;

        tickTimer -= tick;
        ApplyPoisonTick(tick);
    }

    private void ApplyPoisonTick(float tickInterval)
    {
        if (targetsInField.Count == 0 && playersInField.Count == 0)
            return;

        float damage = Mathf.Max(0f, config.poisonDamagePerTick);
        if (damage <= 0f)
            return;

        float now = Time.time;
        List<EnemyHealth> invalid = null;

        if (ShouldDamageEnemies())
        {
            foreach (EnemyHealth hp in targetsInField)
            {
                if (hp == null || hp.GetCurrentHP() <= 0f)
                {
                    invalid ??= new List<EnemyHealth>();
                    invalid.Add(hp);
                    continue;
                }

                int id = hp.GetInstanceID();
                if (NextAllowedTickTimeByTarget.TryGetValue(id, out float nextTime) && now < nextTime)
                    continue;

                NextAllowedTickTimeByTarget[id] = now + tickInterval;
                hp.ApplyDamage(damage, Vector3.zero, poisonWeaponProxy, 1f, null);
            }
        }

        if (invalid != null)
        {
            for (int i = 0; i < invalid.Count; i++)
                targetsInField.Remove(invalid[i]);
        }

        if (!ShouldDamagePlayers())
            return;

        List<PlayerHealth> invalidPlayers = null;
        foreach (PlayerHealth hp in playersInField)
        {
            if (hp == null || hp.GetCurrentHP() <= 0f)
            {
                invalidPlayers ??= new List<PlayerHealth>();
                invalidPlayers.Add(hp);
                continue;
            }

            int id = hp.GetInstanceID();
            if (NextAllowedTickTimeByTarget.TryGetValue(id, out float nextTime) && now < nextTime)
                continue;

            NextAllowedTickTimeByTarget[id] = now + tickInterval;
            hp.ApplyDamage(damage, Vector3.zero, poisonWeaponProxy, 1f, null);
        }

        if (invalidPlayers == null)
            return;

        for (int i = 0; i < invalidPlayers.Count; i++)
            playersInField.Remove(invalidPlayers[i]);
    }

    private void OnTriggerEnter(Collider other)
    {
        EnemyHealth enemyHp = other.GetComponentInParent<EnemyHealth>();
        if (enemyHp != null)
            targetsInField.Add(enemyHp);

        PlayerHealth playerHp = other.GetComponentInParent<PlayerHealth>();
        if (playerHp != null)
            playersInField.Add(playerHp);
    }

    private void OnTriggerExit(Collider other)
    {
        EnemyHealth enemyHp = other.GetComponentInParent<EnemyHealth>();
        if (enemyHp != null)
            targetsInField.Remove(enemyHp);

        PlayerHealth playerHp = other.GetComponentInParent<PlayerHealth>();
        if (playerHp != null)
            playersInField.Remove(playerHp);
    }

    private void OnDestroy()
    {
        targetsInField.Clear();
        playersInField.Clear();

        if (poisonWeaponProxy != null)
        {
            Destroy(poisonWeaponProxy);
            poisonWeaponProxy = null;
        }
    }

    private bool ShouldDamageEnemies()
    {
        return config != null &&
               (config.poisonDamageTargets == AngelCurseDamageTargetType.EnemyOnly ||
                config.poisonDamageTargets == AngelCurseDamageTargetType.Both);
    }

    private bool ShouldDamagePlayers()
    {
        return config != null &&
               config.poisonDamageTargets == AngelCurseDamageTargetType.Both;
    }
}
