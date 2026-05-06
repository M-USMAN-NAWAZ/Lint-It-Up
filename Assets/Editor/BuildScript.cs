using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildScript
{
    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    public static void BuildAndroid()
    {
        var scenes = GetEnabledScenes();
        if (scenes.Length == 0)
        {
            throw new Exception("No enabled scenes found in Build Settings.");
        }

        const string outputDirectory = "Builds/Android";
        const string outputFileName = "Lint-It-Up.apk";
        Directory.CreateDirectory(outputDirectory);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDirectory, outputFileName),
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"Android build failed: {report.summary.result}");
        }

        Console.WriteLine($"Android build completed: {options.locationPathName}");
    }
}
