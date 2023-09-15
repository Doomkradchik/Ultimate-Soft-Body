using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MeshProcess;

public class TestVHACD : MonoBehaviour
{
    MeshFilter mf;
    private MeshCollider[] poolColliders;
    private VHACD vHACD;

    void Start()
    {
        vHACD = GetComponent<VHACD>();
        mf = GetComponent<MeshFilter>();
        poolColliders = GetComponents<MeshCollider>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            UpdateMesh();
    }

    [ContextMenu("IPD")]
    void UpdateMesh()
    {
        var meshes = vHACD.GenerateConvexMeshes(mf.mesh);
       // SetMeshes(meshes);
    }

    void SetMeshes(List<Mesh> meshes)
    {
        if (meshes.Count > poolColliders.Length)
            throw new System.InvalidOperationException();

        for (int i = 0; i < poolColliders.Length; i++)
            if (i < meshes.Count)
                poolColliders[i].sharedMesh = meshes[i];
            else
                poolColliders[i].sharedMaterial = null;
    }
}
