using System;
using UnityEngine;

/// <summary>
/// 管理普通开火、弹夹换弹、能量恢复、右键技能和持续大招弹幕。
/// </summary>
public sealed class GameWeaponFireController : MonoBehaviour
{
    private const float MinAttackInterval = 0.1f;

    /// <summary>
    /// 武器开火系统所需的运行时数值上下文。
    /// </summary>
    public struct WeaponFireRuntimeContext
    {
        public GamePlayerController Player;
        public WeaponData WeaponData;
        public Func<GameProjectileCombatController.ProjectileFireContext> CreateNormalProjectileContext;
        public Func<float, float, float, int, int, float, float, GameProjectileCombatController.ProjectileFireContext> CreateSkillProjectileContext;
        public Action<int, float, bool, GameProjectileCombatController.ProjectileFireContext> FireProjectileVolleyFromWeaponInstances;
        public Func<bool> HasRecoverableNails;
        public Action RecallRecoverableNails;
        public Func<Vector3> GetMouseFireDirection;
        public Action<Vector3, float> ApplyRecoil;
        public Action<float> PlayCursorFirePulse;
    }

    private float _fireTimer;
    private float _currentEnergy;
    private float _reloadTimer;
    private float _rightMouseHoldTimer;
    private float _majorSkillTimer;
    private float _majorSkillFireTimer;
    private bool _isReloading;
    private bool _isChargingSkill;
    private bool _rightMouseSkillConsumed;
    private bool _loyaltyRecallSkillEnabled;
    private int _currentAmmo;

    /// <summary>
    /// 当前弹夹内弹药数量。
    /// </summary>
    public int CurrentAmmo => _currentAmmo;

    /// <summary>
    /// 当前能量值。
    /// </summary>
    public float CurrentEnergy => _currentEnergy;

    /// <summary>
    /// 是否正在换弹。
    /// </summary>
    public bool IsReloading => _isReloading;

    /// <summary>
    /// 是否正在蓄力。
    /// </summary>
    public bool IsChargingSkill => _isChargingSkill;

    /// <summary>
    /// 当前蓄力时间。
    /// </summary>
    public float RightMouseHoldTimer => _rightMouseHoldTimer;

    /// <summary>
    /// 重置武器开火运行时状态。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    public void InitializeWeaponRuntimeState(WeaponFireRuntimeContext runtimeContext)
    {
        _currentAmmo = GetCurrentMagazineCapacity(runtimeContext);
        _currentEnergy = GetCurrentMaxEnergy(runtimeContext);
        _fireTimer = 0.2f;
        _reloadTimer = 0f;
        _rightMouseHoldTimer = 0f;
        _rightMouseSkillConsumed = false;
        _majorSkillTimer = 0f;
        _majorSkillFireTimer = 0f;
        _isReloading = false;
        _isChargingSkill = false;
        _loyaltyRecallSkillEnabled = false;
    }

    /// <summary>
    /// 开始下一层时重置开火计时、换弹和技能持续状态。
    /// </summary>
    public void ResetForNextFloor()
    {
        _fireTimer = 0.2f;
        _reloadTimer = 0f;
        _isReloading = false;
        _isChargingSkill = false;
        _majorSkillTimer = 0f;
        _majorSkillFireTimer = 0f;
    }

    /// <summary>
    /// 进入结算或选房间交互前重置武器交互态，并补满当前弹夹。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    public void ResetForPostCombatInteraction(WeaponFireRuntimeContext runtimeContext)
    {
        _fireTimer = 0.2f;
        _rightMouseHoldTimer = 0f;
        _rightMouseSkillConsumed = false;
        _majorSkillTimer = 0f;
        _majorSkillFireTimer = 0f;
        StopSkillCharge(runtimeContext);
        RefillMagazine(runtimeContext);
    }

    /// <summary>
    /// 清理整局运行时的开火状态。
    /// </summary>
    public void ResetRunState()
    {
        _currentAmmo = 0;
        _currentEnergy = 0f;
        _fireTimer = 0f;
        _reloadTimer = 0f;
        _rightMouseHoldTimer = 0f;
        _majorSkillTimer = 0f;
        _majorSkillFireTimer = 0f;
        _isReloading = false;
        _isChargingSkill = false;
        _rightMouseSkillConsumed = false;
        _loyaltyRecallSkillEnabled = false;
    }

    /// <summary>
    /// 启用忠诚 III 的召回钉子技能替换规则。
    /// </summary>
    public void EnableLoyaltyRecallSkill()
    {
        _loyaltyRecallSkillEnabled = true;
        _majorSkillTimer = 0f;
        _majorSkillFireTimer = 0f;
        _rightMouseHoldTimer = 0f;
        _rightMouseSkillConsumed = false;
        _isChargingSkill = false;
    }

    /// <summary>
    /// 更新弹夹、换弹和能量恢复。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    public void TickWeaponResources(WeaponFireRuntimeContext runtimeContext)
    {
        _currentEnergy = Mathf.Min(GetCurrentMaxEnergy(runtimeContext), _currentEnergy + GetCurrentEnergyRecoveryPerSecond(runtimeContext) * Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.R))
            TryStartReload(runtimeContext);

        if (!_isReloading)
            return;

        _reloadTimer -= Time.deltaTime;
        if (_reloadTimer > 0f)
            return;

        _isReloading = false;
        _currentAmmo = GetCurrentMagazineCapacity(runtimeContext);
    }

    /// <summary>
    /// 处理右键短按和长按技能输入。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    public void TickSkillInput(WeaponFireRuntimeContext runtimeContext)
    {
        WeaponSkillData skillData = GetCurrentSkillData(runtimeContext);
        if (skillData == null)
            return;

        if (_loyaltyRecallSkillEnabled)
        {
            TickLoyaltyRecallSkillInput(runtimeContext, skillData);
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            _rightMouseHoldTimer = 0f;
            _rightMouseSkillConsumed = false;
            StartSkillCharge(runtimeContext, skillData);
        }

        if (Input.GetMouseButton(1))
        {
            _rightMouseHoldTimer += Time.deltaTime;
            if (!_rightMouseSkillConsumed && _isChargingSkill && _rightMouseHoldTimer >= GetCurrentChargeDuration(runtimeContext))
            {
                StopSkillCharge(runtimeContext);
                _rightMouseSkillConsumed = TryCastMajorSkill(runtimeContext, skillData);
            }
        }

        if (Input.GetMouseButtonUp(1))
        {
            StopSkillCharge(runtimeContext);
            if (!_rightMouseSkillConsumed)
                TryCastMinorSkill(runtimeContext, skillData);

            _rightMouseSkillConsumed = false;
        }
    }

    /// <summary>
    /// 更新大技能持续弹幕。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    public void TickMajorSkill(WeaponFireRuntimeContext runtimeContext)
    {
        if (_loyaltyRecallSkillEnabled)
        {
            _majorSkillTimer = 0f;
            _majorSkillFireTimer = 0f;
            return;
        }

        if (_majorSkillTimer <= 0f)
            return;

        WeaponSkillData skillData = GetCurrentSkillData(runtimeContext);
        if (skillData == null)
        {
            _majorSkillTimer = 0f;
            return;
        }

        _majorSkillTimer = Mathf.Max(0f, _majorSkillTimer - Time.deltaTime);
        _majorSkillFireTimer -= Time.deltaTime;
        if (_majorSkillFireTimer > 0f)
            return;

        _majorSkillFireTimer = Mathf.Max(0.02f, skillData.MajorFireInterval);
        GameProjectileCombatController.ProjectileFireContext context = runtimeContext.CreateSkillProjectileContext.Invoke(
            skillData.MajorDamageMultiplier,
            skillData.MajorProjectileSpeedMultiplier,
            skillData.MajorLifeTimeOverride,
            skillData.MajorPierceOverride >= 0 ? skillData.MajorPierceOverride : GetCurrentProjectilePierce(runtimeContext),
            skillData.MajorBounceOverride >= 0 ? skillData.MajorBounceOverride : GetCurrentProjectileBounce(runtimeContext),
            skillData.MajorRecoilMultiplier,
            skillData.MajorImpactMultiplier);
        Vector3 direction = runtimeContext.GetMouseFireDirection != null ? runtimeContext.GetMouseFireDirection.Invoke() : Vector3.right;
        runtimeContext.FireProjectileVolleyFromWeaponInstances?.Invoke(Mathf.Max(1, skillData.MajorProjectileCount), skillData.MajorSpreadAngle * 2f, false, context);
        runtimeContext.ApplyRecoil?.Invoke(direction, GetCurrentRecoilDistance(runtimeContext) * context.RecoilMultiplier);
    }

    /// <summary>
    /// 按玩家左键输入和当前攻击节奏发射普通投射物。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    public void TickNormalFire(WeaponFireRuntimeContext runtimeContext)
    {
        _fireTimer = Mathf.Max(0f, _fireTimer - Time.deltaTime);
        if (!Input.GetMouseButton(0))
            return;

        if (_isReloading)
            return;

        if (_fireTimer > 0f)
            return;

        float attackInterval = Mathf.Max(MinAttackInterval, GetCurrentAttackInterval(runtimeContext));
        _fireTimer = attackInterval;
        if (_currentAmmo <= 0)
        {
            TryStartReload(runtimeContext);
            return;
        }

        _currentAmmo--;
        Vector3 fireDirection = runtimeContext.GetMouseFireDirection != null ? runtimeContext.GetMouseFireDirection.Invoke() : Vector3.right;
        runtimeContext.FireProjectileVolleyFromWeaponInstances?.Invoke(GetCurrentProjectileCount(runtimeContext), 8f, true, runtimeContext.CreateNormalProjectileContext.Invoke());
        runtimeContext.ApplyRecoil?.Invoke(fireDirection, GetCurrentRecoilDistance(runtimeContext));
        runtimeContext.PlayCursorFirePulse?.Invoke(attackInterval);
        if (_currentAmmo <= 0)
            TryStartReload(runtimeContext);
    }

    /// <summary>
    /// 击杀敌人后恢复能量。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    public void AddEnergyPerKill(WeaponFireRuntimeContext runtimeContext)
    {
        _currentEnergy = Mathf.Min(GetCurrentMaxEnergy(runtimeContext), _currentEnergy + GetCurrentEnergyPerKill(runtimeContext));
    }

    /// <summary>
    /// 获取当前弹夹容量。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>弹夹容量。</returns>
    public int GetCurrentMagazineCapacity(WeaponFireRuntimeContext runtimeContext)
    {
        int baseValue = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.MagazineCapacity : 1;
        float multiplier = runtimeContext.Player != null ? runtimeContext.Player.MagazineCapacityMultiplier : 1f;
        int multipliedValue = Mathf.RoundToInt(baseValue * Mathf.Max(0.01f, multiplier));
        if (runtimeContext.Player != null && multiplier > 1f)
            multipliedValue = Mathf.Max(baseValue + 1, multipliedValue);

        return Mathf.Max(1, multipliedValue);
    }

    /// <summary>
    /// 获取当前最大能量。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>最大能量。</returns>
    public float GetCurrentMaxEnergy(WeaponFireRuntimeContext runtimeContext)
    {
        float baseValue = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.MaxEnergy : 100f;
        float bonus = runtimeContext.Player != null ? runtimeContext.Player.MaxEnergyBonus : 0f;
        return Mathf.Max(1f, baseValue + bonus);
    }

    /// <summary>
    /// 获取当前射击准度偏差角。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>偏差角。</returns>
    public float GetCurrentAccuracyAngle(WeaponFireRuntimeContext runtimeContext)
    {
        float baseValue = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.AccuracyAngle : 0f;
        float accuracyBonus = runtimeContext.Player != null ? runtimeContext.Player.AccuracyMultiplier - 1f : 0f;
        return Mathf.Max(0f, baseValue * Mathf.Max(0f, 1f - accuracyBonus));
    }

    /// <summary>
    /// 获取当前后坐力距离。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>后坐力距离。</returns>
    public float GetCurrentRecoilDistance(WeaponFireRuntimeContext runtimeContext)
    {
        float baseValue = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.RecoilDistance : 0f;
        float multiplier = runtimeContext.Player != null ? runtimeContext.Player.RecoilControlMultiplier : 1f;
        return Mathf.Max(0f, baseValue / Mathf.Max(0.01f, multiplier));
    }

    /// <summary>
    /// 获取当前冲击力距离。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>冲击力距离。</returns>
    public float GetCurrentImpactDistance(WeaponFireRuntimeContext runtimeContext)
    {
        float baseValue = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.ImpactDistance : 0f;
        float multiplier = runtimeContext.Player != null ? runtimeContext.Player.ImpactMultiplier : 1f;
        return Mathf.Max(0f, baseValue * Mathf.Max(0.01f, multiplier));
    }

    /// <summary>
    /// 获取当前蓄力总时长。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>蓄力总时长。</returns>
    public float GetCurrentChargeDuration(WeaponFireRuntimeContext runtimeContext)
    {
        WeaponSkillData skillData = GetCurrentSkillData(runtimeContext);
        return skillData != null ? Mathf.Max(0.01f, skillData.ChargeDuration) : 1f;
    }

    /// <summary>
    /// 获取当前换弹时间。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>换弹时间。</returns>
    public float GetCurrentReloadTime(WeaponFireRuntimeContext runtimeContext)
    {
        float baseValue = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.ReloadTime : 0.8f;
        float multiplier = runtimeContext.Player != null ? runtimeContext.Player.ReloadSpeedMultiplier : 1f;
        return Mathf.Max(0.01f, baseValue / Mathf.Max(0.01f, multiplier));
    }

    /// <summary>
    /// 尝试开始换弹。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    private void TryStartReload(WeaponFireRuntimeContext runtimeContext)
    {
        if (_isReloading || _currentAmmo >= GetCurrentMagazineCapacity(runtimeContext))
            return;

        _isReloading = true;
        _reloadTimer = GetCurrentReloadTime(runtimeContext);
    }

    /// <summary>
    /// 补满当前弹夹。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    private void RefillMagazine(WeaponFireRuntimeContext runtimeContext)
    {
        _currentAmmo = GetCurrentMagazineCapacity(runtimeContext);
        _isReloading = false;
        _reloadTimer = 0f;
    }

    /// <summary>
    /// 开始右键蓄力。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <param name="skillData">技能配置。</param>
    private void StartSkillCharge(WeaponFireRuntimeContext runtimeContext, WeaponSkillData skillData)
    {
        if (skillData == null || _currentEnergy <= 0f)
            return;

        _isChargingSkill = true;
        if (runtimeContext.Player != null)
            runtimeContext.Player.SetTemporaryMoveSpeedMultiplier(Mathf.Clamp01(skillData.ChargeMoveSpeedMultiplier));
    }

    /// <summary>
    /// 结束右键蓄力。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    private void StopSkillCharge(WeaponFireRuntimeContext runtimeContext)
    {
        _isChargingSkill = false;
        if (runtimeContext.Player != null)
            runtimeContext.Player.SetTemporaryMoveSpeedMultiplier(1f);
    }

    /// <summary>
    /// 尝试释放小技能。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <param name="skillData">技能配置。</param>
    private void TryCastMinorSkill(WeaponFireRuntimeContext runtimeContext, WeaponSkillData skillData)
    {
        if (_loyaltyRecallSkillEnabled)
        {
            TryCastLoyaltyRecallSkill(runtimeContext, skillData);
            return;
        }

        if (skillData == null || _currentEnergy < skillData.MinorEnergyCost)
            return;

        _currentEnergy = Mathf.Max(0f, _currentEnergy - skillData.MinorEnergyCost);
        int pierce = Mathf.RoundToInt(GetCurrentProjectilePierce(runtimeContext) * Mathf.Max(0f, skillData.MinorPierceMultiplier));
        int bounce = Mathf.RoundToInt(GetCurrentProjectileBounce(runtimeContext) * Mathf.Max(0f, skillData.MinorBounceMultiplier));
        GameProjectileCombatController.ProjectileFireContext context = runtimeContext.CreateSkillProjectileContext.Invoke(
            skillData.MinorDamageMultiplier,
            skillData.MinorProjectileSpeedMultiplier,
            -1f,
            pierce,
            bounce,
            1f,
            1f);
        runtimeContext.FireProjectileVolleyFromWeaponInstances?.Invoke(Mathf.Max(1, skillData.MinorProjectileCount), skillData.MinorSpreadAngle * 2f, false, context);
        if (skillData.RefillMagazineAfterMinorSkill)
            RefillMagazine(runtimeContext);
    }

    /// <summary>
    /// 尝试释放大技能。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <param name="skillData">技能配置。</param>
    /// <returns>成功释放时返回 true。</returns>
    private bool TryCastMajorSkill(WeaponFireRuntimeContext runtimeContext, WeaponSkillData skillData)
    {
        if (_loyaltyRecallSkillEnabled)
            return false;

        if (skillData == null || _currentEnergy < GetCurrentMaxEnergy(runtimeContext))
            return false;

        _currentEnergy = 0f;
        _majorSkillTimer = Mathf.Max(0.01f, skillData.MajorDuration);
        _majorSkillFireTimer = 0f;
        return true;
    }

    /// <summary>
    /// 处理忠诚 III 状态下的右键释放输入，禁用蓄力并只尝试召回钉子。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <param name="skillData">技能配置。</param>
    private void TickLoyaltyRecallSkillInput(WeaponFireRuntimeContext runtimeContext, WeaponSkillData skillData)
    {
        if (Input.GetMouseButtonDown(1))
        {
            _rightMouseHoldTimer = 0f;
            _rightMouseSkillConsumed = false;
            StopSkillCharge(runtimeContext);
        }

        if (Input.GetMouseButton(1))
            _rightMouseHoldTimer += Time.deltaTime;

        if (!Input.GetMouseButtonUp(1))
            return;

        StopSkillCharge(runtimeContext);
        TryCastLoyaltyRecallSkill(runtimeContext, skillData);
        _rightMouseSkillConsumed = false;
    }

    /// <summary>
    /// 尝试释放忠诚 III 的召回钉子技能，场上没有可召回钉子时不会消耗能量。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <param name="skillData">技能配置。</param>
    private void TryCastLoyaltyRecallSkill(WeaponFireRuntimeContext runtimeContext, WeaponSkillData skillData)
    {
        if (skillData == null || _currentEnergy < skillData.MinorEnergyCost)
            return;

        if (runtimeContext.HasRecoverableNails == null || !runtimeContext.HasRecoverableNails.Invoke())
            return;

        _currentEnergy = Mathf.Max(0f, _currentEnergy - skillData.MinorEnergyCost);
        runtimeContext.RecallRecoverableNails?.Invoke();
    }

    /// <summary>
    /// 获取当前武器技能配置。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>技能配置。</returns>
    private WeaponSkillData GetCurrentSkillData(WeaponFireRuntimeContext runtimeContext)
    {
        return runtimeContext.WeaponData != null ? runtimeContext.WeaponData.SkillData : null;
    }

    /// <summary>
    /// 获取当前每秒能量恢复。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>每秒能量恢复。</returns>
    private float GetCurrentEnergyRecoveryPerSecond(WeaponFireRuntimeContext runtimeContext)
    {
        float baseValue = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.EnergyRecoveryPerSecond : 0f;
        float bonus = runtimeContext.Player != null ? runtimeContext.Player.EnergyRecoveryBonus : 0f;
        return Mathf.Max(0f, baseValue + bonus);
    }

    /// <summary>
    /// 获取当前击杀能量奖励。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>击杀能量奖励。</returns>
    private float GetCurrentEnergyPerKill(WeaponFireRuntimeContext runtimeContext)
    {
        float baseValue = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.EnergyPerKill : 0f;
        float bonus = runtimeContext.Player != null ? runtimeContext.Player.EnergyPerKillBonus : 0f;
        return Mathf.Max(0f, baseValue + bonus);
    }

    /// <summary>
    /// 获取当前穿透层数。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>穿透层数。</returns>
    private int GetCurrentProjectilePierce(WeaponFireRuntimeContext runtimeContext)
    {
        return runtimeContext.Player != null ? runtimeContext.Player.ProjectilePierce : 0;
    }

    /// <summary>
    /// 获取当前反弹次数。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>反弹次数。</returns>
    private int GetCurrentProjectileBounce(WeaponFireRuntimeContext runtimeContext)
    {
        return runtimeContext.Player != null ? runtimeContext.Player.ProjectileBounce : 0;
    }

    /// <summary>
    /// 获取当前弹道数。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>弹道数。</returns>
    private int GetCurrentProjectileCount(WeaponFireRuntimeContext runtimeContext)
    {
        return runtimeContext.Player != null ? runtimeContext.Player.ProjectileCount : 1;
    }

    /// <summary>
    /// 获取当前攻击间隔。
    /// </summary>
    /// <param name="runtimeContext">开火运行时上下文。</param>
    /// <returns>攻击间隔。</returns>
    private float GetCurrentAttackInterval(WeaponFireRuntimeContext runtimeContext)
    {
        if (runtimeContext.Player != null && runtimeContext.WeaponData != null)
            return Mathf.Max(MinAttackInterval, runtimeContext.WeaponData.AttackInterval / runtimeContext.Player.AttackSpeedMultiplier);

        return MinAttackInterval;
    }
}
