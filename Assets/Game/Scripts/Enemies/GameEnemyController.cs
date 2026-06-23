using UnityEngine;

/// <summary>
/// 控制敌人通用属性、受伤、奖励数据，并驱动具体敌人行为。
/// </summary>
public sealed class GameEnemyController : MonoBehaviour
{
    [Header("基础属性")]
    [Tooltip("敌人最大生命值；运行时会被 EnemyData 覆盖。")]
    [SerializeField] private int maxHealth = 24;
    [Tooltip("敌人基础移动速度；运行时会被 EnemyData 覆盖。")]
    [SerializeField] private float moveSpeed = 1.6f;
    [Tooltip("敌人与玩家接触时造成的单次伤害；运行时会被 EnemyData 覆盖。")]
    [SerializeField] private int contactDamage = 8;
    [Tooltip("敌人死亡时掉落的经验值；运行时会被 EnemyData 覆盖。")]
    [SerializeField] private int dropExperience = 1;
    [Tooltip("敌人死亡时掉落的金币数量；运行时会被 EnemyData 覆盖。")]
    [SerializeField] private int dropGold = 1;
    [Tooltip("击杀敌人获得的分数；运行时会被 EnemyData 覆盖。")]
    [SerializeField] private int score = 10;

    [Header("编辑器可视化")]
    [Tooltip("仅用于 Scene 视图显示敌人自身大小参考，不参与当前伤害判定。")]
    [SerializeField] private float bodyGizmoRadius = 0.45f;

    private GamePlayerController _target;
    private IGameEnemyBehavior _behavior;
    private float _damageTimer;
    private bool _isStageBoss;
    private bool _isStageBossSummon;

    /// <summary>
    /// 掉落经验。
    /// </summary>
    public int DropExperience => dropExperience;

    /// <summary>
    /// 掉落金币。
    /// </summary>
    public int DropGold => dropGold;

    /// <summary>
    /// 击杀得分。
    /// </summary>
    public int Score => score;

    /// <summary>
    /// 是否为 Stage Boss 本体。
    /// </summary>
    public bool IsStageBoss => _isStageBoss;

    /// <summary>
    /// 是否为 Stage Boss 召唤物。
    /// </summary>
    public bool IsStageBossSummon => _isStageBossSummon;

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public int CurrentHealth { get; private set; }

    /// <summary>
    /// 当前追踪的玩家目标。
    /// </summary>
    public GamePlayerController Target => _target;

    /// <summary>
    /// 当前移动速度。
    /// </summary>
    public float MoveSpeed => moveSpeed;

    /// <summary>
    /// 是否已经死亡。
    /// </summary>
    public bool IsDead => CurrentHealth <= 0;

    /// <summary>
    /// 当前生命百分比。
    /// </summary>
    public float HealthPercent => maxHealth > 0 ? Mathf.Clamp01((float)CurrentHealth / maxHealth) : 0f;

    /// <summary>
    /// 是否存在可用的存活玩家目标。
    /// </summary>
    public bool HasLivingTarget => _target != null && !_target.IsDead;

    /// <summary>
    /// 当前与玩家目标之间的距离。
    /// </summary>
    public float DistanceToTarget => HasLivingTarget ? Vector3.Distance(transform.position, _target.transform.position) : float.MaxValue;

    /// <summary>
    /// 指向玩家目标的单位方向。
    /// </summary>
    public Vector3 DirectionToTarget
    {
        get
        {
            if (!HasLivingTarget)
                return Vector3.zero;

            Vector3 direction = _target.transform.position - transform.position;
            direction.z = 0f;
            return direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.zero;
        }
    }

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    private void Update()
    {
        if (IsDead || _behavior == null)
            return;

        _behavior.TickBehavior(Time.deltaTime);
    }

    /// <summary>
    /// 在 Scene 视图中绘制敌人自身参考半径。
    /// </summary>
    private void OnDrawGizmos()
    {
        GameRadiusGizmoUtility.DrawRadius(transform.position, bodyGizmoRadius, new Color(0.65f, 0.65f, 0.65f, 0.9f), "Enemy Body");
    }

    /// <summary>
    /// 初始化怪物运行时属性。
    /// </summary>
    /// <param name="target">追踪目标。</param>
    /// <param name="enemyData">怪物配置数据。</param>
    /// <param name="healthMultiplier">生命倍率。</param>
    /// <param name="speedMultiplier">速度倍率。</param>
    public void Initialize(GamePlayerController target, EnemyData enemyData, float healthMultiplier, float speedMultiplier)
    {
        Initialize(target, enemyData, healthMultiplier, speedMultiplier, 1f, 1f, 1f);
    }

    /// <summary>
    /// 初始化怪物运行时属性，并应用关卡难度提供的属性和奖励倍率。
    /// </summary>
    /// <param name="target">追踪目标。</param>
    /// <param name="enemyData">怪物配置数据。</param>
    /// <param name="healthMultiplier">生命倍率。</param>
    /// <param name="speedMultiplier">速度倍率。</param>
    /// <param name="damageMultiplier">接触伤害倍率。</param>
    /// <param name="goldMultiplier">金币掉落倍率。</param>
    /// <param name="experienceMultiplier">经验掉落倍率。</param>
    public void Initialize(
        GamePlayerController target,
        EnemyData enemyData,
        float healthMultiplier,
        float speedMultiplier,
        float damageMultiplier,
        float goldMultiplier,
        float experienceMultiplier)
    {
        _target = target;
        if (enemyData != null)
        {
            maxHealth = Mathf.Max(1, enemyData.MaxHealth);
            moveSpeed = Mathf.Max(0.1f, enemyData.MoveSpeed);
            contactDamage = Mathf.Max(0, enemyData.ContactDamage);
            dropExperience = Mathf.Max(0, enemyData.DropExperience);
            dropGold = Mathf.Max(0, enemyData.DropGold);
            score = Mathf.Max(0, enemyData.Score);
        }

        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * healthMultiplier));
        moveSpeed *= Mathf.Max(0.1f, speedMultiplier);
        contactDamage = Mathf.Max(0, Mathf.RoundToInt(contactDamage * Mathf.Max(0f, damageMultiplier)));
        dropGold = Mathf.Max(0, Mathf.RoundToInt(dropGold * Mathf.Max(0f, goldMultiplier)));
        dropExperience = Mathf.Max(0, Mathf.RoundToInt(dropExperience * Mathf.Max(0f, experienceMultiplier)));
        CurrentHealth = maxHealth;
        _damageTimer = 0f;
        _isStageBoss = false;
        _isStageBossSummon = false;
        CacheBehavior(enemyData);
        if (_behavior != null)
            _behavior.Initialize(this);
    }

    /// <summary>
    /// 标记当前敌人在 Stage Boss 战中的运行时身份。
    /// </summary>
    /// <param name="isBoss">是否为 Boss 本体。</param>
    /// <param name="isBossSummon">是否为 Boss 召唤物。</param>
    public void SetStageBossRuntimeFlags(bool isBoss, bool isBossSummon)
    {
        _isStageBoss = isBoss;
        _isStageBossSummon = isBossSummon;
    }

    /// <summary>
    /// 对怪物造成伤害。
    /// </summary>
    /// <param name="damage">伤害值。</param>
    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDead)
            return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
    }

    /// <summary>
    /// 让敌人向玩家目标移动。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    public void MoveTowardTarget(float deltaTime)
    {
        MoveInDirection(DirectionToTarget, moveSpeed * Mathf.Max(0f, deltaTime));
    }

    /// <summary>
    /// 让敌人远离玩家目标移动。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    public void MoveAwayFromTarget(float deltaTime)
    {
        MoveInDirection(-DirectionToTarget, moveSpeed * Mathf.Max(0f, deltaTime));
    }

    /// <summary>
    /// 让敌人沿指定方向移动指定距离。
    /// </summary>
    /// <param name="direction">移动方向。</param>
    /// <param name="distance">移动距离。</param>
    public void MoveInDirection(Vector3 direction, float distance)
    {
        if (direction.sqrMagnitude <= 0.01f || distance <= 0f)
            return;

        transform.position += direction.normalized * distance;
    }

    /// <summary>
    /// 按接触半径和间隔处理对玩家的持续接触伤害。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    /// <param name="contactRadius">接触伤害半径。</param>
    /// <param name="damageInterval">接触伤害间隔。</param>
    public void TickContactDamage(float deltaTime, float contactRadius, float damageInterval)
    {
        if (!HasLivingTarget || contactDamage <= 0)
            return;

        _damageTimer -= deltaTime;
        if (_damageTimer > 0f)
            return;

        if (DistanceToTarget > Mathf.Max(0.01f, contactRadius))
            return;

        _damageTimer = Mathf.Max(0.01f, damageInterval);
        _target.TakeDamage(contactDamage);
    }

    /// <summary>
    /// 缓存当前敌人的行为脚本，缺失时按 EnemyData 行为类型添加默认行为。
    /// </summary>
    /// <param name="enemyData">敌人配置数据。</param>
    private void CacheBehavior(EnemyData enemyData)
    {
        _behavior = GetEnabledBehavior();
        if (_behavior != null)
            return;

        _behavior = AddFallbackBehavior(enemyData);
    }

    /// <summary>
    /// 获取当前启用的敌人行为脚本。
    /// </summary>
    /// <returns>启用中的敌人行为。</returns>
    private IGameEnemyBehavior GetEnabledBehavior()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.enabled && behaviour is IGameEnemyBehavior enemyBehavior)
                return enemyBehavior;
        }

        return null;
    }

    /// <summary>
    /// 根据 EnemyData 行为类型补齐行为脚本，兼容未挂行为组件的旧预制体。
    /// </summary>
    /// <param name="enemyData">敌人配置数据。</param>
    /// <returns>补齐后的敌人行为。</returns>
    private IGameEnemyBehavior AddFallbackBehavior(EnemyData enemyData)
    {
        if (enemyData == null)
            return gameObject.AddComponent<GameFoxEnemyBehavior>();

        switch (enemyData.BehaviorType)
        {
            case EnemyData.EnemyBehaviorType.Wolf:
                return gameObject.AddComponent<GameWolfEnemyBehavior>();

            case EnemyData.EnemyBehaviorType.Snake:
                return gameObject.AddComponent<GameSnakeEnemyBehavior>();

            case EnemyData.EnemyBehaviorType.Bear:
                return gameObject.AddComponent<GameBearEnemyBehavior>();

            case EnemyData.EnemyBehaviorType.Fox:
            default:
                return gameObject.AddComponent<GameFoxEnemyBehavior>();
        }
    }
}
