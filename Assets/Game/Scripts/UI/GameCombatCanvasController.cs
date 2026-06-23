using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 作为战斗 UI 门面，负责查找各面板控制器、转发面板事件并保留外部刷新入口。
/// </summary>
public sealed class GameCombatCanvasController : MonoBehaviour
{
    [Header("流程引用")]
    [Tooltip("游戏流程状态管理器，用于处理暂停、返回标题等流程按钮。")]
    [SerializeField] private GameStateManager gameStateManager;

    [Header("面板控制器")]
    [Tooltip("战斗 HUD 面板控制器，负责血量、经验、弹药和统计信息显示。")]
    [SerializeField] private GameHudPanelController hudPanelController;
    [Tooltip("升级强化面板控制器。")]
    [SerializeField] private GameLevelUpPanelController levelUpPanelController;
    [Tooltip("武器强化面板控制器。")]
    [SerializeField] private GameWeaponUpgradePanelController weaponUpgradePanelController;
    [Tooltip("商店面板控制器。")]
    [SerializeField] private GameShopPanelController shopPanelController;
    [Tooltip("结算面板控制器。")]
    [SerializeField] private GameResultPanelController resultPanelController;
    [Tooltip("暂停面板控制器。")]
    [SerializeField] private GamePausePanelController pausePanelController;
    [Tooltip("游戏内自定义指针控制器，用于同步弹夹等战斗信息。")]
    [SerializeField] private GameCursorController cursorController;

    private bool _panelEventsBound;

    /// <summary>
    /// 请求选择升级强化选项。
    /// </summary>
    public event Action<int> LevelUpOfferSelected;

    /// <summary>
    /// 请求刷新升级强化选项。
    /// </summary>
    public event Action LevelUpRefreshRequested;

    /// <summary>
    /// 请求选择武器强化选项。
    /// </summary>
    public event Action<int> WeaponUpgradeOfferSelected;

    /// <summary>
    /// 请求购买商店商品。
    /// </summary>
    public event Action<int> ShopOfferBuyRequested;

    /// <summary>
    /// 请求切换商店商品锁定状态。
    /// </summary>
    public event Action<int> ShopOfferLockRequested;

    /// <summary>
    /// 请求刷新商店。
    /// </summary>
    public event Action ShopRefreshRequested;

    /// <summary>
    /// 请求进入下一层。
    /// </summary>
    public event Action NextFloorRequested;

    /// <summary>
    /// 请求恢复游戏。
    /// </summary>
    public event Action ResumeRequested;

    /// <summary>
    /// 请求重新开始本局。
    /// </summary>
    public event Action RestartRequested;

    private void Awake()
    {
        if (gameStateManager == null)
            gameStateManager = FindObjectOfType<GameStateManager>();

        if (gameStateManager == null)
            Debug.LogError("[GameUI] 缺少 GameStateManager，战斗 UI 无法处理流程按钮。");

        ResolvePanelControllers();
        BindPanelEvents();
    }

    private void OnDestroy()
    {
        UnbindPanelEvents();
    }

    /// <summary>
    /// 刷新 HUD 的基础运行时显示。
    /// </summary>
    /// <param name="level">玩家等级。</param>
    /// <param name="gold">当前金币。</param>
    /// <param name="currentHealth">当前生命。</param>
    /// <param name="maxHealth">最大生命。</param>
    /// <param name="experienceRate">经验进度，范围 0 到 1。</param>
    /// <param name="floorText">楼层与倒计时文本。</param>
    /// <param name="kills">击杀数量。</param>
    /// <param name="score">当前得分。</param>
    /// <param name="elapsedSeconds">本局经过秒数。</param>
    public void RefreshHud(
        int level,
        int gold,
        int currentHealth,
        int maxHealth,
        float experienceRate,
        string floorText,
        int kills,
        int score,
        int elapsedSeconds)
    {
        RefreshHud(level, gold, currentHealth, maxHealth, null, experienceRate, floorText, kills, score, elapsedSeconds, 1f, 1f, 0, 0, false, 1f);
    }

    /// <summary>
    /// 刷新 HUD 的基础运行时显示，并包含临时特殊血显示。
    /// </summary>
    /// <param name="level">玩家等级。</param>
    /// <param name="gold">当前金币。</param>
    /// <param name="currentHealth">当前红血。</param>
    /// <param name="maxHealth">最大红血。</param>
    /// <param name="specialHealthSegments">临时特殊血量段。</param>
    /// <param name="experienceRate">经验进度，范围 0 到 1。</param>
    /// <param name="floorText">楼层与倒计时文本。</param>
    /// <param name="kills">击杀数量。</param>
    /// <param name="score">当前得分。</param>
    /// <param name="elapsedSeconds">本局经过秒数。</param>
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
        int elapsedSeconds)
    {
        RefreshHud(level, gold, currentHealth, maxHealth, specialHealthSegments, experienceRate, floorText, kills, score, elapsedSeconds, 1f, 1f, 0, 0, false, 1f);
    }

    /// <summary>
    /// 刷新 HUD 的基础运行时显示和武器资源显示。
    /// </summary>
    /// <param name="level">玩家等级。</param>
    /// <param name="gold">当前金币。</param>
    /// <param name="currentHealth">当前生命。</param>
    /// <param name="maxHealth">最大生命。</param>
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
        RefreshHud(level, gold, currentHealth, maxHealth, null, experienceRate, floorText, kills, score, elapsedSeconds, ammoRate, energyRate, currentAmmo, maxAmmo, isReloading, 1f);
    }

    /// <summary>
    /// 刷新 HUD 的基础运行时显示和武器资源显示，并同步当前武器换弹动画速度。
    /// </summary>
    /// <param name="level">玩家等级。</param>
    /// <param name="gold">当前金币。</param>
    /// <param name="currentHealth">当前生命。</param>
    /// <param name="maxHealth">最大生命。</param>
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
    /// <param name="reloadDuration">当前武器换弹总时长。</param>
    public void RefreshHud(
        int level,
        int gold,
        int currentHealth,
        int maxHealth,
        float experienceRate,
        string floorText,
        int kills,
        int score,
        int elapsedSeconds,
        float ammoRate,
        float energyRate,
        int currentAmmo,
        int maxAmmo,
        bool isReloading,
        float reloadDuration)
    {
        RefreshHud(level, gold, currentHealth, maxHealth, null, experienceRate, floorText, kills, score, elapsedSeconds, ammoRate, energyRate, currentAmmo, maxAmmo, isReloading, reloadDuration);
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
        RefreshHud(level, gold, currentHealth, maxHealth, specialHealthSegments, experienceRate, floorText, kills, score, elapsedSeconds, ammoRate, energyRate, currentAmmo, maxAmmo, isReloading, 1f);
    }

    /// <summary>
    /// 刷新 HUD 的基础运行时显示、临时特殊血显示和武器资源显示，并同步当前武器换弹动画速度。
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
    /// <param name="reloadDuration">当前武器换弹总时长。</param>
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
        bool isReloading,
        float reloadDuration)
    {
        if (hudPanelController != null)
        {
            hudPanelController.RefreshHud(
                level,
                gold,
                currentHealth,
                maxHealth,
                specialHealthSegments,
                experienceRate,
                floorText,
                kills,
                score,
                elapsedSeconds,
                ammoRate,
                energyRate,
                currentAmmo,
                maxAmmo,
                isReloading);
        }

        RefreshCursorWeaponState(currentAmmo, maxAmmo, isReloading, reloadDuration);
    }

    /// <summary>
    /// 刷新角色下方蓄力进度条。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    /// <param name="progress">蓄力进度。</param>
    public void RefreshChargeProgress(bool visible, float progress)
    {
        if (hudPanelController != null)
            hudPanelController.RefreshChargeProgress(visible, progress);
    }

    /// <summary>
    /// 切换弹夹、能量和准星资源等武器 HUD 的显示状态。
    /// </summary>
    /// <param name="visible">是否显示武器资源 HUD。</param>
    public void SetWeaponResourceHudVisible(bool visible)
    {
        if (hudPanelController != null)
            hudPanelController.SetWeaponResourceVisible(visible);

        if (cursorController == null)
            ResolvePanelControllers();

        if (cursorController != null)
            cursorController.SetWeaponResourceVisible(visible);
    }

    /// <summary>
    /// 播放游戏内指针的统一反馈动画。
    /// </summary>
    public void PlayCursorPulse()
    {
        if (cursorController == null)
            ResolvePanelControllers();

        if (cursorController != null)
            cursorController.PlayCursorPulse();
    }

    /// <summary>
    /// 播放游戏内指针的实际开火反馈动画。
    /// </summary>
    /// <param name="attackInterval">当前实时开火间隔。</param>
    public void PlayCursorFirePulse(float attackInterval)
    {
        if (cursorController == null)
            ResolvePanelControllers();

        if (cursorController != null)
            cursorController.PlayFirePulse(attackInterval);
    }

    /// <summary>
    /// 刷新升级强化选项。
    /// </summary>
    /// <param name="remainingChoices">剩余选择次数。</param>
    /// <param name="names">选项名称列表。</param>
    /// <param name="descriptions">选项描述列表。</param>
    public void RefreshLevelUp(int remainingChoices, string[] names, string[] descriptions)
    {
        RefreshLevelUp(remainingChoices, 0, names, descriptions, null);
    }

    /// <summary>
    /// 刷新升级强化选项，并显示剩余刷新次数和稀有度颜色。
    /// </summary>
    /// <param name="remainingChoices">剩余选择次数。</param>
    /// <param name="remainingRefreshes">剩余刷新次数。</param>
    /// <param name="names">选项名称列表。</param>
    /// <param name="descriptions">选项描述列表。</param>
    /// <param name="colors">选项背景颜色列表。</param>
    public void RefreshLevelUp(int remainingChoices, int remainingRefreshes, string[] names, string[] descriptions, Color[] colors)
    {
        RefreshLevelUp(remainingChoices, remainingRefreshes, names, descriptions, colors, null);
    }

    /// <summary>
    /// 刷新升级强化选项，并显示当前属性摘要。
    /// </summary>
    /// <param name="remainingChoices">剩余选择次数。</param>
    /// <param name="remainingRefreshes">剩余刷新次数。</param>
    /// <param name="names">选项名称列表。</param>
    /// <param name="descriptions">选项描述列表。</param>
    /// <param name="colors">选项背景颜色列表。</param>
    /// <param name="currentStats">当前属性摘要文本。</param>
    public void RefreshLevelUp(int remainingChoices, int remainingRefreshes, string[] names, string[] descriptions, Color[] colors, string currentStats)
    {
        if (levelUpPanelController != null)
            levelUpPanelController.Refresh(remainingChoices, remainingRefreshes, names, descriptions, colors, currentStats);
    }

    /// <summary>
    /// 通知外部刷新升级强化选项。
    /// </summary>
    public void RefreshLevelUpOffers()
    {
        LevelUpRefreshRequested?.Invoke();
    }

    /// <summary>
    /// 刷新武器强化选项。
    /// </summary>
    /// <param name="currentWeaponName">当前武器名称。</param>
    /// <param name="remainingChoices">剩余选择次数。</param>
    /// <param name="names">选项名称列表。</param>
    /// <param name="descriptions">选项描述列表。</param>
    public void RefreshWeaponUpgrade(string currentWeaponName, int remainingChoices, string[] names, string[] descriptions)
    {
        RefreshWeaponUpgrade(currentWeaponName, remainingChoices, names, descriptions, null);
    }

    /// <summary>
    /// 刷新武器强化选项，并显示当前属性摘要。
    /// </summary>
    /// <param name="currentWeaponName">当前武器名称。</param>
    /// <param name="remainingChoices">剩余选择次数。</param>
    /// <param name="names">选项名称列表。</param>
    /// <param name="descriptions">选项描述列表。</param>
    /// <param name="currentStats">当前属性摘要文本。</param>
    public void RefreshWeaponUpgrade(string currentWeaponName, int remainingChoices, string[] names, string[] descriptions, string currentStats)
    {
        if (weaponUpgradePanelController != null)
            weaponUpgradePanelController.Refresh(currentWeaponName, remainingChoices, names, descriptions, currentStats);
    }

    /// <summary>
    /// 刷新商店页面。
    /// </summary>
    /// <param name="title">商店标题。</param>
    /// <param name="gold">当前金币。</param>
    /// <param name="refreshCount">剩余刷新次数。</param>
    /// <param name="nextText">下一步按钮文本。</param>
    /// <param name="names">商品名称列表。</param>
    /// <param name="descriptions">商品描述列表。</param>
    /// <param name="costs">商品价格列表。</param>
    /// <param name="lockedStates">商品锁定状态列表。</param>
    /// <param name="purchasedStates">商品购买状态列表。</param>
    /// <param name="colors">商品评级颜色列表。</param>
    public void RefreshShop(
        string title,
        int gold,
        int refreshCount,
        string nextText,
        string[] names,
        string[] descriptions,
        int[] costs,
        bool[] lockedStates,
        bool[] purchasedStates,
        Color[] colors)
    {
        if (shopPanelController != null)
            shopPanelController.Refresh(title, gold, refreshCount, nextText, names, descriptions, costs, lockedStates, purchasedStates, colors);
    }

    /// <summary>
    /// 刷新结算页面。
    /// </summary>
    /// <param name="isVictory">是否胜利。</param>
    /// <param name="characterName">角色名称。</param>
    /// <param name="weaponName">武器名称。</param>
    /// <param name="statsText">统计文本。</param>
    public void RefreshResult(bool isVictory, string characterName, string weaponName, string statsText)
    {
        if (resultPanelController != null)
            resultPanelController.Refresh(isVictory, characterName, weaponName, statsText);
    }

    /// <summary>
    /// 刷新结算页面的分组统计文本。
    /// </summary>
    /// <param name="isVictory">是否胜利。</param>
    /// <param name="characterName">角色名称。</param>
    /// <param name="weaponName">武器名称。</param>
    /// <param name="summaryText">本局统计文本。</param>
    /// <param name="characterStatsText">角色属性文本。</param>
    /// <param name="weaponStatsText">武器属性文本。</param>
    public void RefreshResult(
        bool isVictory,
        string characterName,
        string weaponName,
        string summaryText,
        string characterStatsText,
        string weaponStatsText)
    {
        if (resultPanelController != null)
            resultPanelController.Refresh(isVictory, characterName, weaponName, summaryText, characterStatsText, weaponStatsText);
    }

    /// <summary>
    /// 刷新暂停页面。
    /// </summary>
    /// <param name="statsText">属性统计文本。</param>
    /// <param name="weaponName">当前武器名称。</param>
    /// <param name="itemsText">道具文本。</param>
    public void RefreshPause(string statsText, string weaponName, string itemsText)
    {
        if (pausePanelController != null)
            pausePanelController.Refresh(statsText, weaponName, itemsText);
    }

    /// <summary>
    /// 请求从暂停状态恢复游戏，未接入战斗控制器时直接恢复状态机。
    /// </summary>
    public void ResumeGame()
    {
        if (ResumeRequested != null)
        {
            ResumeRequested.Invoke();
            return;
        }

        if (gameStateManager != null)
            gameStateManager.Resume();
    }

    /// <summary>
    /// 请求重新开始本局，未接入战斗控制器时仅输出警告。
    /// </summary>
    public void RestartGame()
    {
        if (RestartRequested != null)
        {
            RestartRequested.Invoke();
            return;
        }

        Debug.LogWarning("[GameUI] 重新开始按钮尚未接入战斗控制器。");
    }

    /// <summary>
    /// 请求返回标题界面。
    /// </summary>
    public void ReturnToTitle()
    {
        if (gameStateManager != null)
            gameStateManager.ReturnToTitle();
    }

    /// <summary>
    /// 输出退出游戏日志，后续接入项目级退出流程。
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("[GameUI] 退出游戏。");
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 查找挂在各 UI 面板上的子控制器。
    /// </summary>
    private void ResolvePanelControllers()
    {
        if (hudPanelController == null)
            hudPanelController = GetComponentInChildren<GameHudPanelController>(true);

        if (levelUpPanelController == null)
            levelUpPanelController = GetComponentInChildren<GameLevelUpPanelController>(true);

        if (weaponUpgradePanelController == null)
            weaponUpgradePanelController = GetComponentInChildren<GameWeaponUpgradePanelController>(true);

        if (shopPanelController == null)
            shopPanelController = GetComponentInChildren<GameShopPanelController>(true);

        if (resultPanelController == null)
            resultPanelController = GetComponentInChildren<GameResultPanelController>(true);

        if (pausePanelController == null)
            pausePanelController = GetComponentInChildren<GamePausePanelController>(true);

        if (cursorController == null)
            cursorController = GetComponentInChildren<GameCursorController>(true);

        if (cursorController == null)
            cursorController = FindObjectOfType<GameCursorController>(true);
    }

    /// <summary>
    /// 刷新游戏内指针的武器资源状态，引用缺失时尝试从场景中恢复。
    /// </summary>
    /// <param name="currentAmmo">当前弹药。</param>
    /// <param name="maxAmmo">最大弹药。</param>
    /// <param name="isReloading">是否正在换弹。</param>
    /// <param name="reloadDuration">当前武器换弹总时长。</param>
    private void RefreshCursorWeaponState(int currentAmmo, int maxAmmo, bool isReloading, float reloadDuration)
    {
        if (cursorController == null)
            ResolvePanelControllers();

        if (cursorController == null)
            return;

        cursorController.RefreshAmmo(currentAmmo, maxAmmo);
        cursorController.RefreshReloadState(isReloading, reloadDuration);
    }

    /// <summary>
    /// 绑定子面板事件，由门面继续对外转发旧事件。
    /// </summary>
    private void BindPanelEvents()
    {
        if (_panelEventsBound)
            return;

        if (levelUpPanelController != null)
        {
            levelUpPanelController.OfferSelected += NotifyLevelUpOfferSelected;
            levelUpPanelController.RefreshRequested += NotifyLevelUpRefreshRequested;
        }

        if (weaponUpgradePanelController != null)
            weaponUpgradePanelController.OfferSelected += NotifyWeaponUpgradeOfferSelected;

        if (shopPanelController != null)
        {
            shopPanelController.OfferBuyRequested += NotifyShopOfferBuyRequested;
            shopPanelController.OfferLockRequested += NotifyShopOfferLockRequested;
            shopPanelController.RefreshRequested += NotifyShopRefreshRequested;
            shopPanelController.NextFloorRequested += NotifyNextFloorRequested;
        }

        if (resultPanelController != null)
        {
            resultPanelController.RestartRequested += NotifyRestartRequested;
            resultPanelController.ReturnTitleRequested += ReturnToTitle;
        }

        if (pausePanelController != null)
        {
            pausePanelController.ResumeRequested += NotifyResumeRequested;
            pausePanelController.RestartRequested += NotifyRestartRequested;
        }

        _panelEventsBound = true;
    }

    /// <summary>
    /// 解绑子面板事件，避免对象销毁后残留委托。
    /// </summary>
    private void UnbindPanelEvents()
    {
        if (!_panelEventsBound)
            return;

        if (levelUpPanelController != null)
        {
            levelUpPanelController.OfferSelected -= NotifyLevelUpOfferSelected;
            levelUpPanelController.RefreshRequested -= NotifyLevelUpRefreshRequested;
        }

        if (weaponUpgradePanelController != null)
            weaponUpgradePanelController.OfferSelected -= NotifyWeaponUpgradeOfferSelected;

        if (shopPanelController != null)
        {
            shopPanelController.OfferBuyRequested -= NotifyShopOfferBuyRequested;
            shopPanelController.OfferLockRequested -= NotifyShopOfferLockRequested;
            shopPanelController.RefreshRequested -= NotifyShopRefreshRequested;
            shopPanelController.NextFloorRequested -= NotifyNextFloorRequested;
        }

        if (resultPanelController != null)
        {
            resultPanelController.RestartRequested -= NotifyRestartRequested;
            resultPanelController.ReturnTitleRequested -= ReturnToTitle;
        }

        if (pausePanelController != null)
        {
            pausePanelController.ResumeRequested -= NotifyResumeRequested;
            pausePanelController.RestartRequested -= NotifyRestartRequested;
        }

        _panelEventsBound = false;
    }

    /// <summary>
    /// 通知外部选择升级强化选项。
    /// </summary>
    /// <param name="index">选项索引。</param>
    private void NotifyLevelUpOfferSelected(int index)
    {
        LevelUpOfferSelected?.Invoke(index);
    }

    /// <summary>
    /// 通知外部刷新升级强化选项。
    /// </summary>
    private void NotifyLevelUpRefreshRequested()
    {
        LevelUpRefreshRequested?.Invoke();
    }

    /// <summary>
    /// 通知外部选择武器强化选项。
    /// </summary>
    /// <param name="index">选项索引。</param>
    private void NotifyWeaponUpgradeOfferSelected(int index)
    {
        WeaponUpgradeOfferSelected?.Invoke(index);
    }

    /// <summary>
    /// 通知外部购买商店商品。
    /// </summary>
    /// <param name="index">商品索引。</param>
    private void NotifyShopOfferBuyRequested(int index)
    {
        ShopOfferBuyRequested?.Invoke(index);
    }

    /// <summary>
    /// 通知外部切换商品锁定。
    /// </summary>
    /// <param name="index">商品索引。</param>
    private void NotifyShopOfferLockRequested(int index)
    {
        ShopOfferLockRequested?.Invoke(index);
    }

    /// <summary>
    /// 通知外部刷新商店。
    /// </summary>
    private void NotifyShopRefreshRequested()
    {
        ShopRefreshRequested?.Invoke();
    }

    /// <summary>
    /// 通知外部进入下一层。
    /// </summary>
    private void NotifyNextFloorRequested()
    {
        NextFloorRequested?.Invoke();
    }

    /// <summary>
    /// 通知外部恢复游戏。
    /// </summary>
    private void NotifyResumeRequested()
    {
        ResumeRequested?.Invoke();
    }

    /// <summary>
    /// 通知外部重新开始本局。
    /// </summary>
    private void NotifyRestartRequested()
    {
        RestartRequested?.Invoke();
    }
}
