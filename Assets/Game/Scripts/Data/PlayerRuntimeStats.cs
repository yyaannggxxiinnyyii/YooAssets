using UnityEngine;

/// <summary>
/// 保存玩家单局内会变化的基础属性数值。
/// </summary>
[System.Serializable]
public sealed class PlayerRuntimeStats
{
    [Header("生命")]
    [Tooltip("运行时最大生命值。")]
    [SerializeField] private int maxHealth;
    [Tooltip("运行时当前生命值。")]
    [SerializeField] private int currentHealth;

    [Header("战斗")]
    [Tooltip("运行时移动速度。")]
    [SerializeField] private float moveSpeed;
    [Tooltip("运行时基础攻击力。")]
    [SerializeField] private int attackDamage;
    [Tooltip("运行时攻击力提升倍率。")]
    [SerializeField] private float attackDamageMultiplier = 1f;

    [Header("成长")]
    [Tooltip("当前等级。")]
    [SerializeField] private int level = 1;
    [Tooltip("当前经验。")]
    [SerializeField] private int experience;
    [Tooltip("当前金币。")]
    [SerializeField] private int gold;

    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int MaxHealth => maxHealth;

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public int CurrentHealth => currentHealth;

    /// <summary>
    /// 移动速度。
    /// </summary>
    public float MoveSpeed => moveSpeed;

    /// <summary>
    /// 攻击伤害。
    /// </summary>
    public int AttackDamage => attackDamage;

    /// <summary>
    /// 攻击力提升倍率。
    /// </summary>
    public float AttackDamageMultiplier => attackDamageMultiplier;

    /// <summary>
    /// 当前等级。
    /// </summary>
    public int Level => level;

    /// <summary>
    /// 当前经验。
    /// </summary>
    public int Experience => experience;

    /// <summary>
    /// 当前金币。
    /// </summary>
    public int Gold => gold;

    /// <summary>
    /// 按角色数据初始化运行时属性。
    /// </summary>
    /// <param name="characterData">角色静态配置。</param>
    public void Initialize(CharacterData characterData)
    {
        if (characterData == null)
        {
            Debug.LogWarning("[PlayerRuntimeStats] 角色数据为空，无法初始化运行时属性。");
            return;
        }

        maxHealth = characterData.MaxHealth;
        currentHealth = maxHealth;
        moveSpeed = characterData.MoveSpeed;
        attackDamage = characterData.AttackDamage;
        attackDamageMultiplier = Mathf.Max(0.01f, characterData.AttackDamageMultiplier);
        level = 1;
        experience = 0;
        gold = 0;
    }

    /// <summary>
    /// 设置当前生命值并限制在合法范围内。
    /// </summary>
    /// <param name="value">目标生命值。</param>
    public void SetCurrentHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
    }

    /// <summary>
    /// 增加最大生命并按相同数值回复生命。
    /// </summary>
    /// <param name="value">增加值。</param>
    public void AddMaxHealth(int value)
    {
        if (value <= 0)
            return;

        maxHealth += value;
        currentHealth = Mathf.Clamp(currentHealth + value, 0, maxHealth);
    }

    /// <summary>
    /// 设置最大生命值，并将当前生命限制在新的上限内。
    /// </summary>
    /// <param name="value">新的最大生命值。</param>
    public void SetMaxHealth(int value)
    {
        maxHealth = Mathf.Max(1, value);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    /// <summary>
    /// 增加攻击伤害。
    /// </summary>
    /// <param name="value">增加值。</param>
    public void AddAttackDamage(int value)
    {
        if (value <= 0)
            return;

        attackDamage += value;
    }

    /// <summary>
    /// 设置成长数据。
    /// </summary>
    /// <param name="newLevel">当前等级。</param>
    /// <param name="newExperience">当前经验。</param>
    /// <param name="newGold">当前金币。</param>
    public void SetProgression(int newLevel, int newExperience, int newGold)
    {
        level = Mathf.Max(1, newLevel);
        experience = Mathf.Max(0, newExperience);
        gold = Mathf.Max(0, newGold);
    }
}
