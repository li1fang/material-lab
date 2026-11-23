// BuildingChapter.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MaterialDestruction.Campus
{
    internal sealed class BuildingChapter : LabChapter
    {
        const int Version = 1;
        const float OriginalHeight = 1.959f;
        const float JacketHeight = BayFrame.CoreHeight - .10f;
        const float JacketDepth = .11f;

        [Serializable]
        sealed class Finish
        {
            public string style = "clay", profile = "A";
            public float width, height, brickWidth = .60f, brickHeight = .245f, brickDepth = .23f;

            public Finish Copy() => (Finish)MemberwiseClone();

            public BrickWallSpec Spec() => new BrickWallSpec
            {
                width = width, height = height,
                brickWidth = brickWidth, brickHeight = brickHeight, brickDepth = brickDepth,
                profile = profile, surfaceMaterial = BayBrickMaterials.ForStyle(style)
            };
        }

        // Contact coordinates belong to the picked collider, not the camera or the yard.
        // A fit supersedes earlier fits and chip contacts on that skin only.
        [Serializable]
        sealed class Move
        {
            public string op, id;
            public float time;
            public int collider;
            public Vector3 point, normal;
            public Finish finish;
        }

        [Serializable]
        sealed class Recipe
        {
            public int version = Version, count = 2;
            public float pitch = 4f, elapsed;
            public string baseline = "original-clay", selection = "span/0/brick";
            public bool tried, applied, connected;
            public List<Move> moves = new List<Move>();
        }

        enum Kind { Post, Beam, Deck, Rail, Wall, Jacket, Chip, Fiber }

        sealed class Part
        {
            public string id;
            public Kind kind;
            public int index, slot;
            public int[] nodes;
            public Transform tr, restParent;
            public Vector3 rest;
            public Renderer[] ink;
            public Collider[] contacts;
            public Part owner;
            public BrickSkin skin;
            public Finish finish;
            public bool live = true;
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] Styles = { "clay", "kiln", "lime" };
        static readonly string[] StyleNames = { "陶土", "窑褐", "石灰" };
        static readonly string[] Profiles = { "A", "B", "C", "D" };
        static readonly string[] FiberHex = { "50BDC1", "F1AE58", "A18AD2", "E17F97" };
        static readonly string[] Fields = { "墙面宽", "墙面高", "砖宽", "砖高", "砖厚" };
        static readonly float[] Steps = { .10f, .10f, .05f, .025f, .01f };

        static readonly Color Paper = new Color32(31, 48, 48, 255);
        static readonly Color Tile = new Color32(50, 70, 69, 255);
        static readonly Color Chosen = new Color32(34, 108, 102, 255);
        static readonly Color Ink = new Color32(244, 242, 227, 255);
        static readonly Color Muted = new Color32(183, 205, 198, 255);
        static readonly Color Accent = new Color32(252, 201, 108, 255);

        Recipe recipe;
        CampusKit kit;
        BayFrame frame;
        Transform yard;
        Mesh fiberMesh;
        Material dimFiber;
        Texture2D seams;
        Font font;
        GUIStyle text, small, input;

        readonly SortedDictionary<string, Part> parts = new SortedDictionary<string, Part>(StringComparer.Ordinal);
        readonly Dictionary<Collider, Part> picks = new Dictionary<Collider, Part>();
        readonly SortedSet<int> marked = new SortedSet<int>();
        Part[] posts, beams, decks, rails, walls, jackets;
        Part[,] fibers;
        int[] nativePost, off;
        Part selected;
        Move pickedContact;
        bool pickedCasting, masonry = true, allMatching;
        Finish draft;
        readonly string[] raw = new string[5];
        float origin, offset;
        string note;

        public override string Hint => Complete ? "配方已存档。可继续比较砖面与连接状态。"
            : recipe == null || !recipe.tried ? "点选外砖，再按「试敲砖片」。"
            : !recipe.applied ? "在砌体页调整样式或尺寸，然后应用配方。"
            : "在连接页选一束彩纤，改动一次连接。";

        public override bool Complete => recipe != null && recipe.tried && recipe.applied && recipe.connected;

        internal string[] TestIds() => parts.Keys.ToArray();
        internal string TestSelection => selected?.id;
        internal bool TestComplete => Complete;
        internal string TestRecipe => recipe == null ? null : JsonUtility.ToJson(recipe);
        internal bool TryProofBrick(out Vector2 pixel)
        {
            Physics.SyncTransforms();
            foreach (var candidate in picks)
            {
                if (candidate.Value.kind != Kind.Chip || candidate.Value.owner.kind != Kind.Wall) continue;
                pixel = Eye.WorldToScreenPoint(candidate.Key.bounds.center);
                if (Eye.pixelRect.Contains(pixel) &&
                    Physics.Raycast(Eye.ScreenPointToRay(pixel), out var hit, Eye.farClipPlane) &&
                    picks.TryGetValue(hit.collider, out var current) && current == candidate.Value) return true;
            }
            pixel = default;
            return false;
        }

        internal bool TestApplyWall()
        {
            if (!recipe.tried || SkinOf(selected) == null) return false;
            draft.profile = "D";
            draft.style = "lime";
            allMatching = true;
            ApplyFit();
            return recipe.applied && Array.TrueForAll(walls, wall => wall.finish?.profile == "D" && wall.finish.style == "lime");
        }

        internal bool TestPostChange()
        {
            Select(fibers[1, 0]);
            SingleStep();
            return TestActiveMask(1) == 14 && recipe.connected;
        }

        internal bool TestSharedChange()
        {
            if (TestActiveMask(1) != 14 || TestActiveMask(4) != 15) return false;
            int[] keys = { 5, 6, 7, 16, 17, 18, 19 };
            int start = recipe.moves.Count;
            Commit(keys);
            if (recipe.moves.Count != start + keys.Length || off[1] != 15 || off[4] != 15) return false;
            float time = recipe.moves[start].time;
            return recipe.moves.Skip(start).All(m => m.time == time) &&
                frame.Deck.Wear(nativePost[1]) == 2 && frame.Deck.Wear(nativePost[4]) == 2 &&
                decks.Count(d => d.nodes.Contains(1) && d.nodes.Contains(4)) == 2;
        }

        internal bool TestExtend()
        {
            Extend();
            return recipe.count == 3 && decks.Length == 3 && frame.Layout.spans.Count == 10;
        }
        internal int TestActiveMask(int stablePost) => off != null && (uint)stablePost < (uint)off.Length
            ? 15 & ~off[stablePost] : -1;
        internal string[] TestLinks(string id) => id != null && parts.TryGetValue(id, out var p)
            ? Links(p).Select(q => q.id).ToArray() : Array.Empty<string>();

        float Now => offset + Mathf.Max(0, Time.time - origin);
        float WallWidth => recipe.pitch - .68f;

        static string NameOfStyle(string id)
        {
            int i = Array.IndexOf(Styles, id);
            return i >= 0 ? StyleNames[i] : "原版陶砖";
        }

        Finish Defaults(bool jacket) => new Finish
        {
            width = jacket ? .66f : WallWidth,
            height = jacket ? JacketHeight : OriginalHeight,
            brickDepth = jacket ? JacketDepth : .23f
        };

        static Recipe Read(string json, out bool discarded)
        {
            discarded = false;
            if (string.IsNullOrWhiteSpace(json)) return new Recipe();
            try
            {
                var r = JsonUtility.FromJson<Recipe>(json);
                if (r == null || r.version != Version || (r.count != 2 && r.count != 3) ||
                    r.baseline != "original-clay" || !float.IsFinite(r.pitch) || r.pitch < 3 || r.pitch > 6 ||
                    !float.IsFinite(r.elapsed) || r.elapsed < 0 || r.moves == null)
                    throw new FormatException();

                float last = 0;
                foreach (var m in r.moves)
                {
                    if (m == null || string.IsNullOrEmpty(m.id) || !float.IsFinite(m.time) ||
                        m.time < last || (m.op != "fit" && m.op != "fiber" && m.op != "chip" && m.op != "rail"))
                        throw new FormatException();
                    if (m.op == "fit")
                    {
                        var f = m.finish;
                        if (f == null || Array.IndexOf(Styles, f.style) < 0 ||
                            Array.IndexOf(Profiles, f.profile) < 0 ||
                            !In(f.width, .10f, r.pitch - .68f) || !In(f.height, .10f, JacketHeight) ||
                            !In(f.brickWidth, .16f, 1.60f) || !In(f.brickHeight, .10f, .75f) ||
                            !In(f.brickDepth, .05f, .40f)) throw new FormatException();
                    }
                    last = m.time;
                }
                r.elapsed = Mathf.Max(last, r.elapsed);
                return r;
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException)
            {
                discarded = true;
                return new Recipe();
            }
        }

        static bool In(float v, float lo, float hi) => float.IsFinite(v) && v >= lo && v <= hi;

        protected override void Open()
        {
            ClearScene();
            recipe = Read(Saved, out bool discarded);
            kit = new CampusKit();
            // One view for both table lengths. No inspection action moves the camera.
            View(new Vector3(0, 1.65f, 0), new Vector3(12, 12, 0),
                Mathf.Max(3.65f, (1.5f * recipe.pitch + 1.4f) / Mathf.Max(.1f, Eye.aspect)), 20f);
            Build();
            note = discarded ? "旧配方不可用，已打开原版双跨。" : "单击查看；再次点同一砖片可试敲。";
            Store();
        }

        void Store()
        {
            recipe.elapsed = Now;
            recipe.selection = selected?.id ?? "span/0/brick";
            Save(JsonUtility.ToJson(recipe));
        }

        static int Pair(int a, int b) => Mathf.Min(a, b) * 16 + Mathf.Max(a, b);

        // BayFrame uses row-major native indices. Keep the original six public IDs
        // when a longer row shifts its rear indices; append only genuinely new IDs.
        int StablePost(int rawPost)
        {
            int stride = recipe.count + 1, row = rawPost / stride, col = rawPost % stride;
            return col < 3 ? row * 3 + col : 6 + row;
        }

        Part Add(string id, Kind kind, int index, int[] nodes, Transform tr = null)
        {
            var p = new Part { id = id, kind = kind, index = index, nodes = nodes, tr = tr };
            parts.Add(id, p);
            return p;
        }

        void Build()
        {
            yard = CampusKit.Node("砌体工坊", transform, Vector3.zero);
            yard.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            kit.Base(yard, recipe.count * recipe.pitch + 3.4f, recipe.pitch + 3.6f);

            // Requires the columns-aware BayFrame overload (including its shared-edge lookup).
            frame = new BayFrame(kit, yard, recipe.pitch, "FFF8E9", "D5CDC2", recipe.count);
            int n = frame.Posts.Length, count = recipe.count;
            posts = new Part[n]; jackets = new Part[n]; fibers = new Part[n, 4];
            off = new int[n]; nativePost = new int[n];
            decks = new Part[count]; rails = new Part[count]; walls = new Part[count];
            beams = new Part[frame.Girders.Length];

            var native = new Dictionary<string, Part>(StringComparer.Ordinal);
            Part Member(string prefix, Kind kind, int rawIndex, int stable, int[] nodes, Transform tr)
            {
                var p = Add(prefix + stable, kind, stable, nodes, tr);
                p.ink = tr.GetComponentsInChildren<Renderer>();
                native.Add(prefix + rawIndex, p);
                tr.name = p.id;
                return p;
            }

            for (int i = 0; i < n; ++i)
            {
                int s = StablePost(i);
                nativePost[s] = i;
                posts[s] = Member("post/", Kind.Post, i, s, new[] { s }, frame.Posts[i]);
            }

            var originalEdges = new Dictionary<int, int>();
            foreach (var e in CampusLayout.Parking(2, 2, recipe.pitch).spans)
                if (e.x < 6 && e.y < 6) originalEdges.Add(Pair(e.x, e.y), originalEdges.Count);
            int next = originalEdges.Count;
            for (int i = 0; i < beams.Length; ++i)
            {
                var e = frame.Layout.spans[i];
                int a = StablePost(e.x), b = StablePost(e.y);
                if (!originalEdges.TryGetValue(Pair(a, b), out int s)) s = next++;
                beams[s] = Member("girder/", Kind.Beam, i, s, new[] { a, b }, frame.Girders[i]);
            }

            for (int i = 0; i < count; ++i)
            {
                var nodes = frame.Layout.floors[i].Select(StablePost).ToArray();
                decks[i] = Member("deck/", Kind.Deck, i, i, nodes, frame.Panels[i]);
                string railId = "rail/" + i;
                var rail = frame.Panels[i].GetComponentsInChildren<Transform>().First(t => t.name == railId);
                rails[i] = Member("rail/", Kind.Rail, i, i, nodes, rail);
                rails[i].contacts = rail.GetComponentsInChildren<Collider>();
            }
            foreach (var c in frame.Root.GetComponentsInChildren<Collider>())
                if (frame.TryPart(c, out string id)) picks.Add(c, native[id]);

            fiberMesh = MakeCube();
            dimFiber = kit.Paint("596566");
            var fits = new Dictionary<string, Finish>(StringComparer.Ordinal);
            foreach (var m in recipe.moves) if (m.op == "fit") fits[m.id] = m.finish;

            void Install(Part p)
            {
                fits.TryGetValue(p.id, out var finish);
                InstallSkin(p, finish);
                // During replay a replacement wall begins receiving support at its fit beat.
                p.live = finish == null;
            }

            for (int s = 0; s < n; ++s)
            {
                for (int j = 0; j < 4; ++j) MakeFiber(s, j);
                jackets[s] = Add(posts[s].id + "/brick", Kind.Jacket, s, posts[s].nodes);
                Install(jackets[s]);
            }
            for (int i = 0; i < count; ++i)
            {
                int left = count + 1 + i;
                walls[i] = Add("span/" + i + "/brick", Kind.Wall, i,
                    new[] { StablePost(left), StablePost(left + 1) });
                Install(walls[i]);
            }

            for (int i = 0; i < recipe.moves.Count;)
            {
                float t = recipe.moves[i].time;
                Pose(t);
                do
                {
                    var m = recipe.moves[i++];
                    if (!parts.TryGetValue(m.id, out var p)) continue;
                    if (m.op == "fit") p.live = true;
                    else if (m.op == "fiber" && p.kind == Kind.Fiber) ChangeFiber(p, t);
                    else if (m.op == "chip" || m.op == "rail") Contact(p, m, t);
                } while (i < recipe.moves.Count && recipe.moves[i].time == t);
                Pose(t);
            }

            offset = recipe.elapsed;
            origin = Time.time;
            Pose(offset);
            Select(recipe.selection != null && parts.TryGetValue(recipe.selection, out var saved)
                ? saved : walls[0]);
            Physics.SyncTransforms();
        }

        void InstallSkin(Part p, Finish finish)
        {
            if (p.skin != null)
            {
                foreach (var c in p.skin.Colliders) picks.Remove(c);
                for (int i = 0; i < p.skin.PieceCount; ++i) parts.Remove(ChipId(p, i));
                p.skin.Dispose();
            }
            p.finish = finish;
            p.live = true;
            var spec = finish?.Spec();
            if (p.kind == Kind.Jacket)
                p.skin = BrickSkin.Column(kit, posts[p.index].tr, p.index, BayFrame.CoreHeight, spec);
            else
            {
                var a = frame.Layout.posts[nativePost[p.nodes[0]]];
                var b = frame.Layout.posts[nativePost[p.nodes[1]]];
                var clearance = (b - a).normalized * .15f;
                p.skin = BrickSkin.Wall(kit, frame.Root, a + clearance, b - clearance, p.index, spec);
            }

            p.ink = new Renderer[p.skin.PieceCount];
            for (int i = 0; i < p.ink.Length; ++i)
            {
                var tr = p.skin.Piece(i);
                var chip = Add(ChipId(p, i), Kind.Chip, i, p.nodes, tr);
                chip.owner = p;
                chip.restParent = tr.parent;
                chip.rest = tr.localPosition;
                chip.contacts = tr.GetComponents<Collider>();
                p.ink[i] = tr.GetComponent<Renderer>();
                chip.ink = new[] { p.ink[i] };
                foreach (var c in chip.contacts) picks.Add(c, chip);
            }
        }

        static string ChipId(Part p, int i) => p.id + "/chip/" + i;

        static Mesh MakeCube()
        {
            // A single shared token mesh. Box colliders only serve picking.
            var mesh = new Mesh { name = "彩纤标记" };
            mesh.vertices = new[]
            {
                new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f),
                new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f),
                new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f)
            };
            mesh.triangles = new[] { 0,2,1,0,3,2, 4,5,6,4,6,7, 0,4,7,0,7,3,
                                      1,2,6,1,6,5, 0,1,5,0,5,4, 3,7,6,3,6,2 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        void MakeFiber(int s, int j)
        {
            string id = posts[s].id + "/fiber/" + j;
            var tr = CampusKit.Node(id, posts[s].tr, new Vector3((j - 1.5f) * .083f, 1.46f, -.205f));
            tr.localScale = new Vector3(.055f, 1.38f, .016f);
            tr.gameObject.AddComponent<MeshFilter>().sharedMesh = fiberMesh;
            var r = tr.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = kit.Paint(FiberHex[j]);
            var p = fibers[s, j] = Add(id, Kind.Fiber, s, posts[s].nodes, tr);
            p.slot = j; p.ink = new Renderer[] { r };
            picks.Add(tr.gameObject.AddComponent<BoxCollider>(), p);
        }

        void Pose(float t)
        {
            frame.Pose(t);
            foreach (var w in walls)
            {
                if (!w.live) continue;
                int a = nativePost[w.nodes[0]], b = nativePost[w.nodes[1]];
                w.skin.Support(frame.Deck.Wear(a) * .5f, frame.Deck.Wear(b) * .5f, t,
                    frame.Posts[a].up, frame.Posts[b].up);
                w.skin.Pose(t);
            }
            foreach (var j in jackets) j.skin.Pose(t);
        }

        bool ChangeFiber(Part p, float t)
        {
            int s = p.index, old = off[s], mask = old | (1 << p.slot);
            if (old == mask) return false;
            off[s] = mask;
            p.ink[0].sharedMaterial = dimFiber;
            if (old == 0 || mask == 15)
            {
                int i = nativePost[s];
                var before = frame.Deck.Wear(i);
                frame.Damage(i, t);
                if (frame.Deck.Wear(i) > before) recipe.connected = true;
            }
            return true;
        }

        static Part SkinOf(Part p) => p == null ? null : p.kind == Kind.Chip ? p.owner
            : p.kind == Kind.Wall || p.kind == Kind.Jacket ? p : null;

        void Select(Part p)
        {
            selected = p;
            pickedContact = null;
            pickedCasting = false;
            var skin = SkinOf(p);
            masonry = skin != null;
            if (skin != null)
            {
                draft = skin.finish?.Copy() ?? Defaults(skin.kind == Kind.Jacket);
                float[] values = { draft.width, draft.height, draft.brickWidth, draft.brickHeight, draft.brickDepth };
                for (int i = 0; i < values.Length; ++i) raw[i] = F(values[i]);
            }
            recipe.selection = p.id;
        }

        IEnumerable<Part> Links(Part p)
        {
            foreach (int s in p.nodes) if (posts[s] != p) yield return posts[s];
            foreach (var b in beams)
            {
                bool linked = p.nodes.Length == 1 ? b.nodes.Contains(p.nodes[0])
                    : b.nodes.All(v => p.nodes.Contains(v));
                if (linked && b != p) yield return b;
            }
            foreach (var d in decks)
                if (d != p && p.nodes.All(v => d.nodes.Contains(v))) yield return d;
        }

        public override bool Tap(Vector2 screen)
        {
            if (frame == null || !Eye.pixelRect.Contains(screen)) return false;
            Pose(Now);
            Physics.SyncTransforms();
            if (!Physics.Raycast(Eye.ScreenPointToRay(screen), out var h, Eye.farClipPlane,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                !picks.TryGetValue(h.collider, out var p)) return false;

            bool repeat = selected == p;
            Select(p);
            pickedCasting = p.kind == Kind.Post && frame.TryCoreBody(h.collider, out _);
            if (p.kind == Kind.Chip || p.kind == Kind.Rail)
            {
                var tr = h.collider.transform;
                pickedContact = new Move
                {
                    op = p.kind == Kind.Chip ? "chip" : "rail", id = p.id,
                    collider = Array.IndexOf(p.contacts, h.collider),
                    point = tr.InverseTransformPoint(h.point),
                    normal = tr.InverseTransformDirection(h.normal)
                };
            }
            note = "已选 " + p.id;
            if (repeat && pickedContact != null) TryContact();
            else if (repeat && (pickedCasting || p.kind == Kind.Fiber)) SingleStep();
            return true;
        }

        bool Contact(Part p, Move m, float t)
        {
            if (p.contacts == null || (uint)m.collider >= (uint)p.contacts.Length) return false;
            var c = p.contacts[m.collider];
            if (!c || !c.enabled || !c.gameObject.activeInHierarchy) return false;
            var n = c.transform.TransformDirection(m.normal).normalized;
            var pt = c.transform.TransformPoint(m.point);
            Physics.SyncTransforms();
            if (!c.Raycast(new Ray(pt + n * .035f, -n), out var h, .075f)) return false;
            if (p.kind == Kind.Rail) return frame.TryHitRail(h, t);
            if (p.kind != Kind.Chip) return false;
            int before = p.owner.skin.Missing;
            p.owner.skin.TryHit(h, t);
            return p.owner.skin.Missing > before;
        }

        void TryContact()
        {
            if (pickedContact == null) return;
            float t = Now;
            Pose(t);
            // The cached pick can be used again without mutating an earlier journal entry.
            var m = new Move { op = pickedContact.op, id = selected.id, time = t,
                collider = pickedContact.collider, point = pickedContact.point, normal = pickedContact.normal };
            if (!Contact(selected, m, t)) { Say("该处没有新变化。"); return; }
            recipe.moves.Add(m);
            if (selected.kind == Kind.Chip) recipe.tried = true;
            Pose(t);
            Store();
            Say(selected.kind == Kind.Chip ? "已试敲；砖片可单独查看。" : "栏杆姿态已更新。");
        }

        void SingleStep()
        {
            Part f = selected;
            if (pickedCasting && selected.kind == Kind.Post)
            {
                int j = 0;
                while (j < 4 && (off[selected.index] & (1 << j)) != 0) ++j;
                if (j == 4) { Say("此柱的四束彩纤均已停用。"); return; }
                f = fibers[selected.index, j];
            }
            if (f.kind == Kind.Fiber) Commit(new[] { 4 * f.index + f.slot });
        }

        void Commit(IEnumerable<int> keys)
        {
            float t = Now;
            Pose(t);
            int count = 0;
            foreach (int key in keys.ToArray())
            {
                var f = fibers[key / 4, key % 4];
                marked.Remove(key);
                if (!ChangeFiber(f, t)) continue;
                recipe.moves.Add(new Move { op = "fiber", id = f.id, time = t });
                ++count;
            }
            Pose(t);
            Store();
            Say(count == 0 ? "没有待改动的彩纤。" : $"同一拍已改动 {count} 束彩纤。");
        }

        static string F(float v) => v.ToString("0.###", Inv);
        static bool Number(string s, out float v) =>
            float.TryParse(s, NumberStyles.Float, Inv, out v) && float.IsFinite(v);
        float Min(int i) => i == 2 ? .16f : i == 4 ? .05f : .10f;
        float Max(int i) => i == 0 ? WallWidth : i == 1 ? JacketHeight : i == 2 ? 1.60f : i == 3 ? .75f : .40f;

        void ApplyFit()
        {
            var p = SkinOf(selected);
            if (p == null) return;
            var v = new float[5];
            for (int i = 0; i < v.Length; ++i)
                if (!Number(raw[i], out v[i]) || !In(v[i], Min(i), Max(i)))
                {
                    Say($"{Fields[i]}须在 {F(Min(i))} 至 {F(Max(i))} 之间。");
                    return;
                }
            var f = draft.Copy();
            f.width = v[0]; f.height = v[1]; f.brickWidth = v[2]; f.brickHeight = v[3]; f.brickDepth = v[4];
            f.Spec().Validate();
            Part[] targets = !allMatching ? new[] { p } : p.kind == Kind.Jacket ? jackets : walls;
            float t = Now;
            Pose(t);
            foreach (var target in targets)
            {
                string prefix = target.id + "/chip/";
                recipe.moves.RemoveAll(m => m.id == target.id || m.id.StartsWith(prefix, StringComparison.Ordinal));
                InstallSkin(target, f.Copy());
                recipe.moves.Add(new Move { op = "fit", id = target.id, time = t, finish = target.finish });
            }
            recipe.applied = true;
            Select(p);
            Pose(t);
            Store();
            Say($"已重铺 {targets.Length} 处；其他构件保持原状。");
        }

        void Extend()
        {
            if (recipe.count != 2) return;
            Store();
            recipe.count = 3;
            var pending = marked.ToArray();
            ClearGeometry();
            Build();
            foreach (int key in pending) marked.Add(key);
            Store();
            Say("已增设一跨；相邻楼板共用原边柱与边梁。");
        }

        void Say(string message)
        {
            note = message;
            Result(message);
        }

        public override void Panel()
        {
            if (frame == null) return;
            if (pickedContact != null && ActionButton(selected.kind == Kind.Chip ? "试敲砖片" : "轻推栏杆"))
                TryContact();
            if ((pickedCasting || selected.kind == Kind.Fiber) && ActionButton("改动一束")) SingleStep();
            if (marked.Count != 0 && ActionButton("同拍提交 · " + marked.Count)) Commit(marked);
            if (recipe.count == 2 && ActionButton("增设一跨")) Extend();
        }

        static void Drop(Object o) { if (o) Object.Destroy(o); }

        void ClearGeometry()
        {
            if (yard) yard.gameObject.SetActive(false);
            if (walls != null) foreach (var p in walls) p?.skin?.Dispose();
            if (jackets != null) foreach (var p in jackets) p?.skin?.Dispose();
            if (yard) Drop(yard.gameObject);
            Drop(fiberMesh);
            frame = null; yard = null; fiberMesh = null;
            selected = null; pickedContact = null; pickedCasting = false;
            posts = beams = decks = rails = walls = jackets = null;
            fibers = null; nativePost = off = null;
            parts.Clear(); picks.Clear(); marked.Clear();
        }

        void ClearScene()
        {
            ClearGeometry();
            kit?.Dispose(); kit = null;
            Drop(seams); seams = null;
            Drop(font); font = null;
            text = small = input = null;
            // BayBrickMaterials owns its process-wide cache; it isn't chapter-local.
        }

        public override void Suspend()
        {
            if (frame != null) Store();
        }

        void OnDestroy() => ClearScene();

        void GuiAssets()
        {
            if (text != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "PingFang SC",
                "Noto Sans CJK SC", "Microsoft YaHei", "Arial" }, 18);
            text = new GUIStyle(GUI.skin.label) { font = font ? font : GUI.skin.font, fontSize = 17,
                alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0), richText = false };
            small = new GUIStyle(text) { fontSize = 14 };
            input = new GUIStyle(GUI.skin.textField) { font = text.font, fontSize = 16,
                alignment = TextAnchor.MiddleCenter };
            seams = MakeSeamAtlas();
        }

        // StyledBrickSpan.Seam is private. This is its normalized bottom-to-top
        // polyline table, used only to rasterize one atlas (no preview GameObjects).
        static Texture2D MakeSeamAtlas()
        {
            Vector2[][] lines = {
                new[] { new Vector2(.39f,0), new Vector2(.57f,.5f), new Vector2(.59f,1) },
                new[] { new Vector2(.24f,0), new Vector2(.76f,1) },
                new[] { new Vector2(.74f,0), new Vector2(.33f,.46f), new Vector2(.67f,1) },
                new[] { new Vector2(.38f,0), new Vector2(.63f,.29f), new Vector2(.35f,.62f), new Vector2(.56f,1) }
            };
            const int w = 96, h = 40;
            var pixels = new Color32[4 * w * h];
            var left = new Color32(224, 161, 120, 255);
            var right = new Color32(246, 205, 161, 255);
            for (int k = 0; k < 4; ++k)
                for (int y = 0; y < h; ++y)
                {
                    float v = (y + .5f) / h;
                    var line = lines[k];
                    int j = 1;
                    while (j + 1 < line.Length && line[j].y < v) ++j;
                    float split = Mathf.Lerp(line[j - 1].x, line[j].x,
                        Mathf.InverseLerp(line[j - 1].y, line[j].y, v));
                    for (int x = 0; x < w; ++x)
                    {
                        float u = (x + .5f) / w;
                        if (u <= split * .89f) pixels[y * 4 * w + k * w + x] = left;
                        else if (u >= .11f + split * .89f) pixels[y * 4 * w + k * w + x] = right;
                    }
                }
            var tex = new Texture2D(w * 4, h, TextureFormat.RGBA32, false)
                { name = "四式断面", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels); tex.Apply(false, true);
            return tex;
        }

        static Rect R(float x, float y, float w, float h) => new Rect(x, y, w, h);
        static void Fill(Rect r, Color color) => LabGame.Fill(r, color);

        void Label(Rect r, string s, Color color, bool compact = false, bool center = false)
        {
            GUIStyle st = compact ? small : text;
            st.normal.textColor = color;
            st.alignment = center ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            GUI.Label(r, s, st);
        }

        bool Button(Rect r, string s, bool chosen = false)
        {
            bool click = GUI.Button(r, GUIContent.none, GUIStyle.none);
            Fill(r, chosen ? Chosen : Tile);
            Label(r, s, Ink, true, true);
            return click;
        }

        string Status(Part p)
        {
            switch (p.kind)
            {
                case Kind.Chip:
                    bool moved = p.tr.parent != p.restParent || (p.tr.localPosition - p.rest).sqrMagnitude > 1e-10f;
                    return (moved ? "已移位砖片" : "外饰砖片") + "   ·   本面组已脱离 " + p.owner.skin.Missing + " 片";
                case Kind.Post: case Kind.Fiber:
                    return "柱芯状态   " + (off[p.index] == 0 ? "完整" : off[p.index] == 15 ? "已释放" : "已开裂")
                        + (pickedCasting ? "   ·   已点中裸露芯体" : "");
                case Kind.Beam:
                    return Links(p).Count(q => q.kind == Kind.Deck) > 1 ? "共用梁   ·   两侧楼板引用同一构件" : "边梁   ·   关联构件见下方";
                case Kind.Deck: return "楼板   ·   铺面与封边沿用车台原材";
                case Kind.Rail: return "白漆铁栏   ·   弯曲量 " + F(frame.RailBend(p.index));
                default: return (p.kind == Kind.Jacket ? "四面柱皮" : "后排填充墙") + "   ·   " + p.skin.BrickCount + " 块砖";
            }
        }

        public override void Overlay()
        {
            if (frame == null || selected == null) return;
            GuiAssets();
            if (Event.current.type == EventType.Repaint) Pose(Now);
            DrawHighlight();

            GUI.BeginGroup(R(955, 104, 485, 688));
            try
            {
                Fill(R(0, 0, 485, 688), Paper);
                Label(R(18, 7, 450, 24), $"材料工坊    /    {recipe.count} 跨     ·     {posts.Length} 柱", Muted, true);
                Label(R(18, 34, 450, 28), selected.id, Accent);
                Label(R(18, 64, 450, 24), Status(selected), Ink, true);
                if (Button(R(18, 98, 218, 31), "砌体配方", masonry))
                {
                    if (SkinOf(selected) == null)
                    {
                        if (selected.nodes.Length == 1) Select(jackets[selected.nodes[0]]);
                        else Select(walls[Links(selected).FirstOrDefault(q => q.kind == Kind.Deck)?.index
                            ?? (selected.kind == Kind.Deck ? selected.index : 0)]);
                    }
                    masonry = true;
                }
                if (Button(R(243, 98, 224, 31), "构件连接", !masonry)) masonry = false;
                if (masonry) DrawMasonry(); else DrawConnections();
                Fill(R(18, 641, 449, 1), Tile);
                Label(R(18, 651, 449, 26), note ?? Hint, Muted, true);
            }
            finally { GUI.EndGroup(); }
        }

        void CycleSkin(Part p, int delta)
        {
            int n = walls.Length + jackets.Length;
            int i = p.kind == Kind.Wall ? p.index : walls.Length + p.index;
            i = (i + delta + n) % n;
            Select(i < walls.Length ? walls[i] : jackets[i - walls.Length]);
        }

        void DrawMasonry()
        {
            var p = SkinOf(selected);
            if (p == null) return;
            Label(R(18, 143, 348, 24), "配方对象    " + p.id, Ink);
            if (Button(R(367, 140, 46, 29), "上一面")) { CycleSkin(p, -1); return; }
            if (Button(R(417, 140, 50, 29), "下一面")) { CycleSkin(p, 1); return; }
            Label(R(18, 171, 447, 23), p.finish == null ? "已装：原版装饰砖" :
                $"已装：{NameOfStyle(p.finish.style)}   /   断面 {p.finish.profile}", Muted, true);

            Label(R(18, 201, 78, 28), "草稿面材", Ink, true);
            for (int i = 0; i < Styles.Length; ++i)
                if (Button(R(101 + 123 * i, 200, 117, 29), StyleNames[i], draft.style == Styles[i]))
                    draft.style = Styles[i];

            var map = BayBrickMaterials.ForStyle(draft.style).GetTexture("_BaseMap");
            GUI.DrawTextureWithTexCoords(R(18, 239, 118, 38), map, R(0, 0, 1, .55f));
            Label(R(149, 239, 316, 38), "面材预览   /   " + NameOfStyle(draft.style), Muted, true);

            Label(R(18, 285, 449, 24), "断面式样    /    两片分开展示", Ink, true);
            for (int i = 0; i < 4; ++i)
            {
                var r = R(18 + i * 114, 314, 107, 55);
                if (Button(r, "", Profiles[i] == draft.profile)) draft.profile = Profiles[i];
                GUI.DrawTextureWithTexCoords(R(r.x + 27, r.y + 10, 72, 34), seams, R(i * .25f, 0, .25f, 1));
                Label(R(r.x + 5, r.y, 18, r.height), Profiles[i], Ink, true, true);
            }

            bool jacket = p.kind == Kind.Jacket;
            Label(R(18, 378, 449, 22), jacket ? "柱皮按四面满铺；宽、高、厚随柱面固定。" :
                "尺寸   /   台面格值；边砖自动裁齐。", Muted, true);
            for (int i = 0; i < 5; ++i)
            {
                float y = 405 + 31 * i;
                bool fixedSize = jacket && (i < 2 || i == 4);
                Label(R(18, y, 95, 26), Fields[i], Ink, true);
                if (fixedSize)
                {
                    Label(R(114, y, 144, 26), raw[i], Muted, true, true);
                    Label(R(269, y, 196, 26), i == 0 ? "正面宽；侧面自动收口" : "随柱面", Muted, true);
                    continue;
                }
                raw[i] = GUI.TextField(R(114, y, 144, 26), raw[i], 16, input);
                int sign = Button(R(267, y, 50, 26), "−") ? -1 : 0;
                if (Button(R(322, y, 50, 26), "+")) sign = 1;
                if (sign != 0)
                {
                    Number(raw[i], out float v);
                    if (!float.IsFinite(v)) v = Min(i);
                    raw[i] = F(Mathf.Clamp(v + sign * Steps[i], Min(i), Max(i)));
                    GUI.FocusControl(null);
                }
                Label(R(381, y, 86, 26), "步长 " + F(Steps[i]), Muted, true);
            }

            if (Button(R(18, 569, 218, 29), jacket ? "当前柱皮" : "当前墙面", !allMatching)) allMatching = false;
            if (Button(R(243, 569, 224, 29), jacket ? "全部柱皮" : "全部后墙", allMatching)) allMatching = true;
            if (Button(R(18, 606, 449, 29), "应用并保存配方", true)) ApplyFit();
        }

        void DrawConnections()
        {
            var p = selected;
            Label(R(18, 144, 449, 24), "连接查看    /    " + p.id, Ink);
            if (p.nodes.Length == 1)
            {
                int s = p.nodes[0];
                int remaining = 0;
                for (int slot = 0; slot < 4; ++slot)
                    if ((off[s] & (1 << slot)) == 0) remaining++;
                Label(R(153, 174, 300, 28), $"柱芯剖示   ·   仍连接 {remaining} / 4 束", Ink);
                var core = new Color32(184, 170, 151, 255);
                Fill(R(18, 177, 114, 104), Tile);
                Fill(R(22, 181, 106, 22), core);
                Fill(R(22, 255, 106, 22), core);
                for (int j = 0; j < 4; ++j)
                {
                    var f = fibers[s, j];
                    float x = 155 + j * 77;
                    bool active = (off[s] & (1 << j)) == 0;
                    if (Button(R(x, 205, 70, 36), "束 " + (j + 1) + (marked.Contains(4 * s + j) ? " ●" : ""), selected == f))
                        Select(f);
                    ColorUtility.TryParseHtmlString("#" + FiberHex[j], out var color);
                    Fill(R(x, 237, 70, 4), active ? color : Color.gray);
                    float sectionX = 35 + 25 * j;
                    if (active) Fill(R(sectionX, 196, 5, 66), color);
                    else
                    {
                        Fill(R(sectionX, 196, 5, 22), Color.gray);
                        Fill(R(sectionX, 240, 5, 22), Color.gray);
                    }
                }
                var current = selected;
                if (current.kind == Kind.Fiber && (off[s] & (1 << current.slot)) == 0)
                {
                    int key = s * 4 + current.slot;
                    if (Button(R(155, 251, 148, 34), marked.Contains(key) ? "撤销标记" : "加入同拍"))
                    {
                        if (!marked.Add(key)) marked.Remove(key);
                    }
                    if (Button(R(312, 251, 151, 34), "立即改动此束")) SingleStep();
                }
            }
            else
            {
                Label(R(18, 179, 449, 26), "下方列出实际相连的柱、梁与楼板。", Muted, true);
                Label(R(18, 210, 449, 26), "选柱可查看四束彩纤；也可直接点场景构件。", Muted, true);
            }

            Label(R(18, 299, 449, 25), "相连构件", Ink);
            int k = 0;
            foreach (var q in Links(selected).ToArray())
            {
                if (Button(R(18 + k % 4 * 114, 333 + k / 4 * 37, 107, 31), q.id)) Select(q);
                ++k;
            }
            Label(R(18, 465, 449, 25), "柱位索引", Ink);
            for (int i = 0; i < posts.Length; ++i)
                if (Button(R(18 + i % 4 * 114, 499 + i / 4 * 34, 107, 28), posts[i].id, selected == posts[i]))
                    Select(posts[i]);
            Label(R(18, 594, 449, 28), $"试砖 {(recipe.tried ? "✓" : "—")}     配方 {(recipe.applied ? "✓" : "—")}     连接 {(recipe.connected ? "✓" : "—")}",
                Accent, true);
        }

        void DrawHighlight()
        {
            if (Event.current.type != EventType.Repaint || selected.ink == null) return;
            bool any = false;
            Bounds box = default;
            foreach (var r in selected.ink)
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (any) box.Encapsulate(r.bounds); else { box = r.bounds; any = true; }
            }
            if (!any) return;
            var lo = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var hi = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; ++i)
            {
                var e = box.extents;
                var sp = Eye.WorldToScreenPoint(box.center + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
                if (sp.z <= 0) return;
                var pt = GUIUtility.ScreenToGUIPoint(new Vector2(sp.x, Screen.height - sp.y));
                lo = Vector2.Min(lo, pt); hi = Vector2.Max(hi, pt);
            }
            GUI.BeginGroup(R(0, 104, 955, 688));
            try
            {
                float x = lo.x - 3, y = lo.y - 107, w = hi.x - lo.x + 6, h = hi.y - lo.y + 6;
                float dx = Mathf.Min(w * .45f, 13), dy = Mathf.Min(h * .45f, 13);
                for (int i = 0; i < 4; ++i)
                {
                    bool right = (i & 1) != 0, bottom = (i & 2) != 0;
                    Fill(R(right ? x + w - dx : x, bottom ? y + h - 2 : y, dx, 2), Accent);
                    Fill(R(right ? x + w - 2 : x, bottom ? y + h - dy : y, 2, dy), Accent);
                }
                float tagWidth = Mathf.Clamp(selected.id.Length * 9 + 16, 92, 350);
                var tag = R(Mathf.Clamp(x, 0, 955 - tagWidth), Mathf.Clamp(y - 25, 0, 662), tagWidth, 23);
                Fill(tag, Paper); Label(tag, selected.id, Accent, true, true);
            }
            finally { GUI.EndGroup(); }
        }
    }
}
