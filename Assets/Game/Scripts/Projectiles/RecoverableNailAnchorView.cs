using UnityEngine;

/// <summary>
/// 管理忠诚 III 锚点钉子的本地表现，包括召回贴图切换和阴影显隐。
/// </summary>
public sealed class RecoverableNailAnchorView : MonoBehaviour
{
    [Header("表现引用")]
    [Tooltip("锚点钉子的主贴图渲染器，召回时会替换成飞行钉子贴图。")]
    [SerializeField] private SpriteRenderer _nailRenderer;
    [Tooltip("锚点钉子的落地阴影对象，召回飞行时会隐藏。")]
    [SerializeField] private GameObject _shadowObject;

    /// <summary>
    /// 应用召回飞行表现，隐藏落地阴影并按配置替换钉子贴图。
    /// </summary>
    /// <param name="recallSprite">召回飞行时使用的钉子贴图。</param>
    public void ApplyRecallVisual(Sprite recallSprite)
    {
        if (_nailRenderer == null)
        {
            Debug.LogError("[Projectile] 锚点钉子缺少主贴图渲染器，无法切换召回贴图。", this);
            return;
        }

        if (recallSprite != null)
            _nailRenderer.sprite = recallSprite;

        if (_shadowObject != null)
            _shadowObject.SetActive(false);
    }
}
