using UnityEngine;

namespace MaterialDestruction.Campus
{
    internal static class WallStudyProfiles
    {
        static float Ramp(float a, float b, float value) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, value));

        // Rest-space displacements for the three playable toy material treatments.
        internal static void Evaluate(int variant, Vector2 center, int course, float width,
            float amount, out Vector3 offset, out Quaternion rotation)
        {
            offset = Vector3.zero;
            rotation = Quaternion.identity;
            float radius = width * .5f - .13f;
            float u = center.x / radius;
            float gain = amount * (1 - Ramp(.64f, 1, Mathf.Abs(u)));
            if (gain <= 0) return;
            float h = Mathf.InverseLerp(.01f, 1.96f, center.y);
            Vector3 shift, tilt;
            if (variant == 0)
            {
                float arch = 1 - u * u;
                float dh = h - .48f;
                float bow = arch * (1 - .8f * dh * dh);
                shift = new Vector3(0, -.025f * h * bow, .64f * bow);
                tilt = new Vector3(-5 * dh * arch, 24 * u, 0);
            }
            else if (variant == 1)
            {
                float q = center.x / .57f;
                float v = (center.y - 1.03f) / .58f;
                float core = Mathf.Exp(-q * q);
                float shoulder = 1 - Ramp(.36f, 1.14f, Mathf.Abs(center.x));
                float footprint = .70f * core + .30f * shoulder;
                float mid = Mathf.Exp(-v * v);
                float load = .48f + .52f * mid;
                shift = new Vector3(-.045f * q * core * load, -.055f * h * footprint, .70f * footprint * load);
                tilt = new Vector3(-16 * v * mid * footprint, 48 * q * core * load, 0);
            }
            else
            {
                int tier = Mathf.Clamp(course, 0, 7) / 2;
                float seamX = (tier - 1.25f) * width / 6;
                bool slipped = center.x <= seamX;
                float bow = .24f * (1 - u * u);
                shift = new Vector3(slipped ? -.052f : 0, slipped ? -.032f * h : 0,
                    bow + (slipped ? .24f : 0));
                tilt = new Vector3(0, 9 * u, 0);
            }
            offset = gain * shift;
            rotation = Quaternion.Euler(gain * tilt);
        }
    }
}
