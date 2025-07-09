using Unity.Mathematics;
using UnityEngine;

public class AABB
{
    public float3 min;
    public float3 max;
    public float3 centre => (min + max) / 2;
    public float3 size => max - min;

    public AABB(float3 mi, float3 ma)
    {
        min = mi;
        max = ma;
    }

    public void GrowToInclude(Triangle tri)
    {
        GrowToInclude(tri.posA);
        GrowToInclude(tri.posB);
        GrowToInclude(tri.posC);
    }

    public void GrowToInclude(float3 point)
    {
        min = Vector3.Min(min, point);
        max = Vector3.Max(max, point);
    }

    public float SurfaceArea()
    {
        float3 e = max - min;
        return 2 * (e.x * e.y + e.y * e.z + e.z * e.x);
    }
}
