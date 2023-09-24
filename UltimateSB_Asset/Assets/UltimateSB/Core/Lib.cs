using UnityEngine;

[System.Serializable]
public enum ImpulseDetectionKind : int
{
    Sphere,
    Mesh
}

[System.Serializable]
public enum ContiniousDetectionKind
{
    None,
    IgnoreMeshCollider,
    Everything
}

[System.Serializable]
public struct TransfE
{
    public Vector3 lpos;
    public Vector3 lscale;
    public Quaternion lrot;
    public int trianglesCount;
    public int colliderType;
}

[System.Serializable]
public struct ICData
{
    public Vector3 cvel;
    public Vector3 cpoint;
}


[System.Serializable]
public struct TrussData
{
    public float restLength;
    public Vector2Int indexPair;

    public bool Validate(int i1, int i2)
    {
        return (i1 == indexPair.x && i2 == indexPair.y) ||
              (i1 == indexPair.y && i2 == indexPair.x);
    }
}


[System.Serializable]
struct CCData
{
    public TransfE tre;
    public float sphRad;
}

[System.Serializable]
struct TrussNodeInfo
{
    public int trussID;
    public int right;
}

[System.Serializable]
struct NodeOtherData
{
    public int trussesConnected;
    public float mass;
    public Vector3 normal;
    public Vector3 startPosition;
    public float weight;
}
[System.Serializable]
struct ICParams
{
    public float damageMultiplier;
    public float minVelocity;
    public float kind1radius;
}

[System.Serializable]
struct OtherD
{
    public float dTime;
    public float maxAmplitude;
    public float stiffness;
    public float stiffnessO;
    public float damping;
    public int nodesCount;
    public int collisionType;
    public float dampingT;
}