using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    // This is the Material Lab toy support model. Its original ParkingDeck owns all changes
    // to columns, girders, floor poses and queued releases.
    internal sealed class BayFrame
    {
        public const float CoreHeight = 2.95f;

        public readonly Transform Root;
        public readonly CampusLayout Layout;
        public readonly ParkingDeck Deck;
        public Transform[] Posts => Deck.posts;
        public Transform[] Panels => Deck.panels;
        public readonly Transform[] Girders;
        public readonly int SharedGirder;

        readonly BoundarySpan[] rails;
        readonly Dictionary<Collider, string> ids = new();
        readonly Dictionary<Collider, int> coreByCasting = new();
        readonly Dictionary<Collider, int> railByCollider = new();

        public BayFrame(CampusKit kit, Transform parent, float pitch = 4f,
                        string railColor = "FFF8E9", string concreteColor = "D5CDC2", int columns = 2)
        {
            Root = CampusKit.Node("frame", parent, Vector3.zero);
            Layout = FirstRow(columns, pitch);
            Deck = new ParkingDeck(Layout, kit, Root, pitch);
            RemoveParkingFittings(Root);

            var concrete = kit.Paint(concreteColor);
            for (int post = 0; post < Posts.Length; post++)
            {
                var casting = Posts[post].Find("Casting");
                SetPaint(casting, concrete);
                coreByCasting.Add(casting.GetComponent<BoxCollider>(), post);
                Register(Posts[post], "post/" + post);
            }

            Girders = new Transform[Layout.spans.Count];
            for (int edge = 0; edge < Girders.Length; edge++)
            {
                var beam = Girders[edge] = Root.Find("Girder " + edge);
                SetPaint(beam, concrete);
                Register(beam, "girder/" + edge);
            }
            SharedGirder = Layout.spans.IndexOf(new Vector2Int(1, columns + 2));
            if (SharedGirder < 0) throw new InvalidOperationException("Expected a common bay edge.");

            rails = new BoundarySpan[Panels.Length];
            var railPaint = kit.Paint(railColor);
            for (int bay = 0; bay < Panels.Length; bay++)
            {
                var panel = Panels[bay];
                RemoveParkingFittings(panel);
                SetPaint(panel.Find("Sealed slab"), concrete);
                SetPaint(panel.Find("Pavement"), kit.Paint("EADCC6"));
                Register(panel, "deck/" + bay);

                // Build an independently pickable white iron rail on the back of each slab,
                // using the same 37-piece metal span and bend response as the campus fence.
                var center = new Vector3(0, .205f, pitch * .5f - .13f);
                var halfWidth = Vector3.right * (pitch * .5f + .13f);
                var rail = rails[bay] = new BoundarySpan(kit, panel, center - halfWidth,
                                                        center + halfWidth, true, bay);
                rail.root.localScale = new Vector3(1, .48f, .48f);
                foreach (var r in rail.root.GetComponentsInChildren<MeshRenderer>())
                    r.sharedMaterial = railPaint;
                Register(rail.root, "rail/" + bay);
                foreach (var collider in rail.root.GetComponentsInChildren<Collider>())
                    if (collider.enabled) railByCollider.Add(collider, bay);
            }
        }

        // The reference lattice factory accepts two or more rows. Select one row of its
        // topology before constructing any GameObjects; the adjacent slabs keep their
        // original shared indices and refer to a single common beam.
        static CampusLayout FirstRow(int columns, float pitch)
        {
            if (columns < 2 || columns > 32 || !float.IsFinite(pitch) || pitch < 1 || pitch > 50) throw new ArgumentOutOfRangeException(nameof(columns));
            var layout = new CampusLayout { columns = columns, rows = 1 };
            for (int row = 0; row < 2; row++)
                for (int col = 0; col <= columns; col++)
                {
                    int id = layout.posts.Count;
                    layout.posts.Add(new Vector3((col - columns * .5f) * pitch, 0, (row - .5f) * pitch));
                    if (col > 0) layout.spans.Add(new Vector2Int(id-1,id));
                    if (row == 0) continue;
                    layout.spans.Add(new Vector2Int(id-columns-1,id));
                    if (col > 0) layout.floors.Add(new[]{id-columns-2,id-columns-1,id,id-1});
                }
            return layout;
        }

        static void SetPaint(Transform member, Material mat) =>
            member.GetComponent<MeshRenderer>().sharedMaterial = mat;

        static void RemoveParkingFittings(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var t = parent.GetChild(i);
                switch (t.name)
                {
                    case "Entrance ramp": case "Turning landing":
                    case "Ground stall": case "Ground marking": case "Parking stripe":
                    case "Wheel stop": case "Stanchion": case "Edge rail":
                        t.gameObject.SetActive(false);
                        UnityEngine.Object.Destroy(t.gameObject);
                        break;
                }
            }
        }

        void Register(Transform member, string id)
        {
            member.name = id;
            foreach (var collider in member.GetComponentsInChildren<Collider>())
                if (collider.enabled) ids.Add(collider, id);
        }

        public bool TryPart(Collider hit, out string id) => ids.TryGetValue(hit, out id);
        public bool TryCoreBody(Collider hit, out int post) => coreByCasting.TryGetValue(hit, out post);

        public bool TryHitRail(RaycastHit hit, float time)
        {
            if (!railByCollider.TryGetValue(hit.collider, out int bay)) return false;
            var rail = rails[bay];
            var where = rail.root.InverseTransformPoint(hit.point);
            rail.Hit(new Vector2(where.x, where.y), .8f, time);
            rail.Pose(time);
            return true;
        }

        public float RailBend(int bay) => rails[bay].Bend;
        public void Damage(int post, float time) => Deck.Damage(post, time);
        public void Pose(float time) => Deck.Pose(time);
    }
}
