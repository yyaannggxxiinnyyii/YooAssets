using System;
using System.Collections;
using UnityEngine;
using YooAsset;

namespace GameHotUpdate
{
    /// <summary>
    /// YooAssets 热更新启动器。
    /// <para>
    /// 将该组件挂到首个场景的一个 GameObject 上，它会在启动时自动执行资源系统初始化流程：
    /// 初始化 YooAssets 全局系统、初始化资源包、请求资源版本、加载资源清单、下载缺失文件、
    /// 清理无用缓存，最后把资源包注册到 <see cref="YooAssetRuntime"/>。
    /// </para>
    /// </summary>
    public sealed class YooAssetUpdater : MonoBehaviour
    {
        /// <summary>
        /// 项目启动时使用的 YooAssets 运行模式。
        /// </summary>
        public enum StartupPlayMode
        {
            /// <summary>
            /// 编辑器模拟模式。
            /// <para>
            /// 该模式只在 Unity Editor 中使用，不需要真实构建 AssetBundle，也不需要搭建 CDN。
            /// 适合开发阶段快速验证资源加载流程。
            /// </para>
            /// </summary>
            EditorSimulateMode,

            /// <summary>
            /// 联机运行模式。
            /// <para>
            /// 该模式用于真实热更新流程，会从内置资源、沙盒缓存和远端服务器中读取资源。
            /// </para>
            /// </summary>
            HostPlayMode
        }

        [Header("Package")]
        // YooAssets 资源包名称，需要和资源收集配置里的 PackageName 一致。
        [SerializeField] private string packageName = "DefaultPackage";

        // 启动模式。学习阶段建议使用 EditorSimulateMode，真实热更新测试使用 HostPlayMode。
        [SerializeField] private StartupPlayMode playMode = StartupPlayMode.EditorSimulateMode;

        [Header("Remote")]
        // 默认 CDN 根地址。HostPlayMode 下会用它拼接资源版本文件、清单文件和 Bundle 文件地址。
        [SerializeField] private string defaultHostServer = "http://127.0.0.1/CDN/PC/v1.0";

        // 备用 CDN 根地址。默认和主地址一致，正式项目可以填写备用服务器地址。
        [SerializeField] private string fallbackHostServer = "http://127.0.0.1/CDN/PC/v1.0";

        [Header("Download")]
        // 同时下载的最大文件数量。
        [SerializeField] private int downloadMaxConcurrency = 10;

        // 单个文件下载失败后的重试次数。
        [SerializeField] private int downloadRetryCount = 3;

        // 是否在控制台输出下载进度。
        [SerializeField] private bool logDownloadProgress = true;

        /// <summary>
        /// 热更新流程全部完成时触发。
        /// </summary>
        public event Action<ResourcePackage> Completed;

        /// <summary>
        /// 热更新流程失败时触发，参数为失败原因。
        /// </summary>
        public event Action<string> Failed;

        /// <summary>
        /// 当前启动器初始化的资源包。
        /// </summary>
        public ResourcePackage Package { get; private set; }

        /// <summary>
        /// Unity 启动入口。
        /// <para>
        /// 组件启动后会保留自身对象，并开始执行热更新流程。
        /// </para>
        /// </summary>
        /// <returns>热更新流程协程。</returns>
        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            yield return Run();
        }

        /// <summary>
        /// 执行完整的 YooAssets 热更新启动流程。
        /// <para>
        /// 流程顺序为：初始化全局系统、创建资源包、初始化资源包、请求版本、
        /// 加载清单、创建下载器、下载资源、清理缓存、设置默认资源包。
        /// </para>
        /// </summary>
        /// <returns>热更新流程协程。</returns>
        public IEnumerator Run()
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                ReportFailure("资源包名称不能为空。");
                yield break;
            }

            if (!YooAssets.IsInitialized)
                YooAssets.Initialize();

            Package = GetOrCreatePackage(packageName);

            var initOp = Package.InitializePackageAsync(CreateInitializeOptions());
            yield return initOp;
            if (!CheckOperation(initOp, "初始化资源包"))
                yield break;

            var versionOp = Package.RequestPackageVersionAsync();
            yield return versionOp;
            if (!CheckOperation(versionOp, "请求资源版本"))
                yield break;

            Debug.Log($"[YooAssetUpdater] 资源包版本：{versionOp.PackageVersion}");

            var manifestOp = Package.LoadPackageManifestAsync(new LoadPackageManifestOptions(versionOp.PackageVersion, 60));
            yield return manifestOp;
            if (!CheckOperation(manifestOp, "加载资源清单"))
                yield break;

            var downloader = Package.CreateResourceDownloader(
                new ResourceDownloaderOptions(downloadMaxConcurrency, downloadRetryCount));

            if (downloader.TotalDownloadCount > 0)
            {
                Debug.Log($"[YooAssetUpdater] 需要下载 {downloader.TotalDownloadCount} 个文件，总大小 {FormatBytes(downloader.TotalDownloadBytes)}。");

                downloader.DownloadError += OnDownloadError;
                downloader.DownloadProgressChanged += OnDownloadProgressChanged;
                downloader.StartDownload();
                yield return downloader;

                downloader.DownloadError -= OnDownloadError;
                downloader.DownloadProgressChanged -= OnDownloadProgressChanged;

                if (!CheckOperation(downloader, "下载资源文件"))
                    yield break;
            }
            else
            {
                Debug.Log("[YooAssetUpdater] 没有需要下载的文件。");
            }

            var clearCacheOp = Package.ClearCacheAsync(new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles));
            yield return clearCacheOp;
            if (!CheckOperation(clearCacheOp, "清理无用缓存"))
                yield break;

            YooAssetRuntime.SetDefaultPackage(Package);
            Debug.Log("[YooAssetUpdater] YooAssets 资源包准备完成。");
            Completed?.Invoke(Package);
        }

        /// <summary>
        /// 根据当前启动模式创建 YooAssets 资源包初始化参数。
        /// </summary>
        /// <returns>适用于当前启动模式的初始化参数。</returns>
        private InitializePackageOptions CreateInitializeOptions()
        {
            switch (playMode)
            {
                case StartupPlayMode.EditorSimulateMode:
                    return CreateEditorSimulateModeOptions();

                case StartupPlayMode.HostPlayMode:
                    return CreateHostPlayModeOptions();

                default:
                    throw new ArgumentOutOfRangeException(nameof(playMode), playMode, "不支持的 YooAssets 运行模式。");
            }
        }

        /// <summary>
        /// 创建编辑器模拟模式初始化参数。
        /// <para>
        /// 该模式会调用 YooAssets 的模拟构建器生成临时清单，然后通过 EditorFileSystem 读取编辑器资源。
        /// </para>
        /// </summary>
        /// <returns>编辑器模拟模式初始化参数。</returns>
        private InitializePackageOptions CreateEditorSimulateModeOptions()
        {
#if UNITY_EDITOR
            var buildResult = EditorSimulateBuildInvoker.Build(packageName, (int)EBundleType.VirtualAssetBundle);
            var options = new EditorSimulateModeOptions();
            options.EditorFileSystemParameters =
                FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
            return options;
#else
            throw new NotSupportedException("EditorSimulateMode 只能在 Unity 编辑器中运行。");
#endif
        }

        /// <summary>
        /// 创建联机运行模式初始化参数。
        /// <para>
        /// HostPlayMode 同时配置内置文件系统和沙盒缓存文件系统。
        /// 内置文件系统负责读取随包资源，沙盒缓存文件系统负责读取和保存远端下载资源。
        /// </para>
        /// </summary>
        /// <returns>联机运行模式初始化参数。</returns>
        private InitializePackageOptions CreateHostPlayModeOptions()
        {
            var remoteService = new YooAssetRemoteService(defaultHostServer, fallbackHostServer);
            var options = new HostPlayModeOptions();

            options.BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
            options.BuiltinFileSystemParameters.AddParameter(EFileSystemParameter.CopyBuiltinPackageManifest, true);

            options.CacheFileSystemParameters = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);
            options.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadMaxConcurrency, Mathf.Max(1, downloadMaxConcurrency));
            options.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadMaxRequestPerFrame, 1);
            options.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadWatchdogTimeout, 10);

            return options;
        }

        /// <summary>
        /// 获取已经存在的资源包，如果不存在则创建一个新的资源包。
        /// </summary>
        /// <param name="targetPackageName">资源包名称。</param>
        /// <returns>资源包实例。</returns>
        private ResourcePackage GetOrCreatePackage(string targetPackageName)
        {
            if (YooAssets.TryGetPackage(targetPackageName, out var package))
                return package;

            return YooAssets.CreatePackage(targetPackageName);
        }

        /// <summary>
        /// 检查 YooAssets 异步操作是否成功。
        /// </summary>
        /// <param name="operation">需要检查的 YooAssets 异步操作。</param>
        /// <param name="step">当前流程步骤名称，用于输出错误信息。</param>
        /// <returns>操作成功返回 true，否则返回 false。</returns>
        private bool CheckOperation(AsyncOperationBase operation, string step)
        {
            if (operation.Status == EOperationStatus.Succeeded)
                return true;

            ReportFailure($"{step}失败：{operation.Error}");
            return false;
        }

        /// <summary>
        /// 下载文件失败时的回调。
        /// </summary>
        /// <param name="args">下载错误信息。</param>
        private void OnDownloadError(DownloadErrorEventArgs args)
        {
            Debug.LogError($"[YooAssetUpdater] 下载失败：{args.FileName}，{args.ErrorInfo}");
        }

        /// <summary>
        /// 下载进度变化时的回调。
        /// </summary>
        /// <param name="args">下载进度信息。</param>
        private void OnDownloadProgressChanged(DownloadProgressChangedEventArgs args)
        {
            if (!logDownloadProgress)
                return;

            Debug.Log(
                $"[YooAssetUpdater] 下载进度 {args.CurrentDownloadCount}/{args.TotalDownloadCount}，" +
                $"{FormatBytes(args.CurrentDownloadBytes)}/{FormatBytes(args.TotalDownloadBytes)}, " +
                $"{args.Progress:P1}");
        }

        /// <summary>
        /// 统一处理热更新流程失败。
        /// </summary>
        /// <param name="error">失败原因。</param>
        private void ReportFailure(string error)
        {
            Debug.LogError($"[YooAssetUpdater] {error}");
            Failed?.Invoke(error);
        }

        /// <summary>
        /// 将字节数转换为便于阅读的字符串。
        /// </summary>
        /// <param name="bytes">字节数。</param>
        /// <returns>格式化后的容量字符串。</returns>
        private static string FormatBytes(long bytes)
        {
            const float kb = 1024f;
            const float mb = kb * 1024f;

            if (bytes >= mb)
                return $"{bytes / mb:F2} MB";
            if (bytes >= kb)
                return $"{bytes / kb:F2} KB";

            return $"{bytes} B";
        }
    }
}
