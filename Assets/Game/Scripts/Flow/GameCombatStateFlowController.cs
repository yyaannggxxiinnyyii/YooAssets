using System;
using UnityEngine;

/// <summary>
/// 协调战斗流程状态切换、暂停恢复、重开和战斗 UI 按钮事件转发。
/// </summary>
public sealed class GameCombatStateFlowController : MonoBehaviour
{
    /// <summary>
    /// 战斗流程切换所需的回调集合。
    /// </summary>
    public struct StateFlowContext
    {
        public Func<bool> IsStarted;
        public Func<bool> HasPlayer;
        public Action<bool> SetPlayerControlEnabled;
        public Action StartCombat;
        public Action StartNextFloor;
        public Action ResetRun;
        public Action ClearCombatObjects;
        public Action ResetWeaponForPostCombatInteraction;
        public Action PrepareShop;
        public Action PrepareUpgradeChoices;
        public Action PrepareWeaponUpgradeChoices;
        public Action RefreshHud;
        public Action RefreshShop;
        public Action RefreshLevelUp;
        public Action RefreshWeaponUpgrade;
        public Action RefreshResult;
        public Action RefreshPause;
        public Action<int> ChooseUpgradeOffer;
        public Action TryRefreshUpgradeOffers;
        public Action<int> ChooseWeaponUpgradeOffer;
        public Action<int> TryBuyOffer;
        public Action<int> ToggleShopOfferLock;
        public Action TryRefreshShop;
        public Func<bool> IsFinalFloor;
    }

    [Header("流程引用")]
    [Tooltip("游戏流程状态管理器。")]
    [SerializeField] private GameStateManager gameStateManager;
    [Tooltip("战斗、升级、商店、暂停和结算界面控制器。")]
    [SerializeField] private GameCombatCanvasController combatCanvasController;

    private StateFlowContext _context;
    private bool _initialized;
    private bool _combatCanvasEventsBound;

    private void Awake()
    {
        if (gameStateManager == null)
            gameStateManager = FindObjectOfType<GameStateManager>();

        if (combatCanvasController == null)
            combatCanvasController = FindObjectOfType<GameCombatCanvasController>();
    }

    private void OnEnable()
    {
        if (gameStateManager != null)
            gameStateManager.StateChanged += OnStateChanged;

        BindCombatCanvasEvents();
    }

    private void OnDisable()
    {
        if (gameStateManager != null)
            gameStateManager.StateChanged -= OnStateChanged;

        UnbindCombatCanvasEvents();
    }

    /// <summary>
    /// 初始化状态流程控制器依赖。
    /// </summary>
    /// <param name="stateManager">游戏流程状态管理器。</param>
    /// <param name="canvasController">战斗 Canvas 控制器。</param>
    /// <param name="context">流程回调上下文。</param>
    public void Initialize(GameStateManager stateManager, GameCombatCanvasController canvasController, StateFlowContext context)
    {
        if (gameStateManager == null)
            gameStateManager = stateManager;

        if (combatCanvasController == null)
            combatCanvasController = canvasController;

        _context = context;
        _initialized = true;
        BindCombatCanvasEvents();
    }

    /// <summary>
    /// 处理 Esc 暂停输入。
    /// </summary>
    public void TogglePause()
    {
        if (gameStateManager == null || !_initialized)
            return;

        if (gameStateManager.CurrentState == GameStateManager.GameState.Playing)
        {
            gameStateManager.Pause();
            return;
        }

        if (gameStateManager.CurrentState == GameStateManager.GameState.Paused)
            ResumeFromPause();
    }

    /// <summary>
    /// 从暂停状态恢复战斗。
    /// </summary>
    public void ResumeFromPause()
    {
        if (gameStateManager == null || !_initialized || !HasPlayer())
            return;

        Time.timeScale = 1f;
        _context.SetPlayerControlEnabled?.Invoke(true);
        gameStateManager.Resume();
    }

    /// <summary>
    /// 直接推进到下一层战斗。
    /// </summary>
    public void ContinueToNextFloor()
    {
        EnterPostFloorState(GameStateManager.GameState.Playing);
    }

    /// <summary>
    /// 根据指定状态推进楼层后流程。
    /// </summary>
    /// <param name="nextState">下一个流程状态。</param>
    public void EnterPostFloorState(GameStateManager.GameState nextState)
    {
        if (gameStateManager == null || !_initialized)
            return;

        switch (nextState)
        {
            case GameStateManager.GameState.WeaponUpgrade:
                gameStateManager.EnterWeaponUpgrade();
                break;

            case GameStateManager.GameState.LevelUp:
                gameStateManager.EnterLevelUp();
                break;

            case GameStateManager.GameState.Shop:
                gameStateManager.EnterShop();
                break;

            case GameStateManager.GameState.Playing:
                if (_context.IsFinalFloor != null && _context.IsFinalFloor.Invoke())
                {
                    gameStateManager.EnterVictory();
                    break;
                }

                _context.StartNextFloor?.Invoke();
                gameStateManager.StartPlaying();
                break;

            default:
                gameStateManager.EnterVictory();
                break;
        }
    }

    /// <summary>
    /// 根据流程状态执行战斗进入、暂停、结算、商店和升级界面的切换动作。
    /// </summary>
    /// <param name="previousState">上一个状态。</param>
    /// <param name="nextState">下一个状态。</param>
    private void OnStateChanged(GameStateManager.GameState previousState, GameStateManager.GameState nextState)
    {
        if (!_initialized)
            return;

        if (nextState != GameStateManager.GameState.Playing)
        {
            HandleExitPlayingState(nextState);
            return;
        }

        Time.timeScale = 1f;
        if (!IsStarted())
        {
            _context.StartCombat?.Invoke();
            _context.RefreshHud?.Invoke();
        }
        else if (previousState == GameStateManager.GameState.Paused && HasPlayer())
        {
            _context.SetPlayerControlEnabled?.Invoke(true);
            _context.RefreshHud?.Invoke();
        }
    }

    /// <summary>
    /// 处理离开战斗状态后的目标状态。
    /// </summary>
    /// <param name="nextState">目标状态。</param>
    private void HandleExitPlayingState(GameStateManager.GameState nextState)
    {
        if (nextState == GameStateManager.GameState.Title)
        {
            Time.timeScale = 1f;
            _context.ResetRun?.Invoke();
            return;
        }

        if (!HasPlayer())
            return;

        if (nextState == GameStateManager.GameState.Shop)
        {
            Time.timeScale = 1f;
            ResetWeaponForPostCombatInteraction();
            _context.SetPlayerControlEnabled?.Invoke(false);
            _context.ClearCombatObjects?.Invoke();
            _context.PrepareShop?.Invoke();
            _context.RefreshShop?.Invoke();
        }
        else if (nextState == GameStateManager.GameState.LevelUp)
        {
            Time.timeScale = 1f;
            ResetWeaponForPostCombatInteraction();
            _context.SetPlayerControlEnabled?.Invoke(false);
            _context.ClearCombatObjects?.Invoke();
            _context.PrepareUpgradeChoices?.Invoke();
            _context.RefreshLevelUp?.Invoke();
        }
        else if (nextState == GameStateManager.GameState.WeaponUpgrade)
        {
            Time.timeScale = 1f;
            ResetWeaponForPostCombatInteraction();
            _context.SetPlayerControlEnabled?.Invoke(false);
            _context.ClearCombatObjects?.Invoke();
            _context.PrepareWeaponUpgradeChoices?.Invoke();
            _context.RefreshWeaponUpgrade?.Invoke();
        }
        else if (nextState == GameStateManager.GameState.GameOver ||
                 nextState == GameStateManager.GameState.Victory)
        {
            Time.timeScale = 1f;
            ResetWeaponForPostCombatInteraction();
            _context.SetPlayerControlEnabled?.Invoke(false);
            _context.ClearCombatObjects?.Invoke();
            _context.RefreshResult?.Invoke();
        }
        else if (nextState == GameStateManager.GameState.Paused)
        {
            _context.SetPlayerControlEnabled?.Invoke(false);
            Time.timeScale = 0f;
            _context.RefreshPause?.Invoke();
        }
    }

    /// <summary>
    /// 绑定 Canvas UI 事件，避免 UI 按钮直接依赖战斗内部实现。
    /// </summary>
    private void BindCombatCanvasEvents()
    {
        if (_combatCanvasEventsBound || combatCanvasController == null)
            return;

        combatCanvasController.LevelUpOfferSelected += OnCanvasLevelUpOfferSelected;
        combatCanvasController.LevelUpRefreshRequested += OnCanvasLevelUpRefreshRequested;
        combatCanvasController.WeaponUpgradeOfferSelected += OnCanvasWeaponUpgradeOfferSelected;
        combatCanvasController.ShopOfferBuyRequested += OnCanvasShopOfferBuyRequested;
        combatCanvasController.ShopOfferLockRequested += OnCanvasShopOfferLockRequested;
        combatCanvasController.ShopRefreshRequested += OnCanvasShopRefreshRequested;
        combatCanvasController.NextFloorRequested += OnCanvasNextFloorRequested;
        combatCanvasController.ResumeRequested += OnCanvasResumeRequested;
        combatCanvasController.RestartRequested += OnCanvasRestartRequested;
        _combatCanvasEventsBound = true;
    }

    /// <summary>
    /// 解绑 Canvas UI 事件，避免对象禁用后残留委托。
    /// </summary>
    private void UnbindCombatCanvasEvents()
    {
        if (!_combatCanvasEventsBound || combatCanvasController == null)
            return;

        combatCanvasController.LevelUpOfferSelected -= OnCanvasLevelUpOfferSelected;
        combatCanvasController.LevelUpRefreshRequested -= OnCanvasLevelUpRefreshRequested;
        combatCanvasController.WeaponUpgradeOfferSelected -= OnCanvasWeaponUpgradeOfferSelected;
        combatCanvasController.ShopOfferBuyRequested -= OnCanvasShopOfferBuyRequested;
        combatCanvasController.ShopOfferLockRequested -= OnCanvasShopOfferLockRequested;
        combatCanvasController.ShopRefreshRequested -= OnCanvasShopRefreshRequested;
        combatCanvasController.NextFloorRequested -= OnCanvasNextFloorRequested;
        combatCanvasController.ResumeRequested -= OnCanvasResumeRequested;
        combatCanvasController.RestartRequested -= OnCanvasRestartRequested;
        _combatCanvasEventsBound = false;
    }

    /// <summary>
    /// 处理 Canvas 升级强化选择。
    /// </summary>
    /// <param name="index">选项索引。</param>
    private void OnCanvasLevelUpOfferSelected(int index)
    {
        _context.ChooseUpgradeOffer?.Invoke(index);
    }

    /// <summary>
    /// 处理 Canvas 升级强化刷新请求。
    /// </summary>
    private void OnCanvasLevelUpRefreshRequested()
    {
        _context.TryRefreshUpgradeOffers?.Invoke();
        _context.RefreshLevelUp?.Invoke();
    }

    /// <summary>
    /// 处理 Canvas 武器强化选择。
    /// </summary>
    /// <param name="index">选项索引。</param>
    private void OnCanvasWeaponUpgradeOfferSelected(int index)
    {
        _context.ChooseWeaponUpgradeOffer?.Invoke(index);
    }

    /// <summary>
    /// 处理 Canvas 商店购买请求。
    /// </summary>
    /// <param name="index">商品索引。</param>
    private void OnCanvasShopOfferBuyRequested(int index)
    {
        _context.TryBuyOffer?.Invoke(index);
        _context.RefreshShop?.Invoke();
    }

    /// <summary>
    /// 处理 Canvas 商店锁定请求。
    /// </summary>
    /// <param name="index">商品索引。</param>
    private void OnCanvasShopOfferLockRequested(int index)
    {
        _context.ToggleShopOfferLock?.Invoke(index);
        _context.RefreshShop?.Invoke();
    }

    /// <summary>
    /// 处理 Canvas 商店刷新请求。
    /// </summary>
    private void OnCanvasShopRefreshRequested()
    {
        _context.TryRefreshShop?.Invoke();
        _context.RefreshShop?.Invoke();
    }

    /// <summary>
    /// 处理 Canvas 进入下一层请求。
    /// </summary>
    private void OnCanvasNextFloorRequested()
    {
        ContinueToNextFloor();
    }

    /// <summary>
    /// 处理 Canvas 恢复游戏请求。
    /// </summary>
    private void OnCanvasResumeRequested()
    {
        ResumeFromPause();
    }

    /// <summary>
    /// 处理 Canvas 重新开始请求。
    /// </summary>
    private void OnCanvasRestartRequested()
    {
        RestartRun();
    }

    /// <summary>
    /// 重新开始本局游戏。
    /// </summary>
    private void RestartRun()
    {
        if (gameStateManager == null)
            return;

        Time.timeScale = 1f;
        _context.ResetRun?.Invoke();
        gameStateManager.StartPlaying();
    }

    /// <summary>
    /// 检查战斗是否已经开始。
    /// </summary>
    /// <returns>是否已开始。</returns>
    private bool IsStarted()
    {
        return _context.IsStarted != null && _context.IsStarted.Invoke();
    }

    /// <summary>
    /// 检查当前是否存在玩家实例。
    /// </summary>
    /// <returns>是否存在玩家。</returns>
    private bool HasPlayer()
    {
        return _context.HasPlayer != null && _context.HasPlayer.Invoke();
    }

    /// <summary>
    /// 重置结算交互前的武器状态，未配置回调时安全跳过。
    /// </summary>
    private void ResetWeaponForPostCombatInteraction()
    {
        _context.ResetWeaponForPostCombatInteraction?.Invoke();
    }
}
