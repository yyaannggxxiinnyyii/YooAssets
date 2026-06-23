using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理战斗中的敌人列表、刷新计时、敌人生成和敌人回收。
/// </summary>
public sealed class GameEnemySpawnController : MonoBehaviour
{
    /// <summary>
    /// 敌人刷新运行时依赖上下文。
    /// </summary>
    public struct EnemySpawnRuntimeContext
    {
        public GameRuntimeObjectPoolController RuntimeObjectPoolController;
        public Transform ActiveRuntimeRoot;
        public GamePlayerController Player;
        public FloorData CurrentFloorData;
        public float ElapsedTime;
        public Func<Vector2> GetArenaMin;
        public Func<Vector2> GetArenaMax;
        public Func<Vector3, Vector3> ClampPositionInsideArena;
        public bool UseStageSpawnPlan;
        public bool UseStageBossRuntime;
        public StageData StageData;
        public BossData StageBossData;
        public int StageCombatNode;
        public RoomType StageRoomType;
        public Action CompleteStageCombatRoom;
    }

    [Header("刷新配置")]
    [Tooltip("默认敌人生成间隔，楼层数据未覆盖时使用。")]
    [SerializeField] private float spawnInterval = 1.2f;
    [Tooltip("敌人随机生成时与玩家保持的最小距离，避免直接刷在玩家身边。")]
    [SerializeField] private float minSpawnDistanceFromPlayer = 5f;
    [Tooltip("敌人随机生成时距离房间边界的最小留白。")]
    [SerializeField] private float spawnBoundaryPadding = 1.2f;
    [Tooltip("敌人生成点检测阻挡重叠时使用的半径。")]
    [SerializeField] private float spawnOverlapRadius = 0.55f;
    [Tooltip("敌人生成点不能重叠的阻挡层，例如 SolidObstacle。为空时不检测阻挡重叠。")]
    [SerializeField] private LayerMask spawnObstacleMask;
    [Tooltip("每次敌人生成最多尝试随机安全点的次数。")]
    [SerializeField] private int maxSpawnPositionAttempts = 32;

    private readonly List<GameEnemyController> _enemies = new List<GameEnemyController>();
    private float _spawnTimer;
    private EnemySpawnPlan _stageSpawnPlan;
    private int _nextStageWaveIndex;
    private float _stageRoomElapsedTime;
    private float _lastStageEarlySpawnTime = -999f;
    private bool _stagePlanCompleted;
    private GameEnemyController _stageBoss;
    private BossData _stageBossData;
    private BossSkillRuntimeState[] _stageBossSkillStates = new BossSkillRuntimeState[0];
    private float _stageBossElapsedTime;

    /// <summary>
    /// 当前存活敌人列表。
    /// </summary>
    public List<GameEnemyController> Enemies => _enemies;

    /// <summary>
    /// 重置敌人刷新计时。
    /// </summary>
    public void ResetSpawnTimer()
    {
        _spawnTimer = 0f;
    }

    /// <summary>
    /// 使用指定刷怪计划开始 Stage 普通或精英战斗房刷怪。
    /// </summary>
    /// <param name="spawnPlan">战斗房进入时生成的刷怪计划。</param>
    public void BeginStageSpawnPlan(EnemySpawnPlan spawnPlan)
    {
        _stageSpawnPlan = spawnPlan;
        _nextStageWaveIndex = 0;
        int planEnemyCount = spawnPlan != null ? spawnPlan.CountTotalEnemies() : 0;
        _stageRoomElapsedTime = 0f;
        _lastStageEarlySpawnTime = 0f;
        _stagePlanCompleted = planEnemyCount <= 0;
        _spawnTimer = 0f;
    }

    /// <summary>
    /// 清理 Stage 刷怪计划运行状态，恢复旧楼层刷怪模式。
    /// </summary>
    public void ClearStageSpawnPlan()
    {
        _stageSpawnPlan = null;
        _nextStageWaveIndex = 0;
        _stageRoomElapsedTime = 0f;
        _lastStageEarlySpawnTime = -999f;
        _stagePlanCompleted = false;
    }

    /// <summary>
    /// 开始 Stage Boss 房运行时，生成 Boss 本体并初始化技能状态。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    public void BeginStageBossRoom(EnemySpawnRuntimeContext runtimeContext)
    {
        ClearStageSpawnPlan();
        ClearStageBossRuntime();

        _stageBossData = runtimeContext.StageBossData;
        _stageBossElapsedTime = 0f;
        _stageBossSkillStates = CreateBossSkillStates(_stageBossData);
        if (_stageBossData == null)
        {
            Debug.LogError("[StageBoss] BossData 为空，Boss 房无法生成 Boss。请检查 StageData.Bosses 配置。");
            return;
        }

        _stageBoss = SpawnBossEnemy(runtimeContext, _stageBossData.BossEnemyData, false);
        if (_stageBoss == null)
        {
            Debug.LogError($"[StageBoss] Boss {_stageBossData.DisplayName} 缺少可生成的 EnemyData，请检查 BossData.BossEnemyData。");
            return;
        }

        TriggerBossSkills(runtimeContext, BossSkillTriggerType.OnStart);
    }

    /// <summary>
    /// 清理 Stage Boss 房运行状态。
    /// </summary>
    public void ClearStageBossRuntime()
    {
        _stageBoss = null;
        _stageBossData = null;
        _stageBossSkillStates = new BossSkillRuntimeState[0];
        _stageBossElapsedTime = 0f;
    }

    /// <summary>
    /// 清理敌人状态和刷新计时，不释放场景对象。
    /// </summary>
    public void ResetEnemyState()
    {
        _spawnTimer = 0f;
        _enemies.Clear();
        ClearStageSpawnPlan();
        ClearStageBossRuntime();
    }

    /// <summary>
    /// 更新敌人刷新计时并按楼层配置生成敌人。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    public void TickSpawn(EnemySpawnRuntimeContext runtimeContext)
    {
        if (runtimeContext.UseStageBossRuntime)
        {
            TickStageBossRuntime(runtimeContext);
            return;
        }

        if (runtimeContext.UseStageSpawnPlan)
        {
            TickStageSpawnPlan(runtimeContext);
            return;
        }

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f)
            return;

        if (_enemies.Count >= GetCurrentMaxAliveEnemies(runtimeContext.CurrentFloorData))
        {
            _spawnTimer = 0.2f;
            return;
        }

        _spawnTimer = Mathf.Max(0.35f, GetCurrentSpawnInterval(runtimeContext.CurrentFloorData) - runtimeContext.ElapsedTime * 0.01f);
        SpawnEnemy(runtimeContext);
    }

    /// <summary>
    /// 释放并清空当前所有敌人。
    /// </summary>
    /// <param name="releaseObject">对象释放回调。</param>
    public void ClearEnemies(Action<GameObject> releaseObject)
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            if (_enemies[i] != null && releaseObject != null)
                releaseObject.Invoke(_enemies[i].gameObject);
        }

        _enemies.Clear();
    }

    /// <summary>
    /// 释放并移除指定索引敌人。
    /// </summary>
    /// <param name="enemyIndex">敌人列表索引。</param>
    /// <param name="enemy">敌人组件。</param>
    /// <param name="releaseObject">对象释放回调。</param>
    public void ReleaseEnemyAt(int enemyIndex, GameEnemyController enemy, Action<GameObject> releaseObject)
    {
        if (enemy != null && releaseObject != null)
            releaseObject.Invoke(enemy.gameObject);

        if (enemyIndex >= 0 && enemyIndex < _enemies.Count)
            _enemies.RemoveAt(enemyIndex);
    }

    /// <summary>
    /// 清理指定 Boss 本体以外的 Stage Boss 召唤物。
    /// </summary>
    /// <param name="boss">Boss 本体。</param>
    /// <param name="releaseObject">对象释放回调。</param>
    public void ClearStageBossSummons(GameEnemyController boss, Action<GameObject> releaseObject)
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            GameEnemyController enemy = _enemies[i];
            if (enemy == null || enemy == boss || !enemy.IsStageBossSummon)
                continue;

            if (releaseObject != null)
                releaseObject.Invoke(enemy.gameObject);

            _enemies.RemoveAt(i);
        }
    }

    /// <summary>
    /// 释放并移除 Stage Boss 本体。
    /// </summary>
    /// <param name="boss">Boss 本体。</param>
    /// <param name="releaseObject">对象释放回调。</param>
    public void ReleaseStageBoss(GameEnemyController boss, Action<GameObject> releaseObject)
    {
        int enemyIndex = _enemies.IndexOf(boss);
        ReleaseEnemyAt(enemyIndex, boss, releaseObject);
        ClearStageBossRuntime();
    }

    /// <summary>
    /// 推进 Stage Boss 技能运行时。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    private void TickStageBossRuntime(EnemySpawnRuntimeContext runtimeContext)
    {
        if (_stageBoss == null || _stageBoss.IsDead)
            return;

        _stageBossElapsedTime += Time.deltaTime;
        TriggerBossSkills(runtimeContext, BossSkillTriggerType.OnHealthPercent);
        TriggerBossSkills(runtimeContext, BossSkillTriggerType.OnCooldown);
    }

    /// <summary>
    /// 推进 Stage 固定预算刷怪计划，并在计划刷完且清场后提前结束战斗房。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    private void TickStageSpawnPlan(EnemySpawnRuntimeContext runtimeContext)
    {
        if (_stageSpawnPlan == null)
            BeginStageSpawnPlan(null);

        _stageRoomElapsedTime += Time.deltaTime;
        TrySpawnDueStageWaves(runtimeContext);
        TrySpawnEarlyStageWave(runtimeContext);

        if (_stagePlanCompleted && _enemies.Count <= 0)
            runtimeContext.CompleteStageCombatRoom?.Invoke();
    }

    /// <summary>
    /// 生成所有已达到计划时间的 Stage 刷怪批次。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    private void TrySpawnDueStageWaves(EnemySpawnRuntimeContext runtimeContext)
    {
        EnemySpawnWavePlan[] waves = _stageSpawnPlan != null ? _stageSpawnPlan.Waves : null;
        if (waves == null || waves.Length <= 0)
        {
            _stagePlanCompleted = true;
            return;
        }

        EnemySpawnProfile spawnProfile = runtimeContext.StageData != null ? runtimeContext.StageData.EnemySpawnProfile : null;
        float spawnCutoffSeconds = spawnProfile != null ? spawnProfile.SpawnCutoffSeconds : float.MaxValue;
        while (_nextStageWaveIndex < waves.Length)
        {
            EnemySpawnWavePlan nextWave = waves[_nextStageWaveIndex];
            float waveTime = nextWave != null ? nextWave.SpawnTimeSeconds : 0f;
            bool reachedTime = _stageRoomElapsedTime >= waveTime;
            bool reachedCutoff = _stageRoomElapsedTime >= spawnCutoffSeconds;
            if (!reachedTime && !reachedCutoff)
                break;

            if (!reachedCutoff && IsStageSoftAliveLimitReached(runtimeContext))
                break;

            if (!TrySpawnStageWave(runtimeContext, nextWave))
                break;

            _nextStageWaveIndex++;
            if (reachedCutoff)
                continue;
        }

        _stagePlanCompleted = _nextStageWaveIndex >= waves.Length;
    }

    /// <summary>
    /// 在场上敌人低于阈值时提前生成下一批 Stage 敌人。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    private void TrySpawnEarlyStageWave(EnemySpawnRuntimeContext runtimeContext)
    {
        EnemySpawnWavePlan[] waves = _stageSpawnPlan != null ? _stageSpawnPlan.Waves : null;
        if (waves == null || _nextStageWaveIndex >= waves.Length)
            return;

        EnemySpawnProfile spawnProfile = runtimeContext.StageData != null ? runtimeContext.StageData.EnemySpawnProfile : null;
        if (spawnProfile == null)
            return;

        if (_enemies.Count > spawnProfile.EarlySpawnEnemyThreshold)
            return;

        if (_stageRoomElapsedTime - _lastStageEarlySpawnTime < spawnProfile.EarlySpawnMinIntervalSeconds)
            return;

        if (!TrySpawnStageWave(runtimeContext, waves[_nextStageWaveIndex]))
            return;

        _lastStageEarlySpawnTime = _stageRoomElapsedTime;
        _nextStageWaveIndex++;
        _stagePlanCompleted = _nextStageWaveIndex >= waves.Length;
    }

    /// <summary>
    /// 尝试生成一个 Stage 刷怪批次，达到硬上限时会延后。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="wave">刷怪批次。</param>
    /// <returns>成功处理批次时返回 true；需要延后时返回 false。</returns>
    private bool TrySpawnStageWave(EnemySpawnRuntimeContext runtimeContext, EnemySpawnWavePlan wave)
    {
        if (wave == null)
            return true;

        EnemySpawnProfile spawnProfile = runtimeContext.StageData != null ? runtimeContext.StageData.EnemySpawnProfile : null;
        int hardLimit = spawnProfile != null ? spawnProfile.HardAliveLimit : int.MaxValue;
        int waveEnemyCount = wave.CountTotalEnemies();
        if (_enemies.Count + waveEnemyCount > hardLimit)
            return false;

        EnemySpawnPlanEntry[] entries = wave.Entries;
        if (entries == null || entries.Length <= 0)
            return true;

        for (int i = 0; i < entries.Length; i++)
        {
            EnemySpawnPlanEntry entry = entries[i];
            if (entry == null || entry.Count <= 0)
                continue;

            for (int count = 0; count < entry.Count; count++)
            {
                if (_enemies.Count >= hardLimit)
                    return false;

                SpawnStageEnemy(runtimeContext, entry.EnemyData);
            }
        }

        return true;
    }

    /// <summary>
    /// 按 Stage 难度倍率生成一个计划敌人。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="enemyData">敌人数据。</param>
    private void SpawnStageEnemy(EnemySpawnRuntimeContext runtimeContext, EnemyData enemyData)
    {
        float healthMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateHealthMultiplier(runtimeContext.StageCombatNode, runtimeContext.StageRoomType)
            : 1f;
        float speedMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateSpeedMultiplier(runtimeContext.StageCombatNode, runtimeContext.StageRoomType)
            : 1f;
        float damageMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateDamageMultiplier(runtimeContext.StageCombatNode, runtimeContext.StageRoomType)
            : 1f;
        float goldMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateGoldMultiplier(runtimeContext.StageCombatNode, runtimeContext.StageRoomType)
            : 1f;
        float experienceMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateExperienceMultiplier(runtimeContext.StageCombatNode, runtimeContext.StageRoomType)
            : 1f;

        SpawnEnemy(runtimeContext, enemyData, healthMultiplier, speedMultiplier, damageMultiplier, goldMultiplier, experienceMultiplier);
    }

    /// <summary>
    /// 生成 Stage Boss 本体或召唤物。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="enemyData">敌人数据。</param>
    /// <param name="isSummon">是否为 Boss 召唤物。</param>
    /// <returns>生成出的敌人控制器。</returns>
    private GameEnemyController SpawnBossEnemy(EnemySpawnRuntimeContext runtimeContext, EnemyData enemyData, bool isSummon)
    {
        float healthMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateHealthMultiplier(runtimeContext.StageCombatNode, RoomType.Boss)
            : 1f;
        float speedMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateSpeedMultiplier(runtimeContext.StageCombatNode, RoomType.Boss)
            : 1f;
        float damageMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateDamageMultiplier(runtimeContext.StageCombatNode, RoomType.Boss)
            : 1f;
        float goldMultiplier = isSummon && runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateGoldMultiplier(runtimeContext.StageCombatNode, RoomType.Boss)
            : 0f;
        float experienceMultiplier = isSummon && runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateExperienceMultiplier(runtimeContext.StageCombatNode, RoomType.Boss)
            : 0f;
        GameEnemyController enemy = SpawnEnemy(
            runtimeContext,
            enemyData,
            healthMultiplier,
            speedMultiplier,
            damageMultiplier,
            goldMultiplier,
            experienceMultiplier,
            CreateBossSpawnPosition(runtimeContext));
        if (enemy == null)
            return null;

        enemy.SetStageBossRuntimeFlags(!isSummon, isSummon);
        return enemy;
    }

    /// <summary>
    /// 触发指定类型的 Boss 技能。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="triggerType">触发类型。</param>
    private void TriggerBossSkills(EnemySpawnRuntimeContext runtimeContext, BossSkillTriggerType triggerType)
    {
        if (_stageBossData == null || _stageBossSkillStates == null)
            return;

        BossSkillData[] skills = _stageBossData.Skills;
        if (skills == null)
            return;

        for (int i = 0; i < skills.Length; i++)
        {
            BossSkillData skillData = skills[i];
            if (skillData == null || skillData.TriggerType != triggerType)
                continue;

            BossSkillRuntimeState state = i < _stageBossSkillStates.Length ? _stageBossSkillStates[i] : null;
            if (!CanTriggerBossSkill(skillData, state, triggerType))
                continue;

            ExecuteBossSkill(runtimeContext, skillData);
            if (state != null)
            {
                state.HasTriggered = true;
                state.NextCooldownTime = _stageBossElapsedTime + skillData.CooldownSeconds;
            }
        }
    }

    /// <summary>
    /// 判断 Boss 技能当前是否满足触发条件。
    /// </summary>
    /// <param name="skillData">Boss 技能配置。</param>
    /// <param name="state">Boss 技能运行时状态。</param>
    /// <param name="triggerType">触发类型。</param>
    /// <returns>满足条件时返回 true。</returns>
    private bool CanTriggerBossSkill(BossSkillData skillData, BossSkillRuntimeState state, BossSkillTriggerType triggerType)
    {
        if (skillData == null || state == null)
            return false;

        if (skillData.TriggerOnce && state.HasTriggered)
            return false;

        if (triggerType == BossSkillTriggerType.OnCooldown)
            return _stageBossElapsedTime >= state.NextCooldownTime;

        if (triggerType == BossSkillTriggerType.OnHealthPercent)
            return _stageBoss != null && _stageBoss.HealthPercent <= skillData.HealthPercentThreshold;

        return triggerType == BossSkillTriggerType.OnStart;
    }

    /// <summary>
    /// 执行 Boss 技能效果。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="skillData">Boss 技能配置。</param>
    private void ExecuteBossSkill(EnemySpawnRuntimeContext runtimeContext, BossSkillData skillData)
    {
        if (skillData == null || skillData.EffectType != BossSkillEffectType.SummonEnemies)
            return;

        BossSummonEntry[] summonEntries = skillData.SummonEnemies;
        if (summonEntries == null)
            return;

        for (int i = 0; i < summonEntries.Length; i++)
        {
            BossSummonEntry summonEntry = summonEntries[i];
            if (summonEntry == null || summonEntry.EnemyData == null)
                continue;

            for (int count = 0; count < summonEntry.Count; count++)
                SpawnBossSummon(runtimeContext, summonEntry.EnemyData, skillData.SummonPositionRule);
        }
    }

    /// <summary>
    /// 生成一个 Boss 召唤物。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="enemyData">召唤敌人数据。</param>
    /// <param name="positionRule">召唤位置规则。</param>
    private void SpawnBossSummon(EnemySpawnRuntimeContext runtimeContext, EnemyData enemyData, BossSummonPositionRule positionRule)
    {
        float healthMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateHealthMultiplier(runtimeContext.StageCombatNode, RoomType.Boss)
            : 1f;
        float speedMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateSpeedMultiplier(runtimeContext.StageCombatNode, RoomType.Boss)
            : 1f;
        float damageMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateDamageMultiplier(runtimeContext.StageCombatNode, RoomType.Boss)
            : 1f;
        float goldMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateGoldMultiplier(runtimeContext.StageCombatNode, RoomType.Boss)
            : 1f;
        float experienceMultiplier = runtimeContext.StageData != null
            ? runtimeContext.StageData.CalculateExperienceMultiplier(runtimeContext.StageCombatNode, RoomType.Boss)
            : 1f;
        GameEnemyController summon = SpawnEnemy(
            runtimeContext,
            enemyData,
            healthMultiplier,
            speedMultiplier,
            damageMultiplier,
            goldMultiplier,
            experienceMultiplier,
            CreateBossSummonPosition(runtimeContext, positionRule));
        if (summon != null)
            summon.SetStageBossRuntimeFlags(false, true);
    }

    /// <summary>
    /// 创建 Boss 技能运行时状态数组。
    /// </summary>
    /// <param name="bossData">Boss 配置。</param>
    /// <returns>技能运行时状态数组。</returns>
    private BossSkillRuntimeState[] CreateBossSkillStates(BossData bossData)
    {
        BossSkillData[] skills = bossData != null ? bossData.Skills : null;
        if (skills == null || skills.Length <= 0)
            return new BossSkillRuntimeState[0];

        BossSkillRuntimeState[] states = new BossSkillRuntimeState[skills.Length];
        for (int i = 0; i < states.Length; i++)
        {
            BossSkillData skillData = skills[i];
            states[i] = new BossSkillRuntimeState
            {
                NextCooldownTime = skillData != null && skillData.TriggerType == BossSkillTriggerType.OnCooldown
                    ? skillData.CooldownSeconds
                    : 0f
            };
        }

        return states;
    }

    /// <summary>
    /// 获取 Boss 本体生成位置。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <returns>Boss 生成位置。</returns>
    private Vector3 CreateBossSpawnPosition(EnemySpawnRuntimeContext runtimeContext)
    {
        Vector3 position = runtimeContext.Player != null
            ? runtimeContext.Player.transform.position + Vector3.up * 6f
            : Vector3.zero;
        return runtimeContext.ClampPositionInsideArena != null
            ? runtimeContext.ClampPositionInsideArena.Invoke(position)
            : position;
    }

    /// <summary>
    /// 根据技能规则获取 Boss 召唤物生成位置。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="positionRule">召唤位置规则。</param>
    /// <returns>召唤物生成位置。</returns>
    private Vector3 CreateBossSummonPosition(EnemySpawnRuntimeContext runtimeContext, BossSummonPositionRule positionRule)
    {
        Vector3 center = GetBossSummonCenter(runtimeContext, positionRule);
        Vector2 offset = UnityEngine.Random.insideUnitCircle.normalized;
        if (offset.sqrMagnitude < 0.01f)
            offset = Vector2.right;

        Vector3 position = center + new Vector3(offset.x, offset.y, 0f) * UnityEngine.Random.Range(1.8f, 3.2f);
        return runtimeContext.ClampPositionInsideArena != null
            ? runtimeContext.ClampPositionInsideArena.Invoke(position)
            : position;
    }

    /// <summary>
    /// 获取 Boss 召唤中心点。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="positionRule">召唤位置规则。</param>
    /// <returns>召唤中心点。</returns>
    private Vector3 GetBossSummonCenter(EnemySpawnRuntimeContext runtimeContext, BossSummonPositionRule positionRule)
    {
        if (positionRule == BossSummonPositionRule.AroundPlayer && runtimeContext.Player != null)
            return runtimeContext.Player.transform.position;

        if (_stageBoss != null)
            return _stageBoss.transform.position;

        return runtimeContext.Player != null ? runtimeContext.Player.transform.position : Vector3.zero;
    }

    /// <summary>
    /// 判断 Stage 当前场上敌人是否达到软上限。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <returns>达到软上限时返回 true。</returns>
    private bool IsStageSoftAliveLimitReached(EnemySpawnRuntimeContext runtimeContext)
    {
        EnemySpawnProfile spawnProfile = runtimeContext.StageData != null ? runtimeContext.StageData.EnemySpawnProfile : null;
        return spawnProfile != null && _enemies.Count >= spawnProfile.SoftAliveLimit;
    }

    /// <summary>
    /// 在玩家周围刷新敌人。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    private void SpawnEnemy(EnemySpawnRuntimeContext runtimeContext)
    {
        EnemyData enemyData = GetRandomEnemyData(runtimeContext.CurrentFloorData);
        float healthMultiplier = GetCurrentEnemyHealthMultiplier(runtimeContext.CurrentFloorData) + runtimeContext.ElapsedTime * 0.015f;
        float speedMultiplier = GetCurrentEnemySpeedMultiplier(runtimeContext.CurrentFloorData) + runtimeContext.ElapsedTime * 0.004f;
        SpawnEnemy(runtimeContext, enemyData, healthMultiplier, speedMultiplier, 1f, 1f, 1f);
    }

    /// <summary>
    /// 在玩家周围刷新指定敌人并应用倍率。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="enemyData">敌人数据。</param>
    /// <param name="healthMultiplier">生命倍率。</param>
    /// <param name="speedMultiplier">速度倍率。</param>
    /// <param name="damageMultiplier">接触伤害倍率。</param>
    /// <param name="goldMultiplier">金币掉落倍率。</param>
    /// <param name="experienceMultiplier">经验掉落倍率。</param>
    private GameEnemyController SpawnEnemy(
        EnemySpawnRuntimeContext runtimeContext,
        EnemyData enemyData,
        float healthMultiplier,
        float speedMultiplier,
        float damageMultiplier,
        float goldMultiplier,
        float experienceMultiplier)
    {
        Vector3 position = CreateRandomSpawnPosition(runtimeContext);
        return SpawnEnemy(
            runtimeContext,
            enemyData,
            healthMultiplier,
            speedMultiplier,
            damageMultiplier,
            goldMultiplier,
            experienceMultiplier,
            position);
    }

    /// <summary>
    /// 创建普通敌人的随机生成位置，优先使用当前房间范围内的安全点。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <returns>生成位置。</returns>
    private Vector3 CreateRandomSpawnPosition(EnemySpawnRuntimeContext runtimeContext)
    {
        Vector3 playerPosition = runtimeContext.Player != null ? runtimeContext.Player.transform.position : Vector3.zero;
        if (TryCreateSafeRandomSpawnPosition(runtimeContext, playerPosition, minSpawnDistanceFromPlayer, out Vector3 safePosition))
            return safePosition;

        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.right;

        Vector3 fallbackPosition = runtimeContext.Player != null
            ? playerPosition + new Vector3(direction.x, direction.y, 0f) * Mathf.Max(1f, minSpawnDistanceFromPlayer)
            : Vector3.zero;
        return runtimeContext.ClampPositionInsideArena != null
            ? runtimeContext.ClampPositionInsideArena.Invoke(fallbackPosition)
            : fallbackPosition;
    }

    /// <summary>
    /// 尝试在当前房间范围内创建一个安全随机生成点。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="avoidPosition">需要避开的中心位置。</param>
    /// <param name="minDistance">最小避让距离。</param>
    /// <param name="position">输出的生成位置。</param>
    /// <returns>找到安全点时返回 true。</returns>
    private bool TryCreateSafeRandomSpawnPosition(
        EnemySpawnRuntimeContext runtimeContext,
        Vector3 avoidPosition,
        float minDistance,
        out Vector3 position)
    {
        position = Vector3.zero;
        if (runtimeContext.GetArenaMin == null || runtimeContext.GetArenaMax == null)
            return false;

        return GameRandomSpawnUtility.TryGetRandomPoint(
            runtimeContext.GetArenaMin.Invoke(),
            runtimeContext.GetArenaMax.Invoke(),
            avoidPosition,
            minDistance,
            spawnBoundaryPadding,
            spawnOverlapRadius,
            spawnObstacleMask,
            maxSpawnPositionAttempts,
            out position);
    }

    /// <summary>
    /// 在指定位置刷新敌人并应用倍率。
    /// </summary>
    /// <param name="runtimeContext">敌人刷新运行时依赖上下文。</param>
    /// <param name="enemyData">敌人数据。</param>
    /// <param name="healthMultiplier">生命倍率。</param>
    /// <param name="speedMultiplier">速度倍率。</param>
    /// <param name="damageMultiplier">接触伤害倍率。</param>
    /// <param name="goldMultiplier">金币掉落倍率。</param>
    /// <param name="experienceMultiplier">经验掉落倍率。</param>
    /// <param name="position">生成位置。</param>
    /// <returns>生成出的敌人控制器。</returns>
    private GameEnemyController SpawnEnemy(
        EnemySpawnRuntimeContext runtimeContext,
        EnemyData enemyData,
        float healthMultiplier,
        float speedMultiplier,
        float damageMultiplier,
        float goldMultiplier,
        float experienceMultiplier,
        Vector3 position)
    {
        if (runtimeContext.RuntimeObjectPoolController == null || runtimeContext.Player == null)
            return null;

        if (runtimeContext.ClampPositionInsideArena != null)
            position = runtimeContext.ClampPositionInsideArena.Invoke(position);

        GamePooledObject enemyObject = runtimeContext.RuntimeObjectPoolController.Get(
            GameRuntimeObjectPoolController.PoolObjectType.Enemy,
            enemyData != null ? enemyData.Prefab : null,
            runtimeContext.ActiveRuntimeRoot,
            position,
            Vector3.one * 0.68f,
            runtimeContext.RuntimeObjectPoolController.GetDefaultColor(GameRuntimeObjectPoolController.PoolObjectType.Enemy));
        GameEnemyController enemy = enemyObject.GetComponent<GameEnemyController>();
        if (enemy == null)
            enemy = enemyObject.gameObject.AddComponent<GameEnemyController>();

        EnsureEnemyBehavior(enemyObject.gameObject, enemyData);
        SpriteRenderer spriteRenderer = ApplyEnemySprite(enemyObject, enemyData);
        EnsureEnemyAnimator(enemyObject, enemyData, spriteRenderer);
        enemy.Initialize(
            runtimeContext.Player,
            enemyData,
            healthMultiplier,
            speedMultiplier,
            damageMultiplier,
            goldMultiplier,
            experienceMultiplier);
        _enemies.Add(enemy);
        return enemy;
    }

    /// <summary>
    /// 确保当前敌人对象挂载与 EnemyData 行为类型匹配的行为脚本。
    /// </summary>
    /// <param name="enemyObject">敌人对象。</param>
    /// <param name="enemyData">敌人配置数据。</param>
    private void EnsureEnemyBehavior(GameObject enemyObject, EnemyData enemyData)
    {
        if (enemyObject == null)
            return;

        MonoBehaviour[] behaviours = enemyObject.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.enabled && behaviour is IGameEnemyBehavior)
                return;
        }

        enemyObject.AddComponent(GetEnemyBehaviorType(enemyData));
    }

    /// <summary>
    /// 根据 EnemyData 中配置的行为类型获取行为脚本类型。
    /// </summary>
    /// <param name="enemyData">敌人配置数据。</param>
    /// <returns>行为脚本类型。</returns>
    private Type GetEnemyBehaviorType(EnemyData enemyData)
    {
        if (enemyData == null)
            return typeof(GameFoxEnemyBehavior);

        switch (enemyData.BehaviorType)
        {
            case EnemyData.EnemyBehaviorType.Wolf:
                return typeof(GameWolfEnemyBehavior);

            case EnemyData.EnemyBehaviorType.Snake:
                return typeof(GameSnakeEnemyBehavior);

            case EnemyData.EnemyBehaviorType.Bear:
                return typeof(GameBearEnemyBehavior);

            case EnemyData.EnemyBehaviorType.Fox:
            default:
                return typeof(GameFoxEnemyBehavior);
        }
    }

    /// <summary>
    /// 将敌人配置中的贴图应用到运行时敌人对象，未配置时保留对象池默认贴图。
    /// </summary>
    /// <param name="enemyObject">运行时敌人对象。</param>
    /// <param name="enemyData">敌人配置数据。</param>
    /// <returns>敌人贴图渲染器。</returns>
    private SpriteRenderer ApplyEnemySprite(GamePooledObject enemyObject, EnemyData enemyData)
    {
        if (enemyObject == null || enemyData == null || enemyData.Sprite == null)
            return enemyObject != null ? enemyObject.GetComponentInChildren<SpriteRenderer>(true) : null;

        SpriteRenderer spriteRenderer = enemyObject.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer == null)
            return null;

        spriteRenderer.sprite = enemyData.Sprite;
        spriteRenderer.color = Color.white;
        return spriteRenderer;
    }

    /// <summary>
    /// 为运行时生成的敌人补齐动画控制组件，并应用敌人数据中的动画状态机。
    /// </summary>
    /// <param name="enemyObject">运行时敌人对象。</param>
    /// <param name="enemyData">敌人配置数据。</param>
    /// <param name="spriteRenderer">敌人贴图渲染器。</param>
    private void EnsureEnemyAnimator(GamePooledObject enemyObject, EnemyData enemyData, SpriteRenderer spriteRenderer)
    {
        if (enemyObject == null)
            return;

        GameEnemyAnimatorController animatorController = enemyObject.GetComponent<GameEnemyAnimatorController>();
        if (animatorController == null)
            animatorController = enemyObject.gameObject.AddComponent<GameEnemyAnimatorController>();

        RuntimeAnimatorController runtimeAnimatorController = enemyData != null ? enemyData.AnimatorController : null;
        animatorController.Configure(runtimeAnimatorController, spriteRenderer);
    }

    /// <summary>
    /// 从当前楼层敌人池中随机获取敌人数据。
    /// </summary>
    /// <param name="floorData">当前楼层数据。</param>
    /// <returns>敌人数据；未配置时返回 null。</returns>
    private EnemyData GetRandomEnemyData(FloorData floorData)
    {
        if (floorData == null || floorData.EnemyPool == null || floorData.EnemyPool.Length <= 0)
            return null;

        int index = UnityEngine.Random.Range(0, floorData.EnemyPool.Length);
        return floorData.EnemyPool[index];
    }

    /// <summary>
    /// 获取当前楼层最大存活敌人数量。
    /// </summary>
    /// <param name="floorData">当前楼层数据。</param>
    /// <returns>最大存活敌人数量。</returns>
    private int GetCurrentMaxAliveEnemies(FloorData floorData)
    {
        if (floorData != null)
            return Mathf.Max(1, floorData.MaxAliveEnemies);

        return 18;
    }

    /// <summary>
    /// 获取当前楼层刷怪间隔。
    /// </summary>
    /// <param name="floorData">当前楼层数据。</param>
    /// <returns>刷怪间隔秒数。</returns>
    private float GetCurrentSpawnInterval(FloorData floorData)
    {
        if (floorData != null)
            return Mathf.Max(0.1f, floorData.SpawnInterval);

        return Mathf.Max(0.1f, spawnInterval);
    }

    /// <summary>
    /// 获取当前楼层敌人生命倍率。
    /// </summary>
    /// <param name="floorData">当前楼层数据。</param>
    /// <returns>敌人生命倍率。</returns>
    private float GetCurrentEnemyHealthMultiplier(FloorData floorData)
    {
        if (floorData != null)
            return Mathf.Max(0.1f, floorData.EnemyHealthMultiplier);

        return 1f;
    }

    /// <summary>
    /// 获取当前楼层敌人速度倍率。
    /// </summary>
    /// <param name="floorData">当前楼层数据。</param>
    /// <returns>敌人速度倍率。</returns>
    private float GetCurrentEnemySpeedMultiplier(FloorData floorData)
    {
        if (floorData != null)
            return Mathf.Max(0.1f, floorData.EnemySpeedMultiplier);

        return 1f;
    }

    /// <summary>
    /// 保存单个 Boss 技能的触发状态和下一次冷却时间。
    /// </summary>
    private sealed class BossSkillRuntimeState
    {
        public bool HasTriggered;
        public float NextCooldownTime;
    }
}
