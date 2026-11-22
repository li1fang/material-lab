using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction
{
    [Serializable]
    public sealed class BranchProfile
    {
        [Min(.1f)] public float length = .75f;
        [Min(.005f)] public float rootRadius = .027f;
        [Range(.3f, 3f)] public float toughness = 1f;
        [Range(-.35f, .35f)] public float sweep = .12f;
        public int seed = 23;

        public void Validate()
        {
            if (!float.IsFinite(length) || length < .1f || length > 3f ||
                !float.IsFinite(rootRadius) || rootRadius < .005f || rootRadius >= length * .2f ||
                !float.IsFinite(toughness) || toughness < .3f || toughness > 3f ||
                !float.IsFinite(sweep) || Mathf.Abs(sweep) > .35f)
                throw new ArgumentException("Invalid branch dimensions, sweep or toughness.");
        }
    }

    [Serializable]
    public struct FiberEvent
    {
        public int tick, joint, fiber;
        public string kind, side;
    }

    // A reduced cantilever. Each joint samples tensile fibers around its cross section.
    // The bending moment follows the lever arm from the selected point to the joint.
    public sealed class BranchModel
    {
        public const int Links = 22;
        public const int Fibers = 16;
        public const float StepSeconds = 1f / 60f;
        public readonly BranchProfile Profile;
        public readonly Vector3[] Centers = new Vector3[Links + 1];
        public readonly float[] Angles = new float[Links];
        public readonly List<FiberEvent> Events = new List<FiberEvent>();
        readonly float[,] wear = new float[Links, Fibers];
        readonly float[] rotation = new float[Links];
        readonly float[] velocity = new float[Links];
        public bool ReleaseAtFirstBreak { get; }
        public int Tick { get; private set; }
        public int CrackJoint { get; private set; } = -1;
        public int Broken { get; private set; }
        public bool Detached => Broken == Fibers;
        public float Load { get; private set; }
        public float Contact { get; private set; }
        public float Gap { get; private set; }

        public BranchModel(BranchProfile profile, bool releaseAtFirstBreak = false)
        {
            profile.Validate();
            Profile = JsonUtility.FromJson<BranchProfile>(JsonUtility.ToJson(profile));
            ReleaseAtFirstBreak = releaseAtFirstBreak;
            Pose();
        }

        public float Radius(float u)
        {
            float neck = 1f - .16f * Mathf.Exp(-Mathf.Pow((u - .29f) / .07f, 2f));
            return Profile.rootRadius * Mathf.Lerp(1f, .32f, u) * neck;
        }

        public static float FiberHeight(int fiber) =>
            Mathf.Cos(2f * Mathf.PI * (fiber + .5f) / Fibers);

        public bool IsBroken(int fiber) => CrackJoint >= 0 && wear[CrackJoint, fiber] >= 1f;

        public void Step(float drive, float contact)
        {
            if (!float.IsFinite(drive) || !float.IsFinite(contact))
                throw new ArgumentException("The applied load and contact must be finite.");
            Tick++;
            Contact = Mathf.Clamp(contact, .12f, 1f);
            Load = Detached ? 0f : Mathf.Clamp(drive, -1.6f, 1.6f);
            for (int j = 1; j < Links; j++)
            {
                float u = (float)j / Links;
                float lever = Mathf.Max(0f, Contact - u);
                float r = Radius(u) / Profile.rootRadius;
                float stress = Load * lever / (r * r * r * Profile.toughness);
                if (!Detached && (CrackJoint < 0 || CrackJoint == j) && lever > 0 && Load != 0)
                    Accumulate(j, stress);

                float lost = CrackJoint == j ? (float)Broken / Fibers : 0f;
                float target = Load * lever * .028f / Mathf.Pow(r, 3.3f);
                if (!Detached && Load!=0) target += Mathf.Sign(Load) * lost * lost * .39f;
                // Semi-implicit damped joint response, including rebound after release.
                velocity[j] += ((target - rotation[j]) * 210f - velocity[j] * 12f) * StepSeconds;
                rotation[j] += velocity[j] * StepSeconds;
            }
            if (!Detached && CrackJoint >= 0)
                Gap = Mathf.Lerp(Gap, (.16f + .84f * Mathf.Min(1f, Mathf.Abs(Load))) * Broken / Fibers, .14f);
            Pose();
        }

        void Accumulate(int j, float stress)
        {
            float lost = CrackJoint == j ? (float)Broken / Fibers : 0f;
            for (int f = 0; f < Fibers; f++)
            {
                if (wear[j,f] >= 1) continue;
                float tension = Mathf.Max(0, -Mathf.Sign(stress) * FiberHeight(f));
                float transfer = .29f + .71f * tension + 1.35f * lost;
                float localStrength = 1.09f + .095f * Mathf.Sin(j * 4.53f + f * 9.71f + Profile.seed);
                float excess = Mathf.Max(0, Mathf.Abs(stress) * transfer - localStrength);
                wear[j,f] = Mathf.Min(1, wear[j,f] + excess * StepSeconds * 4.1f);
                if (wear[j,f] < 1) continue;
                CrackJoint = j;
                Broken++;
                Events.Add(new FiberEvent { tick = Tick, joint = j, fiber = f,
                    kind = "fiber-break", side = FiberHeight(f) >= 0 ? "upper" : "lower" });
                if (ReleaseAtFirstBreak)
                {
                    for (int other=0; other<Fibers; other++) wear[j,other]=1f;
                    Broken=Fibers;
                }
                if (Detached)
                    Events.Add(new FiberEvent { tick = Tick, joint = j, fiber = -1, kind = "detach", side = "" });
            }
        }

        void Pose()
        {
            float bending = 0;
            Centers[0] = Vector3.zero;
            for (int j = 0; j < Links; j++)
            {
                bending += rotation[j];
                float u = (j + .5f) / Links;
                float angle = bending + .055f + Profile.sweep * u + .07f * Mathf.Sin(9f * u);
                Angles[j] = angle;
                Vector3 axis = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .06f * Mathf.Sin(12f * u));
                Centers[j + 1] = Centers[j] + axis.normalized * (Profile.length / Links);
            }
        }
    }
}
