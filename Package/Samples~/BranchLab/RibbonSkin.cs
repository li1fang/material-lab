using UnityEngine;

namespace MaterialDestruction.Sample
{
    // Both panels share seam vertices up to the crack tip. Behind it each panel
    // has its own edge, with continuous material coordinates through deformation.
    internal sealed class RibbonSkin
    {
        const int Rows=RibbonTear.Bundles+1, Across=9;
        const int StrandsPerBundle=3, BridgeStrands=4, Segments=4;
        const int StrandCount=2*RibbonTear.Bundles*StrandsPerBundle+BridgeStrands;
        public readonly Mesh Surface=new Mesh {name="Linen tape"};
        public readonly Mesh Yarns=new Mesh {name="Exposed weft ends"};
        readonly RibbonTear tear;
        readonly Vector3[] points=new Vector3[2*Rows*Across];
        readonly Vector2[] uv=new Vector2[2*Rows*Across];
        readonly int[] indices=new int[2*(Rows-1)*(Across-1)*6];
        readonly Vector3[] yarnPoints=new Vector3[StrandCount*(Segments+1)*2];
        readonly Color[] yarnColors=new Color[StrandCount*(Segments+1)*2];
        readonly int[] yarnIndices=new int[StrandCount*Segments*6];
        int topology=-1;
        public float Lift,Slide;
        public int ActiveSide=1;
        public RibbonSkin(RibbonTear model)
        {
            tear=model;
            Surface.MarkDynamic();Yarns.MarkDynamic();
            for(int s=0;s<2;s++) for(int i=0;i<Rows;i++) for(int j=0;j<Across;j++)
                uv[Index(s,i,j)]=new Vector2(i/(Rows-1f),.5f+(s==0?-.5f:.5f)*j/(Across-1f));
            for(int i=0;i<StrandCount;i++)
            {
                Color c=Color.Lerp(new Color(.58f,.49f,.35f),new Color(.96f,.90f,.70f),Noise(i));
                for(int j=0;j<=Segments;j++)
                {
                    int v=(i*(Segments+1)+j)*2;
                    yarnColors[v]=yarnColors[v+1]=c;
                    if(j<Segments) Quad(yarnIndices,(i*Segments+j)*6,v,v+1,v+3,v+2);
                }
            }
            Update();
        }
        static float Noise(int seed) => Mathf.Repeat(Mathf.Sin(seed*17.731f+.3f)*745.45f,1);
        static int Index(int side,int i,int j) => (side*Rows+i)*Across+j;
        int Joined(int side,int i,int j) => Index(j==0 && i<=RibbonTear.Bundles-RibbonTear.Starter-tear.Broken?0:side,i,j);
        static void Quad(int[] data,int n,int a,int b,int c,int d)
        { data[n]=a;data[n+1]=b;data[n+2]=c;data[n+3]=a;data[n+4]=c;data[n+5]=d; }
        public Vector3 Point(float u,float across,int side)
        {
            float front=tear.Front;
            float loose=Mathf.Clamp01((u-front)/Mathf.Max(.001f,1-front));
            float peel=loose*loose*(2-loose);
            float along=u*RibbonTear.Length;
            float slack=1-Mathf.Min(1,Mathf.Abs(Lift)*8);
            float natural=.012f+.040f*(1-front);
            float hand=side==ActiveSide?Lift:-Lift*.11f;
            float opening=side*natural+hand;
            float seam=.0025f*Mathf.Sin(29*u)+.0013f*Mathf.Sin(73*u);
            float span=side*RibbonTear.Width*.5f*across+seam*(1-across);
            float x=-.24f+along+peel*((side==ActiveSide?Slide:0)-Mathf.Abs(opening)*.33f);
            float y=.038f-.046f*u*u+span+peel*opening-.02f*peel*loose;
            float ripple=Mathf.Sin(11*u+side*across*1.2f)+.42f*Mathf.Sin(28*u+side*across*2);
            float z=-.011f*u*ripple*(.25f+.75f*slack)-.013f*peel*across*side;
            return new Vector3(x,y,z);
        }
        public void Update()
        {
            bool changed=topology!=tear.Broken;
            int n=0;
            for(int s=0;s<2;s++) for(int i=0;i<Rows;i++) for(int j=0;j<Across;j++)
            {
                points[Index(s,i,j)]=Point(i/(Rows-1f),j/(Across-1f),s==0?-1:1);
                if(!changed || i==Rows-1 || j==Across-1) continue;
                int a=Joined(s,i,j),b=Joined(s,i+1,j),c=Joined(s,i+1,j+1),d=Joined(s,i,j+1);
                if(s==0) Quad(indices,n,a,b,c,d); else Quad(indices,n,d,c,b,a);
                n+=6;
            }
            Surface.vertices=points;
            if(changed){ Surface.uv=uv;Surface.triangles=indices; }
            Surface.RecalculateNormals();Surface.RecalculateBounds();
            for(int f=0;f<StrandCount;f++)
            {
                int row=f/(2*StrandsPerBundle);
                int side=(f%2==0)?-1:1;
                bool bridge=row>=RibbonTear.Bundles;
                float u=(row+.12f+.76f*Noise(f))/RibbonTear.Bundles;
                if(bridge) u=tear.Front+(.15f+.23f*(f%BridgeStrands))/RibbonTear.Bundles;
                if(u<=tear.Front || (bridge && tear.Split))
                {
                    for(int j=0;j<2*(Segments+1);j++) yarnPoints[f*2*(Segments+1)+j]=Vector3.zero;
                    continue;
                }
                Vector3 a=Point(u,0,side);
                float seed=Noise(f+71);
                Vector3 b=bridge?Point(u+.0006f,0,-side):a+new Vector3((seed-.45f)*.007f,-side*(.002f+.0045f*seed),-.0008f);
                float width=bridge?.00048f:.00036f;
                Vector3 normal=new Vector3(-(b.y-a.y),b.x-a.x,0).normalized*width;
                for(int j=0;j<=Segments;j++)
                {
                    float t=j/(float)Segments;
                    var p=Vector3.Lerp(a,b,t);
                    p.x+=Mathf.Sin(t*Mathf.PI)*.0018f*(seed-.5f)*(1-tear.Load);
                    p.z-=.0007f;
                    int v=(f*(Segments+1)+j)*2;
                    var w=normal*(bridge?1:1-.7f*t);
                    yarnPoints[v]=p-w;yarnPoints[v+1]=p+w;
                }
            }
            Yarns.vertices=yarnPoints;
            if(topology<0)
            {
                var normals=new Vector3[yarnPoints.Length];
                for(int i=0;i<normals.Length;i++) normals[i]=Vector3.back;
                Yarns.normals=normals;Yarns.colors=yarnColors;Yarns.triangles=yarnIndices;
            }
            Yarns.RecalculateBounds();topology=tear.Broken;
        }
    }
}
