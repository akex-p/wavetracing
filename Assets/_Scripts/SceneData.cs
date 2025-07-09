using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityMeshSimplifier;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SceneData : MonoBehaviour
{
    [Header("Source Settings")]
    [Range(1, 32)]
    [SerializeField] public int maxSources = 3;
    [Range(0f, 5f)]
    [SerializeField] private float sourceRadius = 0.5f;

    [Header("Mesh/BVH Settings")]
    [Range(0, 32)]
    [SerializeField] private int maxBHVDepth = 8;
    [Range(0f, 1f)]
    [SerializeField] private float meshQuality = 1f;

    [Header("Debug Settings")]
    [SerializeField] private bool drawImageSources = false;
    [SerializeField] private Color debugImageSourceColor = Color.red;
    [Range(-1, 32)]
    [SerializeField] private int debugBVHDepth;
    [Range(0f, 1f)]
    [SerializeField] private float debugBVHOpacity;
    [SerializeField] private Color debugBVHColor = Color.green;

    [Header("References")]
    [Tooltip("Reference to GameObject storing all relevant geometry.")]
    [SerializeField] private GameObject geometry;
    [Tooltip("Reference to Player/Listener GameObject.")]
    [SerializeField] public GameObject listener;
    [Tooltip("Reference to Prefab of AudioSource.")]
    [SerializeField] private GameObject sourceFab;
    [Tooltip("Reference to Transform storing all AudioSources.")]
    [SerializeField] private Transform audioSourceParent;
    [Tooltip("Reference to AudioMixer for audio routing.")]
    [SerializeField] private AudioMixer audioMixer;
    [Tooltip("Reference to AudioController Component in Manager.")]
    [SerializeField] private AudioController audioController;

    private MovementTracker listenerTracker;
    private bool sceneInitialized = false;
    private bool simulationTurnedOn = true;
    private float[] collidedCache;

    // Audio
    public List<Source> sources { get; private set; }
    private Dictionary<AudioSource, Source> audioToSource;
    private SortedSet<int> availableSlots;
    private AudioMixerGroup[] audioMixerGroups;

    // Geometry
    public MeshFilter[] meshFilters { get; private set; }
    public List<RMesh> meshes { get; private set; }
    public List<Triangle> triangles { get; private set; }
    public List<BVHNode> bvhNodes { get; private set; }
    public List<RMaterial> materials { get; private set; }
    private List<AudioMaterial> audioMaterials;
    private Dictionary<AudioMaterial, uint> materialToIndex;

    public List<ImageSource[]> imageSources;

    public static Action OnInitialized;
    public static Action OnSourceAdded;
    public static Action OnSourceRemoved;
    public static Action OnListenerMoved;

    void Start()
    {
        InitScene();
    }

    void Update()
    {
        if (sceneInitialized)
        {
            if (listenerTracker.CheckIfMoved()) OnListenerMoved.Invoke();
            // DebugBVH();
        }
    }

    #region Init
    private void InitScene()
    {
        if (!InitMaterial())
        {
            return;
        }

        // // Load from Baked Data
        // List<Triangle> loadedTriangles;
        // List<RMesh> loadedMeshes;
        // BakeManager.LoadMeshData("mesh.dat", out loadedTriangles, out loadedMeshes);
        // triangles = loadedTriangles;
        // meshes = loadedMeshes;

        // List<BVHNode> loadedBVHNodes;
        // BakeManager.LoadNodes("nodes.dat", out loadedBVHNodes);
        // bvhNodes = loadedBVHNodes;

        // Debug.Log($"SceneData: Loaded Mesh with {triangles.Count} triangles and {meshes.Count} meshes.");
        // Debug.Log($"SceneData: Loaded BVH with {bvhNodes.Count} nodes.");

        // RUNTIME generation instead of baked loading
        if (!InitSceneGeometry())
        {
            Debug.LogError("SceneData: Failed to initialize geometry.");
            return;
        }

        InitBVH(); // Build BVH at runtime

        Debug.Log($"SceneData: Generated Mesh with {triangles.Count} triangles and {meshes.Count} meshes.");
        Debug.Log($"SceneData: Generated BVH with {bvhNodes.Count} nodes.");

        if (!InitImageSources())
        {
            Debug.LogError("SceneData: Something went wrong with the ImageSource List... weird.");
            return;
        }
        if (!InitAudioMixerGroups())
        {
            Debug.LogError("SceneData: Failed to initialize AudioMixerGroups. Be sure to add enough AudioMixerGroups to Master!");
            return;
        }
        if (!InitSources())
        {
            Debug.LogError("SceneData: Failed to initialize Sources. Too many Sources!");
            return;
        }

        listenerTracker = new MovementTracker(listener.transform);

        sceneInitialized = true;
        OnInitialized.Invoke();
    }
    #endregion

    #region Bake
    [ContextMenu("Bake Geometry")]
    private void BakeGeometry()
    {
        if (!InitMaterialForBake())
        {
            Debug.LogError("SceneData: Failed to initialize Materials. Be sure to add Materials to the folder!");
            return;
        }
        if (!InitSceneGeometry())
        {
            return;
        }

        InitBVH();

        BakeManager.SaveMeshData(triangles, meshes, "mesh.dat");
        BakeManager.SaveNodes(bvhNodes, "nodes.dat");
    }
    #endregion

    #region Material
    private bool InitMaterial()
    {
        materials = new List<RMaterial>();
        audioMaterials = new List<AudioMaterial>();
        materialToIndex = new Dictionary<AudioMaterial, uint>();

        audioMaterials.AddRange(Resources.LoadAll<AudioMaterial>("Materials"));

        if (audioMaterials.Count < 1)
        {
            Debug.LogError("SceneData: No AudioMaterials found. Be sure to add AudioMaterials to the Materials directory.");
            return false;
        }

        for (int i = 0; i < audioMaterials.Count; i++)
        {
            materials.Add(new RMaterial(
                audioMaterials[i].absorptionCoefficient110,
                audioMaterials[i].absorptionCoefficient630,
                audioMaterials[i].absorptionCoefficient3500,
                audioMaterials[i].absorptionCoefficient22050,
                audioMaterials[i].scatteringCoefficient110,
                audioMaterials[i].scatteringCoefficient630,
                audioMaterials[i].scatteringCoefficient3500,
                audioMaterials[i].scatteringCoefficient22050));

            materialToIndex.Add(audioMaterials[i], (uint)i);
        }

        Debug.Log($"SceneData: Loaded {audioMaterials.Count} AudioMaterials.");

        return true;
    }

    private bool InitMaterialForBake()
    {
        audioMaterials = new List<AudioMaterial>();
        materialToIndex = new Dictionary<AudioMaterial, uint>();

        audioMaterials.AddRange(Resources.LoadAll<AudioMaterial>("Materials"));

        if (audioMaterials.Count < 1)
        {
            Debug.LogError("SceneData: No AudioMaterials found. Be sure to add AudioMaterials to the Materials directory.");
            return false;
        }

        for (int i = 0; i < audioMaterials.Count; i++)
        {
            materialToIndex.Add(audioMaterials[i], (uint)i);
        }

        Debug.Log($"SceneData: Prebaked Materials with {audioMaterials.Count} AudioMaterials.");

        return true;
    }
    #endregion

    #region Mesh
    private bool InitSceneGeometry()
    {
        if (geometry == null)
        {
            Debug.LogError("SceneData: Failed Init. Please reference a GameObject containing all scene Objects.");
            return false;
        }

        meshFilters = geometry.GetComponentsInChildren<MeshFilter>();

        if (meshFilters.Length < 1)
        {
            Debug.LogError("SceneData: Failed Init. GameObject Geometry does not contain any Objects.");
            return false;
        }

        triangles = new List<Triangle>();
        meshes = new List<RMesh>();
        uint currentTriangleIndex = 0;

        foreach (MeshFilter filter in meshFilters)
        {
            Mesh mesh = filter.sharedMesh;

            if (meshQuality != 1f) // reduce mesh quality (but not visually!)
            {
                MeshSimplifier meshSimplifier = new MeshSimplifier();
                meshSimplifier.Initialize(mesh);
                meshSimplifier.SimplifyMesh(meshQuality);
                mesh = meshSimplifier.ToMesh();
            }

            if (mesh == null) continue;

            Transform transform = filter.transform;

            // Calc bounds
            Bounds bounds = mesh.bounds;
            Vector3 boundsMin = transform.TransformPoint(bounds.min);
            Vector3 boundsMax = transform.TransformPoint(bounds.max);

            // Vertex pos and normals
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] indices = mesh.triangles;

            uint numTriangles = (uint)(indices.Length / 3);

            // generate RMaterial
            AudioMaterial audioMaterial = filter.gameObject.GetComponent<GeometryController>().audioMaterial;
            if (audioMaterial == null)
            {
                Debug.LogError($"SceneData: No AudioMaterial found for {filter.gameObject.name}. Be sure to add an AudioMaterial there.");
                return false;
            }
            uint materialIndex = materialToIndex[filter.gameObject.GetComponent<GeometryController>().audioMaterial];

            // Extract Tris
            for (int i = 0; i < indices.Length; i += 3)
            {
                int indexA = indices[i];
                int indexB = indices[i + 1];
                int indexC = indices[i + 2];

                triangles.Add(new Triangle
                (
                    transform.TransformPoint(vertices[indexA]),
                    transform.TransformPoint(vertices[indexB]),
                    transform.TransformPoint(vertices[indexC]),
                    transform.TransformDirection(normals[indexA]),
                    transform.TransformDirection(normals[indexB]),
                    transform.TransformDirection(normals[indexC]),
                    materialIndex
                ));
            }

            // Add RMesh
            meshes.Add(new RMesh(currentTriangleIndex, numTriangles));
            currentTriangleIndex += numTriangles;
        }

        // DEBUGGING
        Debug.Log($"SceneData: Baked Mesh with {triangles.Count} triangles and {meshes.Count} meshes.");
        return true;
    }
    #endregion

    #region BVH
    private void InitBVH()
    {
        bvhNodes = new List<BVHNode>();
        BuildBVH(triangles);

        Debug.Log($"SceneData: Baked BVH with {bvhNodes.Count} nodes.");
    }

    private void BuildBVH(List<Triangle> triangles)
    {
        AABB bounds = new AABB(Vector3.one * float.PositiveInfinity, Vector3.one * float.NegativeInfinity);

        foreach (Triangle tri in triangles)
        {
            bounds.GrowToInclude(tri);
        }

        BVHNode root = new BVHNode(bounds, 0, triangles.Count);
        bvhNodes.Add(root);
        SplitBVH(0, 0);
    }

    private void SplitBVH(int parentIndex, int depth = 0)
    {
        if (depth >= maxBHVDepth)
            return;

        BVHNode parent = bvhNodes[parentIndex];
        float3 size = parent.max - parent.min;
        (int splitAxis, float splitPos, float cost) = ChooseSplit(parent, parent.index, parent.triangleCount);

        AABB leftBounds = new AABB(Vector3.one * float.PositiveInfinity, Vector3.one * float.NegativeInfinity);
        AABB rightBounds = new AABB(Vector3.one * float.PositiveInfinity, Vector3.one * float.NegativeInfinity);
        int leftIndex = parent.index;
        int rightIndex = parent.index;
        int leftCount = 0;
        int rightCount = 0;

        for (int i = parent.index; i < parent.index + parent.triangleCount; i++)
        {
            Triangle tri = triangles[i];
            float centroid = (tri.posA[splitAxis] + tri.posB[splitAxis] + tri.posC[splitAxis]) / 3f;
            if (centroid < splitPos) //isSideA
            {
                leftBounds.GrowToInclude(tri);
                leftCount++;

                int swap = leftIndex + leftCount - 1;
                (triangles[i], triangles[swap]) = (triangles[swap], triangles[i]);
                rightIndex++;
            }
            else
            {
                rightBounds.GrowToInclude(tri);
                rightCount++;
            }
        }

        if (leftCount > 0 && rightCount > 0)
        {
            int childIndex = bvhNodes.Count;
            bvhNodes[parentIndex] = new BVHNode(parent.min, parent.max, childIndex, -1);

            bvhNodes.Add(new BVHNode(leftBounds, leftIndex, leftCount));
            bvhNodes.Add(new BVHNode(rightBounds, rightIndex, rightCount));

            if (leftCount > 0) SplitBVH(childIndex, depth + 1);
            if (rightCount > 0) SplitBVH(childIndex + 1, depth + 1);
        }
    }

    (int axis, float pos, float cost) ChooseSplit(BVHNode node, int start, int count)
    {
        if (count <= 1) return (0, 0, float.PositiveInfinity);

        float bestSplitPos = 0;
        int bestSplitAxis = 0;
        const int numSplitTests = 5;

        float bestCost = float.MaxValue;

        // Estimate best split pos
        for (int axis = 0; axis < 3; axis++)
        {
            for (int i = 0; i < numSplitTests; i++)
            {
                float splitT = (i + 1) / (numSplitTests + 1f);

                float splitPos = Mathf.Lerp(node.min[axis], node.max[axis], splitT);
                float cost = EvaluateSplit(axis, splitPos, start, count);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSplitPos = splitPos;
                    bestSplitAxis = axis;
                }
            }
        }

        return (bestSplitAxis, bestSplitPos, bestCost);
    }

    float EvaluateSplit(int splitAxis, float splitPos, int start, int count)
    {
        AABB boundsLeft = new AABB(Vector3.one * float.PositiveInfinity, Vector3.one * float.NegativeInfinity);
        AABB boundsRight = new AABB(Vector3.one * float.PositiveInfinity, Vector3.one * float.NegativeInfinity);
        int numOnLeft = 0;
        int numOnRight = 0;

        for (int i = start; i < start + count; i++)
        {
            Triangle tri = triangles[i];
            float centroid = (tri.posA[splitAxis] + tri.posB[splitAxis] + tri.posC[splitAxis]) / 3f;
            if (centroid < splitPos)
            {
                boundsLeft.GrowToInclude(tri);
                numOnLeft++;
            }
            else
            {
                boundsRight.GrowToInclude(tri);
                numOnRight++;
            }
        }

        float costA = NodeCost(boundsLeft.size, numOnLeft);
        float costB = NodeCost(boundsRight.size, numOnRight);

        if (float.IsInfinity(costA) || float.IsInfinity(costB) || numOnLeft == 0 || numOnRight == 0) return float.MaxValue;

        return costA + costB;
    }

    static float NodeCost(Vector3 size, int numTriangles)
    {
        float surfaceArea = size.x * size.y + size.x * size.z + size.y * size.z;
        return surfaceArea * numTriangles;
    }
    #endregion

    #region Sources
    private bool InitAudioMixerGroups()
    {
        audioMixerGroups = audioMixer.FindMatchingGroups("Master/Sources/");
        if (audioMixerGroups.Length < maxSources) return false;

        availableSlots = new SortedSet<int>();
        string output = "";

        for (int i = 0; i < audioMixerGroups.Length; i++)
        {
            availableSlots.Add(-1 * (i - audioMixerGroups.Length + 1));
            output += audioMixerGroups[i] + ", ";
        }

        // for switching and stuff
        collidedCache = new float[maxSources];

        // DEBUGGING
        Debug.Log($"SceneData: Found AudioMixerGroups: {output}");
        return true;
    }

    private bool InitSources()
    {
        audioToSource = new Dictionary<AudioSource, Source>();
        //sourceToSlot = new Dictionary<Source, int>();
        sources = new List<Source>();

        AudioSource[] foundSources = audioSourceParent.GetComponentsInChildren<AudioSource>();

        if (foundSources.Length > maxSources) return false;

        foreach (AudioSource foundSource in foundSources)
        {
            AddSourceToData(foundSource);
        }

        // DEBUGGING
        Debug.Log($"SceneData: Loaded {foundSources.Length} AudioSources.");
        return true;
    }

    public void AddSourceToScene(Vector3 pos, AudioClip clip)
    {
        if (sources == null)
        {
            Debug.LogError("SceneData: Failed Initialization. Make sure all errors are fixed.");
            return;
        }
        if (sources.Count >= maxSources)
        {
            Debug.LogError("SceneData: Reached maxmimum amount for AudioSources. Buffer is full!");
            return;
        }
        GameObject audioSourceObject = Instantiate(sourceFab, pos, Quaternion.identity, audioSourceParent);
        AudioSource audioSource = audioSourceObject.GetComponent<AudioSource>();

        AddSourceToData(audioSource);

        audioSource.clip = clip;
        audioSource.Play();

        OnSourceAdded.Invoke();
    }

    private void AddSourceToData(AudioSource audioSource)
    {
        int groupIndex = availableSlots.First();
        availableSlots.Remove(groupIndex);

        Source source = new Source(audioSource.transform.position, sourceRadius, groupIndex);

        audioToSource.Add(audioSource, source);
        //sourceToSlot.Add(source, groupIndex);
        sources.Add(source);

        ComputeImageSources(source);

        audioSource.outputAudioMixerGroup = audioMixerGroups[groupIndex];

        // DEBUGGING
        // DebugSources();
    }

    public void RemoveSourceFromScene(AudioSource audioSource)
    {
        GameObject audioSourceObject = audioSource.gameObject;

        RemoveSourceFromData(audioSource);

        Destroy(audioSourceObject);

        OnSourceRemoved.Invoke();

        // DEBUGGING
        //DebugSources();
    }

    private void RemoveSourceFromData(AudioSource audioSource)
    {
        Source source = audioToSource[audioSource];

        int groupIndex = source.mixerIndex;
        availableSlots.Add(groupIndex);

        DestroyImageSources(source);

        audioToSource.Remove(audioSource);
        //sourceToSlot.Remove(source);
        sources.Remove(source);
    }
    #endregion

    #region Filters
    public void UpdateLowpassFilter(int i, int collided)
    {
        if (simulationTurnedOn) audioMixer.SetFloat("Lowpass0" + i, 22000f - (2200f * collided));
    }

    public void SwitchSimulation()
    {
        if (simulationTurnedOn)
        {
            for (int i = 0; i < maxSources; i++)
            {
                audioMixer.SetFloat("Wet0" + i, 0);

                float cutoff;
                audioMixer.GetFloat("Lowpass0" + i, out cutoff);
                collidedCache[i] = cutoff;

                audioMixer.SetFloat("Lowpass0" + i, 22000);
            }
            simulationTurnedOn = false;
        }
        else
        {
            for (int i = 0; i < maxSources; i++)
            {
                audioMixer.SetFloat("Wet0" + i, 100);
                audioMixer.SetFloat("Lowpass0" + i, collidedCache[i]);
            }
            simulationTurnedOn = true;
        }
    }
    #endregion

    #region Image Source
    private bool InitImageSources()
    {
        imageSources = new List<ImageSource[]>();
        return true;
    }

    private void ComputeImageSources(Source source)
    {
        NativeArray<ImageSource> imageSourcesTemp = new NativeArray<ImageSource>(triangles.Count, Allocator.Temp);
        for (int i = 0; i < triangles.Count; i++)
        {
            imageSourcesTemp[i] = ReflectOnTriangle(source, i); ;
        }
        imageSources.Add(imageSourcesTemp.ToArray());
        // Debug.Log($"SceneData: Constructed {imageSourcesTemp.Length} Image Sources for Source {source.mixerIndex}.");
        imageSourcesTemp.Dispose();
    }

    private void DestroyImageSources(Source source)
    {
        imageSources.RemoveAt(sources.IndexOf(source));
    }

    private ImageSource ReflectOnTriangle(Source source, int triangleIndex)
    {
        Triangle tri = triangles[triangleIndex];
        float3 edge1 = tri.posB - tri.posA;
        float3 edge2 = tri.posC - tri.posA;
        float3 planeNormal = Vector3.Normalize(Vector3.Cross(edge1, edge2));
        float3 toSource = source.position - tri.posA;

        // Reject back-facing triangles
        if (Vector3.Dot(toSource, planeNormal) <= 0)
        {
            return new ImageSource(false); // invalid -> valid = -1, pos = float3.zero
        }

        float distance = Vector3.Dot(toSource, planeNormal);
        float3 reflectedPos = source.position - 2.0f * distance * planeNormal;

        return new ImageSource(reflectedPos);
    }
    #endregion

    #region Debug
    private void DebugBVH(int nodeIndex = 0, int depth = 0)
    {
        if (bvhNodes == null)
        {
            Debug.LogError("SceneData: bakedBVH.bvhNodes is null and cannot be drawn!");
            return;
        }
        if (nodeIndex < 0 || nodeIndex >= bvhNodes.Count || depth > debugBVHDepth)
            return;

        BVHNode node = bvhNodes[nodeIndex];
        DebugAABB(new AABB(node.min, node.max), depth);

        // Recursion
        if (node.triangleCount == -1) // -1 marks inner Node
        {
            DebugBVH(node.index, depth + 1);
            DebugBVH(node.index + 1, depth + 1);
        }
    }

    private void DebugAABB(AABB aabb, int depth = 0)
    {
        Color col = debugBVHColor;
        col.a = Mathf.Clamp(1f / (depth + 1), debugBVHOpacity, 1f);

        // Calc verts
        Vector3 v0 = new Vector3(aabb.min.x, aabb.min.y, aabb.min.z);
        Vector3 v1 = new Vector3(aabb.max.x, aabb.min.y, aabb.min.z);
        Vector3 v2 = new Vector3(aabb.max.x, aabb.max.y, aabb.min.z);
        Vector3 v3 = new Vector3(aabb.min.x, aabb.max.y, aabb.min.z);

        Vector3 v4 = new Vector3(aabb.min.x, aabb.min.y, aabb.max.z);
        Vector3 v5 = new Vector3(aabb.max.x, aabb.min.y, aabb.max.z);
        Vector3 v6 = new Vector3(aabb.max.x, aabb.max.y, aabb.max.z);
        Vector3 v7 = new Vector3(aabb.min.x, aabb.max.y, aabb.max.z);

        // Draw edges
        Debug.DrawLine(v0, v1, col);
        Debug.DrawLine(v1, v2, col);
        Debug.DrawLine(v2, v3, col);
        Debug.DrawLine(v3, v0, col);

        Debug.DrawLine(v4, v5, col);
        Debug.DrawLine(v5, v6, col);
        Debug.DrawLine(v6, v7, col);
        Debug.DrawLine(v7, v4, col);

        Debug.DrawLine(v0, v4, col);
        Debug.DrawLine(v1, v5, col);
        Debug.DrawLine(v2, v6, col);
        Debug.DrawLine(v3, v7, col);
    }

    // OLD DEBUGGING
    private void DebugSources()
    {
        string output = "";
        foreach (Source source in sources)
        {
            output += source.position + " " + source.radius + " " + source.mixerIndex + "\n";
        }
        Debug.Log($"SceneData: {output}");
    }

    // private void OnDrawGizmos()
    // {
    //     if (drawImageSources)
    //     {
    //         Gizmos.color = debugImageSourceColor;

    //         if (imageSources == null || imageSources.Count < 1)
    //         {
    //             return;
    //         }

    //         foreach (ImageSource i in imageSources[0])
    //         {
    //             Gizmos.DrawSphere(i.position, 0.2f); // Draw a small sphere at each position
    //         }
    //     }
    // }
    #endregion
}