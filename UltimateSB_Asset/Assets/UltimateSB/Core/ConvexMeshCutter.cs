using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ConvexMeshCutter : MonoBehaviour
{
    public Vector3Int cuts;
    public int[][] cutVertexIndexes;

    public void UpdateVertices(Vector3[] vertices)
    {
        for (int i = 0; i < cutVertexIndexes.GetLength(0); i++)
        {
            for (int j = 0; j < cutVertexIndexes.GetLength(1); j++)
            {
                
            }
        }
    }

    //void SetMeshes(Mesh[] meshes)
    //{
    //    if (meshes.Length > poolColliders.Length)
    //        throw new System.InvalidOperationException();

    //    for (int i = 0; i < poolColliders.Length; i++)
    //        if (i < meshes.Length)
    //            poolColliders[i].sharedMesh = meshes[i];
    //        else
    //            poolColliders[i].sharedMaterial = null;
    //}
}

public static class Vector3Extension
{
    public static Vector3 DivideBy(this Vector3 a, Vector3 b)
    {
        return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
    }

}