using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UltimateSB.Lib;


//struct CollisionData
//{
//    public Vector3 normal;
//    public Vector3 firstContactPoint;
//}




public class CustomMeshCollider : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public Mesh mesh;

    Collider[] colliders;

    public ComputeShader physicsShader;

    Vector3[] _positions;
    ICData[] _icData;
    TransfE[] _trDataONE;
    CCData[] _ccDatas;


    public int maxCollisionCount;

    private void Awake()
    {
        _ccDatas = new CCData[maxCollisionCount];
        _trDataONE = new TransfE[1];
        _icData = new ICData[1];
        colliders = FindObjectsOfType<Collider>();
        potentialColliders = new List<Collider>();
    }

    List<Collider> potentialColliders;

    void CheckAABBCollisions()
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            var intersects = meshRenderer.bounds.Intersects(colliders[i].bounds);
            if (potentialColliders.Contains(colliders[i]))
            {
                if (intersects == false)
                    potentialColliders.Remove(colliders[i]);
            }
            else
                if (intersects)
                potentialColliders.Add(colliders[i]);
        }
    }

    [System.Serializable]
    public enum ColliderType : int
    {
        Undefined,
        Sphere,
        Box,
        Mesh
    }

    //void RecalculateCollisionData()
    //{
    //    for (int i = 0; i < potentialColliders.Count; i++)
    //    {
    //        _ccDatas[i] = RefreshCCollisionData(potentialColliders[i]);
    //    }
            

    //    _ROCCDataBuffer.SetData(_ccDatas);
    //    _ROCCCounterBuffer.SetData(CollisionsCount(_cccolliders.Count));
    //}

    //CCData RefreshCCollisionData(Collider collider)
    //{
    //    return new CCData
    //    {
    //        tre = CalculateTransformableData(collider),
    //        sphRad = collider is SphereCollider sp ? sp.radius
    //        * RAD_RATIO * scaleMultiplier * sp.transform.localScale.GetRAD() : 0f
    //    };
    //}



    //TransfE CalculateTransformableData(Collider collider, int triCount = 0)
    //{
    //    return new TransfE
    //    {
    //        colliderType = (int)GetColliderType(collider, out var center),
    //        lpos = transform.InverseTransformPoint(collider.transform.TransformPoint(center)),
    //        lscale = collider.transform.localScale * .5f * scaleMultiplier * COLL_AMPLITRUDE,
    //        lrot = Quaternion.Inverse(transform.rotation) * collider.transform.rotation,
    //        trianglesCount = triCount,
    //    };
    //}

    //void SetMeshDataProperty(MeshCollider meshCollider)
    //{
    //    _trDataONE[0] = CalculateTransformableData(meshCollider, meshCollider.sharedMesh.triangles.Length);
    //    _ROTransfONE.SetData(_trDataONE);
    //    _ROMeshVertONE.SetData(meshCollider.sharedMesh.vertices);
    //    _ROMeshTriONE.SetData(meshCollider.sharedMesh.triangles);
    //}

    //ColliderType GetColliderType(Collider collider, out Vector3 center)
    //{
    //    center = Vector3.zero;
    //    if (collider is MeshCollider)
    //        return ColliderType.Mesh;
    //    if (collider is BoxCollider bc)
    //    {
    //        center = bc.center;
    //        return ColliderType.Box;
    //    }
    //    if (collider is SphereCollider sc)
    //    {
    //        center = sc.center;
    //        return ColliderType.Sphere;
    //    }

    //    return ColliderType.Undefined;
    //}



    //private void FixedUpdate()
    //{
    //    CheckAABBCollisions();



    //}

    //[ContextMenu(nameof(SendAllAABBOverlaps))]
    //void SendAllAABBOverlaps()
    //{
    //    foreach (var collider in potentialColliders)
    //        Debug.Log($"{collider.name} intersected target AABB");
    //}



    //void CheckAllCollisions()
    //{
    //    foreach (var collider in potentialColliders)
            
    //}
    

    //bool Collide(Collider collider, )
    //{

    //}



    



}
