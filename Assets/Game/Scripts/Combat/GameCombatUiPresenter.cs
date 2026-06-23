using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责读取战斗运行时数据并刷新战斗 Canvas 显示。
/// </summary>
public sealed class GameCombatUiPresenter : MonoBehaviour
{
    /// <summary>
    /// 战斗 UI 刷新所需的只读运行时数据。
    /// </summary>
    public struct CombatUiContext
    {
        public GameCombatCanvasController CanvasController;
        public GameStateManager StateManager;
        public GamePlayerController Player;
        public GameFloorProgressController FloorProgressController;
        public StageRunState StageRunState;
        public GameWeaponFireController WeaponFireController;
        public GameUpgradeController UpgradeController;
        public GameWeaponUpgradeController WeaponUpgradeController;
        public GameShopController ShopController;
        public IReadOnlyList<string> OwnedItems;
        public int Kills;
        public int TotalKills;
        public int Score;
        public int TotalGoldCollected;
        public int MagazineCapacity;
        public int ShopRefreshCount;
        public int WeaponInstanceCount;
        public int ProjectileCount;
        public int ProjectilePierce;
        public int ProjectileBounce;
        public float ElapsedTime;
        public float MaxEnergy;
        public float ChargeDuration;
        public float ReloadDuration;
        public float AttackSpeedMultiplier;
        public float ProjectileSpeedMultiplier;
        public float CriticalRate;
        public float CriticalDamage;
        public string CharacterName;
        public string WeaponName;
    }

    [Header("UI 引用")]
    [Tooltip("战斗、升级、商店、暂停和结算界面控制器。")]
    [SerializeField] private GameCombatCanvasController combatCanvasController;

    private void Awake()
    {
        if (combatCanvasController == null)
            combatCanvasController = FindObjectOfType<GameCombatCanvasController>();
    }

    /// <summary>
    /// 注入战斗 Canvas 依赖。
    /// </summary>
    /// <param name="canvasController">战斗 Canvas 控制器。</param>
    public void Initialize(GameCombatCanvasController canvasController)
    {
        if (combatCanvasController == null)
            combatCanvasController = canvasController;
    }

    /// <summary>
    /// 根据当前流程状态刷新需要持续更新的 UI。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    public void RefreshByState(CombatUiContext context)
    {
        if (!IsContextReady(context) || context.StateManager == null)
            return;

        if (context.StateManager.CurrentState == GameStateManager.GameState.Playing)
            RefreshHud(context);
    }

    /// <summary>
    /// 刷新战斗 HUD。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    public void RefreshHud(CombatUiContext context)
    {
        if (!IsContextReady(context))
            return;

        float expRate = Mathf.Clamp01((float)context.Player.Experience / context.Player.GetRequiredExperience());
        string floorText = context.FloorProgressController != null
            ? context.FloorProgressController.GetHudFloorText()
            : "1/1  倒计时 0s";
        int currentAmmo = context.WeaponFireController != null ? context.WeaponFireController.CurrentAmmo : 0;
        float currentEnergy = context.WeaponFireController != null ? context.WeaponFireController.CurrentEnergy : 0f;
        bool isReloading = context.WeaponFireController != null && context.WeaponFireController.IsReloading;
        float reloadDuration = Mathf.Max(0.01f, context.ReloadDuration);
        GetCanvas(context).RefreshHud(
            context.Player.Level,
            context.Player.Gold,
            context.Player.CurrentHealth,
            context.Player.MaxHealth,
            context.Player.GetSpecialHealthSegments(),
            expRate,
            floorText,
            context.TotalKills,
            context.Score,
            Mathf.FloorToInt(context.ElapsedTime),
            (float)currentAmmo / Mathf.Max(1, context.MagazineCapacity),
            currentEnergy / Mathf.Max(0.01f, context.MaxEnergy),
            currentAmmo,
            context.MagazineCapacity,
            isReloading,
            reloadDuration);
        bool isChargingSkill = context.WeaponFireController != null && context.WeaponFireController.IsChargingSkill;
        float holdTimer = context.WeaponFireController != null ? context.WeaponFireController.RightMouseHoldTimer : 0f;
        float chargeProgress = holdTimer / Mathf.Max(0.01f, context.ChargeDuration);
        RefreshPlayerActionRing(context.Player, isChargingSkill, chargeProgress);
        GetCanvas(context).RefreshChargeProgress(false, 0f);
    }

    /// <summary>
    /// 刷新升级强化界面。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    public void RefreshLevelUp(CombatUiContext context)
    {
        if (!IsContextReady(context))
            return;

        GetCanvas(context).RefreshLevelUp(
            context.Player.PendingUpgradeChoices,
            context.Player.UpgradeRefreshCount,
            context.UpgradeController != null ? context.UpgradeController.GetOfferNames() : null,
            context.UpgradeController != null ? context.UpgradeController.GetOfferDescriptions() : null,
            context.UpgradeController != null ? context.UpgradeController.GetOfferColors() : null,
            BuildAmplifiedStatsText(context));
    }

    /// <summary>
    /// 刷新武器强化界面。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    public void RefreshWeaponUpgrade(CombatUiContext context)
    {
        if (!IsContextReady(context))
            return;

        GetCanvas(context).RefreshWeaponUpgrade(
            context.WeaponName,
            context.Player.PendingWeaponUpgradeChoices,
            context.WeaponUpgradeController != null ? context.WeaponUpgradeController.GetOfferNames() : null,
            context.WeaponUpgradeController != null ? context.WeaponUpgradeController.GetOfferDescriptions() : null,
            BuildAmplifiedStatsText(context));
    }

    /// <summary>
    /// 刷新商店界面。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    public void RefreshShop(CombatUiContext context)
    {
        if (!IsContextReady(context))
            return;

        string nextText = context.FloorProgressController != null
            ? context.FloorProgressController.GetShopNextButtonText()
            : "完成挑战";
        GetCanvas(context).RefreshShop(
            context.FloorProgressController != null ? context.FloorProgressController.GetShopTitleText() : "商店  楼层 1/1",
            context.Player.Gold,
            context.ShopRefreshCount,
            nextText,
            context.ShopController != null ? context.ShopController.GetOfferNames() : null,
            context.ShopController != null ? context.ShopController.GetOfferDescriptions() : null,
            context.ShopController != null ? context.ShopController.GetOfferCosts() : null,
            context.ShopController != null ? context.ShopController.GetOfferLockedStates() : null,
            context.ShopController != null ? context.ShopController.GetOfferPurchasedStates() : null,
            context.ShopController != null ? context.ShopController.GetOfferColors() : null);
    }

    /// <summary>
    /// 刷新结算界面。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    public void RefreshResult(CombatUiContext context)
    {
        if (!IsContextReady(context) || context.StateManager == null)
            return;

        bool isVictory = context.StateManager.CurrentState == GameStateManager.GameState.Victory;
        GetCanvas(context).RefreshResult(
            isVictory,
            context.CharacterName,
            context.WeaponName,
            BuildResultSummaryText(context),
            BuildResultCharacterStatsText(context),
            BuildResultWeaponStatsText(context));
    }

    /// <summary>
    /// 刷新暂停界面。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    public void RefreshPause(CombatUiContext context)
    {
        if (!IsContextReady(context))
            return;

        GetCanvas(context).RefreshPause(
            BuildPauseStatsText(context),
            context.WeaponName,
            FormatOwnedItems(context.OwnedItems));
    }

    /// <summary>
    /// 获取本次刷新使用的 Canvas 控制器。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    /// <returns>Canvas 控制器。</returns>
    private GameCombatCanvasController GetCanvas(CombatUiContext context)
    {
        return context.CanvasController != null ? context.CanvasController : combatCanvasController;
    }

    /// <summary>
    /// 刷新玩家身上的动作环技能蓄力显示。
    /// </summary>
    /// <param name="player">玩家控制器。</param>
    /// <param name="visible">是否显示技能蓄力环。</param>
    /// <param name="progress">技能蓄力进度。</param>
    private void RefreshPlayerActionRing(GamePlayerController player, bool visible, float progress)
    {
        if (player == null)
            return;

        GamePlayerActionRingController actionRingController = player.GetComponentInChildren<GamePlayerActionRingController>(true);
        if (actionRingController != null)
            actionRingController.RefreshSkillCharge(visible, progress);
    }

    /// <summary>
    /// 检查刷新 UI 所需的基础对象是否存在。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    /// <returns>是否可以刷新。</returns>
    private bool IsContextReady(CombatUiContext context)
    {
        return GetCanvas(context) != null && context.Player != null;
    }

    /// <summary>
    /// 格式化本局已经获得的所有道具名称。
    /// </summary>
    /// <param name="ownedItems">已获得道具名称列表。</param>
    /// <returns>道具显示文本。</returns>
    private string FormatOwnedItems(IReadOnlyList<string> ownedItems)
    {
        if (ownedItems == null || ownedItems.Count <= 0)
            return "暂无";

        return string.Join("、", ownedItems);
    }

    /// <summary>
    /// 构建结算页面的本局统计文本。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    /// <returns>本局统计文本。</returns>
    private string BuildResultSummaryText(CombatUiContext context)
    {
        return
            "本局统计\n" +
            $"击杀怪物：{context.TotalKills}\n" +
            $"最终得分：{context.Score}\n" +
            BuildStageScoreSummaryText(context) +
            $"游玩时长：{Mathf.FloorToInt(context.ElapsedTime)} 秒\n" +
            $"达到楼层：{(context.FloorProgressController != null ? context.FloorProgressController.GetResultFloorProgressText() : "1/1")}\n" +
            $"获得金币：{context.TotalGoldCollected}\n" +
            $"道具：{FormatOwnedItems(context.OwnedItems)}";
    }

    /// <summary>
    /// 构建 Stage 模式的结算分项文本。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    /// <returns>Stage 分项文本。</returns>
    private string BuildStageScoreSummaryText(CombatUiContext context)
    {
        StageRunState stageRunState = context.StageRunState;
        if (stageRunState == null || string.IsNullOrEmpty(stageRunState.CurrentStageId))
            return string.Empty;

        return
            $"击杀分：{stageRunState.KillScore}\n" +
            $"清场奖励：{stageRunState.ClearBonusScore}\n" +
            $"速度奖励：{stageRunState.SpeedBonusScore}\n" +
            $"无伤奖励：{stageRunState.NoDamageBonusScore}\n" +
            $"Boss 分：{stageRunState.BossScore}\n" +
            $"清场房间：{stageRunState.ClearedRoomCount}\n" +
            $"总受伤次数：{stageRunState.TotalDamageTakenCount}\n" +
            $"Boss 用时：{stageRunState.BossKillDuration:F1} 秒\n" +
            $"Boss 召唤物击杀：{stageRunState.BossSummonKillCount}\n";
    }

    /// <summary>
    /// 构建结算页面的角色属性文本。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    /// <returns>角色属性文本。</returns>
    private string BuildResultCharacterStatsText(CombatUiContext context)
    {
        return
            "角色状态\n" +
            $"生命：{HealthDisplayUtility.FormatHeartText(context.Player.CurrentHealth, context.Player.MaxHealth)}\n" +
            $"等级：Lv.{context.Player.Level}\n" +
            $"移速：{context.Player.MoveSpeed:F2}\n" +
            $"经验倍率：{context.Player.ExperienceMultiplier:P0}\n" +
            $"攻击力倍率：{context.Player.AttackDamageMultiplier:P0}\n" +
            $"暴击率：{context.CriticalRate:P0}\n" +
            $"暴击伤害：{context.CriticalDamage:P0}";
    }

    /// <summary>
    /// 构建结算页面的武器属性文本。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    /// <returns>武器属性文本。</returns>
    private string BuildResultWeaponStatsText(CombatUiContext context)
    {
        return
            "武器状态\n" +
            $"当前武器：{context.WeaponName}\n" +
            $"射速倍率：{context.AttackSpeedMultiplier:P0}\n" +
            $"弹速倍率：{context.ProjectileSpeedMultiplier:P0}\n" +
            $"穿透层数：{context.ProjectilePierce}\n" +
            $"反弹次数：{context.ProjectileBounce}\n" +
            $"武器数：{context.WeaponInstanceCount}\n" +
            $"弹道数：{context.ProjectileCount}";
    }

    /// <summary>
    /// 构建暂停界面属性文本。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    /// <returns>暂停属性文本。</returns>
    private string BuildPauseStatsText(CombatUiContext context)
    {
        return
            "当前属性\n" +
            BuildAmplifiedStatsText(context);
    }

    /// <summary>
    /// 构建暂停界面展示的可增幅属性文本。
    /// </summary>
    /// <param name="context">战斗 UI 数据上下文。</param>
    /// <returns>可增幅属性文本。</returns>
    private string BuildAmplifiedStatsText(CombatUiContext context)
    {
        return
            $"攻击力提升倍率：{context.Player.AttackDamageMultiplier:P0}\n" +
            $"射速倍率：{context.AttackSpeedMultiplier:P0}\n" +
            $"弹速倍率：{context.ProjectileSpeedMultiplier:P0}\n" +
            $"移速：{context.Player.MoveSpeed:F2}\n" +
            $"经验倍率：{context.Player.ExperienceMultiplier:P0}\n" +
            $"暴击率：{context.CriticalRate:P0}\n" +
            $"暴击伤害：{context.CriticalDamage:P0}\n" +
            $"穿透层数：{context.ProjectilePierce}\n" +
            $"反弹次数：{context.ProjectileBounce}\n" +
            $"武器数：{context.WeaponInstanceCount}\n" +
            $"弹道数：{context.ProjectileCount}";
    }
}
