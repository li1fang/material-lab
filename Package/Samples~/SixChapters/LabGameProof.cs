using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MaterialDestruction.Sample;
using MaterialDestruction.Campus;

public sealed partial class LabGame
{
    void Require(bool success, string condition)
    {
        if (success)
        {
            Log("PASS " + condition);
            return;
        }
        Debug.LogError("LAB_PROOF_FAIL " + condition);
        Application.Quit(2);
        throw new InvalidOperationException(condition);
    }

    IEnumerator Until(Func<bool> condition, float seconds, string description)
    {
        float deadline = Time.unscaledTime + seconds;
        while (!condition() && Time.unscaledTime < deadline) yield return null;
        Require(condition(), description);
    }

    IEnumerator Shot(string directory, string name)
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG()); }
        finally { Destroy(image); }
    }

    void CheckTap(Vector2 pixel, string description)
    {
        Require(Eye.pixelRect.Contains(pixel), description + " in fixed view");
        BeginTap(pixel);
        EndTap(pixel);
    }

    IEnumerator Verify(string directory)
    {
        while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
        yield return new WaitForSecondsRealtime(.4f);
        Require(progress.chapter == 0 && progress.completed == 0, "fresh launch opens branch");
        yield return Shot(directory, "01-branch");
        var wood = (WoodChapter)chapter;
        CheckTap(wood.JointScreen(7), "select a near-root load point");
        yield return new WaitForSecondsRealtime(.45f);
        float nearTravel = wood.TipTravel;
        Require(Mathf.Abs(wood.Contact - 7f / 22) < .025f, "wood solver uses picked contact");
        wood.Unload();
        yield return new WaitForSecondsRealtime(.8f);
        CheckTap(wood.TipScreen, "start bend");
        yield return new WaitForSecondsRealtime(.45f);
        Require(wood.Contact > .95f && wood.TipTravel > nearTravel * 2, "same first load bends tip farther");
        for (int attempt = 0; attempt < 5 && wood.Broken == 0; attempt++)
        {
            CheckTap(wood.TipScreen, "more bend");
            yield return new WaitForSecondsRealtime(.36f);
        }
        Require(wood.Broken > 0 && !wood.Detached, "cracked branch still connected");
        int cracked = wood.Broken;
        wood.Unload();
        yield return new WaitForSecondsRealtime(.48f);
        Require(!wood.Detached && wood.Broken == cracked, "unloading preserves branch crack");
        yield return Shot(directory, "01-cracked");
        for (int attempt = 0; attempt < 5 && !wood.Detached; attempt++)
        {
            CheckTap(wood.TipScreen, "continue bending");
            yield return new WaitForSecondsRealtime(.42f);
        }
        yield return Until(() => wood.Complete, 4f, "branch separates");
        yield return null;
        Require(IsUnlocked(1), "porcelain unlocked");

        Switch(1);
        var ceramic = (CeramicChapter)chapter;
        yield return Shot(directory, "02-ceramic");
        Vector2 blackbar = new Vector2(2, 2);
        BeginTap(blackbar);
        EndTap(blackbar);
        Require(ceramic.Drops == 0, "letterbox tap ignored");
        CheckTap(ceramic.HeightScreen(0), "choose low mark on the ruler");
        CheckTap(ceramic.DiscScreen(0, 0), "porcelain center");
        CheckTap(ceramic.HeightScreen(2), "choose high mark for the next drop");
        CheckTap(ceramic.DiscScreen(0, 0), "queue high drop onto the same center");
        yield return Until(() => ceramic.Drops >= 1, 8f, "center ball settles");
        float lightImpulse = ceramic.LastPeakImpulseNs;
        yield return Until(() => ceramic.Drops >= 2, 8f, "queued high center drop settles");
        Require(lightImpulse > 0 && ceramic.LastPeakImpulseNs > lightImpulse,
            $"higher release produces greater measured impulse ({lightImpulse:F3} to {ceramic.LastPeakImpulseNs:F3})");
        CheckTap(ceramic.HeightScreen(1), "choose normal height");
        CheckTap(ceramic.DiscScreen(-.90f, .27f), "porcelain diagonal edge");
        yield return Until(() => ceramic.Drops >= 3, 8f, "off-axis hit settles");
        CheckTap(ceramic.DiscScreen(.88f, -.32f), "porcelain opposite edge");
        yield return Until(() => ceramic.Drops >= 4, 8f, "opposite ball settles");
        CheckTap(ceramic.DiscScreen(-.90f, .27f), "repeat porcelain opening");
        yield return Until(() => ceramic.Drops >= 5, 8f, "repeat drop accounted");
        Require(ceramic.Complete && ceramic.Missing > 0, "actual ceramic chips and completion");
        yield return Shot(directory, "02-chipped");

        Switch(2);
        var ribbon = (RibbonChapter)chapter;
        yield return Shot(directory, "03-ribbon");
        Vector2 ear = ribbon.EarScreen(1);
        Vector2 pull = ear + Vector2.up * (.115f * Eye.pixelHeight / (2 * Eye.orthographicSize));
        BeginTap(ear);
        MovePointer(pull);
        int held = ribbon.Broken;
        Require(held >= 21 && held < 58, "distance-driven drag tears ribbon partway");
        yield return new WaitForSecondsRealtime(.7f);
        Require(ribbon.IsPulling && ribbon.Broken == held, "stationary hand does not tear more");
        EndTap(pull);
        int tear = ribbon.Broken;
        Require(!ribbon.IsPulling && tear < 58, "released before full split");
        yield return Shot(directory, "03-half");
        progress = ReadProgress();
        Switch(2);
        ribbon = (RibbonChapter)chapter;
        yield return new WaitForSecondsRealtime(5f);
        Require(!ribbon.IsPulling && ribbon.Broken == tear, "reload preserves tear without pulling");
        ear = ribbon.EarScreen(-1);
        pull = ear + Vector2.down * (.052f * Eye.pixelHeight / (2 * Eye.orthographicSize));
        BeginTap(ear);
        MovePointer(pull);
        EndTap(pull);
        Require(!ribbon.IsPulling && ribbon.Broken > tear, "lower ear continues the same tear by dragging");
        CheckTap(ribbon.EarScreen(-1), "grip opposite ear to continue");
        yield return Until(() => ribbon.Complete, 12f, "split after two separate pulls");
        yield return Shot(directory, "03-split");

        yield return VerifyRemaining(directory);
        File.WriteAllLines(Path.Combine(directory, "events.txt"), events);
        File.WriteAllText(Path.Combine(directory, "proof-ok.txt"), progress.completed.ToString());
        Application.Quit();
    }

    IEnumerator VerifyRemaining(string directory)
    {
        Switch(3);
        yield return Shot(directory, "04-wall");
        yield return VerifyWall();
        yield return Shot(directory, "04-crossed");

        Switch(4);
        var parking = (ParkingChapter)chapter;
        yield return Shot(directory, "05-parking");
        var posts = new List<int>();
        var points = new List<Vector2>();
        bool undone = false;
        for (int clicks = 0; clicks < 90 && !parking.Complete; clicks++)
        {
            yield return Until(() => parking.IsSettled, 6, "parking settles");
            if (parking.Complete) break;
            Require(parking.GetVisiblePosts(posts, points) > 0, "parking has a reachable post");
            int next = posts.FindIndex(id => parking.WearAt(id) == 1);
            next = next < 0 ? 0 : next;
            int post = posts[next];
            int previous = parking.WearAt(post);
            CheckTap(points[next], "parking post " + post);
            yield return Until(() => parking.IsSettled, 6, "parking settles after tap");
            Require(parking.WearAt(post) == previous + 1, "parking applied clicked post");
            if (!undone && clicks >= 4)
            {
                Require(parking.UndoLast(), "parking undo");
                yield return Until(() => parking.IsSettled, 6, "parking settled after undo");
                Require(parking.WearAt(post) == previous, "parking undo restores state");
                undone = true;
            }
        }
        Require(undone && parking.Complete && parking.PanelCount == 15, "fixed-view 15-panel puzzle solved");
        yield return Shot(directory, "05-landed");

        Switch(5);
        yield return Shot(directory, "06-building");
        yield return VerifyBuilding(directory);
        yield return Shot(directory, "06-expanded");
        Require(progress.completed == 63, "six unlocked and completed");
    }

    IEnumerator VerifyWall()
    {
        var wall = (WallChapter)chapter;
        Require(wall.TryProjectApproach(0, out Vector2 marker), "wall approach target is visible");
        wall.SetGentle(true);
        CheckTap(marker, "drive toward intact wall");
        yield return Until(() => wall.StudyAmount(0) > .63f, 10, "default C offsets the connected bricks");
        Require(wall.Fragments == 0 && wall.ClearWidthAt(wall.LaneX) == 0, "C dent still obstructs the full vehicle");
        yield return Until(() => wall.DriveState == "Parked", 12, "light stroke returns without losing bricks");
        float[] residual = wall.StudySpan(0).ReadDepths();
        Require(Array.Exists(residual, d => d > .10f) && wall.Fragments == 0, "light stroke leaves intact stepped dent");
        Switch(3);
        wall = (WallChapter)chapter;
        var restored = wall.StudySpan(0).ReadDepths();
        for (int i = 0; i < residual.Length; i++)
            if (Mathf.Abs(restored[i] - residual[i]) > .00001f)
                throw new InvalidOperationException("Restored brick pose changed at chip " + i);
        Require(wall.Fragments == 0 && wall.TryProjectApproach(0, out marker), "reopened wall keeps dent and lane");
        CheckTap(marker, "continue pressing the retained dent");
        yield return Until(() => wall.BlockedCount > 0, 12, "intact wall blocks vehicle");
        Require(wall.ContactCount > 0 && wall.Fragments > 0 && !wall.Complete, "contact releases fragments before passage");
        for (int attempt = 0; attempt < 8 && !wall.Complete; attempt++)
        {
            yield return new WaitForSecondsRealtime(.45f);
            Require(wall.TryProjectApproach(0, out marker), "can try the same gap");
            CheckTap(marker, "drive toward damaged wall");
            float time = Time.unscaledTime + 6;
            int lastBlocked = wall.BlockedCount;
            while (Time.unscaledTime < time && !wall.Complete && wall.BlockedCount == lastBlocked) yield return null;
        }
        Require(wall.Complete && wall.TailZ >= wall.BackOfWallZ, "full vehicle traverses expanded gap");
    }

    IEnumerator VerifyBuilding(string directory)
    {
        var layout = (BuildingChapter)chapter;
        Require(layout.TestIds().Length > 30 && layout.TestLinks("post/1").Length >= 2, "two bays share original members");
        Require(layout.TryProofBrick(out Vector2 brick), "building wall brick exposed to fixed view");
        CheckTap(brick, "select real brick");
        CheckTap(brick, "detach real brick");
        Require(layout.TestApplyWall(), "apply wall profile using player controls");
        Require(layout.TestPostChange(), "change an independent connection");
        Require(layout.TestSharedChange(), "two shared posts receive changes on the same step");
        yield return new WaitForSecondsRealtime(.7f);
        yield return Shot(directory, "06-connection");
        Require(layout.Complete && layout.TestExtend(), "duplicate a bay with shared members");
        string selected = layout.TestSelection;
        string[] ids = layout.TestIds();
        progress = ReadProgress();
        Switch(5);
        layout = (BuildingChapter)chapter;
        Require(Array.TrueForAll(ids, id => Array.IndexOf(layout.TestIds(), id) >= 0) &&
            layout.TestSelection == selected && layout.Complete, "saved recipe restores building and selected member");
        yield return null;
    }
}
