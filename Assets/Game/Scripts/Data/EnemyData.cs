using UnityEngine;

/// <summary>
/// 定义敌人的静态配置数据，包含通用基础属性、资源引用和兜底行为类型。
/// </summary>
[CreateAssetMenu(fileName = "EnemyData", menuName = "Game/Data/Enemy Data")]
public sealed class EnemyData : ScriptableObject
{
    /// <summary>
    /// 敌人运行时使用的兜底行为类型。
    /// </summary>
    public enum EnemyBehaviorType
    {
        Fox,
        Wolf,
        Snake,
        Bear
    }

    [Header("基础信息")]
    [Tooltip("敌人唯一标识，用于存档、日志或逻辑查找。")]
    [SerializeField] private string enemyId = "default_enemy";
    [Tooltip("敌人在 UI、调试信息中显示的名称。")]
    [SerializeField] private string displayName = "默认敌人";
    [Tooltip("敌人运行时显示的 Sprite；为空时保留对象池默认贴图。")]
    [SerializeField] private Sprite sprite;
    [Tooltip("敌人运行时使用的预制体；建议在预制体上挂对应的敌人行为脚本以便调整能力参数。")]
    [SerializeField] private GameObject prefab;
    [Tooltip("敌人动画状态机；为空时不启用动画状态机。")]
    [SerializeField] private RuntimeAnimatorController animatorController;

    [Header("战斗属性")]
    [Tooltip("预制体没有挂敌人行为脚本时使用的兜底行为类型。")]
    [SerializeField] private EnemyBehaviorType behaviorType = EnemyBehaviorType.Fox;
    [Tooltip("敌人最大生命值。")]
    [SerializeField] private int maxHealth = 24;
    [Tooltip("敌人基础移动速度。")]
    [SerializeField] private float moveSpeed = 1.6f;
    [Tooltip("敌人与玩家接触时造成的单次伤害。")]
    [SerializeField] private int contactDamage = 8;

    [Header("奖励")]
    [Tooltip("敌人死亡时掉落的经验值。")]
    [SerializeField] private int dropExperience = 1;
    [Tooltip("敌人死亡时掉落的金币数量。")]
    [SerializeField] private int dropGold = 1;
    [Tooltip("击杀敌人获得的分数。")]
    [SerializeField] private int score = 10;

    /// <summary>
    /// 敌人唯一标识。
    /// </summary>
    public string EnemyId => enemyId;

    /// <summary>
    /// 敌人显示名称。
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// 敌人 Sprite。
    /// </summary>
    public Sprite Sprite => sprite;

    /// <summary>
    /// 敌人预制体。
    /// </summary>
    public GameObject Prefab => prefab;

    /// <summary>
    /// 敌人动画状态机。
    /// </summary>
    public RuntimeAnimatorController AnimatorController => animatorController;

    /// <summary>
    /// 敌人运行时兜底行为类型。
    /// </summary>
    public EnemyBehaviorType BehaviorType => behaviorType;

    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int MaxHealth => maxHealth;

    /// <summary>
    /// 移动速度。
    /// </summary>
    public float MoveSpeed => moveSpeed;

    /// <summary>
    /// 接触伤害。
    /// </summary>
    public int ContactDamage => contactDamage;

    /// <summary>
    /// 掉落经验。
    /// </summary>
    public int DropExperience => dropExperience;

    /// <summary>
    /// 掉落金币。
    /// </summary>
    public int DropGold => dropGold;

    /// <summary>
    /// 击杀分数。
    /// </summary>
    public int Score => score;
}
