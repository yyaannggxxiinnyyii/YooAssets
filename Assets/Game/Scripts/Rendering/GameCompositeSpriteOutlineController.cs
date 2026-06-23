using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 为由多个 SpriteRenderer 拼装的角色生成统一外轮廓描边。
/// </summary>
public sealed class GameCompositeSpriteOutlineController : MonoBehaviour
{
    private const string OutlineRootName = "CompositeOutlineRoot";
    private const string SilhouetteMaterialResourcePath = "GameMaterials/SpriteSilhouette";
    private const string SilhouetteShaderName = "Game/Sprite Silhouette";

    private static readonly Vector2[] OutlineDirections =
    {
        Vector2.right,
        Vector2.left,
        Vector2.up,
        Vector2.down,
        new Vector2(1f, 1f).normalized,
        new Vector2(-1f, 1f).normalized,
        new Vector2(1f, -1f).normalized,
        new Vector2(-1f, -1f).normalized
    };

    private static Material _fallbackSilhouetteMaterial;

    [Header("描边目标")]
    [Tooltip("需要生成统一外轮廓的 SpriteRenderer 根节点。为空时使用当前节点。")]
    [SerializeField] private Transform renderRoot;
    [Tooltip("运行时生成的描边层父节点。")]
    [SerializeField] private Transform outlineRoot;
    [Tooltip("用于绘制剪影描边的材质。为空时从 Resources/GameMaterials/SpriteSilhouette 加载。")]
    [SerializeField] private Material silhouetteMaterial;

    [Header("描边参数")]
    [Tooltip("组合描边颜色。")]
    [SerializeField] private Color outlineColor = Color.white;
    [Tooltip("组合描边宽度，按 Sprite 像素单位换算到世界距离。")]
    [SerializeField] private float outlineWidth = 1.4f;
    [Tooltip("组合描边外扩层数，层数越高描边越厚实。")]
    [SerializeField] private int outlineLayers = 3;
    [Tooltip("组合描边发光亮度系数。")]
    [SerializeField] private float glowIntensity = 1f;
    [Tooltip("组合描边呼吸脉冲强度，0 表示不脉冲。")]
    [SerializeField] private float pulseStrength;
    [Tooltip("组合描边呼吸脉冲速度。")]
    [SerializeField] private float pulseSpeed = 2f;
    [Tooltip("组合描边相对角色最低 Sorting Order 的偏移。")]
    [SerializeField] private int outlineSortingOffset = -1;

    private readonly List<SpriteRenderer> _sourceRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> _outlineRenderers = new List<SpriteRenderer>();
    private int _lowestSourceSortingOrder;
    private bool _outlineVisible = true;

    private void Awake()
    {
        EnsureInitialized();
        Apply();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        Apply();
    }

    private void LateUpdate()
    {
        SyncOutlineRenderers();
    }

    private void OnValidate()
    {
        outlineWidth = Mathf.Max(0f, outlineWidth);
        outlineLayers = Mathf.Clamp(outlineLayers, 1, 12);
        glowIntensity = Mathf.Max(0f, glowIntensity);
        pulseStrength = Mathf.Clamp01(pulseStrength);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
    }

    /// <summary>
    /// 使用指定参数配置组合描边并立即应用。
    /// </summary>
    /// <param name="settings">描边参数。</param>
    public void SetOutline(GameSpriteOutlineSettings settings)
    {
        if (settings == null)
            return;

        outlineColor = settings.OutlineColor;
        outlineWidth = settings.OutlineWidth;
        outlineLayers = settings.OutlineLayers;
        glowIntensity = settings.GlowIntensity;
        pulseStrength = settings.PulseStrength;
        pulseSpeed = settings.PulseSpeed;
        EnsureInitialized();
        Apply();
    }

    /// <summary>
    /// 配置指定根节点的组合描边组件。
    /// </summary>
    /// <param name="targetRoot">角色美术根节点。</param>
    /// <param name="settings">描边参数。</param>
    /// <returns>组合描边控制器。</returns>
    public static GameCompositeSpriteOutlineController Configure(Transform targetRoot, GameSpriteOutlineSettings settings)
    {
        if (targetRoot == null || settings == null)
            return null;

        GameCompositeSpriteOutlineController controller = targetRoot.GetComponent<GameCompositeSpriteOutlineController>();
        if (controller == null)
            controller = targetRoot.gameObject.AddComponent<GameCompositeSpriteOutlineController>();

        controller.renderRoot = targetRoot;
        controller.SetOutline(settings);
        return controller;
    }

    /// <summary>
    /// 应用当前描边配置。
    /// </summary>
    public void Apply()
    {
        DisableNestedSingleOutlineControllers();
        RefreshSourceRenderers();
        EnsureOutlineRenderers();
        SyncOutlineRenderers();
    }

    /// <summary>
    /// 切换运行时生成的组合描边显示状态。
    /// </summary>
    /// <param name="visible">是否显示描边。</param>
    public void SetOutlineVisible(bool visible)
    {
        _outlineVisible = visible;
        for (int i = 0; i < _outlineRenderers.Count; i++)
        {
            if (_outlineRenderers[i] != null)
                _outlineRenderers[i].gameObject.SetActive(visible && outlineWidth > 0f);
        }
    }

    /// <summary>
    /// 确保根节点和描边父节点存在。
    /// </summary>
    private void EnsureInitialized()
    {
        if (renderRoot == null)
            renderRoot = transform;

        if (outlineRoot != null)
            return;

        Transform existedRoot = renderRoot.Find(OutlineRootName);
        if (existedRoot != null)
        {
            outlineRoot = existedRoot;
            outlineRoot.SetParent(renderRoot.parent, true);
            return;
        }

        GameObject outlineRootObject = new GameObject(OutlineRootName);
        outlineRootObject.transform.SetParent(renderRoot.parent, false);
        outlineRoot = outlineRootObject.transform;
    }

    /// <summary>
    /// 收集角色部件 SpriteRenderer，排除本控制器生成的描边层。
    /// </summary>
    private void RefreshSourceRenderers()
    {
        _sourceRenderers.Clear();
        _lowestSourceSortingOrder = int.MaxValue;
        if (renderRoot == null)
            return;

        SpriteRenderer[] renderers = renderRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null || spriteRenderer.sprite == null)
                continue;

            if (IsGeneratedOutlineRenderer(spriteRenderer))
                continue;

            _sourceRenderers.Add(spriteRenderer);
            _lowestSourceSortingOrder = Mathf.Min(_lowestSourceSortingOrder, spriteRenderer.sortingOrder);
        }

        if (_lowestSourceSortingOrder == int.MaxValue)
            _lowestSourceSortingOrder = 0;
    }

    /// <summary>
    /// 确保所有部件和描边方向都有对应剪影层。
    /// </summary>
    private void EnsureOutlineRenderers()
    {
        int layerCount = Mathf.Clamp(outlineLayers, 1, 12);
        int requiredCount = _sourceRenderers.Count * layerCount * OutlineDirections.Length;
        Material material = ResolveSilhouetteMaterial();
        while (_outlineRenderers.Count < requiredCount)
        {
            GameObject outlineObject = new GameObject($"CompositeOutline_{_outlineRenderers.Count}");
            outlineObject.transform.SetParent(outlineRoot, false);
            SpriteRenderer outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
            outlineRenderer.sharedMaterial = material;
            _outlineRenderers.Add(outlineRenderer);
        }

        for (int i = 0; i < _outlineRenderers.Count; i++)
        {
            if (_outlineRenderers[i] == null)
                continue;

            _outlineRenderers[i].gameObject.SetActive(_outlineVisible && i < requiredCount && outlineWidth > 0f);
        }
    }

    /// <summary>
    /// 同步剪影描边层的 Sprite、空间变换、颜色和排序。
    /// </summary>
    private void SyncOutlineRenderers()
    {
        if (renderRoot == null || outlineRoot == null)
            return;

        SyncOutlineRootTransform();
        RefreshSourceRenderers();
        EnsureOutlineRenderers();
        int rendererIndex = 0;
        int layerCount = Mathf.Clamp(outlineLayers, 1, 12);
        Material material = ResolveSilhouetteMaterial();
        for (int sourceIndex = 0; sourceIndex < _sourceRenderers.Count; sourceIndex++)
        {
            SpriteRenderer sourceRenderer = _sourceRenderers[sourceIndex];
            if (sourceRenderer == null || sourceRenderer.sprite == null)
                continue;

            float pixelsPerUnit = Mathf.Max(1f, sourceRenderer.sprite.pixelsPerUnit);
            float worldWidth = Mathf.Max(0f, outlineWidth) / pixelsPerUnit;
            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                float distance = worldWidth * (layerIndex + 1f) / layerCount;
                Color layerColor = GetOutlineLayerColor(layerIndex, layerCount);
                for (int directionIndex = 0; directionIndex < OutlineDirections.Length; directionIndex++)
                {
                    if (rendererIndex < 0 || rendererIndex >= _outlineRenderers.Count)
                        continue;

                    SpriteRenderer outlineRenderer = _outlineRenderers[rendererIndex];
                    rendererIndex++;
                    if (outlineRenderer == null)
                        continue;

                    SyncSingleOutlineRenderer(outlineRenderer, sourceRenderer, layerColor, material, OutlineDirections[directionIndex] * distance);
                }
            }
        }

        for (int i = rendererIndex; i < _outlineRenderers.Count; i++)
        {
            if (_outlineRenderers[i] != null)
                _outlineRenderers[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 同步单个剪影渲染器，让它匹配源部件并做世界空间外扩。
    /// </summary>
    /// <param name="outlineRenderer">剪影渲染器。</param>
    /// <param name="sourceRenderer">源部件渲染器。</param>
    /// <param name="layerColor">当前描边层颜色。</param>
    /// <param name="material">剪影材质。</param>
    /// <param name="worldOffset">世界空间偏移。</param>
    private void SyncSingleOutlineRenderer(
        SpriteRenderer outlineRenderer,
        SpriteRenderer sourceRenderer,
        Color layerColor,
        Material material,
        Vector2 worldOffset)
    {
        outlineRenderer.sprite = sourceRenderer.sprite;
        outlineRenderer.color = layerColor;
        outlineRenderer.flipX = sourceRenderer.flipX;
        outlineRenderer.flipY = sourceRenderer.flipY;
        outlineRenderer.drawMode = sourceRenderer.drawMode;
        outlineRenderer.size = sourceRenderer.size;
        outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        outlineRenderer.sortingOrder = _lowestSourceSortingOrder + outlineSortingOffset;
        outlineRenderer.sharedMaterial = material;
        outlineRenderer.gameObject.SetActive(_outlineVisible && outlineWidth > 0f);

        Transform outlineTransform = outlineRenderer.transform;
        Transform sourceTransform = sourceRenderer.transform;
        outlineTransform.position = sourceTransform.position + (Vector3)worldOffset;
        outlineTransform.rotation = sourceTransform.rotation;
        outlineTransform.localScale = sourceTransform.lossyScale;
    }

    /// <summary>
    /// 同步描边根节点到世界原点，避免继承角色翻转和缩放造成二次变换。
    /// </summary>
    private void SyncOutlineRootTransform()
    {
        if (outlineRoot == null)
            return;

        outlineRoot.position = Vector3.zero;
        outlineRoot.rotation = Quaternion.identity;
        outlineRoot.localScale = Vector3.one;
    }

    /// <summary>
    /// 判断 SpriteRenderer 是否属于运行时生成的描边层。
    /// </summary>
    /// <param name="spriteRenderer">待检查 SpriteRenderer。</param>
    /// <returns>属于描边层时返回 true。</returns>
    private bool IsGeneratedOutlineRenderer(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
            return false;

        if (outlineRoot != null && spriteRenderer.transform.IsChildOf(outlineRoot))
            return true;

        return spriteRenderer.gameObject.name.StartsWith("SpriteOutline_", System.StringComparison.Ordinal);
    }

    /// <summary>
    /// 关闭子部件旧的单 Sprite 描边，避免组合描边和内部描边同时出现。
    /// </summary>
    private void DisableNestedSingleOutlineControllers()
    {
        if (renderRoot == null)
            return;

        GameSpriteOutlineController[] controllers = renderRoot.GetComponentsInChildren<GameSpriteOutlineController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            GameSpriteOutlineController controller = controllers[i];
            if (controller == null)
                continue;

            controller.enabled = false;
            DisableGeneratedSpriteOutlineChildren(controller.transform);
        }
    }

    /// <summary>
    /// 关闭单 Sprite 描边脚本创建的剪影子节点。
    /// </summary>
    /// <param name="targetRoot">单 Sprite 描边脚本所在节点。</param>
    private void DisableGeneratedSpriteOutlineChildren(Transform targetRoot)
    {
        if (targetRoot == null)
            return;

        for (int i = 0; i < targetRoot.childCount; i++)
        {
            Transform child = targetRoot.GetChild(i);
            if (child == null)
                continue;

            if (child.gameObject.name.StartsWith("SpriteOutline_", System.StringComparison.Ordinal))
                child.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 计算指定描边层颜色，支持发光和脉冲效果。
    /// </summary>
    /// <param name="layerIndex">描边层索引。</param>
    /// <param name="layerCount">描边层总数。</param>
    /// <returns>描边层颜色。</returns>
    private Color GetOutlineLayerColor(int layerIndex, int layerCount)
    {
        float safeLayerCount = Mathf.Max(1, layerCount);
        float layerProgress = (layerIndex + 1f) / safeLayerCount;
        float pulse = 1f + Mathf.Sin(Time.time * Mathf.Max(0f, pulseSpeed)) * Mathf.Clamp01(pulseStrength);
        float brightness = Mathf.Max(0f, 1f + Mathf.Max(0f, glowIntensity) * 0.18f) * pulse;
        float outerAlpha = Mathf.Lerp(1f, Mathf.Clamp01(0.18f + glowIntensity * 0.08f), layerProgress);

        Color color = outlineColor;
        color.r *= brightness;
        color.g *= brightness;
        color.b *= brightness;
        color.a = Mathf.Clamp01(outlineColor.a * outerAlpha * pulse);
        return color;
    }

    /// <summary>
    /// 获取剪影描边材质，缺失资源时创建运行时兜底材质。
    /// </summary>
    /// <returns>可用剪影材质。</returns>
    private Material ResolveSilhouetteMaterial()
    {
        if (silhouetteMaterial != null)
            return silhouetteMaterial;

        silhouetteMaterial = Resources.Load<Material>(SilhouetteMaterialResourcePath);
        if (silhouetteMaterial != null)
            return silhouetteMaterial;

        if (_fallbackSilhouetteMaterial != null)
            return _fallbackSilhouetteMaterial;

        Shader shader = Shader.Find(SilhouetteShaderName);
        if (shader == null)
            return null;

        _fallbackSilhouetteMaterial = new Material(shader)
        {
            name = "RuntimeCompositeSpriteSilhouette"
        };
        return _fallbackSilhouetteMaterial;
    }
}
