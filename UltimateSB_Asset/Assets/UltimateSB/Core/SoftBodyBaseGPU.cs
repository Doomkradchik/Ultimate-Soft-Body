using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class SoftBodyBaseGPU : MonoBehaviour
{
    protected abstract float DampingTrusses {get;}

    bool HasPair(List<TrussData> trusses, int leftIndex, int rightIndex)
    {
        if (leftIndex == rightIndex) { return true; }

        foreach (var pair in trusses)
            if (pair.Validate(leftIndex, rightIndex)) { return true; }

        return false;
    }

    public float scaleMultiplier = 1;

    [Tooltip("Defines surface tension force. Best value 2.0 / 25.0")]
    [Range(0.05f, 0.9f)]
    public float surfaceTension = 0.5f;

    [HideInInspector] [SerializeField] private ComputeShader physicsComputeShader;

    public int maxCollisionsCount = 5;
    public int maxVerticesCount = 3000;
    public int maxTrianglesCount = 10000;

    public ContiniousDetectionKind continiousDetectionKind;


    [Header("Impulse collision properties")]
    public ImpulseDetectionKind impuseDetectionKind;

    [HideInInspector] [SerializeField] internal float impulseDamageMultiplier = 1;
    [HideInInspector] [SerializeField] internal float impulseMinVelocity = 0;
    [HideInInspector] [SerializeField] internal float radius = 1f;

    private int _trussesCount;
    private int _nodesCount;


    Mesh _mesh;

    ComputeBuffer _ROTrussesDataBuffer;
    ComputeBuffer _RONodeTInfoBuffer;
    ComputeBuffer _RONodeOtherDataBuffer;
    ComputeBuffer _RWTotalDForceBufer;
    ComputeBuffer _RWNodePositionsBuffer;
    ComputeBuffer _RWNodeVelocitiesBuffer;
    ComputeBuffer _RWTrussForceBuffer;
    ComputeBuffer _RWNodeStiffnessLengthTO;
    ComputeBuffer _otherDBuffer;
    ComputeBuffer _ICParamsConstBuffer;
    ComputeBuffer _ROCCDataBuffer;
    ComputeBuffer _ROCCCounterBuffer;
    ComputeBuffer _RWStiffnesPointsBuffer;
    ComputeBuffer _ROICDataONE;
    ComputeBuffer _ROMeshVertONE;
    ComputeBuffer _ROMeshTriONE;
    ComputeBuffer _ROTransfONE;
    ComputeBuffer _Diagnostics;




    MeshFilter m_MeshFilter;
    ConvexMeshCutter m_MeshCutter;

    int _maxTrussesConnectionInNode;

    Vector3[] _positions;
    ICData[] _icData;
    TransfE[] _trDataONE;
    CCData[] _ccDatas;

    void Awake()
    {
        _ccDatas = new CCData[maxCollisionsCount];
        _trDataONE = new TransfE[1];
        _icData = new ICData[1];

        m_MeshFilter = GetComponent<MeshFilter>();
        m_MeshCutter = GetComponent<ConvexMeshCutter>();

        _mesh = m_MeshFilter.mesh;

        var normals = _mesh.normals;
        var vertices = _mesh.vertices;
        var triangles = _mesh.triangles;

        _nodesCount = vertices.Length;

        var nodesOther = new NodeOtherData[_nodesCount];

        var listTrusses = new List<TrussData>();
        var trussNodeInfosCount = new int[_nodesCount];
        for (int i = 0; i < triangles.Length - 1; i++)
            if (HasPair(listTrusses, triangles[i], triangles[i + 1]) == false)
            {
                listTrusses.Add(
                    new TrussData
                    {
                        indexPair = new Vector2Int(triangles[i], triangles[i + 1]),
                        restLength = (vertices[triangles[i]] - vertices[triangles[i + 1]]).magnitude
                    });

                trussNodeInfosCount[triangles[i]]++;
                trussNodeInfosCount[triangles[i + 1]]++;
            }

        _trussesCount = listTrusses.Count;

        if (_trussesCount > MAX_TRUSSES)
        {
            Debug.LogError($"Noticed {_trussesCount} trusses. The number of trusses should be less than 20736. Use mesh with less triagles and vertices!");
            return;
        }

        var trusses = listTrusses.ToArray();
        _maxTrussesConnectionInNode = trussNodeInfosCount.Max();
        var trussNodeInfos = new TrussNodeInfo[_nodesCount * _maxTrussesConnectionInNode];
        var lastTrussNodeInfoIndex = new int[_nodesCount];


        for (int i = 0; i < _trussesCount; i++)
        {
            var idLeft = trusses[i].indexPair.x;
            var idRight = trusses[i].indexPair.y;

            trussNodeInfos[idLeft + _nodesCount * lastTrussNodeInfoIndex[idLeft]] = new TrussNodeInfo
            {
                trussID = i,
                right = -1
            };

            trussNodeInfos[idRight + _nodesCount * lastTrussNodeInfoIndex[idRight]] = new TrussNodeInfo
            {
                trussID = i,
                right = 1
            };

            lastTrussNodeInfoIndex[idLeft]++;
            lastTrussNodeInfoIndex[idRight]++;
        }

        for (int i = 0; i < _nodesCount; i++)
        {
            nodesOther[i] = new NodeOtherData
            {
                trussesConnected = trussNodeInfosCount[i],
                startPosition = vertices[i],
                mass = 0.1f,
                //weight = colors[i].a,
                normal = normals[i],
            };
        }

        InitializeBuffers();
        FillBuffers(trusses, vertices,
            nodesOther, trussNodeInfos);
        //m_MeshCutter.Init(_mesh.vertexCount);

        Debug.Log($"Body initialized successfully: {_nodesCount} nodes, {_trussesCount} trusses");
    }

    const int MAX_TRUSSES = 20736;
    void FixedUpdate() => GPUUpdate();


    void OnDestroy()
    {
        _ROTrussesDataBuffer.Release();
        _RONodeTInfoBuffer.Release();
        _RONodeOtherDataBuffer.Release();
        _RWTotalDForceBufer.Release();
        _RWNodePositionsBuffer.Release();
        _RWNodeVelocitiesBuffer.Release();
        _RWTrussForceBuffer.Release();
        _RWNodeStiffnessLengthTO.Release();
        _otherDBuffer.Release();
        _ICParamsConstBuffer.Release();
        _ROCCDataBuffer.Release();
        _ROCCCounterBuffer.Release();
        _RWStiffnesPointsBuffer.Release();
        _ROICDataONE.Release();

        _ROMeshVertONE.Release();
        _ROMeshTriONE.Release();
        _ROTransfONE.Release();
        _Diagnostics.Release();

        _ROTrussesDataBuffer.Dispose();
        _RONodeTInfoBuffer.Dispose();
        _RONodeOtherDataBuffer.Dispose();
        _RWTotalDForceBufer.Dispose();
        _RWNodePositionsBuffer.Dispose();
        _RWNodeVelocitiesBuffer.Dispose();
        _RWTrussForceBuffer.Dispose();
        _RWNodeStiffnessLengthTO.Dispose();
        _otherDBuffer.Dispose();
        _ICParamsConstBuffer.Dispose();
        _ROCCDataBuffer.Dispose();
        _ROCCCounterBuffer.Dispose();
        _RWStiffnesPointsBuffer.Dispose();
        _ROICDataONE.Dispose();

        _ROMeshVertONE.Dispose();
        _ROMeshTriONE.Dispose();
        _ROTransfONE.Dispose();
        _Diagnostics.Dispose();
    }


    int m_SimulateTrussKarnelID { get { return physicsComputeShader.FindKernel("SimulateTruss"); } }
    int m_HashTrussForcesID { get { return physicsComputeShader.FindKernel("HashTrussForces"); } }
    int m_SimulateNodeID { get { return physicsComputeShader.FindKernel("SimulateNode"); } }
    int m_RunCCID { get { return physicsComputeShader.FindKernel("RunCC"); } }
    int m_RunICID { get { return physicsComputeShader.FindKernel("RunIC"); } }
    int m_CCMeshCalcID { get { return physicsComputeShader.FindKernel("CCMeshCalc"); } }

    void Subscribe(Collider other)
    {
        if (other is MeshCollider mc)
            _ccmeshColliders.Add(mc);
        else
            _cccolliders.Add(other);
    }


    void Unsubscribe(Collider other)
    {
        if (other is MeshCollider mc)
            _ccmeshColliders.Remove(mc);
        else
            _cccolliders.Remove(other);
    }

    int[] _collisionsCount;
    int[] CollisionsCount(int value)
    {
        _collisionsCount[0] = value;
        return _collisionsCount;
    }

    const float AMPLITUDE = 5f;
    //const float T_STIFFNESS = 2f;
    //const float O_STIFFNESS = 20f;
    const float STIFFNESS = 20f;
    const float DAMPING = 1f;

    void FillBuffers(TrussData[] trusses,
        Vector3[] nodesPosition, NodeOtherData[] nodesOther, TrussNodeInfo[] trussNodeInfos)
    {
        _collisionsCount = new int[1];
        _positions = nodesPosition;
        _RWNodePositionsBuffer.SetData(nodesPosition);
        _RONodeOtherDataBuffer.SetData(nodesOther);
        _RONodeTInfoBuffer.SetData(trussNodeInfos);
        _ROTrussesDataBuffer.SetData(trusses);
        _RWStiffnesPointsBuffer.SetData(nodesPosition);

        var otherDParams = new OtherD[] {new OtherD {
            dTime = Time.deltaTime,
            maxAmplitude = AMPLITUDE * scaleMultiplier,
            stiffness = 2f * STIFFNESS * surfaceTension,
            stiffnessO = STIFFNESS,
            damping = DAMPING,
            nodesCount= _nodesCount,
            collisionType = (int)impuseDetectionKind,
        }};


        var icParamsArr = new ICParams[] { new ICParams{
            damageMultiplier = impulseDamageMultiplier,
            minVelocity = impulseMinVelocity,
            kind1radius = radius * scaleMultiplier,
        } };

        _ICParamsConstBuffer.SetData(icParamsArr);
        _otherDBuffer.SetData(otherDParams);
    }

    [System.Serializable]
    public enum ColliderType : int
    {
        Undefined,
        Sphere,
        Box,
        Mesh
    }

    void RecalculateCollisionData()
    {
        for (int i = 0; i < _cccolliders.Count; i++)
            _ccDatas[i] = RefreshCCollisionData(_cccolliders[i]);

        _ROCCDataBuffer.SetData(_ccDatas);
        _ROCCCounterBuffer.SetData(CollisionsCount(_cccolliders.Count));
    }

    CCData RefreshCCollisionData(Collider collider)
    {
        return new CCData
        {
            tre = CalculateTransformableData(collider),
            sphRad = collider is SphereCollider sp ? sp.radius
            * RAD_RATIO * scaleMultiplier * sp.transform.localScale.GetRAD() : 0f
        };
    }

    const float DAMAGE_RATIO = 0.2f;
    const float RAD_RATIO = 1.25f;


    void OnCollisionEnter(Collision collision)
    {
        _icData[0] = new ICData
        {
            cpoint = transform.InverseTransformPoint(collision.contacts[0].point),
            cvel = transform.InverseTransformDirection(collision.relativeVelocity * DAMAGE_RATIO)
        };

        if (impuseDetectionKind == ImpulseDetectionKind.Mesh)
        {
            if (collision.collider is MeshCollider mc)
                SetMeshDataProperty(mc);
            else return;
        }

        _ROICDataONE.SetData(_icData);
        physicsComputeShader.Dispatch(m_RunICID, Extensions.GetLength(_nodesCount, 256), 1, 1);
    }

    void OnTriggerEnter(Collider other) => Subscribe(other);
    void OnTriggerExit(Collider other) => Unsubscribe(other);

    List<Collider> _cccolliders = new List<Collider>();
    List<MeshCollider> _ccmeshColliders = new List<MeshCollider>();


    void RunCCMesh()
    {
        foreach (var mc in _ccmeshColliders)
        {
            SetMeshDataProperty(mc);
            physicsComputeShader.Dispatch(m_CCMeshCalcID, Extensions.GetLength(_nodesCount, 256), 1, 1);
        }
    }

    void SetMeshDataProperty(MeshCollider meshCollider)
    {
        _trDataONE[0] = CalculateTransformableData(meshCollider, meshCollider.sharedMesh.triangles.Length);
        _ROTransfONE.SetData(_trDataONE);
        _ROMeshVertONE.SetData(meshCollider.sharedMesh.vertices);
        _ROMeshTriONE.SetData(meshCollider.sharedMesh.triangles);
    }

    TransfE CalculateTransformableData(Collider collider, int triCount = 0)
    {
        return new TransfE
        {
            colliderType = (int)GetColliderType(collider, out var center),
            lpos = transform.InverseTransformPoint(collider.transform.TransformPoint(center)),
            lscale = collider.transform.localScale * .5f * scaleMultiplier,
            lrot = Quaternion.Inverse(transform.rotation) * collider.transform.rotation,
            trianglesCount = triCount,
        };
    }

    ColliderType GetColliderType(Collider collider, out Vector3 center)
    {
        center = Vector3.zero;
        if (collider is MeshCollider)
            return ColliderType.Mesh;
        if (collider is BoxCollider bc)
        {
            center = bc.center;
            return ColliderType.Box;
        }
        if (collider is SphereCollider sc)
        {
            center = sc.center;
            return ColliderType.Sphere;
        }

        return ColliderType.Undefined;
    }

    void InitializeBuffers()
    {
        _ROTrussesDataBuffer = new ComputeBuffer(_trussesCount, sizeof(int) * 2 + sizeof(float));
        _RONodeTInfoBuffer = new ComputeBuffer(_nodesCount * _maxTrussesConnectionInNode, sizeof(int) * 2);
        _RONodeOtherDataBuffer = new ComputeBuffer(_nodesCount, sizeof(float) * 8 + sizeof(int));

        _RWTotalDForceBufer = new ComputeBuffer(_nodesCount, sizeof(float) * 3);
        _RWNodePositionsBuffer = new ComputeBuffer(_nodesCount, sizeof(float) * 3);
        _RWNodeVelocitiesBuffer = new ComputeBuffer(_nodesCount, sizeof(float) * 3);
        _RWTrussForceBuffer = new ComputeBuffer(_trussesCount, sizeof(float) * 3);
        _RWNodeStiffnessLengthTO = new ComputeBuffer(_nodesCount, sizeof(float));

        _ROCCDataBuffer = new ComputeBuffer(maxCollisionsCount, sizeof(float) * 11 + sizeof(int) * 2);
        _ROCCCounterBuffer = new ComputeBuffer(1, sizeof(int));

        _ROICDataONE = new ComputeBuffer(1, sizeof(float) * 6);
        _ROMeshVertONE = new ComputeBuffer(maxVerticesCount, sizeof(float) * 3);
        _ROMeshTriONE = new ComputeBuffer(maxTrianglesCount * 3, sizeof(int));
        _ROTransfONE = new ComputeBuffer(1, sizeof(float) * 10 + sizeof(int) * 2);


        var cStride1 = sizeof(float) * 6 + sizeof(int) * 2;
        _otherDBuffer = new ComputeBuffer(1, cStride1, ComputeBufferType.Constant);

        var cStride2 = sizeof(float) * 3;
        _ICParamsConstBuffer = new ComputeBuffer(1, cStride2, ComputeBufferType.Constant);

        _Diagnostics = new ComputeBuffer(_nodesCount, sizeof(float) * 3);
        _RWStiffnesPointsBuffer = new ComputeBuffer(_nodesCount, sizeof(float) * 3);

        physicsComputeShader.SetBuffer(m_SimulateTrussKarnelID, "ROTrussesData", _ROTrussesDataBuffer);
        physicsComputeShader.SetBuffer(m_SimulateTrussKarnelID, "RWNodePositions", _RWNodePositionsBuffer);
        physicsComputeShader.SetBuffer(m_SimulateTrussKarnelID, "RWNodeVelocities", _RWNodeVelocitiesBuffer);
        physicsComputeShader.SetBuffer(m_SimulateTrussKarnelID, "RWTrussForce", _RWTrussForceBuffer);

        physicsComputeShader.SetBuffer(m_HashTrussForcesID, "RONodeOtherData", _RONodeOtherDataBuffer);
        physicsComputeShader.SetBuffer(m_HashTrussForcesID, "RONodeTInfo", _RONodeTInfoBuffer);
        physicsComputeShader.SetBuffer(m_HashTrussForcesID, "RWTrussForce", _RWTrussForceBuffer);
        physicsComputeShader.SetBuffer(m_HashTrussForcesID, "TotalDForce", _RWTotalDForceBufer);

        physicsComputeShader.SetBuffer(m_SimulateNodeID, "TotalDForce", _RWTotalDForceBufer);
        physicsComputeShader.SetBuffer(m_SimulateNodeID, "RWNodeVelocities", _RWNodeVelocitiesBuffer);
        physicsComputeShader.SetBuffer(m_SimulateNodeID, "RONodeOtherData", _RONodeOtherDataBuffer);
        physicsComputeShader.SetBuffer(m_SimulateNodeID, "RWNodePositions", _RWNodePositionsBuffer);
        physicsComputeShader.SetBuffer(m_SimulateNodeID, "RWNodeStiffnessLengthTO", _RWNodeStiffnessLengthTO);
        physicsComputeShader.SetBuffer(m_SimulateNodeID, "RWStiffnesPoints", _RWStiffnesPointsBuffer);

        physicsComputeShader.SetBuffer(m_RunCCID, "ROCCData", _ROCCDataBuffer);
        physicsComputeShader.SetBuffer(m_RunCCID, "ROCCCounter", _ROCCCounterBuffer);
        physicsComputeShader.SetBuffer(m_RunCCID, "RWNodePositions", _RWNodePositionsBuffer);
        physicsComputeShader.SetBuffer(m_RunCCID, "RWNodeVelocities", _RWNodeVelocitiesBuffer);

        physicsComputeShader.SetBuffer(m_RunICID, "RWNodePositions", _RWNodePositionsBuffer);
        physicsComputeShader.SetBuffer(m_RunICID, "RWNodeVelocities", _RWNodeVelocitiesBuffer);
        physicsComputeShader.SetBuffer(m_RunICID, "ROICDataONE", _ROICDataONE);
        physicsComputeShader.SetBuffer(m_RunICID, "RONodeOtherData", _RONodeOtherDataBuffer);
        physicsComputeShader.SetBuffer(m_RunICID, "ROMeshVertONE", _ROMeshVertONE);
        physicsComputeShader.SetBuffer(m_RunICID, "ROMeshTriONE", _ROMeshTriONE);
        physicsComputeShader.SetBuffer(m_RunICID, "ROTransfONE", _ROTransfONE);

        physicsComputeShader.SetBuffer(m_CCMeshCalcID, "RWNodePositions", _RWNodePositionsBuffer);
        physicsComputeShader.SetBuffer(m_CCMeshCalcID, "RWNodeVelocities", _RWNodeVelocitiesBuffer);
        physicsComputeShader.SetBuffer(m_CCMeshCalcID, "ROMeshVertONE", _ROMeshVertONE);
        physicsComputeShader.SetBuffer(m_CCMeshCalcID, "ROMeshTriONE", _ROMeshTriONE);
        physicsComputeShader.SetBuffer(m_CCMeshCalcID, "ROTransfONE", _ROTransfONE);


        physicsComputeShader.SetBuffer(m_RunCCID, "Diagnostics", _Diagnostics);

        physicsComputeShader.SetConstantBuffer(Shader.PropertyToID("SimulationParams"), _otherDBuffer, 0, cStride1);
        physicsComputeShader.SetConstantBuffer(Shader.PropertyToID("ICParams"), _ICParamsConstBuffer, 0, cStride2);
    }

    void GPUUpdate()
    {
        physicsComputeShader.Dispatch(m_SimulateTrussKarnelID, 18, 18, 1);
        physicsComputeShader.Dispatch(m_HashTrussForcesID, Extensions.GetLength(_nodesCount, 256), 1, 1);
        physicsComputeShader.Dispatch(m_SimulateNodeID, Extensions.GetLength(_nodesCount, 256), 1, 1);

        switch (continiousDetectionKind)
        {
            case ContiniousDetectionKind.None:
                break;
            case ContiniousDetectionKind.IgnoreMeshCollider:
                RecalculateCollisionData();
                physicsComputeShader.Dispatch(m_RunCCID, Extensions.GetLength(_nodesCount, 256), 1, 1);
                break;
            case ContiniousDetectionKind.Everything:
                RecalculateCollisionData();
                physicsComputeShader.Dispatch(m_RunCCID, Extensions.GetLength(_nodesCount, 256), 1, 1);
                RunCCMesh();
                break;
        }

        _RWNodePositionsBuffer.GetData(_positions);
     //   UpdateMesh();
        ///TEST
        ///
        if (Input.GetKeyDown(KeyCode.Space))
            m_MeshCutter.UpdateAllColliderVertices(_positions);
    }
}
