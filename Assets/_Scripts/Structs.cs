using System;
using NUnit.Framework.Internal;
using Unity.Mathematics;
using UnityEngine;

#region Source
public readonly struct Source
{
    public readonly float3 position;
    public readonly float radius;
    public readonly int mixerIndex;

    public Source(float3 pos, float r, int mxr)
    {
        position = pos;
        radius = r;
        mixerIndex = mxr;
    }
}

public readonly struct ImageSource
{
    public readonly float3 position;
    public readonly int valid;

    public ImageSource(float3 pos)
    {
        position = pos;
        valid = 1;
    }

    public ImageSource(bool f)
    {
        position = float3.zero;
        valid = f ? 1 : -1;
    }

}
#endregion

#region Geoemtry
public readonly struct Triangle
{
    public readonly float3 posA, posB, posC;
    public readonly float3 normalA, normalB, normalC;
    public readonly uint materialIndex;

    public Triangle(float3 pA, float3 pB, float3 pC, float3 nA, float3 nB, float3 nC, uint mat)
    {
        posA = pA;
        posB = pB;
        posC = pC;
        normalA = nA;
        normalB = nB;
        normalC = nC;
        materialIndex = mat;
    }
}

public readonly struct RMaterial
{
    public readonly float aC1, aC2, aC3, aC4;
    public readonly float sC1, sC2, sC3, sC4;

    public RMaterial(float a1, float a2, float a3, float a4, float s1, float s2, float s3, float s4)
    {
        aC1 = a1;
        aC2 = a2;
        aC3 = a3;
        aC4 = a4;
        sC1 = s1;
        sC2 = s2;
        sC3 = s3;
        sC4 = s4;
    }
}

public readonly struct RMesh
{
    public readonly uint firstTriangleIndex;
    public readonly uint numTriangles;

    public RMesh(uint firstTriIndex, uint numTri)
    {
        firstTriangleIndex = firstTriIndex;
        numTriangles = numTri;
    }
}

public readonly struct BVHNode
{
    public readonly float3 min;
    public readonly float3 max;
    public readonly int index; // if -1, points to childnode (!)
    public readonly int triangleCount;

    public BVHNode(AABB bounds) : this()
    {
        min = bounds.min;
        max = bounds.max;
        index = -1;
        triangleCount = -1;
    }

    public BVHNode(AABB bounds, int i, int triCount)
    {
        min = bounds.min;
        max = bounds.max;
        index = i;
        triangleCount = triCount;
    }

    public BVHNode(float3 mi, float3 ma, int i, int triCount)
    {
        min = mi;
        max = ma;
        index = i;
        triangleCount = triCount;
    }
}
#endregion

#region Rays
public readonly struct EnergyBand
{
    public readonly float e1, e2, e3, e4;

    public EnergyBand(float e)
    {
        e1 = e;
        e2 = e;
        e3 = e;
        e4 = e;
    }
}

public readonly struct RayData
{
    public readonly uint directionIndex; // get ray-direction (from Listener) with directions[directionIndex]
    public readonly uint sourceIndex;
    public readonly EnergyBand energy; // ray energy (per frequency)
    public readonly float distance; // ray travel distance

    public RayData(uint dirIndex, uint sIndex, EnergyBand e, float d)
    {
        directionIndex = dirIndex;
        sourceIndex = sIndex;
        energy = e;
        distance = d;
    }
}
#endregion