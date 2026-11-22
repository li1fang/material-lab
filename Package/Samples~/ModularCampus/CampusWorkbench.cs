using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    [Serializable] internal struct CampusAction {public int tick,target;public string kind;public Vector2 point;}
    public sealed class CampusWorkbench:MonoBehaviour
    {
        internal CampusDimensions config;internal CampusLayout layout;internal ParkingDeck parking;
        internal readonly List<BoundarySpan> walls=new List<BoundarySpan>();
        internal readonly List<CampusAction> record=new List<CampusAction>();
        internal bool garage,large,extended,iron;internal int selected=1,tick;
        internal float ContactDepth{get;private set;}
        Transform yard,marker;Transform[] posts;float[] angles;CampusKit kit;
        YardCrawler cart;int driveStart=-10000,driveSpan,contactStep=-1,applied;
        int alloySpan=1;Camera view;Font font;GUIStyle style;float orbit=-27,zoom=1,carry;
        internal bool testing;bool running=true;
        public void Start(){
            Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
            config=JsonUtility.FromJson<CampusDimensions>(Resources.Load<TextAsset>("campus-dimensions").text);
            config.Validate();kit=new CampusKit();
            view=new GameObject("Campus view").AddComponent<Camera>();view.orthographic=true;view.tag="MainCamera";view.allowHDR=false;
            view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.78f,.85f,.83f);view.farClipPlane=160;
            var sun=new GameObject("Morning sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(46,-30,0);sun.shadows=LightShadows.Soft;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},18);
            Build();testing=Array.IndexOf(Environment.GetCommandLineArgs(),"--campus-proof")>=0;
            if(testing)gameObject.AddComponent<ModularProof>().Run(this);
            Debug.Log("MODULAR_CAMPUS_READY");
        }
        internal void Configure(bool park,bool big,bool longer,bool rail,int slot=1){
            garage=park;large=big;extended=longer;iron=rail;alloySpan=slot;selected=slot;record.Clear();Build();running=true;
        }
        void Build(){
            if(yard){yard.gameObject.SetActive(false);Destroy(yard.gameObject);}
            foreach(var wall in walls)wall.Dispose();walls.Clear();parking=null;cart=null;
            tick=applied=0;driveStart=-10000;contactStep=-1;ContactDepth=0;
            yard=CampusKit.Node("Campus model",transform,Vector3.zero);
            if(garage){
                layout=CampusLayout.Parking(large?config.largeX:config.smallX,large?config.largeZ:config.smallZ,config.parkingPitch);
                kit.Base(yard,(layout.columns+1.35f)*config.parkingPitch,layout.rows*config.parkingPitch+5);
                parking=new ParkingDeck(layout,kit,yard,config.parkingPitch);posts=parking.posts;
                Sign(layout.posts[0]+new Vector3(-.73f,0,-.62f),"P","398781");
            }else{
                float length=extended?config.extendedFront:config.front;
                layout=CampusLayout.Boundary(length,config.side,config.pitch);
                kit.Base(yard,length+4,config.side+10);
                kit.Solid(yard,"Campus lawn",new Vector3(0,.026f,0),new Vector3(length-.10f,.05f,config.side-.12f),"B6C3A0");
                kit.Solid(yard,"Paved approach",new Vector3(0,.011f,-config.side/2-2.45f),new Vector3(length+2,.022f,4.85f),"BCB6AB");
                for(float x=-length/2;x<length/2;x+=1.1f)kit.Solid(yard,"Paint dash",new Vector3(x,.034f,-config.side/2-3.70f),new Vector3(.59f,.012f,.07f),"F3DFB0");
                posts=new Transform[layout.posts.Count];angles=new float[posts.Length];
                for(int i=0;i<posts.Length;i++)posts[i]=kit.Pillar(yard,i,layout.posts[i],2.1f);
                for(int i=0;i<layout.spans.Count;i++){
                    var edge=layout.spans[i];walls.Add(new BoundarySpan(kit,yard,layout.posts[edge.x],layout.posts[edge.y],iron&&i==alloySpan,i));
                }
                selected=Mathf.Clamp(selected,0,walls.Count-1);
                cart=new YardCrawler(kit,walls[selected].root);
                marker=kit.Solid(yard,"Selected span",Vector3.zero,new Vector3(.85f,.025f,.12f),"EDA846");
                Sign(layout.posts[0]+new Vector3(-.75f,0,.3f),"C","50817B");Mark();
            }
            SetView();Physics.SyncTransforms();
        }
        void Sign(Vector3 basePos,string label,string color){
            kit.Solid(yard,"Sign pole",basePos+Vector3.up*.80f,new Vector3(.1f,1.58f,.12f),"6F837F");
            kit.Solid(yard,"Enamel sign",basePos+Vector3.up*1.68f,new Vector3(.78f,.78f,.12f),color);
            var text=CampusKit.Node("Campus sign",yard,basePos+new Vector3(0,1.68f,-.07f)).gameObject.AddComponent<TextMesh>();
            text.text=label;text.fontSize=60;text.characterSize=.093f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(1,.93f,.74f);
        }
        void SetView(){
            var q=Quaternion.Euler(garage?23:28,orbit,0);float aspect=(float)Screen.width/Mathf.Max(1,Screen.height);
            float extent=garage?Mathf.Max(layout.columns*config.parkingPitch*.58f,layout.rows*config.parkingPitch*.45f)+2.1f:
                Mathf.Max((extended?config.extendedFront:config.front)*.53f,config.side*.58f)+1.25f;
            view.orthographicSize=extent*zoom*Mathf.Max(1,1.66f/aspect);
            var target=new Vector3(0,garage?1.4f:.8f,garage?0:-.7f)-q*Vector3.right*(view.orthographicSize*aspect*.155f);
            view.transform.SetPositionAndRotation(target-q*Vector3.forward*47,q);
        }
        void Mark(){
            marker.position=walls[selected].root.TransformPoint(new Vector3(0,.053f,-.47f));
            marker.rotation=walls[selected].root.rotation;
        }
        void ResizeBoundary(){
            var next=CampusLayout.Boundary(extended?config.front:config.extendedFront,config.side,config.pitch);
            int Remap(int i)=>i>=layout.corner?next.corner+i-layout.corner:Mathf.Min(i,next.corner-1);
            int chosen=Remap(selected),alloy=Remap(alloySpan);
            Configure(false,false,!extended,iron,alloy);SelectSpan(chosen);
        }
        void Apply(CampusAction a){
            float t=a.tick/60f;
            if(a.kind=="drive"){
                driveStart=a.tick;driveSpan=a.target;contactStep=-1;ContactDepth=0;
                cart.root.SetParent(walls[driveSpan].root,false);cart.Place(-3);return;
            }
            if(a.kind=="sequence"){parking.Sequence(t);return;}
            if(a.kind=="post"){
                if(garage){parking.Damage(a.target,t);return;}
                float v=angles[a.target]=Mathf.Min(1,angles[a.target]+.5f);
                posts[a.target].localRotation=Quaternion.Euler(14*v,0,-8*v);
                for(int i=0;i<walls.Count;i++){
                    var e=layout.spans[i];if(e.x==a.target||e.y==a.target)
                        walls[i].Support(angles[e.x],angles[e.y],t,posts[e.x].up,posts[e.y].up);
                }
            }else walls[a.target].Hit(a.point,1,t);
        }
        internal void Act(string kind,int target=0,Vector2 point=default){
            if(kind=="drive"&&tick-driveStart<205)return;
            if(applied<record.Count)record.RemoveRange(applied,record.Count-applied);
            var a=new CampusAction{tick=tick+1,kind=kind,target=target,point=point};
            record.Add(a);running=true;Step();
        }
        internal void Step(){
            tick++;while(applied<record.Count&&record[applied].tick<=tick)Apply(record[applied++]);
            if(garage){parking.Pose(tick/60f);return;}
            int age=tick-driveStart;
            if(age>=0&&age<=205){
                float phase=age<100?age/100f:age<=115?1:(205-age)/90f;
                float z=Mathf.Lerp(-3,-.21f,Mathf.SmoothStep(0,1,phase));
                // The commanded stroke builds pressure while the wall resists the roller.
                var wall = walls[driveSpan];
                float contactZ = -(1.15f + (wall.metal ? .04f : .115f));
                float penetration = Mathf.Clamp01((z - contactZ) / (-.21f - contactZ));
                float travel = Mathf.Max(0, penetration - .28f) * .23f;
                cart.Place(Mathf.Min(z, contactZ) + travel);
                if(penetration>0){
                    ContactDepth=Mathf.Max(ContactDepth,penetration);
                    int step=Mathf.CeilToInt(penetration*10);
                    if(walls[driveSpan].metal || step>contactStep){
                        walls[driveSpan].Contact(0,penetration,tick/60f);contactStep=step;
                    }
                }
            }
            foreach(var wall in walls)wall.Pose(tick/60f);
        }
        internal int End=>record.Count==0?0:record[record.Count-1].tick+210;
        internal void Seek(int to){
            int limit=Mathf.Clamp(to,0,End);if(limit<tick||limit==0)Build();while(tick<limit)Step();running=false;
        }
        void Undo(){if(applied==0)return;record.RemoveRange(applied-1,record.Count-applied+1);int end=End;Build();Seek(end);}
        void SelectSpan(int i){
            if(tick-driveStart<205)return;
            selected=i;cart.root.SetParent(walls[i].root,false);cart.Place(-3);Mark();
        }
        internal bool Click(Ray ray){
            if(!Physics.Raycast(ray,out var hit,120))return false;
            for(int i=0;i<posts.Length;i++)if(hit.transform.IsChildOf(posts[i])){Act("post",i);return true;}
            if(!garage)for(int i=0;i<walls.Count;i++)if(hit.transform.IsChildOf(walls[i].root)&&!hit.transform.IsChildOf(cart.root)){
                SelectSpan(i);Act("hit",i,walls[i].root.InverseTransformPoint(hit.point));return true;
            }
            return false;
        }
        void Update(){
            if(testing)return;
            if(Input.GetKeyDown(KeyCode.R)){record.Clear();Build();running=true;}
            if(Input.GetKeyDown(KeyCode.Z))Undo();
            if(Input.GetKeyDown(KeyCode.Space)){Seek(0);running=true;}
            if(!garage&&Input.GetKeyDown(KeyCode.V))Act("drive",selected);
            float scale=UiScale;Vector2 mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y)/scale;
            if(mouse.x>247&&mouse.y<Screen.height/scale-63){
                if(Input.GetMouseButtonDown(0))Click(view.ScreenPointToRay(Input.mousePosition));
                if(Input.GetMouseButton(1))orbit+=Input.GetAxis("Mouse X")*3;
                zoom=Mathf.Clamp(zoom-Input.mouseScrollDelta.y*.08f,.65f,1.5f);
            }
            SetView();if(running&&tick<End){
                carry+=Mathf.Min(Time.unscaledDeltaTime,.15f)*60;
                while(carry>=1&&tick<End){carry--;Step();}
            }else carry=0;
        }
        float UiScale=>Mathf.Clamp(Mathf.Min(Screen.width/1170f,Screen.height/690f),.60f,2);
        static Color ColorOf(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
        void Fill(Rect r,string c){GUI.color=ColorOf(c);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
        void Label(Rect r,string text,int size=14,bool bold=false,string c="285051"){
            style??=new GUIStyle(GUI.skin.label);style.font=font;style.fontSize=size;style.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
            style.normal.textColor=ColorOf(c);style.alignment=TextAnchor.MiddleLeft;style.wordWrap=false;GUI.Label(r,text,style);
        }
        bool Button(float x,float y,float width,string label,bool active=false){
            var r=new Rect(x,y,width,31);Fill(r,active?"346C69":r.Contains(Event.current.mousePosition)?"C8D9CD":"E1E9DE");
            Label(new Rect(x+8,y,width-8,31),label,14,active,active?"FFFFFF":"244C4E");return GUI.Button(r,GUIContent.none,GUIStyle.none);
        }
        void OnGUI(){
            if(filmView)return;
            if(layout==null)return;float s=UiScale,w=Screen.width/s,h=Screen.height/s;GUI.matrix=Matrix4x4.Scale(new Vector3(s,s,1));
            Fill(new Rect(16,18,220,399),"F6F2E7F6");Label(new Rect(29,27,190,23),"MATERIAL LAB    /    CAMPUS",12,true,"508079");
            Label(new Rect(27,55,202,36),garage?"敞开停车楼":"校园转角围墙",25,true);
            if(Button(29,103,90,"围墙",!garage)&&garage)Configure(false,false,false,false);
            if(Button(126,103,94,"停车楼",garage)&&!garage)Configure(true,false,false,false);
            if(garage){
                Label(new Rect(29,149,194,25),layout.floors.Count+" 跨   ·   "+layout.posts.Count+" 根共用柱",15,true);
                if(Button(29,183,90,"小楼",!large)&&large)Configure(true,false,false,false);
                if(Button(126,183,94,"扩建",large)&&!large)Configure(true,true,false,false);
                if(Button(29,230,191,"运行连续失撑对照"))Act("sequence");
                Label(new Rect(29,271,195,24),"倾斜 "+parking.Tilted+" 跨    落下 "+parking.Fallen+" 跨");
                Label(new Rect(29,304,196,24),"点击柱体可逐次改变支撑");
            }else{
                Label(new Rect(29,147,195,24),layout.spans.Count+" 跨   ·   "+posts.Length+" 根柱   ·   已选 "+(selected+1),14,true);
                if(Button(29,184,90,"选下一跨"))SelectSpan((selected+1)%walls.Count);
                if(Button(126,184,94,walls[selected].metal?"换成砖墙":"换铁栅栏"))Configure(false,false,extended,!walls[selected].metal,selected);
                if(Button(29,225,191,"V   履带车压过再退回"))Act("drive",selected);
                if(Button(29,265,191,extended?"恢复原墙线":"延长第一段墙线"))ResizeBoundary();
                if(Button(29,305,191,"加载共用角柱"))Act("post",layout.corner);
            }
            Label(new Rect(29,353,193,24),"右键拖动旋转 · 滚轮缩放",13);
            if(Button(29,378,90,"Z  撤回"))Undo();
            if(Button(126,378,94,"R  复位")){record.Clear();Build();}
            Fill(new Rect(16,h-59,w-32,43),"F6F2E7F6");
            if(Button(27,h-53,91,"回放   ▶")){Seek(0);running=true;}
            float v=GUI.HorizontalSlider(new Rect(135,h-40,w*.29f,16),tick,0,Mathf.Max(1,End));
            if(Mathf.Abs(v-tick)>.6f)Seek(Mathf.RoundToInt(v));
            Label(new Rect(w*.29f+153,h-53,67,31),(tick/60f).ToString("0.0")+" s",15,true);
            Label(new Rect(w*.29f+231,h-53,w*.49f,31),garage?"柱、梁与楼面共用同一张连接图":walls[selected].metal?"观察横杆弯曲、松脱与退车后的残余形状":"点砖面可打开局部缺口");
        }
        void OnDestroy(){foreach(var wall in walls)wall.Dispose();kit?.Dispose();}
        internal bool filmView;
    }
}
