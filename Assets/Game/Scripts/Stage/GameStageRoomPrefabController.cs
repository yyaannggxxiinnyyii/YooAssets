using System;
using UnityEngine;

/// <summary>
/// 按 Stage 房间类型加载和卸载房间 Prefab，并暴露当前房间的地图、出生点和门根引用。
/// </summary>
public sealed class GameStageRoomPrefabController : MonoBehaviour
{
    [Header("房间实例")]
    [Tooltip("房间 Prefab 实例生成的父节点。为空时使用当前对象。")]
    [SerializeField] private Transform roomInstanceRoot;
    [Tooltip("不同房间类型对应的房间 Prefab 配置。")]
    [SerializeField] private StageRoomPrefabEntry[] roomPrefabs = new StageRoomPrefabEntry[0];
    [Tooltip("精英房或 Boss 房未配置专用 Prefab 时，是否允许回退使用普通战斗房 Prefab。")]
    [SerializeField] private bool fallbackCombatRoomToNormal = true;

    private GameStageRoomView _activeRoom;
    private RoomType _activeRoomType;

    /// <summary>
    /// 当前激活的房间视图。
    /// </summary>
    public GameStageRoomView ActiveRoom => _activeRoom;

    /// <summary>
    /// 当前房间地图边界控制器。
    /// </summary>
    public GameArenaController ActiveArenaController => _activeRoom != null ? _activeRoom.ArenaController : null;

    /// <summary>
    /// 当前房间玩家出生点。
    /// </summary>
    public Transform ActivePlayerSpawnPoint => _activeRoom != null ? _activeRoom.PlayerSpawnPoint : null;

    /// <summary>
    /// 当前房间门生成根节点。
    /// </summary>
    public Transform ActiveDoorRoot => _activeRoom != null ? _activeRoom.DoorRoot : null;

    /// <summary>
    /// 当前房间相机边界碰撞器。
    /// </summary>
    public PolygonCollider2D ActiveCameraBoundsCollider => _activeRoom != null ? _activeRoom.CameraBoundsCollider : null;

    /// <summary>
    /// 当前房间商店商品摆放点。
    /// </summary>
    public StageShopOfferPoint[] ActiveShopOfferPoints => _activeRoom != null ? _activeRoom.ShopOfferPoints : null;

    /// <summary>
    /// 当前房间自动商品排布控制器。
    /// </summary>
    public StageShopOfferLayoutController ActiveShopOfferLayoutController => _activeRoom != null ? _activeRoom.ShopOfferLayoutController : null;

    /// <summary>
    /// 当前房间功能对象生成器。
    /// </summary>
    public StageRoomFunctionObjectSpawner ActiveFunctionObjectSpawner => _activeRoom != null ? _activeRoom.FunctionObjectSpawner : null;

    /// <summary>
    /// 当前房间战斗场地道具生成器。
    /// </summary>
    public StageCombatFieldPropSpawner ActiveCombatFieldPropSpawner => _activeRoom != null ? _activeRoom.CombatFieldPropSpawner : null;

    /// <summary>
    /// 当前是否有激活的房间实例。
    /// </summary>
    public bool HasActiveRoom => _activeRoom != null;

    private void Awake()
    {
        if (roomInstanceRoot == null)
            roomInstanceRoot = transform;
    }

    /// <summary>
    /// 尝试加载指定房间类型对应的 Prefab，加载成功会替换当前激活房间。
    /// </summary>
    /// <param name="roomType">目标房间类型。</param>
    /// <returns>成功加载房间 Prefab 时返回 true。</returns>
    public bool TryLoadRoom(RoomType roomType)
    {
        GameStageRoomView roomPrefab = FindRoomPrefab(roomType);
        if (roomPrefab == null)
            return false;

        ClearActiveRoom();
        Transform parent = roomInstanceRoot != null ? roomInstanceRoot : transform;
        _activeRoom = Instantiate(roomPrefab, parent);
        _activeRoom.transform.localPosition = Vector3.zero;
        _activeRoom.transform.localRotation = Quaternion.identity;
        _activeRoom.transform.localScale = Vector3.one;
        _activeRoom.RefreshRoom();
        _activeRoomType = roomType;
        return true;
    }

    /// <summary>
    /// 判断指定房间类型是否配置了可用 Prefab。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>存在可加载 Prefab 时返回 true。</returns>
    public bool HasRoomPrefab(RoomType roomType)
    {
        return FindRoomPrefab(roomType) != null;
    }

    /// <summary>
    /// 清理当前激活的房间实例。
    /// </summary>
    public void ClearActiveRoom()
    {
        if (_activeRoom != null)
            Destroy(_activeRoom.gameObject);

        _activeRoom = null;
    }

    /// <summary>
    /// 查找指定房间类型对应的 Prefab，战斗房可按配置回退到普通战斗房。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>房间 Prefab；未配置时返回 null。</returns>
    private GameStageRoomView FindRoomPrefab(RoomType roomType)
    {
        GameStageRoomView exactPrefab = FindExactRoomPrefab(roomType);
        if (exactPrefab != null)
            return exactPrefab;

        if (!fallbackCombatRoomToNormal)
            return null;

        if (roomType == RoomType.CombatElite || roomType == RoomType.Boss)
            return FindExactRoomPrefab(RoomType.CombatNormal);

        return null;
    }

    /// <summary>
    /// 查找完全匹配指定房间类型的 Prefab。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>完全匹配的房间 Prefab；不存在时返回 null。</returns>
    private GameStageRoomView FindExactRoomPrefab(RoomType roomType)
    {
        if (roomPrefabs == null)
            return null;

        for (int i = 0; i < roomPrefabs.Length; i++)
        {
            StageRoomPrefabEntry entry = roomPrefabs[i];
            if (entry != null && entry.RoomType == roomType && entry.RoomPrefab != null)
                return entry.RoomPrefab;
        }

        return null;
    }
}

/// <summary>
/// 定义一个房间类型和对应房间 Prefab 的配置项。
/// </summary>
[Serializable]
public sealed class StageRoomPrefabEntry
{
    [Header("房间 Prefab")]
    [Tooltip("该配置项对应的房间类型。")]
    [SerializeField] private RoomType roomType = RoomType.CombatNormal;
    [Tooltip("该房间类型进入时实例化的房间 Prefab，根节点需要挂 GameStageRoomView。")]
    [SerializeField] private GameStageRoomView roomPrefab;

    /// <summary>
    /// 房间类型。
    /// </summary>
    public RoomType RoomType => roomType;

    /// <summary>
    /// 房间 Prefab。
    /// </summary>
    public GameStageRoomView RoomPrefab => roomPrefab;
}
