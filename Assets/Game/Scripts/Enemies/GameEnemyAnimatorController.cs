using UnityEngine;

/// <summary>
/// 控制单张贴图敌人的移动动画参数和左右朝向。
/// </summary>
public sealed class GameEnemyAnimatorController : MonoBehaviour
{
    [Header("动画引用")]
    [Tooltip("敌人对象上的 Animator。为空时会自动查找或创建。")]
    [SerializeField] private Animator animator;
    [Tooltip("敌人贴图渲染器。为空时会自动从当前对象或子对象查找。")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("动画参数")]
    [Tooltip("Animator 中用于切换 idle/walk 的 bool 参数名。")]
    [SerializeField] private string movingParameterName = "IsMoving";
    [Tooltip("位置变化超过该速度时视为正在移动。")]
    [SerializeField] private float movingSpeedThreshold = 0.02f;

    [Header("朝向参数")]
    [Tooltip("敌人贴图默认是否朝右。当前敌人素材默认朝右时保持勾选。")]
    [SerializeField] private bool defaultFacesRight = true;
    [Tooltip("水平位移小于该值时不更新朝向，避免接近垂直移动时抖动。")]
    [SerializeField] private float facingDeadZone = 0.002f;

    private Vector3 _lastPosition;
    private int _movingParameterHash;
    private bool _hasMovingParameter;

    private void Awake()
    {
        CacheReferences(false);
        CacheAnimatorParameter();
        _lastPosition = transform.position;
    }

    private void OnEnable()
    {
        _lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        Vector3 deltaPosition = transform.position - _lastPosition;
        deltaPosition.z = 0f;

        RefreshMoveAnimation(deltaPosition);
        RefreshFacingDirection(deltaPosition);
        _lastPosition = transform.position;
    }

    private void OnValidate()
    {
        movingSpeedThreshold = Mathf.Max(0f, movingSpeedThreshold);
        facingDeadZone = Mathf.Max(0f, facingDeadZone);
    }

    /// <summary>
    /// 配置运行时敌人的动画控制器和渲染引用。
    /// </summary>
    /// <param name="controller">敌人动画状态机资源。</param>
    /// <param name="targetRenderer">敌人贴图渲染器。</param>
    public void Configure(RuntimeAnimatorController controller, SpriteRenderer targetRenderer)
    {
        if (targetRenderer != null)
            spriteRenderer = targetRenderer;

        CacheReferences(controller != null);
        if (animator != null)
        {
            if (controller != null)
                animator.runtimeAnimatorController = controller;

            animator.enabled = animator.runtimeAnimatorController != null;
        }

        CacheAnimatorParameter();
        if (animator != null && animator.enabled)
            animator.Rebind();

        _lastPosition = transform.position;
    }

    /// <summary>
    /// 缓存动画器和贴图渲染器，缺失动画器时为运行时敌人自动创建。
    /// </summary>
    /// <param name="createAnimatorWhenMissing">缺失动画器时是否自动创建。</param>
    private void CacheReferences(bool createAnimatorWhenMissing)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null && createAnimatorWhenMissing)
            animator = gameObject.AddComponent<Animator>();
    }

    /// <summary>
    /// 缓存 Animator 参数哈希，并确认状态机中存在对应 bool 参数。
    /// </summary>
    private void CacheAnimatorParameter()
    {
        _hasMovingParameter = false;
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(movingParameterName))
            return;

        _movingParameterHash = Animator.StringToHash(movingParameterName);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash != _movingParameterHash || parameter.type != AnimatorControllerParameterType.Bool)
                continue;

            _hasMovingParameter = true;
            return;
        }

        Debug.LogWarning($"[EnemyAnimator] Animator 缺少 bool 参数：{movingParameterName}", this);
    }

    /// <summary>
    /// 根据敌人本帧位移速度更新 idle/walk 动画状态。
    /// </summary>
    /// <param name="deltaPosition">敌人本帧位移。</param>
    private void RefreshMoveAnimation(Vector3 deltaPosition)
    {
        if (animator == null || !_hasMovingParameter)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        bool isMoving = deltaPosition.magnitude / deltaTime > movingSpeedThreshold;
        animator.SetBool(_movingParameterHash, isMoving);
    }

    /// <summary>
    /// 根据水平位移方向翻转单张敌人贴图。
    /// </summary>
    /// <param name="deltaPosition">敌人本帧位移。</param>
    private void RefreshFacingDirection(Vector3 deltaPosition)
    {
        if (spriteRenderer == null || Mathf.Abs(deltaPosition.x) <= facingDeadZone)
            return;

        bool movingRight = deltaPosition.x > 0f;
        spriteRenderer.flipX = defaultFacesRight ? !movingRight : movingRight;
    }
}
