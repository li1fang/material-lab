using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction
{
    // All positions are in specimen space. A released span can freeze this mesh and
    // keep the same silhouette, down to its leaves, when handed to a Rigidbody.
    public sealed class BranchSkin
    {
        public readonly Mesh Mesh = new Mesh { name = "Green wood" };
        readonly List<Vector3> vertices = new List<Vector3>(7000);
        readonly List<Color32> colors = new List<Color32>(7000);
        readonly List<int> triangles = new List<int>(12000);
        static readonly Color Bark = new Color(.42f,.285f,.16f);
        static readonly Color Sap = new Color(.94f,.77f,.48f);
        static readonly Color Midrib = new Color(.68f,.72f,.29f);
        BranchModel model;

        public BranchSkin() { Mesh.MarkDynamic(); }

        int Add(Vector3 p, Color color)
        {
            int i = vertices.Count;
            vertices.Add(p);
            colors.Add(color);
            return i;
        }
        void Tri(int a, int b, int c)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }

        public void Update(BranchModel branch, int start, int end)
        {
            model = branch;
            vertices.Clear(); colors.Clear(); triangles.Clear();
            for (int j = start; j < end; j++) BarkSection(j);
            if (start == 0) Cap(0, false);
            if (end == BranchModel.Links) Cap(end, true);
            int seam = model.CrackJoint;
            if (seam >= 0 && start < seam && end >= seam) FractureFace(seam, false);
            if (seam >= 0 && start <= seam && end > seam) FractureFace(seam, true);
            if (seam >= 0 && !model.Detached) Bridges(seam);

            for (int twig = 0; twig < 8; twig++)
            {
                int j = 5 + twig * 2;
                if (j >= start && j < end) Shoot(j, twig);
            }
            Mesh.Clear();
            Mesh.SetVertices(vertices); Mesh.SetColors(colors); Mesh.SetTriangles(triangles,0);
            Mesh.RecalculateNormals(); Mesh.RecalculateBounds();
        }

        Vector3 Axis(int ring)
        {
            int a = Mathf.Max(0, ring - 1), b = Mathf.Min(BranchModel.Links, ring + 1);
            return (model.Centers[b] - model.Centers[a]).normalized;
        }
        Vector3 Radial(int ring, float phi)
        {
            Vector3 axis = Axis(ring);
            Vector3 up = Vector3.Cross(Vector3.forward,axis).normalized;
            return up * Mathf.Cos(phi) + Vector3.Cross(axis,up) * Mathf.Sin(phi);
        }
        Vector3 Ring(int ring, int side, bool tipHalf)
        {
            int k = side % BranchModel.Fibers;
            float phi = k * 2f * Mathf.PI / BranchModel.Fibers;
            float radius = model.Radius((float)ring / BranchModel.Links);
            float noise = Mathf.Sin(7.2f * k + .6f * ring + model.Profile.seed);
            Vector3 axis = Axis(ring);
            Vector3 p = model.Centers[ring] + Radial(ring,phi) * radius * (1 + .055f * noise);
            // Interlocking ends follow the grain even before this particular joint fails.
            p += axis * radius * (.52f * Mathf.Sin(k * 7.81f + ring * .61f) + .23f * noise);
            if (ring != model.CrackJoint) return p;
            int prev = (k + BranchModel.Fibers - 1) % BranchModel.Fibers;
            float weight = ((model.IsBroken(k) ? 1 : 0) + (model.IsBroken(prev) ? 1 : 0)) * .5f;
            float opening = model.Detached ? .36f : model.Gap * 1.55f;
            return p + axis * radius * opening * weight * (tipHalf ? 1 : -1);
        }
        void BarkSection(int j)
        {
            for (int f = 0; f < BranchModel.Fibers; f++)
            {
                float m = .85f + .12f * Mathf.Sin(f * 5.1f + model.Profile.seed)
                    + .035f * Mathf.Sin(j * 2.3f + f);
                Color c = Bark * m; c.a = 1;
                int a = Add(Ring(j,f,true),c);
                int b = Add(Ring(j,f+1,true),c);
                int d = Add(Ring(j+1,f,false),c);
                int e = Add(Ring(j+1,f+1,false),c);
                Tri(a,b,d); Tri(b,e,d);
                if ((f*7+j*5)%9==0)
                {
                    var radial=Radial(j,(f+.5f)*2f*Mathf.PI/BranchModel.Fibers);
                    var axis=Axis(j);
                    var spot=(vertices[a]+vertices[b]+vertices[d]+vertices[e])*.25f+radial*.0004f;
                    var tangent=Vector3.Cross(axis,radial);
                    float r=model.Radius((float)j/BranchModel.Links);
                    Color mark=Color.Lerp(Bark,Sap,.38f);
                    int l=Add(spot-axis*r*.12f,mark), top=Add(spot+tangent*r*.065f,mark);
                    int right=Add(spot+axis*r*.18f,mark), bot=Add(spot-tangent*r*.06f,mark);
                    Tri(l,top,right); Tri(l,right,bot);
                }
            }
        }
        void Bridges(int ring)
        {
            float radius=model.Radius((float)ring/BranchModel.Links);
            Vector3 axis=Axis(ring);
            for(int f=0;f<BranchModel.Fibers;f++)
            {
                if(model.IsBroken(f)) continue;
                var radial=Radial(ring,(f+.5f)*2f*Mathf.PI/BranchModel.Fibers);
                var width=Vector3.Cross(axis,radial)*radius*.12f;
                var center=model.Centers[ring]+radial*radius*.84f;
                var span=axis*radius*(.48f+model.Gap);
                var mid=center+radial*radius*.17f*model.Gap;
                int a=Add(center-span-width,Sap), b=Add(center-span+width,Sap);
                int c=Add(mid-width*.46f,Sap), d=Add(mid+width*.46f,Sap);
                int e=Add(center+span-width,Bark), g=Add(center+span+width,Bark);
                Tri(a,b,c); Tri(b,d,c); Tri(c,d,e); Tri(d,g,e);
            }
        }
        void Cap(int ring, bool end)
        {
            int middle = Add(model.Centers[ring], Sap * .83f);
            for (int f = 0; f < BranchModel.Fibers; f++)
            {
                int a = Add(Ring(ring,f,!end), Sap), b = Add(Ring(ring,f+1,!end), Sap);
                if (end) Tri(middle,a,b); else Tri(middle,b,a);
            }
        }
        void FractureFace(int ring, bool tip)
        {
            var axis = Axis(ring);
            for (int f = 0; f < BranchModel.Fibers; f++)
            {
                if (!model.IsBroken(f)) continue;
                var a = Ring(ring,f,tip); var b = Ring(ring,f+1,tip);
                float length = model.Radius((float)ring / BranchModel.Links);
                var inner = model.Centers[ring] + axis * (tip ? .1f : -.1f) * length * model.Gap;
                Color shade = Color.Lerp(Sap, Bark, .14f + .16f * Mathf.Abs(Mathf.Sin(f*3.62f)));
                int i = Add(a,shade), k = Add(b,shade), c = Add(inner,Sap);
                Tri(i,k,c);
                // Tapered sapwood slivers give the tear a readable longitudinal silhouette.
                var root = Vector3.Lerp(inner,(a+b)*.5f,.74f);
                var point = root + axis * length * (.55f + .7f * Mathf.Abs(Mathf.Sin(f*8.21f)))
                    * (tip ? -1 : 1);
                int s = Add(Vector3.Lerp(root,a,.48f),shade);
                int t = Add(Vector3.Lerp(root,b,.48f),Sap);
                Tri(s,t,Add(point,Sap));
            }
        }

        void Stick(Vector3 a, Vector3 b, float r)
        {
            Vector3 axis = (b-a).normalized;
            Vector3 front = Vector3.Cross(axis,Vector3.forward).normalized;
            Vector3 other = Vector3.Cross(axis,front);
            int start = vertices.Count;
            for (int i=0; i<6; i++)
            {
                float angle = i * Mathf.PI / 3;
                var d = r * (front * Mathf.Cos(angle) + other * Mathf.Sin(angle));
                Add(a+d,Bark); Add(b+d*.46f,Color.Lerp(Bark,Midrib,.3f));
            }
            for (int i=0; i<6; i++)
            {
                int first = start+2*i, next = start+2*((i+1)%6);
                Tri(first,next,first+1); Tri(next,next+1,first+1);
            }
        }
        void Shoot(int j, int index)
        {
            var root = model.Centers[j];
            Vector3 x = Axis(j), y = Radial(j,0), z = Vector3.Cross(x,y);
            float sign = index % 3 == 1 ? -1f : 1f;
            float scale = model.Profile.length;
            Vector3 dir = (x * .58f + y * sign + z * Mathf.Sin(index * 2.8f)).normalized;
            Vector3 end = root + dir * scale * (.096f + .024f * Mathf.Sin(index));
            Stick(root,end,scale*.0052f);
            for (int i=0; i<3; i++)
            {
                Vector3 basePoint = Vector3.Lerp(root,end,.47f + i*.25f);
                float flutter = .035f * Mathf.Sin(model.Tick*.12f+index+i) * Mathf.Min(1,Mathf.Abs(model.Load));
                var along = (dir*.72f + x*(i == 1 ? -.8f : .68f) + y*flutter + z*Mathf.Sin(i+index)).normalized;
                Leaf(basePoint,along,scale*(.084f + .022f*Mathf.Sin(index*3+i)),index+i);
            }
        }
        void Leaf(Vector3 root, Vector3 along, float size, int tint)
        {
            var width = Vector3.Cross(along, new Vector3(.2f,.22f,-1f)).normalized;
            var toward = Vector3.Cross(along,width);
            Color green = Color.Lerp(new Color(.15f,.39f,.23f),new Color(.33f,.54f,.22f),
                .5f+.5f*Mathf.Sin(tint*4.1f));
            int first = vertices.Count;
            for (int i=0; i<=8; i++)
            {
                float t=i/8f;
                float w=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.9f)*size*.235f;
                Vector3 mid=root+along*size*t + toward*(Mathf.Sin(t*Mathf.PI)*size*.065f);
                var fold=toward*w*.29f;
                Add(mid-width*w-fold,green*.89f); Add(mid-width*w*.052f,green);
                Add(mid,Midrib); Add(mid+width*w*.052f,green); Add(mid+width*w-fold,green*1.09f);
                if(i==0) continue;
                for(int col=0; col<4; col++)
                {
                    int a=first+(i-1)*5+col;
                    Tri(a,a+1,a+5); Tri(a+1,a+6,a+5);
                }
            }
        }
    }
}
