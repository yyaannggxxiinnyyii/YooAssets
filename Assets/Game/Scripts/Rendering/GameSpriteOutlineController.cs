using UnityEngine;

/// <summary>
/// 保存 Sprite 描边的可配置参数，供不同战斗对象在 Inspector 中独立调整。
/// </summary>
[System.Serializable]
public sealed class GameSpriteOutlineSettings
{
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 1.5f;
    [SerializeField] private int outlineLayers = 3;
    [SerializeField] private float glowIntensity = 0.9f;
    [SerializeField] private float pulseStrength;
    [SerializeField] private float pulseSpeed = 2f;

    /// <summary>
    /// 描边颜色。
    /// </summary>
    public Color OutlineColor => outlineColor;

    /// <summary>
    /// 描边宽度。
    /// </summary>
    public float OutlineWidth => Mathf.Max(0f, outlineWidth);

    /// <summary>
    /// 用于填充厚描边的外扩层数。
    /// </summary>
    public int OutlineLayers => Mathf.Clamp(outlineLayers, 1, 12);

    /// <summary>
    /// 发光强度。
    /// </summary>
    public float GlowIntensity => Mathf.Max(0f, glowIntensity);

    /// <summary>
    /// 脉冲强度。
    /// </summary>
    public float PulseStrength => Mathf.Clamp01(pulseStrength);

    /// <summary>
    /// 脉冲速度。
    /// </summary>
    public float PulseSpeed => Mathf.Max(0f, pulseSpeed);

    /// <summary>
    /// 创建一组描边参数。
    /// </summary>
    /// <param name="outlineColor">描边颜色。</param>
    /// <param name="outlineWidth">描边宽度。</param>
    /// <param name="glowIntensity">发光强度。</param>
    /// <param name="pulseStrength">脉冲强度。</param>
    /// <param name="pulseSpeed">脉冲速度。</param>
    public GameSpriteOutlineSettings(
        Color outlineColor,
        float outlineWidth,
        float glowIntensity,
        float pulseStrength = 0f,
        float pulseSpeed = 2f,
        int outlineLayers = 3)
    {
        this.outlineColor = outlineColor;
        this.outlineWidth = outlineWidth;
        this.outlineLayers = outlineLayers;
        this.glowIntensity = glowIntensity;
        this.pulseStrength = pulseStrength;
        this.pulseSpeed = pulseSpeed;
    }

    /// <summary>
    /// 创建默认描边参数，供 Unity 序列化使用。
    /// </summary>
    public GameSpriteOutlineSettings()
    {
    }
}

/// <summary>
/// 为 SpriteRenderer 应用外描边和发光轮廓效果。
/// </summary>
//[RequireComponent(typeof(SpriteRenderer))]
public sealed class GameSpriteOutlineController : MonoBehaviour
{
    private const string OutlineMaterialResourcePath = "GameMaterials/SpriteOutlineGlow";
    private const string SilhouetteMaterialResourcePath = "GameMaterials/SpriteSilhouette";
    private const string OutlineShaderName = "Game/Sprite Outline Glow";
    private const string SilhouetteShaderName = "Game/Sprite Silhouette";
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int PulseStrengthId = Shader.PropertyToID("_PulseStrength");
    private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
    private static Material _fallbackMaterial;
    private static Material _fallbackSilhouetteMaterial;
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

    [Header("渲染引用")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Material silhouetteMaterial;

    [Header("描边参数")]
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 1.25f;
    [SerializeField] private int outlineLayers = 3;
    [SerializeField] private float glowIntensity = 0.8f;
    [SerializeField] private float pulseStrength;
    [SerializeField] private float pulseSpeed = 2f;

    private MaterialPropertyBlock _propertyBlock;
    private readonly System.Collections.Generic.List<SpriteRenderer> _outlineRenderers = new System.Collections.Generic.List<SpriteRenderer>();

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
    /// 应用描边参数。
    /// </summary>
    public void Apply()
    {
        if (targetRenderer == null)
            return;

        Material material = ResolveMaterial();
        if (material != null && targetRenderer.sharedMaterial != material)
            targetRenderer.sharedMaterial = material;

        EnsureOutlineRenderers();
        SyncOutlineRenderers();
        targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(OutlineColorId, outlineColor);
        _propertyBlock.SetFloat(OutlineWidthId, 0f);
        _propertyBlock.SetFloat(GlowIntensityId, Mathf.Max(0f, glowIntensity));
        _propertyBlock.SetFloat(PulseStrengthId, Mathf.Clamp01(pulseStrength));
        _propertyBlock.SetFloat(PulseSpeedId, Mathf.Max(0f, pulseSpeed));
        targetRenderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>
    /// 设置描边表现并立即应用。
    /// </summary>
    /// <param name="color">描边颜色。</param>
    /// <param name="width">描边宽度。</param>
    /// <param name="glow">发光强度。</param>
    /// <param name="pulse">脉冲强度。</param>
    /// <param name="speed">脉冲速度。</param>
    public void SetOutline(Color color, float width, float glow, float pulse = 0f, float speed = 2f)
    {
        outlineColor = color;
        outlineWidth = Mathf.Max(0f, width);
        glowIntensity = Mathf.Max(0f, glow);
        pulseStrength = Mathf.Clamp01(pulse);
        pulseSpeed = Mathf.Max(0f, speed);
        EnsureInitialized();
        Apply();
    }

    /// <summary>
    /// 设置描边表现并立即应用。
    /// </summary>
    /// <param name="settings">描边参数。</param>
    public void SetOutline(GameSpriteOutlineSettings settings)
    {
        if (settings == null)
            return;

        SetOutline(
            settings.OutlineColor,
            settings.OutlineWidth,
            settings.GlowIntensity,
            settings.PulseStrength,
            settings.PulseSpeed);
        outlineLayers = settings.OutlineLayers;
        Apply();
    }

    /// <summary>
    /// 切换运行时生成的描边剪影渲染器显示状态。
    /// </summary>
    /// <param name="visible">是否显示描边。</param>
    public void SetOutlineVisible(bool visible)
    {
        for (int i = 0; i < _outlineRenderers.Count; i++)
        {
            if (_outlineRenderers[i] != null)
                _outlineRenderers[i].gameObject.SetActive(visible && outlineWidth > 0f);
        }
    }

    /// <summary>
    /// 配置指定 SpriteRenderer 的描边组件。
    /// </summary>
    /// <param name="spriteRenderer">目标 SpriteRenderer。</param>
    /// <param name="color">描边颜色。</param>
    /// <param name="width">描边宽度。</param>
    /// <param name="glow">发光强度。</param>
    /// <param name="pulse">脉冲强度。</param>
    /// <param name="speed">脉冲速度。</param>
    /// <returns>描边控制器。</returns>
    public static GameSpriteOutlineController Configure(
        SpriteRenderer spriteRenderer,
        Color color,
        float width,
        float glow,
        float pulse = 0f,
        float speed = 2f)
    {
        if (spriteRenderer == null)
            return null;

        GameSpriteOutlineController controller = spriteRenderer.GetComponent<GameSpriteOutlineController>();
        if (controller == null)
            controller = spriteRenderer.gameObject.AddComponent<GameSpriteOutlineController>();

        controller.targetRenderer = spriteRenderer;
        controller.SetOutline(color, width, glow, pulse, speed);
        return controller;
    }

    /// <summary>
    /// 配置指定 SpriteRenderer 的描边组件。
    /// </summary>
    /// <param name="spriteRenderer">目标 SpriteRenderer。</param>
    /// <param name="settings">描边参数。</param>
    /// <returns>描边控制器。</returns>
    public static GameSpriteOutlineController Configure(SpriteRenderer spriteRenderer, GameSpriteOutlineSettings settings)
    {
        if (spriteRenderer == null || settings == null)
            return null;

        GameSpriteOutlineController controller = spriteRenderer.GetComponent<GameSpriteOutlineController>();
        if (controller == null)
            controller = spriteRenderer.gameObject.AddComponent<GameSpriteOutlineController>();

        controller.targetRenderer = spriteRenderer;
        controller.SetOutline(settings);
        return controller;
    }

    /// <summary>
    /// 确保组件引用和属性缓存可用。
    /// </summary>
    private void EnsureInitialized()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// 确保厚描边需要的剪影 SpriteRenderer 数量足够。
    /// </summary>
    private void EnsureOutlineRenderers()
    {
        int layerCount = Mathf.Clamp(outlineLayers, 1, 12);
        int requiredCount = layerCount * OutlineDirections.Length;
        Material material = ResolveSilhouetteMaterial();
        while (_outlineRenderers.Count < requiredCount)
        {
            GameObject outlineObject = new GameObject($"SpriteOutline_{_outlineRenderers.Count}");
            outlineObject.transform.SetParent(targetRenderer.transform, false);
            SpriteRenderer outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
            outlineRenderer.sharedMaterial = material;
            _outlineRenderers.Add(outlineRenderer);
        }

        for (int i = 0; i < _outlineRenderers.Count; i++)
        {
            if (_outlineRenderers[i] == null)
                continue;

            if (_outlineRenderers[i].transform.parent != targetRenderer.transform)
                _outlineRenderers[i].transform.SetParent(targetRenderer.transform, false);

            _outlineRenderers[i].gameObject.SetActive(i < requiredCount && outlineWidth > 0f);
        }
    }

    /// <summary>
    /// 同步剪影描边层的 Sprite、偏移和排序。
    /// </summary>
    private void SyncOutlineRenderers()
    {
        if (targetRenderer == null || _outlineRenderers.Count <= 0)
            return;

        Sprite sprite = targetRenderer.sprite;
        float pixelsPerUnit = sprite != null ? Mathf.Max(1f, sprite.pixelsPerUnit) : 100f;
        int layerCount = Mathf.Clamp(outlineLayers, 1, 12);
        float worldWidth = Mathf.Max(0f, outlineWidth) / pixelsPerUnit;
        Material material = ResolveSilhouetteMaterial();
        for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
        {
            float distance = worldWidth * (layerIndex + 1f) / layerCount;
            Color layerColor = GetOutlineLayerColor(layerIndex, layerCount);
            for (int directionIndex = 0; directionIndex < OutlineDirections.Length; directionIndex++)
            {
                int rendererIndex = layerIndex * OutlineDirections.Length + directionIndex;
                if (rendererIndex < 0 || rendererIndex >= _outlineRenderers.Count || _outlineRenderers[rendererIndex] == null)
                    continue;

                SpriteRenderer outlineRenderer = _outlineRenderers[rendererIndex];
                outlineRenderer.sprite = sprite;
                outlineRenderer.color = layerColor;
                outlineRenderer.flipX = targetRenderer.flipX;
                outlineRenderer.flipY = targetRenderer.flipY;
                outlineRenderer.drawMode = targetRenderer.drawMode;
                outlineRenderer.size = targetRenderer.size;
                outlineRenderer.sortingLayerID = targetRenderer.sortingLayerID;
                outlineRenderer.sortingOrder = targetRenderer.sortingOrder - 1;
                outlineRenderer.sharedMaterial = material;
                outlineRenderer.transform.localPosition = (Vector3)(OutlineDirections[directionIndex] * distance);
                outlineRenderer.transform.localRotation = Quaternion.identity;
                outlineRenderer.transform.localScale = Vector3.one;
                outlineRenderer.gameObject.SetActive(targetRenderer.enabled && sprite != null && outlineWidth > 0f);
            }
        }
    }

    /// <summary>
    /// 计算指定描边层的颜色，使用亮度和透明度模拟发光与脉冲。
    /// </summary>
    /// <param name="layerIndex">描边层索引。</param>
    /// <param name="layerCount">描边层总数。</param>
    /// <returns>本层描边颜色。</returns>
    private Color GetOutlineLayerColor(int layerIndex, int layerCount)
    {
        float safeLayerCount = Mathf.Max(1, layerCount);
        float layerProgress = (layerIndex + 1f) / safeLayerCount;
        float pulse = 1f + Mathf.Sin(Time.time * Mathf.Max(0f, pulseSpeed)) * Mathf.Clamp01(pulseStrength);
        float glow = Mathf.Max(0f, glowIntensity);
        float brightness = Mathf.Max(0f, 1f + glow * 0.18f) * pulse;
        float outerAlpha = Mathf.Lerp(1f, Mathf.Clamp01(0.18f + glow * 0.08f), layerProgress);

        Color color = outlineColor;
        color.r *= brightness;
        color.g *= brightness;
        color.b *= brightness;
        color.a = Mathf.Clamp01(outlineColor.a * outerAlpha * pulse);
        return color;
    }

    /// <summary>
    /// 获取描边材质，缺失资源时创建运行时兜底材质。
    /// </summary>
    /// <returns>可用材质。</returns>
    private Material ResolveMaterial()
    {
        if (outlineMaterial != null)
            return outlineMaterial;

        outlineMaterial = Resources.Load<Material>(OutlineMaterialResourcePath);
        if (outlineMaterial != null)
            return outlineMaterial;

        if (_fallbackMaterial != null)
            return _fallbackMaterial;

        Shader shader = Shader.Find(OutlineShaderName);
        if (shader == null)
            return null;

        _fallbackMaterial = new Material(shader)
        {
            name = "RuntimeSpriteOutlineGlow"
        };
        return _fallbackMaterial;
    }

    /// <summary>
    /// 获取剪影材质，缺失资源时创建运行时兜底材质。
    /// </summary>
    /// <returns>可用材质。</returns>
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
            name = "RuntimeSpriteSilhouette"
        };
        return _fallbackSilhouetteMaterial;
    }
}
