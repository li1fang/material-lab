using System;
using System.Collections;
using System.IO;
using System.Linq;
using MaterialDestruction.Campus;
using UnityEngine;

public sealed partial class LabGame
{
    [Serializable] sealed class WallReading
    {
        public string variant;
        public float beforeContact, peak, after, clearance;
        public float[] peakChipDepths;
        public int releasedLight, releasedHeavy;
        public bool passed;
    }

    [Serializable] sealed class WallComparison
    {
        public WallReading[] walls = new WallReading[3];
        public bool fixedCamera, freshReset;
    }

    IEnumerator VerifyWallStudy(string directory)
    {
        while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
        var wall = (WallChapter)chapter;
        var receipt = new WallComparison();
        var cameraPosition = Eye.transform.position;
        var cameraRotation = Eye.transform.rotation;
        yield return Shot(directory, "00-intact");
        for (int lane = 0; lane < 3; lane++)
        {
            string variant = ((char)('A' + lane)).ToString();
            var read = receipt.walls[lane] = new WallReading { variant = variant };
            var span = wall.StudySpan(lane);
            if (lane == 0) wall.SetStudyHold(true);
            Require(wall.TryProjectApproach(lane, out var pixel), "green approach target visible " + variant);
            CheckTap(pixel, "light push " + variant);
            read.beforeContact = span.ReadDepths().Max();
            Require(read.beforeContact == 0, "no premature deformation " + variant);
            yield return Until(() => wall.StudyState == "Hold", 15, "loaded " + variant);
            read.peakChipDepths = span.ReadDepths();
            read.peak = read.peakChipDepths.Max();
            read.clearance = wall.StudyClearance;
            Require(read.peak > .24f && read.clearance >= 0 && read.clearance < .04f &&
                span.Missing == 0 && wall.ClearWidthAt(wall.LaneX) == 0, "intact deformed geometry stops crawler " + variant);
            yield return Shot(directory, variant + "-loaded");
            if (lane == 0)
            {
                yield return new WaitForSecondsRealtime(1.8f);
                Require(wall.StudyState == "Hold" && span.Missing == 0, "inspection holds light peak until released");
                wall.ReleaseStudyHold();
                wall.SetStudyHold(false);
            }
            yield return Until(() => wall.StudyState == "Ready", 12, "retreated " + variant);
            read.after = span.ReadDepths().Max();
            read.releasedLight = span.Missing;
            Require(read.after > .02f && read.after < read.peak && span.Missing == 0, "residual indentation with no loose bricks " + variant);
            yield return Shot(directory, variant + "-unloaded");
        }
        for (int i = 0; i < 3; i++)
            for (int j = i + 1; j < 3; j++)
            {
                var a = receipt.walls[i]; var b = receipt.walls[j];
                float difference = a.peakChipDepths.Zip(b.peakChipDepths,
                    (v, other) => Mathf.Abs(v / a.peak - other / b.peak)).Max();
                Require(difference > .18f, $"distinct spatial response {i}/{j}, normalized max difference {difference:F3}");
            }

        for (int lane = 0; lane < 3; lane++)
        {
            int target = lane;
            wall.RequestStudy(lane, true);
            yield return Until(() => wall.StudySpan(target).Missing > 0, 20, "strong stroke first release " + lane);
            yield return Until(() => wall.StudyState == "Through", 45, "full crawler clears fractured span " + lane);
            receipt.walls[lane].releasedHeavy = wall.StudySpan(lane).Missing;
            receipt.walls[lane].passed = wall.TailZ > wall.BackOfWallZ;
            Require(receipt.walls[lane].passed, "rear crosses former wall line " + lane);
            yield return Shot(directory, receipt.walls[lane].variant + "-through");
        }
        receipt.fixedCamera = Eye.transform.position == cameraPosition && Eye.transform.rotation == cameraRotation;
        Require(receipt.fixedCamera, "one fixed camera");
        Switch(3, true);
        wall = (WallChapter)chapter;
        receipt.freshReset = wall.Fragments == 0 && wall.StudyAmount(0) == 0 && wall.StudyState == "Ready";
        Require(receipt.freshReset, "reset restores three walls");
        File.WriteAllText(Path.Combine(directory, "verified.json"), JsonUtility.ToJson(receipt, true));
        Debug.Log("WALL_STUDY_VERIFIED");
        Application.Quit(0);
    }
}
