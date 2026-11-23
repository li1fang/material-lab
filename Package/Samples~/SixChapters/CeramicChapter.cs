using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Sample
{
    internal sealed class CeramicChapter : global::LabChapter
    {
        const float Radius = CeramicRim.Radius;
        const float BallRadius = .018f;
        static readonly float[] Heights = { .07f, .21f, .37f };
        const float MarkTime = .22f;
        const float QuietTime = .20f;
        const float FlightTimeout = 3f;
        const float Pitch = 58f;

        enum Phase { Ready, Marked, Released }

        [Serializable]
        sealed class Notebook
        {
            public int drops, chips;
            public bool center, edge, complete;
        }

        struct Pick
        {
            public int id;
            public Vector2 pixel;
            public Vector3 local;
            public float height; // Clearance from the picked surface to the bottom of the ball.

            public float Radial => new Vector2(local.x, local.z).magnitude / Radius;
            public string Zone => Radial <= .45f ? "碟心" : Radial >= .71f ? "碟沿" : "碟面";
        }

        readonly Queue<Pick> waiting = new Queue<Pick>();
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        Notebook book = new Notebook();
        Transform station;
        readonly Transform[] heightTicks = new Transform[3];
        CeramicSpecimen dish;
        GameObject aimMark, contactMark;
        Rigidbody ball;
        Material glaze, oak, steel, ink, copper;
        PhysicsMaterial ballSurface;

        Phase phase;
        Pick current;
        int serial, contactBase, fragmentBase;
        float planeY, baseY, framedAspect, elapsed, quiet;
        bool firstShown;
        string feedback = "";

        public override bool Complete => book.complete;

        public override string Hint
        {
            get
            {
                if (!dish) return feedback;
                if (phase != Phase.Ready)
                {
                    string action = phase == Phase.Marked ? "已标记，即将落球。" :
                        firstShown ? "已碰碟，等小球停稳。" : "落球中，观察接触点。";
                    return F($"{current.Zone}（落高 {current.height:F2} m）：") + action +
                        (waiting.Count == 0 ? "可点下一处。" : $"待落 {waiting.Count} 次。");
                }

                return feedback + (Complete ? "可继续点碟或换新碟。" :
                    book.center ? "再点碟沿。" : book.edge ? "再点碟心。" : "点尺上的高度，再点碟心或碟沿落球。");
            }
        }

        protected override void Open()
        {
            var finish = Resources.Load<Shader>("CeramicFinish");
            var bench = Resources.Load<Shader>("WoodWorkbench");
            if (!finish || !bench || !finish.isSupported || !bench.isSupported)
            {
                feedback = "请检查青釉与工作台着色器。";
                Result(feedback);
                return;
            }

            glaze = new Material(finish);
            oak = Tinted(bench, new Color(.67f, .51f, .33f));
            steel = Tinted(bench, new Color(.49f, .58f, .61f));
            ink = Tinted(bench, new Color(.11f, .22f, .24f));
            copper = Tinted(bench, new Color(.94f, .39f, .12f));
            ballSurface = new PhysicsMaterial("桌面模型 · 钢球")
            {
                bounciness = .40f,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            // Persist finished observations, not a reconstruction of a moving specimen.
            string json = Saved;
            if (!string.IsNullOrEmpty(json))
            {
                try { book = JsonUtility.FromJson<Notebook>(json) ?? new Notebook(); }
                catch (ArgumentException) { Result("未能读取观察记录；使用新记录。"); }
            }
            NewDish();
        }

        static Material Tinted(Shader shader, Color color)
        {
            var mat = new Material(shader);
            mat.SetColor("_Tint", color);
            return mat;
        }

        GameObject Shape(string label, PrimitiveType type, Vector3 position, Vector3 size,
                         Material mat, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = label;
            go.transform.SetParent(station, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var collider = go.GetComponent<Collider>();
            if (solid) collider.contactOffset = .0015f;
            else
            {
                collider.enabled = false;
                Destroy(collider);
            }
            return go;
        }

        void NewDish()
        {
            RemoveStation();
            waiting.Clear();
            bodies.Clear();
            serial = book.drops;
            phase = Phase.Ready;
            feedback = "";
            firstShown = false;
            LastPeakImpulseNs = 0;

            // World-horizontal, metre-scale station even if the shell has a transformed root.
            station = new GameObject("青釉观察台").transform;
            station.position = transform.position;
            station.SetParent(transform, true);
            var specimen = new GameObject("青釉陶碟");
            specimen.transform.SetParent(station, false);
            dish = specimen.AddComponent<CeramicSpecimen>();
            dish.Initialize(glaze);

            Bounds bounds = specimen.GetComponent<MeshFilter>().sharedMesh.bounds;
            baseY = bounds.min.y;
            planeY = bounds.max.y; // Keep the intact rim's reference plane even after a chip leaves.
            float top = baseY - .0004f;
            Vector3 foot = new Vector3(-.222f, planeY, -.018f);
            Shape("落高尺", PrimitiveType.Cube, foot + Vector3.up * .19f,
                new Vector3(.004f,.39f,.004f), ink, false);
            for (int i = 0; i < Heights.Length; i++)
                heightTicks[i] = Shape("落高刻度", PrimitiveType.Cube,
                    foot + Vector3.up * Heights[i], new Vector3(.024f,.004f,.006f), copper, false).transform;
            const float side = .80f, lip = .012f;
            Shape("承片木盘", PrimitiveType.Cube, Vector3.up * (top - .022f),
                new Vector3(side, .044f, side), oak, true);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float at = sign * (side - lip) * .5f;
                Shape("盘边", PrimitiveType.Cube, new Vector3(at, top + .014f, 0),
                    new Vector3(lip, .028f, side), oak, true);
                Shape("盘边", PrimitiveType.Cube, new Vector3(0, top + .014f, at),
                    new Vector3(side - 2 * lip, .028f, lip), oak, true);
            }

            aimMark = Shape("点选墨记", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(.019f, .003f, .019f), ink, false);
            contactMark = Shape("首次接触 · 铜点", PrimitiveType.Sphere, Vector3.zero,
                Vector3.one * .009f, copper, false);
            aimMark.SetActive(false);
            contactMark.SetActive(false);
            Frame();

            // Pixel guides for manual taps only; all trials enter via Tap.
            Result(F($"新碟：历史落球={book.drops}；参考面 y={planeY:F4}；屏幕像素 碟心={Guide(0, 0)} 左缘={Guide(-.94f, 0)} 右缘={Guide(.94f, 0)} 后缘={Guide(0, .94f)}"));
        }

        string Guide(float x, float z) => Pixel(Eye.WorldToScreenPoint(
            dish.transform.TransformPoint(new Vector3(x * Radius, planeY, z * Radius))));

        internal Vector2 DiscScreen(float x, float z)
        {
            Vector3 point = dish.transform.TransformPoint(new Vector3(x * Radius, planeY, z * Radius));
            var vertical = new Ray(point + Vector3.up * .20f, Vector3.down);
            return Eye.WorldToScreenPoint(dish.GetComponent<Collider>().Raycast(vertical, out var hit, .4f) ? hit.point : point);
        }
        internal int Drops => book.drops;
        internal int Missing => dish.Rim.Missing;
        internal float LastPeakImpulseNs { get; private set; }
        internal float SelectedHeight { get; private set; } = Heights[1];
        internal Vector2 HeightScreen(int i) => Eye.WorldToScreenPoint(heightTicks[i].position);

        internal void SetHeight(float meters)
        {
            if (Array.IndexOf(Heights, meters) < 0)
                throw new ArgumentOutOfRangeException(nameof(meters));
            SelectedHeight = meters;
        }

        public override void Overlay()
        {
            if (!dish) return;
            for (int i = 0; i < Heights.Length; i++)
                Cue(Eye.WorldToScreenPoint(heightTicks[i].position), F($"{Heights[i]*100:0} 厘米{(SelectedHeight == Heights[i] ? " ✓" : "")}"),
                    new Vector2(-86, -14));
            if (phase == Phase.Marked && aimMark.activeSelf)
                Cue(Eye.WorldToScreenPoint(aimMark.transform.position), "钢珠从这里落下", new Vector2(14, -18));
            if (phase == Phase.Ready && contactMark.activeSelf)
                Cue(Eye.WorldToScreenPoint(contactMark.transform.position), "上次接触", new Vector2(12, -12));
        }

        void Frame()
        {
            framedAspect = Mathf.Max(.01f, Eye.aspect);
            float top = planeY + Heights[Heights.Length - 1] + 2 * BallRadius;
            float halfHeight = (top - baseY) * .5f;
            float span = Radius + BallRadius + .018f;
            float angle = Pitch * Mathf.Deg2Rad;
            float size = Mathf.Max(span / framedAspect,
                span * Mathf.Sin(angle) + halfHeight * Mathf.Cos(angle) + .015f);
            float pad = Mathf.Min(.55f, (Eye.farClipPlane - Eye.nearClipPlane) * .49f);
            float distance = Mathf.Clamp(1.5f, Eye.nearClipPlane + pad, Eye.farClipPlane - pad);
            View(dish.transform.TransformPoint(Vector3.up * (baseY + halfHeight)),
                new Vector3(Pitch, -18f, 0), size, distance);
        }

        void Update()
        {
            if (dish && !Mathf.Approximately(framedAspect, Mathf.Max(.01f, Eye.aspect)))
                Frame();
        }

        public override bool Tap(Vector2 screen)
        {
            if (!dish || !Eye.pixelRect.Contains(screen)) return false;
            float scale = Eye.pixelRect.height / 688f;
            for (int i = 0; i < Heights.Length; i++)
            {
                Vector2 delta = (screen - (Vector2)Eye.WorldToScreenPoint(heightTicks[i].position)) / scale;
                if (delta.x >= -86 && delta.x <= 24 && Mathf.Abs(delta.y) <= 18)
                {
                    SetHeight(Heights[i]);
                    return true;
                }
            }
            Ray ray = Eye.ScreenPointToRay(screen);
            Vector3 local;
            if (dish.GetComponent<Collider>().Raycast(ray, out var hit, Eye.farClipPlane))
                local = dish.transform.InverseTransformPoint(hit.point);
            else
            {
                var plane = new Plane(Vector3.up, dish.transform.TransformPoint(Vector3.up * planeY));
                if (!plane.Raycast(ray, out float along)) return false;
                local = dish.transform.InverseTransformPoint(ray.GetPoint(along));
            }
            if (local.x * local.x + local.z * local.z > Radius * Radius + 1e-8f)
            {
                feedback = "点在碟外了。";
                Result($"碟外：pixel={Pixel(screen)} planeLocal={Point(local)}");
                return false;
            }
            if (waiting.Count >= 8)
            {
                Result($"待落实验已满，请稍候。pixel={Pixel(screen)}");
                return false;
            }

            var pick = new Pick { id = ++serial, pixel = screen, local = local, height = SelectedHeight };
            Result(F($"点选 #{pick.id} {pick.Zone} requestedPx={Pixel(screen)} requestedLocal={Point(local)} r/R={pick.Radial:F3} heightM={pick.height:F2}"));
            if (phase == Phase.Ready) Mark(pick);
            else waiting.Enqueue(pick);
            return true;
        }

        void Mark(Pick pick)
        {
            current = pick;
            contactMark.SetActive(false);
            aimMark.transform.position = dish.transform.TransformPoint(pick.local) + Vector3.up * .0016f;
            aimMark.SetActive(true);
            elapsed = 0;
            firstShown = false;
            phase = Phase.Marked;
        }

        void Release()
        {
            aimMark.SetActive(false);
            contactBase = dish.Contacts.Count;
            fragmentBase = dish.Fragments;
            Vector3 column = dish.transform.TransformPoint(current.local);
            var go = Shape("观察钢球", PrimitiveType.Sphere, Vector3.zero,
                Vector3.one * (2 * BallRadius), steel, true);
            go.transform.position = column + Vector3.up * (BallRadius + current.height);
            go.GetComponent<Collider>().sharedMaterial = ballSurface;
            ball = go.AddComponent<Rigidbody>();
            ball.mass = .19f;
            ball.useGravity = true;
            ball.interpolation = RigidbodyInterpolation.Interpolate;
            ball.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            dish.ListenTo(ball, null);
            elapsed = quiet = 0;
            phase = Phase.Released;
            Result($"落球 #{current.id} targetWorld={Point(column)} releaseWorld={Point(ball.position)}");
        }

        void FixedUpdate()
        {
            if (!dish) return;
            ++dish.Step;
            if (phase == Phase.Ready) return;
            elapsed += Time.fixedDeltaTime;
            if (phase == Phase.Marked)
            {
                if (elapsed >= MarkTime) Release();
                return;
            }

            // The specimen owns collision selection, damage, mesh rebuilding and debris.
            if (!firstShown && dish.Contacts.Count > contactBase)
            {
                var hit = dish.Contacts[contactBase];
                Vector3 world = dish.transform.TransformPoint(hit.localPoint);
                contactMark.transform.position = world - Eye.transform.forward * .003f;
                contactMark.SetActive(true);
                firstShown = true;
                Result(F($"首次碰碟 #{current.id} requestedPx={Pixel(current.pixel)} requestedLocal={Point(current.local)} actualPx={Pixel(Eye.WorldToScreenPoint(world))} actualLocal={Point(hit.localPoint)} step={hit.step} impulseNs={hit.impulseNs:F4}"));
            }

            quiet = AllQuiet() ? quiet + Time.fixedDeltaTime : 0;
            if (quiet >= QuietTime || elapsed >= FlightTimeout)
                Finish(quiet >= QuietTime);
        }

        bool AllQuiet()
        {
            station.GetComponentsInChildren<Rigidbody>(false, bodies);
            foreach (var body in bodies)
            {
                if (body.isKinematic || body.IsSleeping()) continue;
                if (body.linearVelocity.sqrMagnitude > .015f * .015f ||
                    body.angularVelocity.sqrMagnitude > .7f * .7f) return false;
            }
            return true;
        }

        void Finish(bool settled)
        {
            int hits = dish.Contacts.Count - contactBase;
            int chips = dish.Fragments - fragmentBase;
            float peak = 0;
            for (int i = contactBase; i < dish.Contacts.Count; ++i)
                peak = Mathf.Max(peak, dish.Contacts[i].impulseNs);
            LastPeakImpulseNs = peak;

            dish.ListenTo(null, null);
            if (ball)
            {
                ball.gameObject.SetActive(false);
                Destroy(ball.gameObject);
                ball = null;
            }

            ++book.drops;
            book.chips += chips;
            book.center |= current.Radial <= .45f;
            book.edge |= current.Radial >= .71f;
            book.complete |= (book.center && book.edge) || (book.drops >= 2 && book.chips > 0);
            Save(JsonUtility.ToJson(book));

            feedback = chips > 0 ? $"{current.Zone}落下 {chips} 片。" :
                hits > 0 ? $"{current.Zone}接触，未掉片。" : "未碰到陶面。";
            feedback += F($"上次落高 {current.height*100:0} 厘米。");
            string actual = hits > 0 ? Point(dish.Contacts[contactBase].localPoint) : "none";
            Result(F($"观察 #{current.id} {(settled ? "停稳" : "超时收球")} t={elapsed:F2} heightM={current.height:F2} requestedLocal={Point(current.local)} firstLocal={actual} contacts={hits} peakNs={peak:F4} newChips={chips} fragments={dish.Fragments} missing={dish.Rim.Missing} drops={book.drops} complete={Complete}"));

            phase = Phase.Ready;
            if (waiting.Count != 0) Mark(waiting.Dequeue());
        }

        public override void Panel()
        {
            if (!dish) return;
            if (!ActionButton("换新碟")) return;
            // Clear this desk and its trial history. Only the shell resets an earned chapter.
            book = new Notebook { complete = Complete };
            Save(JsonUtility.ToJson(book));
            NewDish();
            Result("已清除钢球、碎片、标记及待落点。");
        }

        void RemoveStation()
        {
            if (station)
            {
                station.gameObject.SetActive(false);
                Destroy(station.gameObject);
            }
            station = null;
            dish = null;
            ball = null;
        }

        static string F(FormattableString text) => FormattableString.Invariant(text);
        static string Pixel(Vector2 p) => F($"({p.x:F1},{p.y:F1})");
        static string Point(Vector3 p) => F($"({p.x:F4},{p.y:F4},{p.z:F4})");

        void OnDestroy()
        {
            RemoveStation();
            if (glaze) Destroy(glaze);
            if (oak) Destroy(oak);
            if (steel) Destroy(steel);
            if (ink) Destroy(ink);
            if (copper) Destroy(copper);
            if (ballSurface) Destroy(ballSurface);
        }
    }
}
