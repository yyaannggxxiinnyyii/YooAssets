using UnityEngine;

/// <summary>
/// 控制玩家待机、移动动画切换，并根据鼠标位置同步角色美术朝向。
/// </summary>
public sealed class GamePlayerAnimatorController : MonoBehaviour
{
    [Header("动画引用")]
    [Tooltip("玩家美术节点上的 Animator。为空时会在子节点中自动查找。")]
    [SerializeField] private Animator animator;
    [Tooltip("需要按鼠标左右翻转的美术根节点，建议填写 PlayerVisual 或 VisualRoot。")]
    [SerializeField] private Transform facingRoot;

    [Header("动画参数")]
    [Tooltip("Animator 中用于切换 idle/walk 的 bool 参数名。")]
    [SerializeField] private string movingParameterName = "IsMoving";
    [Tooltip("位置变化超过该速度时视为正在移动。")]
    [SerializeField] private float movingSpeedThreshold = 0.05f;

    [Header("朝向参数")]
    [Tooltip("鼠标位于角色右侧时，美术根节点的 X 缩放正负号。")]
    [SerializeField] private float rightFacingScaleSign = 1f;

    private Camera _camera;
    private Vector3 _lastPosition;
    private float _initialFacingScaleX = 1f;
    private int _movingParameterHash;
    private bool _hasMovingParameter;

    private void Awake()
    {
        CacheReferences();
        CacheAnimatorParameters();
        _lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        RefreshMoveAnimation();
        RefreshFacingDirection();
        _lastPosition = transform.position;
    }

    private void OnValidate()
    {
        movingSpeedThreshold = Mathf.Max(0f, movingSpeedThreshold);
        if (Mathf.Approximately(rightFacingScaleSign, 0f))
            rightFacingScaleSign = 1f;
    }

    /// <summary>
    /// 缓存动画器、朝向节点和相机引用，缺失时尽量自动补齐。
    /// </summary>
    private void CacheReferences()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (facingRoot == null && animator != null)
            facingRoot = animator.transform;

        if (facingRoot != null)
            _initialFacingScaleX = Mathf.Abs(facingRoot.localScale.x);

        _camera = Camera.main;
    }

    /// <summary>
    /// 缓存 Animator 参数哈希，并确认目标参数存在。
    /// </summary>
    private void CacheAnimatorParameters()
    {
        _hasMovingParameter = false;
        if (animator == null)
            return;

        CacheMovingParameter();
    }

    /// <summary>
    /// 缓存移动动画参数。
    /// </summary>
    private void CacheMovingParameter()
    {
        if (string.IsNullOrWhiteSpace(movingParameterName))
            return;

        _movingParameterHash = Animator.StringToHash(movingParameterName);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash != _movingParameterHash || parameter.type != AnimatorControllerParameterType.Bool)
                continue;

            _hasMovingParameter = true;
            return;
        }

        Debug.LogWarning($"[PlayerAnimator] Animator 缺少 bool 参数：{movingParameterName}", this);
    }

    /// <summary>
    /// 根据玩家本帧位移速度更新 idle/walk 动画状态。
    /// </summary>
    private void RefreshMoveAnimation()
    {
        if (animator == null || !_hasMovingParameter)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 deltaPosition = transform.position - _lastPosition;
        deltaPosition.z = 0f;
        bool isMoving = deltaPosition.magnitude / deltaTime > movingSpeedThreshold;
        animator.SetBool(_movingParameterHash, isMoving);
    }

    /// <summary>
    /// 根据鼠标相对玩家的位置左右翻转角色美术根节点。
    /// </summary>
    private void RefreshFacingDirection()
    {
        if (facingRoot == null)
            return;

        if (_camera == null || !_camera.isActiveAndEnabled)
            _camera = Camera.main;

        if (_camera == null)
            return;

        Vector3 mouseWorldPosition = _camera.ScreenToWorldPoint(Input.mousePosition);
        float directionSign = mouseWorldPosition.x >= transform.position.x ? 1f : -1f;
        Vector3 targetScale = facingRoot.localScale;
        float scaleMagnitudeX = Mathf.Abs(targetScale.x);
        if (Mathf.Approximately(scaleMagnitudeX, 0f))
            scaleMagnitudeX = _initialFacingScaleX;

        targetScale.x = scaleMagnitudeX * directionSign * Mathf.Sign(rightFacingScaleSign);
        facingRoot.localScale = targetScale;
    }
}
