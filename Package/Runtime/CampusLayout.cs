using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction
{
    [Serializable]
    public class CampusDimensions
    {
        public float front=9, side=6, extendedFront=10.4f, pitch=3, parkingPitch=4;
        public int smallX=2,smallZ=3,largeX=3,largeZ=5;
        public void Validate(){
            foreach(float v in new[]{front,side,extendedFront,pitch,parkingPitch})
                if(!float.IsFinite(v)||v<1||v>50)throw new ArgumentOutOfRangeException("Campus dimensions");
            foreach(int v in new[]{smallX,smallZ,largeX,largeZ})if(v<2||v>8)throw new ArgumentOutOfRangeException("Parking grid");
        }
    }

    public sealed class CampusLayout
    {
        public readonly List<Vector3> posts=new List<Vector3>();
        public readonly List<Vector2Int> spans=new List<Vector2Int>();
        public readonly List<int[]> floors=new List<int[]>();
        public int corner=-1, columns,rows;
        int Post(Vector3 p){posts.Add(p);return posts.Count-1;}
        public static CampusLayout Boundary(float front,float side,float pitch){
            foreach(float v in new[]{front,side,pitch})
                if(!float.IsFinite(v)||v<1||v>50)throw new ArgumentOutOfRangeException(nameof(pitch),"Expected finite campus lengths in [1, 50].");
            var layout=new CampusLayout();layout.Post(new Vector3(-front/2,0,-side/2));
            void Leg(float length,Vector3 direction){
                int n=Mathf.FloorToInt(length/pitch);float tail=length-n*pitch;
                if(tail>pitch*.25f||n==0)n++;
                for(int i=0;i<n;i++){
                    int a=layout.posts.Count-1;
                    float distance=i==n-1?length-i*pitch:pitch;
                    int b=layout.Post(layout.posts[a]+direction*distance);
                    layout.spans.Add(new Vector2Int(a,b));
                }
            }
            Leg(front,Vector3.right);layout.corner=layout.posts.Count-1;Leg(side,Vector3.forward);return layout;
        }
        public static CampusLayout Parking(int nx,int nz,float pitch){
            if(nx<2||nz<2||nx>8||nz>8||!float.IsFinite(pitch)||pitch<1||pitch>50)
                throw new ArgumentOutOfRangeException(nameof(pitch),"Expected a 2–8 bay grid and a finite pitch in [1, 50].");
            var l=new CampusLayout{columns=nx,rows=nz};
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){
                int p=l.Post(new Vector3((x-nx*.5f)*pitch,0,(z-nz*.5f)*pitch));
                if(x>0)l.spans.Add(new Vector2Int(p-1,p));
                if(z>0)l.spans.Add(new Vector2Int(p-nx-1,p));
                if(x>0&&z>0)l.floors.Add(new[]{p-nx-2,p-nx-1,p,p-1});
            }
            return l;
        }
    }
}
