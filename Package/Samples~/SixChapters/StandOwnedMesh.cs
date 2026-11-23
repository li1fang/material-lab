using UnityEngine;
internal sealed class StandOwnedMesh : MonoBehaviour
{
    internal Mesh mesh;
    void OnDestroy() { if (mesh) Destroy(mesh); }
}
