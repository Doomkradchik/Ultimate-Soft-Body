using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ConvexMeshCutter : MonoBehaviour
{
    public Vector3Int cuts;
    [HideInInspector][SerializeField] private ComputeShader vertexConverter;

    ComputeBuffer indexes;
    ComputeBuffer outputVerticesBuffer;
    ComputeBuffer ROMeshVerticesBuffer;

    [HideInInspector] public ConvexDataPair[] convexPairs;
    Dictionary<MeshCollider, int[]> cutVertexIndexes = new Dictionary<MeshCollider, int[]>();

    public void Init(int vertexCount)
    {
        if (convexPairs == null || convexPairs.Length == 0)
            Debug.LogError("Convex colliders are not updated. Plaease cut the mesh first.");

        indexes = new ComputeBuffer(vertexCount, sizeof(int));
        outputVerticesBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        ROMeshVerticesBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);

        vertexConverter.SetBuffer(0, "ID", indexes);
        vertexConverter.SetBuffer(0, "ResultV", outputVerticesBuffer);
        vertexConverter.SetBuffer(0, "InputV", ROMeshVerticesBuffer);

        foreach (var pair in convexPairs)
            cutVertexIndexes.Add(pair.meshCollider, pair.indexes);
    }

   /// public void UpdateLocalVertices(Vector3[] vertices, )

    public void UpdateAllColliderVertices(Vector3[] vertices)
    {
        ROMeshVerticesBuffer.SetData(vertices);

        foreach (var pair in cutVertexIndexes)
        {
            var outRes = new Vector3[pair.Value.Length];
            indexes.SetData(pair.Value);
            vertexConverter.Dispatch(0, Extensions.GetLength(pair.Value.Length, 64), 1, 1);
            outputVerticesBuffer.GetData(outRes);
            var mesh = pair.Key.sharedMesh;
            mesh.vertices = outRes;
            pair.Key.sharedMesh = mesh;
        }
    }
}

[System.Serializable]
public struct ConvexDataPair
{
    public MeshCollider meshCollider;
    public int[] indexes;
}

public static class Vector3Extension
{
    public static Vector3 DivideBy(this Vector3 a, Vector3 b)
    {
        return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
    }

}