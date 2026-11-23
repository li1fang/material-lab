using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    internal sealed class ToyLabVehicle : IDisposable
    {
        [Serializable] sealed class Section
        {
            public Vector2[] loop;
            public string[] left, right, wheels;
        }
        [Serializable] sealed class Geometry { public string version; public float spacing; public Section front, rear; }

        sealed class Shoe
        {
            public Transform part, car;
            public TrackPath path;
            public float start;
            public Vector3 offset;
            public Quaternion rest;
            public Vector3 direction;
        }

        sealed class Wheel
        {
            public Transform part;
            public Quaternion rest;
            public Vector3 axle;
        }

        sealed class TrackPath
        {
            readonly Vector2[] points;
            readonly float[] length;
            public TrackPath(Vector2[] points)
            {
                this.points = points;
                length = new float[points.Length + 1];
                for (int i = 0; i < points.Length; i++)
                    length[i + 1] = length[i] + Vector2.Distance(points[i], points[(i + 1) % points.Length]);
            }

            public float Project(Vector2 point)
            {
                float best = float.PositiveInfinity;
                float at = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 edge = points[(i + 1) % points.Length] - points[i];
                    float along = Mathf.Clamp01(Vector2.Dot(point - points[i], edge) / Mathf.Max(1e-9f, edge.sqrMagnitude));
                    float error = (points[i] + edge * along - point).sqrMagnitude;
                    if (error >= best) continue;
                    best = error;
                    at = Mathf.Lerp(length[i], length[i + 1], along);
                }
                return at;
            }

            public Vector3 Point(float distance)
            {
                float at = Mathf.Repeat(distance, length[^1]);
                int index = Array.BinarySearch(length, at);
                if (index < 0) index = ~index - 1;
                var point = Vector2.Lerp(points[index], points[(index + 1) % points.Length],
                    Mathf.InverseLerp(length[index], length[index + 1], at));
                return new Vector3(0, point.y, point.x);
            }

            public Vector3 Tangent(float at) => (Point(at + .025f) - Point(at - .025f)).normalized;
        }

        const float Scale = .64f;
        // Authoring colors from build_gemini_bvs10.py, matched by the FBX material names.
        static readonly Dictionary<string, string> PaperColors = new(StringComparer.Ordinal) {
            ["ivory"] = "E8D8BF", ["ivory_light"] = "F4E6D0", ["ivory_shade"] = "BCA890",
            ["ink"] = "332B46", ["edge"] = "B78F6B", ["blue"] = "777FBC", ["lilac"] = "A98BC4",
            ["rose"] = "BE83A9", ["glass"] = "344B58", ["glint"] = "607784", ["cabin"] = "292A3F",
            ["track"] = "393448", ["wheel"] = "59506C", ["hub"] = "7A718C", ["module"] = "697F91",
            ["Cabin | matte warm white"] = "EBE8E1", ["Lightly tinted clear vinyl"] = "7896A6"
        };
        readonly List<Shoe> shoes = new();
        readonly List<Wheel> wheels = new();
        readonly Dictionary<Material, Material> paints = new();
        float trackTravel;
        public Transform Root { get; }
        public float HalfWidth { get; }
        public float HalfLength { get; }

        public ToyLabVehicle(Transform parent)
        {
            var geometry = JsonUtility.FromJson<Geometry>(Resources.Load<TextAsset>("MaterialLabVehicle/tracks").text);
            Root = CampusKit.Node("Gemini · full twin crawler", parent, Vector3.zero);
            var body = CampusKit.Node("Vehicle geometry", Root, Vector3.zero);
            Assemble(body, "front", geometry.front, geometry.spacing * .5f);
            Assemble(body, "rear", geometry.rear, -geometry.spacing * .5f);
            if (shoes.Count != geometry.front.left.Length + geometry.front.right.Length +
                geometry.rear.left.Length + geometry.rear.right.Length)
                throw new InvalidOperationException("Gemini track links are incomplete");
            body.localScale = Vector3.one * Scale;
            var renderers = body.GetComponentsInChildren<MeshRenderer>(false);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            body.position += new Vector3(Root.position.x - bounds.center.x, Root.position.y - bounds.min.y, Root.position.z - bounds.center.z);
            HalfWidth = bounds.extents.x;
            HalfLength = bounds.extents.z;
            Debug.Log($"LAB_VEHICLE_ASSEMBLY {geometry.version} meshes={renderers.Length} shoes={shoes.Count} wheels={wheels.Count} halfWidth={HalfWidth:F4} halfLength={HalfLength:F4}");
        }

        void Assemble(Transform parent, string label, Section section, float position)
        {
            var car = CampusKit.Node(label + " section", parent, Vector3.forward * position);
            var asset = Resources.Load<GameObject>("MaterialLabVehicle/" + label);
            if (!asset) throw new InvalidOperationException("Missing Gemini section " + label);
            var donor = UnityEngine.Object.Instantiate(asset, car, false);
            var nodes = donor.GetComponentsInChildren<Transform>(true);
            Transform Named(string name) => Array.Find(nodes, part => part.name == name)
                ?? throw new InvalidOperationException("Gemini missing " + label + "/" + name);
            var forward = Named(label + "_carriage_basis_forward");
            var up = Named(label + "_carriage_basis_up");
            Vector3 origin = forward.parent.position;
            donor.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(forward.position - origin, up.position - origin))
                * donor.transform.localRotation;
            donor.transform.position += car.position - forward.parent.position;
            foreach (var animated in donor.GetComponentsInChildren<Animator>(true)) animated.enabled = false;
            foreach (var animated in donor.GetComponentsInChildren<Animation>(true)) animated.enabled = false;
            foreach (var collider in donor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var part in donor.GetComponentsInChildren<MeshRenderer>(true))
            {
                part.sharedMaterials = part.sharedMaterials.Select(Surface).ToArray();
            }
            var path = new TrackPath(section.loop);
            foreach (string name in section.left.Concat(section.right))
            {
                Transform shoe = Named(name);
                Vector3 at = car.InverseTransformPoint(shoe.position);
                float start = path.Project(new Vector2(at.z, at.y));
                shoes.Add(new Shoe {
                    part = shoe, car = car, path = path, start = start,
                    offset = at - path.Point(start),
                    rest = Quaternion.Inverse(car.rotation) * shoe.rotation,
                    direction = path.Tangent(start)
                });
            }
            foreach (string name in section.wheels.Where(n => n.Contains("wheel", StringComparison.Ordinal)))
            {
                var wheel = Named(name);
                wheels.Add(new Wheel { part = wheel, rest = wheel.localRotation, axle = wheel.InverseTransformDirection(car.right) });
            }
        }

        Material Surface(Material source)
        {
            if (!source) throw new InvalidOperationException("Gemini model is missing an original material slot");
            if (paints.TryGetValue(source, out var paint)) return paint;
            bool named = PaperColors.TryGetValue(source.name.Split('.')[0], out string hex);
            if (!named && source.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)) return source;
            paint = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name };
            Color color = source.HasProperty("_Color") || source.HasProperty("_BaseColor") ? source.color : Color.white;
            if (named && ColorUtility.TryParseHtmlString("#" + hex, out var actual)) color = actual;
            paint.SetColor("_BaseColor", color);
            if (source.HasProperty("_MainTex") || source.HasProperty("_BaseMap")) paint.SetTexture("_BaseMap", source.mainTexture);
            if (source.HasProperty("_Metallic")) paint.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            paint.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : .18f);
            paints.Add(source, paint);
            return paint;
        }

        public void Pose(Vector3 position, float signedTravel)
        {
            Root.localPosition = position;
            trackTravel -= signedTravel / Scale;
            float travel = trackTravel;
            foreach (var shoe in shoes)
            {
                float at = shoe.start + travel;
                var rotation = Quaternion.FromToRotation(shoe.direction, shoe.path.Tangent(at));
                var along = shoe.path.Point(at);
                Vector3 offset = rotation * shoe.offset;
                shoe.part.SetPositionAndRotation(shoe.car.TransformPoint(along + offset),
                    shoe.car.rotation * rotation * shoe.rest);
            }
            foreach (var wheel in wheels)
                wheel.part.localRotation = wheel.rest * Quaternion.AngleAxis(travel * Mathf.Rad2Deg / .18f, wheel.axle);
        }

        public void Dispose()
        {
            foreach (var material in paints.Values) UnityEngine.Object.Destroy(material);
        }
    }
}
