using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ConvexMeshCutter : MonoBehaviour
{
    public Vector3Int cuts;
    [HideInInspector][SerializeField] private ComputeShader vertexConverter;
    [Range(0.1f, 1f)]
    [SerializeField] private float qualityMeshSimp = 0.5f;
    ComputeBuffer indexes;
    ComputeBuffer outputVerticesBuffer;
    ComputeBuffer ROMeshVerticesBuffer;

    [HideInInspector] public ConvexDataPair[] convexPairs;
    Dictionary<MeshCollider, int[]> cutVertexIndexes = new Dictionary<MeshCollider, int[]>();


    [HideInInspector] public Mesh target;


    struct DestinateSubmeshJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Vector3> source;
        [ReadOnly]
        public NativeArray<int> ID;
        [WriteOnly]
        public NativeArray<Vector3> destination;

        public void Execute(int index)
        {
            destination[index] = source[ID[index]];
        }
    }

    public void Init(int vertCount)
    {
        if (convexPairs == null || convexPairs.Length == 0)
            Debug.LogError("Convex colliders are not updated. Plaease cut the mesh first.");

        indexes = new ComputeBuffer(vertCount, sizeof(int));
        outputVerticesBuffer = new ComputeBuffer(vertCount, sizeof(float) * 3);
        ROMeshVerticesBuffer = new ComputeBuffer(vertCount, sizeof(float) * 3);

        vertexConverter.SetBuffer(0, "ID", indexes);
        vertexConverter.SetBuffer(0, "ResultV", outputVerticesBuffer);
        vertexConverter.SetBuffer(0, "InputV", ROMeshVerticesBuffer);

        foreach (var pair in convexPairs)
            cutVertexIndexes.Add(pair.meshCollider, pair.indexes);
    }

    public void UpdateLocalVerticesSegmentColliders(Vector3[] vertices, int[] localIndexes)
    {
        ROMeshVerticesBuffer.SetData(vertices);

        foreach (var pair in cutVertexIndexes)
        {
            var verts = pair.Key.sharedMesh.vertices;
            indexes.SetData(localIndexes);
            vertexConverter.Dispatch(0, Extensions.GetLength(pair.Value.Length, 64), 1, 1);
            outputVerticesBuffer.GetData(verts);
            var mesh = pair.Key.sharedMesh;
            mesh.vertices = verts;
            pair.Key.sharedMesh = mesh;

            
        }
    }

    public void UpdateAllColliderVerticesThroughJob(Vector3[] vertices)
    {
        var source = new NativeArray<Vector3>(vertices.Length, Allocator.Persistent);
        source.CopyFrom(vertices);

        foreach (var pair in cutVertexIndexes)
        {
            var length = pair.Value.Length;
            var id = new NativeArray<int>(length, Allocator.TempJob);
            var destination = new NativeArray<Vector3>(length, Allocator.TempJob);

            id.CopyFrom(pair.Value);
            id.AsReadOnly();

            var destinateJob = new DestinateSubmeshJob
            {
                source = source,
                destination = destination,
                ID = id,
            };

            var handler = destinateJob.Schedule(length, 64);
            handler.Complete();

            var mesh = pair.Key.sharedMesh;
            mesh.vertices = destination.ToArray();
            pair.Key.sharedMesh = mesh;
            id.Dispose();
            destination.Dispose();
        }

        source.Dispose();
    }

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

    public void UpdateColliersByAnchors(Vector3[] anchors)
    {
        
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

