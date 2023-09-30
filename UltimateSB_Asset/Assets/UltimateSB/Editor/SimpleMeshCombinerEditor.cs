using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SimpleMeshCombiner))]
public class SimpleMeshCombinerEditor : Editor
{
    SimpleMeshCombiner combiner;

    private void OnEnable()
    {
        combiner = target as SimpleMeshCombiner;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Init system (Extended)"))
        {
            AdvancedMerge(combiner.filters, combiner.transform);
            var sbgpu = combiner.GetComponent<SolidBodyGPU>();
            sbgpu.anchorsMesh = AnchorMeshCreator.Create(combiner.filters, combiner.transform, combiner.qualityMeshSimp);
            EditorUtility.SetDirty(sbgpu);
        }
    }
    public void AdvancedMerge(MeshFilter[] filters, Transform transform)
    {
        List<Material> materials = new List<Material>();

        for (int i = 0; i < filters.Length; i++)
        {
            var renderer = filters[i].GetComponent<MeshRenderer>();
            if (renderer.transform == transform)
                continue;
            Material[] localMats = renderer.sharedMaterials;
            foreach (Material localMat in localMats)
                if (!materials.Contains(localMat))
                    materials.Add(localMat);
        }

        List<Mesh> submeshes = new List<Mesh>();
        foreach (Material material in materials)
        {
            List<CombineInstance> combiners = new List<CombineInstance>();
            foreach (MeshFilter filter in filters)
            {
                if (filter.transform == transform) continue;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    Debug.LogError(filter.name + " has no MeshRenderer");
                    continue;
                }

                Material[] localMaterials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < localMaterials.Length; materialIndex++)
                {
                    if (localMaterials[materialIndex] != material)
                        continue;

                    CombineInstance ci = new CombineInstance
                    {
                        mesh = filter.sharedMesh,
                        subMeshIndex = materialIndex,
                        transform = filter.transform.localToWorldMatrix,
                    };
                    combiners.Add(ci);
                }
            }

            Mesh mesh = new Mesh();
            mesh.CombineMeshes(combiners.ToArray(), true);
            submeshes.Add(mesh);
        }

        List<CombineInstance> finalCombiners = new List<CombineInstance>();
        foreach (Mesh mesh in submeshes)
        {

            CombineInstance ci = new CombineInstance
            {
                mesh = mesh,
                subMeshIndex = 0,
                transform = Matrix4x4.identity,
            };
            finalCombiners.Add(ci);
        }
        Mesh finalMesh = new Mesh();
        finalMesh.CombineMeshes(finalCombiners.ToArray(), false);
        finalMesh.RecalculateBounds();
        finalMesh.RecalculateNormals();
        finalMesh.RecalculateTangents();

        transform.GetComponent<MeshFilter>().sharedMesh = finalMesh;
        transform.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();


        Debug.Log("Final mesh has " + submeshes.Count + " materials.");
    }
}

public class AnchorMeshCreator
{
    public static Mesh Create(MeshFilter[] filters, Transform transform, float qualityLevel)
    {
        List<Material> materials = new List<Material>();
        int materialsCount = 0;

        for (int i = 0; i < filters.Length; i++)
        {
            var renderer = filters[i].GetComponent<MeshRenderer>();
            if (renderer.transform == transform)
                continue;
            Material[] localMats = renderer.sharedMaterials;
            foreach (Material localMat in localMats)
                if (!materials.Contains(localMat))
                {
                    materials.Add(localMat);
                    materialsCount++;
                }
        }

        List<Mesh> submeshes = new List<Mesh>();
        foreach (Material material in materials)
        {
            List<CombineInstance> combiners = new List<CombineInstance>();
            foreach (MeshFilter filter in filters)
            {
                if (filter.transform == transform) continue;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    Debug.LogError(filter.name + " has no MeshRenderer");
                    continue;
                }

                Material[] localMaterials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < localMaterials.Length; materialIndex++)
                {
                    if (localMaterials[materialIndex] != material)
                        continue;

                    CombineInstance ci = new CombineInstance
                    {
                        mesh = SimpifyMesh(qualityLevel, filter.sharedMesh),
                        subMeshIndex = materialIndex,
                        transform = filter.transform.localToWorldMatrix,
                    };
                    combiners.Add(ci);
                }
            }

            Mesh mesh = new Mesh();
            mesh.CombineMeshes(combiners.ToArray(), true);
            submeshes.Add(mesh);
        }

        List<CombineInstance> finalCombiners = new List<CombineInstance>();
        foreach (Mesh mesh in submeshes)
        {

            CombineInstance ci = new CombineInstance
            {
                mesh = mesh,
                subMeshIndex = 0,
                transform = Matrix4x4.identity,
            };
            finalCombiners.Add(ci);
        }
        Mesh finalMesh = new Mesh();
        finalMesh.CombineMeshes(finalCombiners.ToArray(), false);
        finalMesh.RecalculateBounds();
        finalMesh.RecalculateNormals();
        finalMesh.RecalculateTangents();
        return finalMesh;
    }

    static Mesh SimpifyMesh(float quality, Mesh mesh)
    {
        var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier();
        meshSimplifier.Initialize(mesh);
        meshSimplifier.SimplifyMesh(quality);
        return meshSimplifier.ToMesh();
    }
}
