using UnityEngine;

/// <summary>
/// 保存从主界面进入单局时选择的角色和武器启动数据。
/// </summary>
public static class GameRunStartContext
{
    private static CharacterData _characterData;
    private static WeaponData _weaponData;
    private static bool _hasRunStartData;

    /// <summary>
    /// 是否已经配置本局启动数据。
    /// </summary>
    public static bool HasRunStartData => _hasRunStartData;

    /// <summary>
    /// 本局选择的角色数据。
    /// </summary>
    public static CharacterData CharacterData => _characterData;

    /// <summary>
    /// 本局选择的武器数据。
    /// </summary>
    public static WeaponData WeaponData => _weaponData;

    /// <summary>
    /// 写入本局启动数据，缺少关键配置时会拒绝写入并清空旧数据。
    /// </summary>
    /// <param name="characterData">本局选择的角色数据。</param>
    /// <param name="weaponData">本局选择的武器数据。</param>
    public static void Configure(CharacterData characterData, WeaponData weaponData)
    {
        if (characterData == null)
        {
            Debug.LogError("[RunStart] 缺少角色数据，无法进入游戏场景。");
            Clear();
            return;
        }

        if (weaponData == null)
        {
            Debug.LogError("[RunStart] 缺少武器数据，无法进入游戏场景。");
            Clear();
            return;
        }

        _characterData = characterData;
        _weaponData = weaponData;
        _hasRunStartData = true;
    }

    /// <summary>
    /// 尝试读取完整的本局启动数据。
    /// </summary>
    /// <param name="characterData">本局选择的角色数据。</param>
    /// <param name="weaponData">本局选择的武器数据。</param>
    /// <returns>启动数据是否完整可用。</returns>
    public static bool TryGetSelection(out CharacterData characterData, out WeaponData weaponData)
    {
        characterData = _characterData;
        weaponData = _weaponData;
        return _hasRunStartData && characterData != null && weaponData != null;
    }

    /// <summary>
    /// 清空本局启动数据，用于返回主界面或启动配置失效时重置。
    /// </summary>
    public static void Clear()
    {
        _characterData = null;
        _weaponData = null;
        _hasRunStartData = false;
    }
}
