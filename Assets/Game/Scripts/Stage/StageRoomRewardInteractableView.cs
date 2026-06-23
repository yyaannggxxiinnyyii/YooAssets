using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 功能房中的通用奖励交互对象，可配置成宝箱、回血机或属性训练器。
/// </summary>
public sealed class StageRoomRewardInteractableView : MonoBehaviour, IStageRoomFunctionObject
{
    [Header("交互")]
    [Tooltip("玩家靠近功能对象后允许交互的距离。")]
    [SerializeField] private float interactRadius = 1.2f;
    [Tooltip("触发功能对象交互的按键。")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("交互成功后是否只允许使用一次。")]
    [SerializeField] private bool useOnce = true;

    [Header("奖励")]
    [Tooltip("功能对象显示名称，用于 Tooltip 文案。")]
    [SerializeField] private string displayName = "宝箱";
    [Tooltip("功能对象说明文本，用于 Tooltip 第二行。")]
    [SerializeField] private string description = "打开后获得奖励。";
    [Tooltip("交互成功后应用的奖励列表。")]
    [SerializeField] private StageRoomRewardEntry[] rewards = new StageRoomRewardEntry[0];

    [Header("显示")]
    [Tooltip("未使用时显示的可视根节点；为空时不主动隐藏任何表现。")]
    [SerializeField] private GameObject availableVisualRoot;
    [Tooltip("使用后显示的可视根节点；为空时使用后只隐藏交互。")]
    [SerializeField] private GameObject usedVisualRoot;
    [Tooltip("Tooltip 整体根节点；用于同时显隐背景框和文字。为空时只显隐 Tooltip 文本。")]
    [SerializeField] private GameObject tooltipRoot;
    [Tooltip("Tooltip TMP 文本；可绑定世界空间 TextMeshPro 或 Canvas 下的 TextMeshProUGUI。")]
    [SerializeField] private TMP_Text tooltipText;
    [Tooltip("Tooltip 文本相对功能对象的本地偏移，仅在脚本自动创建文本时使用。")]
    [SerializeField] private Vector3 tooltipOffset = new Vector3(0f, 1.1f, 0f);

    private StageRoomFunctionContext _context;
    private Transform _player;
    private bool _initialized;
    private bool _used;
    private bool _playerInRange;

    private void Awake()
    {
        EnsureTooltipText();
        SetTooltipVisible(false);
        SetUsedVisualVisible(false);
    }

    private void Update()
    {
        if (!_initialized || (_used && useOnce))
        {
            SetPlayerInRange(false);
            return;
        }

        RefreshPlayerRangeByDistance();
        if (!_playerInRange)
            return;

        if (Input.GetKeyDown(interactKey))
            TryUse();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        GamePlayerController player = other.GetComponentInParent<GamePlayerController>();
        _player = player != null ? player.transform : other.transform;
        SetPlayerInRange(!_used || !useOnce);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_player == null || other.GetComponentInParent<GamePlayerController>() == null)
            return;

        _player = null;
        SetPlayerInRange(false);
    }

    /// <summary>
    /// 在 Scene 视图中绘制功能对象的距离交互范围。
    /// </summary>
    private void OnDrawGizmos()
    {
        GameRadiusGizmoUtility.DrawRadius(transform.position, Mathf.Max(0.1f, interactRadius), new Color(0.25f, 0.85f, 1f, 0.85f), "Reward Interact");
    }

    /// <summary>
    /// 使用功能房上下文初始化通用奖励交互对象。
    /// </summary>
    /// <param name="context">功能房运行时上下文。</param>
    public void Initialize(StageRoomFunctionContext context)
    {
        _context = context;
        _initialized = true;
        _used = false;
        RefreshTooltipText();
        SetAvailableVisualVisible(true);
        SetUsedVisualVisible(false);
        SetTooltipVisible(false);
    }

    /// <summary>
    /// 尝试使用当前功能对象并应用奖励。
    /// </summary>
    public void TryUse()
    {
        if (!_initialized || _context.Player == null || (_used && useOnce))
            return;

        if (!ApplyRewards(_context.Player))
            return;

        _used = true;
        SetPlayerInRange(false);
        SetAvailableVisualVisible(false);
        SetUsedVisualVisible(true);
        _context.RefreshHud();
        _context.CompleteFunctionObject();
    }

    /// <summary>
    /// 应用配置中的所有奖励条目。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    private bool ApplyRewards(GamePlayerController player)
    {
        if (player == null || rewards == null)
            return false;

        bool appliedAnyReward = false;
        for (int i = 0; i < rewards.Length; i++)
            appliedAnyReward |= ApplyReward(player, rewards[i]);

        return appliedAnyReward;
    }

    /// <summary>
    /// 应用单条奖励配置。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="reward">奖励配置。</param>
    private bool ApplyReward(GamePlayerController player, StageRoomRewardEntry reward)
    {
        if (reward == null)
            return false;

        switch (reward.RewardType)
        {
            case StageRoomRewardType.Gold:
                int gold = Mathf.RoundToInt(reward.Value);
                player.AddGold(gold);
                _context.AddGoldCollected(gold);
                return true;

            case StageRoomRewardType.Experience:
                player.AddExperience(Mathf.RoundToInt(reward.Value));
                return true;

            case StageRoomRewardType.HealFlat:
                return player.TryConsumeNormalHeal(Mathf.RoundToInt(reward.Value));

            case StageRoomRewardType.HealPercent:
                return player.TryConsumeNormalHeal(Mathf.RoundToInt(player.MaxHealth * Mathf.Max(0f, reward.Value)));

            case StageRoomRewardType.MaxHealth:
                player.AddMaxHealth(Mathf.RoundToInt(reward.Value));
                return true;

            case StageRoomRewardType.AddedAttackDamage:
                player.AddWeaponAttackDamage(Mathf.RoundToInt(reward.Value));
                return true;

            case StageRoomRewardType.AttackDamageMultiplier:
                player.AddAttackDamageMultiplier(reward.Value);
                return true;

            case StageRoomRewardType.MoveSpeedMultiplier:
                player.AddMoveSpeedMultiplier(reward.Value);
                return true;

            case StageRoomRewardType.AttackSpeedMultiplier:
                player.AddAttackSpeedMultiplier(reward.Value);
                return true;

            case StageRoomRewardType.ReloadSpeedMultiplier:
                player.AddReloadSpeedMultiplier(reward.Value);
                return true;

            case StageRoomRewardType.MagazineCapacityMultiplier:
                player.AddMagazineCapacityMultiplier(reward.Value);
                return true;

            case StageRoomRewardType.AccuracyMultiplier:
                player.AddAccuracyMultiplier(reward.Value);
                return true;

            case StageRoomRewardType.ProjectileSpeedMultiplier:
                player.AddProjectileSpeedMultiplier(reward.Value);
                return true;

            case StageRoomRewardType.ProjectileScaleMultiplier:
                player.AddProjectileScaleMultiplier(reward.Value);
                return true;

            case StageRoomRewardType.ProjectileLifeTimeMultiplier:
                player.AddProjectileLifeTimeMultiplier(reward.Value);
                return true;

            case StageRoomRewardType.CriticalRate:
                player.AddCriticalRate(reward.Value);
                return true;

            case StageRoomRewardType.CriticalDamageMultiplier:
                player.AddCriticalDamage(reward.Value);
                return true;
        }

        return false;
    }

    /// <summary>
    /// 使用距离检测刷新玩家是否处于交互范围。
    /// </summary>
    private void RefreshPlayerRangeByDistance()
    {
        if (_player == null)
            CachePlayerTransform();

        bool isInRange = _player != null &&
            (!_used || !useOnce) &&
            Vector2.Distance(transform.position, _player.position) <= Mathf.Max(0.1f, interactRadius);
        SetPlayerInRange(isInRange);
    }

    /// <summary>
    /// 查找并缓存当前玩家 Transform。
    /// </summary>
    private void CachePlayerTransform()
    {
        GamePlayerController player = FindObjectOfType<GamePlayerController>();
        if (player != null)
            _player = player.transform;
    }

    /// <summary>
    /// 设置玩家交互范围状态并同步 Tooltip。
    /// </summary>
    /// <param name="isInRange">玩家是否在交互范围内。</param>
    private void SetPlayerInRange(bool isInRange)
    {
        if (isInRange && (_used && useOnce))
            isInRange = false;

        if (_playerInRange == isInRange)
            return;

        _playerInRange = isInRange;
        SetTooltipVisible(_playerInRange);
    }

    /// <summary>
    /// 刷新 Tooltip 文本内容。
    /// </summary>
    private void RefreshTooltipText()
    {
        if (tooltipText == null)
            return;

        tooltipText.text = $"E 使用{displayName}\n{description}";
    }

    /// <summary>
    /// 设置 Tooltip 显隐。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    private void SetTooltipVisible(bool visible)
    {
        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(visible);
            return;
        }

        if (tooltipText != null)
            tooltipText.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 设置未使用可视根节点显隐。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    private void SetAvailableVisualVisible(bool visible)
    {
        if (availableVisualRoot != null)
            availableVisualRoot.SetActive(visible);
    }

    /// <summary>
    /// 设置已使用可视根节点显隐。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    private void SetUsedVisualVisible(bool visible)
    {
        if (usedVisualRoot != null)
            usedVisualRoot.SetActive(visible);
    }

    /// <summary>
    /// 判断碰撞体是否属于玩家。
    /// </summary>
    /// <param name="other">进入触发区的碰撞体。</param>
    /// <returns>属于玩家时返回 true。</returns>
    private bool IsPlayerCollider(Collider2D other)
    {
        return other != null && other.GetComponentInParent<GamePlayerController>() != null;
    }

    /// <summary>
    /// 确保 Tooltip 文本存在，未配置时创建世界空间 TextMeshPro 兜底。
    /// </summary>
    private void EnsureTooltipText()
    {
        if (tooltipText == null)
            tooltipText = GetComponentInChildren<TMP_Text>(true);

        if (tooltipText == null)
        {
            GameObject tooltipObject = new GameObject("FunctionTooltip");
            tooltipObject.transform.SetParent(transform, false);
            tooltipObject.transform.localPosition = tooltipOffset;
            TextMeshPro worldText = tooltipObject.AddComponent<TextMeshPro>();
            worldText.alignment = TextAlignmentOptions.Center;
            worldText.fontSize = 0.28f;
            worldText.enableWordWrapping = false;
            worldText.sortingOrder = 100;
            tooltipText = worldText;
        }

        if (tooltipRoot == null && tooltipText != null)
            tooltipRoot = tooltipText.gameObject;
    }
}

/// <summary>
/// 功能房通用奖励类型。
/// </summary>
public enum StageRoomRewardType
{
    Gold,
    Experience,
    HealFlat,
    HealPercent,
    MaxHealth,
    AddedAttackDamage,
    AttackDamageMultiplier,
    MoveSpeedMultiplier,
    AttackSpeedMultiplier,
    ReloadSpeedMultiplier,
    MagazineCapacityMultiplier,
    AccuracyMultiplier,
    ProjectileSpeedMultiplier,
    ProjectileScaleMultiplier,
    ProjectileLifeTimeMultiplier,
    CriticalRate,
    CriticalDamageMultiplier
}

/// <summary>
/// 定义功能房交互对象的一条奖励效果。
/// </summary>
[Serializable]
public sealed class StageRoomRewardEntry
{
    [Header("奖励")]
    [Tooltip("奖励类型。")]
    [SerializeField] private StageRoomRewardType rewardType = StageRoomRewardType.Gold;
    [Tooltip("奖励数值；倍率类填小数，例如 0.2 表示 +20%。")]
    [SerializeField] private float value = 1f;

    /// <summary>
    /// 奖励类型。
    /// </summary>
    public StageRoomRewardType RewardType => rewardType;

    /// <summary>
    /// 奖励数值。
    /// </summary>
    public float Value => value;
}
