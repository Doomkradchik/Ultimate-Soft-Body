using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleMeshCombiner : MonoBehaviour
{
    public MeshFilter[] filters;

    [Range(0.1f, 1f)]
    public float qualityMeshSimp = 0.5f;

    //struct MergeData
    //{
    //    public Mesh mesh;
    //    public Material[] materials;
    //}

}