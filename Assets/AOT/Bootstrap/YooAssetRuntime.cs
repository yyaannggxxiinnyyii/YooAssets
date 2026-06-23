using System;
using UnityEngine;
using YooAsset;

namespace GameHotUpdate
{
    /// <summary>
    /// YooAssets 运行时访问入口。
    /// <para>
    /// 该类保存已经初始化完成的默认资源包，并提供简单的资源和场景加载方法。
    /// 业务代码应该在 <see cref="YooAssetUpdater"/> 完成后再通过这里加载热更新资源。
    /// </para>
    /// </summary>
    public static class YooAssetRuntime
    {
        /// <summary>
        /// 当前项目使用的默认资源包。
        /// </summary>
        public static ResourcePackage DefaultPackage { get; private set; }

        /// <summary>
        /// 默认资源包是否已经初始化完成并且资源清单可用。
        /// </summary>
        public static bool IsReady => DefaultPackage != null && DefaultPackage.PackageValid;

        /// <summary>
        /// 设置默认资源包。
        /// <para>
        /// 该方法由 <see cref="YooAssetUpdater"/> 在初始化、清单更新和下载流程完成后调用。
        /// </para>
        /// </summary>
        /// <param name="package">已经准备好的 YooAssets 资源包。</param>
        public static void SetDefaultPackage(ResourcePackage package)
        {
            DefaultPackage = package ?? throw new ArgumentNullException(nameof(package));
        }

        /// <summary>
        /// 从默认资源包异步加载一个资源。
        /// </summary>
        /// <typeparam name="TObject">要加载的 Unity 资源类型，例如 GameObject、Texture2D、AudioClip。</typeparam>
        /// <param name="location">资源定位地址，通常是 YooAssets 收集器生成的 Address。</param>
        /// <returns>资源加载句柄。调用方在不再使用资源时需要释放该句柄。</returns>
        public static AssetHandle LoadAssetAsync<TObject>(string location) where TObject : UnityEngine.Object
        {
            EnsureReady();
            return DefaultPackage.LoadAssetAsync<TObject>(location);
        }

        /// <summary>
        /// 从默认资源包异步加载场景。
        /// </summary>
        /// <param name="location">场景资源定位地址。</param>
        /// <returns>场景加载句柄。</returns>
        public static SceneHandle LoadSceneAsync(string location)
        {
            EnsureReady();
            return DefaultPackage.LoadSceneAsync(location);
        }

        /// <summary>
        /// 释放资源加载句柄。
        /// </summary>
        /// <param name="handle">需要释放的资源句柄。</param>
        public static void Release(AssetHandle handle)
        {
            if (handle != null)
                handle.Release();
        }

        /// <summary>
        /// 确保默认资源包已经准备好。
        /// </summary>
        /// <exception cref="InvalidOperationException">默认资源包尚未初始化完成时抛出。</exception>
        private static void EnsureReady()
        {
            if (!IsReady)
                throw new InvalidOperationException("YooAssets 资源包尚未准备完成，请等待 YooAssetUpdater 流程结束。");
        }
    }
}
