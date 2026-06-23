using System.Collections.Generic;
using YooAsset;

namespace GameHotUpdate
{
    /// <summary>
    /// YooAssets 远端资源地址查询服务。
    /// <para>
    /// YooAssets 在下载资源文件时只知道文件名，例如资源清单、Hash 文件或 Bundle 文件名。
    /// 该类负责把文件名拼接成完整的远端下载地址，并提供主地址和备用地址。
    /// </para>
    /// </summary>
    public sealed class YooAssetRemoteService : IRemoteService
    {
        private readonly string _defaultHostServer;
        private readonly string _fallbackHostServer;

        /// <summary>
        /// 创建远端资源地址查询服务。
        /// </summary>
        /// <param name="defaultHostServer">默认 CDN 根地址，例如 http://127.0.0.1/CDN/PC/v1.0。</param>
        /// <param name="fallbackHostServer">备用 CDN 根地址。如果为空，则使用默认 CDN 根地址。</param>
        public YooAssetRemoteService(string defaultHostServer, string fallbackHostServer)
        {
            _defaultHostServer = TrimEndSlash(defaultHostServer);
            _fallbackHostServer = TrimEndSlash(string.IsNullOrWhiteSpace(fallbackHostServer)
                ? defaultHostServer
                : fallbackHostServer);
        }

        /// <summary>
        /// 根据 YooAssets 传入的文件名返回可下载的完整 URL 列表。
        /// </summary>
        /// <param name="fileName">YooAssets 要访问的文件名。</param>
        /// <returns>包含默认地址和备用地址的 URL 列表。</returns>
        public IReadOnlyList<string> GetRemoteUrls(string fileName)
        {
            return new[]
            {
                $"{_defaultHostServer}/{fileName}",
                $"{_fallbackHostServer}/{fileName}"
            };
        }

        /// <summary>
        /// 移除 URL 末尾的斜杠，避免拼接文件名时出现重复斜杠。
        /// </summary>
        /// <param name="url">原始 URL。</param>
        /// <returns>去掉末尾斜杠后的 URL。</returns>
        private static string TrimEndSlash(string url)
        {
            return string.IsNullOrWhiteSpace(url) ? string.Empty : url.TrimEnd('/');
        }
    }
}
