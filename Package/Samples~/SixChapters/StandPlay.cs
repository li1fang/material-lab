using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MaterialDestruction.Campus;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

[Serializable] internal sealed class GrandstandRecipe
{
    public int bays, stairBay;
    public float pitch, metersPerUnit;
    public string brickStyle, fracture, pressure;
}

internal sealed class StandPlay : MonoBehaviour
{
    internal static bool ReturnToChapters;
    GrandstandRecipe recipe;
    CampusKit kit;
    BayFrame frame;
    Transform yard;
    Camera eye;
    Font font;
    GUIStyle label, button;
    readonly List<BrickSkin> jackets = new();
    readonly Dictionary<Collider,BrickSkin> skinAt = new();
    readonly List<StyledBrickSpan> walls = new();
    readonly List<WallStudySpan> responses = new();
    readonly Dictionary<Collider,int> wallAt = new();
    readonly List<BoundarySpan> guards = new();
    readonly Dictionary<Collider,BoundarySpan> guardAt = new();
    float now, animateUntil;
    int pressed = -1;
    
    float since;
    string note;
    long buildMs;
    int stairGaps;
    float head, foot;
    Bounds framingBounds;
    int viewportWidth, viewportHeight;
    void Start()
    {
        Application.targetFrameRate=60;
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Noto Sans CJK SC","Arial"},20);
        eye=new GameObject("Stand camera").AddComponent<Camera>();
        eye.transform.SetParent(transform,false);
        eye.orthographic=true; eye.clearFlags=CameraClearFlags.SolidColor; eye.backgroundColor=new Color32(198,220,212,255);
        var sun=new GameObject("Afternoon").AddComponent<Light>();
        sun.transform.SetParent(transform,false); sun.type=LightType.Directional; sun.intensity=1.1f;
        sun.transform.rotation=Quaternion.Euler(42,-28,0); sun.shadows=LightShadows.Soft;
        string[] args=Environment.GetCommandLineArgs();
        int custom=Array.IndexOf(args,"--stand-recipe");
        recipe=JsonUtility.FromJson<GrandstandRecipe>(custom>=0 ? File.ReadAllText(args[custom+1]) : Resources.Load<TextAsset>("stand-two-bay").text);
        Rebuild();
        int proof=Array.IndexOf(args,"--stand-proof");
        if(proof>=0){Application.runInBackground=true; StartCoroutine(Verify(args[proof+1]));}
    }

    void Rebuild()
    {
        if(recipe.bays<2 || recipe.bays>16 || recipe.stairBay<0 || recipe.stairBay>=recipe.bays ||
            !float.IsFinite(recipe.pitch) || recipe.pitch<3 || recipe.pitch>8 ||
            !float.IsFinite(recipe.metersPerUnit) || recipe.metersPerUnit<=0 || recipe.pressure!="stepped")
            throw new ArgumentException("Invalid grandstand recipe");
        var timer=Stopwatch.StartNew();
        Clear();
        kit=new CampusKit(); yard=CampusKit.Node("Stand",transform,Vector3.zero);
        frame=new BayFrame(kit,yard,recipe.pitch,"FAF6E9","D6C8B5",recipe.bays);
        var color=BayBrickMaterials.ForStyle(recipe.brickStyle);
        var trim=new BrickWallSpec{width=.66f,height=BayFrame.CoreHeight,brickWidth=.51f,brickHeight=.245f,brickDepth=.11f,profile=recipe.fracture,surfaceMaterial=color};
        for(int i=0;i<frame.Posts.Length;i++)
        {
            var skin=BrickSkin.Column(kit,frame.Posts[i],i,BayFrame.CoreHeight,trim);
            jackets.Add(skin); foreach(var c in skin.Colliders)skinAt.Add(c,skin);
        }
        stairGaps=0;
        for(int i=0;i<recipe.bays;i++)
        {
            var panel=frame.Panels[i];
            float x=panel.localPosition.x, half=recipe.pitch*.5f;
            float opening=i==recipe.stairBay ? Mathf.Min(1.54f,recipe.pitch*.64f)*.5f+.054f : 0;
            if(opening==0) Guard(panel,-half,half); else
            {
                Guard(panel,-half,-opening); Guard(panel,opening,half); stairGaps++;
            }
            // Alternate enclosed and open areas under the balcony. Leave the stair bay open.
            if(i==recipe.stairBay) continue;
            var a=new Vector3(x-half+.32f,0,-half+.40f);
            var b=new Vector3(x+half-.32f,0,a.z);
            var spec=new BrickWallSpec{width=b.x-a.x,height=1.97f,profile=recipe.fracture,surfaceMaterial=color};
            var wall=new StyledBrickSpan(kit,yard,a,b,i,spec);
            int index=walls.Count; walls.Add(wall);
            responses.Add(new WallStudySpan(wall,2,spec));
            foreach(var c in wall.root.GetComponentsInChildren<Collider>())wallAt.Add(c,index);
        }
        StandArchitecture.Build(kit,yard,frame,recipe.stairBay,recipe.pitch);
        float width=recipe.bays*recipe.pitch;
        var plinth=CampusKit.Node("Plinth",yard,new Vector3(0,0,-2.8f));
        kit.Base(plinth,width+2.4f,recipe.pitch+14);
        kit.Solid(yard,"Track",new Vector3(0,.013f,-10.35f),new Vector3(width+2.3f,.024f,2.6f),"BE745E");
        for(int j=0;j<3;j++)kit.Solid(yard,"Lane",new Vector3(0,.03f,-9.55f-j*.8f),new Vector3(width+2.3f,.01f,.034f),"F3D9BA");
        var visible=yard.GetComponentsInChildren<Renderer>();
        framingBounds=visible[0].bounds;
        foreach(var surface in visible) framingBounds.Encapsulate(surface.bounds);
        Frame(); Physics.SyncTransforms(); pressed=-1;
        note="点砖面看错台，再点同处看断面。点柱外砖可以露出柱芯。";
        buildMs=timer.ElapsedMilliseconds;
    }

    void Guard(Transform panel,float x0,float x1)
    {
        float z=-recipe.pitch*.5f+.11f;
        var rail=new BoundarySpan(kit,panel,new Vector3(x0-.15f,.21f,z),new Vector3(x1+.15f,.21f,z),true,guards.Count);
        rail.root.localScale=new Vector3(1,.48f,.48f); guards.Add(rail);
        foreach(var renderer in rail.root.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=kit.Paint("FFF9EB");
        foreach(var c in rail.root.GetComponentsInChildren<Collider>())guardAt.Add(c,rail);
    }

    void Frame()
    {
        head=72;foot=77;
        eye.pixelRect=new Rect(0,foot,Screen.width,Mathf.Max(1,Screen.height-head-foot));
        eye.transform.rotation=Quaternion.Euler(14,-15,0);
        viewportWidth=Screen.width; viewportHeight=Screen.height;
        // Fit actual geometry projected onto the two axes of this fixed camera.
        var bounds = framingBounds;
        var ext = bounds.extents;
        float Radius(Vector3 axis) => Mathf.Abs(axis.x)*ext.x+Mathf.Abs(axis.y)*ext.y+Mathf.Abs(axis.z)*ext.z;
        eye.aspect=eye.pixelRect.width/eye.pixelRect.height;
        eye.transform.position=bounds.center-eye.transform.forward*65;
        eye.orthographicSize=1.035f*Mathf.Max(Radius(eye.transform.up),Radius(eye.transform.right)/eye.aspect);
        eye.farClipPlane=150;
    }
    void Update()
    {
        if (viewportWidth!=Screen.width || viewportHeight!=Screen.height) Frame();
        now+=Time.deltaTime;
        if (pressed>=0)
        {
            float elapsed=now-since;
            var face=responses[pressed];
            face.Amount=Mathf.SmoothStep(0,.9f,elapsed/.45f);
            if(elapsed>=.75f){face.Retain(face.ContactX,.72f);face.Pose(now);pressed=-1;}
            else face.Pose(now);
            Physics.SyncTransforms();
        }
        if(now<=animateUntil)
        {
            frame.Pose(now);
            foreach(var skin in jackets)skin.Pose(now);
            foreach(var face in responses)face.Pose(now);
        }
        if(Input.GetMouseButtonUp(0) && eye.pixelRect.Contains(Input.mousePosition)) Pick(Input.mousePosition);
    }
    int scar=-1; Vector2 lastScar;
    bool Pick(Vector2 pixel)
    {
        Physics.SyncTransforms();
        if(!Physics.Raycast(eye.ScreenPointToRay(pixel),out var hit,150))return false;
        if(wallAt.TryGetValue(hit.collider,out int i))
        {
            Vector2 p=walls[i].root.InverseTransformPoint(hit.point);
            if(i==scar && Vector2.Distance(p,lastScar)<.7f)
            {
                walls[i].Hit(p,.54f,now); animateUntil=now+2;
                note="砖片脱落，露出断面。";
            }
            else
            {
                if(pressed>=0){var old=responses[pressed];old.Retain(old.ContactX,old.Amount*.8f);old.Pose(now);}
                pressed=scar=i;since=now;lastScar=p;responses[i].ContactX=p.x;note="墙面受压，砖缝逐层错台。";
            }
            return true;
        }
        if(skinAt.TryGetValue(hit.collider,out var skin))
        {skin.TryHit(hit,now);animateUntil=now+2;note="外砖已松开。沿缺口可以点到里层。";return true;}
        if(frame.TryCoreBody(hit.collider,out int post))
        {frame.Damage(post,now);animateUntil=now+2;note="柱芯状态已更新，观察相邻梁板。";return true;}
        if(guardAt.TryGetValue(hit.collider,out var rail))
        {var p=rail.root.InverseTransformPoint(hit.point);rail.Hit(new Vector2(p.x,p.y),.8f,now);rail.Pose(now);return true;}
        return frame.TryHitRail(hit,now);
    }
    void Clear()
    {
        if(yard){yard.gameObject.SetActive(false);Destroy(yard.gameObject);}
        foreach(var j in jackets)j.Dispose();
        foreach(var w in walls)w.Dispose();
        foreach(var g in guards)g.Dispose();
        jackets.Clear(); walls.Clear(); guards.Clear(); responses.Clear(); skinAt.Clear();wallAt.Clear();guardAt.Clear();
        kit?.Dispose(); scar=pressed=-1;animateUntil=-1;
    }
    void OnDestroy(){Clear();BayBrickMaterials.ReleaseAll();}
    void OnGUI()
    {
        if(eye==null)return;
        label??=new GUIStyle(GUI.skin.label){font=font,fontSize=19,normal={textColor=new Color(.12f,.22f,.20f)}};
        button??=new GUIStyle(GUI.skin.button){font=font,fontSize=17};
        GUI.color=new Color(.96f,.94f,.88f);GUI.DrawTexture(new Rect(0,0,Screen.width,head),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0,Screen.height-foot,Screen.width,foot),Texture2D.whiteTexture);GUI.color=Color.white;
        GUI.Label(new Rect(22,17,Screen.width-30,42),$"Material Lab   /   运动场看台       {recipe.bays} 跨  ·  {recipe.pitch*recipe.bays*recipe.metersPerUnit:0.#} 米",label);
        GUILayout.BeginArea(new Rect(22,Screen.height-foot+3,Screen.width-35,foot));
        GUILayout.Label(note,label); GUILayout.BeginHorizontal();
        if(GUILayout.Button("双跨",button)){recipe.bays=2;recipe.stairBay=0;Rebuild();}
        if(GUILayout.Button("增一跨",button)&&recipe.bays<16){recipe.bays++;Rebuild();}
        if(GUILayout.Button("移动楼梯口",button)){recipe.stairBay=(recipe.stairBay+1)%recipe.bays;Rebuild();}
        if(GUILayout.Button("85 米全长",button)){recipe.bays=10;recipe.stairBay=4;Rebuild();}
        if(GUILayout.Button("复原",button))Rebuild();
        if(GUILayout.Button("材料六关",button)){ReturnToChapters=true;SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
        GUILayout.EndHorizontal();GUILayout.EndArea();
    }
    [Serializable] class Reading { public int bays,stairBay,posts,girders,panels,renderers,colliders; public float meters,stairCenterX,deckHeight,frameMedianMs,frameP95Ms;public long buildMs; }
    [Serializable] class Proof { public Reading[] runs=new Reading[4]; public bool brickPicked,jacketPicked,retainedDent; }
    IEnumerator Verify(string dir)
    {
        Directory.CreateDirectory(dir);
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        var proof=new Proof();
        for(int stage=0;stage<4;stage++)
        {
            if(stage==1){recipe.bays++;Rebuild();}
            if(stage==2){recipe.stairBay++;Rebuild();}
            if(stage==3){recipe.bays=10;recipe.stairBay=4;Rebuild();}
            yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir,$"stand-{stage}.png"));
            var times=new float[90];
            for(int f=0;f<100;f++){yield return null;if(f>=10)times[f-10]=Time.unscaledDeltaTime*1000;}
            Array.Sort(times);
            var m=proof.runs[stage]=new Reading{bays=recipe.bays,stairBay=recipe.stairBay,posts=frame.Posts.Length,
                girders=frame.Girders.Length,panels=frame.Panels.Length,renderers=yard.GetComponentsInChildren<Renderer>().Length,
                colliders=yard.GetComponentsInChildren<Collider>().Length,meters=recipe.pitch*recipe.bays*recipe.metersPerUnit,
                frameMedianMs=times[45],frameP95Ms=times[85],buildMs=buildMs};
            var sill=yard.GetComponentsInChildren<Transform>().Single(t=>t.name=="Landing sill");
            var slab=frame.Panels[m.stairBay].Find("Pavement").GetComponent<Renderer>().bounds;
            m.stairCenterX=sill.position.x;
            m.deckHeight=slab.max.y;
            bool joined=Mathf.Abs(sill.position.x-slab.center.x)<.002f && Mathf.Abs(sill.position.y-slab.max.y)<.02f;
            bool open=guardAt.Keys.All(c=>!c.bounds.Contains(new Vector3(sill.position.x,c.bounds.center.y,c.bounds.center.z)) ||
                Mathf.Abs(c.bounds.center.z-(frame.Panels[m.stairBay].position.z-recipe.pitch*.5f+.11f))>.3f);
            if(!joined||!open||m.posts!=2*(m.bays+1)||m.girders!=3*m.bays+1||stairGaps!=1) {Debug.LogError($"Stand topology {m.bays} joined={joined} open={open} sill={sill.position} deck={slab.max.y}");Application.Quit(2);yield break;}
            if(stage==0)
            {
                foreach(var c in wallAt.Keys)
                {
                    var p=eye.WorldToScreenPoint(c.bounds.center);
                    if(!eye.pixelRect.Contains(p)||!Pick(p)||pressed<0)continue;
                    int selected=pressed;
                    yield return new WaitForSeconds(.85f);
                    proof.retainedDent |= responses[selected].Missing==0 && responses[selected].ReadDepths().Any(depth=>depth>.02f);
                    Pick(p);proof.brickPicked=walls.Any(w=>w.Missing>0);if(proof.brickPicked)break;
                }
                foreach(var c in skinAt.Keys)
                {
                    var p=eye.WorldToScreenPoint(c.bounds.center);if(!eye.pixelRect.Contains(p))continue;
                    var n=jackets.Sum(j=>j.Missing);Pick(p);
                    if(jackets.Sum(j=>j.Missing)>n){proof.jacketPicked=true;break;}
                }
            }
        }
        File.WriteAllText(Path.Combine(dir,"stand-results.json"),JsonUtility.ToJson(proof,true));
        Application.Quit(proof.brickPicked&&proof.jacketPicked&&proof.retainedDent?0:3);
    }
}
