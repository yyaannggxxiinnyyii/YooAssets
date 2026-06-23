using System;
using UnityEngine;

/// <summary>
/// 战斗房中的浆果丛场地道具，成熟后玩家靠近即可采集果子并恢复生命。
/// </summary>
public sealed class StageBerryBushFieldProp : MonoBehaviour, IProjectileHitTarget
{
    private enum BerryBushState
    {
        Growing,
        WaitingFruit,
        Mature,
        Despawning
    }

    [Header("采集")]
    [Tooltip("浆果成熟后，玩家进入该半径即可自动采集并恢复生命。")]
    [SerializeField] private float collectRadius = 0.8f;
    [Tooltip("是否仍允许投射物命中采集；默认关闭，主要用于临时调试或特殊道具。")]
    [SerializeField] private bool allowProjectileCollect;

    [Header("成长")]
    [Tooltip("每轮果实从无果状态长到成熟的最短时间。")]
    [SerializeField] private float minGrowDuration = 3f;
    [Tooltip("每轮果实从无果状态长到成熟的最长时间。")]
    [SerializeField] private float maxGrowDuration = 6f;

    [Header("奖励")]
    [Tooltip("采集果子后恢复的固定生命值。")]
    [SerializeField] private int healAmount = 15;
    [Tooltip("采集果子后按玩家最大生命恢复的比例，0.1 表示 10%。")]
    [SerializeField] private float healPercent;

    [Header("生命周期")]
    [Tooltip("浆果丛最大寿命；寿命内可以多次结果，小于等于 0 时不会因寿命结束而消失。")]
    [SerializeField] private float lifeTime = 18f;
    [Tooltip("触发后是否隐藏可视根节点。为空时隐藏当前对象。")]
    [SerializeField] private GameObject visualRoot;
    [Tooltip("浆果丛主渲染器，用于在无果和成熟有果状态之间切换。")]
    [SerializeField] private SpriteRenderer bushRenderer;
    [Tooltip("未成熟时显示的无果灌木 Sprite。")]
    [SerializeField] private Sprite immatureSprite;
    [Tooltip("成熟后显示的有果灌木 Sprite。")]
    [SerializeField] private Sprite matureSprite;

    [Header("表现动画")]
    [Tooltip("执行缩放和摇晃动画的根节点；为空时优先使用可视根节点，否则使用当前对象。")]
    [SerializeField] private Transform animationRoot;
    [Tooltip("浆果丛从 0 缩放到完整大小的出生动画时长。")]
    [SerializeField] private float growAnimationDuration = 0.45f;
    [Tooltip("采集果实时剧烈摇晃的动画时长，动画结束后果实才会消失并进入下一轮生长。")]
    [SerializeField] private float collectShakeDuration = 0.35f;
    [Tooltip("采集摇晃时的最大旋转角度。")]
    [SerializeField] private float collectShakeRotation = 12f;
    [Tooltip("采集摇晃时的缩放强度。")]
    [SerializeField] private float collectShakeScale = 0.08f;
    [Tooltip("寿命结束时缩小消失的动画时长。")]
    [SerializeField] private float despawnAnimationDuration = 0.35f;
    [Tooltip("成熟或等待结果期间是否播放轻微 idle 摇晃。")]
    [SerializeField] private bool idleWobbleEnabled = true;
    [Tooltip("Idle 状态下左右摇晃的最大旋转角度。")]
    [SerializeField] private float idleWobbleRotation = 1.2f;
    [Tooltip("Idle 状态下横向缩放强度，用于模拟弹簧蠕动。")]
    [SerializeField] private float idleWobbleScaleX = 0.015f;
    [Tooltip("Idle 状态下纵向缩放强度，用于模拟弹簧蠕动。")]
    [SerializeField] private float idleWobbleScaleY = 0.035f;
    [Tooltip("Idle 蠕动速度。")]
    [SerializeField] private float idleWobbleSpeed = 2.2f;

    private GamePlayerController _player;
    private Action<StageBerryBushFieldProp> _completedCallback;
    private BerryBushState _state;
    private Vector3 _baseAnimationScale;
    private Quaternion _baseAnimationRotation;
    private float _lifeTimer;
    private float _stateTimer;
    private float _fruitGrowDuration;
    private float _touchShakeTimer;
    private bool _completed;
    private bool _playerInTouchRange;
    private bool _consumeFruitAfterShake;

    /// <summary>
    /// 目标是否仍可被投射物命中。
    /// </summary>
    public bool CanBeHitByProjectile => allowProjectileCollect && _state == BerryBushState.Mature && !_consumeFruitAfterShake && !_completed && gameObject.activeInHierarchy;

    /// <summary>
    /// 目标用于距离命中的世界坐标。
    /// </summary>
    public Vector3 ProjectileHitPosition => transform.position;

    /// <summary>
    /// 目标自身命中半径。
    /// </summary>
    public float ProjectileHitRadius => Mathf.Max(0.01f, collectRadius);

    private void Awake()
    {
        EnsureReferences();
    }

    private void Update()
    {
        if (_completed)
            return;

        float deltaTime = Time.deltaTime;
        _lifeTimer += deltaTime;
        _stateTimer += deltaTime;

        if (lifeTime > 0f && _lifeTimer >= lifeTime && _state != BerryBushState.Despawning)
            StartDespawn();

        TickState();
        TickTouchShake(deltaTime);
        ApplyAnimationPose();
    }

    private void OnValidate()
    {
        collectRadius = Mathf.Max(0.01f, collectRadius);
        minGrowDuration = Mathf.Max(0f, minGrowDuration);
        maxGrowDuration = Mathf.Max(minGrowDuration, maxGrowDuration);
        healAmount = Mathf.Max(0, healAmount);
        healPercent = Mathf.Max(0f, healPercent);
        growAnimationDuration = Mathf.Max(0f, growAnimationDuration);
        collectShakeDuration = Mathf.Max(0f, collectShakeDuration);
        collectShakeRotation = Mathf.Max(0f, collectShakeRotation);
        collectShakeScale = Mathf.Max(0f, collectShakeScale);
        despawnAnimationDuration = Mathf.Max(0f, despawnAnimationDuration);
        idleWobbleRotation = Mathf.Max(0f, idleWobbleRotation);
        idleWobbleScaleX = Mathf.Max(0f, idleWobbleScaleX);
        idleWobbleScaleY = Mathf.Max(0f, idleWobbleScaleY);
        idleWobbleSpeed = Mathf.Max(0f, idleWobbleSpeed);
    }

    /// <summary>
    /// 在 Scene 视图中绘制浆果丛采集范围。
    /// </summary>
    private void OnDrawGizmos()
    {
        GameRadiusGizmoUtility.DrawRadius(transform.position, Mathf.Max(0.01f, collectRadius), new Color(0.35f, 1f, 0.45f, 0.85f), "Berry Collect");
    }

    /// <summary>
    /// 初始化浆果丛运行时依赖和状态。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="completedCallback">浆果丛消失或被采集后的回调。</param>
    public void Initialize(GamePlayerController player, Action<StageBerryBushFieldProp> completedCallback)
    {
        EnsureReferences();
        _player = player;
        _completedCallback = completedCallback;
        _lifeTimer = 0f;
        _stateTimer = 0f;
        _completed = false;
        if (visualRoot != null)
            visualRoot.SetActive(true);

        CaptureAnimationBasePose();
        SetMature(false);
        ChangeState(BerryBushState.Growing);
        if (growAnimationDuration <= 0f)
            BeginFruitGrowthCycle();

        ApplyAnimationPose();
    }

    /// <summary>
    /// 处理投射物命中；仅在允许投射物采集且浆果成熟时触发恢复。
    /// </summary>
    /// <param name="projectile">命中的投射物。</param>
    /// <param name="damageInfo">投射物携带的伤害信息。</param>
    public void HandleProjectileHit(GameProjectile projectile, DamageHitInfo damageInfo)
    {
        if (!CanBeHitByProjectile)
            return;

        StartAdultTouchFeedback(true);
    }

    /// <summary>
    /// 按当前状态推进成熟、采集和消失流程。
    /// </summary>
    private void TickState()
    {
        switch (_state)
        {
            case BerryBushState.Growing:
                if (_stateTimer >= growAnimationDuration)
                    BeginFruitGrowthCycle();
                break;

            case BerryBushState.WaitingFruit:
                if (_stateTimer >= _fruitGrowDuration)
                {
                    SetMature(true);
                    ChangeState(BerryBushState.Mature);
                }
                TryTriggerAdultTouchByPlayerDistance();
                break;

            case BerryBushState.Mature:
                TryTriggerAdultTouchByPlayerDistance();
                break;

            case BerryBushState.Despawning:
                if (_stateTimer >= despawnAnimationDuration)
                    CompleteWithoutReward();
                break;
        }
    }

    /// <summary>
    /// 根据玩家与浆果丛的距离触发成体灌木晃动，并在成熟有果且缺血时消耗果实。
    /// </summary>
    private void TryTriggerAdultTouchByPlayerDistance()
    {
        if (_player == null)
            return;

        float radius = Mathf.Max(0.01f, collectRadius);
        bool isInRange = ((Vector2)(_player.transform.position - transform.position)).sqrMagnitude <= radius * radius;
        if (!isInRange)
        {
            _playerInTouchRange = false;
            return;
        }

        bool canConsumeFruit = _state == BerryBushState.Mature && !_consumeFruitAfterShake && CanConsumeFruit();
        if (!_playerInTouchRange || canConsumeFruit)
            StartAdultTouchFeedback(canConsumeFruit);

        _playerInTouchRange = true;
    }

    /// <summary>
    /// 切换浆果丛成熟状态，并刷新显示 Sprite。
    /// </summary>
    /// <param name="mature">是否成熟。</param>
    private void SetMature(bool mature)
    {
        if (bushRenderer == null)
            return;

        Sprite targetSprite = mature ? matureSprite : immatureSprite;
        if (targetSprite != null)
            bushRenderer.sprite = targetSprite;
    }

    /// <summary>
    /// 进入下一轮果实生长阶段。
    /// </summary>
    private void BeginFruitGrowthCycle(bool keepPlayerTouchRange = false)
    {
        if (_state == BerryBushState.Despawning || _completed)
            return;

        if (!keepPlayerTouchRange)
            _playerInTouchRange = false;

        _consumeFruitAfterShake = false;
        _fruitGrowDuration = RollFruitGrowDuration();
        SetMature(false);
        ChangeState(BerryBushState.WaitingFruit);
        if (_fruitGrowDuration <= 0f)
        {
            SetMature(true);
            ChangeState(BerryBushState.Mature);
        }
    }

    /// <summary>
    /// 开始成体灌木触碰反馈；有果且允许消耗时记录果实消耗，其余情况只播放晃动。
    /// </summary>
    /// <param name="tryConsumeFruit">是否尝试消耗成熟果实。</param>
    private void StartAdultTouchFeedback(bool tryConsumeFruit)
    {
        if (!IsAdultState() || _completed)
            return;

        if (tryConsumeFruit && _state == BerryBushState.Mature && !_consumeFruitAfterShake)
            _consumeFruitAfterShake = TryApplyHeal();

        _touchShakeTimer = Mathf.Max(_touchShakeTimer, collectShakeDuration);
        if (collectShakeDuration <= 0f)
            FinishTouchShake();
    }

    /// <summary>
    /// 结束触碰晃动；已消耗果实时进入下一轮结果，否则保持当前成体状态。
    /// </summary>
    private void FinishTouchShake()
    {
        if (_completed)
            return;

        if (_consumeFruitAfterShake)
        {
            BeginFruitGrowthCycle(true);
            return;
        }

        _consumeFruitAfterShake = false;
    }

    /// <summary>
    /// 开始寿命结束消失动画。
    /// </summary>
    private void StartDespawn()
    {
        if (_completed || _state == BerryBushState.Despawning)
            return;

        _touchShakeTimer = 0f;
        _consumeFruitAfterShake = false;
        ChangeState(BerryBushState.Despawning);
        if (despawnAnimationDuration <= 0f)
            CompleteWithoutReward();
    }

    /// <summary>
    /// 不发放奖励并结束自身，用于生命周期结束。
    /// </summary>
    private void CompleteWithoutReward()
    {
        if (_completed)
            return;

        _completed = true;
        if (visualRoot != null)
            visualRoot.SetActive(false);
        _completedCallback?.Invoke(this);
    }

    /// <summary>
    /// 尝试按配置为玩家消耗普通治疗并恢复生命。
    /// </summary>
    /// <returns>治疗被实际消耗时返回 true。</returns>
    private bool TryApplyHeal()
    {
        if (_player == null)
            return false;

        int percentHeal = Mathf.RoundToInt(_player.MaxHealth * Mathf.Max(0f, healPercent));
        int totalHeal = Mathf.Max(0, healAmount) + percentHeal;
        return _player.TryConsumeNormalHeal(totalHeal);
    }

    /// <summary>
    /// 判断当前成熟果实是否能被玩家实际消耗。
    /// </summary>
    /// <returns>玩家缺血且果实治疗量有效时返回 true。</returns>
    private bool CanConsumeFruit()
    {
        if (_player == null)
            return false;

        int percentHeal = Mathf.RoundToInt(_player.MaxHealth * Mathf.Max(0f, healPercent));
        int totalHeal = Mathf.Max(0, healAmount) + percentHeal;
        return _player.CanConsumeNormalHeal(totalHeal);
    }

    /// <summary>
    /// 切换浆果丛内部状态并重置状态计时。
    /// </summary>
    /// <param name="state">新的浆果丛状态。</param>
    private void ChangeState(BerryBushState state)
    {
        _state = state;
        _stateTimer = 0f;
    }

    /// <summary>
    /// 判断灌木是否已经完成出生生长，进入可以被路过触发晃动的成体阶段。
    /// </summary>
    /// <returns>处于等待结果或成熟有果状态时返回 true。</returns>
    private bool IsAdultState()
    {
        return _state == BerryBushState.WaitingFruit || _state == BerryBushState.Mature;
    }

    /// <summary>
    /// 推进路过触发的独立晃动计时，结束后按需处理果实消耗。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickTouchShake(float deltaTime)
    {
        if (_touchShakeTimer <= 0f)
            return;

        _touchShakeTimer = Mathf.Max(0f, _touchShakeTimer - deltaTime);
        if (_touchShakeTimer <= 0f)
            FinishTouchShake();
    }

    /// <summary>
    /// 随机计算下一轮果实成熟所需时间。
    /// </summary>
    /// <returns>果实成熟时间。</returns>
    private float RollFruitGrowDuration()
    {
        float minDuration = Mathf.Max(0f, minGrowDuration);
        float maxDuration = Mathf.Max(minDuration, maxGrowDuration);
        return UnityEngine.Random.Range(minDuration, maxDuration);
    }

    /// <summary>
    /// 确保可视、动画和渲染引用可用。
    /// </summary>
    private void EnsureReferences()
    {
        if (visualRoot == null)
            visualRoot = gameObject;

        if (bushRenderer == null)
            bushRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (animationRoot == null)
            animationRoot = bushRenderer != null ? bushRenderer.transform : visualRoot != null ? visualRoot.transform : transform;
    }

    /// <summary>
    /// 记录动画根节点的初始缩放和旋转，供运行时动画叠加。
    /// </summary>
    private void CaptureAnimationBasePose()
    {
        if (animationRoot == null)
            return;

        _baseAnimationScale = animationRoot.localScale;
        _baseAnimationRotation = animationRoot.localRotation;
    }

    /// <summary>
    /// 按当前状态应用缩放、摇晃和消失动画。
    /// </summary>
    private void ApplyAnimationPose()
    {
        if (animationRoot == null)
            return;

        Vector3 scaleMultiplier = Vector3.one;
        float rotationZ = 0f;
        switch (_state)
        {
            case BerryBushState.Growing:
                scaleMultiplier = Vector3.one * SmoothStep01(growAnimationDuration <= 0f ? 1f : _stateTimer / growAnimationDuration);
                break;

            case BerryBushState.Despawning:
                scaleMultiplier = Vector3.one * (1f - SmoothStep01(despawnAnimationDuration <= 0f ? 1f : _stateTimer / despawnAnimationDuration));
                break;

            case BerryBushState.WaitingFruit:
            case BerryBushState.Mature:
                ApplyIdleWobble(ref scaleMultiplier, ref rotationZ);
                break;
        }

        ApplyTouchShake(ref scaleMultiplier, ref rotationZ);

        animationRoot.localScale = new Vector3(
            _baseAnimationScale.x * Mathf.Max(0f, scaleMultiplier.x),
            _baseAnimationScale.y * Mathf.Max(0f, scaleMultiplier.y),
            _baseAnimationScale.z * Mathf.Max(0f, scaleMultiplier.z));
        animationRoot.localRotation = _baseAnimationRotation * Quaternion.Euler(0f, 0f, rotationZ);
    }

    /// <summary>
    /// 为 idle 状态叠加轻微晃动。
    /// </summary>
    /// <param name="scaleMultiplier">当前缩放倍率。</param>
    /// <param name="rotationZ">当前 Z 轴旋转角度。</param>
    private void ApplyIdleWobble(ref Vector3 scaleMultiplier, ref float rotationZ)
    {
        if (!idleWobbleEnabled)
            return;

        float time = Time.time * Mathf.Max(0f, idleWobbleSpeed);
        float bounce = Mathf.Sin(time);
        float spring = Mathf.Abs(bounce);
        float sway = Mathf.Sin(time * 0.5f);
        scaleMultiplier.x *= Mathf.Max(0f, 1f + spring * idleWobbleScaleX);
        scaleMultiplier.y *= Mathf.Max(0f, 1f - spring * idleWobbleScaleY);
        rotationZ += sway * idleWobbleRotation;
    }

    /// <summary>
    /// 为路过或采集触碰叠加快速晃动。
    /// </summary>
    /// <param name="scaleMultiplier">当前缩放倍率。</param>
    /// <param name="rotationZ">当前 Z 轴旋转角度。</param>
    private void ApplyTouchShake(ref Vector3 scaleMultiplier, ref float rotationZ)
    {
        if (_touchShakeTimer <= 0f || collectShakeDuration <= 0f)
            return;

        float progress = 1f - Mathf.Clamp01(_touchShakeTimer / collectShakeDuration);
        float fade = 1f - progress;
        float shake = Mathf.Sin(progress * Mathf.PI * 8f) * fade;
        float scale = Mathf.Max(0f, 1f + Mathf.Abs(shake) * collectShakeScale);
        scaleMultiplier.x *= scale;
        scaleMultiplier.y *= Mathf.Max(0f, 1f + Mathf.Abs(shake) * collectShakeScale * 0.5f);
        rotationZ += shake * collectShakeRotation;
    }

    /// <summary>
    /// 对 0 到 1 的进度做平滑插值。
    /// </summary>
    /// <param name="value">原始进度。</param>
    /// <returns>平滑后的进度。</returns>
    private float SmoothStep01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }
}
