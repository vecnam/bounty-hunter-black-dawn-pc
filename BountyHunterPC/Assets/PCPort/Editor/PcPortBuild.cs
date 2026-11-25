using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Development build: Unity.exe -batchmode -quit -projectPath BountyHunterPC -executeMethod PcPortBuild.BuildWindowsDev
public static class PcPortBuild
{
    public static void BuildWindowsDev()
    {
        string[] baseScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (baseScenes.Length == 0)
            throw new System.Exception("EditorBuildSettings has no scenes");
        string gallery = PcPortGallery.EnsureGalleryScene();
        string[] scenes = baseScenes.Concat(new[] { gallery }).Distinct().ToArray();
        string exe = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/BountyHunter.exe"));
        BuildReport report = BuildPipeline.BuildPlayer(scenes, exe, BuildTarget.StandaloneWindows64, BuildOptions.Development);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("build failed: " + report.summary.result);
    }

    // Final build, the one you play: no Development (no error console or watermark), only the game's scenes,
    // straight into game/ with the README listing the keys. Saves stay in the same place (same product and company).
    public static void BuildWindowsRelease()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        BuildReport report = BuildPipeline.BuildPlayer(scenes, Path.Combine(root, "game/BountyHunter.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("final build failed: " + report.summary.result);
        File.Copy(Path.Combine(root, "docs/README.txt"), Path.Combine(root, "game/README.txt"), true);
    }
}
