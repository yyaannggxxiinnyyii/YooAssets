using UnityEngine;

/// <summary>
/// 标记一个 Stage 房间 Prefab 的关键运行时引用，包括地图边界、玩家出生点和门生成根节点。
/// </summary>
public sealed class GameStageRoomView : MonoBehaviour
{
    [Header("房间信息")]
    [Tooltip("该房间 Prefab 对应的房间类型。")]
    [SerializeField] private RoomType roomType = RoomType.CombatNormal;

    [Header("房间引用")]
    [Tooltip("该房间使用的地图边界控制器，用于刷新玩家移动范围和相机边界。")]
    [SerializeField] private GameArenaController arenaController;
    [Tooltip("玩家进入该房间时复位到的位置。为空时使用房间根节点。")]
    [SerializeField] private Transform playerSpawnPoint;
    [Tooltip("战斗结束后场景门生成的父节点。为空时使用房间根节点。")]
    [SerializeField] private Transform doorRoot;
    [Tooltip("该房间使用的相机边界 PolygonCollider2D。为空时相机继续使用地图矩形边界。")]
    [SerializeField] private PolygonCollider2D cameraBoundsCollider;
    [Tooltip("商店房使用的场景商品摆放点列表；非商店房可留空。")]
    [SerializeField] private StageShopOfferPoint[] shopOfferPoints = new StageShopOfferPoint[0];
    [Tooltip("商店房使用的自动商品排布控制器；配置后会按当前商品数量生成商品槽位。")]
    [SerializeField] private StageShopOfferLayoutController shopOfferLayoutController;
    [Tooltip("功能房使用的功能对象生成器；用于在同一个房间内生成宝箱、回血机或属性装置。")]
    [SerializeField] private StageRoomFunctionObjectSpawner functionObjectSpawner;
    [Tooltip("战斗房使用的场地道具生成器；用于生成浆果丛等中立场景物。")]
    [SerializeField] private StageCombatFieldPropSpawner combatFieldPropSpawner;

    /// <summary>
    /// 房间类型。
    /// </summary>
    public RoomType RoomType => roomType;

    /// <summary>
    /// 该房间使用的地图边界控制器。
    /// </summary>
    public GameArenaController ArenaController => arenaController;

    /// <summary>
    /// 玩家进入该房间时使用的出生点。
    /// </summary>
    public Transform PlayerSpawnPoint => playerSpawnPoint != null ? playerSpawnPoint : transform;

    /// <summary>
    /// 场景门生成根节点。
    /// </summary>
    public Transform DoorRoot => doorRoot != null ? doorRoot : transform;

    /// <summary>
    /// 该房间使用的相机边界碰撞器。
    /// </summary>
    public PolygonCollider2D CameraBoundsCollider => cameraBoundsCollider;

    /// <summary>
    /// 商店房场景商品摆放点列表。
    /// </summary>
    public StageShopOfferPoint[] ShopOfferPoints => shopOfferPoints;

    /// <summary>
    /// 商店房自动商品排布控制器。
    /// </summary>
    public StageShopOfferLayoutController ShopOfferLayoutController => shopOfferLayoutController;

    /// <summary>
    /// 功能房对象生成器。
    /// </summary>
    public StageRoomFunctionObjectSpawner FunctionObjectSpawner => functionObjectSpawner;

    /// <summary>
    /// 战斗房场地道具生成器。
    /// </summary>
    public StageCombatFieldPropSpawner CombatFieldPropSpawner => combatFieldPropSpawner;

    private void Awake()
    {
        ResolveMissingReferences();
    }

    private void OnValidate()
    {
        ResolveMissingReferences();
    }

    /// <summary>
    /// 刷新房间内部引用和地图边界显示。
    /// </summary>
    public void RefreshRoom()
    {
        ResolveMissingReferences();
        if (arenaController != null)
            arenaController.RefreshArena();
    }

    /// <summary>
    /// 补齐未手动拖拽的常用引用，只查找已有子节点，不创建运行时内容。
    /// </summary>
    private void ResolveMissingReferences()
    {
        if (arenaController == null)
            arenaController = GetComponentInChildren<GameArenaController>(true);

        if (playerSpawnPoint == null)
            playerSpawnPoint = FindChildByName("PlayerSpawnPoint");

        if (doorRoot == null)
            doorRoot = FindChildByName("DoorRoot");

        if (cameraBoundsCollider == null)
            cameraBoundsCollider = FindCameraBoundsCollider();

        if (shopOfferPoints == null || shopOfferPoints.Length <= 0)
            shopOfferPoints = GetComponentsInChildren<StageShopOfferPoint>(true);

        if (shopOfferLayoutController == null)
            shopOfferLayoutController = GetComponentInChildren<StageShopOfferLayoutController>(true);

        if (functionObjectSpawner == null)
            functionObjectSpawner = GetComponentInChildren<StageRoomFunctionObjectSpawner>(true);

        if (combatFieldPropSpawner == null)
            combatFieldPropSpawner = GetComponentInChildren<StageCombatFieldPropSpawner>(true);
    }

    /// <summary>
    /// 在当前房间子节点中按名称查找 Transform。
    /// </summary>
    /// <param name="childName">子节点名称。</param>
    /// <returns>找到的子节点；不存在时返回 null。</returns>
    private Transform FindChildByName(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    /// <summary>
    /// 查找房间内约定命名的相机边界碰撞器，避免没有手动拖拽时边界引用缺失。
    /// </summary>
    /// <returns>找到的相机边界碰撞器；不存在时返回 null。</returns>
    private PolygonCollider2D FindCameraBoundsCollider()
    {
        Transform cameraBounds = FindChildByName("CameraBounds");
        if (cameraBounds == null)
            cameraBounds = FindChildByName("相机边界");

        if (cameraBounds != null && cameraBounds.TryGetComponent(out PolygonCollider2D namedCollider))
            return namedCollider;

        PolygonCollider2D[] colliders = GetComponentsInChildren<PolygonCollider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            PolygonCollider2D collider = colliders[i];
            if (collider != null && collider.transform != transform)
                return collider;
        }

        return null;
    }
}
