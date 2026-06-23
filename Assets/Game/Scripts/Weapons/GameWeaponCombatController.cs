using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理玩家武器实例的创建、数量变化、布局刷新和枪口查询。
/// </summary>
public sealed class GameWeaponCombatController : MonoBehaviour
{
    [Header("武器预制体")]
    [Tooltip("运行时生成武器时使用的兜底武器预制体。")]
    [SerializeField] private GameWeaponInstanceView fallbackWeaponPrefab;

    private readonly List<GameWeaponInstanceView> _weaponInstances = new List<GameWeaponInstanceView>();

    private GamePlayerController _player;
    private GamePlayerWeaponLayoutController _weaponLayoutController;
    private WeaponData _currentWeaponData;
    private bool _weaponVisualsVisible = true;

    /// <summary>
    /// 使用当前玩家和武器数据初始化武器运行时实例。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="weaponData">当前武器数据。</param>
    public void Initialize(GamePlayerController player, WeaponData weaponData)
    {
        _player = player;
        _currentWeaponData = weaponData;
        EnsureWeaponLayoutController();
        SetWeaponInstanceCount(1);
    }

    /// <summary>
    /// 重置武器运行时引用和实例列表。
    /// </summary>
    public void ResetWeaponRuntime()
    {
        ClearWeaponInstances();
        _player = null;
        _weaponLayoutController = null;
        _currentWeaponData = null;
        _weaponVisualsVisible = true;
    }

    /// <summary>
    /// 清理当前创建的所有武器实例。
    /// </summary>
    public void ClearWeaponInstances()
    {
        for (int i = _weaponInstances.Count - 1; i >= 0; i--)
        {
            GameWeaponInstanceView weaponInstance = _weaponInstances[i];
            if (weaponInstance == null)
                continue;

            if (_weaponLayoutController != null)
                _weaponLayoutController.UnregisterWeapon(weaponInstance);

            Destroy(weaponInstance.gameObject);
        }

        _weaponInstances.Clear();
    }

    /// <summary>
    /// 增加当前武器实例数量，用于双持或后续道具扩展。
    /// </summary>
    /// <param name="value">增加数量。</param>
    public void AddWeaponInstanceCount(int value)
    {
        if (value <= 0)
            return;

        SetWeaponInstanceCount(GetCurrentWeaponInstanceCount() + value);
    }

    /// <summary>
    /// 按倍率调整当前武器实例数量，用于后续稀有道具扩展。
    /// </summary>
    /// <param name="multiplier">实例数量倍率。</param>
    public void MultiplyWeaponInstanceCount(float multiplier)
    {
        if (multiplier <= 0f)
            return;

        SetWeaponInstanceCount(Mathf.Max(1, Mathf.RoundToInt(GetCurrentWeaponInstanceCount() * multiplier)));
    }

    /// <summary>
    /// 设置当前武器实例数量并刷新实例布局。
    /// </summary>
    /// <param name="count">目标实例数量。</param>
    public void SetWeaponInstanceCount(int count)
    {
        if (_player == null)
            return;

        EnsureWeaponLayoutController();
        int targetCount = Mathf.Max(1, count);
        while (_weaponInstances.Count < targetCount)
            CreateWeaponInstance();

        for (int i = _weaponInstances.Count - 1; i >= targetCount; i--)
        {
            if (_weaponLayoutController != null)
                _weaponLayoutController.UnregisterWeapon(_weaponInstances[i]);

            if (_weaponInstances[i] != null)
                Destroy(_weaponInstances[i].gameObject);

            _weaponInstances.RemoveAt(i);
        }

        RefreshWeaponInstanceLayouts();
    }

    /// <summary>
    /// 确保至少存在一把当前武器实例。
    /// </summary>
    public void EnsureWeaponInstances()
    {
        if (_player == null)
            return;

        for (int i = _weaponInstances.Count - 1; i >= 0; i--)
        {
            if (_weaponInstances[i] == null)
                _weaponInstances.RemoveAt(i);
        }

        if (_weaponInstances.Count <= 0)
            SetWeaponInstanceCount(1);
        else
            RefreshWeaponInstanceLayouts();
    }

    /// <summary>
    /// 遍历当前有效武器实例。
    /// </summary>
    /// <param name="handler">武器实例处理回调。</param>
    public void ForEachWeaponInstance(Action<GameWeaponInstanceView> handler)
    {
        if (handler == null)
            return;

        EnsureWeaponInstances();
        if (_weaponLayoutController != null)
            _weaponLayoutController.RefreshWeaponLayouts();

        for (int i = 0; i < _weaponInstances.Count; i++)
        {
            GameWeaponInstanceView weaponInstance = _weaponInstances[i];
            if (weaponInstance == null)
                continue;

            handler.Invoke(weaponInstance);
        }
    }

    /// <summary>
    /// 切换所有运行时武器实例的图形显示，保留武器实例和枪口逻辑。
    /// </summary>
    /// <param name="visible">是否显示武器图形。</param>
    public void SetWeaponVisualsVisible(bool visible)
    {
        _weaponVisualsVisible = visible;
        for (int i = _weaponInstances.Count - 1; i >= 0; i--)
        {
            GameWeaponInstanceView weaponInstance = _weaponInstances[i];
            if (weaponInstance == null)
            {
                _weaponInstances.RemoveAt(i);
                continue;
            }

            weaponInstance.SetVisualVisible(visible);
        }
    }

    /// <summary>
    /// 获取当前武器实例数量。
    /// </summary>
    /// <returns>当前武器实例数量。</returns>
    public int GetCurrentWeaponInstanceCount()
    {
        return Mathf.Max(1, _weaponInstances.Count);
    }

    /// <summary>
    /// 获取旧发射入口使用的默认枪口位置。
    /// </summary>
    /// <param name="direction">发射方向。</param>
    /// <returns>枪口位置。</returns>
    public Vector3 GetDefaultMuzzlePosition(Vector3 direction)
    {
        EnsureWeaponInstances();
        if (_weaponInstances.Count > 0 && _weaponInstances[0] != null)
        {
            if (_weaponLayoutController != null)
                _weaponLayoutController.RefreshWeaponLayouts();

            return _weaponInstances[0].MuzzlePosition;
        }

        if (_weaponLayoutController != null)
            return _weaponLayoutController.GetPrimaryMuzzlePosition();

        return _player != null ? _player.transform.position + direction.normalized : direction.normalized;
    }

    /// <summary>
    /// 创建一个当前武器实例视图。
    /// </summary>
    private void CreateWeaponInstance()
    {
        if (_player == null)
            return;

        GameWeaponInstanceView weaponPrefab = _currentWeaponData != null && _currentWeaponData.WeaponPrefab != null
            ? _currentWeaponData.WeaponPrefab
            : fallbackWeaponPrefab;
        GameWeaponInstanceView weaponInstance;
        Transform weaponParent = _weaponLayoutController != null ? _weaponLayoutController.WeaponRoot : _player.transform;
        if (weaponPrefab != null)
        {
            weaponInstance = Instantiate(weaponPrefab, weaponParent);
        }
        else
        {
            GameObject weaponObject = new GameObject("WeaponInstance");
            weaponObject.transform.SetParent(weaponParent);
            weaponInstance = weaponObject.AddComponent<GameWeaponInstanceView>();
        }

        weaponInstance.transform.localPosition = Vector3.zero;
        weaponInstance.transform.localRotation = Quaternion.identity;
        weaponInstance.transform.localScale = Vector3.one;
        weaponInstance.Initialize(_currentWeaponData);
        weaponInstance.SetVisualVisible(_weaponVisualsVisible);
        _weaponInstances.Add(weaponInstance);
        if (_weaponLayoutController != null)
            _weaponLayoutController.RegisterWeapon(weaponInstance);
    }

    /// <summary>
    /// 刷新所有武器实例的队列索引和武器图标。
    /// </summary>
    private void RefreshWeaponInstanceLayouts()
    {
        for (int i = 0; i < _weaponInstances.Count; i++)
        {
            GameWeaponInstanceView weaponInstance = _weaponInstances[i];
            if (weaponInstance == null)
                continue;

            weaponInstance.SetWeaponData(_currentWeaponData);
            weaponInstance.SetVisualVisible(_weaponVisualsVisible);
        }

        if (_weaponLayoutController != null)
            _weaponLayoutController.RefreshWeaponLayouts();
    }

    /// <summary>
    /// 确保玩家身上存在武器布局控制器。
    /// </summary>
    private void EnsureWeaponLayoutController()
    {
        if (_player == null)
            return;

        _weaponLayoutController = _player.GetComponent<GamePlayerWeaponLayoutController>();
        if (_weaponLayoutController == null)
            _weaponLayoutController = _player.gameObject.AddComponent<GamePlayerWeaponLayoutController>();
    }
}
