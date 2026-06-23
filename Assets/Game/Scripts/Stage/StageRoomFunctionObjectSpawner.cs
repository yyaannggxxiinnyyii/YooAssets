using System;
using UnityEngine;

/// <summary>
/// 在通用功能房中按房间类型和权重生成一个功能对象 Prefab。
/// </summary>
public sealed class StageRoomFunctionObjectSpawner : MonoBehaviour
{
    [Header("生成点")]
    [Tooltip("功能对象实例的父节点；为空时使用当前对象。")]
    [SerializeField] private Transform functionRoot;
    [Tooltip("功能对象生成位置；为空时使用当前对象位置。")]
    [SerializeField] private Transform spawnPoint;

    [Header("功能对象池")]
    [Tooltip("可在功能房中随机生成的功能对象 Prefab 配置。")]
    [SerializeField] private StageRoomFunctionObjectEntry[] functionEntries = new StageRoomFunctionObjectEntry[0];
    [Tooltip("生成新功能对象前是否清理旧对象。")]
    [SerializeField] private bool clearExistingBeforeSpawn = true;

    private GameObject _activeFunctionObject;

    private void Awake()
    {
        if (functionRoot == null)
            functionRoot = transform;
    }

    /// <summary>
    /// 根据当前中转房类型生成一个功能对象。
    /// </summary>
    /// <param name="roomType">当前中转房类型。</param>
    /// <param name="context">功能房运行时上下文。</param>
    /// <returns>成功生成并初始化功能对象时返回 true。</returns>
    public bool SpawnFunctionObject(RoomType roomType, StageRoomFunctionContext context)
    {
        if (clearExistingBeforeSpawn)
            ClearActiveFunctionObject();

        StageRoomFunctionObjectEntry selectedEntry = SelectEntry(roomType);
        if (selectedEntry == null || selectedEntry.FunctionPrefab == null)
            return false;

        Transform root = functionRoot != null ? functionRoot : transform;
        _activeFunctionObject = Instantiate(selectedEntry.FunctionPrefab, root);
        Transform targetSpawnPoint = spawnPoint != null ? spawnPoint : transform;
        _activeFunctionObject.transform.position = targetSpawnPoint.position;
        _activeFunctionObject.transform.rotation = targetSpawnPoint.rotation;
        _activeFunctionObject.transform.localScale = selectedEntry.FunctionPrefab.transform.localScale;
        _activeFunctionObject.name = selectedEntry.DisplayName;
        InitializeFunctionObjects(_activeFunctionObject, context);
        return true;
    }

    /// <summary>
    /// 清理当前生成的功能对象实例。
    /// </summary>
    public void ClearActiveFunctionObject()
    {
        if (_activeFunctionObject != null)
            Destroy(_activeFunctionObject);

        _activeFunctionObject = null;
    }

    /// <summary>
    /// 在生成出的 Prefab 内初始化所有功能对象脚本。
    /// </summary>
    /// <param name="functionObject">功能对象实例。</param>
    /// <param name="context">功能房运行时上下文。</param>
    private void InitializeFunctionObjects(GameObject functionObject, StageRoomFunctionContext context)
    {
        if (functionObject == null)
            return;

        MonoBehaviour[] behaviours = functionObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IStageRoomFunctionObject stageFunctionObject)
                stageFunctionObject.Initialize(context);
        }
    }

    /// <summary>
    /// 从功能对象池中按权重选择一个适用于当前房间类型的条目。
    /// </summary>
    /// <param name="roomType">当前中转房类型。</param>
    /// <returns>被选中的功能对象条目。</returns>
    private StageRoomFunctionObjectEntry SelectEntry(RoomType roomType)
    {
        if (functionEntries == null || functionEntries.Length <= 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < functionEntries.Length; i++)
        {
            StageRoomFunctionObjectEntry entry = functionEntries[i];
            if (entry != null && entry.CanSpawnInRoom(roomType) && entry.FunctionPrefab != null)
                totalWeight += entry.Weight;
        }

        if (totalWeight <= 0f)
            return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < functionEntries.Length; i++)
        {
            StageRoomFunctionObjectEntry entry = functionEntries[i];
            if (entry == null || !entry.CanSpawnInRoom(roomType) || entry.FunctionPrefab == null)
                continue;

            roll -= entry.Weight;
            if (roll <= 0f)
                return entry;
        }

        return null;
    }
}

/// <summary>
/// 定义一个功能房功能对象 Prefab 的生成条件和权重。
/// </summary>
[Serializable]
public sealed class StageRoomFunctionObjectEntry
{
    [Header("功能对象")]
    [Tooltip("该功能对象的显示名称，也会用于运行时实例命名。")]
    [SerializeField] private string displayName = "功能对象";
    [Tooltip("要生成的功能对象 Prefab，根节点或子节点需要挂实现 IStageRoomFunctionObject 的脚本。")]
    [SerializeField] private GameObject functionPrefab;
    [Tooltip("该功能对象允许出现的中转房类型；为空时默认允许 Event 和 Treasure。")]
    [SerializeField] private RoomType[] allowedRoomTypes = { RoomType.Event, RoomType.Treasure };
    [Tooltip("该功能对象在候选池中的随机权重，小于 0 时按 0 处理。")]
    [SerializeField] private float weight = 1f;

    /// <summary>
    /// 显示名称。
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "功能对象" : displayName;

    /// <summary>
    /// 功能对象 Prefab。
    /// </summary>
    public GameObject FunctionPrefab => functionPrefab;

    /// <summary>
    /// 随机权重。
    /// </summary>
    public float Weight => Mathf.Max(0f, weight);

    /// <summary>
    /// 判断该功能对象是否允许在指定房间类型中生成。
    /// </summary>
    /// <param name="roomType">当前中转房类型。</param>
    /// <returns>允许生成时返回 true。</returns>
    public bool CanSpawnInRoom(RoomType roomType)
    {
        if (roomType != RoomType.Event && roomType != RoomType.Treasure)
            return false;

        if (allowedRoomTypes == null || allowedRoomTypes.Length <= 0)
            return true;

        for (int i = 0; i < allowedRoomTypes.Length; i++)
        {
            if (allowedRoomTypes[i] == roomType)
                return true;
        }

        return false;
    }
}
