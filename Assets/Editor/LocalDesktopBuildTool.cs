using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TheCall.Editor
{
    /// <summary>
    /// 将 Standalone 玩家包输出到桌面 monster_game1；每次打包前清空该目录，不包含 Editor/Library 缓存。
    /// </summary>
    public static class LocalDesktopBuildTool
    {
        const string OutputFolderName = "monster_game1";
        const string MenuPath = "TheCall/Build/打包到桌面 (monster_game1)";

        [MenuItem(MenuPath, false, 200)]
        public static void BuildToDesktop()
        {
            if (!EditorUtility.DisplayDialog(
                    "打包到桌面",
                    $"将在桌面创建或覆盖文件夹「{OutputFolderName}」，并生成全新的 Windows 64 位玩家包（不含项目缓存）。\n\n是否继续？",
                    "打包",
                    "取消"))
            {
                return;
            }

            try
            {
                BuildToDesktopInternal(showDialogs: true);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("打包失败", ex.Message, "确定");
            }
        }

        public static void BuildToDesktopInternal(bool showDialogs)
        {
            var scenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray();

            if (scenePaths.Length == 0)
            {
                throw new InvalidOperationException("Build Settings 中没有启用的场景，无法打包。");
            }

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktop))
            {
                throw new InvalidOperationException("无法解析当前用户的桌面路径。");
            }

            var outputRoot = Path.Combine(desktop, OutputFolderName);
            var productName = PlayerSettings.productName;
            var executablePath = Path.Combine(outputRoot, productName + ".exe");

            if (Directory.Exists(outputRoot))
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayProgressBar("打包到桌面", "正在清空旧输出…", 0.05f);
                }

                Directory.Delete(outputRoot, recursive: true);
            }

            Directory.CreateDirectory(outputRoot);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayProgressBar("打包到桌面", "切换 StandaloneWindows64…", 0.1f);
                }

                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                        BuildTargetGroup.Standalone,
                        BuildTarget.StandaloneWindows64))
                {
                    throw new InvalidOperationException("无法切换到 StandaloneWindows64 构建目标。");
                }
            }

            var previousDevelopment = EditorUserBuildSettings.development;
            EditorUserBuildSettings.development = false;

            var buildOptions = BuildOptions.CleanBuildCache;

            if (showDialogs)
            {
                EditorUtility.DisplayProgressBar("打包到桌面", "正在构建玩家包…", 0.35f);
            }

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(scenePaths, executablePath, BuildTarget.StandaloneWindows64, buildOptions);
            }
            finally
            {
                EditorUserBuildSettings.development = previousDevelopment;
                if (showDialogs)
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                var errors = report.steps
                    .SelectMany(step => step.messages)
                    .Where(message => message.type == LogType.Error)
                    .Select(message => message.content)
                    .Take(8)
                    .ToArray();

                var detail = errors.Length > 0 ? string.Join("\n", errors) : report.summary.result.ToString();
                throw new InvalidOperationException("BuildPipeline 未成功完成。\n" + detail);
            }

            Debug.Log($"[LocalDesktopBuild] 打包完成：{outputRoot}（{report.summary.totalSize} bytes）");
            EditorUtility.RevealInFinder(executablePath);

            if (showDialogs)
            {
                EditorUtility.DisplayDialog(
                    "打包完成",
                    $"输出目录：\n{outputRoot}",
                    "确定");
            }
        }
    }
}
