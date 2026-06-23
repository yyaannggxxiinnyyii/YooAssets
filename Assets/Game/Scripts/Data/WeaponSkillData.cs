using UnityEngine;

/// <summary>
/// 定义武器右键小技能和大技能的投射物弹幕配置。
/// </summary>
[CreateAssetMenu(fileName = "WeaponSkillData", menuName = "Game/Data/Weapon Skill Data")]
public sealed class WeaponSkillData : ScriptableObject
{
    [Header("小技能")]
    [Tooltip("释放小技能需要消耗的能量。")]
    [SerializeField] private float minorEnergyCost = 30f;
    [Tooltip("小技能一次发射的投射物数量。")]
    [SerializeField] private int minorProjectileCount = 5;
    [Tooltip("小技能投射物伤害倍率，基于最终攻击力计算。")]
    [SerializeField] private float minorDamageMultiplier = 1f;
    [Tooltip("小技能投射物向瞄准方向两侧扩散的角度。")]
    [SerializeField] private float minorSpreadAngle = 10f;
    [Tooltip("小技能投射物速度倍率。")]
    [SerializeField] private float minorProjectileSpeedMultiplier = 1f;
    [Tooltip("小技能穿透层数倍率。")]
    [SerializeField] private float minorPierceMultiplier = 1f;
    [Tooltip("小技能反弹次数倍率。")]
    [SerializeField] private float minorBounceMultiplier = 1f;
    [Tooltip("小技能释放后是否补满弹夹。")]
    [SerializeField] private bool refillMagazineAfterMinorSkill = true;

    [Header("大技能")]
    [Tooltip("长按右键蓄满所需时间。")]
    [SerializeField] private float chargeDuration = 1.2f;
    [Tooltip("蓄力期间玩家移动速度倍率。")]
    [SerializeField] private float chargeMoveSpeedMultiplier = 0.45f;
    [Tooltip("大技能持续发射弹幕的时间。")]
    [SerializeField] private float majorDuration = 3f;
    [Tooltip("大技能持续期间每轮发射的间隔。")]
    [SerializeField] private float majorFireInterval = 0.12f;
    [Tooltip("大技能每轮发射的投射物数量。")]
    [SerializeField] private int majorProjectileCount = 1;
    [Tooltip("大技能投射物伤害倍率，基于最终攻击力计算。")]
    [SerializeField] private float majorDamageMultiplier = 1.2f;
    [Tooltip("大技能投射物向瞄准方向两侧扩散的角度。")]
    [SerializeField] private float majorSpreadAngle = 25f;
    [Tooltip("大技能投射物速度倍率。")]
    [SerializeField] private float majorProjectileSpeedMultiplier = 1f;
    [Tooltip("大技能穿透覆盖值，小于 0 时沿用当前武器值。")]
    [SerializeField] private int majorPierceOverride = -1;
    [Tooltip("大技能反弹覆盖值，小于 0 时沿用当前武器值。")]
    [SerializeField] private int majorBounceOverride = -1;
    [Tooltip("大技能投射物持续时间覆盖值，小于等于 0 时沿用当前武器值。")]
    [SerializeField] private float majorLifeTimeOverride = -1f;
    [Tooltip("大技能后坐力倍率。")]
    [SerializeField] private float majorRecoilMultiplier = 1f;
    [Tooltip("大技能冲击力倍率。")]
    [SerializeField] private float majorImpactMultiplier = 1f;

    /// <summary>
    /// 小技能能量消耗。
    /// </summary>
    public float MinorEnergyCost => minorEnergyCost;

    /// <summary>
    /// 小技能发射弹丸数量。
    /// </summary>
    public int MinorProjectileCount => minorProjectileCount;

    /// <summary>
    /// 小技能伤害倍率。
    /// </summary>
    public float MinorDamageMultiplier => minorDamageMultiplier;

    /// <summary>
    /// 小技能散射角度。
    /// </summary>
    public float MinorSpreadAngle => minorSpreadAngle;

    /// <summary>
    /// 小技能弹速倍率。
    /// </summary>
    public float MinorProjectileSpeedMultiplier => minorProjectileSpeedMultiplier;

    /// <summary>
    /// 小技能穿透倍率。
    /// </summary>
    public float MinorPierceMultiplier => minorPierceMultiplier;

    /// <summary>
    /// 小技能反弹倍率。
    /// </summary>
    public float MinorBounceMultiplier => minorBounceMultiplier;

    /// <summary>
    /// 小技能结束后是否补满弹夹。
    /// </summary>
    public bool RefillMagazineAfterMinorSkill => refillMagazineAfterMinorSkill;

    /// <summary>
    /// 长按蓄满所需时间。
    /// </summary>
    public float ChargeDuration => chargeDuration;

    /// <summary>
    /// 蓄力期间移速倍率。
    /// </summary>
    public float ChargeMoveSpeedMultiplier => chargeMoveSpeedMultiplier;

    /// <summary>
    /// 大技能持续时间。
    /// </summary>
    public float MajorDuration => majorDuration;

    /// <summary>
    /// 大技能发射间隔。
    /// </summary>
    public float MajorFireInterval => majorFireInterval;

    /// <summary>
    /// 大技能每轮弹丸数量。
    /// </summary>
    public int MajorProjectileCount => majorProjectileCount;

    /// <summary>
    /// 大技能伤害倍率。
    /// </summary>
    public float MajorDamageMultiplier => majorDamageMultiplier;

    /// <summary>
    /// 大技能散射角度。
    /// </summary>
    public float MajorSpreadAngle => majorSpreadAngle;

    /// <summary>
    /// 大技能弹速倍率。
    /// </summary>
    public float MajorProjectileSpeedMultiplier => majorProjectileSpeedMultiplier;

    /// <summary>
    /// 大技能穿透覆盖值，小于 0 时沿用当前武器值。
    /// </summary>
    public int MajorPierceOverride => majorPierceOverride;

    /// <summary>
    /// 大技能反弹覆盖值，小于 0 时沿用当前武器值。
    /// </summary>
    public int MajorBounceOverride => majorBounceOverride;

    /// <summary>
    /// 大技能持续时间覆盖值，小于等于 0 时沿用当前武器值。
    /// </summary>
    public float MajorLifeTimeOverride => majorLifeTimeOverride;

    /// <summary>
    /// 大技能后坐力倍率。
    /// </summary>
    public float MajorRecoilMultiplier => majorRecoilMultiplier;

    /// <summary>
    /// 大技能冲击力倍率。
    /// </summary>
    public float MajorImpactMultiplier => majorImpactMultiplier;
}
