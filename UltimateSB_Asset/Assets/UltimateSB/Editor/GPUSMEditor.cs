using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#if UNITY_EDITOR

[CustomEditor(typeof(SoftBodyGPU))]
public class GPUSMEditor : Editor
{
    SerializedProperty icKindProp,
        radiusProp,
        physicsComputeShaderProp,
        impulseDamageMultiplierProp,
        impulseMinVelocityProp
        ;


    private void OnEnable()
    {
        icKindProp = serializedObject.FindProperty("impuseDetectionKind");
        radiusProp = serializedObject.FindProperty("radius");
        physicsComputeShaderProp = serializedObject.FindProperty("physicsComputeShader");
        impulseDamageMultiplierProp = serializedObject.FindProperty("impulseDamageMultiplier");
        impulseMinVelocityProp = serializedObject.FindProperty("impulseMinVelocity");

        smgOld = (SoftBodyGPU)target;
        transform = smgOld.transform;
        meshFilter = transform.GetComponent<MeshFilter>();
        renderer = transform.GetComponent<Renderer>();

        editMat = new Material[] { new Material((Shader)GetAssetByName("EditorWeightShader")) };
        editMat[0].name = "edit";


        if(transform.Find("trigger") == null)
        {
            GameObject gameObject = new GameObject();
            gameObject.transform.parent = transform;
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
            gameObject.name = "trigger";
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            gameObject.AddComponent<BoxCollider>().isTrigger = false;
        }
    }

    Material[] editMat;
    SoftBodyGPU smgOld;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (meshFilter.sharedMesh.colors == null || meshFilter.sharedMesh.colors.Length == 0)
        {
            meshFilter.sharedMesh.colors = new Color[meshFilter.sharedMesh.vertexCount];
            EditorUtility.SetDirty(meshFilter);
        }

        physicsComputeShaderProp = serializedObject.FindProperty("physicsComputeShader");
        physicsComputeShaderProp.objectReferenceValue = GetAssetByName("ComputeSB");
        var icKindStatus = (SoftBodyGPU.ImpulseDetectionKind)icKindProp.enumValueIndex;

        if (icKindStatus == SoftBodyGPU.ImpulseDetectionKind.Sphere)
        {
            EditorGUILayout.PropertyField(radiusProp, new GUIContent("Radius"));
            EditorGUILayout.Space();
        }
            
        EditorGUILayout.PropertyField(impulseDamageMultiplierProp, new GUIContent("Damage Multiplier"));
        EditorGUILayout.PropertyField(impulseMinVelocityProp, new GUIContent("Min Velocity"));

        AssetDatabase.SaveAssets();
        EditorGUILayout.Space();


        if (!isEditingWeights)
        {
            if (GUILayout.Button("Edit Weight"))
            {
                smgOld.materials = renderer.sharedMaterials;
                renderer.sharedMaterials = editMat;
                Tools.current = Tool.None;
            }
        }
        else
        {
            EditorGUILayout.LabelField("Edit Weight Mode");
            brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 0.01f, 100f);
            brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0f, 1f);

            EditorGUILayout.LabelField("0 = fully plastic | 1 = fully elastic");
            weight = EditorGUILayout.Slider("Elastisity", weight, 0f, 1f);

            if (GUILayout.Button("Set to all vertices"))
            {
                var colors = meshFilter.sharedMesh.colors;
                for (int i = 0; i < meshFilter.sharedMesh.vertexCount; i++)
                    colors[i] = new Color(0, 0, 0, weight);
                meshFilter.sharedMesh.colors = colors;
            }

            if (GUILayout.Button("Stop Editing"))
            {
                renderer.sharedMaterials = smgOld.materials;
                Tools.current = Tool.Move;
                EditorUtility.SetDirty(meshFilter);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    //void RecalculateColors(MeshFilter meshFilter, Color[] colors)
    //{
    //    if (PrefabUtility.IsPartOfAnyPrefab(meshFilter.gameObject))
    //    {
    //        Mesh clonedMesh = Instantiate(targetMesh);
    //        clonedMesh.colors = colors;
    //        targetMesh = clonedMesh;
    //        PrefabUtility.RecordPrefabInstancePropertyModifications(meshFilter);

    //        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    //    }
    //}

    private float brushSize = 0.1f;
    private float brushStrength = 0.5f;
    float weight = 0.5f;

    bool isEditingWeights => renderer.sharedMaterial != null && renderer.sharedMaterial.name.Equals("edit");


    private void OnSceneGUI()
    {
        if (isEditingWeights)
        {
            Event e = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.type)
            {
                case EventType.MouseDown:
                case EventType.MouseDrag:
                    if (e.button == 0)
                    {
                        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                        RaycastHit hit;

                        if (Physics.Raycast(ray, out hit))
                        {
                            EditWeights(hit.point);
                            SceneView.RepaintAll();
                        }

                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (e.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }
    }

    Transform transform;
    MeshFilter meshFilter;
    Renderer renderer;

    private void EditWeights(Vector3 position)
    {
        var colors = meshFilter.sharedMesh.colors;

        for (int i = 0; i < meshFilter.sharedMesh.vertexCount; i++)
        {
            Vector3 vertexPosition = transform.TransformPoint(meshFilter.sharedMesh.vertices[i]);
            float distance = Vector3.Distance(vertexPosition, position);
            if (distance <= brushSize)
            {
                float normalizedDistance = 1f - (distance / brushSize);
                var w = Mathf.Lerp(meshFilter.sharedMesh.colors[i].a, weight, normalizedDistance * brushStrength);
                colors[i] = new Color(0, 0, 0, w);
            }
        }

        meshFilter.sharedMesh.colors = colors;
    }

    private Object GetAssetByName(string assetName)
    {
        string[] assetGuids = AssetDatabase.FindAssets(assetName);
        if (assetGuids.Length > 0)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[0]);
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            return asset;
        }
        return null;
    }
}

#endif