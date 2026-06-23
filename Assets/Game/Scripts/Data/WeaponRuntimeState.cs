using UnityEngine;

/// <summary>
/// 保存武器在单局内的等级和冷却状态。
/// </summary>
[System.Serializable]
public sealed class WeaponRuntimeState
{
    [Header("武器")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private int level = 1;
    [SerializeField] private float cooldownTimer;

    /// <summary>
    /// 武器静态配置。
    /// </summary>
    public WeaponData WeaponData => weaponData;

    /// <summary>
    /// 当前武器等级。
    /// </summary>
    public int Level => level;

    /// <summary>
    /// 当前冷却计时。
    /// </summary>
    public float CooldownTimer => cooldownTimer;

    /// <summary>
    /// 按武器数据初始化运行时状态。
    /// </summary>
    /// <param name="data">武器静态配置。</param>
    public void Initialize(WeaponData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[WeaponRuntimeState] 武器数据为空，无法初始化。");
            return;
        }

        weaponData = data;
        level = 1;
        cooldownTimer = 0f;
    }

    /// <summary>
    /// 提升武器等级，不超过配置上限。
    /// </summary>
    public void LevelUp()
    {
        if (weaponData == null)
        {
            Debug.LogWarning("[WeaponRuntimeState] 武器数据为空，无法升级。");
            return;
        }

        level = Mathf.Min(level + 1, weaponData.MaxLevel);
    }

    /// <summary>
    /// 设置冷却计时。
    /// </summary>
    /// <param name="seconds">冷却秒数。</param>
    public void SetCooldown(float seconds)
    {
        cooldownTimer = Mathf.Max(0f, seconds);
    }

    /// <summary>
    /// 推进冷却计时。
    /// </summary>
    /// <param name="deltaSeconds">增量秒数。</param>
    public void TickCooldown(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
            return;

        cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaSeconds);
    }
}
