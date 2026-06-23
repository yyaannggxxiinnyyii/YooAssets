using UnityEngine;

/// <summary>
/// 定义投射物武器的静态配置数据。
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "Game/Data/Weapon Data")]
public sealed class WeaponData : ScriptableObject
{
    /// <summary>
    /// 武器攻击模式，现阶段仅保留投射物。
    /// </summary>
    public enum WeaponAttackType
    {
        Projectile
    }

    [Header("基础信息")]
    [Tooltip("武器唯一标识，用于存档或逻辑查找。")]
    [SerializeField] private string weaponId = "default_weapon";
    [Tooltip("武器在 UI 中显示的名称。")]
    [SerializeField] private string displayName = "默认武器";
    [Tooltip("武器在 UI 中显示的说明。")]
    [SerializeField] private string description = "朝鼠标方向发射投射物。";
    [Tooltip("武器图标，当前 UI 可为空。")]
    [SerializeField] private Sprite icon;
    [Tooltip("武器运行时实例预制体，未配置时会使用临时武器对象。")]
    [SerializeField] private GameWeaponInstanceView weaponPrefab;
    [Tooltip("武器发射的投射物预制体，未配置时使用默认圆形子弹。")]
    [SerializeField] private GameProjectile projectilePrefab;
    [Tooltip("武器发射的投射物贴图，未配置时使用投射物预制体自身贴图。")]
    [SerializeField] private Sprite projectileSprite;
    [Tooltip("投射物命中敌人时播放的特效预制体。")]
    [SerializeField] private GameObject projectileHitEffectPrefab;
    [Tooltip("投射物消失或被命中消耗时播放的特效预制体。")]
    [SerializeField] private GameObject projectileExpireEffectPrefab;

    [Header("投射物战斗属性")]
    [Tooltip("武器攻击模式，现阶段固定为投射物。")]
    [SerializeField] private WeaponAttackType attackType = WeaponAttackType.Projectile;
    [Tooltip("武器提供的基础攻击力，会参与最终基础伤害计算。")]
    [SerializeField] private int baseDamage = 12;
    [Tooltip("普通攻击两次开火之间的最短间隔。")]
    [SerializeField] private float attackInterval = 0.55f;
    [Tooltip("普通投射物飞行速度。")]
    [SerializeField] private float projectileSpeed = 9f;
    [Tooltip("普通投射物未命中时的最大持续时间。")]
    [SerializeField] private float projectileLifeTime = 2.2f;
    [Tooltip("普通伤害暴击概率，0.05 表示 5%。")]
    [SerializeField] private float criticalRate = 0.05f;
    [Tooltip("暴击伤害倍率，1.8 表示 180%。")]
    [SerializeField] private float criticalDamage = 1.8f;
    [Tooltip("投射物初始穿透层数。")]
    [SerializeField] private int projectilePierce;
    [Tooltip("投射物初始反弹次数。")]
    [SerializeField] private int projectileBounce;
    [Tooltip("普通攻击每次发射的弹道数量。")]
    [SerializeField] private int projectileCount = 1;
    [Tooltip("弹夹最大弹药数量。")]
    [SerializeField] private int magazineCapacity = 12;
    [Tooltip("弹夹打空或手动换弹所需时间。")]
    [SerializeField] private float reloadTime = 0.8f;
    [Tooltip("武器最大能量，满能量才允许释放大招。")]
    [SerializeField] private float maxEnergy = 100f;
    [Tooltip("每秒自动恢复的能量值。")]
    [SerializeField] private float energyRecoveryPerSecond = 5f;
    [Tooltip("每次击杀敌人获得的能量值。")]
    [SerializeField] private float energyPerKill = 5f;
    [Tooltip("普通攻击偏离鼠标瞄准方向的最大角度。")]
    [SerializeField] private float accuracyAngle = 0f;
    [Tooltip("普通攻击发射后反推玩家的距离。")]
    [SerializeField] private float recoilDistance = 0f;
    [Tooltip("投射物命中敌人时击退敌人的距离。")]
    [SerializeField] private float impactDistance = 0f;
    [Tooltip("武器最高等级，当前仅作为显示和后续扩展预留。")]
    [SerializeField] private int maxLevel = 5;

    [Header("技能")]
    [Tooltip("武器技能配置，包含右键小技能和大招参数。")]
    [SerializeField] private WeaponSkillData skillData;

    [Header("武器强化")]
    [Tooltip("每次武器强化选择展示的候选数量。")]
    [SerializeField] private int weaponUpgradeOfferCount = 2;
    [Tooltip("该武器单局最大可触发的武器强化次数。")]
    [SerializeField] private int weaponUpgradeMaxCount = 4;
    [Tooltip("角色达到这些等级时触发该武器的强化选择，超过最大强化次数的等级会被忽略。")]
    [SerializeField] private int[] weaponUpgradeTriggerLevels = { 5, 10, 15, 20 };
    [Tooltip("该武器可随机到的专属强化 SO 词条池。")]
    [SerializeField] private WeaponUpgradeData[] weaponUpgradeEntries;

    /// <summary>
    /// 武器唯一标识。
    /// </summary>
    public string WeaponId => weaponId;

    /// <summary>
    /// 武器显示名称。
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// 武器说明文本。
    /// </summary>
    public string Description => description;

    /// <summary>
    /// 武器图标。
    /// </summary>
    public Sprite Icon => icon;

    /// <summary>
    /// 武器运行时实例预制体。
    /// </summary>
    public GameWeaponInstanceView WeaponPrefab => weaponPrefab;

    /// <summary>
    /// 武器发射的投射物预制体。
    /// </summary>
    public GameProjectile ProjectilePrefab => projectilePrefab;

    /// <summary>
    /// 武器发射的投射物贴图。
    /// </summary>
    public Sprite ProjectileSprite => projectileSprite;

    /// <summary>
    /// 投射物命中敌人时播放的特效预制体。
    /// </summary>
    public GameObject ProjectileHitEffectPrefab => projectileHitEffectPrefab;

    /// <summary>
    /// 投射物消失或被命中消耗时播放的特效预制体。
    /// </summary>
    public GameObject ProjectileExpireEffectPrefab => projectileExpireEffectPrefab;

    /// <summary>
    /// 武器攻击模式。
    /// </summary>
    public WeaponAttackType AttackType => attackType;

    /// <summary>
    /// 武器攻击力。
    /// </summary>
    public int BaseDamage => baseDamage;

    /// <summary>
    /// 攻击间隔。
    /// </summary>
    public float AttackInterval => attackInterval;

    /// <summary>
    /// 投射物速度。
    /// </summary>
    public float ProjectileSpeed => projectileSpeed;

    /// <summary>
    /// 投射物持续时间。
    /// </summary>
    public float ProjectileLifeTime => projectileLifeTime;

    /// <summary>
    /// 武器暴击率。
    /// </summary>
    public float CriticalRate => criticalRate;

    /// <summary>
    /// 武器暴击伤害倍率。
    /// </summary>
    public float CriticalDamage => criticalDamage;

    /// <summary>
    /// 武器子弹穿透层数。
    /// </summary>
    public int ProjectilePierce => projectilePierce;

    /// <summary>
    /// 武器子弹反弹次数。
    /// </summary>
    public int ProjectileBounce => projectileBounce;

    /// <summary>
    /// 武器单次攻击弹道数量。
    /// </summary>
    public int ProjectileCount => projectileCount;

    /// <summary>
    /// 弹夹容量。
    /// </summary>
    public int MagazineCapacity => magazineCapacity;

    /// <summary>
    /// 换弹时间。
    /// </summary>
    public float ReloadTime => reloadTime;

    /// <summary>
    /// 总能量值。
    /// </summary>
    public float MaxEnergy => maxEnergy;

    /// <summary>
    /// 每秒能量恢复值。
    /// </summary>
    public float EnergyRecoveryPerSecond => energyRecoveryPerSecond;

    /// <summary>
    /// 每次击杀获得能量。
    /// </summary>
    public float EnergyPerKill => energyPerKill;

    /// <summary>
    /// 射击准度，偏差当前瞄准方向的最大角度。
    /// </summary>
    public float AccuracyAngle => accuracyAngle;

    /// <summary>
    /// 后坐力，射击后反推玩家距离。
    /// </summary>
    public float RecoilDistance => recoilDistance;

    /// <summary>
    /// 冲击力，命中时击退敌人距离。
    /// </summary>
    public float ImpactDistance => impactDistance;

    /// <summary>
    /// 武器技能配置。
    /// </summary>
    public WeaponSkillData SkillData => skillData;

    /// <summary>
    /// 武器强化候选展示数量。
    /// </summary>
    public int WeaponUpgradeOfferCount => Mathf.Max(1, weaponUpgradeOfferCount);

    /// <summary>
    /// 武器单局最大强化次数。
    /// </summary>
    public int WeaponUpgradeMaxCount => Mathf.Max(0, weaponUpgradeMaxCount);

    /// <summary>
    /// 武器强化触发角色等级列表。
    /// </summary>
    public int[] WeaponUpgradeTriggerLevels => weaponUpgradeTriggerLevels;

    /// <summary>
    /// 武器专属强化词条池。
    /// </summary>
    public WeaponUpgradeData[] WeaponUpgradeEntries => weaponUpgradeEntries;

    /// <summary>
    /// 武器最高等级。
    /// </summary>
    public int MaxLevel => maxLevel;
}
