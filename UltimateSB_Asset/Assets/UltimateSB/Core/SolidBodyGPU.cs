using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UltimateSB.Lib;

[System.Serializable]
struct OtherFSB
{
    public float dTime;
    public float stiffnessT;
    public int nodesCount;
    public int collisionType;
}

[RequireComponent(typeof(ConvexMeshCutter))]
public class SolidBodyGPU : MonoBehaviour
{
    public float scaleMultiplier = 1;
    [Tooltip("Defines surface tension force. Best value 2.0 / 25.0")]
    [Range(0.05f, 0.9f)]
    public float surfaceTension = 0.5f;

    [SerializeField] private ComputeShader physicsComputeShader;

    public int maxCollisionsCount = 5;
    public int maxVerticesCount = 3000;
    public int maxTrianglesCount = 10000;

    public ContiniousDetectionKind continiousDetectionKind;


    const float COLL_AMPLITRUDE = 1.3f;

    [Header("Impulse collision properties")]
    public ImpulseDetectionKind impuseDetectionKind;

    [SerializeField] internal float impulseDamageMultiplier = 1;
    [SerializeField] internal float impulseMinVelocity = 0;
    [SerializeField] internal float radius = 1f;

    private int _nodesCount;
    [Range(0f, 1f)]
    public float syncColliderTime;
    [Header("Debug only")]
    public Mesh anchorsMesh;

    ComputeBuffer _RONodeOtherDataBuffer;
    ComputeBuffer _RWNodePositionsBuffer;
    ComputeBuffer _RWNodeVelocitiesBuffer;

    ComputeBuffer _otherDBuffer;
    ComputeBuffer _ICParamsConstBuffer;
    ComputeBuffer _ROCCDataBuffer;
    ComputeBuffer _ROCCCounterBuffer;
    ComputeBuffer _ROICDataONE;
    ComputeBuffer _ROMeshVertONE;
    ComputeBuffer _ROMeshTriONE;
    ComputeBuffer _ROTransfONE;


    ComputeBuffer _RWOriginNodes;
    ComputeBuffer _ROOriginOthers;

    ComputeBuffer OUTICMinDist;
    ComputeBuffer ICMeshData;


    MeshFilter m_MeshFilter;
    ConvexMeshCutter m_MeshCutter;


    ComputeBuffer Diagnostics;

    Rigidbody rigidbody;

    Vector3[] _positions;
    ICData[] _icData;
    TransfE[] _trDataONE;
    CCData[] _ccDatas;

    Vector3[] _originPos;

    Mesh _originMesh;

    MeshRenderer m_MeshRenderer;

    void Awake()
    {
        bounds = new Bounds();
        _ccDatas = new CCData[maxCollisionsCount];
        _trDataONE = new TransfE[1];
        _icData = new ICData[1];

        m_MeshFilter = GetComponent<MeshFilter>();
        m_MeshCutter = GetComponent<ConvexMeshCutter>();
        m_MeshRenderer = GetComponent<MeshRenderer>();
        rigidbody = GetComponent<Rigidbody>();


        _originMesh = m_MeshFilter.mesh;
        _originPos = _originMesh.vertices;
        _originCount = _originMesh.vertexCount;
        var originOthers = new NodeOtherData[_originCount];

        _nodesCount = anchorsMesh.vertexCount;
        var vertices = anchorsMesh.vertices;
        var nodesOther = new NodeOtherData[_nodesCount];
        anchorsMesh.RecalculateNormals();
        var normals = anchorsMesh.normals;


        for (int i = 0; i < _nodesCount; i++)
        {
            nodesOther[i] = new NodeOtherData
            {
                trussesConnected = 0,
                startPosition = vertices[i],
                mass = 0.1f,
                weight = 0f,
                normal = normals[i],
            };
        }

        for (int i = 0; i < _originMesh.vertexCount; i++)
        {
            originOthers[i] = new NodeOtherData
            {
                trussesConnected = 0,
                startPosition = _originMesh.vertices[i],
                mass = 0.1f,
                weight = 0f,
                normal = _originMesh.normals[i],
            };
        }



        velocities = new Vector3[_nodesCount];


        m_MeshCutter.Init(_nodesCount);
        InitializeBuffers();
        FillBuffers(vertices, nodesOther, originOthers, velocities);

        Debug.Log($"Body initialized successfully: {_originCount} nodes");
    }

    //void FixedUpdate() => GPUUpdate();


    void OnDestroy()
    {
        _RONodeOtherDataBuffer.Release();
        _RWNodePositionsBuffer.Release();
        _RWNodeVelocitiesBuffer.Release();
        _otherDBuffer.Release();
        _ICParamsConstBuffer.Release();
        _ROCCDataBuffer.Release();
        _ROCCCounterBuffer.Release();
        _ROICDataONE.Release();

        _ROMeshVertONE.Release();
        _ROMeshTriONE.Release();
        _ROTransfONE.Release();

        _RONodeOtherDataBuffer.Dispose();
        _RWNodePositionsBuffer.Dispose();
        _RWNodeVelocitiesBuffer.Dispose();
        _otherDBuffer.Dispose();
        _ICParamsConstBuffer.Dispose();
        _ROCCDataBuffer.Dispose();
        _ROCCCounterBuffer.Dispose();
        _ROICDataONE.Dispose();

        _ROMeshVertONE.Dispose();
        _ROMeshTriONE.Dispose();
        _ROTransfONE.Dispose();
    }

    int m_NodeInterpolation { get { return physicsComputeShader.FindKernel("NodeInter"); } }
    int m_FSBSimulateNodeID { get { return physicsComputeShader.FindKernel("FSBSimulateNode"); } }
    int m_RunCCID { get { return physicsComputeShader.FindKernel("RunCC"); } }
    int m_RunICID { get { return physicsComputeShader.FindKernel("RunIC"); } }
    int m_CCMeshCalcID { get { return physicsComputeShader.FindKernel("CCMeshCalc"); } } //NodeInter

    int m_ICMeshProject { get { return physicsComputeShader.FindKernel("ICMeshProject"); } }
    int m_ICMeshVelOverride { get { return physicsComputeShader.FindKernel("ICMeshOverrideVel"); } } //NodeInter

    int m_FindMinDist { get { return physicsComputeShader.FindKernel("FindMinDist"); } }

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

    const float STIFFNESS = 20f;

    void FillBuffers(Vector3[] nodesPosition, NodeOtherData[] nodeOtherDatas, NodeOtherData[] originOthers, Vector3[] velocities)
    {
        _collisionsCount = new int[1];
        _positions = nodesPosition;
        _ROOriginOthers.SetData(originOthers);
        _RWOriginNodes.SetData(_originPos);
        _RONodeOtherDataBuffer.SetData(nodeOtherDatas);
        _RWNodePositionsBuffer.SetData(nodesPosition);
        _RWNodeVelocitiesBuffer.SetData(velocities);

        var otherDParams = new OtherD[] {new OtherD {
            dTime = Time.deltaTime,
            stiffness = 2f * STIFFNESS * surfaceTension,
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

    //void OnCollisionEnter(Collision collision)
    //{

    //    if (collision.relativeVelocity.magnitude < impulseMinVelocity) { return; }

    //    _icData[0] = new ICData
    //    {
    //        cpoint = transform.InverseTransformPoint(collision.contacts[0].point),
    //        cvel = transform.InverseTransformDirection(collision.relativeVelocity * DAMAGE_RATIO)
    //    };

    //    if (impuseDetectionKind == ImpulseDetectionKind.Mesh)
    //    {
    //        if (collision.collider is MeshCollider mc)
    //            SetMeshDataProperty(mc);
    //        else return;
    //    }

    //    _ROICDataONE.SetData(_icData);
    //    physicsComputeShader.Dispatch(m_RunICID, Extensions.GetLength(_nodesCount, 256), 1, 1);
    //    ICRun = true;
    //}


    [ContextMenu("TestCalculation")]
    void StartRoutine()
    {
        StartCoroutine(LaunchICPredicitionRoutine());
    }


    void OnTriggerExit(Collider other) => Unsubscribe(other);

    List<Collider> _cccolliders = new List<Collider>();
    List<MeshCollider> _ccmeshColliders = new List<MeshCollider>();


    //private void OnDrawGizmos()
    //{
    //    Gizmos.DrawCube(bounds.center, bounds.size);
    //}

    Bounds bounds;

    IEnumerator LaunchICPredicitionRoutine()
    {
        var velocity = rigidbody.velocity;
        if (velocity.magnitude < impulseMinVelocity  || impuseDetectionKind != ImpulseDetectionKind.Mesh) { yield break; }

        //bounds = GetComponent<MeshRenderer>().bounds;
        bounds = new Bounds(m_MeshRenderer.bounds.center, m_MeshRenderer.bounds.size);
        var relativeVel = transform.InverseTransformDirection(velocity);
        bounds.Encapsulate(velocity * 100f);

        var pair = FindClosestMeshBoundColliderInDirection(velocity.normalized,
            Physics.OverlapBox(bounds.center, bounds.extents));

        if (pair.Item1 == null) { yield break; }
       
        _icData[0] = new ICData
        {
            cpoint = Vector3.zero,
            cvel = relativeVel * -1f * DAMAGE_RATIO
        };

        Debug.Log(pair.Item2 / velocity.magnitude);

        var time = (pair.Item2 / velocity.magnitude);
        if (time < 0.6f) { yield break; }

        yield return new WaitForSeconds(time - 0.1f);
        SetMeshDataProperty(pair.Item1);
        _ROICDataONE.SetData(_icData);
        physicsComputeShader.Dispatch(m_ICMeshProject, Extensions.GetLength(_nodesCount, 256), 1, 1);
        physicsComputeShader.Dispatch(m_FindMinDist, 1, 1, 1);
        physicsComputeShader.Dispatch(m_ICMeshVelOverride, Extensions.GetLength(_nodesCount, 256), 1, 1);

        yield return null;
        yield return null;

        _RWNodeVelocitiesBuffer.GetData(velocities);
        m_MeshCutter.DestinateCollidersOverPrediction(_positions, velocities);

        while (true)
        {
            physicsComputeShader.Dispatch(m_FSBSimulateNodeID, Extensions.GetLength(_nodesCount, 256), 1, 1);
            physicsComputeShader.Dispatch(m_NodeInterpolation, Extensions.GetLength(_originCount, 256), 1, 1);
            yield return new WaitForFixedUpdate();

            _RWNodePositionsBuffer.GetData(_positions);
            _RWOriginNodes.GetData(_originPos);
            UpdateMesh(_originPos, _originMesh);
        }
    }


    (MeshCollider, float) FindClosestMeshBoundColliderInDirection(Vector3 direction, Collider[] colliders)
    {
        float minDist = float.MaxValue;
        MeshCollider targetCollider = null;
        if (colliders == null) { return (null, 0); }
        foreach (var collider in colliders)
        {
            if (collider is not MeshCollider mc || collider.GetComponentInParent<SolidBodyGPU>() != null) { continue; }
            var dist = Vector3.Distance(transform.TransformPoint(_originMesh.bounds.ClosestPoint(direction * 100f)),
                mc.ClosestPointOnBounds(-100f * direction));

            if (dist < minDist)
            {
                minDist = dist;
                targetCollider = mc;
            }
        }

        return (targetCollider, minDist);
    }

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
            lscale = collider.transform.localScale * .5f * scaleMultiplier * COLL_AMPLITRUDE, 
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

    int _originCount = 0;

    void InitializeBuffers()
    {
        _RONodeOtherDataBuffer = new ComputeBuffer(_nodesCount, sizeof(float) * 8 + sizeof(int));
        _RWNodePositionsBuffer = new ComputeBuffer(_nodesCount, sizeof(float) * 3);
        _RWNodeVelocitiesBuffer = new ComputeBuffer(_nodesCount, sizeof(float) * 3);


        _RWOriginNodes = new ComputeBuffer(_originCount, sizeof(float) * 3);
        _ROOriginOthers = new ComputeBuffer(_originCount, sizeof(float) * 8 + sizeof(int));

        _ROCCDataBuffer = new ComputeBuffer(maxCollisionsCount, sizeof(float) * 11 + sizeof(int) * 2);
        _ROCCCounterBuffer = new ComputeBuffer(1, sizeof(int));

        _ROICDataONE = new ComputeBuffer(1, sizeof(float) * 6);
        _ROMeshVertONE = new ComputeBuffer(maxVerticesCount, sizeof(float) * 3);
        _ROMeshTriONE = new ComputeBuffer(maxTrianglesCount * 3, sizeof(int));
        _ROTransfONE = new ComputeBuffer(1, sizeof(float) * 10 + sizeof(int) * 2);
        Diagnostics = new ComputeBuffer(_nodesCount, sizeof(float));

        var cStride1 = sizeof(float) * 6 + sizeof(int) * 2;
        _otherDBuffer = new ComputeBuffer(1, cStride1, ComputeBufferType.Constant);

        var cStride2 = sizeof(float) * 3;
        _ICParamsConstBuffer = new ComputeBuffer(1, cStride2, ComputeBufferType.Constant);

        ICMeshData = new ComputeBuffer(_nodesCount, sizeof(int) + sizeof(float) * 4);
        OUTICMinDist = new ComputeBuffer(1, sizeof(float));

        physicsComputeShader.SetBuffer(m_FSBSimulateNodeID, "RWNodeVelocities", _RWNodeVelocitiesBuffer);
        physicsComputeShader.SetBuffer(m_FSBSimulateNodeID, "RWNodePositions", _RWNodePositionsBuffer);
        physicsComputeShader.SetBuffer(m_FSBSimulateNodeID, "RONodeOtherData", _RONodeOtherDataBuffer);

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
        physicsComputeShader.SetBuffer(m_RunICID, "Diagnostics", Diagnostics);

        physicsComputeShader.SetBuffer(m_CCMeshCalcID, "RWNodePositions", _RWNodePositionsBuffer);
        physicsComputeShader.SetBuffer(m_CCMeshCalcID, "RWNodeVelocities", _RWNodeVelocitiesBuffer);
        physicsComputeShader.SetBuffer(m_CCMeshCalcID, "ROMeshVertONE", _ROMeshVertONE);
        physicsComputeShader.SetBuffer(m_CCMeshCalcID, "ROMeshTriONE", _ROMeshTriONE);
        physicsComputeShader.SetBuffer(m_CCMeshCalcID, "ROTransfONE", _ROTransfONE);


        physicsComputeShader.SetBuffer(m_NodeInterpolation, "RWNodePositions", _RWNodePositionsBuffer);
        physicsComputeShader.SetBuffer(m_NodeInterpolation, "RONodeOtherData", _RONodeOtherDataBuffer);
        physicsComputeShader.SetBuffer(m_NodeInterpolation, "RWOriginVertices", _RWOriginNodes);
        physicsComputeShader.SetBuffer(m_NodeInterpolation, "ROOriginOther", _ROOriginOthers);

        physicsComputeShader.SetBuffer(m_ICMeshProject, "ICMeshData", ICMeshData);
        physicsComputeShader.SetBuffer(m_ICMeshProject, "RWNodePositions", _RWNodePositionsBuffer);
        physicsComputeShader.SetBuffer(m_ICMeshProject, "ROICDataONE", _ROICDataONE);
        physicsComputeShader.SetBuffer(m_ICMeshProject, "ROMeshVertONE", _ROMeshVertONE);
        physicsComputeShader.SetBuffer(m_ICMeshProject, "ROMeshTriONE", _ROMeshTriONE);
        physicsComputeShader.SetBuffer(m_ICMeshProject, "ROTransfONE", _ROTransfONE);

        physicsComputeShader.SetBuffer(m_ICMeshVelOverride, "OUTICMinDist", OUTICMinDist);
        physicsComputeShader.SetBuffer(m_ICMeshVelOverride, "ICMeshData", ICMeshData);
        physicsComputeShader.SetBuffer(m_ICMeshVelOverride, "ROICDataONE", _ROICDataONE);
        physicsComputeShader.SetBuffer(m_ICMeshVelOverride, "RWNodeVelocities", _RWNodeVelocitiesBuffer);
        physicsComputeShader.SetBuffer(m_ICMeshVelOverride, "Diagnostics", Diagnostics);


        physicsComputeShader.SetBuffer(m_FindMinDist, "OUTICMinDist", OUTICMinDist);
        physicsComputeShader.SetBuffer(m_FindMinDist, "ICMeshData", ICMeshData);

        physicsComputeShader.SetConstantBuffer(Shader.PropertyToID("SimulationParams"), _otherDBuffer, 0, cStride1);
        physicsComputeShader.SetConstantBuffer(Shader.PropertyToID("ICParams"), _ICParamsConstBuffer, 0, cStride2);
    }

    bool ICRun = false;
    Vector3[] velocities;
    float[] temp;
    void GPUUpdate()
    {
      //  if (ICRun == false) return;

        //_RWNodePositionsBuffer.GetData(_positions);
        //_RWOriginNodes.GetData(_originPos);

        //if (ICRun)
        //{
        //    _RWNodeVelocitiesBuffer.GetData(velocities);
        //    m_MeshCutter.DestinateCollidersOverPrediction(_positions, velocities);
        //    ICRun = false;
        //}


        //physicsComputeShader.Dispatch(m_FSBSimulateNodeID, Extensions.GetLength(_nodesCount, 256), 1, 1);
        //physicsComputeShader.Dispatch(m_NodeInterpolation, Extensions.GetLength(_originCount, 256), 1, 1);

        //switch (continiousDetectionKind)
        //{
        //    case ContiniousDetectionKind.None:
        //        break;
        //    case ContiniousDetectionKind.IgnoreMeshCollider:
        //        //TryUpdateColliders(_cccolliders.Count != 0);
        //        RecalculateCollisionData();
        //        physicsComputeShader.Dispatch(m_RunCCID, Extensions.GetLength(_nodesCount, 256), 1, 1);
        //        break;
        //    case ContiniousDetectionKind.Everything:
        //        //TryUpdateColliders(_cccolliders.Count != 0 || _ccmeshColliders.Count != 0);
        //        RecalculateCollisionData();
        //        physicsComputeShader.Dispatch(m_RunCCID, Extensions.GetLength(_nodesCount, 256), 1, 1);
        //        RunCCMesh();
        //        break;
        //}

        //UpdateMesh(_originPos, _originMesh);
    }

    [ContextMenu("DEBUG")]
    void DebugG(float[] temp)
    {
        for (int i = 0; i < _nodesCount; i++)
        {
            Debug.Log(temp[i]);
        }
    }


    Coroutine _updateColliderRoutine;

    IEnumerator UpdateCollidersRoutine()
    {
        while (true)
        {
            m_MeshCutter.DestinateColliders(_positions, Vector3.zero);
            yield return new WaitForSeconds(syncColliderTime);
        }
    }

    void TryUpdateColliders(bool update)
    {
        if (update && _updateColliderRoutine == null)
            _updateColliderRoutine = StartCoroutine(UpdateCollidersRoutine());

        else if (_updateColliderRoutine != null)
        {
            StopCoroutine(_updateColliderRoutine);
            _updateColliderRoutine = null;
        }
    }

    void UpdateMesh(Vector3[] verts, Mesh mesh)
    {
        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        m_MeshFilter.mesh = mesh;
    }
}
