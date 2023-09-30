
struct TransfE
{
    float3 lpos;
    float3 lscale;
    float4 lrot;
    int trianglesCount;
    int colliderType;
};

struct CCData
{
    TransfE tre;
    float sphRad;
};

struct RD
{
    float3 n;
    int itrs;
    float dist;
};

struct PointerForce
{
    int CE_index;
    float3 origin;
    float3 vect;
};

struct TrussData
{
    float restLength;
    int2 indexPair;
};

struct TrussNodeInfo
{
    int trussID;
    int right;
};

struct CCOUT
{
    bool coll;
    float3 t;
    float ml;
};

struct ICOUT{
    bool det;
    float3 vel;
};

struct NodeOtherData
{
    int trussesConnected;
    float mass;
    float3 normal;
    float3 startPosition;
    float weight;
};

struct ICData
{
    float3 cvel;
    float3 cpoint;
};

float FuncM(float x, float c, float a)
{
    if(x < c) return 0.0;
    return 2.0 * atan(x * a)  / PI;
}

float3 BARYC(float3 p, float3 vertex1, float3 vertex2, float3 vertex3)
{
    float3 v0 = vertex2 - vertex1;
    float3 v1 = vertex3 - vertex1;
    float3 v2 = p - vertex1;

    float dot00 = dot(v0, v0);
    float dot01 = dot(v0, v1);
    float dot11 = dot(v1, v1);
    float dot20 = dot(v2, v0);
    float dot21 = dot(v2, v1);

    float invDenom = 1.0 / (dot00 * dot11 - dot01 * dot01);
    float barycentricY = (dot11 * dot20 - dot01 * dot21) * invDenom;
    float barycentricZ = (dot00 * dot21 - dot01 * dot20) * invDenom;
    float barycentricX = 1.0 - barycentricY - barycentricZ;

    return float3(barycentricX, barycentricY, barycentricZ);
}

float3x3 ComputeInverse(float3x3 mat)
{
    float a = mat[0][0], b = mat[0][1], c = mat[0][2];
    float d = mat[1][0], e = mat[1][1], f = mat[1][2];
    float g = mat[2][0], h = mat[2][1], i = mat[2][2];

    float det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
    
    float invDet = 1.0 / det;

    float3x3 inverseMatrix;
    inverseMatrix[0][0] = (e * i - f * h) * invDet;
    inverseMatrix[0][1] = (c * h - b * i) * invDet;
    inverseMatrix[0][2] = (b * f - c * e) * invDet;
    inverseMatrix[1][0] = (f * g - d * i) * invDet;
    inverseMatrix[1][1] = (a * i - c * g) * invDet;
    inverseMatrix[1][2] = (c * d - a * f) * invDet;
    inverseMatrix[2][0] = (d * h - e * g) * invDet;
    inverseMatrix[2][1] = (b * g - a * h) * invDet;
    inverseMatrix[2][2] = (a * e - b * d) * invDet;

    return inverseMatrix;
}

float3 PLine(float3 np, float p0, float3 p1)
{
    float3 lV = p1 - p0;
    float d = dot(np - p0, normalize(lV));
    if(d <= 0) return p0;
    if(d >= length(lV))return p1;
    return p0 + normalize(lV) * d;
}

float3 RotV(float4 r, float3 v)
{
    float3 u = r.xyz;
    float s = r.w;
    return 2.0 * dot(u, v) * u
          + (s*s - dot(u, u)) * v
          + 2.0 * s * cross(u, v);
}

float3x3 GetLocalCoordMatrix(float4 rot)
{
    float3x3 IDENTITY = float3x3(
        float3(1.0, 0, 0),
        float3(0, 1.0, 0),
        float3(0, 0, 1.0)
        );

    float3 r = RotV(rot, IDENTITY[0]);
    float3 u = RotV(rot, IDENTITY[1]);
    float3 f = RotV(rot, IDENTITY[2]);

    return float3x3(
        float3(r.x, u.x, f.x),
        float3(r.y, u.y, f.y),
        float3(r.z, u.z, f.z)
        );
}