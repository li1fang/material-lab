using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction
{
    [Serializable]
    public sealed class CeramicContact
    {
        public int step;
        public string source;
        public Vector3 localPoint, localDirection;
        public float impulseNs;
        public int[] released;
    }

    // Thin glazed dish supported at its base. Only the rim cells can separate.
    public sealed class CeramicRim
    {
        public const int Cells = 36;
        public const float Radius = .138f;
        public const float Strength = .19f; // workbench calibration in N·s
        readonly bool[] missing = new bool[Cells];
        public int Missing { get; private set; }
        public bool Gone(int cell) => missing[(cell + Cells) % Cells];

        public int[] Strike(Vector3 point, Vector3 direction, float impulse)
        {
            if (!float.IsFinite(impulse) || impulse < 0 || !float.IsFinite(point.sqrMagnitude)
                || !float.IsFinite(direction.sqrMagnitude)) throw new ArgumentException("Invalid contact.");
            float radial = new Vector2(point.x,point.z).magnitude / Radius;
            if (radial < .71f || radial > 1.08f || direction.sqrMagnitude < 1e-8f) return Array.Empty<int>();
            float angle = Mathf.Repeat(Mathf.Atan2(point.z,point.x),2*Mathf.PI);
            int center = Mathf.FloorToInt(angle * Cells / (2*Mathf.PI)) % Cells;
            var radialAxis = new Vector3(point.x,0,point.z).normalized;
            var unit = direction.normalized;
            float efficiency = (.42f+.58f*Mathf.Clamp01(Mathf.Abs(unit.y)+.45f*Mathf.Abs(Vector3.Dot(unit,radialAxis))))
                * Mathf.Lerp(.7f,1f,Mathf.InverseLerp(.71f,.94f,radial));
            var cells = new List<int>();
            for (int offset=-2;offset<=2;offset++)
            {
                int i=(center+offset+Cells)%Cells;
                float local=impulse*efficiency*Mathf.Exp(-.59f*Mathf.Abs(offset));
                float resistance=Strength*(1+.07f*Mathf.Sin(i*2.271f));
                if (missing[i] || local<resistance) continue;
                missing[i]=true;
                cells.Add(i);
            }
            Missing+=cells.Count;
            return cells.ToArray();
        }
    }
}
