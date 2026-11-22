using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MaterialDestruction.Tests
{
    public class ReleasedBranchTests
    {
        [UnityTest]
        public IEnumerator DetachedSpanFallsAndStopsOnSupport()
        {
            var scene=SceneManager.CreateScene("Detached span test",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            try
            {
                var surface=new GameObject("Support");
                SceneManager.MoveGameObjectToScene(surface,scene);
                surface.transform.position=Vector3.down*.52f;
                surface.AddComponent<BoxCollider>().size=new Vector3(8,.10f,8);
                var model=new BranchModel(new BranchProfile());
                var old=new Vector3[model.Centers.Length];
                for(int i=0;i<300 && !model.Detached;i++)
                {
                    Array.Copy(model.Centers,old,old.Length);
                    model.Step(1.24f,.83f);
                }
                Assert.That(model.Detached);
                var go=new GameObject("Released branch");
                SceneManager.MoveGameObjectToScene(go,scene);
                var body=BranchFragment.Attach(go,model,old,.08f);
                float y=body.worldCenterOfMass.y;
                var physics=scene.GetPhysicsScene();
                Physics.SyncTransforms();
                for(int i=0;i<240;i++) physics.Simulate(BranchModel.StepSeconds);
                Assert.That(body.worldCenterOfMass.y,Is.LessThan(y-.10f));
                Assert.That(body.worldCenterOfMass.y,Is.GreaterThan(-.50f));
                Assert.That(body.linearVelocity.magnitude,Is.LessThan(.15f));
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }
    }
}
