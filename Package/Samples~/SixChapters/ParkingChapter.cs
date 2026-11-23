using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    internal sealed class ParkingChapter : global::LabChapter
    {
        // Times are model ticks, not frame numbers. Idle time is not part of a move.
        const int Hz = 60;
        const int Columns = 5, Rows = 3;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly float[] SampleHeights = { 1.35f, .65f, 2.15f, .19f, 2.76f, .40f };

        [Serializable]
        sealed class Click
        {
            public int post, before, after, start, end, waves;
            public Vector2 pixel;           // Actual scene pixel, bottom-left origin.
            public Vector3 localContact;    // Relative to the original post pivot.
            public bool inCutaway;
            public string wear, panels;     // In layout order; panels: '=' level, '~' tilted, '_' landed.
        }

        [Serializable]
        sealed class Journal
        {
            public int version = 1;
            public bool awarded;
            public List<Click> clicks = new List<Click>();
        }

        CampusKit kit;
        CampusLayout layout;
        ParkingDeck deck;
        Transform yard, tag;
        float pitch;
        Journal book = new Journal();

        // Each pick solid belongs to exactly one original top-level model part.
        readonly Dictionary<Collider, Transform> partOf = new Dictionary<Collider, Transform>();
        readonly Dictionary<Transform, int> postId = new Dictionary<Transform, int>();
        readonly List<Transform> cut = new List<Transform>();
        Renderer[][] postRenderers;

        // Cache witnesses only for the current pose, aperture and projection.
        sbyte[] known;
        Vector2[] witness;
        Rect viewRect;
        Matrix4x4 projection;
        int tick, endTick, selected = -1;
        Vector3 selectedContact;
        float fraction;
        bool complete;

        public override bool Complete => complete;
        public override string Hint =>
            complete ? "楼板已全部落稳，可以领取钥匙。" :
            !IsSettled ? "等模型停稳，再找露出的柱面。" :
            cut.Count != 0 ? "边缘剖窗已打开；点窗口中露出的模型柱。" :
            "点露出的模型柱；等楼板停稳，再从新空隙里找柱面。";

        internal bool IsSettled => deck != null && tick == endTick;
        internal int PostCount => deck == null ? 0 : deck.posts.Length;
        internal int PanelCount => deck == null ? 0 : deck.panels.Length;
        internal int WearAt(int id) => id >= 0 && id < PostCount ? deck.Wear(id) : -1;
        internal int SelectedPost => selected;

        protected override void Open()
        {
            var asset = Resources.Load<TextAsset>("campus-dimensions");
            var dims = asset ? JsonUtility.FromJson<CampusDimensions>(asset.text) : new CampusDimensions();
            dims.Validate();
            pitch = dims.parkingPitch;
            layout = CampusLayout.Parking(Columns, Rows, pitch);
            kit = new CampusKit();
            BuildModel();
            FrameOnce();
            viewRect = Eye.pixelRect;
            projection = Eye.projectionMatrix;

            string json = Saved;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var read = JsonUtility.FromJson<Journal>(json);
                    if (read == null || read.version != 1 || read.clicks == null ||
                        read.clicks.Count > 2 * PostCount)
                        throw new FormatException("journal");
                    foreach (Click c in read.clicks) Replay(c);
                    if (read.awarded && !AllLanded()) throw new FormatException("key");
                    book = read;
                }
                catch (Exception e) when (e is FormatException || e is ArgumentException)
                {
                    BuildModel();
                    book = new Journal();
                    Persist();
                    Result("停车桌：记录不符，已恢复空白模型。");
                }
            }

            Result(string.Format(Inv, "停车桌 5×3：{0} 根模型柱，{1} 块楼板，已还原 {2} 次点击。",
                PostCount, PanelCount, book.clicks.Count));
            EnterRest();
        }

        void BuildModel()
        {
            if (yard)
            {
                yard.gameObject.SetActive(false);
                Destroy(yard.gameObject);
            }
            cut.Clear();
            partOf.Clear();
            postId.Clear();
            tick = endTick = 0;
            fraction = 0;
            selected = -1;

            yard = CampusKit.Node("Parking craft desk", transform, Vector3.zero);
            kit.Base(yard, (Columns + 1.35f) * pitch, Rows * pitch + 5);
            deck = new ParkingDeck(layout, kit, yard, pitch);
            BuildSign();

            for (int i = 0; i < deck.posts.Length; ++i)
                postId.Add(deck.posts[i], i);

            // CampusKit's parking solids are cube meshes. Some builds supply only a
            // renderer for the incline; rails, kerbs and markings can also hide a post.
            // Give those meshes matching pick volumes, not an oversized bounds proxy
            // around the whole panel/ramp. Leave the original enabled colliders intact.
            foreach (MeshFilter mesh in yard.GetComponentsInChildren<MeshFilter>())
            {
                Collider col = mesh.GetComponent<Collider>();
                if (!col || !col.enabled)
                {
                    // A disabled original may already be scheduled for Destroy.
                    var box = mesh.gameObject.AddComponent<BoxCollider>();
                    Bounds b = mesh.sharedMesh.bounds;
                    box.center = b.center;
                    box.size = b.size;
                    col = box;
                }
                Transform part = mesh.transform;
                while (part.parent != yard) part = part.parent;
                partOf.Add(col, part);
            }

            known = new sbyte[PostCount];
            witness = new Vector2[PostCount];
            postRenderers = new Renderer[PostCount][];
            for (int i = 0; i < PostCount; ++i)
                postRenderers[i] = deck.posts[i].GetComponentsInChildren<Renderer>();

            // A tiny enamel lozenge, not a button or a pick proxy.
            tag = CampusKit.Node("Selected contact", yard, Vector3.zero);
            kit.Solid(tag, "Enamel rim", Vector3.zero, new Vector3(.19f, .19f, .018f), "357B7C");
            kit.Solid(tag, "Enamel centre", Vector3.back * .013f, new Vector3(.11f, .11f, .014f), "FAE4AB");
            tag.gameObject.SetActive(false);
            SyncPose();
        }

        void BuildSign()
        {
            var sign = CampusKit.Node("Parking sign", yard, layout.posts[0] + new Vector3(-.73f, 0, -.62f));
            kit.Solid(sign, "Sign pole", Vector3.up * .80f, new Vector3(.10f, 1.58f, .12f), "6F837F");
            kit.Solid(sign, "Enamel sign", Vector3.up * 1.68f, new Vector3(.78f, .78f, .12f), "398781");
            void Stroke(float x, float y, float w, float h)
            {
                kit.Solid(sign, "P", new Vector3(x, 1.68f + y, -.066f),
                    new Vector3(w, h, .012f), "F6E2AE");
            }
            Stroke(-.11f, 0, .065f, .48f);
            Stroke(.02f, .208f, .26f, .064f);
            Stroke(.12f, .115f, .06f, .18f);
            Stroke(.02f, .021f, .26f, .062f);
        }

        void FrameOnce()
        {
            var euler = new Vector3(23, -27, 0);
            Quaternion back = Quaternion.Inverse(Quaternion.Euler(euler));
            Vector3 focus = yard.TransformPoint(new Vector3(0, 1.50f, 0));
            float half = 1, depth = 1, aspect = Mathf.Max(.05f, Eye.aspect);
            foreach (Renderer r in yard.GetComponentsInChildren<Renderer>())
            {
                Bounds b = r.bounds;
                for (int k = 0; k < 8; ++k)
                {
                    Vector3 v = back * (Corner(b, k) - focus);
                    half = Mathf.Max(half, Mathf.Max(Mathf.Abs(v.x) / aspect, Mathf.Abs(v.y)));
                    depth = Mathf.Max(depth, Mathf.Abs(v.z));
                }
            }
            View(focus, euler, half * 1.065f, depth + 9);
        }

        static Vector3 Corner(Bounds b, int k)
        {
            Vector3 e = b.extents;
            return b.center + new Vector3((k & 1) == 0 ? -e.x : e.x,
                (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
        }

        void SyncPose()
        {
            Physics.SyncTransforms();
            Array.Clear(known, 0, known.Length);
        }

        void CheckProjection()
        {
            if (viewRect == Eye.pixelRect && projection == Eye.projectionMatrix) return;
            viewRect = Eye.pixelRect;
            projection = Eye.projectionMatrix;
            CloseCut();
            SyncPose();
            if (IsSettled && !complete) EnsureAccess();
            Mark(IsSettled);
        }

        static Vector2 Pixel(Vector2 p) =>
            new Vector2(Mathf.Floor(p.x) + .5f, Mathf.Floor(p.y) + .5f);

        int ReadHit(Vector2 pixel, out RaycastHit hit)
        {
            hit = default;
            if (!viewRect.Contains(pixel)) return -1;
            Ray ray = Eye.ScreenPointToRay(pixel);
            if (!Physics.Raycast(ray, out hit, Eye.farClipPlane - Eye.nearClipPlane,
                Eye.cullingMask, QueryTriggerInteraction.Ignore)) return -1;
            return partOf.TryGetValue(hit.collider, out Transform part) &&
                   postId.TryGetValue(part, out int id) ? id : -1;
        }

        // Proof API: physical front hit; no "nearest projected post" substitution.
        internal int PostAt(Vector2 screen)
        {
            if (deck == null) return -1;
            CheckProjection();
            return viewRect.Contains(screen) ? ReadHit(Pixel(screen), out _) : -1;
        }

        bool Project(Transform t, Vector3 local, out Vector2 pixel)
        {
            Vector3 p = Eye.WorldToScreenPoint(t.TransformPoint(local));
            pixel = Pixel(new Vector2(p.x, p.y));
            return p.z > Eye.nearClipPlane && p.z < Eye.farClipPlane && viewRect.Contains(pixel);
        }

        bool QuickWitness(int id, out Vector2 pixel)
        {
            Transform post = deck.posts[id];
            foreach (float h in SampleHeights)
            {
                for (int j = 0; j < 5; ++j)
                {
                    float x = j == 1 ? -.155f : j == 2 ? .155f : 0;
                    float z = j == 3 ? -.155f : j == 4 ? .155f : 0;
                    if (Project(post, new Vector3(x, h, z), out pixel) && ReadHit(pixel, out _) == id)
                        return true;
                }
            }
            pixel = default;
            return false;
        }

        // Only bounds the search. A pixel inside this rectangle is NOT necessarily a hit.
        RectInt SearchRect(int id)
        {
            Renderer[] rs = postRenderers[id];
            Bounds b = rs[0].bounds;
            for (int j = 1; j < rs.Length; ++j) b.Encapsulate(rs[j].bounds);
            Vector2 lo = Vector2.positiveInfinity, hi = Vector2.negativeInfinity;
            for (int j = 0; j < 8; ++j)
            {
                Vector3 s = Eye.WorldToScreenPoint(Corner(b, j));
                lo = Vector2.Min(lo, s);
                hi = Vector2.Max(hi, s);
            }
            int x0 = Mathf.FloorToInt(Mathf.Max(viewRect.xMin, lo.x));
            int y0 = Mathf.FloorToInt(Mathf.Max(viewRect.yMin, lo.y));
            int x1 = Mathf.CeilToInt(Mathf.Min(viewRect.xMax, hi.x)) + 1;
            int y1 = Mathf.CeilToInt(Mathf.Min(viewRect.yMax, hi.y)) + 1;
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        bool FindPixel(int id, out Vector2 pixel)
        {
            if (known[id] != 0)
            {
                pixel = witness[id];
                return known[id] > 0;
            }
            if (deck.posts[id].gameObject.activeInHierarchy)
            {
                if (QuickWitness(id, out pixel))
                {
                    witness[id] = pixel;
                    known[id] = 1;
                    return true;
                }
                RectInt r = SearchRect(id);
                for (int y = r.yMin; y < r.yMax; ++y)
                    for (int x = r.xMin; x < r.xMax; ++x)
                    {
                        pixel = new Vector2(x + .5f, y + .5f);
                        if (ReadHit(pixel, out _) != id) continue;
                        witness[id] = pixel;
                        known[id] = 1;
                        return true;
                    }
            }
            known[id] = -1;
            pixel = witness[id] = default;
            return false;
        }

        // Returns bottom-left screen x/y at the CURRENT pose, including for a spent
        // post if still visible. For a playable move also check IsSettled, !Complete
        // and WearAt(id) < 2, then pass this very pixel to Tap.
        internal bool TryGetVisiblePost(int id, out Vector2 screen)
        {
            screen = default;
            if (id < 0 || id >= PostCount) return false;
            CheckProjection();
            return FindPixel(id, out screen);
        }

        internal int GetVisiblePosts(List<int> ids, List<Vector2> pixels)
        {
            ids.Clear();
            pixels.Clear();
            if (deck == null) return 0;
            CheckProjection();
            for (int i = 0; i < PostCount; ++i)
                if (deck.Wear(i) < 2 && FindPixel(i, out Vector2 p))
                {
                    ids.Add(i);
                    pixels.Add(p);
                }
            return ids.Count;
        }

        void Mark(bool thorough)
        {
            if (selected < 0)
            {
                tag.gameObject.SetActive(false);
                return;
            }

            bool visible = Project(deck.posts[selected], selectedContact, out Vector2 p);
            RaycastHit hit = default;
            visible = visible && ReadHit(p, out hit) == selected;
            if (!visible)
            {
                visible = thorough ? FindPixel(selected, out p) : QuickWitness(selected, out p);
                visible = visible && ReadHit(p, out hit) == selected;
            }
            if (visible)
                tag.SetPositionAndRotation(hit.point - Eye.transform.forward * .022f,
                    Eye.transform.rotation * Quaternion.AngleAxis(45, Vector3.forward));
            tag.gameObject.SetActive(visible);
        }

        void CloseCut()
        {
            foreach (Transform part in cut) part.gameObject.SetActive(true);
            cut.Clear();
        }

        void EnsureAccess()
        {
            if (cut.Count != 0 || !IsSettled || complete || viewRect.width < 1 || viewRect.height < 1)
                return;

            // Usually a few rays suffice. Never infer a dead end from missed samples.
            for (int i = 0; i < PostCount; ++i)
                if (deck.Wear(i) < 2 && QuickWitness(i, out Vector2 p))
                {
                    known[i] = 1;
                    witness[i] = p;
                    return;
                }
            for (int i = 0; i < PostCount; ++i)
                if (deck.Wear(i) < 2 && FindPixel(i, out _)) return;

            // At rest, no usable original post has even one clickable scene pixel.
            // Thus there is no first click of ANY continuation from this snapshot.
            // Find a narrow inspection opening. Hide actual parts (paint AND picking)
            // together, and leave the player to make the next real click.
            List<Transform> best = null;
            Vector2 window = default;
            var blockers = new List<Transform>();

            void Inspect(Vector2 p)
            {
                if (!viewRect.Contains(p)) return;
                var hits = Physics.RaycastAll(Eye.ScreenPointToRay(p), Eye.farClipPlane - Eye.nearClipPlane,
                    Eye.cullingMask, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                blockers.Clear();
                foreach (RaycastHit h in hits)
                {
                    if (!partOf.TryGetValue(h.collider, out Transform part)) return;
                    if (postId.TryGetValue(part, out int id) && deck.Wear(id) < 2)
                    {
                        if (blockers.Count != 0 && (best == null || blockers.Count < best.Count))
                        {
                            best = new List<Transform>(blockers);
                            window = p;
                        }
                        return;
                    }
                    if (!blockers.Contains(part)) blockers.Add(part);
                    if (best != null && blockers.Count >= best.Count) return;
                }
            }

            for (int i = 0; i < PostCount; ++i)
                if (deck.Wear(i) < 2)
                    foreach (float h in SampleHeights)
                        if (Project(deck.posts[i], Vector3.up * h, out Vector2 p)) Inspect(p);

            // Covers subpixel centre samples on very small proof viewports.
            for (int i = 0; best == null && i < PostCount; ++i)
            {
                if (deck.Wear(i) >= 2) continue;
                RectInt r = SearchRect(i);
                for (int y = r.yMin; best == null && y < r.yMax; ++y)
                    for (int x = r.xMin; best == null && x < r.xMax; ++x)
                        Inspect(new Vector2(x + .5f, y + .5f));
            }

            if (best == null) return;
            cut.AddRange(best);
            foreach (Transform part in cut) part.gameObject.SetActive(false);
            SyncPose();
            int exposed = ReadHit(window, out _);
            if (exposed < 0 || deck.Wear(exposed) >= 2)
            {
                CloseCut();
                SyncPose();
                return;
            }
            Result(string.Format(Inv, "停车桌：停稳后无可点柱面，已打开边缘剖窗（{0} 件饰件或板件）；柱状态未改。", cut.Count));
        }

        // Evaluate the ORIGINAL deck at deterministic times. Pose is a pure sampling
        // operation on its motion history. Its Fallen counter denotes *start* of a
        // fall, so it is not by itself a completion test.
        int ApplyToRest(int post, int start)
        {
            int oldFalls = deck.Fallen;
            deck.Damage(post, start / (float)Hz);

            // At most one queued release per panel, spaced by at most .32 s
            // per link. Earlier journal moves have already completely settled.
            int probe = start + 20 * PanelCount + Hz;
            deck.Pose(probe / (float)Hz);
            int eventual = deck.Fallen;
            int rest = start + 30; // post motion lasts .47 s
            if (eventual > oldFalls)
            {
                int lo = start, hi = probe;
                while (lo < hi)
                {
                    int mid = lo + (hi - lo) / 2;
                    deck.Pose(mid / (float)Hz);
                    if (deck.Fallen == eventual) hi = mid;
                    else lo = mid + 1;
                }
                rest = Mathf.Max(rest, lo + 51); // last release plus its full .83 s descent
            }
            deck.Pose(rest / (float)Hz);
            return rest;
        }

        string WearSnapshot()
        {
            var chars = new char[PostCount];
            for (int i = 0; i < chars.Length; ++i) chars[i] = (char)('0' + deck.Wear(i));
            return new string(chars);
        }

        static bool OnDesk(Transform panel) => Mathf.Abs(panel.localPosition.y - .39f) < .0001f;

        string PanelSnapshot()
        {
            // Called at a settled pose only. A symmetrically dipped panel can have
            // zero rotation but still contributes to the original deck's Tilted count.
            var chars = new char[PanelCount];
            for (int i = 0; i < chars.Length; ++i)
            {
                if (OnDesk(deck.panels[i])) { chars[i] = '_'; continue; }
                chars[i] = '=';
                foreach (int id in layout.floors[i])
                    if (deck.Wear(id) != 0) { chars[i] = '~'; break; }
            }
            return new string(chars);
        }

        bool AllLanded()
        {
            if (!IsSettled || deck.Fallen != PanelCount) return false;
            foreach (Transform panel in deck.panels) if (!OnDesk(panel)) return false;
            return true;
        }

        public override bool Tap(Vector2 screen)
        {
            if (!IsSettled || complete) return false;
            CheckProjection();
            if (!viewRect.Contains(screen)) return false;
            Vector2 pixel = Pixel(screen);
            int id = ReadHit(pixel, out RaycastHit hit);
            if (id < 0 || deck.Wear(id) >= 2) return false;

            selected = id;
            selectedContact = deck.posts[id].InverseTransformPoint(hit.point);
            var c = new Click
            {
                post = id, before = deck.Wear(id), start = tick + 1, pixel = pixel,
                localContact = selectedContact, inCutaway = cut.Count != 0
            };
            CloseCut();
            c.end = ApplyToRest(id, c.start);
            c.after = deck.Wear(id);
            c.wear = WearSnapshot();
            c.panels = PanelSnapshot();
            c.waves = deck.Waves;
            book.clicks.Add(c);

            // Never present or award the look-ahead pose. Play this click from its start.
            tick = c.start;
            endTick = c.end;
            fraction = 0;
            deck.Pose(tick / (float)Hz);
            SyncPose();
            Mark(false);
            Persist();
            Result(string.Format(Inv,
                "停车桌 click={0} xy=({1:F1},{2:F1}) post={3} wear={4}->{5} cut={6} ticks={7}..{8} | 停稳快照 wear={9} panels={10} waves={11}",
                book.clicks.Count, pixel.x, pixel.y, id, c.before, c.after, c.inCutaway ? 1 : 0,
                c.start, c.end, c.wear, c.panels, c.waves));
            return true;
        }

        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);

        void Replay(Click c)
        {
            if (c == null || c.post < 0 || c.post >= PostCount || c.start != tick + 1 ||
                c.before != deck.Wear(c.post) || c.before >= 2 || !Finite(c.localContact) ||
                !float.IsFinite(c.pixel.x) || !float.IsFinite(c.pixel.y) || AllLanded())
                throw new FormatException("click");

            // Reapply in order, not from a histogram of wear: release order and waves
            // belong to ParkingDeck. Never re-pick an old pixel at a new resolution.
            int rest = ApplyToRest(c.post, c.start);
            if (rest != c.end || c.after != deck.Wear(c.post) || c.wear != WearSnapshot() ||
                c.panels != PanelSnapshot() || c.waves != deck.Waves)
                throw new FormatException("settled pose");
            tick = endTick = rest;
            selected = c.post;
            selectedContact = c.localContact;
        }

        void Persist() => Save(JsonUtility.ToJson(book));

        void EnterRest()
        {
            SyncPose();
            if (AllLanded())
            {
                complete = true;
                if (!book.awarded)
                {
                    book.awarded = true;
                    Persist();
                    Result("停车桌：15 块玩具楼板均已落稳，钥匙就绪。");
                }
            }
            else EnsureAccess();
            Mark(true);
        }

        void Update()
        {
            if (deck == null) return;
            CheckProjection();
            if (tick >= endTick) return;
            fraction += Time.unscaledDeltaTime * Hz;
            int advance = Mathf.Min(endTick - tick, Mathf.FloorToInt(fraction));
            if (advance <= 0) return;
            tick += advance;
            fraction -= advance;
            deck.Pose(tick / (float)Hz);
            if (tick == endTick)
            {
                fraction = 0;
                Result(string.Format(Inv, "停车桌 settled={0} tilted={1} landed={2}/{3} panels={4}",
                    tick, deck.Tilted, deck.Fallen, PanelCount, PanelSnapshot()));
                EnterRest();
            }
            else
            {
                SyncPose();
                Mark(false);
            }
        }

        internal bool UndoLast()
        {
            if (complete || book.clicks.Count == 0) return false;
            int last = book.clicks.Count - 1;
            int removed = book.clicks[last].post;
            book.clicks.RemoveAt(last);
            BuildModel();
            foreach (Click c in book.clicks) Replay(c);
            Persist();
            Result(string.Format(Inv, "停车桌 undo post={0} clicks={1} wear={2} panels={3}",
                removed, book.clicks.Count, WearSnapshot(), PanelSnapshot()));
            EnterRest();
            return true;
        }

        public override void Panel()
        {
            if (!complete && book.clicks.Count != 0 && ActionButton("撤回一步")) UndoLast();
        }

        void OnDestroy() => kit?.Dispose();
    }
}
