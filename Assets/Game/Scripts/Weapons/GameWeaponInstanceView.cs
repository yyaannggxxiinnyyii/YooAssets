using UnityEngine;

/// <summary>
/// 控制单个武器实例的图标显示、镜像状态和枪口发射点。
/// </summary>
public sealed class GameWeaponInstanceView : MonoBehaviour
{
    [Header("显示")]
    [Tooltip("武器图标渲染器，用于显示当前武器 Sprite。")]
    [SerializeField] private SpriteRenderer weaponRenderer;
    [Tooltip("武器图标描边配置。")]
    [SerializeField] private GameSpriteOutlineSettings outlineSettings = new GameSpriteOutlineSettings(new Color(1f, 0.78f, 0.28f, 1f), 1.35f, 1f, 0.08f);
    [Header("发射点")]
    [Tooltip("武器枪口参考点，未配置时会自动查找名为 MuzzlePoint 的子节点。")]
    [SerializeField] private Transform muzzlePoint;

    private Vector3 _fireDirection = Vector3.right;
    private float _muzzleOffset = 0.36f;
    private GameSpriteOutlineController _outlineController;

    /// <summary>
    /// 当前武器朝向。
    /// </summary>
    public Vector3 FireDirection => _fireDirection;

    /// <summary>
    /// 当前枪口世界坐标。
    /// </summary>
    public Vector3 MuzzlePosition => transform.position + _fireDirection * _muzzleOffset;

    /// <summary>
    /// 初始化武器实例视图。
    /// </summary>
    /// <param name="weaponData">当前武器配置。</param>
    public void Initialize(WeaponData weaponData)
    {
        SetWeaponData(weaponData);
    }

    /// <summary>
    /// 更新武器静态数据，图标为空时隐藏武器图形但保留逻辑枪口点。
    /// </summary>
    /// <param name="weaponData">当前武器配置。</param>
    public void SetWeaponData(WeaponData weaponData)
    {
        EnsureRenderer();
        EnsureMuzzlePoint();
        weaponRenderer.sprite = weaponData != null ? weaponData.Icon : null;
        weaponRenderer.enabled = weaponRenderer.sprite != null;
        weaponRenderer.sortingOrder = 4;
        _outlineController = GameSpriteOutlineController.Configure(weaponRenderer, outlineSettings);
        if (_outlineController != null)
            _outlineController.SetOutlineVisible(weaponRenderer.enabled);
    }

    /// <summary>
    /// 应用玩家武器布局控制器计算出的显示和发射参数。
    /// </summary>
    /// <param name="position">武器世界坐标。</param>
    /// <param name="rotationAngle">武器世界旋转角度。</param>
    /// <param name="fireDirection">投射物发射方向。</param>
    /// <param name="mirrored">是否镜像显示。</param>
    /// <param name="muzzleOffset">枪口前向偏移。</param>
    /// <param name="visualScale">图标显示缩放。</param>
    public void ApplyLayout(
        Vector3 position,
        float rotationAngle,
        Vector3 fireDirection,
        bool mirrored,
        float muzzleOffset,
        float visualScale,
        int sortingOrder = 4)
    {
        EnsureRenderer();
        _fireDirection = fireDirection.sqrMagnitude > 0.01f ? fireDirection.normalized : Vector3.right;
        _muzzleOffset = Mathf.Max(0f, muzzleOffset);
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, 0f, rotationAngle);
        transform.localScale = Vector3.one;
        weaponRenderer.transform.localScale = Vector3.one * Mathf.Max(0.01f, visualScale);
        weaponRenderer.flipX = false;
        weaponRenderer.flipY = mirrored;
        weaponRenderer.sortingOrder = sortingOrder;
    }

    /// <summary>
    /// 切换武器图形显示状态，保留对象和枪口逻辑用于后续恢复。
    /// </summary>
    /// <param name="visible">是否显示武器图形。</param>
    public void SetVisualVisible(bool visible)
    {
        EnsureRenderer();
        weaponRenderer.enabled = visible && weaponRenderer.sprite != null;
        if (_outlineController == null)
            _outlineController = weaponRenderer.GetComponent<GameSpriteOutlineController>();

        if (_outlineController != null)
            _outlineController.SetOutlineVisible(weaponRenderer.enabled);
    }

    /// <summary>
    /// 确保武器对象持有 SpriteRenderer。
    /// </summary>
    private void EnsureRenderer()
    {
        if (weaponRenderer != null)
            return;

        weaponRenderer = GetComponent<SpriteRenderer>();
        if (weaponRenderer == null)
            weaponRenderer = GetComponentInChildren<SpriteRenderer>();

        if (weaponRenderer == null)
            weaponRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 自动查找预制体内名为 MuzzlePoint 的枪口点。
    /// </summary>
    private void EnsureMuzzlePoint()
    {
        if (muzzlePoint != null)
            return;

        Transform[] childTransforms = GetComponentsInChildren<Transform>();
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform childTransform = childTransforms[i];
            if (childTransform != transform && childTransform.name == "MuzzlePoint")
            {
                muzzlePoint = childTransform;
                return;
            }
        }
    }
}
