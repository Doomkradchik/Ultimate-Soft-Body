using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ConvexMeshCutter : MonoBehaviour
{
    public Vector3Int cuts;
    [HideInInspector] [SerializeField] private ComputeShader vertexConverter;
    ComputeBuffer indexes;
    ComputeBuffer outputVerticesBuffer;
    ComputeBuffer ROMeshVerticesBuffer;

    [HideInInspector] public ConvexDataPair[] convexPairs;
    Dictionary<MeshCollider, int[]> cutVertexIndexes = new Dictionary<MeshCollider, int[]>();


    struct DestinateSubmeshJob : IJobParallelFor
    {
        public Vector3 offset;
        [ReadOnly]
        public NativeArray<Vector3> source;
        [ReadOnly]
        public NativeArray<int> ID;
        [WriteOnly]
        public NativeArray<Vector3> destination;

        public void Execute(int index)
        {
            destination[index] = source[ID[index]] + offset;
        }
    }

    struct DestinateSubmeshOverPredictionJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Vector3> source;
        [ReadOnly]
        public NativeArray<Vector3> vels;
        [ReadOnly]
        public NativeArray<int> ID;
        [WriteOnly]
        public NativeArray<Vector3> destination;

        public float k;

        public void Execute(int index)
        {
            destination[index] = source[ID[index]] + vels[ID[index]] * k;
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

    Collider tColl;

    public void OnContact(Collider collider)
    {
        tColl = collider;
        foreach (var coll in cutVertexIndexes.Keys)
            Physics.IgnoreCollision(coll, collider);
    }

    public void Unfreeze()
    {
        foreach (var coll in cutVertexIndexes.Keys)
            Physics.IgnoreCollision(coll, tColl, false);
    }

    public void DestinateCollidersOverPrediction(Vector3[] vertices, Vector3[] velocities)
    {
        var source = new NativeArray<Vector3>(vertices.Length, Allocator.Persistent);
        var vels = new NativeArray<Vector3>(velocities.Length, Allocator.Persistent);
        source.CopyFrom(vertices);
        vels.CopyFrom(velocities);

        foreach (var pair in cutVertexIndexes)
        {
            var length = pair.Value.Length;
            var id = new NativeArray<int>(length, Allocator.TempJob);
            var destination = new NativeArray<Vector3>(length, Allocator.TempJob);

            id.CopyFrom(pair.Value);
            id.AsReadOnly();

            var destinateJob = new DestinateSubmeshOverPredictionJob
            {
                source = source,
                destination = destination,
                vels = vels,
                ID = id,
                k = 0.04f
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

    public void DestinateColliders(Vector3[] vertices, Vector3 offset)
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
                offset = offset
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

