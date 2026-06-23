using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 管理战斗地图背景、四周边界显示和战斗区域逻辑范围。
/// </summary>
public sealed class GameArenaController : MonoBehaviour
{
    [Header("地图配置")]
    [Tooltip("战斗区域背景显示尺寸。")]
    [SerializeField] private Vector2 arenaSize = new Vector2(24f, 24f);
    [Tooltip("角色、敌人和掉落物距离边界的内缩距离。")]
    [SerializeField] private float arenaInnerPadding = 0.7f;
    [Tooltip("战斗区域背景贴图。")]
    [SerializeField] private Sprite backgroundSprite;
    [Tooltip("上下边界位置偏移，X/Y 分别影响上下两条边界。")]
    [SerializeField] private Vector2 horizontalBorderOffset;
    [Tooltip("左右边界位置偏移，X/Y 分别影响左右两条边界。")]
    [SerializeField] private Vector2 verticalBorderOffset;

    [Header("地图对象")]
    [Tooltip("地图背景渲染器。")]
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [Tooltip("上边界渲染器。")]
    [SerializeField] private SpriteRenderer topBorderRenderer;
    [Tooltip("下边界渲染器。")]
    [SerializeField] private SpriteRenderer bottomBorderRenderer;
    [Tooltip("左边界渲染器。")]
    [SerializeField] private SpriteRenderer leftBorderRenderer;
    [Tooltip("右边界渲染器。")]
    [SerializeField] private SpriteRenderer rightBorderRenderer;

    private const float BorderThickness = 0.16f;
    private Sprite _squareSprite;
    private Vector2 _lastArenaSize;
    private Vector2 _lastHorizontalBorderOffset;
    private Vector2 _lastVerticalBorderOffset;
    private Sprite _lastBackgroundSprite;
    private Vector3 _lastTopBorderPosition;
    private Vector3 _lastBottomBorderPosition;
    private Vector3 _lastLeftBorderPosition;
    private Vector3 _lastRightBorderPosition;
#if UNITY_EDITOR
    private bool _editorRefreshQueued;
#endif

    /// <summary>
    /// 获取战斗区域内部最小坐标。
    /// </summary>
    public Vector2 ArenaMin => new Vector2(GetLeftWallX() + arenaInnerPadding, GetBottomWallY() + arenaInnerPadding);

    /// <summary>
    /// 获取战斗区域内部最大坐标。
    /// </summary>
    public Vector2 ArenaMax => new Vector2(GetRightWallX() - arenaInnerPadding, GetTopWallY() - arenaInnerPadding);

    /// <summary>
    /// 获取四周边界形成的世界坐标矩形，不包含内缩距离。
    /// </summary>
    public Rect WorldBounds => Rect.MinMaxRect(GetLeftWallX(), GetBottomWallY(), GetRightWallX(), GetTopWallY());

    private void Awake()
    {
        RefreshArena();
    }

    private void Update()
    {
        RefreshRuntimeState();
    }

    private void OnValidate()
    {
        arenaSize.x = Mathf.Max(0.1f, arenaSize.x);
        arenaSize.y = Mathf.Max(0.1f, arenaSize.y);
        arenaInnerPadding = Mathf.Max(0f, arenaInnerPadding);
#if UNITY_EDITOR
        QueueEditorRefreshArena();
#else
        RefreshArena();
#endif
    }

    /// <summary>
    /// 按当前配置刷新背景和四周边界。
    /// </summary>
    public void RefreshArena()
    {
        EnsureSquareSprite();
        ConfigureBackground();
        ConfigureBorders();
        CacheState();
    }

    /// <summary>
    /// 将世界坐标限制在战斗区域内部。
    /// </summary>
    /// <param name="position">待限制的世界坐标。</param>
    /// <returns>限制后的世界坐标。</returns>
    public Vector3 ClampPositionInsideArena(Vector3 position)
    {
        Vector2 min = ArenaMin;
        Vector2 max = ArenaMax;
        position.x = Mathf.Clamp(position.x, min.x, max.x);
        position.y = Mathf.Clamp(position.y, min.y, max.y);
        return position;
    }

    /// <summary>
    /// 运行时同步地图配置变化或手动拖动后的边界位置。
    /// </summary>
    private void RefreshRuntimeState()
    {
        if (HasConfigChanged())
        {
            RefreshArena();
            return;
        }

        if (HasBorderTransformChanged())
            CacheState();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 在编辑器校验完成后延迟刷新地图显示，避免 OnValidate 中直接修改渲染器触发 Unity SendMessage 警告。
    /// </summary>
    private void QueueEditorRefreshArena()
    {
        if (_editorRefreshQueued)
            return;

        _editorRefreshQueued = true;
        EditorApplication.delayCall += RefreshArenaAfterValidation;
    }

    /// <summary>
    /// 执行编辑器延迟地图刷新。
    /// </summary>
    private void RefreshArenaAfterValidation()
    {
        EditorApplication.delayCall -= RefreshArenaAfterValidation;
        _editorRefreshQueued = false;

        if (this == null)
            return;

        RefreshArena();
    }
#endif

    /// <summary>
    /// 配置九宫格地图背景。
    /// </summary>
    private void ConfigureBackground()
    {
        if (backgroundRenderer == null)
            return;

        backgroundRenderer.sprite = backgroundSprite != null ? backgroundSprite : _squareSprite;
        backgroundRenderer.color = Color.white;
        backgroundRenderer.drawMode = SpriteDrawMode.Sliced;
        backgroundRenderer.size = arenaSize;
        backgroundRenderer.sortingLayerName = "Default";
        backgroundRenderer.sortingOrder = -20;
        backgroundRenderer.transform.localPosition = new Vector3(0f, 0f, 2f);
        backgroundRenderer.transform.localRotation = Quaternion.identity;
        backgroundRenderer.transform.localScale = Vector3.one;
        backgroundRenderer.gameObject.SetActive(true);
    }

    /// <summary>
    /// 配置四条透明边界对象。
    /// </summary>
    private void ConfigureBorders()
    {
        ConfigureBorder(
            topBorderRenderer,
            new Vector3(horizontalBorderOffset.x, GetConfiguredTopWallY(), 1.5f),
            new Vector3(arenaSize.x, BorderThickness, 1f));
        ConfigureBorder(
            bottomBorderRenderer,
            new Vector3(horizontalBorderOffset.x, GetConfiguredBottomWallY(), 1.5f),
            new Vector3(arenaSize.x, BorderThickness, 1f));
        ConfigureBorder(
            leftBorderRenderer,
            new Vector3(GetConfiguredLeftWallX(), verticalBorderOffset.y, 1.5f),
            new Vector3(BorderThickness, arenaSize.y, 1f));
        ConfigureBorder(
            rightBorderRenderer,
            new Vector3(GetConfiguredRightWallX(), verticalBorderOffset.y, 1.5f),
            new Vector3(BorderThickness, arenaSize.y, 1f));
    }

    /// <summary>
    /// 配置单条透明边界对象。
    /// </summary>
    /// <param name="targetRenderer">边界渲染器。</param>
    /// <param name="localPosition">本地位置。</param>
    /// <param name="localScale">本地缩放。</param>
    private void ConfigureBorder(SpriteRenderer targetRenderer, Vector3 localPosition, Vector3 localScale)
    {
        if (targetRenderer == null)
            return;

        targetRenderer.sprite = _squareSprite;
        targetRenderer.color = Color.clear;
        targetRenderer.sortingLayerName = "Default";
        targetRenderer.sortingOrder = -10;
        targetRenderer.transform.localPosition = localPosition;
        targetRenderer.transform.localRotation = Quaternion.identity;
        targetRenderer.transform.localScale = localScale;
        targetRenderer.gameObject.SetActive(true);
    }

    /// <summary>
    /// 判断地图配置是否变化。
    /// </summary>
    /// <returns>发生变化时返回 true。</returns>
    private bool HasConfigChanged()
    {
        return _lastArenaSize != arenaSize
            || _lastHorizontalBorderOffset != horizontalBorderOffset
            || _lastVerticalBorderOffset != verticalBorderOffset
            || _lastBackgroundSprite != backgroundSprite;
    }

    /// <summary>
    /// 判断四周边界 Transform 是否被手动移动。
    /// </summary>
    /// <returns>边界 Transform 变化时返回 true。</returns>
    private bool HasBorderTransformChanged()
    {
        return _lastTopBorderPosition != GetRendererPosition(topBorderRenderer)
            || _lastBottomBorderPosition != GetRendererPosition(bottomBorderRenderer)
            || _lastLeftBorderPosition != GetRendererPosition(leftBorderRenderer)
            || _lastRightBorderPosition != GetRendererPosition(rightBorderRenderer);
    }

    /// <summary>
    /// 缓存当前地图配置和边界位置。
    /// </summary>
    private void CacheState()
    {
        _lastArenaSize = arenaSize;
        _lastHorizontalBorderOffset = horizontalBorderOffset;
        _lastVerticalBorderOffset = verticalBorderOffset;
        _lastBackgroundSprite = backgroundSprite;
        _lastTopBorderPosition = GetRendererPosition(topBorderRenderer);
        _lastBottomBorderPosition = GetRendererPosition(bottomBorderRenderer);
        _lastLeftBorderPosition = GetRendererPosition(leftBorderRenderer);
        _lastRightBorderPosition = GetRendererPosition(rightBorderRenderer);
    }

    /// <summary>
    /// 获取渲染器当前世界坐标。
    /// </summary>
    /// <param name="targetRenderer">目标渲染器。</param>
    /// <returns>渲染器世界坐标。</returns>
    private Vector3 GetRendererPosition(SpriteRenderer targetRenderer)
    {
        return targetRenderer != null ? targetRenderer.transform.position : Vector3.zero;
    }

    /// <summary>
    /// 确保透明边界使用的基础方块 Sprite 存在。
    /// </summary>
    private void EnsureSquareSprite()
    {
        if (_squareSprite != null)
            return;

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        _squareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    /// <summary>
    /// 获取基于配置的上边界本地 Y 坐标。
    /// </summary>
    /// <returns>上边界本地 Y 坐标。</returns>
    private float GetConfiguredTopWallY()
    {
        return arenaSize.y * 0.5f + horizontalBorderOffset.y;
    }

    /// <summary>
    /// 获取基于配置的下边界本地 Y 坐标。
    /// </summary>
    /// <returns>下边界本地 Y 坐标。</returns>
    private float GetConfiguredBottomWallY()
    {
        return -arenaSize.y * 0.5f - horizontalBorderOffset.y;
    }

    /// <summary>
    /// 获取基于配置的左边界本地 X 坐标。
    /// </summary>
    /// <returns>左边界本地 X 坐标。</returns>
    private float GetConfiguredLeftWallX()
    {
        return -arenaSize.x * 0.5f - verticalBorderOffset.x;
    }

    /// <summary>
    /// 获取基于配置的右边界本地 X 坐标。
    /// </summary>
    /// <returns>右边界本地 X 坐标。</returns>
    private float GetConfiguredRightWallX()
    {
        return arenaSize.x * 0.5f + verticalBorderOffset.x;
    }

    /// <summary>
    /// 获取当前上边界世界 Y 坐标。
    /// </summary>
    /// <returns>上边界世界 Y 坐标。</returns>
    private float GetTopWallY()
    {
        return topBorderRenderer != null ? topBorderRenderer.transform.position.y : transform.TransformPoint(0f, GetConfiguredTopWallY(), 0f).y;
    }

    /// <summary>
    /// 获取当前下边界世界 Y 坐标。
    /// </summary>
    /// <returns>下边界世界 Y 坐标。</returns>
    private float GetBottomWallY()
    {
        return bottomBorderRenderer != null ? bottomBorderRenderer.transform.position.y : transform.TransformPoint(0f, GetConfiguredBottomWallY(), 0f).y;
    }

    /// <summary>
    /// 获取当前左边界世界 X 坐标。
    /// </summary>
    /// <returns>左边界世界 X 坐标。</returns>
    private float GetLeftWallX()
    {
        return leftBorderRenderer != null ? leftBorderRenderer.transform.position.x : transform.TransformPoint(GetConfiguredLeftWallX(), 0f, 0f).x;
    }

    /// <summary>
    /// 获取当前右边界世界 X 坐标。
    /// </summary>
    /// <returns>右边界世界 X 坐标。</returns>
    private float GetRightWallX()
    {
        return rightBorderRenderer != null ? rightBorderRenderer.transform.position.x : transform.TransformPoint(GetConfiguredRightWallX(), 0f, 0f).x;
    }
}
