using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Sample
{
    internal sealed class LabBench
    {
        public Camera Camera { get; private set; }
        readonly Camera detail;
        public readonly RenderTexture DetailTexture;
        public Transform Probe { get; private set; }
        readonly Transform root;
        readonly Shader shader;
        readonly Dictionary<Color,Material> materials = new Dictionary<Color,Material>();
        readonly Color steel = new Color(.23f,.33f,.35f);
        readonly Color dark = new Color(.14f,.20f,.20f);

        public LabBench(Transform parent)
        {
            root=parent;
            shader=Resources.Load<Shader>("WoodWorkbench");
            if(!shader || !shader.isSupported) throw new System.InvalidOperationException("URP workbench shader is missing or unsupported.");
            var camera=new GameObject("Specimen camera");
            camera.transform.SetParent(root,false);
            Camera=camera.AddComponent<Camera>();
            Camera.orthographic=true;
            Camera.nearClipPlane=.01f;
            Camera.farClipPlane=10;
            Camera.clearFlags=CameraClearFlags.SolidColor;
            Camera.backgroundColor=new Color(.77f,.82f,.78f);
            var aim=new Vector3(.315f,.015f,0);
            Camera.transform.position=aim+new Vector3(.13f,.22f,-1.5f);
            Camera.transform.LookAt(aim);
            var detailObject=new GameObject("Fracture close-up");
            detailObject.transform.SetParent(root,false);
            detail=detailObject.AddComponent<Camera>();
            detail.CopyFrom(Camera);
            DetailTexture=new RenderTexture(448,330,24);
            detail.targetTexture=DetailTexture;
            detail.orthographicSize=.066f;
            detail.backgroundColor=new Color(.79f,.81f,.73f);
            Frame();

            var oak=new Color(.67f,.51f,.33f);
            Box("Bench",new Vector3(.32f,-.426f,.12f),new Vector3(2.8f,.09f,1.45f),oak,true);
            for(int i=0;i<9;i++)
                Box("Plank joint",new Vector3(-.9f+.31f*i,-.3808f,.12f),new Vector3(.0017f,.0005f,1.45f),oak*.79f);
            Box("Vise foot",new Vector3(-.079f,-.356f,.02f),new Vector3(.28f,.05f,.28f),dark);
            Box("Vise column",new Vector3(-.082f,-.19f,.022f),new Vector3(.19f,.29f,.175f),steel);
            Box("Lower jaw",new Vector3(-.069f,-.053f,.018f),new Vector3(.167f,.057f,.15f),steel);
            Box("Lower jaw pad",new Vector3(-.071f,-.027f,.018f),new Vector3(.16f,.012f,.153f),dark);
            Box("Upper jaw",new Vector3(-.065f,.055f,.022f),new Vector3(.15f,.05f,.15f),steel);
            Box("Upper jaw pad",new Vector3(-.065f,.028f,.022f),new Vector3(.15f,.012f,.151f),dark);
            Box("Jaw bridge",new Vector3(-.128f,.005f,.027f),new Vector3(.038f,.09f,.126f),steel);
            var metal=new Color(.64f,.69f,.67f);
            var hub=Shape("Vise screw",PrimitiveType.Cylinder,new Vector3(-.088f,-.143f,-.071f),new Vector3(.052f,.030f,.052f),metal);
            hub.localRotation=Quaternion.Euler(90,0,0);
            var handle=Shape("Sliding bar",PrimitiveType.Cylinder,new Vector3(-.088f,-.143f,-.108f),new Vector3(.012f,.084f,.012f),metal);
            handle.localRotation=Quaternion.Euler(0,0,-34);
            for(int i=-1;i<=1;i+=2)
                Shape("Knob",PrimitiveType.Sphere,handle.position+handle.up*.084f*i,Vector3.one*.022f,dark);
            // The little steel ruler sits behind the branch and exposes scale.
            Box("Scale rail",new Vector3(.35f,-.362f,.145f),new Vector3(.81f,.038f,.009f),new Color(.82f,.82f,.72f));
            for(int i=0;i<=75;i++)
            {
                float h=i%10==0?.021f:(i%5==0?.015f:.008f);
                Box("Tick",new Vector3(i*.01f,-.343f-h*.5f,.1395f),new Vector3(.0012f,h,.001f),dark);
            }
            Probe=Shape("Contact point",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.027f,new Color(.87f,.36f,.15f));
        }
        public void Frame()
        {
            bool wide=Screen.width>Screen.height*1.18f;
            Camera.rect=new Rect(0,.13f,wide?.76f:1f,.74f);
            Camera.orthographicSize=Mathf.Max(.49f,.55f / Mathf.Max(.4f,Camera.aspect));
        }
        public void Focus(Vector3 p)
        {
            detail.orthographicSize=.066f;
            detail.transform.position=p+new Vector3(.26f,.10f,-.50f);
            detail.transform.LookAt(p);
        }
        public void WoodCamera()
        {
            var aim=new Vector3(.315f,.015f,0);
            Camera.transform.position=aim+new Vector3(.13f,.22f,-1.5f);
            Camera.transform.LookAt(aim);
            Frame();
        }
        public void CeramicCamera(Vector3 center,Vector3 edge,bool wide,bool reverse)
        {
            Frame();
            Vector3 aim=wide?new Vector3(.32f,-.165f,.065f):center+Vector3.up*.07f;
            float side=reverse?1:-1;
            Camera.transform.position=aim+new Vector3(wide?.09f:.025f,wide?.48f:.40f,side*(wide?1.2f:.53f));
            Camera.transform.LookAt(aim);
            float halfWidth=wide?.53f:.218f;
            Camera.orthographicSize=Mathf.Max(wide?.32f:.167f,halfWidth/Mathf.Max(.4f,Camera.aspect));
            detail.orthographicSize=.052f;
            Vector3 outward=new Vector3(edge.x-center.x,0,edge.z-center.z).normalized;
            detail.transform.position=edge+outward*(reverse?-.35f:.35f)+Vector3.up*(reverse?.37f:.18f);
            detail.transform.LookAt(edge);
        }
        public Material Material(Color tint)
        {
            if(materials.TryGetValue(tint,out var shared)) return shared;
            var mat=new Material(shader);
            mat.SetColor("_Tint",tint);
            materials.Add(tint,mat);
            return mat;
        }
        Transform Shape(string name,PrimitiveType kind,Vector3 p,Vector3 size,Color tint,bool solid=false)
        {
            GameObject go=GameObject.CreatePrimitive(kind);
            go.name=name;
            go.transform.SetParent(root,false);
            go.transform.localPosition=p; go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=Material(tint);
            if(!solid) Object.Destroy(go.GetComponent<Collider>());
            return go.transform;
        }
        void Box(string name,Vector3 p,Vector3 size,Color color,bool solid=false)
            => Shape(name,PrimitiveType.Cube,p,size,color,solid);
    }
}
