using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MaterialDestruction.Tests
{
    public class CeramicColliderTests
    {
        [UnityTest]
        public IEnumerator RebuiltColliderHasAnOpenNotchAndSolidOppositeRim()
        {
            var scene=SceneManager.CreateScene("Ceramic notch",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            Mesh intact=null, chipped=null;
            try
            {
                var dish=new GameObject("Supported saucer");
                SceneManager.MoveGameObjectToScene(dish,scene);
                var collider=dish.AddComponent<MeshCollider>();
                var rim=new CeramicRim();
                intact=CeramicShell.Make(rim);
                collider.sharedMesh=intact;
                var physics=scene.GetPhysicsScene();
                Vector3 edge=new Vector3(.13f,.3f,.01f);
                Physics.SyncTransforms();
                Assert.That(physics.Raycast(edge,Vector3.down,.6f));
                Assert.That(rim.Strike(edge,Vector3.down,.47f).Length,Is.GreaterThan(0));
                chipped=CeramicShell.Make(rim);
                collider.sharedMesh=chipped;
                Physics.SyncTransforms();
                Assert.That(physics.Raycast(edge,Vector3.down,.6f),Is.False);
                Assert.That(physics.Raycast(new Vector3(-edge.x,edge.y,-edge.z),Vector3.down,.6f));
            }
            finally {
                SceneManager.UnloadSceneAsync(scene);
                if(intact) Object.Destroy(intact);
                if(chipped) Object.Destroy(chipped);
            }
            yield return null;
        }
    }
}
