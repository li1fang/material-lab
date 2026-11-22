using System;
using UnityEngine;

namespace MaterialDestruction
{
    [Serializable]
    public struct BranchInput
    {
        public float drive, contact;
    }

    [Serializable]
    public sealed class BranchTrial
    {
        public int version = 1;
        public bool releaseAtFirstBreak;
        public BranchProfile profile = new BranchProfile();
        public float contact = .83f;
        public float peak = -1.24f;
        public int rampTicks = 108;
        public int holdTicks = 40;
        public int releaseTicks = 100;
        public BranchInput[] samples;

        public int Duration => samples != null && samples.Length > 0 ? samples.Length : rampTicks + holdTicks + releaseTicks;
        public void Validate()
        {
            if (version != 1 || profile == null || !float.IsFinite(contact) || contact < .12f || contact > 1 ||
                !float.IsFinite(peak) || Mathf.Abs(peak) > 1.6f || rampTicks < 1 || holdTicks < 0 ||
                releaseTicks < 1 || rampTicks > 3600 || holdTicks > 3600 || releaseTicks > 3600)
                throw new ArgumentException("Unsupported trial. Check version, contact, peak and tick counts.");
            profile.Validate();
            if (samples == null) return;
            if (samples.Length > 36000) throw new ArgumentException("Recording exceeds ten minutes.");
            foreach (var s in samples)
                if (!float.IsFinite(s.drive) || Mathf.Abs(s.drive) > 1.6f || !float.IsFinite(s.contact) || s.contact < .12f || s.contact > 1f)
                    throw new ArgumentException("Invalid recorded sample.");
        }
        public float DriveAt(int tick)
        {
            if (samples != null && samples.Length > 0) return samples[tick].drive;
            if (tick >= rampTicks + holdTicks) return 0;
            return peak * Mathf.SmoothStep(0, 1, Mathf.Clamp01((tick + 1f) / rampTicks));
        }
        public float ContactAt(int tick) => samples != null && samples.Length > 0 ? samples[tick].contact : contact;
        public BranchModel Simulate()
        {
            Validate();
            var model = new BranchModel(profile, releaseAtFirstBreak);
            for (int i = 0; i < Duration; i++) model.Step(DriveAt(i), ContactAt(i));
            return model;
        }
        public static BranchTrial Read(string json)
        {
            var trial = new BranchTrial();
            JsonUtility.FromJsonOverwrite(json, trial);
            trial.Validate();
            return trial;
        }
    }
}
