using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MaterialDestruction.Sample
{
    public sealed partial class BranchLab : MonoBehaviour
    {
        public BranchProfile profile = new BranchProfile();
        LabBench bench;
        BranchModel model;
        BranchSkin stump, tip;
        GameObject tipObject;
        Material wood;
        Rigidbody fragment;
        Vector3 initialTip, releaseCenter;
        float peakDeflection, fall;
        float drive, contact = .83f, pointerStart, manualDrive, pendingDrive;
        float accumulator;
        bool dragging, logging, capturing;
        BranchTrial playing;
        int playTick;
        readonly List<BranchInput> inputs = new List<BranchInput>();
        readonly Vector3[] previousCenters = new Vector3[BranchModel.Links+1];
        BranchTrial saved;
        string statusLine="", proofDir;
        GUIStyle title, small, control, badge;
        Font font;

        void Awake()
        {
            Application.targetFrameRate=60;
            Time.fixedDeltaTime=BranchModel.StepSeconds;
            bench=new LabBench(transform);
            wood=bench.Material(Color.white);
            stump=new BranchSkin(); tip=new BranchSkin();
            MakeSkin("Clamped wood",stump);
            tipObject=MakeSkin("Released wood",tip);
            ResetSpecimen(profile);
            var args=Environment.GetCommandLineArgs();
            proofDir=Option(args,"--branch-proof");
            string path=Option(args,"--branch-trial");
            if(proofDir!=null)
            {
                capturing=true;
                Time.captureFramerate=30;
                StartCoroutine(RecordProof(path));
            }
            else if(path!=null) Begin(BranchTrial.Read(File.ReadAllText(path)));
            string benchmark=Option(args,"--branch-benchmark");
            if(benchmark!=null && !capturing) StartCoroutine(Benchmark(benchmark));
            if(!capturing && path==null && benchmark==null && Array.IndexOf(args,"--wood")<0)
                EnterCeramic(Option(args,"--ceramic-proof"));
        }
        static string Option(string[] args,string name)
        {
            int i=Array.IndexOf(args,name);
            return i>=0 && i+1<args.Length ? args[i+1] : null;
        }
        GameObject MakeSkin(string label,BranchSkin skin)
        {
            var go=new GameObject(label);
            go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=skin.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=wood;
            return go;
        }
        void ResetSpecimen(BranchProfile config, bool instant=false)
        {
            // Replace the released object's physics immediately as Destroy is end-of-frame.
            if(fragment)
            {
                tipObject.SetActive(false);
                Destroy(tipObject);
                tipObject=MakeSkin("Released wood",tip);
                fragment=null;
            }
            tipObject.SetActive(false);
            model=new BranchModel(config,instant);
            initialTip=model.Centers[BranchModel.Links];
            peakDeflection=fall=0;
            drive=0; manualDrive=0; pendingDrive=0; contact=.83f; dragging=false; logging=false; inputs.Clear();
            playing=null; playTick=0; accumulator=0; statusLine="";
            Refresh();
        }
        void Begin(BranchTrial trial)
        {
            trial.Validate();
            ResetSpecimen(trial.profile,trial.releaseAtFirstBreak);
            playing=trial;
            statusLine=trial.releaseAtFirstBreak?"对照 · 首裂即脱落":"轨迹回放";
        }
        void Update()
        {
            if(ceramicMode) { FrameCeramic(); return; }
            if(capturing) return;
            Pointer();
            accumulator+=Mathf.Min(Time.deltaTime,.12f);
            while(accumulator>=BranchModel.StepSeconds)
            {
                accumulator-=BranchModel.StepSeconds;
                if(playing!=null && playTick<playing.Duration)
                {
                    AdvanceAndRecord(playing.DriveAt(playTick),playing.ContactAt(playTick)); playTick++;
                }
                else
                {
                    if(playing!=null) { playing=null; statusLine="回放结束"; }
                    if(logging || model.Tick>0)
                    {
                        float sample=Mathf.Abs(pendingDrive)>Mathf.Abs(manualDrive)?pendingDrive:manualDrive;
                        pendingDrive=0;
                        AdvanceAndRecord(sample,contact);
                    }
                }
            }
            Refresh();
        }

        void AdvanceAndRecord(float force,float at)
        {
            if(inputs.Count<36000) inputs.Add(new BranchInput { drive=force,contact=at });
            Advance(force,at);
        }

        void Advance(float force,float at)
        {
            drive=force; contact=at;
            bool wasDetached=model.Detached;
            if(!wasDetached) Array.Copy(model.Centers,previousCenters,model.Centers.Length);
            model.Step(force,at);
            if(!wasDetached)
                peakDeflection=Mathf.Max(peakDeflection,Vector3.Distance(initialTip,model.Centers[BranchModel.Links]));
            if(!wasDetached && model.Detached) Release();
            if(fragment) fall=Mathf.Max(fall,releaseCenter.y-fragment.worldCenterOfMass.y);
        }

        void Release()
        {
            int j=model.CrackJoint;
            tip.Update(model,j,BranchModel.Links);
            tipObject.SetActive(true);
            fragment=BranchFragment.Attach(tipObject,model,previousCenters,.08f);
            releaseCenter=fragment.worldCenterOfMass;
        }
        void Refresh()
        {
            stump.Update(model,0,model.Detached?model.CrackJoint:BranchModel.Links);
            bench.Frame();
            int focus=model.CrackJoint>=0?model.CrackJoint:6;
            bench.Focus(transform.TransformPoint(model.Centers[focus]));
            float at=contact*BranchModel.Links;
            int j=Mathf.Min(BranchModel.Links-1,(int)at);
            var p=Vector3.Lerp(model.Centers[j],model.Centers[j+1],at-j);
            float sign=drive>0?-1:1;
            bench.Probe.position=transform.TransformPoint(p+Vector3.up*(model.Radius(contact)+.014f)*sign);
            bench.Probe.gameObject.SetActive(!model.Detached);
        }
        void Pointer()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            if(keyboard!=null)
            {
                if(keyboard.rKey.wasPressedThisFrame) { ResetSpecimen(profile); return; }
                if(keyboard.spaceKey.wasPressedThisFrame) { Begin(new BranchTrial {profile=profile}); return; }
                if(keyboard.f5Key.wasPressedThisFrame) SaveTake();
                if(keyboard.f9Key.wasPressedThisFrame) LoadTake();
            }
            if(!Application.isMobilePlatform) return;
            var pointer=UnityEngine.InputSystem.Pointer.current;
            if(pointer==null) return;
            Vector2 point=pointer.position.ReadValue();
            bool down=pointer.press.wasPressedThisFrame,held=pointer.press.isPressed,up=pointer.press.wasReleasedThisFrame;
#else
            if(Input.GetKeyDown(KeyCode.R)) { ResetSpecimen(profile); return; }
            if(Input.GetKeyDown(KeyCode.Space)) { Begin(new BranchTrial {profile=profile}); return; }
            if(Input.GetKeyDown(KeyCode.F5)) SaveTake();
            if(Input.GetKeyDown(KeyCode.F9)) LoadTake();
            if(!Application.isMobilePlatform || Input.touchCount==0) return;
            var touch=Input.GetTouch(0); Vector2 point=touch.position;
            bool down=touch.phase==TouchPhase.Began;
            bool up=touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled;
            bool held=!up;
#endif
            Gesture(point,down,held,up);
        }

        void Gesture(Vector2 point,bool down,bool held,bool up)
        {
            if(down && point.y>Screen.height*.18f && !model.Detached)
            {
                int nearest=1;
                float distance=float.PositiveInfinity;
                for(int j=2;j<=BranchModel.Links;j++)
                {
                    Vector2 screen=bench.Camera.WorldToScreenPoint(transform.TransformPoint(model.Centers[j]));
                    float d=Vector2.SqrMagnitude(screen-point);
                    if(d<distance) { distance=d; nearest=j; }
                }
                if(distance>Mathf.Pow(Screen.height*.13f,2)) return;
                playing=null;
                contact=Mathf.Clamp((float)nearest/BranchModel.Links,.12f,1f);
                pointerStart=point.y; dragging=true; logging=true; statusLine="手动施力";
            }
            if(dragging && (held||up))
            {
                manualDrive=Mathf.Clamp((point.y-pointerStart)/(Screen.height*.23f),-1.6f,1.6f);
                if(Mathf.Abs(manualDrive)>Mathf.Abs(pendingDrive)) pendingDrive=manualDrive;
            }
            if(up) { manualDrive=0; dragging=false; }
        }

        void SaveTake()
        {
            if(inputs.Count==0) { statusLine="先拖动枝条，再保存"; return; }
            saved=new BranchTrial {profile=model.Profile,releaseAtFirstBreak=model.ReleaseAtFirstBreak,samples=inputs.ToArray()};
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath,"branch-take.json"),JsonUtility.ToJson(saved));
                statusLine="操作已存盘 · F9 重放";
            }
            catch(IOException e) { statusLine=e.Message; }
        }
        void LoadTake()
        {
            try
            {
                string path=Path.Combine(Application.persistentDataPath,"branch-take.json");
                if(File.Exists(path)) saved=BranchTrial.Read(File.ReadAllText(path));
                if(saved!=null) Begin(saved); else statusLine="还没有保存的操作";
            }
            catch(Exception e) { statusLine=e.Message; }
        }

        [Serializable]
        sealed class Timing
        {
            public string gpu, editor;
            public int width, height, frames, trials, vSync;
            public float p95Ms, maxMs, fractureFrameMaxMs;
        }
        IEnumerator Benchmark(string file)
        {
            var times=new List<float>();
            var timing=new Timing {gpu=SystemInfo.graphicsDeviceName, editor=Application.unityVersion,
                width=Screen.width,height=Screen.height,trials=3,vSync=QualitySettings.vSyncCount};
            // Let the preceding update render and present before attributing its elapsed time.
            for(int pass=0;pass<=timing.trials;pass++)
            {
                Begin(new BranchTrial {peak=pass%2==0?1.24f:-1.24f});
                while(playing!=null)
                {
                    int before=model.Broken;
                    double start=Time.realtimeSinceStartupAsDouble;
                    yield return null;
                    bool changed=before!=model.Broken;
                    yield return new WaitForEndOfFrame();
                    float ms=(float)((Time.realtimeSinceStartupAsDouble-start)*1000);
                    if(pass==0) continue;
                    times.Add(ms);
                    if(changed) timing.fractureFrameMaxMs=Mathf.Max(timing.fractureFrameMaxMs,ms);
                }
            }
            times.Sort();
            timing.frames=times.Count;
            timing.p95Ms=times[Mathf.Clamp(Mathf.CeilToInt(times.Count*.95f)-1,0,times.Count-1)];
            timing.maxMs=times[times.Count-1];
            File.WriteAllText(file,JsonUtility.ToJson(timing,true));
            Debug.Log("BRANCH_BENCHMARK_OK");
            Application.Quit();
        }

        static GUIStyle TextStyle(int size,Color color,FontStyle weight=FontStyle.Normal)
        {
            var style=new GUIStyle(GUI.skin.label) {fontSize=size,fontStyle=weight,alignment=TextAnchor.MiddleLeft};
            style.normal.textColor=color; return style;
        }
        static void Panel(Rect r,Color color)
        {
            var old=GUI.color; GUI.color=color; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=old;
        }
        bool Button(Rect r,string label)
        {
            Panel(r,new Color(.19f,.28f,.26f,.96f));
            return GUI.Button(r,label,control);
        }
        void OnGUI()
        {
            // Mouse transitions can all arrive between fixed steps. Preserve the strongest
            // unsampled displacement, including the final position of a quick release.
            var ev=Event.current;
            if(!ceramicMode && !capturing && !Application.isMobilePlatform && ev.button==0 &&
                (ev.type==EventType.MouseDown || ev.type==EventType.MouseDrag || ev.rawType==EventType.MouseUp))
            {
                var p=new Vector2(ev.mousePosition.x,Screen.height-ev.mousePosition.y);
                Gesture(p,ev.type==EventType.MouseDown,ev.type==EventType.MouseDrag,ev.rawType==EventType.MouseUp);
            }
            if(title==null)
            {
                font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","PingFang SC","Noto Sans CJK SC","Arial"},16);
                var ink=new Color(.10f,.17f,.14f);
                title=TextStyle(28,ink,FontStyle.Bold); small=TextStyle(15,ink);
                badge=TextStyle(15,Color.white,FontStyle.Bold);
                control=TextStyle(15,Color.white);
                control.alignment=TextAnchor.MiddleCenter;
                foreach(var s in new[]{title,small,badge,control}) s.font=font;
            }
            float scale=Mathf.Clamp(Mathf.Min(Screen.width/940f,Screen.height/560f),.55f,2.5f);
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float w=Screen.width/scale,h=Screen.height/scale;
            if(ceramicMode) { DrawCeramic(w,h); return; }
            Panel(new Rect(0,0,w,72),new Color(.94f,.94f,.88f,.95f));
            GUI.Label(new Rect(22,6,w-240,40),"鲜枝弯折试验",title);
            GUI.Label(new Rect(24,45,w-245,22),"夹持端固定   /   茎长 "+(model.Profile.length*100).ToString("0")+" cm",small);
            if(!capturing && Button(new Rect(w-314,18,108,34),"去陶碟台")) EnterCeramic(null);
            string phase=model.Detached?"枝梢脱落":model.Broken>0?"纤维开裂":Mathf.Abs(model.Load)>.03f?"弯曲受力":"自由状态";
            Panel(new Rect(w-192,16,170,38),new Color(.21f,.31f,.26f));
            GUI.Label(new Rect(w-183,17,159,36),phase+"   "+model.Broken+" / "+BranchModel.Fibers,badge);
            if(Screen.width>Screen.height*1.18f)
            {
                float x=w*.76f, width=w-x;
                Panel(new Rect(x,72,width,h-148),new Color(.91f,.92f,.86f));
                GUI.Label(new Rect(x+12,84,width-20,26),"断口特写",small);
                float height=(width-20)*330f/448f;
                GUI.DrawTexture(new Rect(x+10,116,width-20,height),bench.DetailTexture,ScaleMode.ScaleToFit,false);
                float top=125+height;
                GUI.Label(new Rect(x+12,top,width-20,26),"仍相连的纤维",small);
                for(int f=0;f<BranchModel.Fibers;f++)
                {
                    var tint=model.IsBroken(f)?new Color(.80f,.60f,.42f):new Color(.25f,.40f,.29f);
                    Panel(new Rect(x+13+(f%8)*(width-26)/8,top+33+(f/8)*13,(width-26)/8-3,9),tint);
                }
                if(model.Events.Count>0)
                    GUI.Label(new Rect(x+12,top+66,width-20,25),model.Events[0].side=="upper"?"上侧首先开裂":"下侧首先开裂",small);
            }
            Panel(new Rect(0,h-76,w,76),new Color(.94f,.94f,.88f,.97f));
            GUI.Label(new Rect(20,h-74,w-32,27),"按住枝条，上下拖动施力，松开卸载。  "+statusLine,small);
            string[] labels={"新枝  R","浅压","向下深压","向上深压","即时脱落对照","保存操作","重放操作"};
            float buttonWidth=Mathf.Min(116,(w-44)/labels.Length-5);
            for(int i=0;i<labels.Length;i++)
            {
                if(!Button(new Rect(20+i*(buttonWidth+5),h-42,buttonWidth,32),labels[i])||capturing) continue;
                if(i==0) ResetSpecimen(profile);
                if(i>=1 && i<=3) Begin(new BranchTrial {profile=profile, peak=i==1?-.24f:i==2?-1.24f:1.24f});
                if(i==4) Begin(new BranchTrial {profile=profile,releaseAtFirstBreak=true});
                if(i==5) SaveTake();
                if(i==6) LoadTake();
            }
        }
        IEnumerator RecordProof(string trialPath)
        {
            Directory.CreateDirectory(proofDir);
            var cases=!string.IsNullOrEmpty(trialPath)?new[]{BranchTrial.Read(File.ReadAllText(trialPath))}:
                new[]{new BranchTrial {peak=-.24f},new BranchTrial(),new BranchTrial {peak=1.24f},
                    new BranchTrial {releaseAtFirstBreak=true}};
            var endFrame=new WaitForEndOfFrame();
            yield return null;
            var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            for(int c=0;c<cases.Length;c++)
            {
                var trial=cases[c]; Begin(trial);
                string dir=Path.Combine(proofDir,"case-"+c);
                Directory.CreateDirectory(dir);
                for(int i=0;i<trial.Duration;)
                {
                    for(int sub=0;sub<2 && i<trial.Duration;sub++,i++)
                        Advance(trial.DriveAt(i),trial.ContactAt(i));
                    Refresh();
                    yield return endFrame;
                    texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(dir,((i-1)/2).ToString("D4")+".jpg"),texture.EncodeToJPG(88));
                    yield return null;
                }
                var report=new BranchReport {tick=model.Tick,joint=model.CrackJoint,broken=model.Broken,
                    detached=model.Detached,events=model.Events.ToArray(),peakDeflectionM=peakDeflection,
                    residualDeflectionM=Vector3.Distance(initialTip,model.Centers[BranchModel.Links]),fragmentFallM=fall};
                File.WriteAllText(Path.Combine(dir,"result.json"),JsonUtility.ToJson(report,true));
            }
            Destroy(texture); Debug.Log("BRANCH_CAPTURE_OK"); Application.Quit();
        }
    }
}
