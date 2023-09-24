using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityMeshSimplifier;

#if UNITY_EDITOR

[CustomEditor(typeof(ConvexMeshCutter))]
public class ConvexMeshCutterEditor : Editor
{
    private ConvexMeshCutter convexMeshCutter;
    private GameObject ConvexPool
    {
        get
        {
            var cp = convexMeshCutter.transform.Find("convexPool");
            if(cp != null)
                DestroyImmediate(cp.gameObject);

            GameObject gameObject = new GameObject();
            gameObject.transform.parent = convexMeshCutter.transform;
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
            gameObject.name = "convexPool";
            return gameObject;
        }
    }

    private void OnEnable()
    {
        convexMeshCutter = target as ConvexMeshCutter;
        serializedObject.FindProperty("vertexConverter").objectReferenceValue = AssetProvider.GetAssetByName("VertexConverter");
        serializedObject.ApplyModifiedProperties();
    }


    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Cut Into Colliders"))
        {
            var mesh = SimpifyMesh(serializedObject.FindProperty("qualityMeshSimp").floatValue, convexMeshCutter.GetComponent<MeshFilter>().sharedMesh);
            var segments = CutToSegments(mesh, convexMeshCutter.cuts);
            InitColliders(segments, mesh.vertices);
            convexMeshCutter.target = mesh;
            EditorUtility.SetDirty(target);
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"Combined {mesh.vertexCount} vertices to convex meshes");
        }

    }

    private void InitColliders(List<int[]> segments, Vector3[] vertices)
    {
        var poolGM = ConvexPool;
        convexMeshCutter.convexPairs = new ConvexDataPair[segments.Count];
        for (int i = 0; i < segments.Count; i++)
        {
            var collider = poolGM.AddComponent<MeshCollider>();
            collider.convex = true;
            var instanceMesh = new Mesh();
            instanceMesh.vertices = ConvertToVertices(segments[i], vertices);
            collider.sharedMesh = instanceMesh;
            convexMeshCutter.convexPairs[i] = new ConvexDataPair { 
            meshCollider = collider,
            indexes = segments[i]
            };
        }
    }

    Vector3[] ConvertToVertices(int[] indexes, Vector3[] vertices)
    {
        var res = new List<Vector3>();
        for (int i = 0; i < indexes.Length; i++)
        {
            res.Add(vertices[indexes[i]]);
        }
        return res.ToArray();
    }

    void Append(Dictionary<Vector3Int, List<int>> pairs, Vector3Int c, int i)
    {
        if (pairs.ContainsKey(c) == false)
        {
            pairs.Add(c, new List<int> { i });
            return;
        }

        if (pairs[c].Contains(i) == false)
            pairs[c].Add(i);
           
    }

    List<int[]> CutToSegments(Mesh target, Vector3Int cuts)
    {
        if (target is null)
            return null;
        target.RecalculateBounds();
        var voxelSize = target.bounds.size.DivideBy(cuts + Vector3Int.one);
        var cutVertices = new Dictionary<Vector3Int, List<int>>();
        var result = new List<Mesh>();

        for (int i = 0; i < target.triangles.Length - 2; i += 3)
        {
            var c1 = CalculateCoord(target.vertices[target.triangles[i]] + target.bounds.extents, voxelSize, cuts);
            var c2 = CalculateCoord(target.vertices[target.triangles[i + 1]] + target.bounds.extents, voxelSize, cuts);
            var c3 = CalculateCoord(target.vertices[target.triangles[i + 2]] + target.bounds.extents, voxelSize, cuts);

            Append(cutVertices, c1, target.triangles[i]);
            Append(cutVertices, c1, target.triangles[i]);
            Append(cutVertices, c1, target.triangles[i]);

            if (c2 != c1)
            {
                Append(cutVertices, c2, target.triangles[i + 1]);
                Append(cutVertices, c2, target.triangles[i + 1]);
                Append(cutVertices, c2, target.triangles[i + 1]);
            }

            if (c3 != c1 && c3 != c2)
            {
                Append(cutVertices, c3, target.triangles[i + 2]);
                Append(cutVertices, c3, target.triangles[i + 2]);
                Append(cutVertices, c3, target.triangles[i + 2]);
            }
        }

        List<int[]> res = new List<int[]>();
        foreach (var pair in cutVertices)
        {
            if (pair.Value.Count < 3) { continue; }
            res.Add(pair.Value.ToArray());
        }

        return res;
    }

    Mesh SimpifyMesh(float quality, Mesh mesh)
    {
        var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier();
        meshSimplifier.Initialize(mesh);
        meshSimplifier.SimplifyMesh(quality);
        return meshSimplifier.ToMesh();
    }

    public Vector3Int CalculateCoord(Vector3 pos, Vector3 size, Vector3Int cuts)
    {
        int x = pos.x <= size.x ? 0 : pos.x > size.x * cuts.x ? cuts.x : (int)(pos.x / size.x);
        int y = pos.y <= size.y ? 0 : pos.y > size.y * cuts.y ? cuts.y : (int)(pos.y / size.y);
        int z = pos.z <= size.z ? 0 : pos.z > size.z * cuts.z ? cuts.z : (int)(pos.z / size.z);

        return new Vector3Int(x, y, z);
    }
}

#endif