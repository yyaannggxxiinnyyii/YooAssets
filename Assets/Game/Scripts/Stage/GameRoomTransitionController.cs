using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理房间切换时的全屏圆形黑幕转场，并在遮满时提供安全的房间内容切换回调。
/// </summary>
public sealed class GameRoomTransitionController : MonoBehaviour
{
    private static readonly int CenterPropertyId = Shader.PropertyToID("_Center");
    private static readonly int RadiusPropertyId = Shader.PropertyToID("_Radius");
    private static readonly int FeatherPropertyId = Shader.PropertyToID("_Feather");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    [Header("显示引用")]
    [Tooltip("承载转场黑幕的 Canvas；为空时会自动创建。")]
    [SerializeField] private Canvas transitionCanvas;
    [Tooltip("全屏黑幕 Image；为空时会自动创建。")]
    [SerializeField] private Image transitionImage;
    [Tooltip("圆形黑幕使用的 UI Shader；为空时尝试按名称查找，仍失败则退回普通淡入淡出黑屏。")]
    [SerializeField] private Shader circleTransitionShader;

    [Header("转场参数")]
    [Tooltip("黑幕收拢到玩家位置所需时间。")]
    [SerializeField] private float closeDuration = 0.45f;
    [Tooltip("黑幕从玩家位置扩散打开所需时间。")]
    [SerializeField] private float openDuration = 0.45f;
    [Tooltip("黑幕完全遮住画面后保留的时间，便于执行房间替换和相机复位。")]
    [SerializeField] private float coveredHoldDuration = 0.08f;
    [Tooltip("圆形边缘柔化宽度，数值越大边缘越软。")]
    [SerializeField] private float circleFeather = 0.035f;
    [Tooltip("圆形洞口最大半径，1.5 通常能覆盖全部屏幕角落。")]
    [SerializeField] private float maxCircleRadius = 1.5f;
    [Tooltip("转场黑幕颜色。")]
    [SerializeField] private Color transitionColor = Color.black;
    [Tooltip("转场 Canvas 排序值，需要高于普通 UI。")]
    [SerializeField] private int sortingOrder = 5000;

    private const string GeneratedCanvasName = "RoomTransitionCanvas";
    private const string GeneratedImageName = "RoomTransitionImage";
    private const string CircleShaderName = "Game/UI/CircleRoomTransition";

    private Material _runtimeMaterial;
    private Coroutine _transitionRoutine;
    private bool _isTransitioning;

    /// <summary>
    /// 当前是否正在播放房间转场。
    /// </summary>
    public bool IsTransitioning => _isTransitioning;

    private void Awake()
    {
        EnsureTransitionView();
        SetTransitionVisible(false);
        SetTransitionProgress(Vector2.one * 0.5f, maxCircleRadius);
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
    }

    /// <summary>
    /// 播放以世界坐标为中心的圆形房间转场，并在黑幕遮满后执行回调。
    /// </summary>
    /// <param name="worldCenter">转场圆心对应的世界坐标。</param>
    /// <param name="coveredCallback">画面完全遮住时执行的回调。</param>
    public void PlayWorldTransition(Vector3 worldCenter, Action coveredCallback)
    {
        PlayWorldTransition(worldCenter, coveredCallback, null);
    }

    /// <summary>
    /// 播放以世界坐标为中心的圆形房间转场，并在遮满和打开完成时执行回调。
    /// </summary>
    /// <param name="worldCenter">转场圆心对应的世界坐标。</param>
    /// <param name="coveredCallback">画面完全遮住时执行的回调。</param>
    /// <param name="completedCallback">画面重新打开后执行的回调。</param>
    public void PlayWorldTransition(Vector3 worldCenter, Action coveredCallback, Action completedCallback)
    {
        Camera targetCamera = Camera.main;
        Vector2 viewportCenter = targetCamera != null
            ? (Vector2)targetCamera.WorldToViewportPoint(worldCenter)
            : Vector2.one * 0.5f;
        PlayViewportTransition(viewportCenter, coveredCallback, completedCallback);
    }

    /// <summary>
    /// 播放以 Viewport 坐标为中心的圆形房间转场，并在黑幕遮满后执行回调。
    /// </summary>
    /// <param name="viewportCenter">圆心 Viewport 坐标，左下角为 0,0，右上角为 1,1。</param>
    /// <param name="coveredCallback">画面完全遮住时执行的回调。</param>
    public void PlayViewportTransition(Vector2 viewportCenter, Action coveredCallback)
    {
        PlayViewportTransition(viewportCenter, coveredCallback, null);
    }

    /// <summary>
    /// 播放以 Viewport 坐标为中心的圆形房间转场，并在遮满和打开完成时执行回调。
    /// </summary>
    /// <param name="viewportCenter">圆心 Viewport 坐标，左下角为 0,0，右上角为 1,1。</param>
    /// <param name="coveredCallback">画面完全遮住时执行的回调。</param>
    /// <param name="completedCallback">画面重新打开后执行的回调。</param>
    public void PlayViewportTransition(Vector2 viewportCenter, Action coveredCallback, Action completedCallback)
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = StartCoroutine(PlayTransitionRoutine(ClampViewportCenter(viewportCenter), coveredCallback, completedCallback));
    }

    /// <summary>
    /// 立即隐藏转场黑幕并停止当前转场。
    /// </summary>
    public void HideImmediate()
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = null;
        _isTransitioning = false;
        SetTransitionProgress(Vector2.one * 0.5f, maxCircleRadius);
        SetTransitionVisible(false);
    }

    /// <summary>
    /// 按关闭、遮满回调、打开的顺序播放转场。
    /// </summary>
    /// <param name="viewportCenter">圆形转场中心。</param>
    /// <param name="coveredCallback">遮满后执行的回调。</param>
    /// <returns>协程迭代器。</returns>
    private IEnumerator PlayTransitionRoutine(Vector2 viewportCenter, Action coveredCallback, Action completedCallback)
    {
        _isTransitioning = true;
        EnsureTransitionView();
        SetTransitionVisible(true);

        float coverRadius = CalculateCoverRadius(viewportCenter);
        yield return AnimateRadius(viewportCenter, coverRadius, 0f, closeDuration);
        coveredCallback?.Invoke();
        if (coveredHoldDuration > 0f)
            yield return WaitUnscaledSeconds(coveredHoldDuration);

        yield return AnimateRadius(viewportCenter, 0f, coverRadius, openDuration);

        SetTransitionVisible(false);
        _transitionRoutine = null;
        _isTransitioning = false;
        completedCallback?.Invoke();
    }

    /// <summary>
    /// 使用不受 Time.timeScale 影响的时间推进圆形半径。
    /// </summary>
    /// <param name="viewportCenter">圆形中心。</param>
    /// <param name="fromRadius">起始半径。</param>
    /// <param name="toRadius">目标半径。</param>
    /// <param name="duration">动画时长。</param>
    /// <returns>协程迭代器。</returns>
    private IEnumerator AnimateRadius(Vector2 viewportCenter, float fromRadius, float toRadius, float duration)
    {
        if (duration <= 0f)
        {
            SetTransitionProgress(viewportCenter, toRadius);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float rate = Mathf.Clamp01(elapsed / duration);
            float easedRate = Mathf.SmoothStep(0f, 1f, rate);
            SetTransitionProgress(viewportCenter, Mathf.Lerp(fromRadius, toRadius, easedRate));
            yield return null;
        }

        SetTransitionProgress(viewportCenter, toRadius);
    }

    /// <summary>
    /// 等待指定真实时间，不受暂停或战斗时间缩放影响。
    /// </summary>
    /// <param name="seconds">等待秒数。</param>
    /// <returns>协程迭代器。</returns>
    private IEnumerator WaitUnscaledSeconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 确保转场 Canvas、Image 和材质可用。
    /// </summary>
    private void EnsureTransitionView()
    {
        EnsureCanvas();
        EnsureImage();
        EnsureRuntimeMaterial();
    }

    /// <summary>
    /// 确保转场 Canvas 存在并处于最高显示层。
    /// </summary>
    private void EnsureCanvas()
    {
        if (transitionCanvas == null)
        {
            Transform existingCanvas = transform.Find(GeneratedCanvasName);
            if (existingCanvas != null)
                transitionCanvas = existingCanvas.GetComponent<Canvas>();
        }

        if (transitionCanvas == null)
        {
            GameObject canvasObject = new GameObject(GeneratedCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            transitionCanvas = canvasObject.GetComponent<Canvas>();
        }

        if (transitionCanvas.GetComponent<GraphicRaycaster>() == null)
            transitionCanvas.gameObject.AddComponent<GraphicRaycaster>();

        transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transitionCanvas.overrideSorting = true;
        transitionCanvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = transitionCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    /// <summary>
    /// 确保全屏黑幕 Image 存在。
    /// </summary>
    private void EnsureImage()
    {
        if (transitionImage == null && transitionCanvas != null)
        {
            Transform existingImage = transitionCanvas.transform.Find(GeneratedImageName);
            if (existingImage != null)
                transitionImage = existingImage.GetComponent<Image>();
        }

        if (transitionImage == null)
        {
            GameObject imageObject = new GameObject(GeneratedImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(transitionCanvas.transform, false);
            transitionImage = imageObject.GetComponent<Image>();
        }

        RectTransform rectTransform = transitionImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        transitionImage.raycastTarget = true;
        transitionImage.color = transitionColor;
        transitionImage.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 创建圆形转场材质，缺少 Shader 时保留普通 Image 作为淡入淡出兜底。
    /// </summary>
    private void EnsureRuntimeMaterial()
    {
        if (transitionImage == null)
            return;

        if (circleTransitionShader == null)
            circleTransitionShader = Shader.Find(CircleShaderName);

        if (circleTransitionShader == null)
        {
            transitionImage.material = null;
            return;
        }

        if (_runtimeMaterial == null || _runtimeMaterial.shader != circleTransitionShader)
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);

            _runtimeMaterial = new Material(circleTransitionShader);
        }

        transitionImage.material = _runtimeMaterial;
    }

    /// <summary>
    /// 设置转场圆形中心和半径。
    /// </summary>
    /// <param name="viewportCenter">圆心 Viewport 坐标。</param>
    /// <param name="radius">当前圆形透明半径。</param>
    private void SetTransitionProgress(Vector2 viewportCenter, float radius)
    {
        if (transitionImage == null)
            return;

        transitionImage.color = transitionColor;
        if (_runtimeMaterial == null)
        {
            float alpha = Mathf.InverseLerp(maxCircleRadius, 0f, radius);
            Color color = transitionColor;
            color.a *= alpha;
            transitionImage.color = color;
            return;
        }

        _runtimeMaterial.SetVector(CenterPropertyId, new Vector4(viewportCenter.x, viewportCenter.y, 0f, 0f));
        _runtimeMaterial.SetFloat(RadiusPropertyId, Mathf.Max(0f, radius));
        _runtimeMaterial.SetFloat(FeatherPropertyId, Mathf.Max(0.0001f, circleFeather));
        _runtimeMaterial.SetColor(ColorPropertyId, transitionColor);
    }

    /// <summary>
    /// 切换转场显示对象显隐。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    private void SetTransitionVisible(bool visible)
    {
        if (transitionCanvas != null)
            transitionCanvas.enabled = visible;

        if (transitionImage != null)
        {
            transitionImage.enabled = visible;
            transitionImage.raycastTarget = visible;
        }
    }

    /// <summary>
    /// 将圆心限制在屏幕范围内，避免异常坐标导致遮罩方向错乱。
    /// </summary>
    /// <param name="viewportCenter">原始圆心。</param>
    /// <returns>限制后的圆心。</returns>
    private Vector2 ClampViewportCenter(Vector2 viewportCenter)
    {
        return new Vector2(Mathf.Clamp01(viewportCenter.x), Mathf.Clamp01(viewportCenter.y));
    }

    /// <summary>
    /// 根据当前屏幕比例和圆心位置计算足以覆盖四角的最大圆半径。
    /// </summary>
    /// <param name="viewportCenter">圆心 Viewport 坐标。</param>
    /// <returns>覆盖全屏所需半径。</returns>
    private float CalculateCoverRadius(Vector2 viewportCenter)
    {
        float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 1f;
        float left = Mathf.Abs(viewportCenter.x) * aspect;
        float right = Mathf.Abs(1f - viewportCenter.x) * aspect;
        float bottom = Mathf.Abs(viewportCenter.y);
        float top = Mathf.Abs(1f - viewportCenter.y);
        float radius = Mathf.Max(
            new Vector2(left, bottom).magnitude,
            new Vector2(left, top).magnitude,
            new Vector2(right, bottom).magnitude,
            new Vector2(right, top).magnitude);
        return Mathf.Max(maxCircleRadius, radius + Mathf.Max(0f, circleFeather));
    }
}
