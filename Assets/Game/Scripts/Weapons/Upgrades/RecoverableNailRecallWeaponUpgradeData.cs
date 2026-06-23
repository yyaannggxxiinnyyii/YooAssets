using UnityEngine;

/// <summary>
/// 武器强化效果：将射钉枪改造为忠诚 III 的钉子回收机制。
/// </summary>
[CreateAssetMenu(fileName = "WeaponUpgrade_RecoverableNailRecall", menuName = "Game/Data/Weapon Upgrade/Recoverable Nail Recall")]
public sealed class RecoverableNailRecallWeaponUpgradeData : WeaponUpgradeData
{
    [Header("召回")]
    [Tooltip("召回钉子造成的伤害相对当前攻击力的倍率。")]
    [SerializeField] private float recallDamageMultiplier = 0.45f;
    [Tooltip("场上最多保留的可回收钉子数量。")]
    [SerializeField] private int maxRecoverableNailCount = 16;
    [Tooltip("子弹落点处生成的锚点钉子预制体，未配置时不会生成锚点。")]
    [SerializeField] private GameObject anchorNailPrefab;
    [Tooltip("锚点钉子预制体生成后的整体缩放倍率。")]
    [SerializeField] private float anchorNailScale = 1f;
    [Tooltip("锚点钉子自然存在时间，0 表示一直存在直到被召回、容量挤掉或清场。")]
    [SerializeField] private float anchorNailLifeTime = 6f;
    [Tooltip("锚点钉子被召回飞行时替换使用的正常钉子贴图，贴图默认尖头朝右。")]
    [SerializeField] private Sprite recallNailSprite;
    [Tooltip("允许生成锚点钉子的投射物来源范围。")]
    [SerializeField] private RecoverableNailSpawnScope spawnScope = RecoverableNailSpawnScope.NormalProjectilesAndChildren;
    [Tooltip("允许生成锚点钉子的最大派生代数，0 表示只允许原始弹，1 表示包含第一代派生弹。")]
    [SerializeField] private int maxChildGeneration = 1;
    [Tooltip("命中敌人并消耗子弹时是否生成锚点钉子。")]
    [SerializeField] private bool spawnOnHitEnemy = true;
    [Tooltip("飞行时间结束时是否生成锚点钉子。")]
    [SerializeField] private bool spawnOnLifeTimeEnded = true;
    [Tooltip("撞到战斗边界时是否生成锚点钉子。")]
    [SerializeField] private bool spawnOnHitWall = true;

    [Header("射钉手感")]
    [Tooltip("获得忠诚 III 时额外增加的普通子弹穿透层数。")]
    [SerializeField] private int projectilePierceBonus = 1;
    [Tooltip("忠诚 III 激活后普通射钉的固定最大飞行时间，填 1 表示固定为 1 秒。")]
    [SerializeField] private float projectileLifeTimeOverrideSeconds = 1f;

    /// <summary>
    /// 开启可回收钉子机制，替换右键技能，并应用忠诚 III 的普通射钉手感调整。
    /// </summary>
    /// <param name="context">武器强化应用上下文。</param>
    public override void Apply(WeaponUpgradeApplyContext context)
    {
        if (context.Player == null || context.ProjectileCombatController == null || context.WeaponFireController == null)
        {
            Debug.LogError("[WeaponUpgrade] 忠诚 III 缺少玩家、投射物控制器或开火控制器，无法应用。", this);
            return;
        }

        context.Player.AddProjectilePierce(projectilePierceBonus);
        context.ProjectileCombatController.EnableRecoverableNailRecall(
            recallDamageMultiplier,
            maxRecoverableNailCount,
            projectileLifeTimeOverrideSeconds,
            anchorNailPrefab,
            anchorNailScale,
            anchorNailLifeTime,
            recallNailSprite,
            spawnScope,
            maxChildGeneration,
            spawnOnHitEnemy,
            spawnOnLifeTimeEnded,
            spawnOnHitWall);
        context.WeaponFireController.EnableLoyaltyRecallSkill();
    }
}
