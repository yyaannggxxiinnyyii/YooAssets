using UnityEngine;

/// <summary>
/// 控制玩家翻滚状态、翻滚充能、翻滚无敌和残影表现。
/// </summary>
[RequireComponent(typeof(GamePlayerAfterimageController))]
public sealed class GamePlayerRollController : MonoBehaviour
{
    private float _rollSpeed = 12f;
    private float _rollDuration = 0.22f;
    private float _rollCooldown = 0.8f;
    private float _rollInvincibleDuration = 0.2f;
    private float _rollTimer;
    private float _rollRechargeTimer;
    private float _invincibleTimer;
    private int _currentRollCharges = 1;
    private int _maxRollCharges = 1;
    private Vector2 _rollDirection = Vector2.right;
    private GamePlayerAfterimageController _afterimageController;

    /// <summary>
    /// 是否正在翻滚。
    /// </summary>
    public bool IsRolling => _rollTimer > 0f;

    /// <summary>
    /// 翻滚无敌是否生效。
    /// </summary>
    public bool IsInvincible => _invincibleTimer > 0f;

    /// <summary>
    /// 当前翻滚方向。
    /// </summary>
    public Vector2 RollDirection => _rollDirection;

    /// <summary>
    /// 当前翻滚速度。
    /// </summary>
    public float RollSpeed => _rollSpeed;

    /// <summary>
    /// 闪避冷却是否正在恢复。
    /// </summary>
    public bool IsRollCoolingDown => _currentRollCharges < _maxRollCharges;

    /// <summary>
    /// 当前可使用的翻滚充能次数。
    /// </summary>
    public int CurrentRollCharges => _currentRollCharges;

    /// <summary>
    /// 当前最大翻滚充能次数。
    /// </summary>
    public int MaxRollCharges => _maxRollCharges;

    /// <summary>
    /// 当前翻滚冷却进度。
    /// </summary>
    public float RollCooldownProgress
    {
        get
        {
            if (_rollCooldown <= 0f)
                return 1f;

            if (_currentRollCharges >= _maxRollCharges)
                return 1f;

            return Mathf.Clamp01(_rollRechargeTimer / _rollCooldown);
        }
    }

    private void Awake()
    {
        _afterimageController = GetComponent<GamePlayerAfterimageController>();
        if (_afterimageController == null)
            Debug.LogError($"[PlayerRoll] 玩家 {name} 缺少 GamePlayerAfterimageController。", this);

        _maxRollCharges = Mathf.Max(1, _maxRollCharges);
        _currentRollCharges = _maxRollCharges;
    }

    /// <summary>
    /// 使用角色数据初始化翻滚基础参数。
    /// </summary>
    /// <param name="characterData">角色配置数据。</param>
    public void Initialize(CharacterData characterData)
    {
        if (characterData == null)
            return;

        _rollSpeed = Mathf.Max(0.1f, characterData.RollSpeed);
        _rollDuration = Mathf.Max(0.01f, characterData.RollDuration);
        _rollCooldown = Mathf.Max(0.01f, characterData.RollCooldown);
        _maxRollCharges = Mathf.Max(1, characterData.MaxRollCharges);
        _rollInvincibleDuration = Mathf.Max(0f, characterData.RollInvincibleDuration);
        _currentRollCharges = _maxRollCharges;
        _rollTimer = 0f;
        _rollRechargeTimer = 0f;
        _invincibleTimer = 0f;
    }

    /// <summary>
    /// 推进翻滚无敌计时。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    public void TickInvincible(float deltaTime)
    {
        _invincibleTimer = Mathf.Max(0f, _invincibleTimer - Mathf.Max(0f, deltaTime));
    }

    /// <summary>
    /// 推进翻滚充能恢复。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    public void TickRecharge(float deltaTime)
    {
        _maxRollCharges = Mathf.Max(1, _maxRollCharges);
        _currentRollCharges = Mathf.Clamp(_currentRollCharges, 0, _maxRollCharges);
        if (_currentRollCharges >= _maxRollCharges)
        {
            _rollRechargeTimer = 0f;
            return;
        }

        if (_rollCooldown <= 0f)
        {
            _currentRollCharges = _maxRollCharges;
            _rollRechargeTimer = 0f;
            return;
        }

        _rollRechargeTimer += Mathf.Max(0f, deltaTime);
        while (_rollRechargeTimer >= _rollCooldown && _currentRollCharges < _maxRollCharges)
        {
            _rollRechargeTimer -= _rollCooldown;
            _currentRollCharges++;
        }

        if (_currentRollCharges >= _maxRollCharges)
            _rollRechargeTimer = 0f;
    }

    /// <summary>
    /// 推进当前翻滚位移持续时间。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    public void TickRollMovement(float deltaTime)
    {
        _rollTimer = Mathf.Max(0f, _rollTimer - Mathf.Max(0f, deltaTime));
    }

    /// <summary>
    /// 尝试按当前输入方向开始翻滚。
    /// </summary>
    /// <param name="moveInput">当前移动输入。</param>
    /// <param name="lastMoveDirection">最后一次有效移动方向。</param>
    /// <returns>成功开始翻滚时返回 true。</returns>
    public bool TryStartRoll(Vector2 moveInput, Vector2 lastMoveDirection)
    {
        if (_currentRollCharges <= 0 || IsRolling)
            return false;

        _rollDirection = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : lastMoveDirection;
        _rollTimer = _rollDuration;
        _currentRollCharges--;
        _invincibleTimer = Mathf.Max(_invincibleTimer, _rollInvincibleDuration);
        if (_afterimageController != null)
            _afterimageController.PlayAfterimage(_rollDuration);

        return true;
    }

    /// <summary>
    /// 按倍率调整翻滚距离，通过提升翻滚速度保持翻滚持续时间不变。
    /// </summary>
    /// <param name="value">倍率增量，0.3 表示提升 30%。</param>
    public void AddRollDistanceMultiplier(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        _rollSpeed = Mathf.Max(0.1f, _rollSpeed * Mathf.Max(0.01f, 1f + value));
    }

    /// <summary>
    /// 按比例降低翻滚冷却时间。
    /// </summary>
    /// <param name="value">降低比例，0.2 表示冷却减少 20%。</param>
    public void ReduceRollCooldownRate(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        _rollCooldown = Mathf.Max(0.01f, _rollCooldown * Mathf.Max(0.01f, 1f - value));
        _rollRechargeTimer = Mathf.Min(_rollRechargeTimer, _rollCooldown);
    }

    /// <summary>
    /// 增加最大翻滚充能次数，并同步补充相同数量的当前可用次数。
    /// </summary>
    /// <param name="value">增加的翻滚次数。</param>
    public void AddRollChargeCount(int value)
    {
        if (value <= 0)
            return;

        _maxRollCharges = Mathf.Max(1, _maxRollCharges + value);
        _currentRollCharges = Mathf.Clamp(_currentRollCharges + value, 0, _maxRollCharges);
        if (_currentRollCharges >= _maxRollCharges)
            _rollRechargeTimer = 0f;
    }

    /// <summary>
    /// 增加翻滚无敌持续时间。
    /// </summary>
    /// <param name="value">增加的无敌秒数。</param>
    public void AddRollInvincibleDuration(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        _rollInvincibleDuration = Mathf.Max(0f, _rollInvincibleDuration + value);
    }

    /// <summary>
    /// 应用斯安威斯坦残影表现配置。
    /// </summary>
    public void ApplySandevistanAfterimage()
    {
        if (_afterimageController == null)
            _afterimageController = GetComponent<GamePlayerAfterimageController>();

        if (_afterimageController == null)
        {
            Debug.LogError($"[PlayerRoll] 玩家 {name} 缺少 GamePlayerAfterimageController，无法应用残影配置。", this);
            return;
        }

        _afterimageController.SetSpawnInterval(0.035f);
        _afterimageController.SetStartColor(new Color(0.55f, 0.9f, 1f, 0.42f));
        _afterimageController.ClearSourceRenderers();
    }
}
