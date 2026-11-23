using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    // The default constructor path uses pinned Modular Campus geometry unchanged.
    // Configured spans extend that source with clipped courses and selectable fracture faces.
    internal sealed class BrickSkin : IDisposable
    {
        sealed class Face
        {
            public IBrickFace span;
            public Transform debrisFrame;
        }

        sealed class Part
        {
            public Face face;
            public Transform node;
            public Vector3 rest;
            public bool released, loose;
            public int index;
        }

        readonly CampusKit kit;
        readonly Transform owner, yard;
        readonly int seed;
        readonly bool wrap;
        readonly float height;
        readonly Vector3 a, b;
        readonly BrickWallSpec spec;
        readonly List<Face> faces = new List<Face>();
        readonly List<Part> parts = new List<Part>();
        readonly Dictionary<Collider,Part> pick = new Dictionary<Collider,Part>();

        public string Id { get; }
        public int PieceCount => parts.Count;
        public int ColliderCount => pick.Count;
        public IEnumerable<Collider> Colliders => pick.Keys;
        public int BrickCount => PieceCount / 2;
        public Transform FirstPiece => Piece(0);
        public Transform Piece(int index) => parts[index].node;
        public int Missing {
            get {
                int count = 0;
                foreach (var f in faces) count += f.span.Missing;
                return count;
            }
        }

        public static BrickSkin Wall(CampusKit kit, Transform yard, Vector3 a, Vector3 b, int spanId, BrickWallSpec spec = null)
        {
            return new BrickSkin(kit, yard, yard, spanId, false, 0, a, b, spec);
        }

        public static BrickSkin Column(CampusKit kit, Transform postPivot, int postId, float coreHeight, BrickWallSpec spec = null)
        {
            if (coreHeight <= 0 || !postPivot.parent)
                throw new ArgumentException("A column skin needs a positive height and a pivot in the yard.");
            return new BrickSkin(kit, postPivot, postPivot.parent, postId, true, coreHeight, Vector3.zero, Vector3.zero, spec);
        }

        BrickSkin(CampusKit kit, Transform owner, Transform yard, int seed, bool wrap, float height, Vector3 a, Vector3 b, BrickWallSpec spec)
        {
            this.kit = kit; this.owner = owner; this.yard = yard;
            this.seed = seed; this.wrap = wrap; this.height = height;
            this.a = a; this.b = b; this.spec = spec?.Copy();
            Id = (wrap ? "post/" : "span/") + seed + "/brick";
            Build();
        }

        void Build()
        {
            if (!wrap) {
                if (spec == null) Register(new OriginalBrickFace(new BoundarySpan(kit, owner, a, b, false, seed)), 0);
                else {
                    var wall = spec.Copy();
                    if (wall.width == 0) wall.width = Vector3.Distance(a, b) - .38f;
                    Register(new StyledBrickSpan(kit,owner,a,b,seed,wall), 0);
                }
                return;
            }

            // Column width is set by the original .38 casting, .42 band and .61 shoe.
            // The end faces cover the returns, with no overlapping bricks at corners.
            const float outside = .66f, thickness = .11f;
            var normals = new[] { Vector3.back, Vector3.right, Vector3.forward, Vector3.left };
            for (int i = 0; i < normals.Length; i++) {
                float faceWidth = i % 2 == 0 ? outside : outside - 2 * thickness;
                float distanceBetweenSourcePosts = faceWidth + .38f;
                var end = Vector3.right * (distanceBetweenSourcePosts * .5f);
                int faceSeed = 10000 + seed * 4 + i;
                IBrickFace span;
                if (spec == null) {
                    span = new OriginalBrickFace(new BoundarySpan(kit,owner,-end,end,false,faceSeed));
                    span.root.localScale = new Vector3(1, (height - .10f)/1.959f, thickness/.23f);
                } else {
                    var face = spec.Copy();
                    face.width = faceWidth; face.height = height - .10f; face.brickDepth = thickness;
                    span = new StyledBrickSpan(kit,owner,-end,end,faceSeed,face);
                }
                span.root.localRotation = Quaternion.LookRotation(normals[i], Vector3.up);
                span.root.localPosition = normals[i] * ((outside - thickness) * .5f);
                Register(span, i);
            }
        }

        void Register(IBrickFace span, int faceId)
        {
            if (span.root.childCount == 0 || span.root.childCount % 2 != 0)
                throw new InvalidOperationException("Expected the source wall's paired clay fragments.");
            var face = new Face { span = span };
            faces.Add(face);
            span.root.name = Id + "/face-" + faceId;
            for (int i = 0; i < span.root.childCount; i++) {
                var node = span.root.GetChild(i);
                node.name = "brick-" + (i / 2) + (i % 2 == 0 ? ".a" : ".b");
                var part = new Part { face = face, node = node, rest = node.localPosition, index = i };
                parts.Add(part);
                foreach (var coll in node.GetComponents<MeshCollider>()) pick.Add(coll, part);
            }
        }

        // An already released fragment consumes the pick but cannot hit the remaining wall.
        public bool TryHit(RaycastHit hit, float time)
        {
            if (!hit.collider || !pick.TryGetValue(hit.collider, out var part)) return false;
            if (part.released) return true;
            var point = part.face.span.root.InverseTransformPoint(hit.point);
            int before = part.face.span.Missing;
            part.face.span.Hit(new Vector2(point.x, point.y), .52f, time);
            TrackReleases(part.face, before, time);
            Pose(time);
            return true;
        }

        public void Support(float left, float right, float time, Vector3 leftAxis, Vector3 rightAxis)
        {
            if (wrap) return;
            var face = faces[0];
            int before = face.span.Missing;
            face.span.Support(left, right, time, leftAxis, rightAxis);
            TrackReleases(face, before, time);
        }

        // BoundarySpan exposes its released count and replay poses. A terminal pose
        // query identifies exactly which children it released, including delayed
        // course releases. Restore the current pose before rendering or physics.
        void TrackReleases(Face face, int before, float time)
        {
            if (before == face.span.Missing) return;
            if (face.span is StyledBrickSpan styled) {
                foreach (var p in parts)
                    if (p.face == face) p.released = styled.IsReleased(p.index);
                return;
            }
            face.span.Pose(time + 3f);
            foreach (var p in parts)
                if (p.face == face && (p.node.localPosition - p.rest).sqrMagnitude > 1e-8f)
                    p.released = true;
            face.span.Pose(time);
        }

        public void Pose(float time)
        {
            if (Missing == 0) return;
            foreach (var f in faces) if (f.span.Missing != 0) f.span.Pose(time);
            foreach (var part in parts) {
                if (!part.released || part.loose || (part.node.localPosition - part.rest).sqrMagnitude < 1e-12f) continue;
                part.loose = true;
                if (!wrap) continue;
                // Preserve the pose at the moment of separation. Further changes of the
                // post pivot move only the bricks that are still on the jacket.
                var src = part.face.span.root;
                var frame = part.face.debrisFrame;
                if (!frame || Vector3.Distance(frame.position, src.position) > 1e-4f ||
                    Quaternion.Angle(frame.rotation, src.rotation) > .01f) {
                    frame = CampusKit.Node("Detached " + src.name, yard, yard.InverseTransformPoint(src.position));
                    frame.rotation = src.rotation;
                    // The original ParkingDeck post pivot is a rigid transform, with
                    // uniform yard installation scale above it.
                    frame.localScale = src.localScale;
                    part.face.debrisFrame = frame;
                    debrisFrames.Add(frame);
                }
                part.node.SetParent(frame, false);
            }
        }

        readonly List<Transform> debrisFrames = new List<Transform>();

        public void Reset()
        {
            Dispose();
            Build();
        }

        public void Dispose()
        {
            foreach (var f in faces) {
                if (f.span.root) {
                    f.span.root.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(f.span.root.gameObject);
                }
                f.span.Dispose();
            }
            foreach (var frame in debrisFrames) {
                if (!frame) continue;
                frame.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(frame.gameObject);
            }
            pick.Clear(); parts.Clear(); faces.Clear(); debrisFrames.Clear();
        }
    }
}
