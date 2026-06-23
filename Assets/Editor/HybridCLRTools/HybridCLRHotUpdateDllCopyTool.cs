using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HybridCLR 热更新 DLL 复制工具。
/// <para>
/// HybridCLR 编译出的热更新 DLL 位于 HybridCLRData/HotUpdateDlls/平台目录下。
/// YooAssets 不能直接收集项目外部文件，因此这里把 HotUpdate.dll 复制到 Assets/HotUpdateDlls，
/// 并改名为 HotUpdate.dll.bytes，方便作为 TextAsset 被 YooAssets 收集和热更新。
/// </para>
/// </summary>
public static class HybridCLRHotUpdateDllCopyTool
{
    private const string SourceRoot = "HybridCLRData/HotUpdateDlls";
    private const string TargetDirectory = "Assets/HotUpdateDlls";
    private const string HotUpdateDllName = "HotUpdate.dll";
    private const string HotUpdateBytesName = "HotUpdate.dll.bytes";

    /// <summary>
    /// 复制当前激活平台的 HotUpdate.dll 到 Assets/HotUpdateDlls/HotUpdate.dll.bytes。
    /// </summary>
    [MenuItem("Tools/HybridCLR/复制热更新DLL到YooAssets资源目录")]
    public static void CopyActiveTargetHotUpdateDll()
    {
        string buildTargetName = EditorUserBuildSettings.activeBuildTarget.ToString();
        string sourcePath = Path.Combine(SourceRoot, buildTargetName, HotUpdateDllName);
        string targetPath = Path.Combine(TargetDirectory, HotUpdateBytesName);

        if (!File.Exists(sourcePath))
        {
            Debug.LogError($"[HybridCLRHotUpdateDllCopyTool] 未找到热更新 DLL：{sourcePath}。请先执行 HybridCLR/Generate/All 或 CompileDll。");
            return;
        }

        Directory.CreateDirectory(TargetDirectory);
        File.Copy(sourcePath, targetPath, true);
        AssetDatabase.ImportAsset(targetPath);
        AssetDatabase.Refresh();

        Debug.Log($"[HybridCLRHotUpdateDllCopyTool] 已复制热更新 DLL：{sourcePath} -> {targetPath}");
    }
}
