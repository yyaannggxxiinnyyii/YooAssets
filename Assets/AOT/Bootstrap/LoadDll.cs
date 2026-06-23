using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using GameHotUpdate;
using UnityEngine;
using YooAsset;

/// <summary>
/// HybridCLR 热更新程序集加载器。
/// <para>
/// 该脚本应该放在 AOT 主工程中，负责在 YooAssets 完成资源更新之后，
/// 从资源包中读取 HotUpdate.dll.bytes，然后加载热更新程序集并调用入口方法。
/// </para>
/// </summary>
public sealed class LoadDll : MonoBehaviour
{
    /// <summary>
    /// 热更新 DLL 在 YooAssets 中的资源地址。
    /// <para>
    /// 该地址需要和 Bundle Collector 收集到的 Address 一致。
    /// 如果使用自动寻址，通常可以直接使用文件名 HotUpdate.dll.bytes。
    /// </para>
    /// </summary>
    [SerializeField] private string hotUpdateDllLocation = "HotUpdate.dll.bytes";

    /// <summary>
    /// 热更新程序集名称。
    /// </summary>
    [SerializeField] private string hotUpdateAssemblyName = "HotUpdate";

    /// <summary>
    /// 热更新入口类型名称。
    /// </summary>
    [SerializeField] private string entryTypeName = "Hello";

    /// <summary>
    /// 热更新入口静态方法名称。
    /// </summary>
    [SerializeField] private string entryMethodName = "Run";

    /// <summary>
    /// Unity 启动入口。
    /// </summary>
    private IEnumerator Start()
    {
        yield return WaitYooAssetsReady();

        Assembly hotUpdateAssembly = LoadHotUpdateAssembly();
        InvokeHotUpdateEntry(hotUpdateAssembly);
    }

    /// <summary>
    /// 等待 YooAssets 默认资源包准备完成。
    /// </summary>
    private IEnumerator WaitYooAssetsReady()
    {
        while (!YooAssetRuntime.IsReady)
            yield return null;
    }

    /// <summary>
    /// 加载热更新程序集。
    /// <para>
    /// 编辑器模式下，HotUpdate 程序集已经被 Unity 自动加载，直接从当前 AppDomain 查找即可。
    /// 非编辑器模式下，从 YooAssets 加载 HotUpdate.dll.bytes 并通过 Assembly.Load 加载。
    /// </para>
    /// </summary>
    /// <returns>加载完成的热更新程序集。</returns>
    private Assembly LoadHotUpdateAssembly()
    {
#if UNITY_EDITOR
        Assembly editorAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == hotUpdateAssemblyName);

        if (editorAssembly == null)
            throw new InvalidOperationException($"编辑器中未找到热更新程序集：{hotUpdateAssemblyName}");

        Debug.Log($"[LoadDll] 编辑器模式，直接使用已加载的热更新程序集：{hotUpdateAssemblyName}");
        return editorAssembly;
#else
        AssetHandle handle = YooAssetRuntime.LoadAssetAsync<TextAsset>(hotUpdateDllLocation);
        handle.WaitForAsyncComplete();

        TextAsset dllAsset = handle.GetAssetObject<TextAsset>();
        if (dllAsset == null)
        {
            handle.Release();
            throw new InvalidOperationException($"加载热更新 DLL 失败，资源地址：{hotUpdateDllLocation}");
        }

        byte[] dllBytes = dllAsset.bytes;
        Assembly hotUpdateAssembly = Assembly.Load(dllBytes);
        handle.Release();

        Debug.Log($"[LoadDll] 热更新程序集加载完成：{hotUpdateAssemblyName}");
        return hotUpdateAssembly;
#endif
    }

    /// <summary>
    /// 调用热更新程序集入口方法。
    /// </summary>
    /// <param name="hotUpdateAssembly">热更新程序集。</param>
    private void InvokeHotUpdateEntry(Assembly hotUpdateAssembly)
    {
        Type entryType = hotUpdateAssembly.GetType(entryTypeName);
        if (entryType == null)
            throw new InvalidOperationException($"热更新入口类型不存在：{entryTypeName}");

        MethodInfo entryMethod = entryType.GetMethod(
            entryMethodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (entryMethod == null)
            throw new InvalidOperationException($"热更新入口方法不存在：{entryTypeName}.{entryMethodName}");

        entryMethod.Invoke(null, null);
        Debug.Log($"[LoadDll] 热更新入口执行完成：{entryTypeName}.{entryMethodName}");
    }
}
