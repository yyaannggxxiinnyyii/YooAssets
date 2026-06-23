using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂载在玩家对象上，负责实时根据鼠标方向排列玩家持有的武器实例。
/// </summary>
public sealed class GamePlayerWeaponLayoutController : MonoBehaviour
{
    [Header("挂点")]
    [Tooltip("武器实例运行时父节点，为空时直接挂到玩家对象下。")]
    [SerializeField] private Transform weaponRoot;

    [Header("武器前后层次")]
    [Tooltip("前景武器相对玩家中心的本地偏移，x 会随角色朝向镜像。")]
    [SerializeField] private Vector2 frontWeaponOffset = new Vector2(0.42f, -0.05f);
    [Tooltip("背景武器相对玩家中心的本地偏移，x 会随角色朝向镜像。")]
    [SerializeField] private Vector2 backWeaponOffset = new Vector2(-0.22f, 0.42f);
    [Tooltip("同一层后续武器的额外偏移。")]
    [SerializeField] private Vector2 stackedWeaponOffset = new Vector2(0.16f, 0.12f);
    [Tooltip("前景武器排序值，应高于角色主体。")]
    [SerializeField] private int frontWeaponSortingOrder = 8;
    [Tooltip("背景武器排序值，应低于角色主体。")]
    [SerializeField] private int backWeaponSortingOrder = -1;

    [Header("武器朝向")]
    [Tooltip("前景武器相对瞄准方向的旋转偏移。")]
    [SerializeField] private float frontWeaponRotationOffset = -8f;
    [Tooltip("背景武器相对瞄准方向的旋转偏移。")]
    [SerializeField] private float backWeaponRotationOffset = 10f;
    [Tooltip("武器显示角度限制，避免极端角度导致贴图倒置。")]
    [SerializeField] private float maxDisplayAngle = 72f;
    [Tooltip("枪口相对武器中心沿自身前方的偏移。")]
    [SerializeField] private float muzzleOffset = 0.36f;
    [Tooltip("武器图标显示缩放。")]
    [SerializeField] private float weaponVisualScale = 0.2f;

    private readonly List<GameWeaponInstanceView> _weaponInstances = new List<GameWeaponInstanceView>();
    private Camera _camera;
    private Vector3 _aimDirection = Vector3.right;

    /// <summary>
    /// 武器实例运行时父节点。
    /// </summary>
    public Transform WeaponRoot => weaponRoot != null ? weaponRoot : transform;

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void OnValidate()
    {
        maxDisplayAngle = Mathf.Clamp(maxDisplayAngle, 0f, 89f);
        muzzleOffset = Mathf.Max(0f, muzzleOffset);
        weaponVisualScale = Mathf.Max(0.01f, weaponVisualScale);
    }

    private void LateUpdate()
    {
        RefreshWeaponLayouts();
    }

    /// <summary>
    /// 注册武器实例，实例对象会被挂到武器根节点下。
    /// </summary>
    /// <param name="weaponInstance">武器实例视图。</param>
    public void RegisterWeapon(GameWeaponInstanceView weaponInstance)
    {
        if (weaponInstance == null || _weaponInstances.Contains(weaponInstance))
            return;

        weaponInstance.transform.SetParent(WeaponRoot, false);
        _weaponInstances.Add(weaponInstance);
        RefreshWeaponLayouts();
    }

    /// <summary>
    /// 注销武器实例。
    /// </summary>
    /// <param name="weaponInstance">武器实例视图。</param>
    public void UnregisterWeapon(GameWeaponInstanceView weaponInstance)
    {
        if (weaponInstance == null)
            return;

        _weaponInstances.Remove(weaponInstance);
        RefreshWeaponLayouts();
    }

    /// <summary>
    /// 清理空引用并刷新所有武器实例的世界位置、旋转、镜像和排序。
    /// </summary>
    public void RefreshWeaponLayouts()
    {
        for (int i = _weaponInstances.Count - 1; i >= 0; i--)
        {
            if (_weaponInstances[i] == null)
                _weaponInstances.RemoveAt(i);
        }

        _aimDirection = GetAimDirection();
        Vector3 aimPoint = GetAimPoint();
        Vector3 layoutOrigin = GetLayoutOrigin();
        float facingSign = _aimDirection.x >= 0f ? 1f : -1f;
        Vector3 fireDirection = _aimDirection.sqrMagnitude > 0.01f ? _aimDirection.normalized : Vector3.right;

        for (int i = 0; i < _weaponInstances.Count; i++)
        {
            GameWeaponInstanceView weaponInstance = _weaponInstances[i];
            if (weaponInstance == null)
                continue;

            bool isFrontSlot = i % 2 == 0;
            int slotStackIndex = i / 2;
            Vector2 slotOffset = GetSlotOffset(isFrontSlot, slotStackIndex, facingSign);
            Vector3 position = layoutOrigin + new Vector3(slotOffset.x, slotOffset.y, 0f);
            position.z = layoutOrigin.z - 0.05f;

            Vector3 slotFireDirection = aimPoint - position;
            slotFireDirection.z = 0f;
            if (slotFireDirection.sqrMagnitude <= 0.01f)
                slotFireDirection = fireDirection;

            slotFireDirection.Normalize();
            float rotationOffset = isFrontSlot ? frontWeaponRotationOffset : backWeaponRotationOffset;
            float displayAngle = GetReadableWeaponAngle(slotFireDirection, rotationOffset, facingSign, out bool mirrored);
            int sortingOrder = isFrontSlot ? frontWeaponSortingOrder : backWeaponSortingOrder;

            weaponInstance.ApplyLayout(
                position,
                displayAngle,
                slotFireDirection,
                mirrored,
                muzzleOffset,
                weaponVisualScale,
                sortingOrder);
        }
    }

    /// <summary>
    /// 获取当前第一把武器的枪口点，武器不存在时返回玩家前方默认点。
    /// </summary>
    /// <returns>枪口世界坐标。</returns>
    public Vector3 GetPrimaryMuzzlePosition()
    {
        RefreshWeaponLayouts();
        if (_weaponInstances.Count > 0 && _weaponInstances[0] != null)
            return _weaponInstances[0].MuzzlePosition;

        return GetLayoutOrigin() + _aimDirection.normalized * (frontWeaponOffset.magnitude + muzzleOffset);
    }

    /// <summary>
    /// 获取武器布局的世界原点，优先使用预制体中配置的 WeaponRoot 位置。
    /// </summary>
    /// <returns>武器布局世界原点。</returns>
    private Vector3 GetLayoutOrigin()
    {
        return WeaponRoot.position;
    }

    /// <summary>
    /// 获取槽位偏移，前景槽优先给主武器，背景槽给第二把武器。
    /// </summary>
    /// <param name="isFrontSlot">是否前景槽。</param>
    /// <param name="slotStackIndex">同层堆叠索引。</param>
    /// <param name="facingSign">角色水平朝向。</param>
    /// <returns>槽位本地偏移。</returns>
    private Vector2 GetSlotOffset(bool isFrontSlot, int slotStackIndex, float facingSign)
    {
        Vector2 offset = isFrontSlot ? frontWeaponOffset : backWeaponOffset;
        Vector2 stackOffset = stackedWeaponOffset * slotStackIndex;
        offset += stackOffset;
        offset.x *= facingSign;
        return offset;
    }

    /// <summary>
    /// 计算可读的武器显示角度，通过上下镜像避免默认朝右的贴图倒置。
    /// </summary>
    /// <param name="direction">武器实际发射方向。</param>
    /// <param name="rotationOffset">显示角度偏移。</param>
    /// <param name="mirrored">是否上下镜像显示。</param>
    /// <returns>武器显示旋转角度。</returns>
    private float GetReadableWeaponAngle(Vector3 direction, float rotationOffset, float facingSign, out bool mirrored)
    {
        float aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float facingRotationOffset = rotationOffset * Mathf.Sign(facingSign);
        if (direction.x >= 0f)
        {
            mirrored = false;
            float rightAngle = Mathf.Clamp(Mathf.DeltaAngle(0f, aimAngle), -maxDisplayAngle, maxDisplayAngle);
            return rightAngle + facingRotationOffset;
        }

        mirrored = true;
        float leftAngle = Mathf.Clamp(Mathf.DeltaAngle(180f, aimAngle), -maxDisplayAngle, maxDisplayAngle);
        return 180f + leftAngle + facingRotationOffset;
    }

    /// <summary>
    /// 获取玩家指向鼠标的归一化方向。
    /// </summary>
    /// <returns>瞄准方向。</returns>
    private Vector3 GetAimDirection()
    {
        Vector3 aimPoint = GetAimPoint();
        Vector3 direction = aimPoint - transform.position;
        direction.z = 0f;
        return direction.sqrMagnitude > 0.01f ? direction.normalized : _aimDirection;
    }

    /// <summary>
    /// 获取当前鼠标对应的世界坐标，摄像机缺失时返回玩家前方默认点。
    /// </summary>
    /// <returns>鼠标世界坐标。</returns>
    private Vector3 GetAimPoint()
    {
        if (_camera == null || !_camera.isActiveAndEnabled)
            _camera = Camera.main;

        if (_camera == null)
            return transform.position + (_aimDirection.sqrMagnitude > 0.01f ? _aimDirection.normalized : Vector3.right);

        Vector3 mouseWorld = _camera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = transform.position.z;
        return mouseWorld;
    }
}
