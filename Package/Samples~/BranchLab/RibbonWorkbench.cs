using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaterialDestruction.Sample
{
    public sealed class RibbonWorkbench : MonoBehaviour
    {
        Camera view,detail;
        RenderTexture loupe;
        RibbonTear ribbon;
        RibbonSkin skin;
        MeshFilter cloth, fringe, shadow;
        readonly List<RibbonStroke> grips=new List<RibbonStroke>();
        readonly List<Material> materials=new List<Material>();
        Vector2 origin, target;
        int side=1,controlId;
        bool held, redraw;
        float lift,slide;
        string takeFile;
        GUIStyle heading,copy,caption,button;
        readonly Color paper=new Color(.94f,.93f,.89f),ink=new Color(.13f,.22f,.24f);
        static bool Wide => Screen.width>Screen.height*1.22f;

        void Awake()
        {
            var args=Environment.GetCommandLineArgs();
            foreach(string arg in args)
                if(arg=="--wood" || arg=="--ceramic" || arg.StartsWith("--branch-") || arg=="--ceramic-proof")
                { enabled=false;SceneManager.LoadScene("Workbench");return; }
            Application.targetFrameRate=60;
            takeFile=Path.Combine(Application.persistentDataPath,"ribbon-take.json");
            int opt=Array.IndexOf(args,"--ribbon-log");
            if(opt>=0 && opt+1<args.Length) takeFile=args[opt+1];
            view=MakeCamera("Ribbon camera");
            detail=MakeCamera("Crack tip macro");
            loupe=new RenderTexture(504,336,24);
            detail.targetTexture=loupe;
            detail.orthographicSize=.041f;
            var textile=new Material(Resources.Load<Shader>("RibbonWeave"));
            materials.Add(textile);
            cloth=MeshObject("Striped ribbon",textile);
            fringe=MeshObject("Torn cross yarns",Tint(Color.white));
            shadow=MeshObject("Cast shadow",Tint(new Color(.65f,.61f,.53f)));
            shadow.transform.localPosition=new Vector3(.002f,-.005f,.039f);
            var jaw=Tint(new Color(.39f,.43f,.42f));
            Block("Rear clamp",new Vector3(-.265f,.037f,.03f),new Vector3(.071f,.18f,.027f),jaw);
            Block("Face of clamp",new Vector3(-.262f,.037f,-.023f),new Vector3(.062f,.169f,.016f),Tint(new Color(.52f,.56f,.53f)));
            Block("Jaw lip",new Vector3(-.233f,.037f,-.033f),new Vector3(.006f,.159f,.004f),jaw);
            var metal=Tint(new Color(.31f,.35f,.34f));
            var slot=Tint(new Color(.15f,.19f,.18f));
            foreach(float y in new[]{.101f,-.026f})
            {
                var screw=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                screw.transform.SetParent(transform,false);
                screw.transform.localPosition=new Vector3(-.264f,y,-.035f);
                screw.transform.localRotation=Quaternion.Euler(90,0,0);
                screw.transform.localScale=new Vector3(.014f,.003f,.014f);
                screw.GetComponent<Renderer>().sharedMaterial=metal;
                Block("Screw slot",new Vector3(-.264f,y,-.039f),new Vector3(.008f,.0015f,.001f),slot);
            }
            NewStrip();
            Debug.Log("RIBBON_READY");
        }
        Camera MakeCamera(string label)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var cam=go.AddComponent<Camera>();cam.orthographic=true;cam.nearClipPlane=.01f;cam.farClipPlane=4;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.83f,.81f,.75f);
            return cam;
        }
        Material Tint(Color c)
        {
            var m=new Material(Resources.Load<Shader>("WoodWorkbench"));m.SetColor("_Tint",c);
            materials.Add(m);return m;
        }
        MeshFilter MeshObject(string label,Material material)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var mesh=go.AddComponent<MeshFilter>();go.AddComponent<MeshRenderer>().sharedMaterial=material;return mesh;
        }
        void Block(string label,Vector3 p,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=label;go.transform.SetParent(transform,false);go.transform.localPosition=p;
            go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
        }
        void NewStrip()
        {
            if(held) Finish();
            if(skin!=null){Destroy(skin.Surface);Destroy(skin.Yarns);}
            ribbon=new RibbonTear();skin=new RibbonSkin(ribbon);
            cloth.sharedMesh=shadow.sharedMesh=skin.Surface;fringe.sharedMesh=skin.Yarns;
            target=Vector2.zero;lift=slide=0;side=1;redraw=true;grips.Clear();
            Frame();
        }
        void Frame()
        {
            view.rect=new Rect(0,.125f,Wide?.77f:1f,.745f);
            view.transform.position=new Vector3(-.02f,.014f,-1);
            view.orthographicSize=Mathf.Max(.189f,.325f/Mathf.Max(.4f,view.aspect));
            detail.transform.position=skin.Point(ribbon.Front,0,1)+new Vector3(.017f,0,-.6f);
        }
        void Update()
        {
            float speed=1-Mathf.Exp(-Time.unscaledDeltaTime*(held?23:9));
            float nextLift=Mathf.Lerp(lift,target.y,speed),nextSlide=Mathf.Lerp(slide,target.x,speed);
            if(redraw || Mathf.Abs(nextLift-lift)+Mathf.Abs(nextSlide-slide)>.000002f)
            {
                lift=nextLift;slide=nextSlide;
                skin.Lift=lift;skin.Slide=slide;skin.ActiveSide=side;
                skin.Update();redraw=false;
            }
            Frame();
        }
        Vector2 World(Vector2 guiPoint) => view.ScreenToWorldPoint(new Vector3(guiPoint.x,Screen.height-guiPoint.y,1));
        Vector2 ScreenPoint(Vector3 world)
        {
            var p=view.WorldToScreenPoint(world);return new Vector2(p.x,Screen.height-p.y);
        }
        bool TryGrip(Vector2 guiPoint)
        {
            float nearest=.031f;
            int selection=0;
            Vector2 hand=World(guiPoint);
            for(int s=-1;s<=1;s+=2)
                for(int i=0;i<10;i++) for(int j=0;j<5;j++)
                {
                    var p=(Vector2)skin.Point(.68f+.32f*i/9f,.13f+.86f*j/4f,s);
                    float distance=Vector2.Distance(p,hand);
                    if(distance<nearest) {nearest=distance;selection=s;}
                }
            if(selection==0) return false;
            origin=hand;side=selection;held=true;ribbon.Grab(side);
            return true;
        }
        void DragTo(Vector2 guiPoint)
        {
            if(!held) return;
            Vector2 move=World(guiPoint)-origin;
            // Pulling across the cut loads the cross yarns. A lengthwise pull has
            // only a small peeling component, and pushing the lips together unloads.
            float outward=Mathf.Max(0,side*move.y);
            ribbon.Open(Mathf.Max(0,side*move.y+.14f*Mathf.Max(0,-move.x)));
            target=new Vector2(Mathf.Clamp(move.x,-.068f,.018f),side*Mathf.Min(.16f,outward));
            redraw=true;
        }
        void Finish()
        {
            if(!held) return;
            var stroke=ribbon.Release();
            grips.Add(stroke);held=false;target=Vector2.zero;redraw=true;
            GUIUtility.hotControl=0;
            try {File.WriteAllText(takeFile,JsonUtility.ToJson(new RibbonTake{grips=grips.ToArray()},true));}
            catch(IOException e){ Debug.LogWarning("Ribbon take: "+e.Message); }
            Debug.Log("RIBBON_RELEASE "+stroke.before+" -> "+stroke.after);
        }
        void OnApplicationFocus(bool focus){if(!focus && held) Finish();}
        void HandleInput()
        {
            var ev=Event.current;
            controlId=GUIUtility.GetControlID(FocusType.Passive);
            if(ev.type==EventType.MouseDown && ev.button==0 && view.pixelRect.Contains(new Vector2(ev.mousePosition.x,Screen.height-ev.mousePosition.y))
                && TryGrip(ev.mousePosition))
            {GUIUtility.hotControl=controlId;ev.Use();}
            else if(held && ev.type==EventType.MouseDrag){DragTo(ev.mousePosition);ev.Use();}
            else if(held && ev.rawType==EventType.MouseUp){DragTo(ev.mousePosition);Finish();ev.Use();}
        }
        static void Fill(Rect r,Color c) {Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        GUIStyle Style(int size,Font font,FontStyle weight=FontStyle.Normal)
        {
            var s=new GUIStyle(GUI.skin.label) {fontSize=size,font=font,fontStyle=weight,alignment=TextAnchor.MiddleLeft};
            s.normal.textColor=ink;return s;
        }
        void OnGUI()
        {
            if(skin==null) return;
            HandleInput();
            if(heading==null)
            {
                var font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","PingFang SC","Arial"},18);
                heading=Style(27,font,FontStyle.Bold);copy=Style(17,font);caption=Style(14,font);
                button=Style(16,font);button.normal.textColor=Color.white;button.alignment=TextAnchor.MiddleCenter;
            }
            float scale=Mathf.Clamp(Mathf.Min(Screen.width/920f,Screen.height/555f),.6f,2.3f);
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float w=Screen.width/scale,h=Screen.height/scale,top=h*.13f,bottom=h*.875f;
            Fill(new Rect(0,0,w,top),paper);
            GUI.Label(new Rect(20,2,w-180,top-8),"条纹织带",heading);
            GUI.Label(new Rect(w-157,0,150,top),"棉织物   /   48 cm",caption);
            if(Wide)
            {
                float x=w*.77f,pw=w-x;
                Fill(new Rect(x,top,pw,bottom-top),new Color(.91f,.90f,.85f));
                GUI.Label(new Rect(x+12,top+12,pw-24,30),"裂口特写",copy);
                float imgH=(pw-24)*2/3;
                GUI.DrawTexture(new Rect(x+12,top+50,pw-24,imgH),loupe,ScaleMode.ScaleToFit,false);
                var y=top+imgH+68;
                GUI.Label(new Rect(x+12,y,pw-24,28),ribbon.Split?"已撕到夹口":held?"正在牵拉":ribbon.Broken>0?"已松手 · 可接着撕":"右端已开小口",copy);
                float progress=ribbon.Broken/(float)(RibbonTear.Bundles-RibbonTear.Starter);
                Fill(new Rect(x+12,y+42,pw-24,5),new Color(.74f,.73f,.67f));
                Fill(new Rect(x+12,y+42,(pw-24)*progress,5),new Color(.23f,.42f,.46f));
                GUI.Label(new Rect(x+12,y+52,pw-22,30),"新撕开   "+(100*RibbonTear.Length*ribbon.Broken/RibbonTear.Bundles).ToString("0.0")+" cm",caption);
            }
            if(!held && !ribbon.Split)
            {
                Vector2 corner=ScreenPoint(skin.Point(.97f,.62f,side))/scale;
                Fill(new Rect(corner.x-6,corner.y-2,12,4),paper);
                Fill(new Rect(corner.x-2,corner.y-6,4,12),paper);
            }
            Fill(new Rect(0,bottom,w,h-bottom),paper);
            GUI.Label(new Rect(18,bottom,w-140,h-bottom),"抓住右上布角向上拖，或抓右下角向下拖。松手后可再抓。",caption);
            var r=new Rect(w-114,bottom+((h-bottom)-36)/2,96,36);
            Fill(r,ink);if(GUI.Button(r,"换新布带",button)) NewStrip();
        }
        void OnDestroy()
        {
            if(skin!=null){Destroy(skin.Surface);Destroy(skin.Yarns);}
            if(loupe) {detail.targetTexture=null;loupe.Release();Destroy(loupe);}
            foreach(var mat in materials) Destroy(mat);
        }
    }
}
