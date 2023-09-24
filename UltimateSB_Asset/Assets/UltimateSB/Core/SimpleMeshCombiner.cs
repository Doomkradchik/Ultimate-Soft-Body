using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleMeshCombiner : MonoBehaviour
{
    [SerializeField] private MeshFilter[] filters;
    public void CombineMesh()
    {
        var combiners = new CombineInstance[filters.Length];

        int a = 0;
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].transform == transform) { continue; }
            var mesh = filters[i].sharedMesh.subMeshCount;
            combiners[i] = new CombineInstance
            {
                mesh = filters[i].sharedMesh,
                subMeshIndex = 0,
                transform = filters[i].transform.localToWorldMatrix
            };
            filters[i].gameObject.SetActive(false);
            a++;
        }

        var curMesh = new Mesh();
        curMesh.CombineMeshes(combiners, true, true, false);
        curMesh.Optimize();
        curMesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = curMesh;
       // GetComponent<MeshCollider>().sharedMesh = curMesh;
        Debug.Log($"Vertices : {curMesh.vertices.Length}");
        Debug.Log($"Triangles : {curMesh.triangles.Length}");
    }
}
