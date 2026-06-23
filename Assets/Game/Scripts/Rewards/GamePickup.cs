using UnityEngine;

/// <summary>
/// 控制经验和金币掉落物延迟自动飞向玩家并结算奖励。
/// </summary>
public sealed class GamePickup : MonoBehaviour
{
    private const float DirectCollectDistance = 0.25f;

    /// <summary>
    /// 掉落物类型。
    /// </summary>
    public enum PickupType
    {
        Experience,
        Gold
    }

    [Header("掉落属性")]
    [Tooltip("掉落物类型，用于决定结算为经验或金币。")]
    [SerializeField] private PickupType pickupType;
    [Tooltip("掉落物包含的奖励数值。")]
    [SerializeField] private int value = 1;
    [Tooltip("自动飞向玩家时的移动速度。")]
    [SerializeField] private float magnetSpeed = 7f;
    [Tooltip("掉落后等待自动吸附的时间，期间玩家贴近仍可直接拾取。")]
    [SerializeField] private float autoCollectDelay = 0.5f;

    private GamePlayerController _target;
    private GamePooledObject _pooledObject;
    private float _timer;

    private void Awake()
    {
        _pooledObject = GetComponent<GamePooledObject>();
    }

    private void Update()
    {
        if (_target == null)
            return;

        float distance = Vector3.Distance(transform.position, _target.transform.position);
        if (distance <= DirectCollectDistance)
        {
            Collect();
            return;
        }

        _timer += Time.deltaTime;
        if (_timer < autoCollectDelay)
            return;

        Vector3 direction = (_target.transform.position - transform.position).normalized;
        transform.position += direction * magnetSpeed * Time.deltaTime;
    }

    /// <summary>
    /// 初始化掉落物数据。
    /// </summary>
    /// <param name="target">可拾取该物体的玩家。</param>
    /// <param name="type">掉落物类型。</param>
    /// <param name="amount">掉落物数值。</param>
    public void Initialize(GamePlayerController target, PickupType type, int amount)
    {
        _target = target;
        pickupType = type;
        value = Mathf.Max(1, amount);
        _timer = 0f;
    }

    /// <summary>
    /// 将掉落物奖励发给玩家并回收到对象池。
    /// </summary>
    private void Collect()
    {
        if (pickupType == PickupType.Experience)
            _target.AddExperience(value);
        else
            _target.AddGold(value);

        if (_pooledObject != null)
        {
            _pooledObject.Release();
            return;
        }

        Destroy(gameObject);
    }
}

