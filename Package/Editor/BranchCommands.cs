using System;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace MaterialDestruction.Editor
{
    internal static class BranchCommands
    {
        [CliGroupCommand("destruction","Describe the workbench materials and input units.", Effect=CommandEffect.ReadOnly)]
        internal static ExitCode Inspect(CommandContext cmd)
        {
            cmd.Result.Write(new { version=1, material="green wood", stepSeconds=BranchModel.StepSeconds,
                lengthUnit="metre", drive="signed normalized load [-1.6, 1.6], positive bends up",
                contact="fraction of length from clamp [0.12, 1]", example=JsonUtility.ToJson(new BranchTrial()),
                ceramic=new {cells=CeramicRim.Cells,radiusM=CeramicRim.Radius, impulseUnit="N s", command="destruction ceramic --file <result.json>"} });
            return ExitCode.Success;
        }

        [CliGroupCommand("destruction","Replay a branch trial and report broken fibers.", Effect=CommandEffect.ReadOnly)]
        internal static ExitCode Run(CommandContext cmd,
            [CliArg("trial","Short inline BranchTrial JSON.")] string trial="",
            [CliArg("file","Path to a saved trial on the editor machine.")] string file="")
        {
            if (string.IsNullOrWhiteSpace(trial)==string.IsNullOrWhiteSpace(file))
                throw new ArgumentException("Supply either --trial JSON or --file PATH.");
            if (!string.IsNullOrWhiteSpace(file)) trial=File.ReadAllText(file);
            var model = BranchTrial.Read(trial).Simulate();
            cmd.Result.Write(new { tick=model.Tick, detached=model.Detached, joint=model.CrackJoint,
                broken=model.Broken, events=model.Events.ToArray() });
            return ExitCode.Success;
        }

        [CliGroupCommand("destruction","Replay recorded ribbon grips and verify all release positions.", Effect=CommandEffect.ReadOnly)]
        internal static ExitCode Ribbon(CommandContext cmd,
            [CliArg("file","Path to the interactive ribbon-take.json.")] string file)
        {
            var take=JsonUtility.FromJson<RibbonTake>(File.ReadAllText(file));
            cmd.Result.Write(new {broken=take.Replay(),grips=take.grips});
            return ExitCode.Success;
        }

        [CliGroupCommand("destruction","Replay measured ceramic contacts and verify released rim IDs.", Effect=CommandEffect.ReadOnly)]
        internal static ExitCode Ceramic(CommandContext cmd,
            [CliArg("file","Path to a ceramic result.json on the editor machine.")] string file)
        {
            var result=JsonUtility.FromJson<CeramicResult>(File.ReadAllText(file));
            result.VerifyReplay();
            cmd.Result.Write(new {result.trial,result.missingCells,result.fragments,contacts=result.contacts.Length});
            return ExitCode.Success;
        }
    }
}
