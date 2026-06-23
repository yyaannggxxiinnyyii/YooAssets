using UnityEngine;

/// <summary>
/// 定义一个可复用的 Stage 房间门选项资产，供不同门规则按权重引用。
/// </summary>
[CreateAssetMenu(fileName = "StageRoomOptionData", menuName = "Game/Data/Stage Room Option Data")]
public sealed class StageRoomOptionData : ScriptableObject
{
    [Header("房间")]
    [Tooltip("玩家选择该门后进入的房间类型。")]
    [SerializeField] private RoomType roomType = RoomType.CombatNormal;
    [Tooltip("门选项在 Tooltip 或调试信息中显示的名称。")]
    [SerializeField] private string displayName = "普通房";
    [Tooltip("玩家靠近门选项时显示的说明文本。")]
    [SerializeField] private string tooltipDescription = "进入普通战斗房。";
    [Tooltip("门外观配置 ID，由场景门表现层映射到具体 Sprite、颜色或动画。")]
    [SerializeField] private string doorVisualId = "door_normal";
    [Tooltip("当前房间为中转房时，离开后要进入的目标战斗房类型。")]
    [SerializeField] private RoomType transitExitTargetCombatRoomType = RoomType.CombatNormal;

    /// <summary>
    /// 目标房间类型。
    /// </summary>
    public RoomType RoomType => roomType;

    /// <summary>
    /// 门选项显示名称。
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// 门选项 Tooltip 描述。
    /// </summary>
    public string TooltipDescription => tooltipDescription;

    /// <summary>
    /// 门外观 ID。
    /// </summary>
    public string DoorVisualId => doorVisualId;

    /// <summary>
    /// 中转房离开后的目标战斗房类型。
    /// </summary>
    public RoomType TransitExitTargetCombatRoomType => transitExitTargetCombatRoomType;

    /// <summary>
    /// 将资产配置转换为运行时门选项数据。
    /// </summary>
    /// <returns>运行时门选项数据。</returns>
    public RoomOptionData CreateRuntimeOption()
    {
        return new RoomOptionData(roomType, displayName, tooltipDescription, doorVisualId, transitExitTargetCombatRoomType);
    }
}
