using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    internal static class StandArchitecture
    {
        private const float Deck = 3.356f;
        private const string Brick = "AA5947";
        private const string Rose = "BE6B52";
        private const string Oxide = "984B3E";
        private const string Biscuit = "E9CEAA";
        private const string Limestone = "F4E1BC";
        private const string Mortar = "CE997B";
        private const string White = "FFF5E3";
        private const string Iron = "343B3B";
        private const string Wood = "64483D";
        private const string Clay = "C57150";
        private const string ClayLight = "D98660";

        private static Vector3 V(float x, float y, float z)
        {
            return new Vector3(x, y, z);
        }

        private static Transform Group(Transform parent, string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        // Batch the model-maker's little pieces by finish. In particular, a tile,
        // a mortar joint, or a piece of ironwork need not be a scene object.
        private sealed class Surface
        {
            internal readonly List<Vector3> vertices = new List<Vector3>();
            internal readonly List<int> indices = new List<int>();

            internal void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                indices.Add(i); indices.Add(i + 1); indices.Add(i + 2);
            }

            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                indices.Add(i); indices.Add(i + 1); indices.Add(i + 2);
                indices.Add(i); indices.Add(i + 2); indices.Add(i + 3);
            }
        }

        private sealed class Pieces
        {
            private readonly Dictionary<string, Surface> surfaces = new Dictionary<string, Surface>();

            internal Surface Of(string finish)
            {
                Surface s;
                if (!surfaces.TryGetValue(finish, out s))
                {
                    s = new Surface();
                    surfaces.Add(finish, s);
                }
                return s;
            }

            // x,y,z are oriented half edges, in right-handed order.
            internal void Block(string col, Vector3 c, Vector3 x, Vector3 y, Vector3 z)
            {
                Surface s = Of(col);
                Vector3 a = c - x - y - z, b = c + x - y - z;
                Vector3 d = c - x + y - z, e = c + x + y - z;
                Vector3 f = c - x - y + z, g = c + x - y + z;
                Vector3 h = c - x + y + z, j = c + x + y + z;
                s.Quad(a, d, e, b);
                s.Quad(f, g, j, h);
                s.Quad(a, f, h, d);
                s.Quad(b, e, j, g);
                s.Quad(d, h, j, e);
                s.Quad(a, b, g, f);
            }

            internal void Box(string col, float x, float y, float z, float w, float h, float d)
            {
                Block(col, V(x, y, z), V(w * .5f, 0, 0), V(0, h * .5f, 0), V(0, 0, d * .5f));
            }

            internal void Rod(string col, Vector3 a, Vector3 b, float w, float d)
            {
                Vector3 axis = (b - a) * .5f;
                if (axis.sqrMagnitude < .00000001f) return;
                Quaternion q = Quaternion.FromToRotation(Vector3.up, axis.normalized);
                Block(col, (a + b) * .5f, q * V(w * .5f, 0, 0), axis, q * V(0, 0, d * .5f));
            }

            private static float Cross(Vector2 a, Vector2 b)
            {
                return a.x * b.y - a.y * b.x;
            }

            private static Vector3 At(Vector2 p, float z)
            {
                return V(p.x, p.y, z);
            }

            // An extruded paper pattern in the XY plane. Ear clipping also permits
            // re-entrant corners in coat hems, elbows, hat brims, etc.
            internal void Cut(string col, Vector2[] p, float front, float back)
            {
                int n = p.Length;
                float area = 0;
                for (int i = 1; i < n - 1; ++i)
                    area += Cross(p[i] - p[0], p[i + 1] - p[0]);
                if (area < 0) System.Array.Reverse(p);

                Surface s = Of(col);
                List<int> rim = new List<int>(n);
                for (int i = 0; i < n; ++i)
                {
                    Vector2 a = p[i], b = p[(i + 1) % n];
                    s.Quad(At(a, front), At(b, front), At(b, back), At(a, back));
                    rim.Add(i);
                }
                while (rim.Count >= 3)
                {
                    bool clipped = false;
                    int count = rim.Count;
                    for (int k = 0; k < count; ++k)
                    {
                        int ia = rim[(k + count - 1) % count], ib = rim[k], ic = rim[(k + 1) % count];
                        Vector2 a = p[ia], b = p[ib], c = p[ic];
                        if (Cross(b - a, c - b) <= 0.00000001f) continue;
                        bool clear = true;
                        for (int j = 0; j < count && clear; ++j)
                        {
                            int id = rim[j];
                            if (id == ia || id == ib || id == ic) continue;
                            Vector2 q = p[id];
                            if (Cross(b - a, q - a) >= 0 && Cross(c - b, q - b) >= 0 &&
                                Cross(a - c, q - c) >= 0) clear = false;
                        }
                        if (!clear) continue;
                        s.Triangle(At(c, front), At(b, front), At(a, front));
                        s.Triangle(At(a, back), At(b, back), At(c, back));
                        rim.RemoveAt(k);
                        clipped = true;
                        break;
                    }
                    if (!clipped) break;
                }
            }

            internal void Finish(CampusKit kit, Transform parent, string label)
            {
                foreach (KeyValuePair<string, Surface> entry in surfaces)
                {
                    Surface s = entry.Value;
                    if (s.indices.Count == 0) continue;
                    Mesh mesh = new Mesh();
                    mesh.name = label + " " + entry.Key;
                    if (s.vertices.Count > 65535)
                        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.SetVertices(s.vertices);
                    mesh.SetTriangles(s.indices, 0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    Transform t = kit.Solid(parent, mesh.name, Vector3.zero, Vector3.one, entry.Key);
                    Collider collider = t.GetComponent<Collider>();
                    if (collider != null) collider.enabled = false;
                    t.GetComponent<MeshFilter>().sharedMesh = mesh;
                    t.gameObject.AddComponent<StandOwnedMesh>().mesh = mesh;
                }
            }
        }

        internal static void Build(CampusKit kit, Transform yard, BayFrame frame, int stairBay, float pitch)
        {
            List<Transform> bays = new List<Transform>();
            foreach (Transform t in frame.Panels) bays.Add(t);
            if (bays.Count == 0 || pitch <= 0) return;

            stairBay = Mathf.Clamp(stairBay, 0, bays.Count - 1);
            float half = pitch * .5f;
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            foreach (Transform t in bays)
            {
                left = Mathf.Min(left, t.localPosition.x - half);
                right = Mathf.Max(right, t.localPosition.x + half);
            }

            Vector3 selected = bays[stairBay].localPosition;
            float front = selected.z - half, sx = selected.x;
            Transform approach = Group(yard, "Track approach");
            Pieces trim = new Pieces();
            const int risers = 33;
            const float going = .178f;
            const float landing = .58f;
            float toe = front - landing - (risers - 1) * going;
            float stairWidth = Mathf.Min(1.54f, pitch * .64f);
            float gap = stairWidth * .5f + .054f;

            TerraceWing(kit, approach, trim, left, sx - gap, toe - .24f, front - 1.14f, pitch);
            TerraceWing(kit, approach, trim, sx + gap, right, toe - .24f, front - 1.14f, pitch);
            Stair(kit, approach, trim, sx, front, toe, stairWidth, risers, going, landing);
            trim.Finish(kit, approach, "Approach joinery");

            Transform cloister = Group(yard, "Summer cloister");
            RearArcade(kit, cloister, left, right, selected.z + half, bays.Count);

            int occupied = stairBay;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < bays.Count; ++i)
            {
                if (i == stairBay) continue;
                float distance = Mathf.Abs(bays[i].localPosition.x - (left + right) * .5f);
                if (distance < nearest) { nearest = distance; occupied = i; }
            }
            Vector3 center = bays[occupied].localPosition;
            float px = center.x - Mathf.Min(.26f, half * .15f);
            float pz = center.z - half + .85f;
            Transform reader = Figure(kit, yard, V(px, Deck, pz), -12f, true);
            reader.SetParent(bays[occupied], true);
            Figure(kit, yard, V(px, .435f, front - 1.75f), 9f, false);
        }

        private static void TerraceWing(CampusKit kit, Transform parent, Pieces p,
            float l, float r, float near, float far, float pitch)
        {
            float width = r - l;
            if (width < .16f) return;
            float cx = (r + l) * .5f, depth = (far - near) / 3f;
            const float rise = .145f;
            int aisles = Mathf.Max(1, Mathf.RoundToInt(width / (pitch * 2.3f)));
            for (int k = 0; k < 3; ++k)
            {
                float z = near + k * depth, h = (k + 1) * rise;
                kit.Solid(parent, "Standing terrace " + (k + 1), V(cx, h * .5f, z + depth * .5f),
                    V(width, h, depth), k == 1 ? Brick : Rose, true);

                // Projected limestone nosing, with a separate drip shadow beneath.
                p.Box(Oxide, cx, h - .047f, z, width, .021f, .028f);
                p.Box(Biscuit, cx, h - .017f, z + .032f, width + .012f, .038f, .108f);
                int stones = Mathf.Max(1, Mathf.RoundToInt(width / .72f));
                for (int i = 1; i < stones; ++i)
                {
                    float x = l + width * i / stones;
                    p.Box(Mortar, x, h - .091f, z - .001f, .009f, .091f, .005f);
                    p.Box(Mortar, x, h + .001f, z + depth * .5f + .046f,
                        .008f, .002f, depth - .092f);
                }
                p.Box(Mortar, cx, h + .0012f, z + depth * .57f, width, .002f, .008f);

                // Little half-risers mark the short aisles, quite distinct from
                // the full-height stair. They sit ON the preceding standing level.
                for (int i = 0; i < aisles; ++i)
                {
                    float x = l + width * (i + .5f) / aisles;
                    float w = Mathf.Min(.65f, width * .68f);
                    kit.Solid(parent, "Half pace", V(x, h - .75f * rise, z - .127f),
                        V(w, .5f * rise, .254f), Biscuit, true);
                    if (k == 2)
                    {
                        for (int j = 0; j < 3; ++j)
                        {
                            float hh = rise * (3 - j) * .75f;
                            kit.Solid(parent, "Gallery return", V(x, hh * .5f, far + (j + .5f) * .18f),
                                V(w, hh, .18f), j == 1 ? Rose : Biscuit, true);
                        }
                    }
                }
            }
        }

        private static void Stair(CampusKit kit, Transform parent, Pieces p, float x, float front,
            float toe, float width, int n, float run, float landing)
        {
            float rise = Deck / n, lip = front - landing;
            for (int i = 0; i < n; ++i)
            {
                float y = (i + 1) * rise, z = toe + i * run;
                float d = i == n - 1 ? landing : run;
                kit.Solid(parent, i == n - 1 ? "Arrival landing" : "Stone stair tread",
                    V(x, y - rise * .5f, z + d * .5f), V(width, rise, d), Biscuit, true);
                p.Box(Limestone, x, y - .011f, z + .006f, width + .015f, .022f, .040f);
            }
            // A real sill across the butt joint, not a rail closing the stair.
            kit.Solid(parent, "Landing sill", V(x, Deck + .006f, front + .015f),
                V(width, .012f, .14f), Limestone, true);
            const float rail = .57f;
            for (int side = -1; side <= 1; side += 2)
            {
                float cheek = x + side * (width * .5f - .060f);
                p.Rod(Oxide, V(cheek, .052f, toe + .010f), V(cheek, Deck - .105f, lip + run),
                    .114f, .17f);
                p.Box(Brick, cheek, Deck - .11f, lip + landing * .5f, .114f, .18f, landing);
                float rackStart = toe + .070f, rackEnd = lip + .070f;
                for (int j = 0; j <= 2; ++j)
                {
                    float h = j == 2 ? rail : .18f + .19f * j;
                    float w = j == 2 ? .047f : .021f;
                    p.Rod(White, V(cheek, rise + h, rackStart), V(cheek, Deck + h, rackEnd), w, w);
                    p.Rod(White, V(cheek, Deck + h, rackEnd), V(cheek, Deck + h, front - .018f), w, w);
                }
                for (int i = 0; i < n; i += 4)
                {
                    float y = rise * (i + 1), z = rackStart + i * run;
                    p.Box(White, cheek, y + rail * .5f, z, .039f, rail, .039f);
                    p.Box(Biscuit, cheek, y + .014f, z, .075f, .028f, .075f);
                }
                p.Box(White, cheek, Deck + rail * .5f, front - .018f, .043f, rail, .043f);

                // Short masonry feet under the independent flight; no infill wall
                // in front of the host's lower gallery.
                for (int j = 1; j <= 3; ++j)
                {
                    int i = j * 9;
                    float y = rise * i, z = toe + run * (i + .5f);
                    p.Box(Brick, cheek, y * .5f, z, .145f, y, .18f);
                    p.Box(Biscuit, cheek, .042f, z, .195f, .084f, .23f);
                    p.Box(Rose, cheek, y - .105f, z, .177f, .14f, .24f);
                }
            }
            p.Box(Biscuit, x, Deck - .15f, front - .11f, width, .13f, .18f);
        }

        // Each side of the pointed arch is a circular arc struck from the
        // opposite side of the centre line. Both radii terminate at the apex.
        private static Vector2 Arc(float x, float spring, float eccentric, float radius, int side, float t)
        {
            float angle = Mathf.Acos(eccentric / radius) * t;
            return new Vector2(x + side * (radius * Mathf.Cos(angle) - eccentric),
                               spring + radius * Mathf.Sin(angle));
        }

        private static void Voussoir(Pieces p, string col, float x, float y, float e, float r,
            float outer, int side, float from, float to, float z, float depth)
        {
            p.Cut(col, new Vector2[] {
                Arc(x,y,e,r,side,from), Arc(x,y,e,r,side,to),
                Arc(x,y,e,outer,side,to), Arc(x,y,e,outer,side,from)
            }, z, z + depth);
        }

        private static Vector3 InPlane(Vector2 v, float z)
        {
            return V(v.x, v.y, z);
        }

        private static void Arch(Pieces p, int index, float x, float module, float z, float spring,
            float head, float inner, float e, float thickness)
        {
            float r = inner + e, outer = r + thickness;
            float wireZ = z + .092f;
            Vector3 boss = V(x, spring + .21f, wireZ);
            const int blocks = 7;
            for (int s = -1; s <= 1; s += 2)
            {
                for (int j = 0; j < blocks; ++j)
                {
                    float a = j / (float)blocks, b = (j + 1) / (float)blocks;
                    Vector2 v = Arc(x, spring, e, outer, s, a);
                    Vector2 w = Arc(x, spring, e, outer, s, b);
                    p.Cut(Brick, new Vector2[] { v, w, new Vector2(w.x, head), new Vector2(v.x, head) },
                        z + .021f, z + .213f);

                    float seam = .009f;
                    string stone = j == blocks - 1 ? Biscuit :
                        ((index + j * 2 + s + 1) % 5 < 2 ? Rose : (j % 3 == 0 ? Oxide : Clay));
                    Voussoir(p, stone, x, spring, e, r, outer, s, a, b - seam,
                        z - (j == blocks - 1 ? .017f : .004f), .243f);
                    Voussoir(p, Mortar, x, spring, e, r, outer, s, b - seam, b, z + .005f, .222f);
                }

                // Iron fan, left empty between the spokes. The low tie is above
                // head height; the two bent leaves give the fan a scalloped heart.
                for (int k = 1; k <= 3; ++k)
                {
                    Vector2 end = Arc(x, spring, e, r + .012f, s, k * .235f);
                    p.Rod(Iron, boss, InPlane(end, wireZ), .018f, .018f);
                }
                Vector3 leaf = V(x + s * inner * .34f, spring + .50f, wireZ);
                Vector3 shoulder = V(x + s * inner * .27f, spring + .68f, wireZ);
                p.Rod(Iron, boss, leaf, .013f, .016f);
                p.Rod(Iron, leaf, shoulder, .013f, .016f);
                p.Rod(Iron, shoulder, V(x, spring + .86f, wireZ), .013f, .016f);
            }
            float halfTie = Mathf.Sqrt(r * r - .21f * .21f) - e;
            p.Box(Iron, x, boss.y, wireZ, 2 * halfTie + .026f, .028f, .025f);
            p.Rod(Iron, boss, InPlane(Arc(x, spring, e, r, 1, 1), wireZ), .023f, .023f);
            p.Box(Biscuit, x, head - .049f, z + .011f, module * .23f, .046f, .029f);
        }

        private static void RearArcade(CampusKit kit, Transform parent, float left, float right,
            float deckBack, int bays)
        {
            Pieces p = new Pieces();
            float length = right - left, mid = (left + right) * .5f;
            int arches = bays * 2;
            float module = length / arches;
            float z = deckBack + .64f, back = z + 1.22f;
            float edge = back + .22f, frontEdge = z - .19f;

            kit.Solid(parent, "Cloister pavement", V(mid, Deck - .102f, (frontEdge + edge) * .5f),
                V(length, .204f, edge - frontEdge), Biscuit, true);
            kit.Solid(parent, "Paved link behind balcony", V(mid, Deck - .062f, (deckBack + frontEdge) * .5f),
                V(length, .124f, frontEdge - deckBack), Limestone, true);
            p.Box(Oxide, mid, Deck - .220f, (frontEdge + edge) * .5f,
                length - .044f, .035f, edge - frontEdge - .06f);
            for (int i = 0; i <= arches; ++i)
            {
                float x = left + module * i;
                if (i > 0 && i < arches)
                    p.Box(Mortar, x, Deck + .001f, (deckBack + edge) * .5f,
                        .012f, .002f, edge - deckBack);
                if (i % 2 == 0)
                {
                    float footZ = (z + back) * .5f;
                    p.Box(Brick, x, (Deck - .20f) * .5f, footZ, .34f, Deck - .20f, .39f);
                    p.Box(Biscuit, x, .095f, footZ, .48f, .19f, .53f);
                    p.Box(Rose, x, Deck - .425f, footZ, .40f, .25f, .55f);
                    p.Box(Biscuit, x, Deck - .288f, footZ, .48f, .155f, 1.29f);
                }
            }
            float spring = Deck + 1.17f;
            float band = Mathf.Min(.182f, module * .115f);
            float inner = module * .5f - band, e = inner * .38f;
            float outer = inner + e + band;
            float head = spring + Mathf.Sqrt(outer * outer - e * e) + .105f;
            for (int i = 0; i < arches; ++i)
                Arch(p, i, left + (i + .5f) * module, module, z, spring, head, inner, e, band);

            for (int i = 0; i <= arches; ++i)
            {
                float x = left + i * module;
                float pier = 2 * band;
                p.Box(Brick, x, (Deck + spring) * .5f, z + .112f, pier, spring - Deck, .263f);
                p.Box(Biscuit, x, Deck + .073f, z + .11f, pier + .090f, .146f, .365f);
                p.Box(Rose, x, Deck + .186f, z + .11f, pier + .041f, .080f, .307f);
                for (int j = 0; j < 6; ++j)
                {
                    float y = Deck + .24f + j * .141f;
                    p.Box(Mortar, x, y, z + .111f, pier + .002f, .011f, .268f);
                    p.Box(Mortar, x + (j % 2 == 0 ? -.058f : .058f), y + .071f, z - .020f,
                        .009f, .132f, .006f);
                }
                p.Box(Oxide, x, spring - .093f, z + .11f, pier + .046f, .043f, .309f);
                p.Box(Biscuit, x, spring - .033f, z + .11f, pier + .093f, .077f, .357f);
                // The outermost jambs have exposed returns above the spring.
                if (i == 0 || i == arches)
                    p.Box(Brick, x + (i == 0 ? -1 : 1) * band * .5f, (head + spring) * .5f,
                        z + .117f, band, head - spring, .193f);

                p.Box(Brick, x, (head + Deck) * .5f, back, .201f, head - Deck, .215f);
                p.Box(Biscuit, x, Deck + .084f, back, .282f, .168f, .291f);
                p.Box(Biscuit, x, head - .094f, back, .276f, .125f, .286f);
            }
            p.Box(Biscuit, mid, head + .019f, z + .104f, length + .41f, .047f, .332f);
            p.Box(Wood, mid, head + .075f, z + .103f, length + .44f, .096f, .312f);
            p.Box(Wood, mid, head + .065f, back, length + .42f, .128f, .273f);

            Roof(p, left - .25f, right + .25f, z - .172f, back + .235f, head + .078f, arches);
            p.Finish(kit, parent, "Cloister fabric");
        }

        private static void RoofSheet(Pieces p, string col, float left, float right,
            float za, float ya, float zb, float yb, float thick)
        {
            p.Block(col, V((left + right) * .5f, (ya + yb - thick) * .5f, (za + zb) * .5f),
                V((right - left) * .5f, 0, 0), V(0, thick * .5f, 0),
                V(0, (yb - ya) * .5f, (zb - za) * .5f));
        }

        // A low, folded barrel profile. Also closes the little visible tile mouths.
        private static void Roll(Pieces p, string col, Vector3 a, Vector3 b, Vector3 across,
            Vector3 normal, float radius, float height)
        {
            Surface skin = p.Of(col);
            Vector3 prev = -across * radius;
            for (int k = 1; k <= 4; ++k)
            {
                float angle = k * Mathf.PI * .25f;
                Vector3 next = -across * (Mathf.Cos(angle) * radius) + normal * (Mathf.Sin(angle) * height);
                Vector3 desired = (prev + next).normalized;
                Vector3 faceNormal = Vector3.Cross(b - a, next - prev);
                if (Vector3.Dot(faceNormal, desired) > 0)
                    skin.Quad(a + prev, b + prev, b + next, a + next);
                else
                    skin.Quad(a + next, b + next, b + prev, a + prev);
                Vector3 capNormal = Vector3.Cross(prev, next);
                if (Vector3.Dot(capNormal, b - a) > 0)
                {
                    skin.Triangle(b, b + prev, b + next);
                    skin.Triangle(a, a + next, a + prev);
                }
                else
                {
                    skin.Triangle(a, a + prev, a + next);
                    skin.Triangle(b, b + next, b + prev);
                }
                prev = next;
            }
        }

        private static void Roof(Pieces p, float l, float r, float front, float back, float y, int modules)
        {
            float ridge = (front + back) * .5f, top = y + .47f;
            RoofSheet(p, Wood, l, r, front, y, ridge, top, .072f);
            RoofSheet(p, Wood, l, r, ridge, top, back, y, .072f);
            for (int side = 0; side < 2; ++side)
            {
                float eave = side == 0 ? front : back;
                Vector3 slope = V(0, top - y, ridge - eave).normalized;
                Vector3 normal = V(0, Mathf.Abs(slope.z), side == 0 ? -slope.y : slope.y);
                int lanes = Mathf.Max(2, Mathf.RoundToInt((r - l) / .222f));
                for (int course = 0; course < 3; ++course)
                {
                    float t0 = course / 3f, t1 = (course + 1) / 3f;
                    float za = Mathf.Lerp(eave, ridge, t0), zb = Mathf.Lerp(eave, ridge, t1);
                    float ya = Mathf.Lerp(y, top, t0) + .027f, yb = Mathf.Lerp(y, top, t1) + .013f;
                    if (side == 0) RoofSheet(p, course == 1 ? Rose : Clay, l, r, za, ya, zb, yb, .028f);
                    else RoofSheet(p, course == 1 ? Rose : Clay, l, r, zb, yb, za, ya, .028f);
                    for (int i = 0; i < lanes; ++i)
                    {
                        float x = l + (i + .5f) * (r - l) / lanes;
                        Roll(p, (i + course * 2) % 5 == 0 ? Clay : ClayLight,
                            V(x, ya, za) - slope * .012f, V(x, yb, zb),
                            Vector3.right, normal, .044f, .033f);
                    }
                }
                for (int j = 0; j <= modules; ++j)
                {
                    float x = Mathf.Lerp(l + .085f, r - .085f, j / (float)modules);
                    p.Rod(Wood, V(x, y - .081f, eave + (side == 0 ? .012f : -.012f)),
                        V(x, top - .083f, ridge), .061f, .088f);
                }
            }
            int caps = Mathf.CeilToInt((r - l) / .42f);
            for (int i = 0; i < caps; ++i)
            {
                float x1 = Mathf.Lerp(l, r, i / (float)caps), x2 = Mathf.Lerp(l, r, (i + 1f) / caps);
                Roll(p, i % 3 == 0 ? Clay : ClayLight, V(x1, top + .015f, ridge),
                    V(x2 - .004f, top + .015f, ridge), Vector3.forward, Vector3.up, .089f, .064f);
            }
        }

        private sealed class Paper
        {
            internal readonly Pieces pieces = new Pieces();
            private float face = .015f;

            internal void Shape(string col, params float[] xy)
            {
                Vector2[] a = new Vector2[xy.Length / 2];
                for (int i = 0; i < a.Length; ++i) a[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
                Layer(col, a);
            }

            internal void Oval(string col, float x, float y, float rx, float ry)
            {
                Vector2[] a = new Vector2[12];
                for (int i = 0; i < a.Length; ++i)
                {
                    float t = i * 2 * Mathf.PI / a.Length;
                    a[i] = new Vector2(x + rx * Mathf.Cos(t), y + ry * Mathf.Sin(t));
                }
                Layer(col, a);
            }

            private void Layer(string col, Vector2[] a)
            {
                face -= .0013f;
                pieces.Cut(col, a, face, face + .015f);
            }
        }

        private static Transform Figure(CampusKit kit, Transform yard, Vector3 feet, float yaw, bool upper)
        {
            Transform t = Group(yard, upper ? "Mina in her straw boater" : "Otis with the folded programme");
            t.localPosition = feet;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            Paper a = new Paper();
            const string ink = "39353B", chalk = "FFF1CA", teal = "387477";
            const string ochre = "DBA14D", poppy = "BE584B", blue = "465870";
            string skin = upper ? "F0B68C" : "B97956";
            string shade = upper ? "D88E6C" : "9A5C46";
            a.pieces.Box(Biscuit, 0, .005f, 0, .218f, .010f, .108f);

            if (upper)
            {
                a.Shape(skin, -.084f,.39f, -.020f,.39f, -.039f,.054f, -.087f,.053f);
                a.Shape(skin, .019f,.39f, .080f,.39f, .097f,.054f, .051f,.054f);
                a.Shape(chalk, -.085f,.136f, -.043f,.130f, -.042f,.048f, -.089f,.05f);
                a.Shape(chalk, .046f,.131f, .093f,.137f, .099f,.05f, .051f,.047f);
                a.Shape(ink, -.092f,.063f, -.039f,.061f, -.035f,.015f, -.126f,.012f, -.124f,.035f);
                a.Shape(ink, .049f,.060f, .099f,.063f, .129f,.035f, .130f,.012f, .048f,.013f);
                a.Shape(poppy, -.083f,.52f, .082f,.52f, .135f,.336f, .068f,.318f,
                    .006f,.329f, -.063f,.312f, -.130f,.337f);
                a.Shape(Oxide, -.054f,.481f, -.057f,.332f, -.079f,.326f);
                a.Shape(Oxide, .051f,.484f, .095f,.329f, .069f,.323f);
                a.Shape(teal, -.057f,.733f, .061f,.735f, .110f,.692f, .086f,.594f,
                    .099f,.491f, .025f,.476f, -.006f,.500f, -.093f,.482f, -.085f,.612f, -.119f,.674f);
                a.Shape(teal, -.088f,.705f, -.125f,.680f, -.161f,.543f, -.113f,.526f, -.074f,.624f);
                a.Oval(skin, -.137f,.512f,.024f,.040f);
                a.Shape(teal, .075f,.706f, .109f,.697f, .145f,.652f, .162f,.722f,
                    .205f,.707f, .187f,.593f, .145f,.572f, .082f,.630f);
                a.Shape(skin, .159f,.712f, .169f,.790f, .185f,.825f, .200f,.824f,
                    .197f,.790f, .218f,.804f, .224f,.791f, .204f,.756f, .201f,.703f);
                a.Shape(ochre, -.087f,.705f, -.069f,.717f, .089f,.522f, .079f,.503f);
                a.Shape(Wood, .039f,.545f, .116f,.559f, .130f,.483f, .047f,.470f);
                a.Shape(ochre, .040f,.544f, .116f,.558f, .102f,.522f, .061f,.515f);
            }
            else
            {
                a.Shape(blue, -.096f,.502f, .091f,.499f, .105f,.276f, .084f,.053f,
                    .029f,.052f, .016f,.269f, -.011f,.342f, -.042f,.066f, -.101f,.065f, -.087f,.294f);
                a.Shape(ink, -.103f,.077f, -.041f,.072f, -.036f,.014f, -.136f,.013f, -.137f,.036f);
                a.Shape(ink, .026f,.071f, .088f,.072f, .124f,.037f, .122f,.013f, .025f,.014f);
                a.Shape(chalk, -.067f,.735f, .059f,.739f, .113f,.704f, .125f,.611f,
                    .086f,.580f, -.095f,.579f, -.136f,.616f, -.119f,.697f);
                a.Shape(ochre, -.076f,.718f, -.018f,.665f, .052f,.725f, .081f,.706f,
                    .075f,.668f, .105f,.617f, .095f,.479f, -.101f,.479f, -.099f,.619f, -.076f,.668f);
                a.Shape(teal, -.102f,.511f, .096f,.510f, .094f,.483f, -.101f,.483f);
                a.Shape(skin, -.129f,.626f, -.092f,.613f, -.114f,.550f, -.038f,.563f,
                    -.028f,.534f, -.129f,.503f, -.160f,.532f);
                a.Shape(skin, .092f,.615f, .129f,.631f, .155f,.525f, .128f,.511f, .087f,.570f);
                a.Shape(chalk, -.024f,.625f, .047f,.607f, .119f,.647f, .095f,.542f,
                    .037f,.508f, -.038f,.532f);
                a.Shape(Biscuit, .047f,.607f, .119f,.647f, .095f,.542f, .037f,.508f);
                a.Shape(poppy, -.017f,.603f, .040f,.587f, .037f,.575f, -.020f,.591f);
                a.Oval(skin, -.036f,.552f,.027f,.020f);
                a.Oval(skin, .118f,.551f,.027f,.021f);
            }

            a.Oval(ink, -.006f,.842f, .093f, upper ? .093f : .112f);
            a.Shape(shade, -.037f,.802f, .036f,.803f, .042f,.733f, .003f,.705f, -.045f,.739f);
            a.Shape(chalk, -.044f,.748f, .002f,.711f, -.035f,.682f, -.067f,.727f);
            a.Shape(chalk, .042f,.746f, .064f,.725f, .031f,.687f, .002f,.711f);
            a.Oval(shade, -.077f,.826f,.019f,.024f);
            a.Oval(skin, .073f,.828f,.021f,.025f);
            a.Shape(skin, -.076f,.872f, -.045f,.906f, .041f,.906f, .076f,.872f,
                .071f,.800f, .042f,.769f, -.004f,.758f, -.055f,.780f, -.075f,.817f);
            if (upper)
            {
                a.Shape(ink, -.078f,.887f, .057f,.896f, .078f,.866f, .058f,.844f,
                    .042f,.865f, -.004f,.852f, -.034f,.865f, -.077f,.820f);
                a.Shape(ochre, -.133f,.892f, -.074f,.906f, -.059f,.954f, .050f,.963f,
                    .076f,.917f, .128f,.908f, .133f,.893f, -.009f,.882f, -.130f,.879f);
                a.Shape(teal, -.073f,.911f, .072f,.924f, .076f,.910f, -.077f,.898f);
                a.Oval(poppy, .073f,.914f,.018f,.016f);
            }
            else
            {
                a.Shape(ink, -.084f,.862f, -.080f,.908f, -.053f,.936f, -.026f,.930f,
                    -.004f,.955f, .021f,.945f, .048f,.952f, .085f,.916f,
                    .082f,.864f, .064f,.840f, .047f,.876f, .013f,.870f, -.030f,.880f, -.063f,.848f);
                a.Shape(ink, -.066f,.851f, -.018f,.851f, -.018f,.845f, -.066f,.843f);
                a.Shape(ink, .016f,.852f, .062f,.857f, .062f,.849f, .016f,.846f);
            }
            a.Oval(ink, -.041f,.837f,.008f,.009f);
            a.Oval(ink, .035f,.840f,.008f,.009f);
            a.Shape(shade, -.003f,.836f, .009f,.817f, -.006f,.812f);
            a.Shape(ink, -.021f,.795f, .003f,.791f, .031f,.800f, .012f,.785f, -.008f,.784f);
            if (upper) a.Oval(shade, -.055f,.813f,.014f,.007f);
            a.pieces.Finish(kit, t, "Painted card");
            return t;
        }
    }
}
