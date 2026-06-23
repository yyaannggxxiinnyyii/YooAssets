using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 控制玩家闪避时的残影快照生成、淡出和复用。
/// </summary>
public sealed class GamePlayerAfterimageController : MonoBehaviour
{
    private sealed class AfterimageSnapshot
    {
        public readonly GameObject Root;
        public readonly List<SpriteRenderer> Renderers = new List<SpriteRenderer>();
        public float Timer;
        public float LifeTime;

        public AfterimageSnapshot(GameObject root)
        {
            Root = root;
        }
    }

    [Header("渲染来源")]
    [Tooltip("需要复制为残影的玩家 SpriteRenderer。为空时会自动收集玩家子节点中的 SpriteRenderer。")]
    [SerializeField] private SpriteRenderer[] sourceRenderers;

    [Header("残影参数")]
    [Tooltip("残影生成间隔。")]
    [SerializeField] private float spawnInterval = 0.035f;
    [Tooltip("单个残影淡出持续时间。")]
    [SerializeField] private float imageLifeTime = 0.18f;
    [Tooltip("残影初始颜色。")]
    [SerializeField] private Color startColor = new Color(0.55f, 0.9f, 1f, 0.42f);
    [Tooltip("残影相对玩家原图层的排序偏移。")]
    [SerializeField] private int sortingOrderOffset = -1;
    [Tooltip("预创建的残影对象数量。")]
    [SerializeField] private int preloadCount = 6;

    private readonly List<SpriteRenderer> _runtimeSourceRenderers = new List<SpriteRenderer>();
    private readonly List<AfterimageSnapshot> _inactiveSnapshots = new List<AfterimageSnapshot>();
    private readonly List<AfterimageSnapshot> _activeSnapshots = new List<AfterimageSnapshot>();

    private Transform _poolRoot;
    private float _remainingDuration;
    private float _spawnTimer;
    private bool _isPlaying;

    private void Awake()
    {
        CacheSourceRenderers();
        EnsurePoolRoot();
        PreloadSnapshots();
    }

    private void Update()
    {
        TickSpawn(Time.deltaTime);
        TickActiveSnapshots(Time.deltaTime);
    }

    private void OnDisable()
    {
        _isPlaying = false;
        _remainingDuration = 0f;
        ReleaseAllActiveSnapshots();
    }

    private void OnDestroy()
    {
        if (_poolRoot != null)
            Destroy(_poolRoot.gameObject);
    }

    private void OnValidate()
    {
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        imageLifeTime = Mathf.Max(0.01f, imageLifeTime);
        preloadCount = Mathf.Max(0, preloadCount);
    }

    /// <summary>
    /// 播放一次指定持续时间的闪避残影。
    /// </summary>
    /// <param name="duration">残影持续生成时间。</param>
    public void PlayAfterimage(float duration)
    {
        if (!isActiveAndEnabled || duration <= 0f)
            return;

        CacheSourceRenderers();
        if (_runtimeSourceRenderers.Count <= 0)
            return;

        _remainingDuration = Mathf.Max(_remainingDuration, duration);
        _spawnTimer = 0f;
        _isPlaying = true;
        SpawnSnapshot();
    }

    /// <summary>
    /// 设置残影生成间隔。
    /// </summary>
    /// <param name="interval">残影生成间隔。</param>
    public void SetSpawnInterval(float interval)
    {
        spawnInterval = Mathf.Max(0.01f, interval);
    }

    /// <summary>
    /// 设置残影初始颜色。
    /// </summary>
    /// <param name="color">残影初始颜色。</param>
    public void SetStartColor(Color color)
    {
        startColor = color;
    }

    /// <summary>
    /// 清空手动渲染来源，让残影自动收集玩家子节点下的全部 SpriteRenderer。
    /// </summary>
    public void ClearSourceRenderers()
    {
        sourceRenderers = null;
        CacheSourceRenderers();
    }

    /// <summary>
    /// 缓存玩家当前用于显示的精灵渲染器。
    /// </summary>
    private void CacheSourceRenderers()
    {
        _runtimeSourceRenderers.Clear();
        if (sourceRenderers != null && sourceRenderers.Length > 0)
        {
            for (int i = 0; i < sourceRenderers.Length; i++)
                AddSourceRenderer(sourceRenderers[i]);

            return;
        }

        SpriteRenderer[] childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < childRenderers.Length; i++)
            AddSourceRenderer(childRenderers[i]);
    }

    /// <summary>
    /// 添加有效的源渲染器。
    /// </summary>
    /// <param name="sourceRenderer">候选源渲染器。</param>
    private void AddSourceRenderer(SpriteRenderer sourceRenderer)
    {
        if (sourceRenderer == null || _runtimeSourceRenderers.Contains(sourceRenderer))
            return;

        _runtimeSourceRenderers.Add(sourceRenderer);
    }

    /// <summary>
    /// 创建残影对象池根节点。
    /// </summary>
    private void EnsurePoolRoot()
    {
        if (_poolRoot != null)
            return;

        GameObject poolRootObject = new GameObject($"{name}_AfterimagePool");
        _poolRoot = poolRootObject.transform;
        _poolRoot.SetParent(transform.parent, false);
        _poolRoot.localPosition = Vector3.zero;
        _poolRoot.localRotation = Quaternion.identity;
        _poolRoot.localScale = Vector3.one;
    }

    /// <summary>
    /// 预创建残影快照对象，降低首次闪避时的分配压力。
    /// </summary>
    private void PreloadSnapshots()
    {
        for (int i = _inactiveSnapshots.Count; i < preloadCount; i++)
            _inactiveSnapshots.Add(CreateSnapshot());
    }

    /// <summary>
    /// 推进残影生成计时。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickSpawn(float deltaTime)
    {
        if (!_isPlaying)
            return;

        _remainingDuration -= deltaTime;
        _spawnTimer -= deltaTime;
        while (_remainingDuration > 0f && _spawnTimer <= 0f)
        {
            SpawnSnapshot();
            _spawnTimer += spawnInterval;
        }

        if (_remainingDuration <= 0f)
            _isPlaying = false;
    }

    /// <summary>
    /// 推进已生成残影的淡出和回收。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickActiveSnapshots(float deltaTime)
    {
        for (int i = _activeSnapshots.Count - 1; i >= 0; i--)
        {
            AfterimageSnapshot snapshot = _activeSnapshots[i];
            snapshot.Timer += deltaTime;
            float alphaRate = 1f - Mathf.Clamp01(snapshot.Timer / snapshot.LifeTime);
            SetSnapshotAlpha(snapshot, startColor.a * alphaRate);

            if (snapshot.Timer < snapshot.LifeTime)
                continue;

            ReleaseSnapshotAt(i);
        }
    }

    /// <summary>
    /// 生成一帧玩家当前显示状态的残影快照。
    /// </summary>
    private void SpawnSnapshot()
    {
        EnsurePoolRoot();
        AfterimageSnapshot snapshot = GetSnapshot();
        snapshot.Timer = 0f;
        snapshot.LifeTime = imageLifeTime;
        SyncSnapshotRendererCount(snapshot, _runtimeSourceRenderers.Count);

        int visibleCount = 0;
        for (int i = 0; i < _runtimeSourceRenderers.Count; i++)
        {
            SpriteRenderer sourceRenderer = _runtimeSourceRenderers[i];
            if (sourceRenderer == null || !sourceRenderer.enabled || sourceRenderer.sprite == null || !sourceRenderer.gameObject.activeInHierarchy)
                continue;

            SpriteRenderer targetRenderer = snapshot.Renderers[visibleCount];
            CopyRendererSnapshot(sourceRenderer, targetRenderer);
            visibleCount++;
        }

        for (int i = visibleCount; i < snapshot.Renderers.Count; i++)
            snapshot.Renderers[i].gameObject.SetActive(false);

        if (visibleCount <= 0)
        {
            ReleaseSnapshot(snapshot);
            return;
        }

        snapshot.Root.SetActive(true);
        _activeSnapshots.Add(snapshot);
    }

    /// <summary>
    /// 从对象池中取出一个残影快照对象。
    /// </summary>
    /// <returns>残影快照对象。</returns>
    private AfterimageSnapshot GetSnapshot()
    {
        if (_inactiveSnapshots.Count <= 0)
            return CreateSnapshot();

        int lastIndex = _inactiveSnapshots.Count - 1;
        AfterimageSnapshot snapshot = _inactiveSnapshots[lastIndex];
        _inactiveSnapshots.RemoveAt(lastIndex);
        return snapshot;
    }

    /// <summary>
    /// 创建新的残影快照对象。
    /// </summary>
    /// <returns>残影快照对象。</returns>
    private AfterimageSnapshot CreateSnapshot()
    {
        EnsurePoolRoot();
        GameObject snapshotObject = new GameObject("PlayerAfterimage");
        snapshotObject.transform.SetParent(_poolRoot, false);
        snapshotObject.SetActive(false);
        return new AfterimageSnapshot(snapshotObject);
    }

    /// <summary>
    /// 同步残影对象中的渲染器数量。
    /// </summary>
    /// <param name="snapshot">残影快照对象。</param>
    /// <param name="requiredCount">需要的渲染器数量。</param>
    private void SyncSnapshotRendererCount(AfterimageSnapshot snapshot, int requiredCount)
    {
        while (snapshot.Renderers.Count < requiredCount)
        {
            GameObject rendererObject = new GameObject("AfterimageRenderer");
            rendererObject.transform.SetParent(snapshot.Root.transform, false);
            snapshot.Renderers.Add(rendererObject.AddComponent<SpriteRenderer>());
        }
    }

    /// <summary>
    /// 将源渲染器当前显示状态复制到残影渲染器。
    /// </summary>
    /// <param name="sourceRenderer">玩家源渲染器。</param>
    /// <param name="targetRenderer">残影目标渲染器。</param>
    private void CopyRendererSnapshot(SpriteRenderer sourceRenderer, SpriteRenderer targetRenderer)
    {
        Transform sourceTransform = sourceRenderer.transform;
        Transform targetTransform = targetRenderer.transform;
        targetTransform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
        targetTransform.localScale = sourceTransform.lossyScale;

        targetRenderer.sprite = sourceRenderer.sprite;
        targetRenderer.flipX = sourceRenderer.flipX;
        targetRenderer.flipY = sourceRenderer.flipY;
        targetRenderer.drawMode = sourceRenderer.drawMode;
        targetRenderer.size = sourceRenderer.size;
        targetRenderer.tileMode = sourceRenderer.tileMode;
        targetRenderer.maskInteraction = sourceRenderer.maskInteraction;
        targetRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        targetRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
        targetRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        targetRenderer.color = startColor;
        targetRenderer.gameObject.SetActive(true);
    }

    /// <summary>
    /// 设置残影快照整体透明度。
    /// </summary>
    /// <param name="snapshot">残影快照对象。</param>
    /// <param name="alpha">目标透明度。</param>
    private void SetSnapshotAlpha(AfterimageSnapshot snapshot, float alpha)
    {
        for (int i = 0; i < snapshot.Renderers.Count; i++)
        {
            SpriteRenderer targetRenderer = snapshot.Renderers[i];
            if (targetRenderer == null || !targetRenderer.gameObject.activeSelf)
                continue;

            Color color = targetRenderer.color;
            color.a = alpha;
            targetRenderer.color = color;
        }
    }

    /// <summary>
    /// 回收指定下标的激活残影。
    /// </summary>
    /// <param name="index">激活残影下标。</param>
    private void ReleaseSnapshotAt(int index)
    {
        AfterimageSnapshot snapshot = _activeSnapshots[index];
        _activeSnapshots.RemoveAt(index);
        ReleaseSnapshot(snapshot);
    }

    /// <summary>
    /// 回收一个残影快照对象。
    /// </summary>
    /// <param name="snapshot">需要回收的残影快照。</param>
    private void ReleaseSnapshot(AfterimageSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        snapshot.Root.SetActive(false);
        snapshot.Root.transform.SetParent(_poolRoot, false);
        _inactiveSnapshots.Add(snapshot);
    }

    /// <summary>
    /// 立即回收所有激活残影。
    /// </summary>
    private void ReleaseAllActiveSnapshots()
    {
        for (int i = _activeSnapshots.Count - 1; i >= 0; i--)
            ReleaseSnapshotAt(i);
    }
}
