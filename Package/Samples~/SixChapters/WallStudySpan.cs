using System;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    // Keep the original two pre-fractured meshes and their convex colliders together
    // about one brick centre until the source actually releases either piece.
    internal sealed class WallStudySpan
    {
        sealed class Piece
        {
            public Transform node;
            public Vector3 rest, pivot, lastOffset;
            public Quaternion lastRotation = Quaternion.identity;
            public Vector3 retainedShift;
            public Quaternion retainedRotation = Quaternion.identity;
            public int row;
        }

        readonly Transform root;
        readonly Action<float> pose;
        readonly Func<int,float> releasedAt;
        readonly Func<int> missing;
        readonly int variant;
        readonly float width;
        readonly Piece[] pieces;
        internal float Amount { get; set; }
        internal float ContactX { get; set; }
        internal float Retention => variant == 0 ? .23f : variant == 1 ? .57f : .80f;
        internal int Missing => missing();
        internal bool Released(int chip) => releasedAt(chip) >= 0;
        internal int Count => pieces.Length;

        internal WallStudySpan(BoundarySpan source, int variant, float width)
            : this(source.root, source.Pose, source.ReleaseTime, () => source.Missing, variant, width, .245f) { }

        internal WallStudySpan(StyledBrickSpan source, int variant, BrickWallSpec spec)
            : this(source.root, source.Pose, source.ReleaseTime, () => source.Missing, variant, spec.width, spec.brickHeight) { }

        WallStudySpan(Transform root, Action<float> pose, Func<int,float> releasedAt, Func<int> missing,
            int variant, float width, float courseHeight)
        {
            this.root = root;
            this.pose = pose;
            this.releasedAt = releasedAt;
            this.missing = missing;
            this.variant = variant;
            this.width = width;
            pieces = new Piece[root.childCount];
            if (pieces.Length == 0 || pieces.Length % 2 != 0)
                throw new InvalidOperationException("Pinned wall must provide two mesh halves per brick.");
            for (int i = 0; i < pieces.Length; i += 2)
            {
                var bounds = LocalBounds(i);
                bounds.Encapsulate(LocalBounds(i + 1));
                for (int half = 0; half < 2; half++)
                {
                    int index = i + half;
                    var node = root.GetChild(index);
                    
                    pieces[index] = new Piece { node = node, rest = node.localPosition,
                        pivot = bounds.center, row = Mathf.FloorToInt(bounds.center.y / courseHeight) };
                }
            }
        }

        Bounds LocalBounds(int index)
        {
            var node = root.GetChild(index);
            var bounds = node.GetComponent<MeshFilter>().sharedMesh.bounds;
            bounds.center += node.localPosition;
            return bounds;
        }

        void AttachedPose(Piece p, out Vector3 shift, out Quaternion rotation)
        {
            Vector2 relative = p.pivot;
            relative.x -= ContactX;
            float nearPost = Mathf.InverseLerp(width * .5f - .12f, width * .32f, Mathf.Abs(p.pivot.x));
            float gain = Mathf.Lerp(Amount, Amount * nearPost, Mathf.Clamp01(Mathf.Abs(ContactX) / .18f));
            WallStudyProfiles.Evaluate(variant, relative, p.row, width, gain, out shift, out rotation);
            if (p.retainedShift.z > shift.z)
            {
                shift = p.retainedShift;
                rotation = p.retainedRotation;
            }
        }

        internal void Retain(float localX, float amount)
        {
            ContactX = localX;
            Amount = amount;
            foreach (var p in pieces)
            {
                AttachedPose(p, out var shift, out var rotation);
                p.retainedShift = shift;
                p.retainedRotation = rotation;
            }
            Amount = 0;
        }

        internal void Pose(float time)
        {
            pose(time);
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                float released = releasedAt(i);
                if (released < 0)
                {
                    AttachedPose(p, out var offset, out var rotation);
                    p.lastOffset = p.pivot + offset + rotation * (p.rest - p.pivot) - p.rest;
                    p.lastRotation = rotation;
                    p.node.SetLocalPositionAndRotation(p.rest + p.lastOffset, rotation);
                }
                else
                {
                    // Start at the deformed pose. Follow the original fall and finish
                    // at its exact resting spot, including a delayed supported row.
                    float duration = .33f + Mathf.Sqrt(p.rest.y) * .30f;
                    float carry = 1 - Mathf.SmoothStep(0, 1, (time - released) / duration);
                    p.node.SetLocalPositionAndRotation(p.node.localPosition + p.lastOffset * carry,
                        p.node.localRotation * Quaternion.Slerp(Quaternion.identity, p.lastRotation, carry));
                }
            }
        }

        internal float[] ReadDepths()
        {
            var depths = new float[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) depths[i] = pieces[i].node.localPosition.z - pieces[i].rest.z;
            return depths;
        }
    }
}
