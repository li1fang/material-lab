using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Sample
{
    public sealed class CeramicSpecimen : MonoBehaviour
    {
        public readonly CeramicRim Rim=new CeramicRim();
        public readonly List<CeramicContact> Contacts=new List<CeramicContact>();
        public int Step, Fragments;
        public Vector3 LastDamage { get; private set; }
        MeshFilter surface;
        MeshCollider hull;
        Material finish;
        Rigidbody striker, releasedWood;
        readonly List<Mesh> chips=new List<Mesh>();
        readonly List<Rigidbody> pieces=new List<Rigidbody>();
        readonly List<Vector3> origins=new List<Vector3>();
        readonly Dictionary<Collider,float> contactPeaks=new Dictionary<Collider,float>();
        public float MaxChipTravel { get; private set; }

        void FixedUpdate()
        {
            for(int i=0;i<pieces.Count;i++) if(pieces[i])
                MaxChipTravel=Mathf.Max(MaxChipTravel,Vector3.Distance(pieces[i].position,origins[i]));
        }

        public void Initialize(Material finishMaterial)
        {
            finish=finishMaterial;
            surface=gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>().sharedMaterial=finish;
            hull=gameObject.AddComponent<MeshCollider>();
            hull.contactOffset=.0015f;
            gameObject.AddComponent<Rigidbody>().isKinematic=true;
            Rebuild();
        }
        public void ListenTo(Rigidbody sphere,Rigidbody wood)
        {
            striker=sphere;
            releasedWood=wood;
        }
        void Rebuild()
        {
            Mesh old=surface.sharedMesh;
            var next=CeramicShell.Make(Rim);
            hull.sharedMesh=next;
            surface.sharedMesh=next;
            if(old) Destroy(old);
        }
        void OnCollisionEnter(Collision collision) => RecordContact(collision,true);
        void OnCollisionStay(Collision collision) => RecordContact(collision,false);
        void OnCollisionExit(Collision collision) => contactPeaks.Remove(collision.collider);
        void RecordContact(Collision collision,bool entering)
        {
            var body=collision.rigidbody;
            if(!body || (body!=striker && body!=releasedWood) || collision.contactCount==0) return;
            // Initial speculative contact can precede the solver's peak by one or more steps.
            // Keep the onset and new peaks of each collider encounter, including a rolling impact.
            float impulse=collision.impulse.magnitude;
            if(!entering && contactPeaks.TryGetValue(collision.collider,out float peak) && impulse<=peak+.0001f) return;
            contactPeaks[collision.collider]=impulse;
            ContactPoint chosen=collision.GetContact(0);
            float strongest=-1;
            for(int i=0;i<collision.contactCount;i++)
            {
                var p=collision.GetContact(i);
                float weight=p.impulse.sqrMagnitude;
                if(weight>strongest) { strongest=weight; chosen=p; }
            }
            Vector3 point=transform.InverseTransformPoint(chosen.point);
            Vector3 direction=transform.InverseTransformDirection(collision.impulse).normalized;
            var released=Rim.Strike(point,direction,impulse);
            Contacts.Add(new CeramicContact {step=Step,source=body==striker?"striker":"wood",
                localPoint=point,localDirection=direction,impulseNs=impulse,released=released});
            if(released.Length==0) return;
            LastDamage=chosen.point;
            Rebuild();
            var group=new HashSet<int>();
            int previous=-1;
            foreach(int id in released)
            {
                if(previous>=0 && (previous+1)%CeramicRim.Cells!=id)
                { MakeChip(group,point,impulse); group.Clear(); }
                group.Add(id); previous=id;
            }
            MakeChip(group,point,impulse);
        }
        void MakeChip(HashSet<int> cells,Vector3 point,float impulse)
        {
            var mesh=CeramicShell.Make(Rim,cells);
            chips.Add(mesh);
            var part=new GameObject("Detached glazed rim");
            part.transform.SetParent(transform.parent,false);
            var offset=mesh.bounds.center;
            var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++) vertices[i]-=offset;
            mesh.vertices=vertices;mesh.RecalculateBounds();
            Vector3 outward=transform.TransformDirection(new Vector3(point.x,0,point.z).normalized);
            Vector3 origin=transform.TransformPoint(offset);
            part.transform.SetPositionAndRotation(origin+outward*.0015f,transform.rotation);
            var rb=part.AddComponent<Rigidbody>();
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial=finish;
            var collider=part.AddComponent<MeshCollider>();
            collider.convex=true; collider.sharedMesh=mesh; collider.contactOffset=.001f;
            rb.mass=.005f*cells.Count;
            rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity=outward*Mathf.Min(.85f,impulse*1.9f)+Vector3.up*.15f;
            rb.angularVelocity=Vector3.Cross(Vector3.up,outward)*7;
            origins.Add(origin); pieces.Add(rb);
            Fragments++;
        }
        void OnDestroy()
        {
            foreach(var piece in pieces) if(piece) Destroy(piece.gameObject);
            if(surface && surface.sharedMesh) Destroy(surface.sharedMesh);
            foreach(var mesh in chips) if(mesh) Destroy(mesh);
        }
    }
}
