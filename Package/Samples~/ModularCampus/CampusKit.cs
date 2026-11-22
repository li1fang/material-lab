using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    internal sealed class CampusKit
    {
        readonly Dictionary<string,Material> paints=new Dictionary<string,Material>();
        readonly Shader shader=Resources.Load<Shader>("CampusModelPaint");
        public Material Paint(string hex){
            if(!paints.TryGetValue(hex,out var m)){
                m=new Material(shader);ColorUtility.TryParseHtmlString("#"+hex,out var c);
                m.SetColor("_BaseColor",c);m.enableInstancing=true;paints.Add(hex,m);
            }
            return m;
        }
        public static Transform Node(string id,Transform parent,Vector3 p){
            var node=new GameObject(id).transform;node.SetParent(parent,false);node.localPosition=p;return node;
        }
        public Transform Solid(Transform p,string id,Vector3 center,Vector3 size,string color,bool hit=false,PrimitiveType type=PrimitiveType.Cube){
            var obj=GameObject.CreatePrimitive(type);obj.name=id;var t=obj.transform;t.SetParent(p,false);t.localPosition=center;t.localScale=size;
            obj.GetComponent<Renderer>().sharedMaterial=Paint(color);
            var collider=obj.GetComponent<Collider>();collider.enabled=hit;if(!hit)Object.Destroy(collider);return t;
        }
        public static void Connect(Transform beam,Vector3 a,Vector3 b,float width,float depth){
            beam.localPosition=(a+b)/2;beam.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
            beam.localScale=new Vector3(width,(a-b).magnitude,depth);
        }
        public Transform Pillar(Transform p,int index,Vector3 pos,float height){
            var pivot=Node("Post "+index,p,pos);
            Solid(pivot,"Foot",new Vector3(0,.15f,0),new Vector3(.61f,.30f,.61f),"9D96AB",true);
            Solid(pivot,"Casting",new Vector3(0,height/2,0),new Vector3(.38f,height,.38f),"ECDDBE",true);
            Solid(pivot,"Plinth",new Vector3(0,.45f,0),new Vector3(.40f,.38f,.40f),"BAAABD");
            Solid(pivot,"Band",new Vector3(0,height-.18f,0),new Vector3(.42f,.09f,.42f),"DEA654");
            Solid(pivot,"Cap",new Vector3(0,height,0),new Vector3(.54f,.16f,.54f),"F5E6C5");return pivot;
        }
        public void Base(Transform p,float w,float d){
            Solid(p,"Cut sand",new Vector3(0,-.40f,0),new Vector3(w,.65f,d),"BD966B");
            Solid(p,"Ground",new Vector3(0,-.085f,0),new Vector3(w+.02f,.17f,d+.02f),"D8C5A7",true);
        }
        public void Dispose(){foreach(var m in paints.Values)Object.Destroy(m);}
    }
}
