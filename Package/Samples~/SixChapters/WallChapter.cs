using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    internal sealed partial class WallChapter : LabChapter
    {
        // The clock belongs to the scene, so neither simulation nor input depends
        // on how often the host draws the footer or the IMGUI overlay.
        enum Motion { Parked, Returning, Aligning, Driving, Pressing, Passed }

        const float Step = 1f / 60f;
        const float Skin = .028f;
        const float Stroke = 1.22f;
        const float ChipStrength = .025f;
        const float Kick = .065f;

        sealed class Part
        {
            public Collider collider;
            public Bounds box;
            public int span;                 // -1: permanent column
            public int chip;
            public Vector2 seat;             // original chip centre in span coordinates
            public float wear, strength;
            public bool struck, treadable;
        }

        [Serializable]
        struct Impression
        {
            public int span;
            public Vector2 seat;
        }

        [Serializable] struct Imprint { public int span; public float localX, amount; }
        readonly List<Imprint> imprints = new List<Imprint>();
        int pressedSpan = -1;
        float preload;
        bool gentle;

        [Serializable]
        sealed class Notebook
        {
            public int version;
            public float x, z, yaw, aim, halfWidth, halfLength;
            public bool finished;
            public Impression[] impressions;
            public float[] wear;
            public Imprint[] dents;
        }

        readonly List<BoundarySpan> spans = new List<BoundarySpan>();
        readonly List<Part> parts = new List<Part>();
        readonly List<Impression> impressions = new List<Impression>();
        readonly Transform[] pads = new Transform[3];
        readonly Transform[] columns = new Transform[4];

        CampusKit kit;
        Transform yard;
        ToyLabVehicle toy;
        Quaternion straight;
        WallChapterClock clock;
        Motion motion;

        float w, l, pitch, home, finish, farFace, x, z, yaw, aim;
        float speed, cruise, brake, load, age, animateUntil, carry;
        bool queued, touched, won;
        int contacts, blocked;
        string note = "";

        public override bool Complete => won;
        public override string Hint => won ? "整辆履带车已经通过砖墙缺口。" : note;

        // Proof/readback coordinates are world units; projected points are
        // bottom-left-origin screen pixels, directly suitable for Tap().
        internal string DriveState => motion.ToString();
        internal string LastNotice => note;
        internal float LaneX => x;
        internal float SelectedX => aim;
        internal float NoseZ => z + l;       // longitudinal readings when facing the wall
        internal float TailZ => z - l;
        internal float BackOfWallZ => farFace;
        internal int ContactCount => contacts;
        internal int BlockedCount => blocked;
        internal void SetGentle(bool on) => gentle = on;
        internal int Fragments
        {
            get
            {
                int n = 0;
                foreach (var s in spans) n += s.Missing;
                return n;
            }
        }

        internal float ClearWidthAt(float lane)
        {
            if (!yard || Mathf.Abs(lane) >= 1.5f * pitch) return 0;
            float left = -1.5f * pitch, right = -left;
            foreach (var p in parts)
            {
                if (p.treadable) continue;
                var b = p.box;
                if (b.min.x <= lane && lane <= b.max.x) return 0;
                if (b.max.x < lane) left = Mathf.Max(left, b.max.x);
                if (b.min.x > lane) right = Mathf.Min(right, b.min.x);
            }
            return Mathf.Max(0, right - left);
        }

        protected override void Open()
        {
            RemoveScene();
            won = queued = touched = false;
            speed = load = age = carry = 0;
            animateUntil = -1;
            contacts = blocked = 0;
            motion = Motion.Parked;
            note = "选一块砖，或点路上的圆形路标。";

            kit = new CampusKit();
            yard = CampusKit.Node("手作工坊 · 砖墙小径", transform, Vector3.zero);
            toy = new ToyLabVehicle(yard);
            w = toy.HalfWidth;
            l = toy.HalfLength;
            if (!toy.Root || !(w > 0 && l > 0))
            {
                RemoveScene();
                throw new InvalidOperationException("ToyLabVehicle needs a root and positive footprint extents.");
            }
            straight = toy.Root.localRotation;

            float turnRadius = Mathf.Sqrt(w * w + l * l);
            pitch = IsWallStudy ? 4.12f : Mathf.Max(2.95f, 3.25f * w + 1.05f);
            home = -.36f - 2 * turnRadius - 1.12f;
            cruise = Mathf.Max(2.35f, (Mathf.Abs(home) + l) / 1.85f);
            brake = cruise * 2.9f;

            BuildWall();
            Physics.SyncTransforms();
            RefreshBoxes();
            farFace = 0;
            foreach (var p in parts) farFace = Mathf.Max(farFace, p.box.max.z);
            finish = farFace + l + Mathf.Max(.95f, l * .48f);

            float halfBoard = 1.5f * pitch + turnRadius + .68f;
            float near = home - turnRadius - .85f;
            float far = finish + turnRadius + .85f;
            BuildPath(halfBoard, near, far);
            x = aim = 0;
            z = home;
            yaw = 0;
            if (IsWallStudy) InitializeStudy();
            else
            {
                for (int i = 0; i < spans.Count; i++) profiles[i] = new WallStudySpan(spans[i], 2, pitch - .38f);
                RestoreNotebook();
            }
            Place(0);
            FrameBoard(halfBoard, near, far);
            Physics.SyncTransforms();

            clock = yard.gameObject.AddComponent<WallChapterClock>();
            clock.Owner = this;
        }

        void BuildWall()
        {
            for (int i = 0; i < columns.Length; i++)
            {
                var column = columns[i] = kit.Pillar(yard, i,
                    new Vector3((i - 1.5f) * pitch, 0, 0), 2.08f);

                // Include the visible cap and trim as well as the original foot
                // and casting. Aiming at a post never makes it destructible.
                foreach (var r in column.GetComponentsInChildren<MeshRenderer>())
                {
                    Collider c = r.GetComponent<Collider>();
                    if (!c || !c.enabled) c = r.gameObject.AddComponent<BoxCollider>();
                    parts.Add(new Part { collider = c, span = -1 });
                }
            }

            for (int i = 0; i < pads.Length; i++)
            {
                var s = new BoundarySpan(kit, yard, columns[i].localPosition,
                    columns[i + 1].localPosition, false, IsWallStudy ? 17 : i + 17);
                spans.Add(s);
                int chip = 0;
                foreach (var c in s.root.GetComponentsInChildren<MeshCollider>())
                {
                    Vector2 seat = c.transform.localPosition;
                    parts.Add(new Part
                    {
                        collider = c,
                        span = i,
                        chip = chip++,
                        seat = seat,
                        strength = .99f + .24f * Mathf.Clamp01(seat.y / 1.95f)
                            + .075f * Mathf.Repeat(parts.Count * .618034f, 1)
                    });
                }
            }
        }

        void BuildPath(float halfBoard, float near, float far)
        {
            var baseNode = CampusKit.Node("Cutaway base", yard,
                new Vector3(0, -.055f, (near + far) * .5f));
            kit.Base(baseNode, halfBoard * 2, far - near);
            float roadWidth = 3 * pitch + .76f;
            kit.Solid(yard, "Warm cork path", new Vector3(0, -.029f, (near + far) * .5f),
                new Vector3(roadWidth, .058f, far - near - .18f), "C8BC9F", true);

            for (int side = -1; side <= 1; side += 2)
            {
                float edge = side * (roadWidth * .5f - .09f);
                kit.Solid(yard, "Cream selvedge", new Vector3(edge, .003f, (near + far) * .5f),
                    new Vector3(.085f, .006f, far - near - .26f), "FFF1CB");
                float borderWidth = halfBoard - roadWidth * .5f - .12f;
                kit.Solid(yard, "Sage felt border",
                    new Vector3(side * (halfBoard - borderWidth * .5f - .06f), -.032f, (near + far) * .5f),
                    new Vector3(borderWidth, .04f, far - near - .20f), "AABE9B");
            }

            float padZ = -Mathf.Max(.88f, Mathf.Min(-home * .35f, l + .72f));
            float diameter = Mathf.Clamp(pitch * .36f, .78f, 1.52f);
            for (int i = 0; i < pads.Length; i++)
            {
                float lane = (i - 1) * pitch;
                for (float at = near + .56f; at < padZ - diameter * .65f; at += .74f)
                    kit.Solid(yard, "Stitched lane", new Vector3(lane, .004f, at),
                        new Vector3(.074f, .008f, .33f), "F6E6BF");

                var pad = pads[i] = kit.Solid(yard, "Approach button " + (i + 1),
                    new Vector3(lane, .009f, padZ), new Vector3(diameter, .009f, diameter),
                    "51988C", true, PrimitiveType.Cylinder);
                // Build the inlay in yard units, not in the scaled disc's frame.
                for (int side = -1; side <= 1; side += 2)
                {
                    var stitch = kit.Solid(yard, "Arrow inlay",
                        new Vector3(lane + side * diameter * .106f, .020f, padZ),
                        new Vector3(diameter * .065f, .004f, diameter * .33f), "FFF0BF");
                    stitch.localRotation = Quaternion.Euler(0, -side * 42, 0);
                }
                pad.name = "Mint approach target " + (i + 1);
            }
            kit.Solid(yard, "Far hem", new Vector3(0, .003f, far - .45f),
                new Vector3(roadWidth - .4f, .006f, .10f), "F4E3B6");
        }

        void FrameBoard(float halfBoard, float near, float far)
        {
            Vector3 euler = new Vector3(38, -12, 0);
            Quaternion q = Quaternion.Euler(euler);
            Vector3 up = q * Vector3.up, right = q * Vector3.right;
            float top = 2.3f;
            foreach (var r in toy.Root.GetComponentsInChildren<Renderer>())
                top = Mathf.Max(top, r.bounds.max.y + .14f);
            var focus = new Vector3(0, (top - .8f) * .5f, (near + far) * .5f);
            var ext = new Vector3(halfBoard, (top + .8f) * .5f, (far - near) * .5f);
            float Radius(Vector3 axis) => Mathf.Abs(axis.x) * ext.x +
                Mathf.Abs(axis.y) * ext.y + Mathf.Abs(axis.z) * ext.z;

            Rect pixels = Eye.pixelRect;
            float bottom = Mathf.Max(pixels.yMin, Screen.height * (108f / 900));
            float upper = Mathf.Min(pixels.yMax, Screen.height * (792f / 900));
            float fraction = Mathf.Clamp((upper - bottom) / Mathf.Max(1, pixels.height), .1f, 1);
            float size = 1.055f * Mathf.Max(Radius(up) / fraction,
                Radius(right) / Mathf.Max(.1f, Eye.aspect));
            if (IsWallStudy) size *= .74f; // Frame the bricks and all three vehicle routes.
            focus -= up * ((upper + bottom - 2 * pixels.center.y) / Mathf.Max(1, pixels.height) * size);
            View(focus, euler, size, Mathf.Max(16, ext.magnitude * 2 + 5));
        }

        bool ScenePixel(Vector2 screen)
        {
            float guiY = (Screen.height - screen.y) * 900f / Mathf.Max(1, Screen.height);
            return Eye && Eye.pixelRect.Contains(screen) && guiY >= 104 && guiY < 792;
        }

        bool Pick(Vector2 screen, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            if (!yard || !ScenePixel(screen)) return false;
            Physics.SyncTransforms();
            return Physics.Raycast(Eye.ScreenPointToRay(screen), out hit, Eye.farClipPlane,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public override bool Tap(Vector2 screen)
        {
            if (!Pick(screen, out var hit) || won) return false;
            if (IsWallStudy) return TapStudy(hit.transform);
            for (int i = 0; i < pads.Length; i++)
                if (hit.transform.IsChildOf(pads[i])) return Command(pads[i].position.x);
            foreach (var s in spans)
                if (hit.transform.IsChildOf(s.root)) return Command(hit.point.x);
            foreach (var column in columns)
                if (hit.transform.IsChildOf(column)) return Command(hit.point.x);
            return false;
        }

        bool Project(Vector3 world, out Vector2 screen)
        {
            var point = Eye.WorldToScreenPoint(world);
            screen = new Vector2(point.x, point.y);
            return point.z > 0 && ScenePixel(screen);
        }

        internal bool TryProjectApproach(int lane, out Vector2 screen)
        {
            screen = default(Vector2);
            if (!yard || lane < 0 || lane >= pads.Length) return false;
            var t = pads[lane];
            // A corner of a disc can still be visible when its centre is hidden.
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * .25f, r = i == 0 ? 0 : .32f;
                var p = t.TransformPoint(new Vector3(r * Mathf.Cos(a), 1, r * Mathf.Sin(a)));
                if (Project(p, out screen) && Pick(screen, out var h) && h.transform.IsChildOf(t))
                    return true;
            }
            return false;
        }

        internal bool TryProjectWall(float laneX, out Vector2 screen)
        {
            screen = default(Vector2);
            if (!yard) return false;
            float best = float.PositiveInfinity;
            foreach (var p in parts)
            {
                if (p.span < 0 || p.struck || profiles[p.span].Released(p.chip)) continue;
                var front = p.collider.transform.TransformPoint(new Vector3(0, 0, -.115f));
                float score = Mathf.Abs(front.x - laneX) + .13f * Mathf.Abs(front.y - 1.05f);
                if (score >= best || !Project(front, out var px)) continue;
                if (!Pick(px, out var h) || h.collider != p.collider) continue;
                best = score;
                screen = px;
            }
            return !float.IsPositiveInfinity(best);
        }

        bool Command(float lane)
        {
            if (!yard || won) return false;
            lane = Mathf.Clamp(lane, -1.5f * pitch, 1.5f * pitch);
            // Once the nose is THROUGH the face, finish on this line. In
            // particular, never slide sideways through the jamb of a real gap.
            if (NoseZ > -.113f && Mathf.Abs(lane - x) > .008f)
            {
                note = "车头已进缺口；点「再推一次」沿原线通过。";
                return false;
            }
            bool same = Mathf.Abs(lane - aim) <= .008f;
            if (same && (motion == Motion.Driving || motion == Motion.Pressing)) return true;
            aim = lane;
            queued = true;
            if (motion == Motion.Returning) return true;

            if (Mathf.Abs(x - aim) <= .008f && Mathf.Abs(yaw) < .01f)
                BeginStroke();
            else
            {
                FinishDent();
                speed = 0;
                motion = z > home + .001f ? Motion.Returning : Motion.Aligning;
                note = "在起步区对准落点。";
            }
            return true;
        }

        void BeginStroke()
        {
            FinishDent();
            preload = 0;
            queued = touched = false;
            speed = load = 0;
            motion = Motion.Driving;
            note = "慢慢靠近砖面。";
        }

        void Park()
        {
            if (!yard || won) return;
            FinishDent();
            queued = false;
            speed = 0;
            motion = Motion.Parked;
            note = "已停车；砖面的磨损保留。";
            WriteNotebook();
        }

        void RefreshBoxes()
        {
            foreach (var p in parts)
            {
                p.box = p.collider.bounds;
                // A standing bottom-course half brick is low too: height alone
                // must NOT turn it into a ramp. Only flat, low rubble is rideable.
                p.treadable = p.span >= 0 && p.box.max.y < .29f &&
                    Mathf.Abs(p.collider.transform.up.y) < .14f;
            }
        }

        void PoseWall()
        {
            foreach (var profile in profiles) profile.Pose(age);
            Physics.SyncTransforms();
            RefreshBoxes();
        }

        void FinishDent()
        {
            if (pressedSpan < 0) return;
            var face = profiles[pressedSpan];
            float retained = face.Amount * face.Retention;
            imprints.Add(new Imprint { span = pressedSpan, localX = face.ContactX, amount = retained });
            face.Retain(face.ContactX, retained);
            pressedSpan = -1;
            PoseWall();
            float old = z;
            z = Mathf.Min(z, StopCentre(x, out _));
            Place(z - old);
        }

        bool InTrack(Bounds b, float lane)
        {
            return b.max.x >= lane - w - Skin && b.min.x <= lane + w + Skin;
        }

        // Conservative longitudinal sweep of the FULL footprint. For standing
        // pieces these bounds are the x and z projections of the actual prism.
        // A tiny edge sliver, a delayed upper course, or a post can stop the run.
        float StopCentre(float lane, out Part nearest)
        {
            nearest = null;
            float limit = float.PositiveInfinity;
            foreach (var p in parts)
            {
                if (p.treadable || !InTrack(p.box, lane)) continue;
                float front = p.box.min.z - Skin - l;
                if (front < limit) { limit = front; nearest = p; }
            }
            return limit;
        }

        void Place(float signedTravel)
        {
            float lift = .024f;
            // While turning, the entire swept circle is in the clear staging
            // area. In the wall corridor the heading is always exactly +z.
            if (Mathf.Abs(yaw) < .01f)
            {
                const float ramp = .30f;
                foreach (var p in parts)
                {
                    if (!p.treadable) continue;
                    var b = p.box;
                    float dx = Mathf.Max(b.min.x - (x + w), x - w - b.max.x);
                    float dz = Mathf.Max(b.min.z - (z + l), z - l - b.max.z);
                    float d = Mathf.Max(0, Mathf.Max(dx, dz));
                    lift = Mathf.Max(lift, (b.max.y + .012f) * Mathf.Clamp01(1 - d / ramp));
                }
            }
            toy.Pose(new Vector3(x, lift, z), signedTravel);
            toy.Root.localRotation = Quaternion.Euler(0, yaw, 0) * straight;
        }

        // Brake from remaining free distance, then clamp the final substep.
        // Never advance by a nominal animation stroke through a collider.
        float Roll(ref float coordinate, float target, float dt)
        {
            float before = coordinate;
            float distance = Mathf.Abs(target - before);
            float request = Mathf.Min(cruise, Mathf.Sqrt(2 * brake * distance));
            speed = Mathf.MoveTowards(speed, request, dt * (speed > request ? brake : .67f * brake));
            coordinate = Mathf.MoveTowards(before, target, speed * dt);
            if (coordinate == target) speed = 0;
            return coordinate - before;
        }

        void Press(float dt)
        {
            // Stored fatigue belongs to each fired-clay part, not to a lane
            // number or a pass counter. Load is strongest at the centre; higher
            // courses receive less effective shear. BoundarySpan itself decides
            // which neighbouring pieces lose their underlying support.
            float squeeze = Mathf.Lerp(.52f, 1, Mathf.Clamp01(load / .26f));
            int strikes = 0;
            foreach (var p in parts)
            {
                if (p.span < 0 || p.struck || !InTrack(p.box, x)) continue;
                if (profiles[p.span].Released(p.chip)) continue;
                float reach = p.box.min.z - NoseZ;
                if (reach < -.003f || reach > Skin + .018f) continue;

                float lateral = Mathf.Clamp01(Mathf.Abs(p.box.center.x - x) / (w + Skin));
                // A stepped joint can be the first part to touch near either track.
                float rate = Mathf.Lerp(2.8f, 1.7f, Mathf.SmoothStep(0, 1, lateral));
                p.wear += dt * rate * squeeze;
                if (p.wear < p.strength || strikes >= 2) continue;
                p.struck = true;
                strikes++;
                var wall = spans[p.span];
                int before = wall.Missing;
                wall.Hit(p.seat, ChipStrength, age, Kick);
                if (wall.Missing == before) continue;

                if (impressions.Count == 0)
                {
                    note = "第一片砖松开了。";
                    Debug.Log($"WALL_FIRST_FRAGMENT first fragment: span={p.span}, seat={p.seat}");
                }
                impressions.Add(new Impression { span = p.span, seat = p.seat });
                animateUntil = age + 1.40f;
            }
        }

        void Forward(float dt)
        {
            float limit = StopCentre(x, out var obstacle);
            float target = Mathf.Min(finish, limit);
            float travel;
            if (target < z)
            {
                // A rotating fragment can extend a little farther toward the
                // nose on this tick. Yield to its new visible envelope.
                travel = target - z;
                z = target;
                speed = 0;
            }
            else travel = Roll(ref z, target, dt);
            Place(travel);

            if (obstacle == null && z >= finish && TailZ > farFace + Skin)
            {
                FinishDent();
                won = true;
                motion = Motion.Passed;
                note = "通过！车尾也越过了墙线。";
                Debug.Log($"WALL_VEHICLE_PASSED vehicle passed: tail={TailZ:F3}, far face={farFace:F3}");
                WriteNotebook();
                Result(note);
                return;
            }

            if (obstacle == null || z < limit - .0001f)
            {
                motion = Motion.Driving;
                return;
            }

            motion = Motion.Pressing;
            if (!touched)
            {
                touched = true;
                contacts++;
                if (obstacle.span >= 0 && !profiles[obstacle.span].Released(obstacle.chip))
                {
                    pressedSpan = obstacle.span;
                    profiles[pressedSpan].ContactX = x - spans[pressedSpan].root.position.x;
                }
                note = "接触砖面，履带减速施压。";
                Debug.Log($"WALL_CONTACT contact before visible overlap: nose={NoseZ:F3}, " +
                    $"face={obstacle.box.min.z:F3}, clearance={obstacle.box.min.z - NoseZ:F3}");
            }
            if (pressedSpan >= 0 && preload < 1.4f)
            {
                preload += dt;
                profiles[pressedSpan].Amount = Mathf.SmoothStep(0, gentle ? .86f : 1, preload / 1.05f);
                PoseWall();
                float before = z;
                z = Mathf.Min(finish, StopCentre(x, out _));
                Place(z - before);
                if (preload >= 1.4f && gentle)
                {
                    FinishDent();
                    motion = Motion.Returning;
                    queued = false;
                    note = "轻推结束，砖缝留下错台。可以再推或换个位置。";
                    WriteNotebook();
                }
                return;
            }
            if (load < Stroke)
            {
                float slice = Mathf.Min(dt, Stroke - load);
                load += slice;
                Press(slice);
                return;
            }

            // Let moving pieces finish tipping. Time can end a push, but can
            // never grant passage: the next forward tick still uses StopCentre.
            if (age <= animateUntil) return;
            blocked++;
            FinishDent();
            note = obstacle.span < 0 ? "立柱仍挡住履带；试试旁边的砖面。" : "缺口仍窄。退回后可沿原线再推。";
            Debug.Log($"WALL_STILL_BLOCKED still blocked: {obstacle.collider.name}, " +
                $"gap={ClearWidthAt(x):F3}, toy width={2 * w:F3}");
            queued = false;
            motion = Motion.Returning;
            speed = 0;
            WriteNotebook();
        }

        void Tick(float dt)
        {
            age += dt;
            if (IsWallStudy) { TickStudy(dt); return; }
            if (age - dt <= animateUntil) PoseWall();

            switch (motion)
            {
                case Motion.Driving:
                case Motion.Pressing:
                    Forward(dt);
                    break;

                case Motion.Returning:
                    Place(Roll(ref z, home, dt));
                    if (z == home) motion = queued ? Motion.Aligning : Motion.Parked;
                    break;

                case Motion.Aligning:
                    // At home even the corners of a pivoting vehicle clear the
                    // post feet. Turn, travel, and straighten instead of strafing.
                    float dx = aim - x;
                    float heading = Mathf.Abs(dx) > .0001f ? 90 * Mathf.Sign(dx) : 0;
                    yaw = Mathf.MoveTowards(yaw, heading, 175 * dt);
                    float distance = 0;
                    if (yaw == heading && heading != 0) distance = Mathf.Abs(Roll(ref x, aim, dt));
                    Place(distance);
                    if (heading == 0 && yaw == 0) BeginStroke();
                    break;

                case Motion.Parked:
                    if (age - dt <= animateUntil) Place(0);
                    break;
            }
        }

        internal void AdvanceClock(float seconds)
        {
            if (!yard || won) return;
            carry += Mathf.Clamp(seconds, 0, .12f);
            while (carry >= Step && yard && !won)
            {
                carry -= Step;
                Tick(Step);
            }
        }

        public override void Panel()
        {
            if (IsWallStudy) { StudyPanel(); return; }
            if (ActionButton(gentle ? "轻推后退 ✓" : "轻推后退")) SetGentle(!gentle);
            if (ActionButton("停车")) Park();
            if (ActionButton("再推一次") && yard && !won) Command(aim);
        }

        public override void Overlay()
        {
            if (!yard) return;
            if (IsWallStudy) { StudyOverlay(); return; }
            string state;
            switch (motion)
            {
                case Motion.Returning: state = "退回起步区"; break;
                case Motion.Aligning: state = "对准路标"; break;
                case Motion.Driving: state = "沿线前进"; break;
                case Motion.Pressing: state = load < Stroke ? "砖面受压" : "砖片落定中"; break;
                case Motion.Passed: state = "车尾已通过"; break;
                default: state = "已停车"; break;
            }
            Cue(new Vector2(Eye.pixelRect.xMin + Eye.pixelWidth * .018f, Eye.pixelRect.yMax - 12),
                $"{state}   ·   脱落 {Fragments} 片", Vector2.zero);
        }

        void WriteNotebook()
        {
            if (!yard) return;
            var n = new Notebook
            {
                version = 1, x = x, z = z, yaw = yaw, aim = aim,
                halfWidth = w, halfLength = l, finished = won,
                impressions = impressions.ToArray(), wear = new float[parts.Count], dents = imprints.ToArray()
            };
            for (int i = 0; i < parts.Count; i++) n.wear[i] = parts[i].wear;
            Save(JsonUtility.ToJson(n));
        }

        void RestoreNotebook()
        {
            if (string.IsNullOrEmpty(Saved)) return;
            Notebook n;
            try { n = JsonUtility.FromJson<Notebook>(Saved); }
            catch (ArgumentException) { return; }
            if (n == null || n.version != 1 || n.wear == null || n.wear.Length != parts.Count ||
                n.impressions == null || !Mathf.Approximately(w, n.halfWidth) ||
                !Mathf.Approximately(l, n.halfLength)) return;

            foreach (var dent in n.dents ?? Array.Empty<Imprint>())
            {
                if ((uint)dent.span >= (uint)profiles.Length || !float.IsFinite(dent.localX) || !float.IsFinite(dent.amount)) continue;
                profiles[dent.span].Retain(dent.localX, Mathf.Clamp01(dent.amount));
                imprints.Add(dent);
            }
            foreach (var mark in n.impressions)
            {
                if (mark.span < 0 || mark.span >= spans.Count) continue;
                spans[mark.span].Hit(mark.seat, ChipStrength, 0, Kick);
                impressions.Add(mark);
            }
            age = 2;
            PoseWall();
            for (int i = 0; i < parts.Count; i++) parts[i].wear = Mathf.Max(0, n.wear[i]);
            x = Mathf.Clamp(n.x, -1.5f * pitch, 1.5f * pitch);
            aim = Mathf.Clamp(n.aim, -1.5f * pitch, 1.5f * pitch);
            float limit = StopCentre(x, out var obstacle);
            z = Mathf.Clamp(n.z, home, Mathf.Min(limit, finish));
            yaw = z == home ? Mathf.Clamp(n.yaw, -90, 90) : 0;
            won = n.finished && obstacle == null && z >= finish && TailZ > farFace + Skin;
            motion = won ? Motion.Passed : Motion.Parked;
            note = won ? "车尾已通过砖墙。" : "小车已停稳；可以接着上次的落点推。";
        }

        public override void Suspend()
        {
            if (!yard) return;
            if (IsWallStudy) return;
            if (!won) Park();
            WriteNotebook();
        }

        void OnDestroy() => RemoveScene();

        void ReleaseMaterialsAndMeshes()
        {
            foreach (var s in spans) s.Dispose();
            spans.Clear();
            parts.Clear();
            impressions.Clear();
            Array.Clear(pads, 0, pads.Length);
            Array.Clear(columns, 0, columns.Length);
            kit?.Dispose();
            kit = null;
            toy?.Dispose();
            toy = null;
        }

        void RemoveScene()
        {
            if (clock) { clock.Owner = null; clock.enabled = false; }
            clock = null;
            if (yard)
            {
                yard.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(yard.gameObject);
            }
            yard = null;
            ReleaseMaterialsAndMeshes();
        }

        internal void SceneDestroyed(WallChapterClock sender)
        {
            if (!ReferenceEquals(sender, clock)) return;
            clock = null;
            yard = null;
            ReleaseMaterialsAndMeshes();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class WallChapterClock : MonoBehaviour
    {
        internal WallChapter Owner;
        void Update() { Owner?.AdvanceClock(Time.deltaTime); }
        void OnDestroy() { Owner?.SceneDestroyed(this); }
    }
}
