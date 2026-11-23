using UnityEngine;

namespace MaterialDestruction.Campus
{
    internal sealed partial class WallChapter
    {
        static readonly string[] WallNames = { "A  整段缓弯", "B  局部压窝", "C  砖缝错台" };
        enum StudyPhase { Ready, TurnToLane, Approach, Load, Hold, Unload, Break, Exit, Through, Retreat }
        readonly WallStudySpan[] profiles = new WallStudySpan[3];
        StudyPhase phase;
        int selected = 1, requested = -1;
        bool heavy, requestedHeavy, holdLight;
        float phaseTime, startAmount, peakAmount, residual;
        const float LoadingTime = 1.15f;
        const float HoldTime = 1.45f;

        void InitializeStudy()
        {
            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = new WallStudySpan(spans[i], i, pitch - .38f);
            phase = StudyPhase.Ready;
            note = "点一面墙或它前面的绿色路标，轻推后车会退开。三面墙可以接着比。";
        }

        bool TapStudy(Transform hit)
        {
            for (int i = 0; i < profiles.Length; i++)
                if (hit.IsChildOf(spans[i].root) || hit.IsChildOf(pads[i]))
                {
                    RequestStudy(i, false);
                    return true;
                }
            return false;
        }

        internal void RequestStudy(int lane, bool pushThrough)
        {
            if ((uint)lane >= 3) return;
            requested = lane;
            requestedHeavy = pushThrough;
            if (phase == StudyPhase.Ready) StartRequestedStudy();
            else if (phase == StudyPhase.Through) ChangePhase(StudyPhase.Retreat);
            else if (phase == StudyPhase.Hold && holdLight && !heavy) ReleaseStudyHold();
            else note = $"本次结束后，小车将驶向 {WallNames[lane]}。";
        }

        void StartRequestedStudy()
        {
            selected = requested;
            heavy = requestedHeavy;
            requested = -1;
            aim = pads[selected].position.x;
            startAmount = profiles[selected].Amount;
            peakAmount = Mathf.Max(startAmount, heavy ? 1 : .86f);
            note = $"{WallNames[selected]}　·　{(heavy ? "持续顶推" : "轻推后退")}。";
            ChangePhase(StudyPhase.TurnToLane);
        }

        void ChangePhase(StudyPhase next)
        {
            phase = next;
            phaseTime = 0;
            speed = 0;
        }

        internal void ReleaseStudyHold()
        {
            if (phase != StudyPhase.Hold && phase != StudyPhase.Load) return;
            peakAmount = profiles[selected].Amount;
            residual = Mathf.Max(startAmount, peakAmount * profiles[selected].Retention);
            ChangePhase(StudyPhase.Unload);
        }

        float StopNose() => StopCentre(x, out _) + l;

        void FollowStudyFace()
        {
            float target = Mathf.Min(finish, StopNose() - l);
            float travel = target - z;
            z = target;
            Place(travel);
        }

        void PoseStudyWalls()
        {
            foreach (var profile in profiles) profile.Pose(age);
            Physics.SyncTransforms();
            RefreshBoxes();
        }

        void TickStudy(float dt)
        {
            phaseTime += dt;
            var active = profiles[selected];
            switch (phase)
            {
                case StudyPhase.Ready:
                case StudyPhase.Through:
                    return;
                case StudyPhase.TurnToLane:
                    float dx = aim - x;
                    float heading = Mathf.Abs(dx) > .0001f ? 90 * Mathf.Sign(dx) : 0;
                    yaw = Mathf.MoveTowards(yaw, heading, 240 * dt);
                    float travel = 0;
                    if (yaw == heading && heading != 0) travel = Mathf.Abs(Roll(ref x, aim, dt));
                    Place(travel);
                    if (heading == 0 && yaw == 0) ChangePhase(StudyPhase.Approach);
                    break;
                case StudyPhase.Approach:
                    float target = StopCentre(x, out var obstacle);
                    if (obstacle == null) { ChangePhase(StudyPhase.Exit); break; }
                    Place(Roll(ref z, target, dt));
                    if (z == target)
                    {
                        contacts++;
                        note = $"{WallNames[selected]}　·　车头抵住砖面，留意墙顶和砖缝。";
                        ChangePhase(StudyPhase.Load);
                    }
                    break;
                case StudyPhase.Load:
                    active.Amount = Mathf.SmoothStep(startAmount, peakAmount, phaseTime / LoadingTime);
                    PoseStudyWalls();
                    FollowStudyFace();
                    if (phaseTime >= LoadingTime)
                    {
                        ChangePhase(StudyPhase.Hold);
                        note = heavy ? "车头正在持续施压。" : holdLight ?
                            "现在可以慢慢看。点「卸力」观察回弹，也可以直接选下一面墙。" :
                            "墙面已压到最深处，即将卸力。点「保压观察」可以把这一刻留住。";
                    }
                    break;
                case StudyPhase.Hold:
                    if (!heavy && holdLight && requested < 0) break;
                    if (phaseTime >= HoldTime)
                    {
                        if (heavy) ChangePhase(StudyPhase.Break);
                        else ReleaseStudyHold();
                    }
                    break;
                case StudyPhase.Unload:
                    active.Amount = Mathf.SmoothStep(peakAmount, residual, phaseTime / .70f);
                    PoseStudyWalls();
                    BackAway(dt);
                    if (phaseTime >= .70f) ChangePhase(StudyPhase.Retreat);
                    break;
                case StudyPhase.Break:
                    ReleaseContactChips(active, dt);
                    PoseStudyWalls();
                    AdvanceToGap(dt);
                    break;
                case StudyPhase.Exit:
                    PoseStudyWalls();
                    AdvanceToGap(dt);
                    break;
                case StudyPhase.Retreat:
                    BackAway(dt);
                    if (z == home)
                    {
                        ChangePhase(StudyPhase.Ready);
                        note = "可以点另一面墙作比较，或点「顶破这面」沿原位继续。";
                        if (requested >= 0) StartRequestedStudy();
                    }
                    break;
            }
        }

        void BackAway(float dt)
        {
            float before = z;
            Roll(ref z, home, dt);
            // The rebounding face moves towards the vehicle during unloading.
            z = Mathf.Min(z, StopCentre(x, out _));
            Place(z - before);
        }

        void ReleaseContactChips(WallStudySpan active, float dt)
        {
            // Pair-level deformation already happened. Only a still-attached chip that
            // meets the current front footprint takes fresh wear; the original sample
            // propagates loss of support and supplies the release time for each chip.
            foreach (var p in parts)
            {
                if (p.span != selected || active.Released(p.chip) || !InTrack(p.box, x)) continue;
                if (p.box.min.z - NoseZ > Skin + .04f) continue;
                float edge = Mathf.Clamp01(Mathf.Abs(p.box.center.x - x) / (w + Skin));
                p.wear += dt * Mathf.Lerp(2.8f, 1.7f, edge);
                if (p.wear >= p.strength) spans[selected].Hit(p.seat, ChipStrength, age);
            }
        }

        void AdvanceToGap(float dt)
        {
            float limit = StopCentre(x, out var obstacle);
            float target = Mathf.Min(finish, limit);
            float before = z;
            if (z > target) z = target;
            else Roll(ref z, target, dt);
            Place(z - before);
            if (obstacle == null && z == finish)
            {
                ChangePhase(StudyPhase.Through);
                note = "车尾已经过墙。点另一面墙，小车会沿缺口退出再换道。";
            }
        }

        void StudyPanel()
        {
            if (ActionButton("轻推左墙 A")) RequestStudy(0, false);
            if (ActionButton("轻推中墙 B")) RequestStudy(1, false);
            if (ActionButton("轻推右墙 C")) RequestStudy(2, false);
            if (ActionButton("顶破这面")) RequestStudy(selected, true);
            if (ActionButton(holdLight ? "保压观察 ✓" : "保压观察")) SetStudyHold(!holdLight);
            if (ActionButton("卸力")) ReleaseStudyHold();
        }

        void StudyOverlay()
        {
            for (int i = 0; i < profiles.Length; i++)
                Cue(Eye.WorldToScreenPoint(spans[i].root.position + Vector3.up * 2.34f),
                    WallNames[i], new Vector2(-50, -34));
        }

        internal string StudyState => phase.ToString();
        internal float StudyAmount(int lane) => profiles[lane].Amount;
        internal float StudyClearance => StopNose() - NoseZ + Skin;
        internal WallStudySpan StudySpan(int lane) => profiles[lane];
        internal void SetStudyHold(bool on) => holdLight = on;
    }
}
