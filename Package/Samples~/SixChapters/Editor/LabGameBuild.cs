using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class LabGameBuild
{
    public static void Windows()
    {
        string output = Environment.GetEnvironmentVariable("LAB_GAME_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("LAB_GAME_OUTPUT is required.");
        string assetDir = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath("05236afb8635d3f498eaef143d1944a2")).Replace('\\','/');
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetDir + "/MaterialLabPipeline.asset");
        if (!pipeline) throw new InvalidOperationException("Import the complete Six Chapters sample.");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        PlayerSettings.productName = "Material Lab - Six Chapters";
        PlayerSettings.companyName = "Material Lab";
        PlayerSettings.defaultScreenWidth = 1440;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Material Lab").AddComponent<LabGame>();
        string scenePath = assetDir + "/MaterialLabGame.unity";
        if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new IOException(scenePath);
        AssetDatabase.SaveAssets();
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { scenePath },
            target = BuildTarget.StandaloneWindows64,
            locationPathName = Path.Combine(output, "AI-RTS-Spectator.exe")
        });
        if (result.summary.result != BuildResult.Succeeded) throw new Exception(result.summary.result.ToString());
        Debug.Log("MATERIAL_LAB_GAME_BUILD_OK");
    }
}
