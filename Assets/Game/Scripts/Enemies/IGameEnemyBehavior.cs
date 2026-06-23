/// <summary>
/// 定义敌人运行时行为接口，由具体敌人脚本实现移动和攻击逻辑。
/// </summary>
public interface IGameEnemyBehavior
{
    /// <summary>
    /// 初始化敌人行为运行时依赖。
    /// </summary>
    /// <param name="enemy">所属敌人控制器。</param>
    void Initialize(GameEnemyController enemy);

    /// <summary>
    /// 推进敌人行为。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    void TickBehavior(float deltaTime);
}
