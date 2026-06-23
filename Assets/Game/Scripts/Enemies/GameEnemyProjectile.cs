using UnityEngine;

/// <summary>
/// 控制敌人弹幕飞行、命中玩家和生命周期。
/// </summary>
public sealed class GameEnemyProjectile : MonoBehaviour
{
    private static Sprite _defaultSprite;

    private GamePlayerController _target;
    private Vector3 _direction;
    private int _damage;
    private float _speed;
    private float _lifeTime;
    private float _hitRadius;
    private float _timer;

    /// <summary>
    /// 敌人弹幕命中玩家的中心距离判定半径。
    /// </summary>
    public float HitRadius => Mathf.Max(0.01f, _hitRadius);

    private void Update()
    {
        if (_target == null || _target.IsDead)
        {
            Destroy(gameObject);
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= _lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += _direction * _speed * Time.deltaTime;
        if (Vector3.Distance(transform.position, _target.transform.position) > _hitRadius)
            return;

        _target.TakeDamage(_damage);
        Destroy(gameObject);
    }

    /// <summary>
    /// 在 Scene 视图中绘制敌人弹幕命中半径。
    /// </summary>
    private void OnDrawGizmos()
    {
        GameRadiusGizmoUtility.DrawRadius(transform.position, HitRadius, new Color(1f, 0.25f, 0.15f, 0.9f), "Enemy Projectile Hit");
    }

    /// <summary>
    /// 初始化敌人弹幕运行时参数。
    /// </summary>
    /// <param name="target">玩家目标。</param>
    /// <param name="direction">飞行方向。</param>
    /// <param name="damage">命中伤害。</param>
    /// <param name="speed">飞行速度。</param>
    /// <param name="lifeTime">存在时间。</param>
    /// <param name="hitRadius">命中半径。</param>
    public void Initialize(
        GamePlayerController target,
        Vector3 direction,
        int damage,
        float speed,
        float lifeTime,
        float hitRadius)
    {
        _target = target;
        _direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.right;
        _damage = Mathf.Max(0, damage);
        _speed = Mathf.Max(0.1f, speed);
        _lifeTime = Mathf.Max(0.1f, lifeTime);
        _hitRadius = Mathf.Max(0.05f, hitRadius);
        _timer = 0f;
        ConfigureVisual();
        SyncRotationToDirection();
    }

    /// <summary>
    /// 配置敌人弹幕默认显示。
    /// </summary>
    private void ConfigureVisual()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = GetDefaultSprite();
        spriteRenderer.color = new Color(0.65f, 1f, 0.32f, 1f);
        spriteRenderer.sortingOrder = 8;
    }

    /// <summary>
    /// 让弹幕朝向飞行方向。
    /// </summary>
    private void SyncRotationToDirection()
    {
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// 获取运行时创建的默认圆形弹幕 Sprite。
    /// </summary>
    /// <returns>默认弹幕 Sprite。</returns>
    private static Sprite GetDefaultSprite()
    {
        if (_defaultSprite != null)
            return _defaultSprite;

        const int Size = 16;
        Texture2D texture = new Texture2D(Size, Size);
        Vector2 center = new Vector2((Size - 1) * 0.5f, (Size - 1) * 0.5f);
        float radius = Size * 0.42f;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        _defaultSprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        return _defaultSprite;
    }
}
