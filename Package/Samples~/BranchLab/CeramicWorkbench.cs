using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace MaterialDestruction.Sample
{
    public sealed partial class BranchLab
    {
        bool ceramicMode, branchCase, rightEdge, reverseView, highDrop, pendingDrop;
        int dishStep, detachStep;
        string caseName;
        CeramicSpecimen dish;
        Rigidbody ball;
        GameObject fixedBranch;
        Material glaze;
        Vector3 Edge => new Vector3(rightEdge?.82f:-.82f,0,-.57f).normalized*CeramicRim.Radius*.95f;
        static readonly string[] Trials={"light-left","heavy-left","heavy-right","wood-hit","wood-clear"};

        void EnterCeramic(string directory)
        {
            ceramicMode=true;
            if(!glaze) glaze=new Material(Resources.Load<Shader>("CeramicFinish"));
            if(!fixedBranch) fixedBranch=transform.Find("Clamped wood").gameObject;
            NewDish("ready",false,false);
            if(directory!=null)
            {
                capturing=true;
                Time.captureFramerate=30;
                StartCoroutine(CeramicProof(directory));
            }
        }
        void ClearDish()
        {
            if(ball) { ball.gameObject.SetActive(false); Destroy(ball.gameObject); ball=null; }
            if(dish) { dish.gameObject.SetActive(false); Destroy(dish.gameObject); dish=null; }
        }
        void NewDish(string trial,bool coupled,bool shifted)
        {
            ClearDish();
            caseName=trial;dishStep=0;detachStep=-1;branchCase=coupled;pendingDrop=false;
            Begin(new BranchTrial {profile=profile});
            fixedBranch.SetActive(coupled);
            bench.Probe.gameObject.SetActive(coupled);
            var go=new GameObject("Glazed saucer");
            go.transform.SetParent(transform,false);
            go.transform.localPosition=new Vector3(coupled?.47f:.52f,-.381f,shifted?.41f:-.025f);
            dish=go.AddComponent<CeramicSpecimen>();
            dish.Initialize(glaze);
            FrameCeramic();
        }
        void QueueDrop(bool heavy)
        {
            if(branchCase) NewDish("ready",false,false);
            if(ball) { ball.gameObject.SetActive(false); Destroy(ball.gameObject); ball=null; }
            highDrop=heavy; pendingDrop=true;
            caseName=(heavy?"heavy":"light")+(rightEdge?"-right":"-left");
        }
        void Drop()
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name="Test weight";
            go.transform.SetParent(transform,false);
            const float radius=.018f;
            go.transform.localScale=Vector3.one*(2*radius);
            go.transform.position=dish.transform.TransformPoint(Edge+Vector3.up*(.047f+radius+(highDrop?.21f:.012f)));
            go.GetComponent<Renderer>().sharedMaterial=bench.Material(new Color(.70f,.32f,.14f));
            go.GetComponent<Collider>().contactOffset=.0015f;
            ball=go.AddComponent<Rigidbody>();
            ball.mass=.19f;
            ball.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            ball.interpolation=RigidbodyInterpolation.Interpolate;
            dish.ListenTo(ball,null);
            pendingDrop=false;
        }
        void FixedUpdate()
        {
            if(!ceramicMode || !dish) return;
            dish.Step=++dishStep;
            if(pendingDrop) Drop();
            if(!branchCase) return;
            if(playTick<playing.Duration)
            {
                Advance(playing.DriveAt(playTick),playing.ContactAt(playTick));
                playTick++;
                if(model.Detached && detachStep<0) detachStep=dishStep;
            }
            dish.ListenTo(null,fragment);
        }
        void FrameCeramic()
        {
            if(!dish) return;
            if(branchCase) Refresh();
            Vector3 focus=dish.transform.TransformPoint(Edge+Vector3.up*.045f);
            if(dish.Rim.Missing>0) focus=dish.LastDamage;
            else if(branchCase && dish.Contacts.Count>0)
                focus=dish.transform.TransformPoint(dish.Contacts[0].localPoint);
            bench.CeramicCamera(dish.transform.position,focus,branchCase,reverseView);
        }
        void DrawCeramic(float w,float h)
        {
            if(!dish) return;
            Color paper=new Color(.94f,.94f,.89f,.98f),green=new Color(.15f,.31f,.31f);
            Panel(new Rect(0,0,w,72),paper);
            GUI.Label(new Rect(20,5,w-220,40),"青釉陶碟",title);
            string subtitle=branchCase?(caseName=="wood-clear"?"落枝试验   /   碟已移开":"落枝试验   /   碟在枝下"):
                "边沿落锤   /   "+(rightEdge?"右":"左")+"缘"+(caseName=="ready"?"":highDrop?"   ·   高位释放":"   ·   低位释放");
            GUI.Label(new Rect(22,44,w-220,22),subtitle,small);
            Panel(new Rect(w-190,16,168,38),green);
            GUI.Label(new Rect(w-178,16,154,38),dish.Rim.Missing==0?"釉面完整":"碟沿出现缺口",badge);
            int contacts=dish.Contacts.Count;
            if(Screen.width>Screen.height*1.18f)
            {
                float x=w*.76f,width=w-x;
                Panel(new Rect(x,72,width,h-150),new Color(.90f,.92f,.87f));
                GUI.Label(new Rect(x+12,84,width-20,24),"碟沿特写",small);
                float imageH=(width-20)*330/448;
                GUI.DrawTexture(new Rect(x+10,112,width-20,imageH),bench.DetailTexture,ScaleMode.ScaleToFit,false);
                float y=121+imageH;
                GUI.Label(new Rect(x+12,y,width-20,24),"缺损分区    "+dish.Rim.Missing+" / "+CeramicRim.Cells,small);
                for(int i=0;i<CeramicRim.Cells;i++)
                    Panel(new Rect(x+12+(i%18)*(width-24)/18,y+30+(i/18)*11,(width-24)/18-2,8),
                        dish.Rim.Gone(i)?new Color(.72f,.41f,.22f):green);
                float strongest=0;
                foreach(var hit in dish.Contacts) strongest=Mathf.Max(strongest,hit.impulseNs);
                GUI.Label(new Rect(x+12,y+58,width-20,25),"峰值冲量    "+strongest.ToString("0.000")+" N·s",small);
                GUI.Label(new Rect(x+12,y+82,width-20,25),contacts==0?"尚无接触":(branchCase?"木枝":"落锤")+"已接触   /   碎片 "+dish.Fragments,small);
            }
            Panel(new Rect(0,h-77,w,77),paper);
            GUI.Label(new Rect(18,h-75,w-30,26),branchCase?"观察枝梢脱落后是否碰到碟沿。可翻面查看同一处断口。":
                "选一侧碟沿，再从低位或高位释放同一枚锤球。换新碟可复原。",small);
            string[] labels={"换新碟",rightEdge?"换到左缘":"换到右缘","轻碰","重击","落枝击碟","把碟移开",reverseView?"转回正面":"反面看","木枝台"};
            float bw=(w-36)/labels.Length;
            for(int i=0;i<labels.Length;i++)
            {
                if(!Button(new Rect(15+i*bw,h-42,bw-4,32),labels[i]) || capturing) continue;
                switch(i)
                {
                    case 0: NewDish("ready",false,false); break;
                    case 1: rightEdge=!rightEdge; if(branchCase) NewDish("ready",false,false); break;
                    case 2: QueueDrop(false); break;
                    case 3: QueueDrop(true); break;
                    case 4: NewDish("wood-hit",true,false); break;
                    case 5: NewDish("wood-clear",true,true); break;
                    case 6: reverseView=!reverseView; break;
                    case 7:
                        ceramicMode=false; ClearDish();fixedBranch.SetActive(true);
                        bench.WoodCamera();ResetSpecimen(profile);break;
                }
            }
        }
        IEnumerator CeramicProof(string dir)
        {
            Directory.CreateDirectory(dir);
            var frame=new WaitForEndOfFrame();
            yield return null;
            var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            for(int i=0;i<Trials.Length;i++)
            {
                bool coupled=i>=3;
                string folder=Path.Combine(dir,Trials[i]);
                Directory.CreateDirectory(folder);
                rightEdge=i==2; reverseView=false; highDrop=i!=0;
                NewDish(Trials[i],coupled,i==4);
                int frames=coupled?138:76;
                for(int f=0;f<frames;f++)
                {
                    if(!coupled && f==14) QueueDrop(i!=0);
                    if(i==2 && f==56) reverseView=true;
                    yield return frame;
                    texture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder,f.ToString("D4")+".jpg"),texture.EncodeToJPG(88));
                    yield return null;
                }
                var result=new CeramicResult {trial=Trials[i],dishPosition=dish.transform.position,
                    detachStep=detachStep,steps=dishStep,contacts=dish.Contacts.ToArray(),missingCells=dish.Rim.Missing,
                    fragments=dish.Fragments,maxChipTravelM=dish.MaxChipTravel};
                result.VerifyReplay();
                File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(result,true));
            }
            Destroy(texture); Debug.Log("CERAMIC_CAPTURE_OK");Application.Quit();
        }
    }
}
