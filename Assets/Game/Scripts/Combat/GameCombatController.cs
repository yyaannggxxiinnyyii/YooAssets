using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// 管理战斗最小闭环，负责生成玩家、怪物、投射物、掉落物和战斗 UI 数据。
/// </summary>
public sealed class GameCombatController : MonoBehaviour
{
    private const float MinAttackInterval = 0.1f;

    [Header("流程引用")]
    [Tooltip("游戏流程状态管理器。")]
    [SerializeField] private GameStateManager gameStateManager;
    [Tooltip("标题、角色选择和武器选择界面控制器。")]
    [SerializeField] private GameFlowCanvasController flowCanvasController;
    [Tooltip("战斗、升级、商店、暂停和结算界面控制器。")]
    [SerializeField] private GameCombatCanvasController combatCanvasController;
    [Tooltip("战斗 Canvas 显示刷新和文案构建控制器。")]
    [SerializeField] private GameCombatUiPresenter combatUiPresenter;
    [Tooltip("战斗流程状态切换、暂停恢复和 Canvas 事件转发控制器。")]
    [SerializeField] private GameCombatStateFlowController stateFlowController;
    [Tooltip("Playing 状态下战斗 Tick、武器开火、弹丸命中和击杀结算控制器。")]
    [SerializeField] private GameCombatRuntimeController runtimeController;
    [Tooltip("商店商品池、刷新、锁定和购买状态控制器。")]
    [SerializeField] private GameShopController shopController;
    [Tooltip("角色升级强化选项抽取、刷新和应用控制器。")]
    [SerializeField] private GameUpgradeController upgradeController;
    [Tooltip("武器强化选项抽取和选择状态控制器。")]
    [SerializeField] private GameWeaponUpgradeController weaponUpgradeController;
    [Tooltip("投射物生成、命中、生命周期和弹丸特性控制器。")]
    [SerializeField] private GameProjectileCombatController projectileCombatController;
    [Tooltip("遗物拥有状态、堆叠和效果调度控制器。")]
    [SerializeField] private GameRelicRuntimeController relicRuntimeController;
    [Tooltip("玩家武器实例创建、布局和枪口查询控制器。")]
    [SerializeField] private GameWeaponCombatController weaponCombatController;
    [Tooltip("敌人生成、存活列表和敌人回收控制器。")]
    [SerializeField] private GameEnemySpawnController enemySpawnController;
    [Tooltip("经验、金币掉落物生成和金币收益倍率控制器。")]
    [SerializeField] private GameRewardDropController rewardDropController;
    [Tooltip("楼层进度、楼层计时和楼层结束后流程推进控制器。")]
    [SerializeField] private GameFloorProgressController floorProgressController;
    [Tooltip("普通开火、弹夹换弹、能量和武器技能控制器。")]
    [SerializeField] private GameWeaponFireController weaponFireController;
    [Tooltip("伤害跳字对象池和显示控制器。")]
    [SerializeField] private GameDamageNumberController damageNumberController;
    [Tooltip("统一管理战斗运行时对象创建和复用的对象池控制器。")]
    [SerializeField] private GameRuntimeObjectPoolController runtimeObjectPoolController;
    [Tooltip("战斗地图背景、边界和逻辑范围控制器。")]
    [SerializeField] private GameArenaController arenaController;
    [Tooltip("战斗相机跟随和平滑边界控制器。")]
    [SerializeField] private GameCameraController cameraController;

    [Header("Stage 流程")]
    [Tooltip("是否启用 StageData 房间流程。关闭时完全使用现有 FloorData 流程。")]
    [SerializeField] private bool useStageFlow;
    [Tooltip("StageData 房间进度控制器。")]
    [SerializeField] private GameStageProgressController stageProgressController;
    [Tooltip("StageData 场景门生成控制器。")]
    [SerializeField] private GameStageRoomDoorController stageRoomDoorController;
    [Tooltip("Stage 房间切换时使用的圆形黑幕转场控制器。")]
    [SerializeField] private GameRoomTransitionController roomTransitionController;
    [Tooltip("Stage 房间 Prefab 切换控制器，用于按房间类型替换地图、出生点和门根节点。")]
    [SerializeField] private GameStageRoomPrefabController stageRoomPrefabController;
    [Tooltip("进入新房间后玩家复位到的默认出生点。为空时使用世界原点。")]
    [SerializeField] private Transform roomSpawnPoint;

    [Header("初始配置")]
    [Tooltip("没有从角色选择界面获得数据时使用的默认角色数据。")]
    [SerializeField] private CharacterData startingCharacterData;
    [Tooltip("没有从武器选择界面获得数据时使用的默认武器数据。")]
    [SerializeField] private WeaponData startingWeaponData;
    [Tooltip("运行时生成玩家时使用的兜底玩家预制体。")]
    [SerializeField] private GamePlayerController fallbackPlayerPrefab;

    [Header("受击反馈")]
    [Tooltip("玩家成功受伤时播放的 Feel 反馈播放器，可配置 UI 震动、顿帧、音效等反馈。")]
    [SerializeField] private MMF_Player playerHurtFeedbacks;

    [Header("描边参数")]
    [Tooltip("玩家角色默认描边显示参数。")]
    [SerializeField] private GameSpriteOutlineSettings playerOutline = new GameSpriteOutlineSettings(new Color(0.5f, 0.95f, 1f, 1f), 1.4f, 1.15f, 0.06f);

    private readonly List<string> _ownedItems = new List<string>();
    private readonly List<IProjectileHitTarget> _projectileHitTargets = new List<IProjectileHitTarget>();

    private GamePlayerController _player;
    private Sprite _circleSprite;
    private WeaponData _currentWeaponData;
    private Transform _activeRuntimeRoot;
    private Transform _poolRoot;
    private float _elapsedTime;
    private int _kills;
    private int _totalKills;
    private int _score;
    private int _totalGoldCollected;
    private bool _started;
    private Vector2 _lastPlayerArenaMin;
    private Vector2 _lastPlayerArenaMax;
    private bool _hasPlayerArenaBounds;
    private bool _waitingForStageRoomSelection;
    private bool _stagePostCombatPendingRoomOptions;
    private bool _stageRoomTransitionInProgress;
    private bool _stageTransitRoomActive;
    private bool _stageSceneShopActive;
    private RoomOptionData _activeStageTransitRoomOption;
    private bool _stageTransitExitDoorSpawned;
    private int _lastObservedPlayerHealth;

    private void Awake()
    {
        if (gameStateManager == null)
            gameStateManager = FindObjectOfType<GameStateManager>();

        if (combatCanvasController == null)
            combatCanvasController = FindObjectOfType<GameCombatCanvasController>();

        if (flowCanvasController == null)
            flowCanvasController = FindObjectOfType<GameFlowCanvasController>();

        if (combatUiPresenter == null)
            combatUiPresenter = GetComponent<GameCombatUiPresenter>();

        if (combatUiPresenter == null)
            combatUiPresenter = FindObjectOfType<GameCombatUiPresenter>();

        if (combatUiPresenter != null)
            combatUiPresenter.Initialize(combatCanvasController);

        if (stateFlowController == null)
            stateFlowController = GetComponent<GameCombatStateFlowController>();

        if (stateFlowController == null)
            stateFlowController = FindObjectOfType<GameCombatStateFlowController>();

        if (runtimeController == null)
            runtimeController = GetComponent<GameCombatRuntimeController>();

        if (runtimeController == null)
            runtimeController = FindObjectOfType<GameCombatRuntimeController>();

        if (shopController == null)
            shopController = GetComponent<GameShopController>();

        if (shopController == null)
            shopController = FindObjectOfType<GameShopController>();

        if (upgradeController == null)
            upgradeController = GetComponent<GameUpgradeController>();

        if (upgradeController == null)
            upgradeController = FindObjectOfType<GameUpgradeController>();

        if (weaponUpgradeController == null)
            weaponUpgradeController = GetComponent<GameWeaponUpgradeController>();

        if (weaponUpgradeController == null)
            weaponUpgradeController = FindObjectOfType<GameWeaponUpgradeController>();

        if (projectileCombatController == null)
            projectileCombatController = GetComponent<GameProjectileCombatController>();

        if (projectileCombatController == null)
            projectileCombatController = FindObjectOfType<GameProjectileCombatController>();

        if (relicRuntimeController == null)
            relicRuntimeController = GetComponent<GameRelicRuntimeController>();

        if (relicRuntimeController == null)
            relicRuntimeController = FindObjectOfType<GameRelicRuntimeController>();

        if (weaponCombatController == null)
            weaponCombatController = GetComponent<GameWeaponCombatController>();

        if (weaponCombatController == null)
            weaponCombatController = FindObjectOfType<GameWeaponCombatController>();

        if (enemySpawnController == null)
            enemySpawnController = GetComponent<GameEnemySpawnController>();

        if (enemySpawnController == null)
            enemySpawnController = FindObjectOfType<GameEnemySpawnController>();

        if (rewardDropController == null)
            rewardDropController = GetComponent<GameRewardDropController>();

        if (rewardDropController == null)
            rewardDropController = FindObjectOfType<GameRewardDropController>();

        if (floorProgressController == null)
            floorProgressController = GetComponent<GameFloorProgressController>();

        if (floorProgressController == null)
            floorProgressController = FindObjectOfType<GameFloorProgressController>();

        if (weaponFireController == null)
            weaponFireController = GetComponent<GameWeaponFireController>();

        if (weaponFireController == null)
            weaponFireController = FindObjectOfType<GameWeaponFireController>();

        if (damageNumberController == null)
            damageNumberController = GetComponent<GameDamageNumberController>();

        if (damageNumberController == null)
            damageNumberController = FindObjectOfType<GameDamageNumberController>();

        if (runtimeObjectPoolController == null)
            runtimeObjectPoolController = GetComponent<GameRuntimeObjectPoolController>();

        if (runtimeObjectPoolController == null)
            runtimeObjectPoolController = FindObjectOfType<GameRuntimeObjectPoolController>();

        if (arenaController == null)
            arenaController = FindObjectOfType<GameArenaController>();

        if (cameraController == null)
            cameraController = FindObjectOfType<GameCameraController>();

        if (stageProgressController == null)
            stageProgressController = GetComponent<GameStageProgressController>();

        if (stageProgressController == null)
            stageProgressController = FindObjectOfType<GameStageProgressController>();

        if (stageRoomDoorController == null)
            stageRoomDoorController = GetComponent<GameStageRoomDoorController>();

        if (stageRoomDoorController == null)
            stageRoomDoorController = FindObjectOfType<GameStageRoomDoorController>();

        if (roomTransitionController == null)
            roomTransitionController = GetComponentInChildren<GameRoomTransitionController>(true);

        if (roomTransitionController == null)
            roomTransitionController = FindObjectOfType<GameRoomTransitionController>(true);

        if (stageRoomPrefabController == null)
            stageRoomPrefabController = FindObjectOfType<GameStageRoomPrefabController>(true);

        if (stageProgressController != null)
            stageProgressController.RoomOptionSelected += OnStageRoomOptionSelected;

        _circleSprite = CreateCircleSprite(32);
        InitializeObjectPools();
        InitializeStateFlowController();
    }

    private void OnDestroy()
    {
        UnbindPlayerHealEvent();
        if (stageProgressController != null)
            stageProgressController.RoomOptionSelected -= OnStageRoomOptionSelected;
    }

    private void Update()
    {
        if (!_started || _player == null)
            return;

        SyncPlayerArenaBoundsIfChanged();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (stateFlowController != null)
                stateFlowController.TogglePause();
        }

        if (gameStateManager.CurrentState != GameStateManager.GameState.Playing)
            return;

        if (_stageRoomTransitionInProgress)
        {
            RefreshCombatCanvasByState();
            return;
        }

        if (_stageTransitRoomActive)
        {
            RefreshCombatCanvasByState();
            return;
        }

        if (_waitingForStageRoomSelection)
        {
            RefreshCombatCanvasByState();
            return;
        }

        RecordStagePlayerDamageIfNeeded();

        if (runtimeController != null)
            runtimeController.TickCombat(CreateCombatRuntimeContext(Time.deltaTime));
    }

    /// <summary>
    /// 初始化战斗运行时对象。
    /// </summary>
    private void StartCombat()
    {
        _started = true;
        _elapsedTime = 0f;
        if (floorProgressController != null)
            floorProgressController.StartFirstFloor();
        if (ShouldUseStageFlow())
        {
            stageProgressController.StartStage();
            if (floorProgressController != null)
                floorProgressController.ResetCurrentFloorTimer(GetStageCombatRoomDurationSeconds());
        }
        _currentWeaponData = GetSelectedWeaponData();
        _kills = 0;
        _totalKills = 0;
        _score = 0;
        _totalGoldCollected = 0;
        _waitingForStageRoomSelection = false;
        _stagePostCombatPendingRoomOptions = false;
        _stageRoomTransitionInProgress = false;
        _stageTransitRoomActive = false;
        _stageSceneShopActive = false;
        _activeStageTransitRoomOption = null;
        _stageTransitExitDoorSpawned = false;
        _lastObservedPlayerHealth = 0;
        if (roomTransitionController != null)
            roomTransitionController.HideImmediate();
        if (relicRuntimeController != null)
            relicRuntimeController.ResetRuntime();
        if (rewardDropController != null)
            rewardDropController.ResetRewardState();
        if (enemySpawnController != null)
            enemySpawnController.ResetEnemyState();
        ResetProjectileState();
        InitializeWeaponRuntimeState();
        ResetShopState();
        ResetUpgradeState();
        ResetSelectedWeaponUpgrades();

        RefreshArena();
        ApplyStageRoomPrefabForCurrentRoom();
        CreatePlayer();
        _lastObservedPlayerHealth = _player != null ? _player.CurrentHealth : 0;
        if (ShouldUseStageFlow())
            PrepareStageCombatRoomSpawnPlan();
        SpawnCombatFieldPropsForCurrentRoom();
        if (relicRuntimeController != null)
            relicRuntimeController.InitializeRuntime(_player, rewardDropController);

        SetWeaponInteractionVisualsVisible(true);
    }

    /// <summary>
    /// 开始下一层战斗。
    /// </summary>
    private void StartNextFloor()
    {
        if (ShouldUseStageFlow())
        {
            StartNextStageCombatRoom();
            return;
        }

        if (floorProgressController != null)
            floorProgressController.ClearDurationOverride();
        if (floorProgressController != null)
            floorProgressController.StartNextFloor();
        _kills = 0;
        if (enemySpawnController != null)
            enemySpawnController.ClearStageSpawnPlan();
        if (enemySpawnController != null)
            enemySpawnController.ResetSpawnTimer();
        ResetWeaponForPostCombatInteraction();
        ResetShopState();
        ResetUpgradeState();
        ResetWeaponUpgradeState();
        ClearCombatObjects();
        SetPlayerControlEnabled(true);
    }

    /// <summary>
    /// 在 Stage 模式下开始下一个战斗房，复用旧战斗运行时但不推进旧 FloorData 楼层。
    /// </summary>
    private void StartNextStageCombatRoom()
    {
        _kills = 0;
        _waitingForStageRoomSelection = false;
        _stagePostCombatPendingRoomOptions = false;
        _stageTransitRoomActive = false;
        _stageSceneShopActive = false;
        _activeStageTransitRoomOption = null;
        _stageTransitExitDoorSpawned = false;
        _lastObservedPlayerHealth = _player != null ? _player.CurrentHealth : 0;

        if (floorProgressController != null)
            floorProgressController.ResetCurrentFloorTimer(GetStageCombatRoomDurationSeconds());
        if (enemySpawnController != null)
            enemySpawnController.ResetSpawnTimer();
        ResetWeaponForPostCombatInteraction();

        ResetShopState();
        ResetUpgradeState();
        ResetWeaponUpgradeState();
        ClearCombatObjects();
        ApplyStageRoomPrefabForCurrentRoom();
        ResetPlayerForRoomEntry();
        PrepareStageCombatRoomSpawnPlan();
        SpawnCombatFieldPropsForCurrentRoom();

        if (_player != null)
            SetPlayerControlEnabled(!_stageRoomTransitionInProgress);
    }

    /// <summary>
    /// 清理当前整局运行时对象和统计。
    /// </summary>
    private void ResetRun()
    {
        UnbindPlayerHealEvent();
        ClearAllRuntimeObjects();

        _player = null;
        if (weaponCombatController != null)
            weaponCombatController.ResetWeaponRuntime();
        _started = false;
        ResetShopState();
        ResetUpgradeState();
        ResetSelectedWeaponUpgrades();
        _currentWeaponData = null;
        _ownedItems.Clear();
        if (relicRuntimeController != null)
            relicRuntimeController.ResetRuntime();
        ResetProjectileState();
        if (floorProgressController != null)
            floorProgressController.ResetRunProgress();
        if (stageProgressController != null)
            stageProgressController.ResetStageProgress();
        if (stageRoomDoorController != null)
            stageRoomDoorController.ClearDoors();
        if (roomTransitionController != null)
            roomTransitionController.HideImmediate();
        _waitingForStageRoomSelection = false;
        _stagePostCombatPendingRoomOptions = false;
        _stageRoomTransitionInProgress = false;
        _stageTransitRoomActive = false;
        _stageSceneShopActive = false;
        _activeStageTransitRoomOption = null;
        _stageTransitExitDoorSpawned = false;
        _kills = 0;
        _totalKills = 0;
        _score = 0;
        _totalGoldCollected = 0;
        if (rewardDropController != null)
            rewardDropController.ResetRewardState();
        if (enemySpawnController != null)
            enemySpawnController.ResetEnemyState();
        if (weaponFireController != null)
            weaponFireController.ResetRunState();
        _elapsedTime = 0f;
    }

    /// <summary>
    /// 初始化战斗对象池和运行时父级。
    /// </summary>
    private void InitializeObjectPools()
    {
        _activeRuntimeRoot = CreateRuntimeRoot("ActiveRuntimeObjects");
        _poolRoot = CreateRuntimeRoot("PooledRuntimeObjects");

        if (runtimeObjectPoolController != null)
            runtimeObjectPoolController.InitializeRuntimePools(_poolRoot, _circleSprite);

        if (damageNumberController != null)
            damageNumberController.InitializeRuntimeObjectPool(runtimeObjectPoolController);
    }

    /// <summary>
    /// 初始化战斗流程状态控制器。
    /// </summary>
    private void InitializeStateFlowController()
    {
        if (stateFlowController == null)
            return;

        stateFlowController.Initialize(gameStateManager, combatCanvasController, CreateStateFlowContext());
    }

    /// <summary>
    /// 创建战斗流程状态控制器回调上下文。
    /// </summary>
    /// <returns>状态流程回调上下文。</returns>
    private GameCombatStateFlowController.StateFlowContext CreateStateFlowContext()
    {
        return new GameCombatStateFlowController.StateFlowContext
        {
            IsStarted = () => _started,
            HasPlayer = () => _player != null,
            SetPlayerControlEnabled = SetPlayerControlEnabled,
            StartCombat = StartCombat,
            StartNextFloor = StartNextFloor,
            ResetRun = ResetRun,
            ClearCombatObjects = ClearCombatObjectsForPostCombatState,
            ResetWeaponForPostCombatInteraction = ResetWeaponForPostCombatInteraction,
            PrepareShop = PrepareShop,
            PrepareUpgradeChoices = PrepareUpgradeChoices,
            PrepareWeaponUpgradeChoices = PrepareWeaponUpgradeChoices,
            RefreshHud = RefreshCombatCanvasHud,
            RefreshShop = RefreshCombatCanvasShop,
            RefreshLevelUp = RefreshCombatCanvasLevelUp,
            RefreshWeaponUpgrade = RefreshCombatCanvasWeaponUpgrade,
            RefreshResult = RefreshCombatCanvasResult,
            RefreshPause = RefreshCombatCanvasPause,
            ChooseUpgradeOffer = ChooseUpgradeOffer,
            TryRefreshUpgradeOffers = TryRefreshUpgradeOffers,
            ChooseWeaponUpgradeOffer = ChooseWeaponUpgradeOffer,
            TryBuyOffer = TryBuyOffer,
            ToggleShopOfferLock = ToggleShopOfferLock,
            TryRefreshShop = TryRefreshShop,
            IsFinalFloor = () => floorProgressController != null && floorProgressController.IsFinalFloor()
        };
    }

    /// <summary>
    /// 设置玩家控制开关。
    /// </summary>
    /// <param name="enabled">是否允许控制。</param>
    private void SetPlayerControlEnabled(bool enabled)
    {
        bool resolvedEnabled = enabled && !_stageRoomTransitionInProgress;
        if (_player != null)
            _player.SetControlEnabled(resolvedEnabled);

        SetWeaponInteractionVisualsVisible(resolvedEnabled && !_waitingForStageRoomSelection && !_stageTransitRoomActive);
    }

    /// <summary>
    /// 根据当前是否允许攻击，切换武器图形、弹夹、能量和准星资源显示。
    /// </summary>
    /// <param name="visible">是否显示武器交互表现。</param>
    private void SetWeaponInteractionVisualsVisible(bool visible)
    {
        if (weaponCombatController != null)
            weaponCombatController.SetWeaponVisualsVisible(visible);

        if (combatCanvasController != null)
            combatCanvasController.SetWeaponResourceHudVisible(visible);
    }

    /// <summary>
    /// 创建战斗运行控制器上下文。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    /// <returns>战斗运行上下文。</returns>
    private GameCombatRuntimeController.CombatRuntimeContext CreateCombatRuntimeContext(float deltaTime)
    {
        return new GameCombatRuntimeController.CombatRuntimeContext
        {
            DeltaTime = deltaTime,
            ElapsedTime = _elapsedTime,
            Started = _started,
            StateManager = gameStateManager,
            Player = _player,
            WeaponData = _currentWeaponData,
            ActiveRuntimeRoot = _activeRuntimeRoot,
            RuntimeObjectPoolController = runtimeObjectPoolController,
            ProjectileCombatController = projectileCombatController,
            WeaponCombatController = weaponCombatController,
            EnemySpawnController = enemySpawnController,
            RewardDropController = rewardDropController,
            FloorProgressController = floorProgressController,
            WeaponFireController = weaponFireController,
            DamageNumberController = damageNumberController,
            RelicRuntimeController = relicRuntimeController,
            AddElapsedTime = AddElapsedTime,
            AddScore = AddScore,
            AddKill = AddKill,
            AddStageBossSummonKill = AddStageBossSummonKill,
            AddGoldCollected = AddGoldCollected,
            NotifyEnemyDamaged = NotifyEnemyDamaged,
            ReleaseObject = ReleasePooledObject,
            RefreshCombatCanvasByState = RefreshCombatCanvasByState,
            PlayCursorFirePulse = PlayCursorFirePulse,
            CompleteFloor = CompleteFloor,
            HandleStageBossKilled = HandleStageBossKilled,
            UseStageSpawnPlan = ShouldUseStageSpawnPlan(),
            UseStageBossRuntime = ShouldUseStageBossRuntime(),
            StageData = stageProgressController != null ? stageProgressController.StageData : null,
            StageBossData = stageProgressController != null ? stageProgressController.GetCurrentBossData() : null,
            StageCombatNode = stageProgressController != null ? stageProgressController.CurrentCombatNode : 1,
            StageRoomType = stageProgressController != null ? stageProgressController.CurrentRoomType : RoomType.CombatNormal,
            GetArenaMin = GetArenaMin,
            GetArenaMax = GetArenaMax,
            ClampPositionInsideArena = ClampPositionInsideArena,
            GetProjectileHitTargets = GetProjectileHitTargets
        };
    }

    /// <summary>
    /// 累加本局运行时间。
    /// </summary>
    /// <param name="deltaTime">增加时间。</param>
    private void AddElapsedTime(float deltaTime)
    {
        _elapsedTime += deltaTime;
    }

    /// <summary>
    /// 累加当前分数。
    /// </summary>
    /// <param name="score">增加分数。</param>
    private void AddScore(int score)
    {
        int safeScore = Mathf.Max(0, score);
        _score += safeScore;
        if (ShouldUseStageFlow() && stageProgressController != null && !ShouldUseStageBossRuntime())
            stageProgressController.RunState.AddKillScore(safeScore);
    }

    /// <summary>
    /// 累加击杀计数。
    /// </summary>
    private void AddKill()
    {
        _kills++;
        _totalKills++;
        if (ShouldUseStageFlow() && stageProgressController != null)
            stageProgressController.RunState.AddKill();
    }

    /// <summary>
    /// 记录 Stage 模式下玩家生命下降造成的房间受伤统计。
    /// </summary>
    private void RecordStagePlayerDamageIfNeeded()
    {
        if (!ShouldUseStageFlow() || stageProgressController == null || _player == null)
            return;

        if (_lastObservedPlayerHealth <= 0)
        {
            _lastObservedPlayerHealth = _player.CurrentHealth;
            return;
        }

        if (_player.CurrentHealth < _lastObservedPlayerHealth)
        {
            if (ShouldUseStageBossRuntime())
                stageProgressController.RunState.AddBossDamageTaken();
            else
                stageProgressController.RunState.AddCurrentRoomDamageTaken();
        }

        _lastObservedPlayerHealth = _player.CurrentHealth;
    }

    /// <summary>
    /// 累加 Stage Boss 召唤物击杀统计。
    /// </summary>
    private void AddStageBossSummonKill()
    {
        StageRunState runState = stageProgressController != null ? stageProgressController.RunState : null;
        if (runState != null)
            runState.AddBossSummonKill();
    }

    /// <summary>
    /// 累加本局获得金币数量。
    /// </summary>
    /// <param name="gold">增加金币数量。</param>
    private void AddGoldCollected(int gold)
    {
        _totalGoldCollected += gold;
    }

    /// <summary>
    /// 播放准星实际开火反馈动画，并同步当前实时开火间隔。
    /// </summary>
    /// <param name="attackInterval">当前实时开火间隔。</param>
    private void PlayCursorFirePulse(float attackInterval)
    {
        if (combatCanvasController != null)
            combatCanvasController.PlayCursorFirePulse(attackInterval);
    }

    /// <summary>
    /// 转发敌人受伤事件，供后续遗物运行时监听。
    /// </summary>
    /// <param name="context">敌人受伤事件上下文。</param>
    private void NotifyEnemyDamaged(EnemyDamagedContext context)
    {
        if (relicRuntimeController != null)
            relicRuntimeController.DispatchEnemyDamaged(context);
    }

    /// <summary>
    /// 创建战斗控制器下的运行时根节点。
    /// </summary>
    /// <param name="rootName">根节点名称。</param>
    /// <returns>根节点 Transform。</returns>
    private Transform CreateRuntimeRoot(string rootName)
    {
        GameObject rootObject = new GameObject(rootName);
        rootObject.transform.SetParent(transform);
        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;
        return rootObject.transform;
    }

    /// <summary>
    /// 刷新战斗地图背景和四周边界。
    /// </summary>
    private void RefreshArena()
    {
        if (arenaController == null)
            return;

        arenaController.RefreshArena();
    }

    /// <summary>
    /// 同步玩家移动范围，保证运行时移动边界后限制范围实时生效。
    /// </summary>
    private void SyncPlayerArenaBoundsIfChanged()
    {
        if (_player == null || arenaController == null)
            return;

        Vector2 arenaMin = GetArenaMin();
        Vector2 arenaMax = GetArenaMax();
        if (_hasPlayerArenaBounds && _lastPlayerArenaMin == arenaMin && _lastPlayerArenaMax == arenaMax)
            return;

        _player.SetMovementBounds(arenaMin, arenaMax);
        _lastPlayerArenaMin = arenaMin;
        _lastPlayerArenaMax = arenaMax;
        _hasPlayerArenaBounds = true;
    }

    /// <summary>
    /// 创建玩家对象。
    /// </summary>
    private void CreatePlayer()
    {
        UnbindPlayerHealEvent();
        CharacterData characterData = GetSelectedCharacterData();
        GamePlayerController playerPrefab = characterData != null && characterData.PlayerPrefab != null
            ? characterData.PlayerPrefab
            : fallbackPlayerPrefab;
        if (playerPrefab != null)
        {
            _player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity, _activeRuntimeRoot);
        }
        else
        {
            GameObject playerObject = CreateSpriteObject("Player", Vector3.zero, Vector3.one * 0.72f, new Color(0.95f, 0.72f, 0.28f), _circleSprite);
            playerObject.transform.SetParent(_activeRuntimeRoot);
            _player = playerObject.AddComponent<GamePlayerController>();
        }

        _player.transform.position = ClampPositionInsideArena(GetRoomSpawnPosition());
        EnsurePlayerRenderer();
        _player.Initialize(characterData);
        _player.InitializeWeaponStats(_currentWeaponData);
        _player.SetHurtFeedbacks(playerHurtFeedbacks);
        _hasPlayerArenaBounds = false;
        SyncPlayerArenaBoundsIfChanged();
        if (cameraController != null)
            cameraController.BindTarget(_player.transform, arenaController, GetRoomCameraBoundsCollider());

        if (weaponCombatController != null)
            weaponCombatController.Initialize(_player, _currentWeaponData);

        BindPlayerHealEvent();
    }

    /// <summary>
    /// 绑定玩家实际回血事件，用于生成治疗跳字。
    /// </summary>
    private void BindPlayerHealEvent()
    {
        if (_player == null)
            return;

        _player.Healed -= OnPlayerHealed;
        _player.Healed += OnPlayerHealed;
    }

    /// <summary>
    /// 解除玩家实际回血事件，避免玩家对象销毁后保留事件引用。
    /// </summary>
    private void UnbindPlayerHealEvent()
    {
        if (_player != null)
            _player.Healed -= OnPlayerHealed;
    }

    /// <summary>
    /// 处理玩家回血反馈，在玩家头顶显示绿色治疗跳字。
    /// </summary>
    /// <param name="player">恢复生命的玩家。</param>
    /// <param name="healAmount">实际恢复的红血点数。</param>
    private void OnPlayerHealed(GamePlayerController player, int healAmount)
    {
        if (player == null || healAmount <= 0 || damageNumberController == null)
            return;

        damageNumberController.SpawnHealNumber(_activeRuntimeRoot, player.transform.position, healAmount);
    }

    /// <summary>
    /// 将玩家复位到当前房间默认出生点，并同步移动边界和相机位置。
    /// </summary>
    private void ResetPlayerForRoomEntry()
    {
        if (_player == null)
            return;

        SyncRoomReferencesFromActiveStageRoom();
        Vector3 spawnPosition = ClampPositionInsideArena(GetRoomSpawnPosition());
        _player.transform.position = spawnPosition;
        _hasPlayerArenaBounds = false;
        SyncPlayerArenaBoundsIfChanged();
        if (cameraController != null)
        {
            cameraController.BindTarget(_player.transform, arenaController, GetRoomCameraBoundsCollider());
            cameraController.ForceSnapToTarget();
        }
    }

    /// <summary>
    /// 按当前 Stage 房间类型尝试切换房间 Prefab，未配置时保留当前场景地图。
    /// </summary>
    private void ApplyStageRoomPrefabForCurrentRoom()
    {
        if (!ShouldUseStageFlow() || stageRoomPrefabController == null || stageProgressController == null)
            return;

        if (!stageRoomPrefabController.TryLoadRoom(stageProgressController.CurrentRoomType))
        {
            SyncRoomReferencesFromActiveStageRoom();
            return;
        }

        SyncRoomReferencesFromActiveStageRoom();
    }

    /// <summary>
    /// 从当前激活房间 Prefab 同步地图边界和门生成根节点引用。
    /// </summary>
    private void SyncRoomReferencesFromActiveStageRoom()
    {
        if (stageRoomPrefabController == null || !stageRoomPrefabController.HasActiveRoom)
            return;

        GameArenaController activeArenaController = stageRoomPrefabController.ActiveArenaController;
        if (activeArenaController != null)
            arenaController = activeArenaController;

        if (stageRoomDoorController != null)
            stageRoomDoorController.SetDoorRoot(stageRoomPrefabController.ActiveDoorRoot);

        RefreshArena();
        _hasPlayerArenaBounds = false;
    }

    /// <summary>
    /// 获取当前房间玩家默认出生点坐标。
    /// </summary>
    /// <returns>出生点世界坐标。</returns>
    private Vector3 GetRoomSpawnPosition()
    {
        Transform activeSpawnPoint = stageRoomPrefabController != null ? stageRoomPrefabController.ActivePlayerSpawnPoint : null;
        if (activeSpawnPoint != null)
            return activeSpawnPoint.position;

        return roomSpawnPoint != null ? roomSpawnPoint.position : Vector3.zero;
    }

    /// <summary>
    /// 获取当前房间自定义相机边界碰撞器。
    /// </summary>
    /// <returns>房间相机边界碰撞器；不存在时返回 null。</returns>
    private PolygonCollider2D GetRoomCameraBoundsCollider()
    {
        return stageRoomPrefabController != null ? stageRoomPrefabController.ActiveCameraBoundsCollider : null;
    }

    /// <summary>
    /// 获取战斗区域内部最小坐标。
    /// </summary>
    /// <returns>区域最小坐标。</returns>
    private Vector2 GetArenaMin()
    {
        return arenaController != null ? arenaController.ArenaMin : Vector2.zero;
    }

    /// <summary>
    /// 获取战斗区域内部最大坐标。
    /// </summary>
    /// <returns>区域最大坐标。</returns>
    private Vector2 GetArenaMax()
    {
        return arenaController != null ? arenaController.ArenaMax : Vector2.zero;
    }

    /// <summary>
    /// 将世界坐标限制在战斗区域内部。
    /// </summary>
    /// <param name="position">待限制的世界坐标。</param>
    /// <returns>限制后的世界坐标。</returns>
    private Vector3 ClampPositionInsideArena(Vector3 position)
    {
        return arenaController != null ? arenaController.ClampPositionInsideArena(position) : position;
    }

    /// <summary>
    /// 清理当前楼层的怪物、投射物和掉落物。
    /// </summary>
    private void ClearCombatObjects()
    {
        ClearCombatObjects(true);
    }

    /// <summary>
    /// 清理当前楼层的怪物、投射物和掉落物，并按配置决定是否清理场地道具。
    /// </summary>
    private void ClearCombatObjectsForPostCombatState()
    {
        ClearCombatObjects(false);
    }

    /// <summary>
    /// 清理当前楼层的怪物、投射物、掉落物和可选场地道具。
    /// </summary>
    /// <param name="forceClearFieldProps">是否无视房间配置强制清理场地道具。</param>
    private void ClearCombatObjects(bool forceClearFieldProps)
    {
        ClearCombatFieldProps(forceClearFieldProps);

        if (enemySpawnController != null)
            enemySpawnController.ClearEnemies(ReleasePooledObject);

        if (projectileCombatController != null)
            projectileCombatController.ClearProjectiles(ReleasePooledObject);

        if (rewardDropController != null)
            rewardDropController.ClearPickups(_activeRuntimeRoot, ReleasePooledObject);
    }

    /// <summary>
    /// 按当前房间配置生成战斗场地道具。
    /// </summary>
    private void SpawnCombatFieldPropsForCurrentRoom()
    {
        if (!ShouldUseStageFlow() || stageProgressController == null || !IsStageCombatRoom(stageProgressController.CurrentRoomType))
            return;

        StageCombatFieldPropSpawner fieldPropSpawner = GetActiveCombatFieldPropSpawner();
        if (fieldPropSpawner == null || _player == null)
            return;

        fieldPropSpawner.SpawnForCombatRoom(_player, GetArenaMin(), GetArenaMax());
    }

    /// <summary>
    /// 清理当前战斗房生成的场地道具。
    /// </summary>
    private void ClearCombatFieldProps(bool forceClear)
    {
        StageCombatFieldPropSpawner fieldPropSpawner = GetActiveCombatFieldPropSpawner();
        if (fieldPropSpawner == null)
        {
            _projectileHitTargets.Clear();
            return;
        }

        if (forceClear || fieldPropSpawner.ClearFieldPropsOnCombatClear)
        {
            fieldPropSpawner.ClearFieldProps();
        }
        else
        {
            fieldPropSpawner.StopSpawningAndKeepFieldProps();
        }

        _projectileHitTargets.Clear();
    }

    /// <summary>
    /// 收集当前可被投射物命中的非敌方目标。
    /// </summary>
    /// <returns>当前可命中的投射物目标列表。</returns>
    private IReadOnlyList<IProjectileHitTarget> GetProjectileHitTargets()
    {
        _projectileHitTargets.Clear();
        StageCombatFieldPropSpawner fieldPropSpawner = GetActiveCombatFieldPropSpawner();
        if (fieldPropSpawner != null)
            fieldPropSpawner.CollectProjectileHitTargets(_projectileHitTargets);

        return _projectileHitTargets;
    }

    /// <summary>
    /// 获取当前房间的战斗场地道具生成器。
    /// </summary>
    /// <returns>当前激活房间的场地道具生成器。</returns>
    private StageCombatFieldPropSpawner GetActiveCombatFieldPropSpawner()
    {
        return stageRoomPrefabController != null ? stageRoomPrefabController.ActiveCombatFieldPropSpawner : null;
    }

    /// <summary>
    /// 清理整局运行时创建的所有子对象。
    /// </summary>
    private void ClearAllRuntimeObjects()
    {
        ClearCombatObjects();

        for (int i = _activeRuntimeRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = _activeRuntimeRoot.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }

        if (enemySpawnController != null)
            enemySpawnController.ResetEnemyState();
        if (weaponCombatController != null)
            weaponCombatController.ResetWeaponRuntime();
    }

    /// <summary>
    /// 获取当前最终基础伤害。
    /// </summary>
    /// <returns>最终基础伤害。</returns>
    private int GetCurrentAttackDamage()
    {
        if (_currentWeaponData != null)
        {
            float multiplier = Mathf.Max(0.01f, _player.AttackDamageMultiplier);
            int baseDamage = _player.AttackDamage + _currentWeaponData.BaseDamage + _player.AttackDamageBonus;
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
        }

        return _player.AttackDamage;
    }

    /// <summary>
    /// 获取当前武器或玩家提供的攻击间隔。
    /// </summary>
    /// <returns>攻击间隔。</returns>
    private float GetCurrentAttackInterval()
    {
        if (_currentWeaponData != null)
            return Mathf.Max(MinAttackInterval, _currentWeaponData.AttackInterval / _player.AttackSpeedMultiplier);

        return MinAttackInterval;
    }

    /// <summary>
    /// 获取当前射速倍率。
    /// </summary>
    /// <returns>射速倍率。</returns>
    private float GetCurrentAttackSpeedMultiplier()
    {
        return _player != null ? _player.AttackSpeedMultiplier : 1f;
    }

    /// <summary>
    /// 获取当前武器的投射物速度。
    /// </summary>
    /// <returns>投射物速度。</returns>
    private float GetCurrentProjectileSpeed()
    {
        if (_currentWeaponData != null)
            return Mathf.Max(0.1f, _currentWeaponData.ProjectileSpeed * _player.ProjectileSpeedMultiplier);

        return 9f;
    }

    /// <summary>
    /// 获取当前弹速倍率。
    /// </summary>
    /// <returns>弹速倍率。</returns>
    private float GetCurrentProjectileSpeedMultiplier()
    {
        return _player != null ? _player.ProjectileSpeedMultiplier : 1f;
    }

    /// <summary>
    /// 获取当前武器的投射物存在时间。
    /// </summary>
    /// <returns>投射物存在时间。</returns>
    private float GetCurrentProjectileLifeTime()
    {
        if (_currentWeaponData != null)
            return Mathf.Max(0.1f, _currentWeaponData.ProjectileLifeTime * _player.ProjectileLifeTimeMultiplier);

        return 2.2f;
    }

    /// <summary>
    /// 获取当前暴击率。
    /// </summary>
    /// <returns>暴击率。</returns>
    private float GetCurrentCriticalRate()
    {
        float relicBonus = relicRuntimeController != null ? relicRuntimeController.GetCriticalRateBonus() : 0f;
        return Mathf.Clamp01(_player.CriticalRate + relicBonus);
    }

    /// <summary>
    /// 获取当前暴击伤害倍率。
    /// </summary>
    /// <returns>暴击伤害倍率。</returns>
    private float GetCurrentCriticalDamage()
    {
        return Mathf.Max(1f, _player.CriticalDamage);
    }

    /// <summary>
    /// 获取当前子弹穿透层数。
    /// </summary>
    /// <returns>子弹穿透层数。</returns>
    private int GetCurrentProjectilePierce()
    {
        return Mathf.Max(0, _player.ProjectilePierce);
    }

    /// <summary>
    /// 获取当前子弹反弹层数。
    /// </summary>
    /// <returns>子弹反弹层数。</returns>
    private int GetCurrentProjectileBounce()
    {
        return Mathf.Max(0, _player.ProjectileBounce);
    }

    /// <summary>
    /// 获取当前弹道数。
    /// </summary>
    /// <returns>弹道数量。</returns>
    private int GetCurrentProjectileCount()
    {
        return Mathf.Max(1, _player.ProjectileCount);
    }

    /// <summary>
    /// 创建武器开火数值查询上下文。
    /// </summary>
    /// <returns>武器开火数值查询上下文。</returns>
    private GameWeaponFireController.WeaponFireRuntimeContext CreateWeaponFireStatsContext()
    {
        return new GameWeaponFireController.WeaponFireRuntimeContext
        {
            Player = _player,
            WeaponData = _currentWeaponData
        };
    }

    /// <summary>
    /// 初始化当前武器的弹夹和能量状态。
    /// </summary>
    private void InitializeWeaponRuntimeState()
    {
        if (runtimeController != null)
            runtimeController.InitializeWeaponRuntimeState(CreateCombatRuntimeContext(0f));
    }

    /// <summary>
    /// 战斗房结算时重置武器交互状态，并补满当前弹夹。
    /// </summary>
    private void ResetWeaponForPostCombatInteraction()
    {
        if (weaponFireController == null)
            return;

        weaponFireController.ResetForPostCombatInteraction(CreateWeaponFireStatsContext());
    }

    /// <summary>
    /// 重置商店运行时状态。
    /// </summary>
    private void ResetShopState()
    {
        if (shopController != null)
            shopController.ResetShopState();
    }

    /// <summary>
    /// 重置升级强化运行时状态。
    /// </summary>
    private void ResetUpgradeState()
    {
        if (upgradeController != null)
            upgradeController.ResetUpgradeState();
    }

    /// <summary>
    /// 重置武器强化运行时状态。
    /// </summary>
    private void ResetWeaponUpgradeState()
    {
        if (weaponUpgradeController != null)
            weaponUpgradeController.ResetWeaponUpgradeState();
    }

    /// <summary>
    /// 重置本局已经选择过的武器强化记录。
    /// </summary>
    private void ResetSelectedWeaponUpgrades()
    {
        if (weaponUpgradeController != null)
            weaponUpgradeController.ResetSelectedWeaponUpgrades();
    }

    /// <summary>
    /// 重置投射物运行时状态。
    /// </summary>
    private void ResetProjectileState()
    {
        if (projectileCombatController != null)
            projectileCombatController.ResetProjectileState();
    }

    /// <summary>
    /// 增加当前武器实例数量，用于双持或后续道具扩展。
    /// </summary>
    /// <param name="value">增加数量。</param>
    private void AddWeaponInstanceCount(int value)
    {
        if (weaponCombatController == null)
            return;

        weaponCombatController.AddWeaponInstanceCount(value);
    }

    /// <summary>
    /// 按倍率调整当前武器实例数量，用于后续稀有道具扩展。
    /// </summary>
    /// <param name="multiplier">实例数量倍率。</param>
    private void MultiplyWeaponInstanceCount(float multiplier)
    {
        if (weaponCombatController == null)
            return;

        weaponCombatController.MultiplyWeaponInstanceCount(multiplier);
    }

    /// <summary>
    /// 获取当前武器实例数量。
    /// </summary>
    /// <returns>当前武器实例数量。</returns>
    private int GetCurrentWeaponInstanceCount()
    {
        return weaponCombatController != null ? weaponCombatController.GetCurrentWeaponInstanceCount() : 1;
    }

    /// <summary>
    /// 确保预制体玩家在未配置 Sprite 时仍能使用默认圆形图显示，并为玩家美术配置描边。
    /// </summary>
    private void EnsurePlayerRenderer()
    {
        SpriteRenderer[] playerRenderers = _player.GetComponentsInChildren<SpriteRenderer>();
        if (playerRenderers == null || playerRenderers.Length <= 0)
        {
            GameObject visualObject = new GameObject("PlayerVisual");
            visualObject.transform.SetParent(_player.transform, false);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = Vector3.one * 0.72f;
            SpriteRenderer fallbackRenderer = visualObject.AddComponent<SpriteRenderer>();
            fallbackRenderer.sprite = _circleSprite;
            fallbackRenderer.sortingOrder = 3;
            GameSpriteOutlineController.Configure(fallbackRenderer, playerOutline);
            return;
        }

        if (playerRenderers.Length == 1)
        {
            SpriteRenderer playerRenderer = playerRenderers[0];
            if (playerRenderer.sprite == null)
                playerRenderer.sprite = _circleSprite;

            playerRenderer.sortingOrder = 3;
            GameSpriteOutlineController.Configure(playerRenderer, playerOutline);
            return;
        }

        Transform outlineRoot = FindPlayerOutlineRoot(playerRenderers);
        GameCompositeSpriteOutlineController.Configure(outlineRoot, playerOutline);
    }

    /// <summary>
    /// 查找最适合作为组合描边目标的玩家美术根节点。
    /// </summary>
    /// <param name="playerRenderers">玩家所有 SpriteRenderer。</param>
    /// <returns>组合描边目标根节点。</returns>
    private Transform FindPlayerOutlineRoot(SpriteRenderer[] playerRenderers)
    {
        Transform visualRoot = _player.transform.Find("VisualRoot");
        if (visualRoot != null)
            return visualRoot;

        if (playerRenderers == null || playerRenderers.Length <= 0 || playerRenderers[0] == null)
            return _player.transform;

        Transform commonRoot = playerRenderers[0].transform.parent;
        while (commonRoot != null && !ContainsAllRenderers(commonRoot, playerRenderers))
            commonRoot = commonRoot.parent;

        return commonRoot != null && commonRoot != _player.transform.parent ? commonRoot : _player.transform;
    }

    /// <summary>
    /// 判断指定根节点是否包含所有玩家 SpriteRenderer。
    /// </summary>
    /// <param name="root">待检查根节点。</param>
    /// <param name="playerRenderers">玩家 SpriteRenderer 列表。</param>
    /// <returns>全部包含时返回 true。</returns>
    private bool ContainsAllRenderers(Transform root, SpriteRenderer[] playerRenderers)
    {
        if (root == null || playerRenderers == null)
            return false;

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            SpriteRenderer playerRenderer = playerRenderers[i];
            if (playerRenderer == null)
                continue;

            if (!playerRenderer.transform.IsChildOf(root))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 获取当前弹夹容量。
    /// </summary>
    /// <returns>弹夹容量。</returns>
    private int GetCurrentMagazineCapacity()
    {
        return weaponFireController != null ? weaponFireController.GetCurrentMagazineCapacity(CreateWeaponFireStatsContext()) : 1;
    }

    /// <summary>
    /// 获取当前最大能量。
    /// </summary>
    /// <returns>最大能量。</returns>
    private float GetCurrentMaxEnergy()
    {
        return weaponFireController != null ? weaponFireController.GetCurrentMaxEnergy(CreateWeaponFireStatsContext()) : 100f;
    }

    /// <summary>
    /// 获取当前蓄力总时长。
    /// </summary>
    /// <returns>蓄力总时长。</returns>
    private float GetCurrentChargeDuration()
    {
        return weaponFireController != null ? weaponFireController.GetCurrentChargeDuration(CreateWeaponFireStatsContext()) : 1f;
    }

    /// <summary>
    /// 获取当前换弹总时长。
    /// </summary>
    /// <returns>换弹总时长。</returns>
    private float GetCurrentReloadDuration()
    {
        return weaponFireController != null ? weaponFireController.GetCurrentReloadTime(CreateWeaponFireStatsContext()) : 0.8f;
    }

    /// <summary>
    /// 获取当前角色显示名称。
    /// </summary>
    /// <returns>角色显示名称。</returns>
    private string GetCurrentCharacterName()
    {
        CharacterData characterData = GetSelectedCharacterData();
        if (characterData != null)
            return characterData.DisplayName;

        return "见习冒险者";
    }

    /// <summary>
    /// 获取当前选择的角色数据。
    /// </summary>
    /// <returns>角色数据。</returns>
    private CharacterData GetSelectedCharacterData()
    {
        if (GameRunStartContext.TryGetSelection(out CharacterData contextCharacterData, out _))
            return contextCharacterData;

        if (flowCanvasController != null && flowCanvasController.SelectedCharacterData != null)
            return flowCanvasController.SelectedCharacterData;

        return startingCharacterData;
    }

    /// <summary>
    /// 获取当前选择的武器数据。
    /// </summary>
    /// <returns>武器数据。</returns>
    private WeaponData GetSelectedWeaponData()
    {
        if (GameRunStartContext.TryGetSelection(out _, out WeaponData contextWeaponData))
            return contextWeaponData;

        if (flowCanvasController != null && flowCanvasController.SelectedWeaponData != null)
            return flowCanvasController.SelectedWeaponData;

        return startingWeaponData;
    }

    /// <summary>
    /// 获取当前武器显示名称。
    /// </summary>
    /// <returns>武器显示名称。</returns>
    private string GetCurrentWeaponName()
    {
        if (_currentWeaponData != null)
            return _currentWeaponData.DisplayName;

        return "训练飞弹";
    }

    /// <summary>
    /// 获取当前楼层序号。
    /// </summary>
    /// <returns>当前楼层序号。</returns>
    private int GetCurrentFloorIndex()
    {
        return floorProgressController != null ? floorProgressController.CurrentFloor : 1;
    }

    /// <summary>
    /// 完成本层计时并进入后续结算流程。
    /// </summary>
    private void CompleteFloor()
    {
        if (ShouldUseStageFlow())
        {
            CompleteStageCombatRoom();
            return;
        }

        EnterNextPostFloorState();
    }

    /// <summary>
    /// 处理 Stage 模式下的战斗房完成逻辑，清理战斗对象并生成场景门。
    /// </summary>
    private void CompleteStageCombatRoom()
    {
        if (_waitingForStageRoomSelection || _stagePostCombatPendingRoomOptions)
            return;

        RecordStagePlayerDamageIfNeeded();
        ApplyStageRoomScoreBonuses();
        _stagePostCombatPendingRoomOptions = true;
        SetPlayerControlEnabled(false);
        ResetWeaponForPostCombatInteraction();
        ClearCombatObjectsForPostCombatState();

        ContinueStagePostCombatFlow();
    }

    /// <summary>
    /// 推进 Stage 战斗结束后的奖励选择、门生成或通关结算。
    /// </summary>
    private void ContinueStagePostCombatFlow()
    {
        if (TryEnterStagePendingChoiceState())
            return;

        SpawnStageRoomOptionsOrCompleteStage();
    }

    /// <summary>
    /// 如果玩家仍有待处理强化选择，则先进入对应 UI 状态。
    /// </summary>
    /// <returns>进入强化选择状态时返回 true。</returns>
    private bool TryEnterStagePendingChoiceState()
    {
        if (_player != null && _player.PendingWeaponUpgradeChoices > 0)
        {
            EnterPostFloorState(GameStateManager.GameState.WeaponUpgrade);
            return true;
        }

        if (_player != null && _player.PendingUpgradeChoices > 0)
        {
            EnterPostFloorState(GameStateManager.GameState.LevelUp);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 在 Stage 强化选择全部处理完成后生成门，或在完成全部房间节点后进入通关。
    /// </summary>
    private void SpawnStageRoomOptionsOrCompleteStage()
    {
        if (stageProgressController.IsStageComplete())
        {
            _waitingForStageRoomSelection = false;
            _stagePostCombatPendingRoomOptions = false;
            EnterPostFloorState(GameStateManager.GameState.Victory);
            return;
        }

        RoomOptionData[] roomOptions = stageProgressController.CompleteCurrentCombatRoom();
        if (roomOptions == null || roomOptions.Length <= 0)
        {
            Debug.LogError("[Stage] 当前节点没有可用门规则或房间选项池配置无效，请检查 StageData.RoomOptionRules。");
            _waitingForStageRoomSelection = false;
            _stagePostCombatPendingRoomOptions = false;
            SetPlayerControlEnabled(true);
            return;
        }

        if (stageRoomDoorController == null)
        {
            Debug.LogError("[Stage] 缺少 GameStageRoomDoorController，无法生成房间门。请在场景中配置门生成控制器引用。");
            _waitingForStageRoomSelection = false;
            _stagePostCombatPendingRoomOptions = false;
            SetPlayerControlEnabled(true);
            return;
        }

        if (gameStateManager != null && gameStateManager.CurrentState != GameStateManager.GameState.Playing)
            gameStateManager.StartPlaying();

        _waitingForStageRoomSelection = true;
        _stagePostCombatPendingRoomOptions = false;
        stageRoomDoorController.SpawnDoors(roomOptions);
        SetPlayerControlEnabled(true);
        SetWeaponInteractionVisualsVisible(false);
    }

    /// <summary>
    /// 处理 Stage 门选择事件，并按目标房间类型进入战斗房或中转房。
    /// </summary>
    /// <param name="optionIndex">门选项索引。</param>
    /// <param name="roomOption">被选择的门选项。</param>
    private void OnStageRoomOptionSelected(int optionIndex, RoomOptionData roomOption)
    {
        if (!ShouldUseStageFlow() || !_waitingForStageRoomSelection || roomOption == null || _stageRoomTransitionInProgress)
            return;

        ContinueAfterStageRoomOption(roomOption);
    }

    /// <summary>
    /// 根据已选择的 Stage 房间类型恢复或推进到下一段战斗。
    /// </summary>
    /// <param name="roomOption">被选择的门选项。</param>
    private void ContinueAfterStageRoomOption(RoomOptionData roomOption)
    {
        if (roomOption == null)
            return;

        if (roomTransitionController != null)
        {
            PlayStageRoomTransition(roomOption);
            return;
        }

        ContinueAfterStageRoomOptionWithoutTransition(roomOption);
    }

    /// <summary>
    /// 播放 Stage 选门后的房间转场，并在遮满时推进到目标房间。
    /// </summary>
    /// <param name="roomOption">被选择的门选项。</param>
    private void PlayStageRoomTransition(RoomOptionData roomOption)
    {
        _stageRoomTransitionInProgress = true;
        _waitingForStageRoomSelection = true;
        _stagePostCombatPendingRoomOptions = false;
        SetPlayerControlEnabled(false);
        if (stageRoomDoorController != null)
            stageRoomDoorController.ClearDoors();

        Vector3 transitionCenter = _player != null ? _player.transform.position : GetRoomSpawnPosition();
        roomTransitionController.PlayWorldTransition(
            transitionCenter,
            () => ContinueAfterStageRoomOptionWithoutTransition(roomOption),
            OnStageRoomTransitionCompleted);
    }

    /// <summary>
    /// 不播放转场时直接按门选项推进 Stage 房间流程。
    /// </summary>
    /// <param name="roomOption">被选择的门选项。</param>
    private void ContinueAfterStageRoomOptionWithoutTransition(RoomOptionData roomOption)
    {
        if (roomOption == null)
            return;

        if (IsStageCombatRoom(roomOption.RoomType))
        {
            StartNextStageCombatRoomAndResume();
            return;
        }

        if (IsStageTransitRoom(roomOption.RoomType))
        {
            EnterStageTransitRoom(roomOption);
            return;
        }
    }

    /// <summary>
    /// 在 Stage 房间转场完全打开后恢复玩家控制。
    /// </summary>
    private void OnStageRoomTransitionCompleted()
    {
        _stageRoomTransitionInProgress = false;
        if (gameStateManager != null && gameStateManager.CurrentState == GameStateManager.GameState.Playing)
            SetPlayerControlEnabled(true);
    }

    /// <summary>
    /// 进入 Stage 中转房，当前优先支持商店房的真实房间停留和离开门。
    /// </summary>
    /// <param name="roomOption">进入中转房时选择的门选项。</param>
    private void EnterStageTransitRoom(RoomOptionData roomOption)
    {
        if (roomOption == null)
            return;

        _waitingForStageRoomSelection = false;
        _stagePostCombatPendingRoomOptions = false;
        _stageTransitRoomActive = true;
        _stageSceneShopActive = roomOption.RoomType == RoomType.Shop;
        _activeStageTransitRoomOption = roomOption;
        _stageTransitExitDoorSpawned = false;

        ResetWeaponForPostCombatInteraction();
        ResetShopState();
        ClearCombatObjects();
        ApplyStageRoomPrefabForCurrentRoom();
        ResetPlayerForRoomEntry();
        bool shouldSpawnExitDoorImmediately = PrepareStageTransitRoom(roomOption);
        BindStageShopOfferPointsIfNeeded();

        if (gameStateManager != null)
            gameStateManager.StartPlaying();

        if (shouldSpawnExitDoorImmediately)
            SpawnStageTransitExitDoor(roomOption);

        SetPlayerControlEnabled(true);
        SetWeaponInteractionVisualsVisible(false);
    }

    /// <summary>
    /// 准备 Stage 中转房的房间内容。
    /// </summary>
    /// <param name="roomOption">进入中转房时选择的门选项。</param>
    /// <returns>是否需要立即生成离开门。</returns>
    private bool PrepareStageTransitRoom(RoomOptionData roomOption)
    {
        if (roomOption == null)
            return true;

        if (roomOption.RoomType == RoomType.Shop)
        {
            PrepareShop();
            return true;
        }

        if (IsStageFunctionRoom(roomOption.RoomType))
            return !TrySpawnStageFunctionObject(roomOption.RoomType);

        return true;
    }

    /// <summary>
    /// 尝试在当前功能房中生成一个功能对象。
    /// </summary>
    /// <param name="roomType">当前中转房类型。</param>
    /// <returns>成功生成功能对象时返回 true。</returns>
    private bool TrySpawnStageFunctionObject(RoomType roomType)
    {
        StageRoomFunctionObjectSpawner functionSpawner = stageRoomPrefabController != null
            ? stageRoomPrefabController.ActiveFunctionObjectSpawner
            : null;
        if (functionSpawner == null)
        {
            Debug.LogWarning($"[StageFunctionRoom] 当前 {roomType} 房间没有配置 StageRoomFunctionObjectSpawner，将直接生成离开门。");
            return false;
        }

        StageRoomFunctionContext context = new StageRoomFunctionContext(
            _player,
            OnStageFunctionObjectCompleted,
            AddGoldCollected,
            RefreshCombatCanvasHud);
        bool spawned = functionSpawner.SpawnFunctionObject(roomType, context);
        if (!spawned)
            Debug.LogWarning($"[StageFunctionRoom] 当前 {roomType} 房间没有可生成的功能对象，将直接生成离开门。");

        return spawned;
    }

    /// <summary>
    /// 功能房对象完成交互后生成离开门。
    /// </summary>
    private void OnStageFunctionObjectCompleted()
    {
        if (!_stageTransitRoomActive || _activeStageTransitRoomOption == null)
            return;

        SpawnStageTransitExitDoor(_activeStageTransitRoomOption);
    }

    /// <summary>
    /// 如果当前是商店中转房，则把商店商品绑定到房间内的悬浮商品点。
    /// </summary>
    private void BindStageShopOfferPointsIfNeeded()
    {
        if (!_stageSceneShopActive || stageRoomPrefabController == null || shopController == null || _player == null)
            return;

        StageShopOfferPoint[] offerPoints = ResolveStageShopOfferPoints();
        if (offerPoints == null || offerPoints.Length <= 0)
        {
            Debug.LogWarning("[StageShop] 当前商店房没有配置 StageShopOfferPoint，无法生成场景商品。");
            return;
        }

        for (int i = 0; i < offerPoints.Length; i++)
        {
            StageShopOfferPoint offerPoint = offerPoints[i];
            if (offerPoint == null)
                continue;

            int offerIndex = offerPoint.OfferIndex;
            if (!shopController.TryGetOffer(offerIndex, out GameShopController.ShopOffer offer))
            {
                offerPoint.ClearOffer();
                continue;
            }

            offerPoint.BindOffer(offerIndex, offer, _player.Gold >= offer.Cost, TryBuyStageSceneShopOffer);
        }
    }

    /// <summary>
    /// 获取当前商店房应使用的商品点，优先使用自动排布控制器生成的点位。
    /// </summary>
    /// <returns>商品点列表。</returns>
    private StageShopOfferPoint[] ResolveStageShopOfferPoints()
    {
        if (stageRoomPrefabController == null)
            return null;

        StageShopOfferLayoutController layoutController = stageRoomPrefabController.ActiveShopOfferLayoutController;
        if (layoutController != null && shopController != null)
            return layoutController.RebuildOfferPoints(shopController.GetOfferCount());

        return stageRoomPrefabController.ActiveShopOfferPoints;
    }

    /// <summary>
    /// 处理商店房悬浮商品购买请求。
    /// </summary>
    /// <param name="offerIndex">商品索引。</param>
    private void TryBuyStageSceneShopOffer(int offerIndex)
    {
        if (!_stageSceneShopActive)
            return;

        TryBuyOffer(offerIndex);
        BindStageShopOfferPointsIfNeeded();
        RefreshCombatCanvasHud();
    }

    /// <summary>
    /// 在中转房内生成离开门，玩家交互后进入绑定的目标战斗房。
    /// </summary>
    /// <param name="entryOption">进入当前中转房时选择的门选项。</param>
    private void SpawnStageTransitExitDoor(RoomOptionData entryOption)
    {
        if (stageRoomDoorController == null || entryOption == null || _stageTransitExitDoorSpawned)
            return;

        RoomOptionData exitOption = CreateTransitExitOption(entryOption);
        stageRoomDoorController.SpawnDoors(new[] { exitOption }, OnStageTransitExitDoorSelected);
        _stageTransitExitDoorSpawned = true;
    }

    /// <summary>
    /// 创建中转房离开门显示数据。
    /// </summary>
    /// <param name="entryOption">进入当前中转房时选择的门选项。</param>
    /// <returns>离开门选项数据。</returns>
    private RoomOptionData CreateTransitExitOption(RoomOptionData entryOption)
    {
        RoomType targetRoomType = entryOption.TransitExitTargetCombatRoomType;
        string displayName = GetRoomTypeDisplayName(targetRoomType);
        string tooltip = $"离开{entryOption.DisplayName}，进入{displayName}。";
        return new RoomOptionData(targetRoomType, $"离开{entryOption.DisplayName}", tooltip, "door_exit", targetRoomType);
    }

    /// <summary>
    /// 获取房间类型的中文显示名。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>中文显示名。</returns>
    private string GetRoomTypeDisplayName(RoomType roomType)
    {
        switch (roomType)
        {
            case RoomType.CombatElite:
                return "精英战斗房";

            case RoomType.Boss:
                return "Boss 房";

            case RoomType.Shop:
                return "商店房";

            case RoomType.Event:
                return "事件房";

            case RoomType.Treasure:
                return "宝箱房";

            case RoomType.CombatNormal:
            default:
                return "普通战斗房";
        }
    }

    /// <summary>
    /// 处理中转房离开门选择。
    /// </summary>
    /// <param name="optionIndex">离开门索引。</param>
    /// <param name="roomOption">离开门绑定的目标战斗房选项。</param>
    private void OnStageTransitExitDoorSelected(int optionIndex, RoomOptionData roomOption)
    {
        if (!ShouldUseStageFlow() || !_stageTransitRoomActive || _stageRoomTransitionInProgress)
            return;

        if (roomTransitionController != null)
        {
            PlayStageTransitExitTransition();
            return;
        }

        ContinueAfterStageTransitExitWithoutTransition();
    }

    /// <summary>
    /// 播放中转房离开转场，并在遮满时进入目标战斗房。
    /// </summary>
    private void PlayStageTransitExitTransition()
    {
        _stageRoomTransitionInProgress = true;
        SetPlayerControlEnabled(false);
        if (stageRoomDoorController != null)
            stageRoomDoorController.ClearDoors();

        Vector3 transitionCenter = _player != null ? _player.transform.position : GetRoomSpawnPosition();
        roomTransitionController.PlayWorldTransition(
            transitionCenter,
            ContinueAfterStageTransitExitWithoutTransition,
            OnStageRoomTransitionCompleted);
    }

    /// <summary>
    /// 不播放转场时直接离开中转房并进入目标战斗房。
    /// </summary>
    private void ContinueAfterStageTransitExitWithoutTransition()
    {
        bool shouldCompleteStage = stageProgressController != null &&
            stageProgressController.ShouldCompleteStageAfterTransitExit();

        if (!shouldCompleteStage && stageProgressController != null)
            stageProgressController.ExitTransitRoomToTargetCombatRoom();

        ClearActiveStageFunctionObject();
        _stageTransitRoomActive = false;
        _stageSceneShopActive = false;
        _activeStageTransitRoomOption = null;
        _stageTransitExitDoorSpawned = false;
        if (stageRoomDoorController != null)
            stageRoomDoorController.ClearDoors();

        if (shouldCompleteStage)
        {
            EnterPostFloorState(GameStateManager.GameState.Victory);
            return;
        }

        StartNextStageCombatRoomAndResume();
    }

    /// <summary>
    /// 清理当前功能房中生成的功能对象。
    /// </summary>
    private void ClearActiveStageFunctionObject()
    {
        StageRoomFunctionObjectSpawner functionSpawner = stageRoomPrefabController != null
            ? stageRoomPrefabController.ActiveFunctionObjectSpawner
            : null;
        if (functionSpawner != null)
            functionSpawner.ClearActiveFunctionObject();
    }

    /// <summary>
    /// 在 Stage 强化 UI 选择完成后继续 Stage 战斗结束流程。
    /// </summary>
    /// <returns>当前处于 Stage 战斗结束流程并已接管后续时返回 true。</returns>
    private bool TryContinueStagePostCombatAfterChoice()
    {
        if (!ShouldUseStageFlow() || !_stagePostCombatPendingRoomOptions)
            return false;

        ContinueStagePostCombatFlow();
        return true;
    }

    /// <summary>
    /// 重置 Stage 战斗房运行状态，并确保主状态回到 Playing。
    /// </summary>
    private void StartNextStageCombatRoomAndResume()
    {
        StartNextStageCombatRoom();
        if (gameStateManager != null)
            gameStateManager.StartPlaying();
    }

    /// <summary>
    /// 判断当前是否启用有效 Stage 流程。
    /// </summary>
    /// <returns>Stage 流程可用时返回 true。</returns>
    private bool ShouldUseStageFlow()
    {
        return useStageFlow && stageProgressController != null && stageProgressController.HasStageData;
    }

    /// <summary>
    /// 判断当前 Stage 房间是否使用固定预算刷怪计划。
    /// </summary>
    /// <returns>普通或精英 Stage 战斗房返回 true。</returns>
    private bool ShouldUseStageSpawnPlan()
    {
        if (!ShouldUseStageFlow() || stageProgressController == null)
            return false;

        RoomType roomType = stageProgressController.CurrentRoomType;
        return roomType == RoomType.CombatNormal || roomType == RoomType.CombatElite;
    }

    /// <summary>
    /// 判断当前 Stage 房间是否使用 Boss 运行时。
    /// </summary>
    /// <returns>Stage Boss 房返回 true。</returns>
    private bool ShouldUseStageBossRuntime()
    {
        return ShouldUseStageFlow() && stageProgressController != null && stageProgressController.CurrentRoomType == RoomType.Boss;
    }

    /// <summary>
    /// 为当前 Stage 普通或精英战斗房准备固定预算刷怪计划。
    /// </summary>
    private void PrepareStageCombatRoomSpawnPlan()
    {
        if (enemySpawnController == null)
            return;

        if (!ShouldUseStageSpawnPlan())
        {
            enemySpawnController.ClearStageSpawnPlan();
            PrepareStageBossRoomRuntime();
            return;
        }

        enemySpawnController.ClearStageBossRuntime();
        EnemySpawnPlan spawnPlan = stageProgressController.CreateCurrentEnemySpawnPlan();
        enemySpawnController.BeginStageSpawnPlan(spawnPlan);
    }

    /// <summary>
    /// 为当前 Stage Boss 房准备 Boss 本体和技能运行时。
    /// </summary>
    private void PrepareStageBossRoomRuntime()
    {
        if (enemySpawnController == null)
            return;

        if (!ShouldUseStageBossRuntime())
        {
            enemySpawnController.ClearStageBossRuntime();
            return;
        }

        enemySpawnController.BeginStageBossRoom(CreateEnemySpawnRuntimeContextForStageBoss());
        StageRunState runState = stageProgressController != null ? stageProgressController.RunState : null;
        if (runState != null)
            runState.BeginBoss(Time.time);
    }

    /// <summary>
    /// 创建 Stage Boss 生成所需的敌人刷新上下文。
    /// </summary>
    /// <returns>敌人刷新上下文。</returns>
    private GameEnemySpawnController.EnemySpawnRuntimeContext CreateEnemySpawnRuntimeContextForStageBoss()
    {
        return new GameEnemySpawnController.EnemySpawnRuntimeContext
        {
            RuntimeObjectPoolController = runtimeObjectPoolController,
            ActiveRuntimeRoot = _activeRuntimeRoot,
            Player = _player,
            CurrentFloorData = floorProgressController != null ? floorProgressController.CurrentFloorData : null,
            ElapsedTime = _elapsedTime,
            GetArenaMin = GetArenaMin,
            GetArenaMax = GetArenaMax,
            ClampPositionInsideArena = ClampPositionInsideArena,
            UseStageBossRuntime = ShouldUseStageBossRuntime(),
            StageData = stageProgressController != null ? stageProgressController.StageData : null,
            StageBossData = stageProgressController != null ? stageProgressController.GetCurrentBossData() : null,
            StageCombatNode = stageProgressController != null ? stageProgressController.CurrentCombatNode : 1,
            StageRoomType = RoomType.Boss,
            CompleteStageCombatRoom = CompleteFloor
        };
    }

    /// <summary>
    /// 处理 Stage Boss 本体死亡奖励、召唤物清理和房间完成。
    /// </summary>
    /// <param name="boss">死亡的 Boss 本体。</param>
    /// <param name="hitInfo">造成击杀的伤害信息。</param>
    private void HandleStageBossKilled(GameEnemyController boss, DamageHitInfo hitInfo)
    {
        if (!ShouldUseStageBossRuntime() || boss == null)
            return;

        BossData bossData = stageProgressController != null ? stageProgressController.GetCurrentBossData() : null;
        if (bossData != null)
        {
            AddStageBossScore(bossData.KillScore);
            if (_player != null)
            {
                _player.AddGold(bossData.DirectRewardGold);
                _player.AddExperience(bossData.DirectRewardExperience);
            }

            AddGoldCollected(bossData.DirectRewardGold);
        }

        AddKill();
        if (hitInfo.CanTriggerKillEffects && weaponFireController != null)
            weaponFireController.AddEnergyPerKill(CreateWeaponFireStatsContext());

        StageRunState runState = stageProgressController != null ? stageProgressController.RunState : null;
        if (runState != null)
        {
            runState.CompleteBoss(Time.time);
            ApplyStageBossNoDamageBonus(bossData);
        }

        if (enemySpawnController != null)
        {
            enemySpawnController.ClearStageBossSummons(boss, ReleasePooledObject);
            enemySpawnController.ReleaseStageBoss(boss, ReleasePooledObject);
        }

        CompleteStageCombatRoom();
    }

    /// <summary>
    /// 累加 Stage Boss 本体分。
    /// </summary>
    /// <param name="score">Boss 本体得分。</param>
    private void AddStageBossScore(int score)
    {
        int safeScore = Mathf.Max(0, score);
        _score += safeScore;
        if (stageProgressController != null)
            stageProgressController.RunState.AddBossScore(safeScore);
    }

    /// <summary>
    /// 根据当前 Stage 普通或精英房表现结算清场、速度和无伤奖励。
    /// </summary>
    private void ApplyStageRoomScoreBonuses()
    {
        if (!ShouldUseStageFlow() || stageProgressController == null || ShouldUseStageBossRuntime())
            return;

        RoomType roomType = stageProgressController.CurrentRoomType;
        if (roomType != RoomType.CombatNormal && roomType != RoomType.CombatElite)
            return;

        StageRunState runState = stageProgressController.RunState;
        int roomKillScore = runState.CurrentRoomKillScore;
        if (roomKillScore <= 0)
            return;

        bool cleared = enemySpawnController != null && enemySpawnController.Enemies.Count <= 0;
        if (cleared)
            runState.SetCurrentRoomCleared(true);

        int clearBonus = cleared ? Mathf.RoundToInt(roomKillScore * 0.2f) : 0;
        int speedBonus = cleared ? CalculateStageRoomSpeedBonus(roomKillScore) : 0;
        int noDamageBonus = CalculateStageRoomNoDamageBonus(roomKillScore, runState.CurrentRoomDamageTakenCount);

        AddStageBonusScore(clearBonus, bonus => runState.AddClearBonusScore(bonus));
        AddStageBonusScore(speedBonus, bonus => runState.AddSpeedBonusScore(bonus));
        AddStageBonusScore(noDamageBonus, bonus => runState.AddNoDamageBonusScore(bonus));
    }

    /// <summary>
    /// 计算当前 Stage 普通或精英房速度奖励。
    /// </summary>
    /// <param name="roomKillScore">当前房间击杀分。</param>
    /// <returns>速度奖励分。</returns>
    private int CalculateStageRoomSpeedBonus(int roomKillScore)
    {
        if (floorProgressController == null || stageProgressController == null)
            return 0;

        float durationSeconds = GetStageCombatRoomDurationSeconds();
        float elapsedSeconds = floorProgressController.CurrentFloorElapsedSeconds;
        float remainingRatio = Mathf.Clamp01((durationSeconds - elapsedSeconds) / Mathf.Max(0.01f, durationSeconds));
        return Mathf.RoundToInt(roomKillScore * remainingRatio * 0.3f);
    }

    /// <summary>
    /// 计算当前 Stage 普通或精英房无伤奖励。
    /// </summary>
    /// <param name="roomKillScore">当前房间击杀分。</param>
    /// <param name="damageTakenCount">当前房间受伤次数。</param>
    /// <returns>无伤奖励分。</returns>
    private int CalculateStageRoomNoDamageBonus(int roomKillScore, int damageTakenCount)
    {
        if (damageTakenCount <= 0)
            return Mathf.RoundToInt(roomKillScore * 0.2f);

        if (damageTakenCount == 1)
            return Mathf.RoundToInt(roomKillScore * 0.1f);

        return 0;
    }

    /// <summary>
    /// 结算 Stage Boss 无伤奖励。
    /// </summary>
    /// <param name="bossData">Boss 配置。</param>
    private void ApplyStageBossNoDamageBonus(BossData bossData)
    {
        if (bossData == null || stageProgressController == null)
            return;

        StageRunState runState = stageProgressController.RunState;
        if (!runState.BossNoDamage)
            return;

        float bonusRate = bossData.IsFinalBoss ? 0.25f : 0.2f;
        int noDamageBonus = Mathf.RoundToInt(bossData.KillScore * bonusRate);
        AddStageBonusScore(noDamageBonus, bonus => runState.AddNoDamageBonusScore(bonus));
    }

    /// <summary>
    /// 累加 Stage 奖励分，并同步总分。
    /// </summary>
    /// <param name="score">奖励分。</param>
    /// <param name="addToStageState">写入 StageRunState 分项的回调。</param>
    private void AddStageBonusScore(int score, System.Action<int> addToStageState)
    {
        int safeScore = Mathf.Max(0, score);
        if (safeScore <= 0)
            return;

        _score += safeScore;
        addToStageState?.Invoke(safeScore);
    }

    /// <summary>
    /// 获取当前 Stage 普通或精英战斗房持续时间。
    /// </summary>
    /// <returns>当前 Stage 战斗房持续时间。</returns>
    private float GetStageCombatRoomDurationSeconds()
    {
        StageData stageData = stageProgressController != null ? stageProgressController.StageData : null;
        EnemySpawnProfile spawnProfile = stageData != null ? stageData.EnemySpawnProfile : null;
        return spawnProfile != null ? spawnProfile.DurationSeconds : 40f;
    }

    /// <summary>
    /// 判断指定房间类型是否为 Stage 战斗房。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>战斗房返回 true。</returns>
    private bool IsStageCombatRoom(RoomType roomType)
    {
        return roomType == RoomType.CombatNormal || roomType == RoomType.CombatElite || roomType == RoomType.Boss;
    }

    /// <summary>
    /// 判断指定房间类型是否为 Stage 中转房。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>中转房返回 true。</returns>
    private bool IsStageTransitRoom(RoomType roomType)
    {
        return roomType == RoomType.Shop || roomType == RoomType.Event || roomType == RoomType.Treasure;
    }

    /// <summary>
    /// 判断当前中转房是否使用功能对象生成器。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>Event 或 Treasure 返回 true。</returns>
    private bool IsStageFunctionRoom(RoomType roomType)
    {
        return roomType == RoomType.Event || roomType == RoomType.Treasure;
    }

    /// <summary>
    /// 根据待处理奖励、商店节奏和楼层进度进入下一个状态。
    /// </summary>
    private void EnterNextPostFloorState()
    {
        GameStateManager.GameState nextState = floorProgressController != null
            ? floorProgressController.GetNextPostFloorState(_player)
            : GameStateManager.GameState.Victory;
        EnterPostFloorState(nextState);
    }

    /// <summary>
    /// 按楼层流程控制器给出的结果切换到下一个状态。
    /// </summary>
    /// <param name="nextState">下一个流程状态。</param>
    private void EnterPostFloorState(GameStateManager.GameState nextState)
    {
        if (stateFlowController != null)
            stateFlowController.EnterPostFloorState(nextState);
    }

    /// <summary>
    /// 准备升级强化选项。
    /// </summary>
    private void PrepareUpgradeChoices()
    {
        if (upgradeController == null)
            return;

        upgradeController.PrepareUpgradeChoices(_player);
    }

    /// <summary>
    /// 尝试刷新当前升级强化选项。
    /// </summary>
    private void TryRefreshUpgradeOffers()
    {
        if (upgradeController == null || _player == null)
            return;

        upgradeController.TryRefreshUpgradeOffers(_player);
    }

    /// <summary>
    /// 选择一个升级强化选项。
    /// </summary>
    /// <param name="index">选项索引。</param>
    private void ChooseUpgradeOffer(int index)
    {
        if (upgradeController == null || _player == null)
            return;

        if (!upgradeController.TryChooseUpgradeOffer(index, _player))
            return;

        if (_player.PendingUpgradeChoices > 0)
            RefreshCombatCanvasLevelUp();

        if (TryContinueStagePostCombatAfterChoice())
            return;

        EnterNextPostFloorState();
    }

    /// <summary>
    /// 准备武器强化选项。
    /// </summary>
    private void PrepareWeaponUpgradeChoices()
    {
        if (weaponUpgradeController == null)
            return;

        weaponUpgradeController.PrepareWeaponUpgradeChoices(_currentWeaponData);
    }

    /// <summary>
    /// 选择一个武器强化选项。
    /// </summary>
    /// <param name="index">选项索引。</param>
    private void ChooseWeaponUpgradeOffer(int index)
    {
        if (weaponUpgradeController == null || _player == null)
            return;

        if (!weaponUpgradeController.TryChooseWeaponUpgradeOffer(index, _player, out WeaponUpgradeData selectedEntry))
            return;

        ApplyWeaponUpgradeData(selectedEntry);

        if (_player.PendingWeaponUpgradeChoices > 0)
            RefreshCombatCanvasWeaponUpgrade();

        if (TryContinueStagePostCombatAfterChoice())
            return;

        EnterNextPostFloorState();
    }

    /// <summary>
    /// 应用武器强化效果到当前武器运行时。
    /// </summary>
    /// <param name="upgradeData">武器强化 SO 配置。</param>
    private void ApplyWeaponUpgradeData(WeaponUpgradeData upgradeData)
    {
        if (upgradeData == null)
            return;

        upgradeData.Apply(CreateWeaponUpgradeApplyContext());
    }

    /// <summary>
    /// 创建武器强化应用上下文。
    /// </summary>
    /// <returns>武器强化应用上下文。</returns>
    private WeaponUpgradeApplyContext CreateWeaponUpgradeApplyContext()
    {
        return new WeaponUpgradeApplyContext(
            _player,
            projectileCombatController,
            weaponCombatController,
            weaponFireController,
            _currentWeaponData);
    }

    /// <summary>
    /// 准备当前商店商品。
    /// </summary>
    private void PrepareShop()
    {
        if (shopController == null || _player == null)
            return;

        if (!shopController.IsPrepared)
            _player.ResetShopRefreshCount();

        shopController.PrepareShop(GetCurrentFloorIndex(), CanAcquireRelic);
    }

    /// <summary>
    /// 尝试购买指定商品。
    /// </summary>
    /// <param name="index">商品索引。</param>
    private void TryBuyOffer(int index)
    {
        if (shopController == null || _player == null)
            return;

        if (shopController.TryBuyOffer(index, _player, out GameShopController.ShopOffer offer, CanAcquireRelic))
        {
            AddShopPurchase(offer);
            BindStageShopOfferPointsIfNeeded();
        }
    }

    /// <summary>
    /// 尝试消耗次数并刷新当前商店商品。
    /// </summary>
    private void TryRefreshShop()
    {
        if (shopController == null || _player == null)
            return;

        if (shopController.TryRefreshShop(GetCurrentFloorIndex(), _player, CanAcquireRelic))
            BindStageShopOfferPointsIfNeeded();
    }

    /// <summary>
    /// 切换指定商店商品的锁定状态。
    /// </summary>
    /// <param name="index">商品索引。</param>
    private void ToggleShopOfferLock(int index)
    {
        if (shopController == null)
            return;

        shopController.ToggleOfferLock(index);
        BindStageShopOfferPointsIfNeeded();
    }

    /// <summary>
    /// 记录商店购买的道具。
    /// </summary>
    /// <param name="offer">商品数据。</param>
    private void AddShopPurchase(GameShopController.ShopOffer offer)
    {
        if (offer.ItemType == ShopItemData.ShopItemType.Relic)
        {
            ApplyPurchasedRelic(offer.RelicData);
            _ownedItems.Add(GetPurchasedRelicName(offer));
            return;
        }

        ApplyPurchasedItem(offer.ItemData);
        _ownedItems.Add(GetPurchasedItemName(offer));
    }

    /// <summary>
    /// 应用购买道具的一次性效果。
    /// </summary>
    /// <param name="itemData">道具数据。</param>
    private void ApplyPurchasedItem(ItemData itemData)
    {
        if (itemData == null)
            return;

        ItemData.ItemEffectEntry[] effects = itemData.Effects;
        if (effects == null)
            return;

        for (int i = 0; i < effects.Length; i++)
        {
            ApplyPurchasedItemEffect(effects[i]);
        }
    }

    /// <summary>
    /// 应用单条购买道具数值效果。
    /// </summary>
    /// <param name="effect">道具效果。</param>
    private void ApplyPurchasedItemEffect(ItemData.ItemEffectEntry effect)
    {
        if (effect == null)
            return;

        switch (effect.EffectType)
        {
            case ItemData.ItemEffectType.MaxHealth:
                _player.AddMaxHealth(Mathf.RoundToInt(effect.Value));
                break;

            case ItemData.ItemEffectType.AddedAttackDamage:
                _player.AddWeaponAttackDamage(Mathf.RoundToInt(effect.Value));
                break;

            case ItemData.ItemEffectType.AttackDamageMultiplier:
                _player.AddAttackDamageMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.MoveSpeedMultiplier:
                _player.AddMoveSpeedMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.ExperienceMultiplier:
                _player.AddExperienceMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.CriticalRate:
                _player.AddCriticalRate(effect.Value);
                break;

            case ItemData.ItemEffectType.CriticalDamageMultiplier:
                _player.AddCriticalDamage(effect.Value);
                break;

            case ItemData.ItemEffectType.ProjectileScaleMultiplier:
                _player.AddProjectileScaleMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.ProjectileSpeedMultiplier:
                _player.AddProjectileSpeedMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.ProjectileLifeTimeMultiplier:
                _player.AddProjectileLifeTimeMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.MagazineCapacityMultiplier:
                _player.AddMagazineCapacityMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.ReloadSpeedMultiplier:
                _player.AddReloadSpeedMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.AccuracyMultiplier:
                _player.AddAccuracyMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.RecoilControlMultiplier:
                _player.AddRecoilControlMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.ImpactMultiplier:
                _player.AddImpactMultiplier(effect.Value);
                break;

            case ItemData.ItemEffectType.AttackSpeedMultiplier:
                _player.AddAttackSpeedMultiplier(effect.Value);
                break;

        }
    }

    /// <summary>
    /// 应用购买道具的被动效果。
    /// </summary>
    /// <param name="relicData">道具数据。</param>
    private void ApplyPurchasedRelic(RelicData relicData)
    {
        if (relicData == null)
            return;

        if (relicRuntimeController != null)
        {
            if (!relicRuntimeController.TryAcquireRelic(relicData))
                return;

            return;
        }

        Debug.LogError($"[Relic] 遗物运行时未初始化，无法注册遗物：{relicData.RelicId}");
    }

    /// <summary>
    /// 判断遗物是否可以进入商店候选池。
    /// </summary>
    /// <param name="relicData">遗物配置。</param>
    /// <returns>可以获得时返回 true。</returns>
    private bool CanAcquireRelic(RelicData relicData)
    {
        return relicRuntimeController != null && relicRuntimeController.CanAcquireRelic(relicData);
    }

    /// <summary>
    /// 获取购买后记录的道具名称。
    /// </summary>
    /// <param name="offer">已购买商品。</param>
    /// <returns>道具显示名称。</returns>
    private string GetPurchasedItemName(GameShopController.ShopOffer offer)
    {
        if (offer.ItemData != null)
            return offer.ItemData.DisplayName;

        return offer.Name;
    }

    /// <summary>
    /// 获取购买后记录的道具名称。
    /// </summary>
    /// <param name="offer">已购买商品。</param>
    /// <returns>道具显示名称。</returns>
    private string GetPurchasedRelicName(GameShopController.ShopOffer offer)
    {
        if (offer.RelicData != null)
            return offer.RelicData.DisplayName;

        return offer.Name;
    }

    /// <summary>
    /// 创建带 SpriteRenderer 的对象。
    /// </summary>
    /// <param name="objectName">对象名称。</param>
    /// <param name="position">对象位置。</param>
    /// <param name="scale">对象缩放。</param>
    /// <param name="color">显示颜色。</param>
    /// <param name="sprite">显示 Sprite。</param>
    /// <returns>创建的游戏对象。</returns>
    private GameObject CreateSpriteObject(string objectName, Vector3 position, Vector3 scale, Color color, Sprite sprite)
    {
        GameObject targetObject = new GameObject(objectName);
        targetObject.transform.position = position;
        targetObject.transform.localScale = scale;

        SpriteRenderer renderer = targetObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;

        return targetObject;
    }

    /// <summary>
    /// 回收池对象；非池对象按旧逻辑销毁。
    /// </summary>
    /// <param name="targetObject">目标对象。</param>
    private void ReleasePooledObject(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        if (runtimeObjectPoolController != null)
        {
            runtimeObjectPoolController.ReleaseObject(targetObject);
            return;
        }

        GamePooledObject pooledObject = targetObject.GetComponent<GamePooledObject>();
        if (pooledObject != null)
            pooledObject.Release();
        else
            Destroy(targetObject);
    }

    /// <summary>
    /// 创建白色圆形 Sprite。
    /// </summary>
    /// <param name="size">贴图尺寸。</param>
    /// <returns>圆形 Sprite。</returns>
    private Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// 根据当前流程状态刷新 Canvas UI。
    /// </summary>
    private void RefreshCombatCanvasByState()
    {
        if (combatUiPresenter != null)
            combatUiPresenter.RefreshByState(CreateCombatUiContext());
    }

    /// <summary>
    /// 刷新 Canvas 战斗 HUD。
    /// </summary>
    private void RefreshCombatCanvasHud()
    {
        if (combatUiPresenter != null)
            combatUiPresenter.RefreshHud(CreateCombatUiContext());
    }

    /// <summary>
    /// 刷新 Canvas 升级强化界面。
    /// </summary>
    private void RefreshCombatCanvasLevelUp()
    {
        PrepareUpgradeChoices();
        if (combatUiPresenter != null)
            combatUiPresenter.RefreshLevelUp(CreateCombatUiContext());
    }

    /// <summary>
    /// 刷新 Canvas 武器强化界面。
    /// </summary>
    private void RefreshCombatCanvasWeaponUpgrade()
    {
        PrepareWeaponUpgradeChoices();
        if (combatUiPresenter != null)
            combatUiPresenter.RefreshWeaponUpgrade(CreateCombatUiContext());
    }

    /// <summary>
    /// 刷新 Canvas 商店界面。
    /// </summary>
    private void RefreshCombatCanvasShop()
    {
        PrepareShop();
        if (combatUiPresenter != null)
            combatUiPresenter.RefreshShop(CreateCombatUiContext());
    }

    /// <summary>
    /// 刷新 Canvas 结算界面。
    /// </summary>
    private void RefreshCombatCanvasResult()
    {
        if (combatUiPresenter != null)
            combatUiPresenter.RefreshResult(CreateCombatUiContext());
    }

    /// <summary>
    /// 刷新 Canvas 暂停界面。
    /// </summary>
    private void RefreshCombatCanvasPause()
    {
        if (combatUiPresenter != null)
            combatUiPresenter.RefreshPause(CreateCombatUiContext());
    }

    /// <summary>
    /// 创建当前战斗 UI 刷新上下文。
    /// </summary>
    /// <returns>战斗 UI 刷新上下文。</returns>
    private GameCombatUiPresenter.CombatUiContext CreateCombatUiContext()
    {
        return new GameCombatUiPresenter.CombatUiContext
        {
            CanvasController = combatCanvasController,
            StateManager = gameStateManager,
            Player = _player,
            FloorProgressController = floorProgressController,
            StageRunState = ShouldUseStageFlow() && stageProgressController != null ? stageProgressController.RunState : null,
            WeaponFireController = weaponFireController,
            UpgradeController = upgradeController,
            WeaponUpgradeController = weaponUpgradeController,
            ShopController = shopController,
            OwnedItems = _ownedItems,
            Kills = _kills,
            TotalKills = _totalKills,
            Score = _score,
            TotalGoldCollected = _totalGoldCollected,
            MagazineCapacity = GetCurrentMagazineCapacity(),
            ShopRefreshCount = _player != null ? _player.ShopRefreshCount : 0,
            WeaponInstanceCount = GetCurrentWeaponInstanceCount(),
            ProjectileCount = GetCurrentProjectileCount(),
            ProjectilePierce = GetCurrentProjectilePierce(),
            ProjectileBounce = GetCurrentProjectileBounce(),
            ElapsedTime = _elapsedTime,
            MaxEnergy = GetCurrentMaxEnergy(),
            ChargeDuration = GetCurrentChargeDuration(),
            ReloadDuration = GetCurrentReloadDuration(),
            AttackSpeedMultiplier = GetCurrentAttackSpeedMultiplier(),
            ProjectileSpeedMultiplier = GetCurrentProjectileSpeedMultiplier(),
            CriticalRate = GetCurrentCriticalRate(),
            CriticalDamage = GetCurrentCriticalDamage(),
            CharacterName = GetCurrentCharacterName(),
            WeaponName = GetCurrentWeaponName()
        };
    }

    /// <summary>
    /// 退出游戏，编辑器中只输出日志。
    /// </summary>
    private void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("[Combat] 退出游戏。");
#else
        Application.Quit();
#endif
    }
}
