using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

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
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if(GUILayout.Button("Cut Into Colliders"))
        {
            var segments = CutToSegments(convexMeshCutter.GetComponent<MeshFilter>().sharedMesh, convexMeshCutter.cuts);
            InitColliders(segments);
        }
    }

    private void InitColliders(Mesh[] segments)
    {
        var poolGM = ConvexPool;

        for (int i = 0; i < segments.Length; i++)
        {
            var collider = poolGM.AddComponent<MeshCollider>();
            collider.convex = true;
            collider.sharedMesh = segments[i];
        }
    }

    void Append(Dictionary<Vector3Int, List<Vector3>> pairs, Vector3Int c, Vector3 pos)
    {
        if (pairs.ContainsKey(c))
            pairs[c].Add(pos);
        else
            pairs.Add(c, new List<Vector3> { pos });
    }

    Mesh[] CutToSegments(Mesh target, Vector3Int cuts)
    {
        if (target is null)
            return null;
        target.RecalculateBounds();
        var voxelSize = target.bounds.size.DivideBy(cuts + Vector3Int.one);
        var cutVertices = new Dictionary<Vector3Int, List<Vector3>>();
        var result = new List<Mesh>();

        for (int i = 0; i < target.triangles.Length - 2; i += 3)
        {
            var c1 = CalculateCoord(target.vertices[target.triangles[i]] + target.bounds.extents, voxelSize, cuts);
            var c2 = CalculateCoord(target.vertices[target.triangles[i + 1]] + target.bounds.extents, voxelSize, cuts);
            var c3 = CalculateCoord(target.vertices[target.triangles[i + 2]] + target.bounds.extents, voxelSize, cuts);

            Append(cutVertices, c1, target.vertices[target.triangles[i]]);
            Append(cutVertices, c1, target.vertices[target.triangles[i + 1]]);
            Append(cutVertices, c1, target.vertices[target.triangles[i + 2]]);

            if (c2 != c1)
            {
                Append(cutVertices, c2, target.vertices[target.triangles[i]]);
                Append(cutVertices, c2, target.vertices[target.triangles[i + 1]]);
                Append(cutVertices, c2, target.vertices[target.triangles[i + 2]]);
            }

            if (c3 != c1 && c3 != c2)
            {
                Append(cutVertices, c3, target.vertices[target.triangles[i]]);
                Append(cutVertices, c3, target.vertices[target.triangles[i + 1]]);
                Append(cutVertices, c3, target.vertices[target.triangles[i + 2]]);
            }
        }

        foreach (var pair in cutVertices)
        {
            if (pair.Value.Count < 3) { continue; }
            var instanceMesh = new Mesh();
            instanceMesh.vertices = pair.Value.ToArray();
            result.Add(instanceMesh);
        }

        return result.ToArray();
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