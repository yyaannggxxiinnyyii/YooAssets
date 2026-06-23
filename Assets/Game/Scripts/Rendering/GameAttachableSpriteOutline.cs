using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可直接挂载到场景物上的 Sprite 描边脚本，自动为目标 SpriteRenderer 生成外轮廓表现层。
/// </summary>
public sealed class GameAttachableSpriteOutline : MonoBehaviour
{
    private const string OutlineChildPrefix = "AttachableSpriteOutline_";
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
    [Tooltip("需要描边的 SpriteRenderer；为空时自动查找当前对象或子对象上的第一个 SpriteRenderer。")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [Tooltip("用于绘制剪影描边的材质；为空时从 Resources/GameMaterials/SpriteSilhouette 加载。")]
    [SerializeField] private Material silhouetteMaterial;

    [Header("描边参数")]
    [Tooltip("描边颜色。")]
    [SerializeField] private Color outlineColor = new Color(0.08f, 0.05f, 0.04f, 1f);
    [Tooltip("描边宽度，按 Sprite 像素单位换算到世界距离。")]
    [SerializeField] private float outlineWidth = 5f;
    [Tooltip("描边外扩层数，层数越高描边越厚实，但会生成更多辅助 SpriteRenderer。")]
    [SerializeField] private int outlineLayers = 2;
    [Tooltip("描边发光亮度系数。")]
    [SerializeField] private float glowIntensity = 0.2f;
    [Tooltip("描边呼吸脉冲强度，0 表示不脉冲。")]
    [SerializeField] private float pulseStrength;
    [Tooltip("描边呼吸脉冲速度。")]
    [SerializeField] private float pulseSpeed = 2f;
    [Tooltip("描边相对目标 SpriteRenderer 的 Sorting Order 偏移，通常为 -1 让描边位于本体后方。")]
    [SerializeField] private int sortingOrderOffset = -1;

    private readonly List<SpriteRenderer> _outlineRenderers = new List<SpriteRenderer>();

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

    private void OnDisable()
    {
        SetOutlineVisible(false);
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
    /// 按当前 Inspector 配置应用描边表现。
    /// </summary>
    public void Apply()
    {
        if (targetRenderer == null)
            return;

        EnsureOutlineRenderers();
        SyncOutlineRenderers();
    }

    /// <summary>
    /// 设置描边辅助渲染器是否显示。
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
    /// 确保目标 SpriteRenderer 和基础引用可用。
    /// </summary>
    private void EnsureInitialized()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    /// <summary>
    /// 确保描边需要的辅助 SpriteRenderer 数量足够。
    /// </summary>
    private void EnsureOutlineRenderers()
    {
        int layerCount = Mathf.Clamp(outlineLayers, 1, 12);
        int requiredCount = layerCount * OutlineDirections.Length;
        Material material = ResolveSilhouetteMaterial();
        while (_outlineRenderers.Count < requiredCount)
        {
            GameObject outlineObject = new GameObject($"{OutlineChildPrefix}{_outlineRenderers.Count}");
            outlineObject.transform.SetParent(targetRenderer.transform, false);
            SpriteRenderer outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
            outlineRenderer.sharedMaterial = material;
            _outlineRenderers.Add(outlineRenderer);
        }

        for (int i = 0; i < _outlineRenderers.Count; i++)
        {
            SpriteRenderer outlineRenderer = _outlineRenderers[i];
            if (outlineRenderer == null)
                continue;

            if (outlineRenderer.transform.parent != targetRenderer.transform)
                outlineRenderer.transform.SetParent(targetRenderer.transform, false);

            outlineRenderer.gameObject.SetActive(i < requiredCount && outlineWidth > 0f && isActiveAndEnabled);
        }
    }

    /// <summary>
    /// 同步所有描边辅助渲染器的 Sprite、偏移、颜色和排序。
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
                if (rendererIndex < 0 || rendererIndex >= _outlineRenderers.Count)
                    continue;

                SpriteRenderer outlineRenderer = _outlineRenderers[rendererIndex];
                if (outlineRenderer == null)
                    continue;

                outlineRenderer.sprite = sprite;
                outlineRenderer.color = layerColor;
                outlineRenderer.flipX = targetRenderer.flipX;
                outlineRenderer.flipY = targetRenderer.flipY;
                outlineRenderer.drawMode = targetRenderer.drawMode;
                outlineRenderer.size = targetRenderer.size;
                outlineRenderer.sortingLayerID = targetRenderer.sortingLayerID;
                outlineRenderer.sortingOrder = targetRenderer.sortingOrder + sortingOrderOffset;
                outlineRenderer.sharedMaterial = material;
                outlineRenderer.transform.localPosition = (Vector3)(OutlineDirections[directionIndex] * distance);
                outlineRenderer.transform.localRotation = Quaternion.identity;
                outlineRenderer.transform.localScale = Vector3.one;
                outlineRenderer.gameObject.SetActive(targetRenderer.enabled && sprite != null && outlineWidth > 0f && isActiveAndEnabled);
            }
        }

        for (int i = layerCount * OutlineDirections.Length; i < _outlineRenderers.Count; i++)
        {
            if (_outlineRenderers[i] != null)
                _outlineRenderers[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 计算指定描边层颜色，支持轻微发光和脉冲。
    /// </summary>
    /// <param name="layerIndex">描边层索引。</param>
    /// <param name="layerCount">描边层总数。</param>
    /// <returns>描边层颜色。</returns>
    private Color GetOutlineLayerColor(int layerIndex, int layerCount)
    {
        float safeLayerCount = Mathf.Max(1, layerCount);
        float layerProgress = (layerIndex + 1f) / safeLayerCount;
        float pulse = 1f + Mathf.Sin(Time.time * Mathf.Max(0f, pulseSpeed)) * Mathf.Clamp01(pulseStrength);
        float brightness = Mathf.Max(0f, 1f + glowIntensity * 0.18f) * pulse;
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
            name = "RuntimeAttachableSpriteSilhouette"
        };
        return _fallbackSilhouetteMaterial;
    }
}
