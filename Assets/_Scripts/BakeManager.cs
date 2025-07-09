using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;

public class BakeManager
{
    #region BVH
    public static void SaveNodes(List<BVHNode> nodes, string path)
    {
        using (var stream = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(nodes.Count);
            foreach (var node in nodes)
            {
                writer.Write(node.min.x);
                writer.Write(node.min.y);
                writer.Write(node.min.z);
                writer.Write(node.max.x);
                writer.Write(node.max.y);
                writer.Write(node.max.z);
                writer.Write(node.index);
                writer.Write(node.triangleCount);
            }
        }
    }

    public static void LoadNodes(string path, out List<BVHNode> nodes)
    {
        nodes = new List<BVHNode>();
        using (var stream = new FileStream(path, FileMode.Open))
        using (var reader = new BinaryReader(stream))
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                float3 min = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                float3 max = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                int index = reader.ReadInt32();
                int triangleCount = reader.ReadInt32();
                nodes.Add(new BVHNode(min, max, index, triangleCount));
            }
        }
    }
    #endregion

    #region Mesh
    public static void SaveMeshData(List<Triangle> triangles, List<RMesh> meshes, string path)
    {
        using (var stream = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            // Save triangles
            writer.Write(triangles.Count);
            foreach (var triangle in triangles)
            {
                WriteFloat3(writer, triangle.posA);
                WriteFloat3(writer, triangle.posB);
                WriteFloat3(writer, triangle.posC);
                WriteFloat3(writer, triangle.normalA);
                WriteFloat3(writer, triangle.normalB);
                WriteFloat3(writer, triangle.normalC);
                writer.Write(triangle.materialIndex);
            }

            // Save RMeshes
            writer.Write(meshes.Count);
            foreach (var rMesh in meshes)
            {
                writer.Write(rMesh.firstTriangleIndex);
                writer.Write(rMesh.numTriangles);
            }
        }
    }

    public static void LoadMeshData(string path, out List<Triangle> triangles, out List<RMesh> meshes)
    {
        triangles = new List<Triangle>();
        meshes = new List<RMesh>();

        using (var stream = new FileStream(path, FileMode.Open))
        using (var reader = new BinaryReader(stream))
        {
            // Load triangles
            int triangleCount = reader.ReadInt32();
            for (int i = 0; i < triangleCount; i++)
            {
                float3 vertexA = ReadFloat3(reader);
                float3 vertexB = ReadFloat3(reader);
                float3 vertexC = ReadFloat3(reader);
                float3 normalA = ReadFloat3(reader);
                float3 normalB = ReadFloat3(reader);
                float3 normalC = ReadFloat3(reader);
                uint materialIndex = reader.ReadUInt32();
                triangles.Add(new Triangle(vertexA, vertexB, vertexC, normalA, normalB, normalC, materialIndex));
            }

            // Load RMeshes
            int meshCount = reader.ReadInt32();
            for (int i = 0; i < meshCount; i++)
            {
                uint startTriangleIndex = reader.ReadUInt32();
                uint numTriangles = reader.ReadUInt32();
                meshes.Add(new RMesh(startTriangleIndex, numTriangles));
            }
        }
    }

    private static void WriteFloat3(BinaryWriter writer, float3 value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
    }

    private static float3 ReadFloat3(BinaryReader reader)
    {
        return new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
    #endregion

    #region Misc
    public static bool AreListsEqual(List<BVHNode> list1, List<BVHNode> list2)
    {
        if (ReferenceEquals(list1, list2)) return true;

        if (list1 == null || list2 == null || list1.Count != list2.Count) return false;

        for (int i = 0; i < list1.Count; i++)
        {
            if (!AreNodesEqual(list1[i], list2[i]))
                return false;
        }

        return true;
    }

    private static bool AreNodesEqual(BVHNode node1, BVHNode node2)
    {
        return node1.min.Equals(node2.min) &&
               node1.max.Equals(node2.max) &&
               node1.index == node2.index &&
               node1.triangleCount == node2.triangleCount;
    }
    #endregion
}