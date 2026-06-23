using Cinemachine;
using UnityEngine;

/// <summary>
/// 管理战斗相机的 Cinemachine 跟随、平滑移动和地图边界约束。
/// </summary>
public sealed class GameCameraController : MonoBehaviour
{
    [Header("相机引用")]
    [Tooltip("主相机，缺失时自动查找 Camera.main。")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("用于跟随玩家的 Cinemachine 虚拟相机。")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [Tooltip("虚拟相机边界约束组件。")]
    [SerializeField] private CinemachineConfiner2D confiner;
    [Tooltip("用于相机边界的 PolygonCollider2D。可直接引用地图根对象上已编辑好的碰撞器，代码不会修改它的形状。")]
    [SerializeField] private PolygonCollider2D cameraBoundsCollider;

    [Header("跟随参数")]
    [Tooltip("虚拟相机优先级。")]
    [SerializeField] private int cameraPriority = 20;
    [Tooltip("正交相机视野大小。")]
    [SerializeField] private float orthographicSize = 6f;
    [Tooltip("X 轴跟随阻尼。")]
    [SerializeField] private float xDamping = 0.45f;
    [Tooltip("Y 轴跟随阻尼。")]
    [SerializeField] private float yDamping = 0.45f;
    [Tooltip("目标在屏幕横向位置。")]
    [SerializeField] private float screenX = 0.5f;
    [Tooltip("目标在屏幕纵向位置。")]
    [SerializeField] private float screenY = 0.5f;

    [Header("构图参数")]
    [Tooltip("目标在该屏幕宽度范围内移动时，相机不会横向跟随。")]
    [SerializeField] private float deadZoneWidth;
    [Tooltip("目标在该屏幕高度范围内移动时，相机不会纵向跟随。")]
    [SerializeField] private float deadZoneHeight;
    [Tooltip("目标超出死区后，相机横向平滑跟随的软区宽度。")]
    [SerializeField] private float softZoneWidth = 0.8f;
    [Tooltip("目标超出死区后，相机纵向平滑跟随的软区高度。")]
    [SerializeField] private float softZoneHeight = 0.8f;

    [Header("边界参数")]
    [Tooltip("相机边界在地图边界基础上的额外扩展，负数会收缩边界。")]
    [SerializeField] private Vector2 boundsPadding;
    [Tooltip("Confiner 拐角阻尼。")]
    [SerializeField] private float confinerDamping = 0.2f;

    private const string CameraBoundsObjectName = "CameraBounds";

    private GameArenaController _arenaController;
    private Transform _currentTarget;
    private PolygonCollider2D _configuredCameraBoundsCollider;
    private PolygonCollider2D _roomCameraBoundsCollider;
    private Rect _lastCameraBounds;
    private bool _hasCameraBounds;

    private void Awake()
    {
        _configuredCameraBoundsCollider = cameraBoundsCollider;
        EnsureCameraSetup();
    }

    private void LateUpdate()
    {
        EnsureFollowTarget();
        ConfigureVirtualCamera();
        if (confiner != null)
            confiner.m_Damping = confinerDamping;

        RefreshBoundsIfChanged();
    }

    private void OnValidate()
    {
        orthographicSize = Mathf.Max(0.1f, orthographicSize);
        xDamping = Mathf.Max(0f, xDamping);
        yDamping = Mathf.Max(0f, yDamping);
        screenX = Mathf.Clamp01(screenX);
        screenY = Mathf.Clamp01(screenY);
        deadZoneWidth = Mathf.Clamp(deadZoneWidth, 0f, 2f);
        deadZoneHeight = Mathf.Clamp(deadZoneHeight, 0f, 2f);
        softZoneWidth = Mathf.Clamp(softZoneWidth, 0f, 2f);
        softZoneHeight = Mathf.Clamp(softZoneHeight, 0f, 2f);
        confinerDamping = Mathf.Max(0f, confinerDamping);
    }

    /// <summary>
    /// 绑定相机跟随目标和地图边界。
    /// </summary>
    /// <param name="target">玩家或其他相机跟随目标。</param>
    /// <param name="arenaController">战斗地图控制器。</param>
    public void BindTarget(Transform target, GameArenaController arenaController)
    {
        BindTarget(target, arenaController, null);
    }

    /// <summary>
    /// 绑定相机跟随目标、地图边界和房间自定义相机边界。
    /// </summary>
    /// <param name="target">玩家或其他相机跟随目标。</param>
    /// <param name="arenaController">战斗地图控制器。</param>
    /// <param name="roomCameraBoundsCollider">房间 Prefab 内手工配置的相机边界碰撞器。</param>
    public void BindTarget(Transform target, GameArenaController arenaController, PolygonCollider2D roomCameraBoundsCollider)
    {
        EnsureCameraSetup();
        _arenaController = arenaController;
        _currentTarget = target;
        SetRoomCameraBoundsCollider(roomCameraBoundsCollider);

        if (virtualCamera == null)
            return;

        virtualCamera.Follow = target;
        virtualCamera.LookAt = null;
        ConfigureVirtualCamera();
        RefreshCameraBounds(true);
    }

    /// <summary>
    /// 立即将主相机和虚拟相机贴到当前跟随目标位置，用于黑幕转场中的房间复位。
    /// </summary>
    public void ForceSnapToTarget()
    {
        EnsureCameraSetup();
        if (_currentTarget == null)
            return;

        Vector3 targetPosition = _currentTarget.position;
        if (mainCamera != null)
            mainCamera.transform.position = new Vector3(targetPosition.x, targetPosition.y, mainCamera.transform.position.z);

        if (virtualCamera != null)
        {
            virtualCamera.transform.position = new Vector3(targetPosition.x, targetPosition.y, virtualCamera.transform.position.z);
            virtualCamera.PreviousStateIsValid = false;
        }
    }

    /// <summary>
    /// 确保虚拟相机持续跟随当前玩家目标。
    /// </summary>
    private void EnsureFollowTarget()
    {
        if (_currentTarget == null)
            return;

        EnsureCameraSetup();
        if (virtualCamera == null)
            return;

        if (virtualCamera.Follow != _currentTarget)
            virtualCamera.Follow = _currentTarget;

        if (virtualCamera.LookAt != null)
            virtualCamera.LookAt = null;
    }

    /// <summary>
    /// 确保主相机、虚拟相机和 Cinemachine 组件存在。
    /// </summary>
    private void EnsureCameraSetup()
    {
        EnsureMainCamera();
        EnsureVirtualCamera();
        EnsureConfiner();
        ConfigureVirtualCamera();
    }

    /// <summary>
    /// 查找主相机并补齐 CinemachineBrain。
    /// </summary>
    private void EnsureMainCamera()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        if (mainCamera.GetComponent<CinemachineBrain>() == null)
            mainCamera.gameObject.AddComponent<CinemachineBrain>();
    }

    /// <summary>
    /// 查找或创建战斗虚拟相机。
    /// </summary>
    private void EnsureVirtualCamera()
    {
        if (virtualCamera == null)
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();

        if (virtualCamera != null)
            return;

        GameObject cameraObject = new GameObject("CombatVirtualCamera");
        cameraObject.transform.SetParent(transform);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        cameraObject.transform.rotation = Quaternion.identity;
        cameraObject.transform.localScale = Vector3.one;
        virtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
    }

    /// <summary>
    /// 确保虚拟相机带有边界约束扩展。
    /// </summary>
    private void EnsureConfiner()
    {
        if (virtualCamera == null)
            return;

        if (confiner == null)
            confiner = virtualCamera.GetComponent<CinemachineConfiner2D>();

        if (confiner == null)
            confiner = virtualCamera.gameObject.AddComponent<CinemachineConfiner2D>();
    }

    /// <summary>
    /// 创建或查找用于相机边界的 PolygonCollider2D。
    /// </summary>
    private void EnsureBoundsCollider()
    {
        if (_roomCameraBoundsCollider != null)
        {
            cameraBoundsCollider = _roomCameraBoundsCollider;
            return;
        }

        if (_configuredCameraBoundsCollider != null)
        {
            cameraBoundsCollider = _configuredCameraBoundsCollider;
            return;
        }

        if (cameraBoundsCollider != null)
            return;

        if (_arenaController != null)
        {
            cameraBoundsCollider = _arenaController.GetComponent<PolygonCollider2D>();
            if (cameraBoundsCollider != null)
                return;
        }

        GameObject boundsObject = GameObject.Find(CameraBoundsObjectName);
        if (boundsObject == null)
            boundsObject = new GameObject(CameraBoundsObjectName);

        ResetBoundsColliderTransform(boundsObject.transform);
        cameraBoundsCollider = boundsObject.GetComponent<PolygonCollider2D>();
        if (cameraBoundsCollider == null)
            cameraBoundsCollider = boundsObject.AddComponent<PolygonCollider2D>();

        cameraBoundsCollider.isTrigger = true;
    }

    /// <summary>
    /// 判断碰撞器是否为运行时兜底创建的相机专用边界。
    /// </summary>
    /// <param name="targetCollider">待检查的碰撞器。</param>
    /// <returns>是运行时兜底相机边界时返回 true。</returns>
    private bool IsGeneratedCameraBoundsCollider(PolygonCollider2D targetCollider)
    {
        return targetCollider != null && targetCollider.gameObject.name == CameraBoundsObjectName;
    }

    /// <summary>
    /// 将相机边界对象固定在世界原点，保证路径点使用世界坐标时不偏移。
    /// </summary>
    /// <param name="boundsTransform">相机边界 Transform。</param>
    private void ResetBoundsColliderTransform(Transform boundsTransform)
    {
        boundsTransform.SetParent(null);
        boundsTransform.position = Vector3.zero;
        boundsTransform.rotation = Quaternion.identity;
        boundsTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// 配置虚拟相机镜头和 FramingTransposer 平滑跟随参数。
    /// </summary>
    private void ConfigureVirtualCamera()
    {
        if (virtualCamera == null)
            return;

        virtualCamera.Priority = cameraPriority;
        virtualCamera.m_Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        virtualCamera.m_Lens.OrthographicSize = orthographicSize;

        CinemachineFramingTransposer transposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (transposer == null)
            transposer = virtualCamera.AddCinemachineComponent<CinemachineFramingTransposer>();

        if (transposer == null)
            return;

        transposer.m_XDamping = xDamping;
        transposer.m_YDamping = yDamping;
        transposer.m_ZDamping = 0f;
        transposer.m_ScreenX = screenX;
        transposer.m_ScreenY = screenY;
        transposer.m_CameraDistance = 10f;
        transposer.m_DeadZoneWidth = deadZoneWidth;
        transposer.m_DeadZoneHeight = deadZoneHeight;
        transposer.m_SoftZoneWidth = softZoneWidth;
        transposer.m_SoftZoneHeight = softZoneHeight;
    }

    /// <summary>
    /// 检测地图边界变化并同步到 CinemachineConfiner2D。
    /// </summary>
    private void RefreshBoundsIfChanged()
    {
        if (_arenaController == null && _roomCameraBoundsCollider == null)
            return;

        RefreshCameraBounds(false);
    }

    /// <summary>
    /// 刷新相机边界多边形。
    /// </summary>
    /// <param name="forceRefresh">是否强制刷新。</param>
    private void RefreshCameraBounds(bool forceRefresh)
    {
        if (_arenaController == null && _roomCameraBoundsCollider == null)
            return;

        if (_roomCameraBoundsCollider != null)
        {
            cameraBoundsCollider = _roomCameraBoundsCollider;
            ApplyConfinerBounds(forceRefresh);
            return;
        }

        Rect cameraBounds = ExpandBounds(_arenaController.WorldBounds);
        if (!forceRefresh && _hasCameraBounds && Approximately(_lastCameraBounds, cameraBounds))
            return;

        EnsureBoundsCollider();
        if (cameraBoundsCollider == null)
            return;

        if (IsGeneratedCameraBoundsCollider(cameraBoundsCollider))
        {
            cameraBoundsCollider.pathCount = 1;
            cameraBoundsCollider.SetPath(0, BuildBoundsPath(cameraBounds));
            cameraBoundsCollider.enabled = true;
        }

        if (confiner != null)
        {
            confiner.m_BoundingShape2D = cameraBoundsCollider;
            confiner.m_Damping = confinerDamping;
            confiner.m_MaxWindowSize = orthographicSize;
            confiner.InvalidateCache();
        }

        _lastCameraBounds = cameraBounds;
        _hasCameraBounds = true;
    }

    /// <summary>
    /// 设置当前房间自定义相机边界，传入空值时回退到地图矩形边界。
    /// </summary>
    /// <param name="roomCameraBoundsCollider">房间 Prefab 内手工配置的相机边界碰撞器。</param>
    private void SetRoomCameraBoundsCollider(PolygonCollider2D roomCameraBoundsCollider)
    {
        if (_roomCameraBoundsCollider == roomCameraBoundsCollider)
            return;

        _roomCameraBoundsCollider = roomCameraBoundsCollider;
        cameraBoundsCollider = roomCameraBoundsCollider != null ? roomCameraBoundsCollider : _configuredCameraBoundsCollider;
        _hasCameraBounds = false;
    }

    /// <summary>
    /// 将当前相机边界碰撞器应用到 CinemachineConfiner2D。
    /// </summary>
    /// <param name="forceRefresh">是否强制刷新缓存。</param>
    private void ApplyConfinerBounds(bool forceRefresh)
    {
        if (cameraBoundsCollider == null)
            return;

        if (IsGeneratedCameraBoundsCollider(cameraBoundsCollider))
        {
            cameraBoundsCollider.enabled = true;
            cameraBoundsCollider.isTrigger = true;
        }

        if (!forceRefresh && _hasCameraBounds)
            return;

        if (confiner != null)
        {
            confiner.m_BoundingShape2D = cameraBoundsCollider;
            confiner.m_Damping = confinerDamping;
            confiner.m_MaxWindowSize = orthographicSize;
            confiner.InvalidateCache();
        }

        _hasCameraBounds = true;
    }

    /// <summary>
    /// 按配置扩展或收缩地图边界。
    /// </summary>
    /// <param name="sourceBounds">地图世界边界。</param>
    /// <returns>相机约束边界。</returns>
    private Rect ExpandBounds(Rect sourceBounds)
    {
        float minX = sourceBounds.xMin - boundsPadding.x;
        float maxX = sourceBounds.xMax + boundsPadding.x;
        float minY = sourceBounds.yMin - boundsPadding.y;
        float maxY = sourceBounds.yMax + boundsPadding.y;
        if (minX > maxX)
            (minX, maxX) = (maxX, minX);
        if (minY > maxY)
            (minY, maxY) = (maxY, minY);

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// 构建相机边界矩形路径。
    /// </summary>
    /// <param name="bounds">相机世界边界。</param>
    /// <returns>多边形路径点。</returns>
    private Vector2[] BuildBoundsPath(Rect bounds)
    {
        return new[]
        {
            new Vector2(bounds.xMin, bounds.yMin),
            new Vector2(bounds.xMin, bounds.yMax),
            new Vector2(bounds.xMax, bounds.yMax),
            new Vector2(bounds.xMax, bounds.yMin)
        };
    }

    /// <summary>
    /// 判断两个矩形是否近似相等。
    /// </summary>
    /// <param name="left">左侧矩形。</param>
    /// <param name="right">右侧矩形。</param>
    /// <returns>近似相等时返回 true。</returns>
    private bool Approximately(Rect left, Rect right)
    {
        return Mathf.Approximately(left.xMin, right.xMin)
            && Mathf.Approximately(left.xMax, right.xMax)
            && Mathf.Approximately(left.yMin, right.yMin)
            && Mathf.Approximately(left.yMax, right.yMax);
    }
}
