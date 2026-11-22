using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    internal sealed class BoundarySpan
    {
        sealed class Chip{public Transform node;public Vector3 start;public float time=-1;public int course;}
        readonly List<Chip> bricks=new List<Chip>();
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Transform> rails=new List<Transform>();
        readonly CampusKit kit;readonly float width;
        public readonly Transform root;
        public readonly bool metal;
        public int Missing{get;private set;}
        public float Bend{get;private set;}
        public bool Released=>Bend>.67f;
        public float RetainedEndError{
            get{
                bool right=pressureX<=0;var beam=rails[right?36:25];float sign=right?1:-1;
                var tip=beam.localPosition+beam.localRotation*(Vector3.up*beam.localScale.y*.5f*sign);
                return Vector3.Distance(tip,new Vector3(sign*width/2,1.66f,0)+(right?offsetRight:offsetLeft)*1.66f);
            }
        }
        float pressureX,linkLeft,linkRight;
        Vector3 offsetLeft,offsetRight;
        public BoundarySpan(CampusKit kit,Transform yard,Vector3 a,Vector3 b,bool metal,int id){
            this.kit=kit;this.metal=metal;width=Vector3.Distance(a,b)-.38f;
            root=CampusKit.Node("Span "+id,yard,(a+b)/2);
            root.localRotation=Quaternion.LookRotation(Vector3.Cross((b-a).normalized,Vector3.up));
            if(metal){
                for(int i=0;i<=12;i++)rails.Add(kit.Solid(root,"Picket "+i,Vector3.zero,Vector3.one,"307775",true));
                for(int i=0;i<24;i++)rails.Add(kit.Solid(root,"Rail "+i,Vector3.zero,Vector3.one,i<12?"396D72":"4F9490",true));
                Pose(0);return;
            }
            var rng=new System.Random(id+730);
            string[] clay={"D89873","CB8969","DFA67F","BD8066","DC9979"};
            int cols=Mathf.Max(2,Mathf.RoundToInt(width/.60f));float pitch=width/cols;
            for(int r=0;r<8;r++)for(int c=-1;c<cols;c++){
                float x=-width/2+(c+(r%2)*.5f)*pitch;
                float x0=Mathf.Max(-width/2,x)+.009f,x1=Mathf.Min(width/2,x+pitch)-.009f;
                if(x1-x0<.04f)continue;
                float y0=.01f+r*.245f,y1=y0+.234f;
                var bottom=new Vector2(Mathf.Lerp(x0,x1,.39f),y0);var top=new Vector2(Mathf.Lerp(x0,x1,.59f),y1);
                var mid=new Vector2(Mathf.Lerp(x0,x1,.57f),(y0+y1)*.5f);
                string color=r==7?"F2D8AA":clay[rng.Next(clay.Length)];
                Piece(new[]{new Vector2(x0,y0),bottom,mid,top,new Vector2(x0,y1)},color,r);
                Piece(new[]{bottom,new Vector2(x1,y0),new Vector2(x1,y1),top,mid},color,r);
            }
        }
        void Piece(Vector2[] poly,string color,int row){
            Vector2 center=Vector2.zero;foreach(var v in poly)center+=v;center/=poly.Length;
            var points=new List<Vector3>();var face=new List<int>();var core=new List<int>();
            Vector3 V(int i,float z)=>new Vector3(poly[i].x-center.x,poly[i].y-center.y,z);
            void Tri(Vector3 a,Vector3 b,Vector3 c,List<int> idx){
                int n=points.Count;points.Add(a);points.Add(b);points.Add(c);idx.Add(n);idx.Add(n+1);idx.Add(n+2);
            }
            for(int i=0;i<poly.Length;i++){
                int j=(i+1)%poly.Length;
                Tri(new Vector3(0,0,-.115f),V(j,-.115f),V(i,-.115f),face);
                Tri(new Vector3(0,0,.115f),V(i,.115f),V(j,.115f),face);
                Tri(V(i,-.115f),V(j,-.115f),V(j,.115f),core);Tri(V(i,-.115f),V(j,.115f),V(i,.115f),core);
            }
            var mesh=new Mesh();mesh.SetVertices(points);mesh.subMeshCount=2;mesh.SetTriangles(face,0);mesh.SetTriangles(core,1);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var tr=CampusKit.Node("Fired clay "+bricks.Count,root,center);
            tr.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            tr.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[]{kit.Paint(color),kit.Paint("AD7354")};
            var collider=tr.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;
            bricks.Add(new Chip{node=tr,start=center,course=row});
        }
        public void Hit(Vector2 p,float k,float time){
            if(metal){Bend=Mathf.Clamp01(Bend+k*.43f);pressureX=p.x;return;}
            foreach(var c in bricks){
                if(c.time>=0)continue;
                float d=Vector2.Distance(c.start,p);
                if(d<.18f+k*.53f){c.time=time;Missing++;}
            }
            // Each course can remain on overlapping bricks below, or against an end post.
            foreach(var c in bricks){
                if(c.time>=0||c.course==0||Mathf.Abs(c.start.x)>width*.5f-.22f)continue;
                if(!bricks.Exists(n=>n.time<0 && n.course==c.course-1 && Mathf.Abs(n.start.x-c.start.x)<.41f)){
                    c.time=time+.06f*c.course;Missing++;
                }
            }
        }
        public void Support(float left,float right,float time,Vector3 leftAxis,Vector3 rightAxis){
            if(!metal){
                if(left>linkLeft+.01f)Hit(new Vector2(-width/2,1.5f),left*.83f,time);
                if(right>linkRight+.01f)Hit(new Vector2(width/2,1.5f),right*.83f,time);
            }
            linkLeft=left;linkRight=right;
            offsetLeft=root.InverseTransformDirection(leftAxis-Vector3.up);
            offsetRight=root.InverseTransformDirection(rightAxis-Vector3.up);
        }
        public void Contact(float x,float depth,float time){
            if(metal){if(depth>Bend){Bend=Mathf.Clamp01(depth);pressureX=x;}}
            else Hit(new Vector2(x,.64f),depth*1.9f,time);
        }
        Vector3 RailPoint(float t,float h){
            float x=(t-.5f)*width;
            float bend=Bend*(4*t*(1-t))*Mathf.Exp(-Mathf.Pow((x-pressureX)/(.48f+width*.16f),2));
            float loosen=Released?Mathf.InverseLerp(.67f,1,Bend):0;
            float drop=loosen*(pressureX<=0?1-t:t);
            float angle=Mathf.Min(87,bend*59+drop*87)*Mathf.Deg2Rad;
            return new Vector3(x,h*Mathf.Cos(angle)+.12f*(1-drop),h*Mathf.Sin(angle))+
                Vector3.Lerp(offsetLeft,offsetRight,t)*(h+.12f)*(1-drop);
        }
        public void Pose(float time){
            if(metal){
                for(int i=0;i<13;i++)CampusKit.Connect(rails[i],RailPoint(i/12f,.04f),RailPoint(i/12f,1.88f),.075f,.08f);
                for(int i=0;i<24;i++){float h=i<12?.51f:1.54f;int k=i%12;
                    CampusKit.Connect(rails[13+i],RailPoint(k/12f,h),RailPoint((k+1)/12f,h),.095f,.08f);}
                return;
            }
            for(int i=0;i<bricks.Count;i++){
                var c=bricks[i];if(c.time<0)continue;
                float f=Mathf.Clamp01((time-c.time)/(.33f+Mathf.Sqrt(c.start.y)*.30f));float s=Mathf.SmoothStep(0,1,f);
                c.node.localRotation=Quaternion.Euler(-90*s,(i%5-2)*12*s,0);
                c.node.localPosition=new Vector3(c.start.x+Mathf.Sin(i*2.39f)*.39f*s,Mathf.Lerp(c.start.y,.122f+.003f*(i%8),f*f),
                    (.27f+.88f*(i%7)/7f)*s);
            }
        }
        public void Dispose(){foreach(var mesh in meshes)UnityEngine.Object.Destroy(mesh);}
    }
}
