using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// 作为玩家运行时门面，协调移动输入、生命、经验、金币和武器运行时属性。
/// </summary>
[RequireComponent(typeof(GamePlayerRollController))]
[RequireComponent(typeof(GamePlayerSceneCollisionController))]
[RequireComponent(typeof(GamePlayerHurtFeedbackController))]
public sealed class GamePlayerController : MonoBehaviour
{
    [Header("基础属性")]
    [Tooltip("玩家最大生命值。")]
    [SerializeField] private int maxHealth = 100;
    [Tooltip("玩家普通移动速度。")]
    [SerializeField] private float moveSpeed = 4.5f;
    [Tooltip("玩家基础攻击力。")]
    [SerializeField] private int attackDamage = 12;
    [Tooltip("玩家经验获取倍率。")]
    [SerializeField] private float experienceMultiplier = 1f;
    [Header("武器运行时加成")]
    [Tooltip("本局内武器攻击伤害的固定加成。")]
    [SerializeField] private int attackDamageBonus;
    [Tooltip("本局内武器攻击伤害的倍率。")]
    [SerializeField] private float attackDamageMultiplier = 1f;
    [Tooltip("本局内武器攻击间隔的减少值。")]
    [SerializeField] private float attackSpeedMultiplier = 1f;
    [Tooltip("本局内投射物飞行速度加成。")]
    [SerializeField] private float projectileSpeedMultiplier = 1f;
    [Tooltip("本局内投射物体积倍率。")]
    [SerializeField] private float projectileScaleMultiplier = 1f;
    [Tooltip("本局内武器暴击率。")]
    [SerializeField] private float criticalRate = 0.05f;
    [Tooltip("本局内武器暴击伤害倍率。")]
    [SerializeField] private float criticalDamage = 1.8f;
    [Tooltip("本局内投射物穿透层数。")]
    [SerializeField] private int projectilePierce;
    [Tooltip("本局内投射物反弹次数。")]
    [SerializeField] private int projectileBounce;
    [Tooltip("本局内单次攻击发射的弹道数量。")]
    [SerializeField] private int projectileCount = 1;
    [Tooltip("本局内投射物存在时间加成。")]
    [SerializeField] private float projectileLifeTimeMultiplier = 1f;
    [Tooltip("本局内弹夹容量加成。")]
    [SerializeField] private float magazineCapacityMultiplier = 1f;
    [Tooltip("本局内换弹时间加成。")]
    [SerializeField] private float reloadSpeedMultiplier = 1f;
    [Tooltip("本局内最大能量加成。")]
    [SerializeField] private float maxEnergyBonus;
    [Tooltip("本局内每秒能量恢复加成。")]
    [SerializeField] private float energyRecoveryBonus;
    [Tooltip("本局内击杀获得能量加成。")]
    [SerializeField] private float energyPerKillBonus;
    [Tooltip("本局内射击准确角度加成。")]
    [SerializeField] private float accuracyMultiplier = 1f;
    [Tooltip("本局内武器后坐力距离加成。")]
    [SerializeField] private float recoilControlMultiplier = 1f;
    [Tooltip("本局内命中击退距离加成。")]
    [SerializeField] private float impactMultiplier = 1f;

    private WeaponData _currentWeaponData;
    private Vector2 _moveInput;
    private Vector2 _lastMoveDirection = Vector2.right;
    private int _baseUpgradeRefreshCount;
    private int _upgradeRefreshCount;
    private int _baseShopRefreshCount;
    private int _shopRefreshCount;
    private bool _controlEnabled = true;
    private float _temporaryMoveSpeedMultiplier = 1f;
    private GamePlayerRollController _rollController;
    private GamePlayerSceneCollisionController _sceneCollisionController;
    private GamePlayerHurtFeedbackController _hurtFeedbackController;
    private readonly List<SpecialHealthSegment> _specialHealthSegments = new List<SpecialHealthSegment>();
    private readonly List<SpecialHealthSegment> _specialHealthSnapshot = new List<SpecialHealthSegment>();

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public int CurrentHealth { get; private set; }

    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int MaxHealth => maxHealth;

    /// <summary>
    /// 当前等级。
    /// </summary>
    public int Level { get; private set; } = 1;

    /// <summary>
    /// 当前经验。
    /// </summary>
    public int Experience { get; private set; }

    /// <summary>
    /// 待处理的升级强化选择次数。
    /// </summary>
    public int PendingUpgradeChoices { get; private set; }

    /// <summary>
    /// 待处理的武器强化选择次数。
    /// </summary>
    public int PendingWeaponUpgradeChoices { get; private set; }

    /// <summary>
    /// 当前金币。
    /// </summary>
    public int Gold { get; private set; }

    /// <summary>
    /// 攻击伤害。
    /// </summary>
    public int AttackDamage => attackDamage;

    /// <summary>
    /// 本局内通过强化获得的武器攻击伤害加成。
    /// </summary>
    public int AttackDamageBonus => attackDamageBonus;

    /// <summary>
    /// 兼容旧调用的攻击伤害加成。
    /// </summary>
    public int WeaponAttackDamageBonus => attackDamageBonus;

    /// <summary>
    /// 攻击力提升倍率。
    /// </summary>
    public float AttackDamageMultiplier => attackDamageMultiplier;

    /// <summary>
    /// 移动速度。
    /// </summary>
    public float MoveSpeed => moveSpeed;

    /// <summary>
    /// 当前经验倍率。
    /// </summary>
    public float ExperienceMultiplier => experienceMultiplier;

    /// <summary>
    /// 当前剩余升级强化刷新次数。
    /// </summary>
    public int UpgradeRefreshCount => _upgradeRefreshCount;

    /// <summary>
    /// 当前剩余商店刷新次数。
    /// </summary>
    public int ShopRefreshCount => _shopRefreshCount;

    /// <summary>
    /// 射击间隔减少值。
    /// </summary>
    public float AttackSpeedMultiplier => Mathf.Max(0.01f, attackSpeedMultiplier);

    /// <summary>
    /// 子弹速度加成。
    /// </summary>
    public float ProjectileSpeedMultiplier => Mathf.Max(0.01f, projectileSpeedMultiplier);

    /// <summary>
    /// 投射物体积倍率。
    /// </summary>
    public float ProjectileScaleMultiplier => Mathf.Max(0.01f, projectileScaleMultiplier);

    /// <summary>
    /// 暴击率。
    /// </summary>
    public float CriticalRate => criticalRate;

    /// <summary>
    /// 暴击伤害倍率。
    /// </summary>
    public float CriticalDamage => criticalDamage;

    /// <summary>
    /// 子弹穿透层数。
    /// </summary>
    public int ProjectilePierce => projectilePierce;

    /// <summary>
    /// 子弹反弹层数。
    /// </summary>
    public int ProjectileBounce => projectileBounce;

    /// <summary>
    /// 单次攻击发射的弹道数量。
    /// </summary>
    public int ProjectileCount => projectileCount;

    /// <summary>
    /// 子弹存在时间加成。
    /// </summary>
    public float ProjectileLifeTimeMultiplier => Mathf.Max(0.01f, projectileLifeTimeMultiplier);

    /// <summary>
    /// 弹夹容量加成。
    /// </summary>
    public float MagazineCapacityMultiplier => Mathf.Max(0.01f, magazineCapacityMultiplier);

    /// <summary>
    /// 换弹时间加成。
    /// </summary>
    public float ReloadSpeedMultiplier => Mathf.Max(0.01f, reloadSpeedMultiplier);

    /// <summary>
    /// 最大能量加成。
    /// </summary>
    public float MaxEnergyBonus => maxEnergyBonus;

    /// <summary>
    /// 每秒能量恢复加成。
    /// </summary>
    public float EnergyRecoveryBonus => energyRecoveryBonus;

    /// <summary>
    /// 击杀能量奖励加成。
    /// </summary>
    public float EnergyPerKillBonus => energyPerKillBonus;

    /// <summary>
    /// 射击准度倍率，实际散射计算会将超过 1 的部分视为散射抵消比例。
    /// </summary>
    public float AccuracyMultiplier => Mathf.Max(0.01f, accuracyMultiplier);

    /// <summary>
    /// 后坐力距离加成。
    /// </summary>
    public float RecoilControlMultiplier => Mathf.Max(0.01f, recoilControlMultiplier);

    /// <summary>
    /// 命中击退距离加成。
    /// </summary>
    public float ImpactMultiplier => Mathf.Max(0.01f, impactMultiplier);

    /// <summary>
    /// 是否正在翻滚。
    /// </summary>
    public bool IsRolling => _rollController != null && _rollController.IsRolling;

    /// <summary>
    /// 闪避冷却是否正在恢复。
    /// </summary>
    public bool IsRollCoolingDown => _rollController != null && _rollController.IsRollCoolingDown;

    /// <summary>
    /// 当前可使用的翻滚充能次数。
    /// </summary>
    public int CurrentRollCharges => _rollController != null ? _rollController.CurrentRollCharges : 0;

    /// <summary>
    /// 当前最大翻滚充能次数。
    /// </summary>
    public int MaxRollCharges => _rollController != null ? _rollController.MaxRollCharges : 0;

    /// <summary>
    /// 当前闪避冷却回满进度，0 表示刚进入冷却，1 表示可以再次闪避。
    /// </summary>
    public float RollCooldownProgress => _rollController != null ? _rollController.RollCooldownProgress : 1f;

    /// <summary>
    /// 是否已经死亡。
    /// </summary>
    public bool IsDead => CurrentHealth <= 0;

    /// <summary>
    /// 当前是否处于伤害免疫状态。
    /// </summary>
    public bool IsDamageInvincible => (_rollController != null && _rollController.IsInvincible) || (_hurtFeedbackController != null && _hurtFeedbackController.IsInvincible);

    /// <summary>
    /// 玩家实际恢复红血时触发，参数为玩家实例和实际恢复点数。
    /// </summary>
    public event Action<GamePlayerController, int> Healed;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        CacheComponentReferences();
    }

    private void Update()
    {
        if (_rollController != null)
            _rollController.TickInvincible(Time.deltaTime);
        if (_hurtFeedbackController != null)
            _hurtFeedbackController.Tick(Time.deltaTime);

        if (!_controlEnabled)
        {
            _moveInput = Vector2.zero;
            return;
        }

        if (_rollController != null)
            _rollController.TickRecharge(Time.deltaTime);

        _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (_moveInput.sqrMagnitude > 1f)
            _moveInput.Normalize();

        if (_moveInput.sqrMagnitude > 0.01f)
            _lastMoveDirection = _moveInput.normalized;

        if (Input.GetKeyDown(KeyCode.Space))
            TryStartRoll();

        TickMovement(Time.deltaTime);
    }

    /// <summary>
    /// 缓存玩家子功能组件引用。
    /// </summary>
    private void CacheComponentReferences()
    {
        _rollController = GetComponent<GamePlayerRollController>();
        _sceneCollisionController = GetComponent<GamePlayerSceneCollisionController>();
        _hurtFeedbackController = GetComponent<GamePlayerHurtFeedbackController>();

        if (_rollController == null)
            Debug.LogError($"[Player] 玩家 {name} 缺少 GamePlayerRollController。", this);
        if (_sceneCollisionController == null)
            Debug.LogError($"[Player] 玩家 {name} 缺少 GamePlayerSceneCollisionController。", this);
        if (_hurtFeedbackController == null)
            Debug.LogError($"[Player] 玩家 {name} 缺少 GamePlayerHurtFeedbackController。", this);
    }

    /// <summary>
    /// 按当前输入和翻滚状态推进玩家位移，使用渲染帧时间避免高速移动时出现固定帧率跳动。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickMovement(float deltaTime)
    {
        Vector2 movement = _moveInput;
        float currentMoveSpeed = moveSpeed * Mathf.Max(0f, _temporaryMoveSpeedMultiplier);
        if (_rollController != null && _rollController.IsRolling)
        {
            _rollController.TickRollMovement(deltaTime);
            movement = _rollController.RollDirection;
            currentMoveSpeed = _rollController.RollSpeed;
        }

        Vector3 nextPosition = transform.position + new Vector3(movement.x, movement.y, 0f) * currentMoveSpeed * deltaTime;
        if (_sceneCollisionController != null)
            nextPosition = _sceneCollisionController.ResolvePosition(transform.position, nextPosition);

        transform.position = nextPosition;
    }

    /// <summary>
    /// 使用角色数据初始化玩家基础属性。
    /// </summary>
    /// <param name="characterData">角色配置数据。</param>
    public void Initialize(CharacterData characterData)
    {
        if (characterData == null)
            return;

        maxHealth = Mathf.Max(1, characterData.MaxHealth);
        CurrentHealth = maxHealth;
        ClearSpecialHealth();
        moveSpeed = Mathf.Max(0.1f, characterData.MoveSpeed);
        attackDamage = Mathf.Max(1, characterData.AttackDamage);
        attackDamageMultiplier = Mathf.Max(0.01f, characterData.AttackDamageMultiplier);
        experienceMultiplier = Mathf.Max(0.01f, characterData.ExperienceMultiplier);
        if (_rollController != null)
            _rollController.Initialize(characterData);
        if (_hurtFeedbackController != null)
            _hurtFeedbackController.ResetRuntime();
        _baseUpgradeRefreshCount = Mathf.Max(0, characterData.BaseUpgradeRefreshCount);
        ResetUpgradeRefreshCount();
        _baseShopRefreshCount = Mathf.Max(0, characterData.BaseShopRefreshCount);
        ResetShopRefreshCount();
        attackDamageBonus = 0;
        attackSpeedMultiplier = 1f;
        projectileSpeedMultiplier = 1f;
        projectileScaleMultiplier = 1f;
        criticalRate = 0f;
        criticalDamage = 0f;
        projectilePierce = 0;
        projectileBounce = 0;
        projectileCount = 0;
        projectileLifeTimeMultiplier = 1f;
        magazineCapacityMultiplier = 1f;
        reloadSpeedMultiplier = 1f;
        maxEnergyBonus = 0f;
        energyRecoveryBonus = 0f;
        energyPerKillBonus = 0f;
        accuracyMultiplier = 1f;
        recoilControlMultiplier = 1f;
        impactMultiplier = 1f;
    }

    /// <summary>
    /// 对玩家造成伤害。
    /// </summary>
    /// <param name="damage">伤害值。</param>
    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDead || IsDamageInvincible)
            return;

        int remainingDamage = ConsumeSpecialHealth(damage);
        if (remainingDamage <= 0)
        {
            PlayHurtFeedback();
            return;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - remainingDamage);
        PlayHurtFeedback();
    }

    /// <summary>
    /// 播放玩家受伤反馈并启动受伤无敌帧。
    /// </summary>
    private void PlayHurtFeedback()
    {
        if (_hurtFeedbackController != null)
            _hurtFeedbackController.Play();
    }

    /// <summary>
    /// 回复红血生命值，不会超过当前最大红血上限。
    /// </summary>
    /// <param name="amount">回复的红血点数。</param>
    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        int previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0, maxHealth);
        int actualHeal = CurrentHealth - previousHealth;
        if (actualHeal > 0)
            Healed?.Invoke(this, actualHeal);
    }

    /// <summary>
    /// 判断普通回血道具当前是否允许被消耗；满红血时不会消耗普通治疗。
    /// </summary>
    /// <param name="amount">普通治疗提供的红血回复点数。</param>
    /// <returns>当前可以消耗普通治疗时返回 true。</returns>
    public bool CanConsumeNormalHeal(int amount)
    {
        return amount > 0 && !IsDead && CurrentHealth < maxHealth;
    }

    /// <summary>
    /// 尝试消耗普通回血道具并回复红血；满红血时返回 false 且不消耗。
    /// </summary>
    /// <param name="amount">普通治疗提供的红血回复点数。</param>
    /// <returns>治疗被实际消耗时返回 true。</returns>
    public bool TryConsumeNormalHeal(int amount)
    {
        if (!CanConsumeNormalHeal(amount))
            return false;

        Heal(amount);
        return true;
    }

    /// <summary>
    /// 增加指定类型的临时特殊血量。
    /// </summary>
    /// <param name="type">特殊血类型。</param>
    /// <param name="points">增加的血量点数。</param>
    public void AddSpecialHealth(SpecialHealthType type, int points)
    {
        if (points <= 0)
            return;

        if (_specialHealthSegments.Count > 0)
        {
            int lastIndex = _specialHealthSegments.Count - 1;
            SpecialHealthSegment lastSegment = _specialHealthSegments[lastIndex];
            if (lastSegment.Type == type)
            {
                _specialHealthSegments[lastIndex] = lastSegment.WithPoints(lastSegment.Points + points);
                return;
            }
        }

        _specialHealthSegments.Add(new SpecialHealthSegment(type, points));
    }

    /// <summary>
    /// 移除指定类型的临时特殊血量。
    /// </summary>
    /// <param name="type">特殊血类型。</param>
    /// <param name="points">需要移除的血量点数。</param>
    /// <returns>实际移除的血量点数。</returns>
    public int RemoveSpecialHealth(SpecialHealthType type, int points)
    {
        if (points <= 0)
            return 0;

        int remainingPoints = points;
        for (int i = _specialHealthSegments.Count - 1; i >= 0 && remainingPoints > 0; i--)
        {
            SpecialHealthSegment segment = _specialHealthSegments[i];
            if (segment.Type != type)
                continue;

            int consumedPoints = Mathf.Min(segment.Points, remainingPoints);
            remainingPoints -= consumedPoints;
            int leftPoints = segment.Points - consumedPoints;
            if (leftPoints > 0)
                _specialHealthSegments[i] = segment.WithPoints(leftPoints);
            else
                _specialHealthSegments.RemoveAt(i);
        }

        return points - remainingPoints;
    }

    /// <summary>
    /// 清空所有临时特殊血量。
    /// </summary>
    public void ClearSpecialHealth()
    {
        _specialHealthSegments.Clear();
    }

    /// <summary>
    /// 获取当前临时特殊血量段的只读快照。
    /// </summary>
    /// <returns>临时特殊血量段列表。</returns>
    public IReadOnlyList<SpecialHealthSegment> GetSpecialHealthSegments()
    {
        _specialHealthSnapshot.Clear();
        _specialHealthSnapshot.AddRange(_specialHealthSegments);
        return _specialHealthSnapshot;
    }

    /// <summary>
    /// 增加经验并累计升级强化选择次数。
    /// </summary>
    /// <param name="value">经验值。</param>
    public void AddExperience(int value)
    {
        if (value <= 0)
            return;

        int actualValue = Mathf.Max(1, Mathf.RoundToInt(value * Mathf.Max(0.01f, experienceMultiplier)));
        Experience += actualValue;
        while (Experience >= GetRequiredExperience())
        {
            Experience -= GetRequiredExperience();
            Level++;
            PendingUpgradeChoices++;
            if (_currentWeaponData != null && IsWeaponUpgradeLevel(Level, _currentWeaponData))
                PendingWeaponUpgradeChoices++;
        }
    }

    /// <summary>
    /// 消耗一次待处理的强化选择次数。
    /// </summary>
    /// <returns>成功消耗时返回 true。</returns>
    public bool TryConsumeUpgradeChoice()
    {
        if (PendingUpgradeChoices <= 0)
            return false;

        PendingUpgradeChoices--;
        ResetUpgradeRefreshCount();
        return true;
    }

    /// <summary>
    /// 消耗一次待处理的武器强化选择次数。
    /// </summary>
    /// <returns>成功消耗时返回 true。</returns>
    public bool TryConsumeWeaponUpgradeChoice()
    {
        if (PendingWeaponUpgradeChoices <= 0)
            return false;

        PendingWeaponUpgradeChoices--;
        return true;
    }

    /// <summary>
    /// 增加金币。
    /// </summary>
    /// <param name="value">金币数量。</param>
    public void AddGold(int value)
    {
        if (value <= 0)
            return;

        Gold += value;
    }

    /// <summary>
    /// 尝试消耗金币。
    /// </summary>
    /// <param name="cost">消耗数量。</param>
    /// <returns>金币足够并扣除成功时返回 true。</returns>
    public bool TrySpendGold(int cost)
    {
        if (cost <= 0)
            return true;

        if (Gold < cost)
            return false;

        Gold -= cost;
        return true;
    }

    /// <summary>
    /// 提升最大生命并回复同等生命。
    /// </summary>
    /// <param name="value">提升值。</param>
    public void AddMaxHealth(int value)
    {
        if (value == 0)
            return;

        int previousMaxHealth = maxHealth;
        maxHealth = Mathf.Max(1, maxHealth + value);
        int healthDelta = maxHealth - previousMaxHealth;
        CurrentHealth = Mathf.Clamp(CurrentHealth + Mathf.Max(0, healthDelta), 0, maxHealth);
    }

    /// <summary>
    /// 设置最大生命值，并将当前生命限制在新的上限内。
    /// </summary>
    /// <param name="value">新的最大生命值。</param>
    public void SetMaxHealth(int value)
    {
        maxHealth = Mathf.Max(1, value);
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, maxHealth);
    }

    /// <summary>
    /// 按特殊血优先规则消耗伤害，并返回剩余需要扣红血的伤害。
    /// </summary>
    /// <param name="damage">原始伤害值。</param>
    /// <returns>剩余伤害值。</returns>
    private int ConsumeSpecialHealth(int damage)
    {
        int remainingDamage = Mathf.Max(0, damage);
        for (int i = _specialHealthSegments.Count - 1; i >= 0 && remainingDamage > 0; i--)
        {
            SpecialHealthSegment segment = _specialHealthSegments[i];
            int consumedPoints = Mathf.Min(segment.Points, remainingDamage);
            remainingDamage -= consumedPoints;

            int leftPoints = segment.Points - consumedPoints;
            if (leftPoints > 0)
                _specialHealthSegments[i] = segment.WithPoints(leftPoints);
            else
                _specialHealthSegments.RemoveAt(i);
        }

        return remainingDamage;
    }

    /// <summary>
    /// 提升武器攻击伤害加成。
    /// </summary>
    /// <param name="value">提升值。</param>
    public void AddWeaponAttackDamage(int value)
    {
        if (value == 0)
            return;

        attackDamageBonus = Mathf.Max(-attackDamage + 1, attackDamageBonus + value);
    }

    /// <summary>
    /// 提升攻击力倍率。
    /// </summary>
    /// <param name="value">倍率增量，例如 0.05 表示提升 5%。</param>
    public void AddAttackDamageMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        attackDamageMultiplier = Mathf.Max(0.01f, attackDamageMultiplier + value);
    }

    /// <summary>
    /// 提升移动速度。
    /// </summary>
    /// <param name="value">提升值。</param>
    public void AddMoveSpeed(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        moveSpeed = Mathf.Max(0.1f, moveSpeed + value);
    }

    /// <summary>
    /// 按倍率调整移动速度。
    /// </summary>
    /// <param name="value">倍率增量，0.2 表示提升 20%。</param>
    public void AddMoveSpeedMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        moveSpeed = Mathf.Max(0.1f, moveSpeed * Mathf.Max(0.01f, 1f + value));
    }

    /// <summary>
    /// 按倍率调整翻滚距离，通过提升翻滚速度保持翻滚持续时间不变。
    /// </summary>
    /// <param name="value">倍率增量，0.3 表示提升 30%。</param>
    public void AddRollDistanceMultiplier(float value)
    {
        if (_rollController != null)
            _rollController.AddRollDistanceMultiplier(value);
    }

    /// <summary>
    /// 按比例降低翻滚冷却时间。
    /// </summary>
    /// <param name="value">降低比例，0.2 表示冷却减少 20%。</param>
    public void ReduceRollCooldownRate(float value)
    {
        if (_rollController != null)
            _rollController.ReduceRollCooldownRate(value);
    }

    /// <summary>
    /// 增加最大翻滚充能次数，并同步补充相同数量的当前可用次数。
    /// </summary>
    /// <param name="value">增加的翻滚次数。</param>
    public void AddRollChargeCount(int value)
    {
        if (_rollController != null)
            _rollController.AddRollChargeCount(value);
    }

    /// <summary>
    /// 增加翻滚无敌持续时间。
    /// </summary>
    /// <param name="value">增加的无敌秒数。</param>
    public void AddRollInvincibleDuration(float value)
    {
        if (_rollController != null)
            _rollController.AddRollInvincibleDuration(value);
    }

    /// <summary>
    /// 应用斯安威斯坦残影表现配置。
    /// </summary>
    public void ApplySandevistanAfterimage()
    {
        if (_rollController != null)
            _rollController.ApplySandevistanAfterimage();
    }

    /// <summary>
    /// 降低武器射击间隔。
    /// </summary>
    /// <param name="value">降低值。</param>
    public void AddAttackSpeedMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        attackSpeedMultiplier = Mathf.Max(0.01f, attackSpeedMultiplier + value);
    }

    /// <summary>
    /// 兼容旧攻击间隔强化入口，按当前倍率模型换算为射速倍率。
    /// </summary>
    /// <param name="value">旧攻击间隔减少值。</param>
    public void ReduceAttackInterval(float value)
    {
        if (Mathf.Approximately(value, 0f) || _currentWeaponData == null)
            return;

        float baseInterval = Mathf.Max(0.01f, _currentWeaponData.AttackInterval);
        float nextInterval = Mathf.Max(0.01f, baseInterval / attackSpeedMultiplier - value);
        attackSpeedMultiplier = Mathf.Max(0.01f, baseInterval / nextInterval);
    }

    /// <summary>
    /// 提升子弹速度。
    /// </summary>
    /// <param name="value">提升值。</param>
    public void AddProjectileSpeedMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        projectileSpeedMultiplier = Mathf.Max(0.01f, projectileSpeedMultiplier + value);
    }

    /// <summary>
    /// 兼容旧弹速固定值入口，按当前武器基础弹速换算为倍率。
    /// </summary>
    /// <param name="value">旧弹速增加值。</param>
    public void AddProjectileSpeed(float value)
    {
        if (Mathf.Approximately(value, 0f) || _currentWeaponData == null)
            return;

        float baseSpeed = Mathf.Max(0.01f, _currentWeaponData.ProjectileSpeed);
        projectileSpeedMultiplier = Mathf.Max(0.01f, projectileSpeedMultiplier + value / baseSpeed);
    }

    /// <summary>
    /// 调整投射物存在时间加成。
    /// </summary>
    /// <param name="value">存在时间加成值。</param>
    public void AddProjectileLifeTimeMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        projectileLifeTimeMultiplier = Mathf.Max(0.01f, projectileLifeTimeMultiplier + value);
    }

    /// <summary>
    /// 兼容旧存在时间固定值入口，按当前武器基础存在时间换算为倍率。
    /// </summary>
    /// <param name="value">旧存在时间增加值。</param>
    public void AddProjectileLifeTime(float value)
    {
        if (Mathf.Approximately(value, 0f) || _currentWeaponData == null)
            return;

        float baseLifeTime = Mathf.Max(0.01f, _currentWeaponData.ProjectileLifeTime);
        projectileLifeTimeMultiplier = Mathf.Max(0.01f, projectileLifeTimeMultiplier + value / baseLifeTime);
    }

    /// <summary>
    /// 提升经验倍率。
    /// </summary>
    /// <param name="value">提升值。</param>
    public void AddExperienceMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        experienceMultiplier = Mathf.Max(0.01f, experienceMultiplier + value);
    }

    /// <summary>
    /// 增加强化刷新次数。
    /// </summary>
    /// <param name="value">增加次数。</param>
    public void AddUpgradeRefreshCount(int value)
    {
        if (value <= 0)
            return;

        _baseUpgradeRefreshCount += value;
        _upgradeRefreshCount += value;
    }

    /// <summary>
    /// 尝试消耗一次强化刷新次数。
    /// </summary>
    /// <returns>成功消耗时返回 true。</returns>
    public bool TryConsumeUpgradeRefresh()
    {
        if (_upgradeRefreshCount <= 0)
            return false;

        _upgradeRefreshCount--;
        return true;
    }

    /// <summary>
    /// 增加每次进入商店的基础刷新次数，并同步增加当前剩余刷新次数。
    /// </summary>
    /// <param name="value">增加次数。</param>
    public void AddShopRefreshCount(int value)
    {
        if (value <= 0)
            return;

        _baseShopRefreshCount += value;
        _shopRefreshCount += value;
    }

    /// <summary>
    /// 尝试消耗一次商店刷新次数。
    /// </summary>
    /// <returns>成功消耗时返回 true。</returns>
    public bool TryConsumeShopRefresh()
    {
        if (_shopRefreshCount <= 0)
            return false;

        _shopRefreshCount--;
        return true;
    }

    /// <summary>
    /// 重置当前商店的剩余刷新次数。
    /// </summary>
    public void ResetShopRefreshCount()
    {
        _shopRefreshCount = _baseShopRefreshCount;
    }

    /// <summary>
    /// 重置当前升级强化选择的剩余刷新次数。
    /// </summary>
    private void ResetUpgradeRefreshCount()
    {
        _upgradeRefreshCount = _baseUpgradeRefreshCount;
    }

    /// <summary>
    /// 提升暴击率。
    /// </summary>
    /// <param name="value">提升值。</param>
    public void AddCriticalRate(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        criticalRate = Mathf.Clamp01(criticalRate + value);
    }

    /// <summary>
    /// 提升暴击伤害倍率。
    /// </summary>
    /// <param name="value">提升值。</param>
    public void AddCriticalDamage(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        criticalDamage = Mathf.Max(1f, criticalDamage + value);
    }

    /// <summary>
    /// 提升子弹穿透层数。
    /// </summary>
    /// <param name="value">提升值。</param>
    public void AddProjectilePierce(int value)
    {
        if (value == 0)
            return;

        projectilePierce = Mathf.Max(0, projectilePierce + value);
    }

    /// <summary>
    /// 提升子弹反弹层数。
    /// </summary>
    /// <param name="value">提升值。</param>
    public void AddProjectileBounce(int value)
    {
        if (value == 0)
            return;

        projectileBounce = Mathf.Max(0, projectileBounce + value);
    }

    /// <summary>
    /// 提升单次攻击弹道数量。
    /// </summary>
    /// <param name="value">提升值。</param>
    public void AddProjectileCount(int value)
    {
        if (value == 0)
            return;

        projectileCount = Mathf.Max(1, projectileCount + value);
    }

    /// <summary>
    /// 调整弹夹容量加成。
    /// </summary>
    /// <param name="value">弹夹容量加成值。</param>
    public void AddMagazineCapacityMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        magazineCapacityMultiplier = Mathf.Max(0.01f, magazineCapacityMultiplier + value);
    }

    /// <summary>
    /// 兼容旧弹夹固定值入口，按当前武器基础弹夹容量换算为倍率。
    /// </summary>
    /// <param name="value">旧弹夹容量增加值。</param>
    public void AddMagazineCapacity(int value)
    {
        if (value == 0 || _currentWeaponData == null)
            return;

        int baseCapacity = Mathf.Max(1, _currentWeaponData.MagazineCapacity);
        magazineCapacityMultiplier = Mathf.Max(0.01f, magazineCapacityMultiplier + value / (float)baseCapacity);
    }

    /// <summary>
    /// 调整换弹时间加成。
    /// </summary>
    /// <param name="value">换弹时间加成值。</param>
    public void AddReloadSpeedMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        reloadSpeedMultiplier = Mathf.Max(0.01f, reloadSpeedMultiplier + value);
    }

    /// <summary>
    /// 兼容旧换弹时间固定值入口，负值会转换为换弹速度提升。
    /// </summary>
    /// <param name="value">旧换弹时间增加值。</param>
    public void AddReloadTime(float value)
    {
        if (Mathf.Approximately(value, 0f) || _currentWeaponData == null)
            return;

        float baseReloadTime = Mathf.Max(0.01f, _currentWeaponData.ReloadTime);
        float currentReloadTime = baseReloadTime / reloadSpeedMultiplier;
        float nextReloadTime = Mathf.Max(0.01f, currentReloadTime + value);
        reloadSpeedMultiplier = Mathf.Max(0.01f, baseReloadTime / nextReloadTime);
    }

    /// <summary>
    /// 调整最大能量加成。
    /// </summary>
    /// <param name="value">最大能量加成值。</param>
    public void AddMaxEnergy(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        maxEnergyBonus += value;
    }

    /// <summary>
    /// 调整每秒能量恢复加成。
    /// </summary>
    /// <param name="value">每秒能量恢复加成值。</param>
    public void AddEnergyRecovery(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        energyRecoveryBonus += value;
    }

    /// <summary>
    /// 调整击杀能量奖励加成。
    /// </summary>
    /// <param name="value">击杀能量奖励加成值。</param>
    public void AddEnergyPerKill(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        energyPerKillBonus += value;
    }

    /// <summary>
    /// 增加射击精准度加成，超过 1 的部分会线性抵消武器基础散射。
    /// </summary>
    /// <param name="value">精准度加成值。</param>
    public void AddAccuracyMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        accuracyMultiplier = Mathf.Max(0.01f, accuracyMultiplier + value);
    }

    /// <summary>
    /// 兼容旧开火扩散固定值入口，正值会增加扩散，负值会提升精确度。
    /// </summary>
    /// <param name="value">旧开火扩散增加值。</param>
    public void AddAccuracyAngle(float value)
    {
        if (Mathf.Approximately(value, 0f) || _currentWeaponData == null)
            return;

        float baseAccuracy = Mathf.Max(0.01f, _currentWeaponData.AccuracyAngle);
        float currentAccuracy = baseAccuracy * Mathf.Max(0f, 2f - accuracyMultiplier);
        float nextAccuracy = Mathf.Max(0f, currentAccuracy + value);
        accuracyMultiplier = nextAccuracy <= 0f ? 2f : Mathf.Max(0.01f, 2f - nextAccuracy / baseAccuracy);
    }

    /// <summary>
    /// 调整后坐力距离加成。
    /// </summary>
    /// <param name="value">后坐力距离加成值。</param>
    public void AddRecoilControlMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        recoilControlMultiplier = Mathf.Max(0.01f, recoilControlMultiplier + value);
    }

    /// <summary>
    /// 兼容旧后坐力固定值入口，负值会转换为后坐力控制提升。
    /// </summary>
    /// <param name="value">旧后坐力距离增加值。</param>
    public void AddRecoilDistance(float value)
    {
        if (Mathf.Approximately(value, 0f) || _currentWeaponData == null)
            return;

        float baseRecoil = Mathf.Max(0.01f, _currentWeaponData.RecoilDistance);
        float currentRecoil = baseRecoil / recoilControlMultiplier;
        float nextRecoil = Mathf.Max(0f, currentRecoil + value);
        recoilControlMultiplier = nextRecoil <= 0f ? 100f : Mathf.Max(0.01f, baseRecoil / nextRecoil);
    }

    /// <summary>
    /// 调整命中击退距离加成。
    /// </summary>
    /// <param name="value">命中击退距离加成值。</param>
    public void AddImpactMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        impactMultiplier = Mathf.Max(0.01f, impactMultiplier + value);
    }

    /// <summary>
    /// 兼容旧冲击距离固定值入口，按当前武器基础冲击距离换算为倍率。
    /// </summary>
    /// <param name="value">旧冲击距离增加值。</param>
    public void AddImpactDistance(float value)
    {
        if (Mathf.Approximately(value, 0f) || _currentWeaponData == null)
            return;

        float baseImpact = Mathf.Max(0.01f, _currentWeaponData.ImpactDistance);
        impactMultiplier = Mathf.Max(0.01f, impactMultiplier + value / baseImpact);
    }

    /// <summary>
    /// 设置临时移动速度倍率，主要用于蓄力减速。
    /// </summary>
    /// <param name="multiplier">移动速度倍率。</param>
    /// <summary>
    /// 按倍率提升投射物体积。
    /// </summary>
    /// <param name="value">倍率增量，0.15 表示体积 +15%。</param>
    public void AddProjectileScaleMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        projectileScaleMultiplier = Mathf.Max(0.01f, projectileScaleMultiplier + value);
    }

    public void SetTemporaryMoveSpeedMultiplier(float multiplier)
    {
        _temporaryMoveSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>
    /// 设置玩家是否允许移动输入。
    /// </summary>
    /// <param name="enabled">是否启用移动输入。</param>
    public void SetControlEnabled(bool enabled)
    {
        _controlEnabled = enabled;
        if (!_controlEnabled)
            _moveInput = Vector2.zero;
    }

    /// <summary>
    /// 设置玩家移动边界，后续移动会被限制在该矩形区域内。
    /// </summary>
    /// <param name="minPosition">可移动区域最小坐标。</param>
    /// <param name="maxPosition">可移动区域最大坐标。</param>
    public void SetMovementBounds(Vector2 minPosition, Vector2 maxPosition)
    {
        if (_sceneCollisionController != null)
            _sceneCollisionController.SetMovementBounds(minPosition, maxPosition);
    }

    /// <summary>
    /// 尝试按当前移动方向开始翻滚。
    /// </summary>
    private void TryStartRoll()
    {
        if (_rollController != null)
            _rollController.TryStartRoll(_moveInput, _lastMoveDirection);
    }

    /// <summary>
    /// 获取当前等级升级所需经验。
    /// </summary>
    /// <returns>升级所需经验。</returns>
    public int GetRequiredExperience()
    {
        return 5 + (Level - 1) * 3;
    }

    /// <summary>
    /// 使用武器数据初始化武器运行时属性。
    /// </summary>
    /// <param name="weaponData">武器配置数据。</param>
    public void InitializeWeaponStats(WeaponData weaponData)
    {
        if (weaponData == null)
            return;

        _currentWeaponData = weaponData;
        criticalRate = Mathf.Clamp01(weaponData.CriticalRate);
        criticalDamage = Mathf.Max(1f, weaponData.CriticalDamage);
        projectilePierce = Mathf.Max(0, weaponData.ProjectilePierce);
        projectileBounce = Mathf.Max(0, weaponData.ProjectileBounce);
        projectileCount = Mathf.Max(1, weaponData.ProjectileCount);
    }

    /// <summary>
    /// 设置玩家成功受伤时播放的 Feel 反馈播放器。
    /// </summary>
    /// <param name="feedbacks">受击反馈播放器。</param>
    public void SetHurtFeedbacks(MMF_Player feedbacks)
    {
        if (_hurtFeedbackController != null)
            _hurtFeedbackController.SetHurtFeedbacks(feedbacks);
    }

    /// <summary>
    /// 判断指定等级是否触发武器强化选择。
    /// </summary>
    /// <param name="level">需要判断的等级。</param>
    /// <param name="weaponData">当前使用的武器配置。</param>
    /// <returns>该等级触发武器强化时返回 true。</returns>
    private bool IsWeaponUpgradeLevel(int level, WeaponData weaponData)
    {
        int[] triggerLevels = weaponData.WeaponUpgradeTriggerLevels;
        if (triggerLevels == null)
            return false;

        int maxTriggerCount = Mathf.Min(triggerLevels.Length, weaponData.WeaponUpgradeMaxCount);
        for (int i = 0; i < maxTriggerCount; i++)
        {
            if (triggerLevels[i] == level)
                return true;
        }

        return false;
    }
}

