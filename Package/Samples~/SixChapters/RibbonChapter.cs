using System;
using System.Collections.Generic;
using UnityEngine;
using static System.FormattableString;

namespace MaterialDestruction.Sample
{
    internal sealed class RibbonChapter : global::LabChapter
    {
        private const int Tearable = RibbonTear.Bundles - RibbonTear.Starter;
        private const float OpeningPerSecond = .032f; // About eight seconds for an uninterrupted strip.
        private readonly List<RibbonStroke> strokes = new List<RibbonStroke>();
        private readonly List<Material> materials = new List<Material>();

        private RibbonTear tear;
        private RibbonSkin skin;
        private Shader tintShader;
        private float strokeSeconds;
        private double pausedAt = -1;
        private bool earlierPause, complete, redraw;
        private bool hand;
        private Vector2 grip;
        private float handLift, handSlide;

        private bool Pulling => tear != null && tear.Side != 0;
        public override bool Complete => complete;

        private static float CrackCm(int broken) =>
            100f * RibbonTear.Length * (RibbonTear.Starter + broken) / RibbonTear.Bundles;

        // A pause must leave an actual, intermediate advance to resume.
        private static bool Intermediate(RibbonStroke s) => s.after > s.before && s.after < Tearable;

        public override string Hint
        {
            get
            {
                if (skin == null) return "等待织带就绪。";
                if (complete) return "已分次拉到夹口，可进入下一章。";
                if (tear.Split) return "已一次拉到底；请重开本章，裂口前进后先松手一次。";
                if (Pulling)
                {
                    if (hand) return $"已撕开 {100f * tear.Broken / Tearable:F0}% · 松开鼠标或抬起手指，裂口就停在这里。";
                    string action = earlierPause ? "继续拉到夹口，或点布带松手。" : "裂口前进后，点布带或「松手」暂停。";
                    return Invariant($"已开 {100f * tear.Broken / Tearable:F0}% · 本次 {strokeSeconds:F1} 秒｜{action}");
                }
                if (earlierPause)
                    return Invariant($"已松手 · 裂长 {CrackCm(tear.Broken):F1} 厘米｜再抓任一布角继续拖。");
                return "按住右端一角向外拖，撕到一半松手，再抓另一角。轻点布角也可以自动拉。";
            }
        }

        protected override void Open()
        {
            var weave = Resources.Load<Shader>("RibbonWeave");
            tintShader = Resources.Load<Shader>("WoodWorkbench");
            if (!weave || !tintShader)
            {
                Result("织带未就绪：缺少 RibbonWeave 或 WoodWorkbench 着色器。");
                return;
            }

            tear = new RibbonTear();
            RestoreTake();
            skin = new RibbonSkin(tear); // Rest shape, independent of the last recorded peak.

            var textile = new Material(weave);
            materials.Add(textile);
            PutMesh("条纹织带", skin.Surface, textile);
            PutMesh("纬纱线头", skin.Yarns, Tint(Color.white));
            PutMesh("织带投影", skin.Surface, Tint(new Color(.65f, .61f, .53f)), new Vector3(.002f, -.005f, .039f));
            BuildDesk();

            // Look along the local +Z axis. Leave Eye.rect entirely to the shell.
            var scale = transform.lossyScale;
            float size = Mathf.Max(.31f * Mathf.Abs(scale.y),
                .32f * Mathf.Abs(scale.x) / Mathf.Max(.01f, Eye.aspect));
            View(transform.TransformPoint(new Vector3(-.03f, -.027f, 0)), transform.eulerAngles, size, 1f);
            Result(Invariant($"织带就绪：{tear.Broken}/{Tearable} 束，裂长 {CrackCm(tear.Broken):F2} 厘米（含预口），已松手。"));
        }

        private void RestoreTake()
        {
            string json = Saved;
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                var take = JsonUtility.FromJson<RibbonTake>(json);
                if (take == null) throw new ArgumentException("空记录。");
                int expected = take.Replay(); // Validates version and each before/after pair.

                // Replay returns a length, not a live model. Rebuild through the same
                // public operations so neither a grab nor residual load crosses scenes.
                var restored = new RibbonTear();
                foreach (RibbonStroke s in take.grips)
                {
                    restored.Grab(s.side);
                    restored.Open(s.peakOpening);
                    restored.Release();
                }
                if (restored.Broken != expected) throw new InvalidOperationException("裂长不符。");

                tear = restored;
                strokes.AddRange(take.grips);
                earlierPause = strokes.Exists(Intermediate);
                complete = tear.Split && earlierPause;
                Result(Invariant($"织带回放：{strokes.Count} 次松手，重现 {expected} 束；中途暂停={earlierPause}，达标={complete}。"));
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Result("织带记录无法回放，改用新织带：" + e.Message);
            }
        }

        private Material Tint(Color color)
        {
            var m = new Material(tintShader);
            m.SetColor("_Tint", color);
            materials.Add(m);
            return m;
        }

        private void PutMesh(string label, Mesh mesh, Material material, Vector3 offset = default)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void Solid(string label, Vector3 p, Vector3 size, Material material,
            PrimitiveType shape = PrimitiveType.Cube, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = label;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = p;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
        }

        private void BuildDesk()
        {
            Solid("工作垫", new Vector3(-.03f, -.027f, .10f), new Vector3(.79f, .68f, .018f),
                Tint(new Color(.88f, .86f, .80f)));
            var jaw = Tint(new Color(.39f, .43f, .42f));
            Solid("夹座", new Vector3(-.265f, .037f, .03f), new Vector3(.071f, .18f, .027f), jaw);
            Solid("夹面", new Vector3(-.262f, .037f, -.023f), new Vector3(.062f, .169f, .016f),
                Tint(new Color(.52f, .56f, .53f)));
            Solid("夹唇", new Vector3(-.233f, .037f, -.033f), new Vector3(.006f, .159f, .004f), jaw);
            var metal = Tint(new Color(.31f, .35f, .34f));
            var slot = Tint(new Color(.15f, .19f, .18f));
            foreach (float y in new[] { .101f, -.026f })
            {
                Solid("螺钉", new Vector3(-.264f, y, -.035f), new Vector3(.014f, .003f, .014f),
                    metal, PrimitiveType.Cylinder, new Vector3(90, 0, 0));
                Solid("螺钉槽", new Vector3(-.264f, y, -.039f), new Vector3(.008f, .0015f, .001f), slot);
            }
        }

        private void BeginStroke(int side)
        {
            if (skin == null || Pulling || tear.Split) return;
            tear.Grab(side); // Opening is relative to this grip; Broken remains cumulative.
            strokeSeconds = 0;
            if (skin.ActiveSide != side) skin.Lift = skin.Slide = 0;
            skin.ActiveSide = side;
            redraw = true;

            string corner = side == 1 ? "上角" : "下角";
            if (pausedAt >= 0)
                Result(Invariant($"续拉{corner}：停留 {Time.realtimeSinceStartupAsDouble - pausedAt:F2} 秒，起始裂长 {CrackCm(tear.Broken):F2} 厘米。"));
            else
                Result(Invariant($"牵拉{corner}：起始裂长 {CrackCm(tear.Broken):F2} 厘米。"));
            pausedAt = -1;
        }

        private void EndStroke()
        {
            hand = false;
            if (!Pulling) return;
            RibbonStroke receipt = tear.Release();
            strokes.Add(receipt);
            earlierPause |= Intermediate(receipt);
            complete |= tear.Split && earlierPause;
            pausedAt = tear.Split ? -1 : Time.realtimeSinceStartupAsDouble;
            redraw = true;

            Save(JsonUtility.ToJson(new RibbonTake { grips = strokes.ToArray() }));
            string action = tear.Split ? "到达夹口" : "暂停";
            Result(Invariant($"织带{action}：{receipt.before}→{receipt.after}/{Tearable} 束，裂长 {CrackCm(receipt.after):F2} 厘米；本次 {strokeSeconds:F2} 秒，峰值 {receipt.peakOpening * 1000f:F1} 毫米。"));
            if (tear.Split)
                Result(complete ? "实际结果：已分次拉开整条织带，达标。" : "实际结果：整条织带已分开，但没有中途暂停；需重开本章。");
        }

        private void Update()
        {
            if (skin == null) return;
            bool active = Pulling;
            if (!active && !redraw && skin.Lift == 0 && skin.Slide == 0) return;

            // Do not turn a suspended application's first long frame into a whole stroke.
            float dt = Mathf.Min(Time.unscaledDeltaTime, .25f);
            float lift = 0, slide = 0;
            if (active)
            {
                strokeSeconds += dt;
                float opening = hand ? Mathf.Abs(handLift) : strokeSeconds * OpeningPerSecond;
                if (!hand) tear.Open(opening);
                float travel = Mathf.Clamp01(opening / (8f * OpeningPerSecond));
                lift = tear.Side * .16f * travel;
                slide = -.05f * travel;
                if (hand) { lift = handLift; slide = handSlide; }
            }

            float blend = 1f - Mathf.Exp(-(active ? 23f : 8f) * dt);
            skin.Lift = Mathf.Lerp(skin.Lift, lift, blend);
            skin.Slide = Mathf.Lerp(skin.Slide, slide, blend);
            if (!active && Mathf.Abs(skin.Lift) + Mathf.Abs(skin.Slide) < .00001f)
                skin.Lift = skin.Slide = 0;
            skin.Update();
            redraw = false;
            if (active && tear.Split) EndStroke();
        }

        private int HitSide(Vector2 screen)
        {
            var ray = Eye.ScreenPointToRay(screen);
            var plane = new Plane(transform.forward, transform.position);
            if (!plane.Raycast(ray, out float distance)) return 0;
            Vector2 p = transform.InverseTransformPoint(ray.GetPoint(distance));

            // In the fixed orthographic front view each transverse row projects to
            // a vertical segment. Sample the live rows, not the strip's rest bounds.
            Vector2 a = Eye.WorldToScreenPoint(transform.position);
            Vector2 b = Eye.WorldToScreenPoint(transform.TransformPoint(Vector3.right * RibbonTear.Length));
            float pad = Mathf.Clamp(7f * RibbonTear.Length / Mathf.Max(1f, Vector2.Distance(a, b)),
                RibbonTear.Length / RibbonTear.Bundles, .018f);
            float best = pad * pad;
            int hit = 0;
            int first = Pulling ? 2 : Mathf.CeilToInt(.68f * RibbonTear.Bundles);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = first; i <= RibbonTear.Bundles; i++)
                {
                    float u = (float)i / RibbonTear.Bundles;
                    Vector3 seam = skin.Point(u, 0, side);
                    float edgeY = skin.Point(u, 1, side).y;
                    float dy = p.y - Mathf.Clamp(p.y, Mathf.Min(seam.y, edgeY), Mathf.Max(seam.y, edgeY));
                    float dx = p.x - seam.x;
                    float d2 = dx * dx + dy * dy;
                    if (d2 < best) { best = d2; hit = side; }
                }
            }
            return hit;
        }

        public override bool Tap(Vector2 screen)
        {
            if (skin == null || tear.Split || !Eye.pixelRect.Contains(screen)) return false;
            int side = HitSide(screen);
            if (side == 0) return false;
            if (Pulling) EndStroke();
            else BeginStroke(side);
            return true;
        }

        Vector2 ClothPoint(Vector2 screen)
        {
            var ray = Eye.ScreenPointToRay(screen);
            var plane = new Plane(transform.forward, transform.position);
            plane.Raycast(ray, out float along);
            return transform.InverseTransformPoint(ray.GetPoint(along));
        }

        public override bool StartDrag(Vector2 origin)
        {
            if (skin == null || tear.Split) return false;
            int side = HitSide(origin);
            if (side == 0) return false;
            if (Pulling) EndStroke();
            grip = ClothPoint(origin);
            handLift = handSlide = 0;
            BeginStroke(side);
            hand = true;
            return true;
        }

        public override void MoveDrag(Vector2 point)
        {
            if (!hand || !Pulling) return;
            Vector2 delta = ClothPoint(point) - grip;
            float outward = Mathf.Max(0, tear.Side * delta.y);
            handLift = tear.Side * Mathf.Min(outward, .23f);
            handSlide = Mathf.Clamp(delta.x, -.09f, .045f);
            tear.Open(outward + Mathf.Max(0, -handSlide) * .22f);
            if (tear.Split) EndStroke();
        }

        public override void EndDrag() { if (hand) EndStroke(); }

        public override void Panel()
        {
            bool enabledBefore = GUI.enabled;
            bool up, down, stop;
            try
            {
                GUI.enabled = enabledBefore && skin != null && !Pulling && !tear.Split;
                up = ActionButton("拉上角");
                down = ActionButton("拉下角");
                GUI.enabled = enabledBefore && Pulling;
                stop = ActionButton("松手");
            }
            finally { GUI.enabled = enabledBefore; }

            if (stop) EndStroke();
            else if (up || down) BeginStroke(up ? 1 : -1);
        }

        public override void Suspend()
        {
            if (Pulling) EndStroke();
        }

        internal int Broken => tear.Broken;
        internal bool IsPulling => Pulling;
        internal Vector2 EarScreen(int side) => Eye.WorldToScreenPoint(transform.TransformPoint(skin.Point(.98f, .55f, side)));
        public override void Overlay()
        {
            if (skin == null || Pulling || tear.Split) return;
            Cue(EarScreen(1), "向上拖", new Vector2(21, -35));
            Cue(EarScreen(-1), "向下拖", new Vector2(21, 13));
        }

        private void OnDestroy()
        {
            // Deliberately do not commit a grip interrupted by leaving the chapter.
            if (skin != null)
            {
                Destroy(skin.Surface);
                Destroy(skin.Yarns);
            }
            foreach (var material in materials) Destroy(material);
        }
    }
}
