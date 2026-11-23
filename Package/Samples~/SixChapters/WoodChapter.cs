using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Sample
{
    internal sealed class WoodChapter : global::LabChapter
    {
        private float contact = .91f;
        private const int SettleTicks = 120;
        private static readonly float[] Loads = { 0f, -.32f, -.78f, -1.06f, -1.34f };

        // The observation card survives changing desks. A new specimen does not
        // inherit wear from a different specimen, nor erase an observation.
        [Serializable]
        private sealed class ObservationCard
        {
            public int version = 1;
            public int phase; // 1: connected under load, 2: cracked, 3: detached
            public bool sawCrackedRebound;
        }

        private ObservationCard card = new ObservationCard();
        private Transform desk, lowerProbe;
        private LabBench bench;
        private Material wood;
        private readonly HashSet<Material> deskMaterials = new HashSet<Material>();

        private BranchModel model;
        private BranchSkin stumpSkin, tipSkin;
        private GameObject tipObject;
        private Rigidbody fragment;
        private readonly Vector3[] previous = new Vector3[BranchModel.Links + 1];
        private Vector3 restTip;
        private float releaseHeight;
        private bool reportedFall;

        private int level, pendingLevel, ticksAtLevel;
        private bool advancing, inspecting;
        private double accumulator;

        public override bool Complete => card.phase == 3;

        public override string Hint
        {
            get
            {
                if (model == null) return "找到枝梢旁的橙色触点。";
                if (model.Detached) return "枝梢已脱落。可进入下一章，或换新枝复看。";
                if (inspecting) return $"首裂停看：仍有 {BranchModel.Fibers - model.Broken} 根纤维相连。松开看回弹，或再弯一点。";
                if (level == 0 && model.Broken > 0)
                    return $"已卸力，{model.Broken} 根纤维仍断开。点枝梢再次弯折。";
                if (level == 0)
                    return "点树枝选择施力位置，试试夹口旁和枝梢。再点同一处可以加力。";
                if (level == Loads.Length - 1) return "保持当前弯度观察纤维，或点「松开」卸力。";
                return model.Broken > 0 ? "裂处仍相连。再点枝梢加弯，或松开。" : "枝梢仍相连。再点枝梢加弯，或松开看回弹。";
            }
        }

        protected override void Open()
        {
            string json = Saved;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var saved = JsonUtility.FromJson<ObservationCard>(json);
                    if (saved != null && saved.version == 1 && saved.phase >= 0 && saved.phase <= 3)
                        card = saved;
                }
                catch (ArgumentException)
                {
                    Result("观察卡无法读取，本次从新枝开始。");
                }
            }

            desk = new GameObject("木枝模型台").transform;
            desk.SetParent(transform, false);
            bench = new LabBench(desk);
            // Only the cameras just created by LabBench belong to this chapter.
            foreach (Camera c in desk.GetComponentsInChildren<Camera>(true))
            {
                c.enabled = false;
                c.targetTexture = null;
                c.gameObject.SetActive(false);
            }

            wood = bench.Material(Color.white);
            stumpSkin = new BranchSkin();
            tipSkin = new BranchSkin();
            MakeSkin("夹持段", stumpSkin);

            // A pair of contact marks, not physical colliders pushing the mesh.
            bench.Probe.localScale = Vector3.one * .032f;
            lowerProbe = new GameObject("枝梢下标点").transform;
            lowerProbe.SetParent(desk, false);
            lowerProbe.localScale = Vector3.one * .024f;
            lowerProbe.gameObject.AddComponent<MeshFilter>().sharedMesh =
                bench.Probe.GetComponent<MeshFilter>().sharedMesh;
            lowerProbe.gameObject.AddComponent<MeshRenderer>().sharedMaterial =
                bench.Material(new Color(.16f, .45f, .44f));

            foreach (Renderer r in desk.GetComponentsInChildren<Renderer>(true))
                deskMaterials.Add(r.sharedMaterial);

            float aspect = Mathf.Max(1, Eye.pixelWidth) / (float)Mathf.Max(1, Eye.pixelHeight);
            Vector3 s = desk.lossyScale;
            float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            Quaternion aim = desk.rotation * Quaternion.Euler(10f, -5f, 0f);
            View(desk.TransformPoint(new Vector3(.30f, -.12f, .015f)), aim.eulerAngles,
                scale * Mathf.Max(.39f, .57f / aspect), scale * 2f);

            if (card.phase > 0)
            {
                string last = Complete ? "枝梢脱落" : card.phase == 2 ? "纤维开裂" : "弯曲相连";
                Result("沿用观察卡，已见「" + last + "」" +
                       (card.sawCrackedRebound ? "及带裂回弹" : "") + "；本次取新模型。");
            }
            NewSpecimen();
        }

        private GameObject MakeSkin(string label, BranchSkin skin)
        {
            var go = new GameObject(label);
            go.transform.SetParent(desk, false);
            go.AddComponent<MeshFilter>().sharedMesh = skin.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = wood;
            return go;
        }

        private void NewSpecimen()
        {
            // Do not reuse a transform that BranchFragment has given to physics.
            if (tipObject)
            {
                tipObject.SetActive(false);
                Destroy(tipObject);
            }
            fragment = null;
            tipObject = MakeSkin("自由段", tipSkin);
            tipObject.SetActive(false);

            model = new BranchModel(new BranchProfile());
            contact = .91f;
            restTip = model.Centers[BranchModel.Links];
            level = pendingLevel = ticksAtLevel = 0;
            advancing = inspecting = reportedFall = false;
            accumulator = 0;
            Refresh();
            Result($"新枝已夹好：{BranchModel.Fibers} 根模型纤维全部相连。");
        }

        private void Remember(int phase, bool rebound = false)
        {
            if (phase <= card.phase && (!rebound || card.sawCrackedRebound)) return;
            card.phase = Math.Max(card.phase, phase);
            card.sawCrackedRebound |= rebound;
            Save(JsonUtility.ToJson(card));
        }

        public override bool Tap(Vector2 screen)
        {
            if (model == null || model.Detached || !Eye.pixelRect.Contains(screen)) return false;

            float radius = Mathf.Clamp(Mathf.Min(Eye.pixelWidth, Eye.pixelHeight) * .035f, 10f, 32f);
            float r2 = radius * radius;
            Vector3 upper = Eye.WorldToScreenPoint(bench.Probe.position);
            Vector3 lower = Eye.WorldToScreenPoint(lowerProbe.position);
            float best = r2, chosen = -1;
            Vector3 a = ProjectJoint(0);
            for (int j = 1; j <= BranchModel.Links; j++)
            {
                Vector3 b = ProjectJoint(j);
                Vector2 edge = (Vector2)b - (Vector2)a;
                float t = Mathf.Clamp01(Vector2.Dot(screen - (Vector2)a, edge) / Mathf.Max(1e-5f, edge.sqrMagnitude));
                float distance = (screen - ((Vector2)a + t * edge)).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    chosen = (j - 1 + t) / BranchModel.Links;
                }
                a = b;
            }
            if (chosen < 0) return NearSegment(screen, upper, lower, r2) && Bend();
            float next = Mathf.Clamp(chosen, .12f, 1);
            if (Mathf.Abs(contact - next) > .04f)
            {
                // Compare a fresh first bend at a new grip on the same specimen.
                level = pendingLevel = 0;
            }
            contact = next;
            Refresh();
            return Bend();
        }

        private Vector3 ProjectJoint(int j) =>
            Eye.WorldToScreenPoint(desk.TransformPoint(model.Centers[j]));

        private static bool NearSegment(Vector2 p, Vector3 a, Vector3 b, float r2)
        {
            if (a.z <= 0f || b.z <= 0f) return false;
            Vector2 start = a;
            Vector2 ab = (Vector2)b - start;
            float t = Mathf.Clamp01(Vector2.Dot(p - start, ab) / Mathf.Max(ab.sqrMagnitude, .0001f));
            return (p - (start + ab * t)).sqrMagnitude <= r2;
        }

        public override void Panel()
        {
            if (model == null) return;
            if (model.Detached)
            {
                if (ActionButton("换新枝复看")) NewSpecimen();
                return;
            }
            if ((level < Loads.Length - 1 || inspecting) && ActionButton("再弯一点")) Bend();
            if (level > 0 && ActionButton("松开")) Unload();
        }

        internal Vector2 TipScreen => ProjectJoint(BranchModel.Links);
        internal Vector2 JointScreen(int joint) => ProjectJoint(joint);
        internal float Contact => contact;
        internal float TipTravel => Vector3.Distance(restTip, model.Centers[BranchModel.Links]);
        internal int Broken => model.Broken;
        internal bool Detached => model.Detached;
        public override void Overlay()
        {
            if (model == null || model.Detached) return;
            if (level == 0 && model.Broken == 0)
                Cue(ProjectJoint(Mathf.RoundToInt(contact * BranchModel.Links)), "点枝条选位置", new Vector2(-78, -48));
            if (model.CrackJoint >= 0 && model.Broken > 0)
                Cue(ProjectJoint(model.CrackJoint), $"仍连着 {BranchModel.Fibers - model.Broken} 根", new Vector2(14, -43));
        }

        internal void Unload()
        {
            level = 0;
            inspecting = false;
            advancing = true;
            ticksAtLevel = 0;
            Result("松开触点：在同一枝模型上卸力。");
        }

        private bool Bend()
        {
            if (model.Detached || (level == Loads.Length - 1 && !inspecting)) return false;
            level = Mathf.Min(level + 1, Loads.Length - 1);
            // Keep one sample even if bend and release arrive between ticks.
            pendingLevel = Mathf.Max(pendingLevel, level);
            inspecting = false;
            advancing = true;
            ticksAtLevel = 0;
            Result(FormattableString.Invariant($"枝梢加弯：第 {level} 档，模型载荷 {Loads[level]:0.00}。"));
            return true;
        }

        private void Update()
        {
            if (model == null) return;

            if (advancing)
            {
                const double step = BranchModel.StepSeconds;
                accumulator = Math.Min(accumulator + Time.deltaTime, 8 * step);
                bool changed = false;
                while (advancing && accumulator >= step)
                {
                    accumulator -= step;
                    Advance();
                    changed = true;
                }
                if (!advancing) accumulator = 0;
                if (changed) Refresh();
            }

            if (fragment && !reportedFall)
            {
                float drop = releaseHeight - fragment.worldCenterOfMass.y;
                if (drop >= .025f)
                {
                    reportedFall = true;
                    Result(FormattableString.Invariant($"自由段已独立运动，重心下降 {drop * 100f:0.0} 模型厘米。"));
                }
            }
        }

        private void Advance()
        {
            bool wasDetached = model.Detached;
            int before = model.Broken;
            int sample = Mathf.Max(level, pendingLevel);
            pendingLevel = 0;
            if (!wasDetached) Array.Copy(model.Centers, previous, previous.Length);

            model.Step(Loads[sample], Contact);
            ticksAtLevel = sample == level ? ticksAtLevel + 1 : 0;
            if (!wasDetached && sample > 0 && before == 0) Remember(1);

            if (before == 0 && model.Broken > 0)
            {
                FiberEvent first = model.Events[0];
                string side = first.side == "upper" ? "上侧" : "下侧";
                Result(FormattableString.Invariant(
                    $"首裂：第 {first.tick} 步，节点 {first.joint} {side}；已裂 {model.Broken}/{BranchModel.Fibers}。"));
                Remember(2);
                // Stop on the actual first cracked pose so even a slow frame
                // leaves time to choose between unloading and continuing.
                if (!model.Detached && level > 0)
                {
                    inspecting = true;
                    advancing = false;
                }
            }

            if (!wasDetached && model.Detached)
            {
                tipSkin.Update(model, model.CrackJoint, BranchModel.Links);
                tipObject.SetActive(true);
                fragment = BranchFragment.Attach(tipObject, model, previous, .08f);
                releaseHeight = fragment.worldCenterOfMass.y;
                level = pendingLevel = ticksAtLevel = 0;
                inspecting = false;
                Result(FormattableString.Invariant(
                    $"枝梢脱落：第 {model.Tick} 步，{model.Broken}/{BranchModel.Fibers} 根已断；自由段交给刚体。"));
                Remember(3);
                return;
            }

            // Once settled, an unloaded model (or an intact, sub-threshold
            // first bend) need not rebuild the same skin every frame.
            if (ticksAtLevel >= SettleTicks && (level == 0 || (level == 1 && model.Broken == 0)))
            {
                advancing = false;
                if (level == 0 && !model.Detached)
                {
                    float residual = Vector3.Distance(restTip, model.Centers[BranchModel.Links]);
                    Result(FormattableString.Invariant(
                        $"回弹趋稳：梢端距初始位置 {residual * 1000f:0.0} 模型毫米，仍裂 {model.Broken}/{BranchModel.Fibers}。"));
                    if (model.Broken > 0) Remember(2, true);
                }
            }
        }

        private void Refresh()
        {
            stumpSkin.Update(model, 0, model.Detached ? model.CrackJoint : BranchModel.Links);
            bench.Probe.gameObject.SetActive(!model.Detached);
            lowerProbe.gameObject.SetActive(!model.Detached);
            if (model.Detached) return;

            float link = Contact * BranchModel.Links;
            int j = Mathf.Min((int)link, BranchModel.Links - 1);
            Vector3 a = model.Centers[j], b = model.Centers[j + 1];
            Vector3 p = Vector3.Lerp(a, b, link - j);
            Vector3 n = new Vector3(a.y - b.y, b.x - a.x, 0f).normalized;
            float radius = model.Radius(Contact);
            bench.Probe.localPosition = p + n * (radius + .016f);
            lowerProbe.localPosition = p - n * (radius + .012f);
        }

        private void OnDestroy()
        {
            if (bench != null && bench.DetailTexture)
            {
                bench.DetailTexture.Release();
                Destroy(bench.DetailTexture);
            }
            if (stumpSkin != null && stumpSkin.Mesh) Destroy(stumpSkin.Mesh);
            if (tipSkin != null && tipSkin.Mesh) Destroy(tipSkin.Mesh);
            foreach (Material mat in deskMaterials)
                if (mat) Destroy(mat);
            if (desk) Destroy(desk.gameObject);
        }
    }
}
