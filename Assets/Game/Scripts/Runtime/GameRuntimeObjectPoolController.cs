using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 统一管理战斗运行时不同类型对象的创建、取出和复用。
/// </summary>
public sealed class GameRuntimeObjectPoolController : MonoBehaviour
{
    /// <summary>
    /// 运行时对象池类型。
    /// </summary>
    public enum PoolObjectType
    {
        Enemy,
        Projectile,
        ProjectileEffect,
        ExperiencePickup,
        GoldPickup,
        DamageNumber
    }

    /// <summary>
    /// 维护单一对象类型的非激活实例栈。
    /// </summary>
    private sealed class RuntimePool
    {
        private readonly Stack<GamePooledObject> _inactiveObjects = new Stack<GamePooledObject>();
        private readonly GameRuntimeObjectPoolController _owner;
        private readonly PoolObjectType _objectType;
        private readonly GameObject _prefab;
        private readonly bool _usesPrefab;

        /// <summary>
        /// 创建指定类型的运行时对象池。
        /// </summary>
        /// <param name="owner">对象池控制器。</param>
        /// <param name="objectType">对象类型。</param>
        public RuntimePool(GameRuntimeObjectPoolController owner, PoolObjectType objectType, GameObject prefab = null)
        {
            _owner = owner;
            _objectType = objectType;
            _prefab = prefab;
            _usesPrefab = prefab != null;
        }

        /// <summary>
        /// 取出或创建对象，并应用本次生成的父级、位置、缩放和颜色。
        /// </summary>
        /// <param name="activeParent">激活对象父级。</param>
        /// <param name="position">世界坐标。</param>
        /// <param name="scale">本地缩放。</param>
        /// <param name="color">显示颜色。</param>
        /// <returns>池对象标记组件。</returns>
        public GamePooledObject Get(Transform activeParent, Vector3 position, Vector3 scale, Color color)
        {
            GamePooledObject pooledObject = _inactiveObjects.Count > 0
                ? _inactiveObjects.Pop()
                : _owner.CreateObject(_objectType, Release, _prefab);

            if (activeParent != null && !activeParent.gameObject.activeSelf)
                activeParent.gameObject.SetActive(true);

            Transform targetTransform = pooledObject.transform;
            targetTransform.SetParent(activeParent, false);
            targetTransform.position = position;
            targetTransform.localRotation = Quaternion.identity;
            targetTransform.localScale = scale;
            pooledObject.gameObject.SetActive(true);
            pooledObject.MarkSpawned();
            _owner.ApplySpawnVisual(pooledObject, _objectType, color, _usesPrefab);
            _owner.ResetSpawnEffects(pooledObject);
            return pooledObject;
        }

        /// <summary>
        /// 回收对象到当前类型的非激活实例栈。
        /// </summary>
        /// <param name="pooledObject">需要回收的池对象。</param>
        public void Release(GamePooledObject pooledObject)
        {
            if (pooledObject == null)
                return;

            pooledObject.gameObject.SetActive(false);
            pooledObject.transform.SetParent(_owner.PoolRoot, false);
            _inactiveObjects.Push(pooledObject);
        }
    }

    [Header("描边参数")]
    [SerializeField] private GameSpriteOutlineSettings enemyOutline = new GameSpriteOutlineSettings(new Color(1f, 0.26f, 0.22f, 1f), 1.15f, 0.7f);
    [SerializeField] private GameSpriteOutlineSettings projectileOutline = new GameSpriteOutlineSettings(new Color(0.75f, 0.95f, 1f, 1f), 1.1f, 1.1f);
    [SerializeField] private GameSpriteOutlineSettings experiencePickupOutline = new GameSpriteOutlineSettings(new Color(0.55f, 0.9f, 1f, 1f), 1.35f, 1.2f, 0.18f);
    [SerializeField] private GameSpriteOutlineSettings goldPickupOutline = new GameSpriteOutlineSettings(new Color(1f, 0.88f, 0.28f, 1f), 1.35f, 1.25f, 0.22f);

    private readonly Dictionary<PoolObjectType, RuntimePool> _pools = new Dictionary<PoolObjectType, RuntimePool>();
    private readonly Dictionary<GameObject, RuntimePool> _prefabPools = new Dictionary<GameObject, RuntimePool>();

    private Transform _poolRoot;
    private Sprite _defaultSprite;

    private Transform PoolRoot => _poolRoot != null ? _poolRoot : transform;

    /// <summary>
    /// 初始化运行时对象池依赖。
    /// </summary>
    /// <param name="poolRoot">非激活池对象父级。</param>
    /// <param name="defaultSprite">默认 Sprite 图形。</param>
    public void InitializeRuntimePools(Transform poolRoot, Sprite defaultSprite)
    {
        _poolRoot = poolRoot;
        _defaultSprite = defaultSprite;
        _pools.Clear();
        _prefabPools.Clear();
    }

    /// <summary>
    /// 从指定类型对象池中取出对象。
    /// </summary>
    /// <param name="objectType">池对象类型。</param>
    /// <param name="activeParent">激活后的父级。</param>
    /// <param name="position">世界坐标。</param>
    /// <param name="scale">本地缩放。</param>
    /// <param name="color">显示颜色。</param>
    /// <returns>池对象标记组件。</returns>
    public GamePooledObject Get(PoolObjectType objectType, Transform activeParent, Vector3 position, Vector3 scale, Color color)
    {
        return GetPool(objectType).Get(activeParent, position, scale, color);
    }

    /// <summary>
    /// 从指定预制体对象池中取出对象，未配置预制体时回退到类型默认对象池。
    /// </summary>
    /// <param name="objectType">池对象类型。</param>
    /// <param name="prefab">对象预制体。</param>
    /// <param name="activeParent">激活后的父级。</param>
    /// <param name="position">世界坐标。</param>
    /// <param name="scale">本地缩放。</param>
    /// <param name="color">显示颜色。</param>
    /// <returns>池对象标记组件。</returns>
    public GamePooledObject Get(
        PoolObjectType objectType,
        GameObject prefab,
        Transform activeParent,
        Vector3 position,
        Vector3 scale,
        Color color)
    {
        if (prefab == null)
            return Get(objectType, activeParent, position, scale, color);

        return GetPrefabPool(objectType, prefab).Get(activeParent, position, scale, color);
    }

    /// <summary>
    /// 回收池对象；非池对象会直接销毁。
    /// </summary>
    /// <param name="targetObject">需要回收的对象。</param>
    public void ReleaseObject(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        GamePooledObject pooledObject = targetObject.GetComponent<GamePooledObject>();
        if (pooledObject == null)
        {
            Destroy(targetObject);
            return;
        }

        pooledObject.Release();
    }

    /// <summary>
    /// 获取指定类型的默认显示颜色。
    /// </summary>
    /// <param name="objectType">池对象类型。</param>
    /// <returns>默认显示颜色。</returns>
    public Color GetDefaultColor(PoolObjectType objectType)
    {
        switch (objectType)
        {
            case PoolObjectType.Enemy:
                return new Color(0.86f, 0.26f, 0.24f);

            case PoolObjectType.Projectile:
                return new Color(0.35f, 0.82f, 1f);

            case PoolObjectType.ProjectileEffect:
                return Color.white;

            case PoolObjectType.ExperiencePickup:
                return new Color(0.3f, 0.75f, 1f);

            case PoolObjectType.GoldPickup:
                return new Color(1f, 0.82f, 0.24f);

            default:
                return Color.white;
        }
    }

    /// <summary>
    /// 获取或创建指定类型对象池。
    /// </summary>
    /// <param name="objectType">池对象类型。</param>
    /// <returns>对象池实例。</returns>
    private RuntimePool GetPool(PoolObjectType objectType)
    {
        if (!_pools.TryGetValue(objectType, out RuntimePool pool))
        {
            pool = new RuntimePool(this, objectType);
            _pools.Add(objectType, pool);
        }

        return pool;
    }

    /// <summary>
    /// 获取或创建指定预制体的运行时对象池。
    /// </summary>
    /// <param name="objectType">池对象类型。</param>
    /// <param name="prefab">对象预制体。</param>
    /// <returns>对象池实例。</returns>
    private RuntimePool GetPrefabPool(PoolObjectType objectType, GameObject prefab)
    {
        if (!_prefabPools.TryGetValue(prefab, out RuntimePool pool))
        {
            pool = new RuntimePool(this, objectType, prefab);
            _prefabPools.Add(prefab, pool);
        }

        return pool;
    }

    /// <summary>
    /// 创建指定类型的池对象。
    /// </summary>
    /// <param name="objectType">池对象类型。</param>
    /// <param name="releaseAction">对象回收委托。</param>
    /// <returns>池对象标记组件。</returns>
    private GamePooledObject CreateObject(PoolObjectType objectType, Action<GamePooledObject> releaseAction, GameObject prefab = null)
    {
        GameObject targetObject = prefab != null
            ? Instantiate(prefab)
            : new GameObject(GetObjectName(objectType));

        targetObject.name = prefab != null ? prefab.name : GetObjectName(objectType);
        if (prefab == null)
        {
            if (objectType == PoolObjectType.DamageNumber)
                ConfigureDamageNumberObject(targetObject);
            else
                ConfigureSpriteObject(targetObject, GetDefaultColor(objectType));
        }

        GamePooledObject pooledObject = targetObject.GetComponent<GamePooledObject>();
        if (pooledObject == null)
            pooledObject = targetObject.AddComponent<GamePooledObject>();

        pooledObject.Initialize(releaseAction);
        targetObject.SetActive(false);
        targetObject.transform.SetParent(PoolRoot, false);
        return pooledObject;
    }

    /// <summary>
    /// 配置 Sprite 类型池对象。
    /// </summary>
    /// <param name="targetObject">目标对象。</param>
    /// <param name="defaultColor">默认颜色。</param>
    private void ConfigureSpriteObject(GameObject targetObject, Color defaultColor)
    {
        SpriteRenderer spriteRenderer = targetObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = _defaultSprite;
        spriteRenderer.color = defaultColor;
        ApplyOutline(spriteRenderer, GetObjectTypeFromName(targetObject.name));
    }

    /// <summary>
    /// 配置伤害跳字类型池对象。
    /// </summary>
    /// <param name="targetObject">目标对象。</param>
    private void ConfigureDamageNumberObject(GameObject targetObject)
    {
        TextMeshPro text = targetObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.sortingOrder = 50;
        targetObject.AddComponent<GameDamageNumber>();
    }

    /// <summary>
    /// 应用每次取出对象时的显示参数。
    /// </summary>
    /// <param name="pooledObject">池对象。</param>
    /// <param name="objectType">池对象类型。</param>
    /// <param name="color">显示颜色。</param>
    private void ApplySpawnVisual(GamePooledObject pooledObject, PoolObjectType objectType, Color color, bool keepPrefabColor = false)
    {
        if (objectType == PoolObjectType.DamageNumber)
            return;

        if (keepPrefabColor)
        {
            ApplyPrefabOutline(pooledObject, objectType);
            return;
        }

        SpriteRenderer spriteRenderer = pooledObject.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            if (_defaultSprite != null)
                spriteRenderer.sprite = _defaultSprite;

            spriteRenderer.color = color;

            ApplyOutline(spriteRenderer, objectType);
        }
    }

    /// <summary>
    /// 为使用自定义预制体的池对象应用统一描边，同时保留预制体自身贴图和颜色。
    /// </summary>
    /// <param name="pooledObject">池对象。</param>
    /// <param name="objectType">池对象类型。</param>
    private void ApplyPrefabOutline(GamePooledObject pooledObject, PoolObjectType objectType)
    {
        if (pooledObject == null || objectType != PoolObjectType.Projectile)
            return;

        SpriteRenderer[] spriteRenderers = pooledObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null || IsOutlineRenderer(spriteRenderer))
                continue;

            ApplyOutline(spriteRenderer, objectType);
        }
    }

    /// <summary>
    /// 判断渲染器是否为描边控制器生成的辅助渲染层。
    /// </summary>
    /// <param name="spriteRenderer">待判断的渲染器。</param>
    /// <returns>属于描边辅助层时返回 true。</returns>
    private bool IsOutlineRenderer(SpriteRenderer spriteRenderer)
    {
        return spriteRenderer != null && spriteRenderer.name.StartsWith("SpriteOutline_", StringComparison.Ordinal);
    }

    /// <summary>
    /// 重置对象复用时容易残留的拖尾和粒子播放状态。
    /// </summary>
    /// <param name="pooledObject">池对象。</param>
    private void ResetSpawnEffects(GamePooledObject pooledObject)
    {
        if (pooledObject == null)
            return;

        TrailRenderer[] trailRenderers = pooledObject.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            if (trailRenderers[i] != null)
                trailRenderers[i].Clear();
        }

        ParticleSystem[] particleSystems = pooledObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    /// <summary>
    /// 给池对象 SpriteRenderer 应用对应类型的描边效果。
    /// </summary>
    /// <param name="spriteRenderer">目标 SpriteRenderer。</param>
    /// <param name="objectType">池对象类型。</param>
    private void ApplyOutline(SpriteRenderer spriteRenderer, PoolObjectType objectType)
    {
        if (spriteRenderer == null)
            return;

        GameSpriteOutlineSettings outlineSettings;
        switch (objectType)
        {
            case PoolObjectType.Projectile:
                outlineSettings = projectileOutline;
                break;

            case PoolObjectType.ProjectileEffect:
                return;

            case PoolObjectType.ExperiencePickup:
                outlineSettings = experiencePickupOutline;
                break;

            case PoolObjectType.GoldPickup:
                outlineSettings = goldPickupOutline;
                break;

            case PoolObjectType.Enemy:
                outlineSettings = enemyOutline;
                break;

            default:
                outlineSettings = projectileOutline;
                break;
        }

        GameSpriteOutlineController.Configure(spriteRenderer, outlineSettings);
    }

    /// <summary>
    /// 根据对象名称兜底推断池对象类型，仅用于创建阶段的默认描边。
    /// </summary>
    /// <param name="objectName">对象名称。</param>
    /// <returns>池对象类型。</returns>
    private PoolObjectType GetObjectTypeFromName(string objectName)
    {
        if (objectName == nameof(PoolObjectType.Projectile) || objectName == "GameProjectile")
            return PoolObjectType.Projectile;

        if (objectName == "ExperiencePickup")
            return PoolObjectType.ExperiencePickup;

        if (objectName == "GoldPickup")
            return PoolObjectType.GoldPickup;

        if (objectName == "Enemy")
            return PoolObjectType.Enemy;

        return PoolObjectType.Projectile;
    }

    /// <summary>
    /// 获取对象名称。
    /// </summary>
    /// <param name="objectType">池对象类型。</param>
    /// <returns>对象名称。</returns>
    private string GetObjectName(PoolObjectType objectType)
    {
        switch (objectType)
        {
            case PoolObjectType.Enemy:
                return "Enemy";

            case PoolObjectType.Projectile:
                return "GameProjectile";

            case PoolObjectType.ProjectileEffect:
                return "ProjectileEffect";

            case PoolObjectType.ExperiencePickup:
                return "ExperiencePickup";

            case PoolObjectType.GoldPickup:
                return "GoldPickup";

            case PoolObjectType.DamageNumber:
                return "DamageNumber";

            default:
                return objectType.ToString();
        }
    }
}
