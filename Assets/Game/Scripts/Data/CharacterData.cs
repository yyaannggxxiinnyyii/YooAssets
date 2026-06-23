using UnityEngine;

/// <summary>
/// 定义玩家初始角色的静态配置数据。
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "Game/Data/Character Data")]
public sealed class CharacterData : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("角色唯一标识，用于存档或逻辑查找。")]
    [SerializeField] private string characterId = "default_character";
    [Tooltip("角色在 UI 中显示的名称。")]
    [SerializeField] private string displayName = "默认角色";
    [Tooltip("角色头像，当前 UI 可为空。")]
    [SerializeField] private Sprite portrait;
    [Tooltip("角色运行时预制体，未配置时会使用战斗控制器的默认临时对象。")]
    [SerializeField] private GamePlayerController playerPrefab;

    [Header("角色属性")]
    [Tooltip("角色初始最大生命值。")]
    [SerializeField] private int maxHealth = 10;
    [Tooltip("角色基础移动速度。")]
    [SerializeField] private float moveSpeed = 4.5f;
    [Tooltip("角色基础攻击力。")]
    [SerializeField] private int attackDamage = 12;
    [Tooltip("角色初始攻击力提升倍率，1 表示 100%。")]
    [SerializeField] private float attackDamageMultiplier = 1f;
    [Tooltip("角色经验获取倍率，1 表示 100%。")]
    [SerializeField] private float experienceMultiplier = 1f;
    [Tooltip("角色每次升级强化选择的默认免费刷新次数。")]
    [SerializeField] private int baseUpgradeRefreshCount = 1;
    [Tooltip("角色每次进入商店时的默认免费刷新次数。")]
    [SerializeField] private int baseShopRefreshCount = 1;

    [Header("翻滚属性")]
    [Tooltip("角色翻滚移动速度。")]
    [SerializeField] private float rollSpeed = 12f;
    [Tooltip("角色翻滚位移持续时间。")]
    [SerializeField] private float rollDuration = 0.22f;
    [Tooltip("角色两次翻滚之间的冷却时间。")]
    [SerializeField] private float rollCooldown = 0.8f;
    [Tooltip("角色可连续使用的翻滚充能次数。")]
    [SerializeField] private int maxRollCharges = 1;
    [Tooltip("角色闪避后获得无敌的持续时间。")]
    [SerializeField] private float rollInvincibleDuration = 0.2f;

    /// <summary>
    /// 角色唯一标识。
    /// </summary>
    public string CharacterId => characterId;

    /// <summary>
    /// 角色显示名称。
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// 角色头像。
    /// </summary>
    public Sprite Portrait => portrait;

    /// <summary>
    /// 角色运行时预制体。
    /// </summary>
    public GamePlayerController PlayerPrefab => playerPrefab;

    /// <summary>
    /// 初始最大生命值。
    /// </summary>
    public int MaxHealth => maxHealth;

    /// <summary>
    /// 初始移动速度。
    /// </summary>
    public float MoveSpeed => moveSpeed;

    /// <summary>
    /// 初始攻击伤害。
    /// </summary>
    public int AttackDamage => attackDamage;

    /// <summary>
    /// 初始攻击力提升倍率。
    /// </summary>
    public float AttackDamageMultiplier => attackDamageMultiplier;

    /// <summary>
    /// 初始经验倍率。
    /// </summary>
    public float ExperienceMultiplier => experienceMultiplier;

    /// <summary>
    /// 每次升级强化选择的默认免费刷新次数。
    /// </summary>
    public int BaseUpgradeRefreshCount => baseUpgradeRefreshCount;

    /// <summary>
    /// 每次进入商店时的默认免费刷新次数。
    /// </summary>
    public int BaseShopRefreshCount => baseShopRefreshCount;

    /// <summary>
    /// 翻滚移动速度。
    /// </summary>
    public float RollSpeed => rollSpeed;

    /// <summary>
    /// 翻滚位移持续时间。
    /// </summary>
    public float RollDuration => rollDuration;

    /// <summary>
    /// 翻滚冷却时间。
    /// </summary>
    public float RollCooldown => rollCooldown;

    /// <summary>
    /// 最大翻滚充能次数。
    /// </summary>
    public int MaxRollCharges => maxRollCharges;

    /// <summary>
    /// 闪避后无敌持续时间。
    /// </summary>
    public float RollInvincibleDuration => rollInvincibleDuration;
}
