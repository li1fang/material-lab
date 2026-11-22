using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MaterialDestruction;

public static class BranchBuild
{
    static string Output => Environment.GetEnvironmentVariable("MATERIAL_LAB_OUTPUT")
        ?? throw new InvalidOperationException("Set MATERIAL_LAB_OUTPUT to an output directory.");

    public static void Verify()
    {
        var trialPath=Environment.GetEnvironmentVariable("MATERIAL_LAB_TRIAL");
        var trial=string.IsNullOrEmpty(trialPath) ? new BranchTrial() : BranchTrial.Read(File.ReadAllText(trialPath));
        var result=trial.Simulate();
        var proof=new BranchReport { tick=result.Tick, joint=result.CrackJoint, broken=result.Broken,
            detached=result.Detached, events=result.Events.ToArray() };
        File.WriteAllText(Path.Combine(Output,"simulation.json"),JsonUtility.ToJson(proof,true));
        string directory=Environment.GetEnvironmentVariable("MATERIAL_LAB_CERAMIC_PROOF");
        if(!string.IsNullOrWhiteSpace(directory)) VerifyCeramic(directory);
        string ribbonTake=Environment.GetEnvironmentVariable("MATERIAL_LAB_RIBBON_TAKE");
        if(!string.IsNullOrWhiteSpace(ribbonTake))
        {
            var take=JsonUtility.FromJson<RibbonTake>(File.ReadAllText(ribbonTake));
            Debug.Log("RIBBON_REPLAY_OK grips="+take.grips.Length+" broken="+take.Replay());
        }
        Debug.Log("MATERIAL_LAB_OK Verify");
    }
    static void VerifyCeramic(string directory)
    {
        CeramicResult Read(string trial)
        {
            var result=JsonUtility.FromJson<CeramicResult>(File.ReadAllText(Path.Combine(directory,trial,"result.json")));
            if(result.trial!=trial) throw new InvalidDataException("Unexpected trial "+result.trial);
            result.VerifyReplay();
            return result;
        }
        var light=Read("light-left");
        var left=Read("heavy-left");
        var right=Read("heavy-right");
        var hit=Read("wood-hit");
        var clear=Read("wood-clear");
        bool RightSource(CeramicResult r,string source) => r.contacts.Length>0 && r.contacts.All(c=>c.source==source);
        bool ChipOnSide(CeramicResult r,int sign) => RightSource(r,"striker") && r.missingCells>0 && r.fragments>0 && r.maxChipTravelM>.015f
            && r.contacts.Where(c=>c.released.Length>0).All(c=>sign*c.localPoint.x>0);
        if(!RightSource(light,"striker") || light.missingCells!=0 || !ChipOnSide(left,-1) || !ChipOnSide(right,1)
            || !RightSource(hit,"wood") || hit.detachStep<=0 || hit.contacts.Any(c=>c.step<hit.detachStep)
            || clear.detachStep!=hit.detachStep || clear.contacts.Length!=0 || clear.missingCells!=0)
            throw new InvalidDataException("Ceramic workbench comparison failed.");
        Debug.Log("CERAMIC_PROOF_OK");
    }
    public static void Windows()
    {
        const string path="Assets/BranchPipeline.asset";
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if(!pipeline)
        {
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer,"Assets/BranchRenderer.asset");
            pipeline=UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR=false;
            pipeline.msaaSampleCount=4;
            AssetDatabase.CreateAsset(pipeline,path);
        }
        GraphicsSettings.defaultRenderPipeline=pipeline;
        QualitySettings.renderPipeline=pipeline;
        PlayerSettings.productName="Material Lab";
        PlayerSettings.defaultScreenWidth=1100;
        PlayerSettings.defaultScreenHeight=680;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(Path.Combine(Output,"Windows"));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/BranchLab/RibbonWorkbench.unity","Assets/BranchLab/Workbench.unity"},
            locationPathName=Path.Combine(Output,"Windows","AI-RTS-Spectator.exe"),
            target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception(report.summary.result.ToString());
        Debug.Log("MATERIAL_LAB_OK Build");
    }
}
