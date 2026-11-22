using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction
{
    // An intact core and a ring of irregular detachable cells share their boundary vertices.
    public static class CeramicShell
    {
        const int Slices=4;
        const int Count=CeramicRim.Cells*Slices;
        static readonly Color Glaze=new Color(.69f,.86f,.81f), Blue=new Color(.16f,.45f,.52f);
        static readonly Color Clay=new Color(.82f,.52f,.32f);

        sealed class Surface
        {
            public readonly List<Vector3> p=new List<Vector3>();
            public readonly List<Color> colors=new List<Color>();
            public readonly List<Vector3> normals=new List<Vector3>();
            public readonly List<Vector2> uv=new List<Vector2>();
            public readonly List<int> indices=new List<int>();
            public void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,Vector3 outward)
            {
                int k=p.Count; p.Add(a);p.Add(b);p.Add(c);p.Add(d);
                Vector3 normal=Vector3.Cross(b-a,c-a);
                if(normal.sqrMagnitude<1e-17f) normal=Vector3.Cross(c-a,d-a);
                bool forward=Vector3.Dot(normal,outward)>0;
                Vector3 flat=(forward?normal:-normal).normalized;
                for(int j=0;j<4;j++)
                {
                    var v=p[k+j];
                    colors.Add(color);
                    uv.Add(new Vector2(v.x,v.z));
                    Vector3 n=flat;
                    if(Mathf.Abs(outward.y)>.9f)
                    {
                        Vector3 radius=new Vector3(v.x,0,v.z);
                        float slope=.043f*2.7f/CeramicRim.Radius*Mathf.Pow(radius.magnitude/CeramicRim.Radius,1.7f);
                        n=(Vector3.up-radius.normalized*slope).normalized*Mathf.Sign(outward.y);
                    }
                    normals.Add(n);
                }
                if((b-a).sqrMagnitude>1e-12f && Vector3.Cross(b-a,c-a).sqrMagnitude>1e-17f)
                {indices.Add(k);indices.Add(k+(forward?1:2));indices.Add(k+(forward?2:1));}
                if(Vector3.Cross(c-a,d-a).sqrMagnitude>1e-17f)
                {indices.Add(k);indices.Add(k+(forward?2:3));indices.Add(k+(forward?3:2));}
            }
        }

        // Ring 4 is the fracture line. Narrow rings carry continuous painted glaze bands.
        static readonly float[] Rings={0,.28f,.46f,.61f,.73f,.83f,.85f,.915f,.938f,.978f,1f};
        const int Seam=4;
        static Vector3 Position(int slice,int ring,bool underside)
        {
            float a=2*Mathf.PI*(slice%Count)/Count;
            float u=Rings[ring];
            if(ring==Seam) u+=.043f*Mathf.Sin(11*a+.3f)+.026f*Mathf.Sin(27*a);
            if(ring>=Seam) a+=(1-u)*(.075f*Mathf.Sin(15*a)+.044f*Mathf.Cos(27*a));
            float y=.008f+.043f*Mathf.Pow(u,2.7f);
            if(underside) y-=.006f + .002f*(1-u);
            return new Vector3(Mathf.Cos(a)*u*CeramicRim.Radius,y,Mathf.Sin(a)*u*CeramicRim.Radius);
        }
        public static Mesh Make(CeramicRim rim,HashSet<int> chip=null)
        {
            var s=new Surface();
            bool Has(int i) => chip!=null?chip.Contains((i+CeramicRim.Cells)%CeramicRim.Cells):!rim.Gone(i);
            for(int v=0;v<Count;v++)
            {
                int cell=v/Slices;
                bool has=Has(cell);
                for(int r=0;r<Rings.Length-1;r++)
                {
                    if(r<Seam?chip!=null:!has) continue;
                    Vector3 a=Position(v,r,false),b=Position(v+1,r,false),
                        c=Position(v+1,r+1,false),d=Position(v,r+1,false);
                    Vector3 aa=Position(v,r,true),bb=Position(v+1,r,true),
                        cc=Position(v+1,r+1,true),dd=Position(v,r+1,true);
                    Color color=(r==5||r==7)?Blue:Glaze;
                    if(r==6 && v%12<3) color=Color.Lerp(Blue,Glaze,.28f);
                    s.Face(a,b,c,d,color,Vector3.up);
                    s.Face(aa,bb,cc,dd,Color.Lerp(Glaze,Blue,.13f),Vector3.down);
                    if(r==Rings.Length-2) s.Face(d,c,cc,dd,Blue,new Vector3(c.x,0,c.z));
                    if((r==Seam&&chip!=null)||(r==Seam-1&&!has&&chip==null))
                    {
                        var edge=r==Seam?a:d;
                        var n=new Vector3(edge.x,0,edge.z);
                        if(r==Seam) s.Face(a,b,bb,aa,Clay,-n);
                        else s.Face(d,c,cc,dd,Clay,n);
                    }
                    if(r<Seam) continue;
                    var tangent=new Vector3(-d.z,0,d.x);
                    if(v%Slices==0&&!Has(cell-1)) s.Face(a,d,dd,aa,Clay,-tangent);
                    if(v%Slices==Slices-1&&!Has(cell+1)) s.Face(b,c,cc,bb,Clay,tangent);
                }
            }
            Mesh m=new Mesh{name=chip==null?"Glazed stoneware":"Rim chip"};
            m.SetVertices(s.p);m.SetColors(s.colors);m.SetUVs(0,s.uv);m.SetNormals(s.normals);
            m.SetTriangles(s.indices,0);m.RecalculateBounds();
            return m;
        }
    }
}
