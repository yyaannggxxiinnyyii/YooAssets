/// <summary>
/// 功能房内可被流程初始化的功能对象接口，例如宝箱、回血机和属性训练器。
/// </summary>
public interface IStageRoomFunctionObject
{
    /// <summary>
    /// 使用功能房运行时上下文初始化对象。
    /// </summary>
    /// <param name="context">功能房运行时上下文。</param>
    void Initialize(StageRoomFunctionContext context);
}
