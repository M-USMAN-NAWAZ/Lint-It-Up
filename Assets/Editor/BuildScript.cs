using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildScript
{
    private const string OutputPathArgument = "-androidOutputPath";

    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    private static string GetArgumentValue(string argumentName)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    public static void BuildAndroid()
    {
        var scenes = GetEnabledScenes();
        if (scenes.Length == 0)
        {
            throw new Exception("No enabled scenes found in Build Settings.");
        }

        string outputPath = GetArgumentValue(OutputPathArgument);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = "Builds/Android/Lint-It-Up.apk";
        }

        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new Exception($"Could not resolve Android output directory from {outputPath}");
        }

        Directory.CreateDirectory(outputDirectory);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
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
