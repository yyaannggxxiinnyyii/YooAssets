using System;
using UnityEngine;

/// <summary>
/// 标记 运行时对象由对象池管理，并提供回收入口。
/// </summary>
public sealed class GamePooledObject : MonoBehaviour
{
    private Action<GamePooledObject> _releaseAction;
    private bool _isReleased;
    private int _version;

    /// <summary>
    /// 当前取出版本，用于避免旧延迟回收影响新一轮复用。
    /// </summary>
    public int Version => _version;

    /// <summary>
    /// 配置对象回收到池时调用的委托。
    /// </summary>
    /// <param name="releaseAction">回收委托。</param>
    public void Initialize(Action<GamePooledObject> releaseAction)
    {
        _releaseAction = releaseAction;
        _isReleased = false;
    }

    /// <summary>
    /// 标记对象已从池中取出。
    /// </summary>
    public void MarkSpawned()
    {
        _isReleased = false;
        _version++;
    }

    /// <summary>
    /// 将当前对象回收到所属对象池。
    /// </summary>
    public void Release()
    {
        if (_isReleased)
            return;

        _isReleased = true;
        if (_releaseAction == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _releaseAction.Invoke(this);
    }
}

/// <summary>
/// 控制池化特效播放结束后的自动回收。
/// </summary>
internal sealed class GamePooledEffect : MonoBehaviour
{
    private const float FallbackLifeTime = 0.6f;

    private Action<GameObject> _releaseObject;
    private float _timer;
    private float _lifeTime;

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _lifeTime)
            return;

        if (_releaseObject != null)
            _releaseObject.Invoke(gameObject);
        else
            gameObject.SetActive(false);
    }

    /// <summary>
    /// 重置特效播放状态，并在播放结束后回收到对象池。
    /// </summary>
    /// <param name="releaseObject">对象回收委托。</param>
    public void Play(Action<GameObject> releaseObject)
    {
        _releaseObject = releaseObject;
        _timer = 0f;
        _lifeTime = GetParticleLifeTime();
    }

    /// <summary>
    /// 获取当前特效层级中最长的粒子播放时间。
    /// </summary>
    /// <returns>特效存活时间。</returns>
    private float GetParticleLifeTime()
    {
        float maxLifeTime = 0f;
        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            float duration = main.duration;
            float startLifetime = main.startLifetime.constantMax;
            maxLifeTime = Mathf.Max(maxLifeTime, duration + startLifetime);
        }

        return Mathf.Max(FallbackLifeTime, maxLifeTime);
    }
}


