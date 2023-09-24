using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SimpleMeshCombiner))]
public class SimpleMeshCombinerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var combiner = target as SimpleMeshCombiner;
        if (GUILayout.Button("CombineMeshes"))
        {
            combiner.CombineMesh();
        }
    }
}
