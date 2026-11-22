using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    internal sealed class ParkingDeck
    {
        public readonly CampusLayout layout;
        public readonly Transform[] posts,panels;
        readonly Transform[] girders;readonly float[] lostAt;
        readonly int[] wear;
        readonly float[] previous,changedAt;
        readonly Vector3[] centers;
        readonly List<int>[] neighbors;
        readonly float pitch;
        const float Height=2.95f;
        public int Fallen{get;private set;}
        public int Tilted{get;private set;}
        public int Waves{get;private set;}
        public ParkingDeck(CampusLayout layout,CampusKit kit,Transform yard,float pitch){
            this.layout=layout;this.pitch=pitch;
            posts=new Transform[layout.posts.Count];wear=new int[posts.Length];previous=new float[posts.Length];changedAt=new float[posts.Length];
            for(int i=0;i<posts.Length;i++)posts[i]=kit.Pillar(yard,i,layout.posts[i],Height);
            girders=new Transform[layout.spans.Count];
            for(int i=0;i<girders.Length;i++)girders[i]=kit.Solid(yard,"Girder "+i,Vector3.zero,Vector3.one,"B99076",true);
            panels=new Transform[layout.floors.Count];centers=new Vector3[panels.Length];lostAt=new float[panels.Length];neighbors=new List<int>[panels.Length];
            var sides=new Dictionary<Vector2Int,int>();
            for(int i=0;i<panels.Length;i++){
                lostAt[i]=-1;neighbors[i]=new List<int>();int[] f=layout.floors[i];
                for(int k=0;k<4;k++){
                    centers[i]+=layout.posts[f[k]]*.25f;
                    int a=f[k],b=f[(k+1)%4];var edge=new Vector2Int(Mathf.Min(a,b),Mathf.Max(a,b));
                    if(sides.TryGetValue(edge,out int j)){neighbors[i].Add(j);neighbors[j].Add(i);}else sides.Add(edge,i);
                }
                var p=panels[i]=CampusKit.Node("Deck "+i,yard,centers[i]+Vector3.up*(Height+.20f));
                kit.Solid(p,"Sealed slab",Vector3.zero,new Vector3(pitch-.035f,.33f,pitch-.035f),"CFB7A0",true);
                kit.Solid(p,"Pavement",Vector3.up*.18f,new Vector3(pitch-.055f,.052f,pitch-.055f),"8E9B9C");
                for(int s=-1;s<=1;s+=2)kit.Solid(p,"Parking stripe",new Vector3(s*pitch*.28f,.217f,0),new Vector3(.065f,.018f,pitch*.71f),"F6E2AE");
                kit.Solid(p,"Wheel stop",new Vector3(0,.27f,pitch*.30f),new Vector3(pitch*.38f,.14f,.18f),"DBA653");
                float x=centers[i].x,z=centers[i].z;
                bool rampEntry=i==panels.Length-1;
                if(!rampEntry && Mathf.Abs(x)>(layout.columns/2f-1)*pitch)Rail(kit,p,new Vector3(Mathf.Sign(x)*(pitch/2-.12f),0,0),false);
                if(Mathf.Abs(z)>(layout.rows/2f-1)*pitch)Rail(kit,p,new Vector3(0,0,Mathf.Sign(z)*(pitch/2-.12f)),true);
                kit.Solid(yard,"Ground stall",centers[i]+Vector3.up*.012f,new Vector3(pitch-.06f,.024f,pitch-.06f),"ADA89F");
                kit.Solid(yard,"Ground marking",centers[i]+new Vector3(-pitch*.28f,.033f,0),new Vector3(.07f,.017f,pitch*.78f),"E7DAB8");
            }
            Ramp(kit,yard);Pose(0);
        }
        void Ramp(CampusKit kit,Transform yard){
            float x=(layout.columns*.5f+.26f)*pitch,top=(layout.rows*.5f-.63f)*pitch,foot=-layout.rows*.5f*pitch;
            float rise=Height+.39f,run=top-foot,len=Mathf.Sqrt(run*run+rise*rise);
            var slope=CampusKit.Node("Entrance ramp",yard,new Vector3(x,rise/2,(foot+top)*.5f));
            slope.localRotation=Quaternion.Euler(-Mathf.Atan2(rise,run)*Mathf.Rad2Deg,0,0);
            float width=.52f*pitch;
            kit.Solid(slope,"Incline",Vector3.down*.09f,new Vector3(width,.18f,len),"A5AAA3",true);
            foreach(int side in new[]{-1,1}){
                kit.Solid(slope,"Kerb",new Vector3(side*(width/2-.05f),.064f,0),new Vector3(.105f,.13f,len),"D2BCA1");
            }
            for(int i=0;i<5;i++)kit.Solid(slope,"Lane marker",new Vector3(0,.012f,(i-2)*len/6),new Vector3(.065f,.022f,.43f),"FAE4AB");
            float landing=layout.rows*.5f*pitch-top;
            kit.Solid(yard,"Turning landing",new Vector3(x,rise-.08f,top+landing/2),new Vector3(width,.16f,landing),"A5AAA3",true);
        }
        void Rail(CampusKit k,Transform p,Vector3 basePos,bool alongX){
            var axis=alongX?Vector3.right:Vector3.forward;
            for(int j=-1;j<=1;j++)k.Solid(p,"Stanchion",basePos+axis*(pitch*.46f*j)+Vector3.up*.50f,new Vector3(.085f,.63f,.085f),"357B7C");
            foreach(float h in new[]{.48f,.80f})k.Solid(p,"Edge rail",basePos+Vector3.up*h,alongX?new Vector3(pitch-.18f,.085f,.08f):new Vector3(.08f,.085f,pitch-.18f),"3B8987");
        }
        int Live(int i){int count=0;foreach(int p in layout.floors[i])if(wear[p]<2)count++;return count;}
        public int Wear(int i)=>wear[i];
        float Motion(int i,float t)=>Mathf.Lerp(previous[i],wear[i],Mathf.SmoothStep(0,1,(t-changedAt[i])/.47f));
        public void Damage(int post,float t){
            if(wear[post]>=2)return;
            previous[post]=Motion(post,t);changedAt[post]=t;
            wear[post]++;
            // The toy floor can hang on one corner with help from the next
            // panel. Losing a joined panel queues a later, separate release.
            bool changed;
            do{
                changed=false;
                for(int i=0;i<panels.Length;i++){
                    if(lostAt[i]>=0)continue;int live=Live(i);float adjoining=float.PositiveInfinity;
                    foreach(int j in neighbors[i])if(lostAt[j]>=0)adjoining=Mathf.Min(adjoining,lostAt[j]);
                    if(live==0){lostAt[i]=t;changed=true;}
                    else if(live==1 && float.IsFinite(adjoining)){lostAt[i]=Mathf.Max(t,adjoining+.32f);Waves++;changed=true;}
                }
            }while(changed);
        }
        public void Sequence(float time){
            // A reproducible synthetic support-state fixture, shared by both sizes.
            int[] first=layout.floors[0], next=layout.floors[1];
            Damage(next[1],time);Damage(next[1],time);
            foreach(int p in first){Damage(p,time);Damage(p,time);}
        }
        public void Pose(float time){
            Fallen=Tilted=0;
            for(int i=0;i<posts.Length;i++){
                float motion=Motion(i,time);
                float angle=motion<=1?motion*6:Mathf.Lerp(6,76,motion-1);
                posts[i].localRotation=Quaternion.Euler(angle,0,0);
            }
            for(int i=0;i<girders.Length;i++){
                var e=layout.spans[i];var a=posts[e.x];var b=posts[e.y];
                CampusKit.Connect(girders[i],a.localPosition+a.localRotation*(Vector3.up*Height),b.localPosition+b.localRotation*(Vector3.up*Height),.30f,.31f);
            }
            for(int i=0;i<panels.Length;i++){
                int[] f=layout.floors[i];
                float Dip(int corner){float v=Motion(f[corner],time);return v<=1?v*.10f:Mathf.Lerp(.10f,.80f,v-1);}
                float avg=(Dip(0)+Dip(1)+Dip(2)+Dip(3))*.25f;
                float x=(Dip(2)+Dip(3)-Dip(0)-Dip(1))*15/pitch;
                float z=(Dip(0)+Dip(3)-Dip(1)-Dip(2))*15/pitch;
                float elapsed=lostAt[i]<0?-1:time-lostAt[i];
                float drop=elapsed<0?0:Mathf.Clamp01(elapsed/.83f);
                if(elapsed>=0)Fallen++;else if(avg>0)Tilted++;
                panels[i].localRotation=Quaternion.Euler(Mathf.Lerp(x,2.8f,drop),0,Mathf.Lerp(z,-1.6f,drop));
                panels[i].localPosition=centers[i]+Vector3.up*Mathf.Lerp(Height+.2f-avg,.39f,drop*drop);
            }
        }
    }
}
