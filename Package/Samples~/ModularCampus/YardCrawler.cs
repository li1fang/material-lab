using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    // Original procedural maintenance crawler; the moving shoes follow a
    // capsule around each set of wheels, including during the return stroke.
    internal sealed class YardCrawler
    {
        public readonly Transform root;
        readonly List<Transform> wheels=new List<Transform>(),shoes=new List<Transform>();
        const int PerSide=32;
        public YardCrawler(CampusKit kit,Transform parent){
            root=CampusKit.Node("Yard crawler",parent,Vector3.zero);
            kit.Solid(root,"Apricot body",new Vector3(0,.72f,0),new Vector3(1.20f,.56f,1.72f),"E6AB59");
            kit.Solid(root,"Cabin",new Vector3(0,1.14f,-.19f),new Vector3(.92f,.48f,.86f),"F3E1BA");
            kit.Solid(root,"Cab glazing",new Vector3(0,1.18f,.25f),new Vector3(.77f,.29f,.035f),"467A81");
            kit.Solid(root,"Cab roof",new Vector3(0,1.41f,-.18f),new Vector3(1.02f,.09f,.96f),"508D88");
            kit.Solid(root,"Contact roller",new Vector3(0,.57f,1f),new Vector3(.89f,.40f,.30f),"B3774C",true);
            for(int side=-1;side<=1;side+=2){
                kit.Solid(root,"Side glazing",new Vector3(.47f*side,1.18f,-.18f),new Vector3(.022f,.29f,.58f),"4A777B");
                for(int i=0;i<4;i++){
                    var wheel=kit.Solid(root,"Wheel",new Vector3(.64f*side,.33f,-.59f+.395f*i),new Vector3(.49f,.105f,.49f),"42565A",false,PrimitiveType.Cylinder);
                    wheel.localRotation=Quaternion.Euler(0,0,90);wheels.Add(wheel);
                    var hub=kit.Solid(wheel,"Hub",new Vector3(0,-.99f*side,0),new Vector3(.45f,.12f,.45f),"CC985B",false,PrimitiveType.Cylinder);
                    kit.Solid(hub,"Index mark",new Vector3(0,-side,0),new Vector3(.17f,.12f,.68f),"FFE8BC");
                }
                for(int i=0;i<PerSide;i++)shoes.Add(kit.Solid(root,"Track shoe",Vector3.zero,new Vector3(.29f,.073f,.124f),"526366"));
            }
            Place(-3);
        }
        public void Place(float position){
            root.localPosition=new Vector3(0,0,position);
            foreach(var wheel in wheels)wheel.localRotation=Quaternion.Euler(-position/.245f*Mathf.Rad2Deg,0,90);
            const float straight=1.20f,r=.255f;float arc=Mathf.PI*r,total=2*(straight+arc);
            for(int i=0;i<shoes.Count;i++){
                float d=Mathf.Repeat((i%PerSide)*total/PerSide+position,total),z,y,angle;
                if(d<straight){z=-.6f+d;y=.33f+r;angle=0;}
                else if(d<straight+arc){float a=(d-straight)/r;z=.6f+r*Mathf.Sin(a);y=.33f+r*Mathf.Cos(a);angle=a*Mathf.Rad2Deg;}
                else if(d<2*straight+arc){z=.6f-(d-straight-arc);y=.33f-r;angle=180;}
                else{float a=(d-2*straight-arc)/r;z=-.6f-r*Mathf.Sin(a);y=.33f-r*Mathf.Cos(a);angle=180+a*Mathf.Rad2Deg;}
                shoes[i].localPosition=new Vector3(i<PerSide?-.65f:.65f,y,z);shoes[i].localRotation=Quaternion.Euler(angle,0,0);
            }
        }
    }
}
