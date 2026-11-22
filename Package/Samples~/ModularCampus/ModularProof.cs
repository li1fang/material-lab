using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    public sealed class ModularProof:MonoBehaviour
    {
        [Serializable] class RunData {public string caseName;public int spans,posts,fallen,tilted,propagated;public double constructMs,p95Ms,peakMs;public int frames;}
        [Serializable] class Evidence {
            public string unity,device,cpu;public int width,height,brickPieces,cornerFront,cornerSide,untouched;
            public float metalBend,contactDepth,lastSpan;public bool attachedAfterReturn,replayMatches;
            public List<RunData> parking=new List<RunData>();
        }
        CampusWorkbench w;string output;Evidence data=new Evidence();
        public void Run(CampusWorkbench work){
            w=work;var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--output");
            output=i>=0?args[i+1]:Path.Combine(Application.persistentDataPath,"campus-proof");
            Directory.CreateDirectory(output);data.unity=Application.unityVersion;data.device=SystemInfo.graphicsDeviceName;data.cpu=SystemInfo.processorType;
            data.width=Screen.width;data.height=Screen.height;StartCoroutine(Guard());
        }
        IEnumerator Guard(){
            var stack=new Stack<IEnumerator>();stack.Push(Verify());
            while(stack.Count>0){object v;
                try{if(!stack.Peek().MoveNext()){stack.Pop();continue;}v=stack.Peek().Current;}
                catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());UnityEngine.Debug.LogException(e);Application.Quit(2);yield break;}
                if(v is IEnumerator inner)stack.Push(inner);else yield return v;
            }
            File.WriteAllText(Path.Combine(output,"results.json"),JsonUtility.ToJson(data,true));
            UnityEngine.Debug.Log("MODULAR_PROOF_OK");Application.Quit();
        }
        static void Check(bool ok,string s){if(!ok)throw new InvalidDataException(s);}
        void Advance(int ticks){for(int j=0;j<ticks;j++)w.Step();}
        IEnumerator Photo(string label){
            yield return new WaitForEndOfFrame();
            var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,label+".jpg"),tex.EncodeToJPG(92));Destroy(tex);
        }
        IEnumerator Verify(){
            yield return new WaitForSecondsRealtime(3.2f);
            Check(w.layout.spans.Count==5&&w.layout.posts.Count==6&&w.layout.corner==3,"shared corner topology");
            yield return Photo("01-intact");
            w.Act("drive",1);Advance(20);Check(w.ContactDepth==0&&w.walls[1].Missing==0,"early contact");
            Advance(190);data.brickPieces=w.walls[1].Missing;data.contactDepth=w.ContactDepth;
            Check(data.brickPieces>0&&w.walls[0].Missing==0&&w.walls[2].Missing==0,"brick contact locality");
            yield return Photo("02-brick-return");
            w.Configure(false,false,false,true);w.Act("drive",1);Advance(210);
            data.metalBend=w.walls[1].Bend;data.attachedAfterReturn=w.walls[1].Released && w.walls[1].RetainedEndError<.015f;
            Check(data.attachedAfterReturn&&data.metalBend>.9f&&Mathf.Abs(w.ContactDepth-data.contactDepth)<.0001f,"retained rail");
            w.Seek(0);Check(w.walls[1].Bend==0,"rewind rail");w.Seek(w.End);
            data.replayMatches=Mathf.Abs(w.walls[1].Bend-data.metalBend)<.0001f;Check(data.replayMatches,"rail replay");
            yield return Photo("03-metal-return");
            w.Configure(false,false,false,false);w.Act("post",3);Advance(60);
            data.cornerFront=w.walls[2].Missing;data.cornerSide=w.walls[3].Missing;data.untouched=w.walls[0].Missing;
            Check(data.cornerFront>0&&data.cornerSide>0&&data.untouched==0,"shared corner effect");
            w.Configure(false,false,false,true,2);w.Act("post",3);Advance(30);
            Check(w.walls[2].RetainedEndError<.015f && w.walls[3].Missing>0,"iron end follows the same corner as the returning brick wall");
            w.Configure(false,false,true,false);
            var e=w.layout.spans[w.layout.corner-1];data.lastSpan=Vector3.Distance(w.layout.posts[e.x],w.layout.posts[e.y]);
            Check(w.layout.posts.Count==7&&Mathf.Abs(data.lastSpan-1.4f)<.002f,"trimmed end span");
            var last=w.walls[w.layout.corner-1].root;var target=last.TransformPoint(new Vector3(0,.89f,-.114f));
            Physics.SyncTransforms();var cam=Camera.main;
            Check(w.Click(cam.ScreenPointToRay(cam.WorldToScreenPoint(target))) && w.walls[w.layout.corner-1].Missing>0,"trimmed-span pick and collider");
            yield return Photo("04-extended");
            foreach(bool big in new[]{false,true}){
                var watch=Stopwatch.StartNew();w.Configure(true,big,false,false);watch.Stop();
                var g=w.parking;var r=new RunData{caseName=big?"3x5":"2x3",spans=g.panels.Length,posts=g.posts.Length,constructMs=watch.Elapsed.TotalMilliseconds};
                Check(r.spans==(big?15:6)&&r.posts==(big?24:12),"parking layout");
                yield return Photo(big?"07-large":"05-small");
                for(int j=0;j<30;j++)yield return null;
                var times=new double[180];watch.Restart();
                for(int j=0;j<times.Length;j++){
                    if(j==28){w.Act("sequence");Check(g.Fallen==1,"first release");}
                    w.Step();yield return null;times[j]=watch.Elapsed.TotalMilliseconds;watch.Restart();
                }
                Array.Sort(times);r.frames=times.Length;r.p95Ms=times[170];r.peakMs=times[179];r.fallen=g.Fallen;r.propagated=g.Waves;r.tilted=g.Tilted;
                Check(g.Waves>0 && g.Fallen>=2&&g.Fallen<r.spans,"bounded propagation");
                int lastIndex=g.panels.Length-1;Check(g.panels[lastIndex].localPosition.y>3,"remote bay");
                Vector3 end=g.panels[0].position;w.Seek(0);Check(w.parking.Fallen==0,"reset floor");w.Seek(w.End);
                Check(Vector3.Distance(end,w.parking.panels[0].position)<.001f&&w.parking.Fallen==r.fallen,"floor replay");
                data.parking.Add(r);yield return Photo(big?"08-large-drop":"06-small-drop");
            }
        }
    }
}
