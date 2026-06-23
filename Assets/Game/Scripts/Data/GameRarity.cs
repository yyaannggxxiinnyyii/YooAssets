using UnityEngine;

/// <summary>
/// 定义游戏内可复用的物品评级，用于道具、遗物和后续掉落展示。
/// </summary>
public enum GameRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// 提供游戏评级的默认显示名称和颜色。
/// </summary>
public static class GameRarityUtility
{
    /// <summary>
    /// 获取评级的默认中文显示名称。
    /// </summary>
    /// <param name="rarity">评级。</param>
    /// <returns>评级显示名称。</returns>
    public static string GetDisplayName(GameRarity rarity)
    {
        switch (rarity)
        {
            case GameRarity.Uncommon:
                return "稀有";

            case GameRarity.Rare:
                return "罕见";

            case GameRarity.Epic:
                return "史诗";

            case GameRarity.Legendary:
                return "传说";

            case GameRarity.Common:
            default:
                return "普通";
        }
    }

    /// <summary>
    /// 获取评级的默认显示颜色。
    /// </summary>
    /// <param name="rarity">评级。</param>
    /// <returns>评级颜色。</returns>
    public static Color GetColor(GameRarity rarity)
    {
        switch (rarity)
        {
            case GameRarity.Uncommon:
                return new Color(0.18f, 0.45f, 0.22f, 0.95f);

            case GameRarity.Rare:
                return new Color(0.16f, 0.32f, 0.62f, 0.95f);

            case GameRarity.Epic:
                return new Color(0.43f, 0.22f, 0.62f, 0.95f);

            case GameRarity.Legendary:
                return new Color(0.88f, 0.58f, 0.12f, 0.95f);

            case GameRarity.Common:
            default:
                return new Color(0.78f, 0.78f, 0.78f, 0.95f);
        }
    }
}
