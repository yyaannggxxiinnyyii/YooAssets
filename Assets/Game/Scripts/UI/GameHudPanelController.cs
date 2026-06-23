using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理战斗 HUD 面板的血量、经验、弹药、能量和统计文本显示。
/// </summary>
public sealed class GameHudPanelController : Singleton<GameHudPanelController>
{
    [Header("HUD 文本")]
    [Tooltip("显示玩家当前等级的文本。")]
    [SerializeField] private TMP_Text levelText;
    [Tooltip("显示玩家当前金币数量的文本。")]
    [SerializeField] private TMP_Text goldText;
    [Tooltip("HUD 心形血量贴图视图，配置后会替代旧文本血量显示。")]
    [SerializeField] private GameHeartHealthView heartHealthView;
    [Tooltip("未配置心形血量视图时显示生命值的兜底文本。")]
    [SerializeField] private TMP_Text healthText;
    [Tooltip("显示当前楼层和倒计时的文本。")]
    [SerializeField] private TMP_Text floorTimerText;
    [Tooltip("显示当前击杀数量的文本。")]
    [SerializeField] private TMP_Text killText;
    [Tooltip("显示当前得分的文本。")]
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("显示本局经过时间的文本。")]
    [SerializeField] private TMP_Text timeText;

    [Header("HUD 进度")]
    [Tooltip("显示当前经验进度的填充图片。")]
    [SerializeField] private Image experienceFillImage;
    [Tooltip("显示当前弹夹比例的填充图片。")]
    [SerializeField] private Image ammoFillImage;
    [Tooltip("显示当前武器能量比例的填充图片。")]
    [SerializeField] private Image energyFillImage;
    [Tooltip("显示当前蓄力进度的填充图片。")]
    [SerializeField] private Image chargeFillImage;
    [Tooltip("显示当前弹夹数量或换弹状态的文本。")]
    [SerializeField] private TMP_Text ammoText;
    [Tooltip("显示当前武器能量百分比的文本。")]
    [SerializeField] private TMP_Text energyText;

    [Header("HUD 动效")]
    [Tooltip("经验条普通增长时每秒推进的比例。")]
    [SerializeField] private float experienceFillSpeed = 2.8f;
    [Tooltip("升级时经验条先补满的每秒推进比例。")]
    [SerializeField] private float experienceLevelUpFillSpeed = 4.5f;

    private float _displayedExperienceRate;
    private float _targetExperienceRate;
    private float _queuedExperienceRate;
    private int _lastLevel = -1;
    private bool _experienceInitialized;
    private bool _isPlayingLevelUpExperienceFill;

    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
    }

    private void Update()
    {
        TickExperienceFill(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 刷新 HUD 的基础运行时显示、临时特殊血显示和武器资源显示。
    /// </summary>
    /// <param name="level">玩家等级。</param>
    /// <param name="gold">当前金币。</param>
    /// <param name="currentHealth">当前红血。</param>
    /// <param name="maxHealth">最大红血。</param>
    /// <param name="specialHealthSegments">临时特殊血量段。</param>
    /// <param name="experienceRate">经验进度。</param>
    /// <param name="floorText">楼层与倒计时文本。</param>
    /// <param name="kills">击杀数量。</param>
    /// <param name="score">当前得分。</param>
    /// <param name="elapsedSeconds">本局经过秒数。</param>
    /// <param name="ammoRate">弹夹进度。</param>
    /// <param name="energyRate">能量进度。</param>
    /// <param name="currentAmmo">当前弹药。</param>
    /// <param name="maxAmmo">最大弹药。</param>
    /// <param name="isReloading">是否正在换弹。</param>
    public void RefreshHud(
        int level,
        int gold,
        int currentHealth,
        int maxHealth,
        IReadOnlyList<SpecialHealthSegment> specialHealthSegments,
        float experienceRate,
        string floorText,
        int kills,
        int score,
        int elapsedSeconds,
        float ammoRate,
        float energyRate,
        int currentAmmo,
        int maxAmmo,
        bool isReloading)
    {
        SetText(levelText, $"Lv.{level}");
        SetText(goldText, $"Gold {gold}");
        RefreshHealth(currentHealth, maxHealth, specialHealthSegments);
        SetText(floorTimerText, floorText);
        SetText(killText, $"击杀 {kills}");
        SetText(scoreText, $"得分 {score}");
        SetText(timeText, $"时间 {elapsedSeconds}s");

        RefreshExperienceProgress(level, experienceRate);

        if (ammoFillImage != null)
            ammoFillImage.fillAmount = Mathf.Clamp01(ammoRate);

        if (energyFillImage != null)
            energyFillImage.fillAmount = Mathf.Clamp01(energyRate);

        SetText(ammoText, isReloading ? "换弹" : $"{currentAmmo}/{maxAmmo}");
        SetText(energyText, $"{Mathf.RoundToInt(Mathf.Clamp01(energyRate) * 100f)}%");
    }

    /// <summary>
    /// 刷新角色下方蓄力进度条。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    /// <param name="progress">蓄力进度。</param>
    public void RefreshChargeProgress(bool visible, float progress)
    {
        if (chargeFillImage == null)
            return;

        Transform chargeRoot = chargeFillImage.transform.parent;
        if (chargeRoot != null)
            chargeRoot.gameObject.SetActive(visible);

        chargeFillImage.fillAmount = Mathf.Clamp01(progress);
    }

    /// <summary>
    /// 切换 HUD 内弹夹、能量和蓄力相关资源显示。
    /// </summary>
    /// <param name="visible">是否显示武器资源 HUD。</param>
    public void SetWeaponResourceVisible(bool visible)
    {
        if (!visible)
            ResetWeaponResourceFill();

        SetResourceRootVisible(ammoFillImage, visible);
        SetResourceRootVisible(energyFillImage, visible);
        if (!visible)
            SetResourceRootVisible(chargeFillImage, false);

        SetTextVisible(ammoText, visible);
        SetTextVisible(energyText, visible);
    }

    /// <summary>
    /// 刷新 HUD 生命显示，优先使用心形贴图视图，未配置时回退为文本。
    /// </summary>
    /// <param name="currentHealth">当前生命点数。</param>
    /// <param name="maxHealth">最大生命点数。</param>
    /// <param name="specialHealthSegments">临时特殊血量段。</param>
    private void RefreshHealth(int currentHealth, int maxHealth, IReadOnlyList<SpecialHealthSegment> specialHealthSegments)
    {
        ResolveReferences();
        if (heartHealthView != null)
        {
            heartHealthView.Refresh(currentHealth, maxHealth, specialHealthSegments);

            if (healthText != null)
                healthText.gameObject.SetActive(false);

            return;
        }

        if (healthText != null)
            healthText.gameObject.SetActive(true);

        SetText(healthText, HealthDisplayUtility.FormatHeartText(currentHealth, maxHealth));
    }

    /// <summary>
    /// 接收经验进度目标值，并在升级时播放先补满再进入新等级进度的过渡。
    /// </summary>
    /// <param name="level">当前等级。</param>
    /// <param name="experienceRate">当前等级内的经验比例。</param>
    private void RefreshExperienceProgress(int level, float experienceRate)
    {
        float safeExperienceRate = Mathf.Clamp01(experienceRate);
        if (!_experienceInitialized || level < _lastLevel)
        {
            _displayedExperienceRate = safeExperienceRate;
            _targetExperienceRate = safeExperienceRate;
            _queuedExperienceRate = safeExperienceRate;
            _lastLevel = level;
            _experienceInitialized = true;
            _isPlayingLevelUpExperienceFill = false;
            ApplyExperienceFill();
            return;
        }

        if (level > _lastLevel)
        {
            _lastLevel = level;
            _queuedExperienceRate = safeExperienceRate;
            _targetExperienceRate = 1f;
            _isPlayingLevelUpExperienceFill = true;
            return;
        }

        _lastLevel = level;
        if (_isPlayingLevelUpExperienceFill)
        {
            _queuedExperienceRate = safeExperienceRate;
            return;
        }

        _targetExperienceRate = safeExperienceRate;
    }

    /// <summary>
    /// 推进经验条显示值，让 HUD 经验增长具有平滑过渡。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickExperienceFill(float deltaTime)
    {
        if (!_experienceInitialized)
            return;

        float speed = _isPlayingLevelUpExperienceFill ? experienceLevelUpFillSpeed : experienceFillSpeed;
        _displayedExperienceRate = Mathf.MoveTowards(
            _displayedExperienceRate,
            _targetExperienceRate,
            Mathf.Max(0.01f, speed) * Mathf.Max(0f, deltaTime));

        if (_isPlayingLevelUpExperienceFill && Mathf.Approximately(_displayedExperienceRate, 1f))
        {
            _displayedExperienceRate = 0f;
            _targetExperienceRate = _queuedExperienceRate;
            _isPlayingLevelUpExperienceFill = false;
        }

        ApplyExperienceFill();
    }

    /// <summary>
    /// 将当前经验条显示值写入 Image。
    /// </summary>
    private void ApplyExperienceFill()
    {
        if (experienceFillImage != null)
            experienceFillImage.fillAmount = Mathf.Clamp01(_displayedExperienceRate);
    }

    /// <summary>
    /// 将 HUD 武器资源进度恢复为满值，避免暂停、商店和升级等非战斗界面保留上一帧的空条。
    /// </summary>
    private void ResetWeaponResourceFill()
    {
        SetFillAmount(ammoFillImage, 1f);
        SetFillAmount(energyFillImage, 1f);
        SetFillAmount(chargeFillImage, 1f);
    }

    /// <summary>
    /// 安全设置进度图片填充值。
    /// </summary>
    /// <param name="targetImage">目标进度图片。</param>
    /// <param name="fillAmount">需要写入的填充值。</param>
    private void SetFillAmount(Image targetImage, float fillAmount)
    {
        if (targetImage == null)
            return;

        targetImage.fillAmount = Mathf.Clamp01(fillAmount);
    }

    /// <summary>
    /// 切换指定进度图片所在资源节点的显示状态。
    /// </summary>
    /// <param name="targetImage">目标进度图片。</param>
    /// <param name="visible">是否显示。</param>
    private void SetResourceRootVisible(Image targetImage, bool visible)
    {
        if (targetImage == null)
            return;

        Transform resourceRoot = targetImage.transform.parent != null ? targetImage.transform.parent : targetImage.transform;
        resourceRoot.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 切换指定文本对象的显示状态。
    /// </summary>
    /// <param name="targetText">目标文本组件。</param>
    /// <param name="visible">是否显示。</param>
    private void SetTextVisible(TMP_Text targetText, bool visible)
    {
        if (targetText == null)
            return;

        targetText.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 在序列化引用缺失时从 HUD 面板子节点中恢复生命值显示引用。
    /// </summary>
    private void ResolveReferences()
    {
        if (heartHealthView == null)
            heartHealthView = GetComponentInChildren<GameHeartHealthView>(true);
    }

    /// <summary>
    /// 安全设置 TMP 文本。
    /// </summary>
    /// <param name="targetText">目标文本组件。</param>
    /// <param name="content">显示内容。</param>
    private void SetText(TMP_Text targetText, string content)
    {
        if (targetText == null)
            return;

        targetText.text = content;
    }
}
