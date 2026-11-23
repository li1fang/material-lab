// SPDX-License-Identifier: MIT
// The course support and deterministic toy motion below adapt the brick routines in
// Material Lab v0.4.1, BoundarySpan.cs (a74315db2fb0891180e41392d40d02ff00b4c161).
// Copyright (c) 2026 Material Destruction contributors. See MATERIAL-LAB-LICENSE.md.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    [Serializable] internal sealed class BrickWallSpec
    {
        // Width and height are assembled extents. Brick dimensions specify course pitches.
        public float width, height = 1.959f, brickWidth = .60f, brickHeight = .245f, brickDepth = .23f;
        public string profile = "A";
        [NonSerialized] public Material surfaceMaterial, coreMaterial;
        internal BrickWallSpec Copy() => (BrickWallSpec)MemberwiseClone();
        internal void Validate()
        {
            foreach (float v in new[]{width,height,brickWidth,brickHeight,brickDepth})
                if (!float.IsFinite(v) || v <= 0) throw new ArgumentException("Expected positive finite wall and brick dimensions.");
            if (profile != "A" && profile != "B" && profile != "C" && profile != "D") throw new ArgumentException("Unknown fracture profile " + profile);
            int rows=StyledBrickSpan.Cells(height,brickHeight), cols=StyledBrickSpan.Cells(width,brickWidth);
            if (rows < 1 || cols < 1 || rows>256 || cols>256 || (long)rows*(cols+1)>4096) throw new ArgumentException("Wall exceeds sample budget.");
        }
    }

    internal interface IBrickFace : IDisposable
    {
        Transform root {get;}
        int Missing {get;}
        void Hit(Vector2 point, float strength, float time);
        void Support(float left,float right,float time,Vector3 leftAxis,Vector3 rightAxis);
        void Pose(float time);
    }

    // Delegates legacy deliveries to the unmodified, pinned original.
    internal sealed class OriginalBrickFace : IBrickFace
    {
        readonly BoundarySpan source;
        public OriginalBrickFace(BoundarySpan source) {this.source=source;}
        public Transform root => source.root;
        public int Missing => source.Missing;
        public void Hit(Vector2 p,float k,float t) => source.Hit(p,k,t);
        public void Support(float l,float r,float t,Vector3 left,Vector3 right) => source.Support(l,r,t,left,right);
        public void Pose(float t) => source.Pose(t);
        public void Dispose() => source.Dispose();
    }

    internal sealed class StyledBrickSpan : IBrickFace
    {
        sealed class Chip {
            public Transform node; public Vector3 rest; public float released=-1;
            public int course; public float minX,maxX;
        }
        sealed class Geometry {
            public Mesh render;
            public Mesh[] pick;
            public Vector2 pivot;
        }
        readonly BrickWallSpec spec;
        readonly CampusKit kit;
        readonly List<Chip> chips=new List<Chip>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly Dictionary<(float,float,int),Geometry> cache=new Dictionary<(float,float,int),Geometry>();
        readonly List<List<Chip>> courses=new List<List<Chip>>();
        readonly System.Random random;
        readonly Vector2[] seam;
        float previousLeft,previousRight;
        public Transform root {get;}
        public int Missing {get; private set;}
        internal float ReleaseTime(int i) => chips[i].released;
        public bool IsReleased(int i) => chips[i].released>=0;
        static readonly string[] Colors={"D89873","CB8969","DFA67F","BD8066","DC9979"};
        // Each seam runs all the way between top and bottom. Its matching halves
        // share these exact coordinates and keep the outside brick silhouette.
        static Vector2[] Seam(string key)
        {
            switch(key) {
                case "A": return new[]{new Vector2(.39f,0),new Vector2(.57f,.5f),new Vector2(.59f,1)};
                case "B": return new[]{new Vector2(.24f,0),new Vector2(.76f,1)};
                case "C": return new[]{new Vector2(.74f,0),new Vector2(.33f,.46f),new Vector2(.67f,1)};
                case "D": return new[]{new Vector2(.38f,0),new Vector2(.63f,.29f),new Vector2(.35f,.62f),new Vector2(.56f,1)};
                default: throw new ArgumentException(key);
            }
        }
        internal static int Cells(float extent,float pitch)
        {
            float ratio=extent/pitch;
            if(!float.IsFinite(ratio)||ratio>257) return 258;
            int near=Mathf.RoundToInt(ratio);
            return near>0 && Mathf.Abs(ratio-near)<=1e-5f ? near : Mathf.CeilToInt(ratio);
        }

        public StyledBrickSpan(CampusKit kit,Transform owner,Vector3 a,Vector3 b,int seed,BrickWallSpec recipe)
        {
            this.kit=kit; spec=recipe.Copy(); spec.Validate();
            if ((b-a).sqrMagnitude<=1e-10f || Mathf.Abs(b.y-a.y)>.0001f) throw new ArgumentException("Expected distinct level wall anchors.");
            random=new System.Random(seed+730);
            seam=Seam(spec.profile);
            root=CampusKit.Node("Styled span " + seed,owner,(a+b)*.5f);
            root.localRotation=Quaternion.LookRotation(Vector3.Cross((b-a).normalized,Vector3.up));
            Build();
        }
        void Build()
        {
            int count=Cells(spec.height,spec.brickHeight);
            for(int r=0;r<count;r++) {
                courses.Add(new List<Chip>());
                float bottom=r*spec.brickHeight;
                float top=r==count-1 ? spec.height : (r+1)*spec.brickHeight;
                float gapY=Mathf.Min(spec.brickHeight*.0225f,(top-bottom)*.2f);
                float y0=bottom+(r==0?0:gapY), y1=top-(r==count-1?0:gapY);
                float offset=(r%2)*spec.brickWidth*.5f;
                int cols=Cells(spec.width+offset,spec.brickWidth);
                for(int c=0;c<cols;c++){
                    float left=Mathf.Max(0,c*spec.brickWidth-offset);
                    float right=c==cols-1 ? spec.width : Mathf.Min(spec.width,(c+1)*spec.brickWidth-offset);
                    float gapX=Mathf.Min(spec.brickWidth*.015f,(right-left)*.2f);
                    float x0=left+(c==0?0:gapX)-spec.width*.5f, x1=right-(c==cols-1?0:gapX)-spec.width*.5f;
                    var paint=spec.surfaceMaterial ? spec.surfaceMaterial : kit.Paint(r==count-1?"F2D8AA":Colors[random.Next(Colors.Length)]);
                    for(int side=0;side<2;side++) AddPiece(x0,y0,x1-x0,y1-y0,side,r,paint);
                }
            }
        }
        void AddPiece(float x,float y,float w,float h,int side,int course,Material material)
        {
            var key=(w,h,side);
            if (!cache.TryGetValue(key,out var geometry)){
                geometry=MakeGeometry(w,h,side);cache.Add(key,geometry);
            }
            var rest=new Vector3(x+geometry.pivot.x,y+geometry.pivot.y,0);
            var node=CampusKit.Node("Clay fragment "+chips.Count,root,rest);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=geometry.render;
            var renderer=node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials=new[]{material,spec.coreMaterial?spec.coreMaterial:kit.Paint("AD7354")};
            if(material.HasProperty("_BrickOrigin")) {
                var properties=new MaterialPropertyBlock();
                properties.SetVector("_BrickOrigin",new Vector4(rest.x,rest.y,0,0));
                renderer.SetPropertyBlock(properties,0);
            }
            foreach(var mesh in geometry.pick) {
                var collider=node.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh=mesh;collider.convex=true;
            }
            var bounds=geometry.render.bounds;
            var chip=new Chip{node=node,rest=rest,course=course,minX=rest.x+bounds.min.x,maxX=rest.x+bounds.max.x};
            chips.Add(chip);courses[course].Add(chip);
        }

        Geometry MakeGeometry(float w,float h,int side)
        {
            float sx=0;
            foreach(var p in seam)sx+=p.x;
            var pivot=new Vector2((side==0?0.5f*sx/seam.Length:0.5f*(1+sx/seam.Length))*w,.5f*h);
            var vertices=new List<Vector3>(); var face=new List<int>();var core=new List<int>();var uv=new List<Vector2>();
            var colliders=new List<Mesh>();float depth=spec.brickDepth;
            Vector3 V(float x,float y,float z)=>new Vector3(x*w-pivot.x,y*h-pivot.y,z*depth);
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,List<int> sub,bool surface) {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
                if(surface){
                    foreach(var v in new[]{a,b,c,d})uv.Add(new Vector2((v.x+pivot.x)/spec.brickWidth,(v.y+pivot.y)/spec.brickHeight));
                }else uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                sub.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            for(int i=0;i+1<seam.Length;i++){
                var low=seam[i];var high=seam[i+1];
                var front=new[]{
                    V(side==0?0:low.x,low.y,-.5f),V(side==0?low.x:1,low.y,-.5f),
                    V(side==0?high.x:1,high.y,-.5f),V(side==0?0:high.x,high.y,-.5f)};
                var back=Array.ConvertAll(front,p=>p+Vector3.forward*depth);
                Quad(front[0],front[3],front[2],front[1],face,true);
                Quad(back[0],back[1],back[2],back[3],face,true);
                for(int edge=0;edge<4;edge++){
                    if((edge==0 && i!=0)||(edge==2 && i+2!=seam.Length))continue;
                    int j=(edge+1)%4;
                    Quad(front[edge],front[j],back[j],back[edge],core,false);
                }
                // Each prism is convex even for the chevron and zigzag profiles.
                // Its only overlap with a neighbouring prism is a shared boundary.
                var pickMesh=new Mesh{name="Convex clay band"};
                var points=new Vector3[8];Array.Copy(front,points,4);Array.Copy(back,0,points,4,4);
                pickMesh.vertices=points;
                pickMesh.triangles=new[]{0,3,2,0,2,1,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
                pickMesh.RecalculateBounds();colliders.Add(pickMesh);ownedMeshes.Add(pickMesh);
            }
            var render=new Mesh{name="Paired clay "+spec.profile};
            render.SetVertices(vertices);render.SetUVs(0,uv);render.subMeshCount=2;
            render.SetTriangles(face,0);render.SetTriangles(core,1);render.RecalculateNormals();render.RecalculateBounds();
            ownedMeshes.Add(render);
            return new Geometry{pivot=pivot,render=render,pick=colliders.ToArray()};
        }

        public void Hit(Vector2 point,float strength,float time)
        {
            foreach(var c in chips){
                if(c.released>=0)continue;
                var d=new Vector2((c.rest.x-point.x)*.60f/spec.brickWidth,(c.rest.y-point.y)*.245f/spec.brickHeight);
                if(d.magnitude<.18f+strength*.53f){c.released=time;Missing++;}
            }
            foreach(var c in chips){
                if(c.released>=0||c.course==0||Mathf.Abs(c.rest.x)>spec.width*.5f-.22f*(spec.brickWidth/.60f))continue;
                if(!courses[c.course-1].Exists(n=>n.released<0 && n.maxX>c.minX && c.maxX>n.minX)){
                    c.released=time+.06f*c.course;Missing++;
                }
            }
        }
        public void Support(float left,float right,float time,Vector3 leftAxis,Vector3 rightAxis)
        {
            float y=spec.height*(1.5f/1.959f);
            if(left>previousLeft+.01f)Hit(new Vector2(-spec.width/2,y),left*.83f,time);
            if(right>previousRight+.01f)Hit(new Vector2(spec.width/2,y),right*.83f,time);
            previousLeft=left;previousRight=right;
        }
        public void Pose(float time)
        {
            for(int i=0;i<chips.Count;i++){
                var c=chips[i];if(c.released<0)continue;
                float f=Mathf.Clamp01((time-c.released)/(.33f+Mathf.Sqrt(c.rest.y)*.30f)), s=Mathf.SmoothStep(0,1,f);
                c.node.localRotation=Quaternion.Euler(-90*s,(i%5-2)*12*s,0);
                c.node.localPosition=new Vector3(c.rest.x+Mathf.Sin(i*2.39f)*.39f*s,
                    Mathf.Lerp(c.rest.y,spec.brickDepth*.5f+.007f+.003f*(i%8),f*f),
                    (.27f+.88f*(i%7)/7f)*s);
            }
        }
        public void Dispose(){foreach(var mesh in ownedMeshes)if(mesh)UnityEngine.Object.Destroy(mesh);ownedMeshes.Clear();}
    }
}
