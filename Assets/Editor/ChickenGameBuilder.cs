using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>CI 및 검증용 Windows 스탠드얼론 빌드.</summary>
public static class ChickenGameBuilder
{
    private const string ScenePath = "Assets/Scenes/ChickenGame.unity";

    [MenuItem("Chicken Game/Build Windows Player")]
    public static void BuildFromMenu()
    {
        Build("Builds/ChickenGame.exe");
    }

    /// <summary>-executeMethod 로 호출할 때는 -buildOutput 인자로 경로를 넘긴다.</summary>
    public static void BuildFromCommandLine()
    {
        string output = "Builds/ChickenGame.exe";
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "-buildOutput")
            {
                output = args[index + 1];
            }
        }

        Build(output);
    }

    private static void Build(string outputPath)
    {
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"Build {summary.result} -> {outputPath} ({summary.totalSize} bytes, {summary.totalErrors} errors)");

        if (summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }
}
