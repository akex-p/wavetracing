using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class ShaderController : MonoBehaviour
{
    [Header("General Settings")]
    [SerializeField] private Vector3 listenerOffset = new Vector3(0.0f, 0.0f, 0.0f);
    [SerializeField] private bool useDIRECT = true;
    [SerializeField] private bool useISM = true;
    [SerializeField] private bool useRT = true;
    [SerializeField] private bool useOCL = true;
    [SerializeField] private bool useBVH = true;
    [SerializeField] private bool useVBS = true; // Vector based Scattering
    [SerializeField] private bool useRAIN = true; // Diffuse Rain
    [SerializeField] private bool genHisto = false; // Histogram-based Calculation for RT -> DOES NOTHING ANYMORE (Perfomance...)

    [Header("RT Settings")]
    [Range(32, 32768)]
    [SerializeField] private int numRays = 1024;
    [Range(1, 128)]
    [SerializeField] private int maxRayDepth = 1;
    [Range(0f, 2f)]
    [SerializeField] private float airAbsorptionsCoefficient = 0.1f;
    [Range(0f, 0.001f)]
    [SerializeField] private float energyThreshold = 0.0001f;

    [Header("DIRECT/ISM Settings")]
    [Range(1f, 50f)]
    [SerializeField] private float ismEnergyRatio = 20f;
    [Range(1f, 50f)]
    [SerializeField] private float directEnergyRatio = 30f;

    [Header("OCL Settings")]
    [Range(0f, 5f)]
    [SerializeField] private float listenerSideMaxOffset = 0.5f;
    [Range(0f, 5f)]
    [SerializeField] private float sourceSideMaxOffset = 0.5f;

    [Header("Testing Settings")]
    [SerializeField] private bool readBuffer = true;
    [SerializeField] private bool generateIR = true;

    [Header("Debug Settings")]
    [SerializeField] private bool drawRays = true;
    [SerializeField] private bool drawOcclusion = true;
    [Range(0f, 50f)]
    [SerializeField] private float debugRayLength;
    [Range(0f, 1f)]
    [SerializeField] private float debugRayOpacity;
    [SerializeField] private Color debugRayBVHColor = Color.magenta;
    [SerializeField] private Color debugRayMeshColor = Color.red;
    [Space(10)]
    [SerializeField] private Color debugOcclusionColor = Color.blue;
    [SerializeField] private Color debugOcclusionColor2 = Color.cyan;

    [Header("References")]
    [SerializeField] private ComputeShader rayShader;
    [SerializeField] private SceneData sceneData;
    [SerializeField] private AudioController audioController;
    [SerializeField] private LayerMask geometryLayer;

    private int rtKernel;
    private int ismKernel;
    // private int clearKernel;

    private int rayDataCounter;
    private int numRayData;
    private int numRTThreads;
    private int numISMThreads;
    private int numClearThreads;
    private int frame = 0;
    private bool sourceBufferEmpty = true;
    private bool shaderInitialized = false;
    private int numBins;

    private int[] counterArray;
    private Vector3[] directions;
    private RayData[] rayData;
    private EnergyBand[] histograms;

    private ComputeBuffer rayDataBuffer;
    private ComputeBuffer directionBuffer;
    private ComputeBuffer sourceBuffer;
    private ComputeBuffer meshBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer bvhBuffer;
    private ComputeBuffer counterBuffer;
    private ComputeBuffer materialBuffer;
    private ComputeBuffer imageSourceBuffer;
    // private ComputeBuffer histogramBuffer;

    void Awake()
    {
        SceneData.OnInitialized += InitShader;
        SceneData.OnSourceAdded += AddSource;
        SceneData.OnSourceRemoved += RemoveSource;
        SceneData.OnListenerMoved += UpdateListener;
    }

    void Update()
    {
        if (shaderInitialized && !sourceBufferEmpty)
        {
            // audioController.DrawHistograms(histograms);
            // if (drawRays) DebugRays();
        }
    }

    #region Update
    private void RunShader()
    {
        if (shaderInitialized && !sourceBufferEmpty)
        {
            // rayShader.SetBool("useBVH", useBVH);
            // rayShader.SetFloat("airAbsorptionsCoefficient", airAbsorptionsCoefficient);
            // rayShader.SetFloat("energyThreshold", energyThreshold);
            // rayShader.SetFloat("energyRatio", ismEnergyRatio);
            rayShader.SetInt("frame", frame);

            rayDataBuffer.SetCounterValue(0);
            if (useRT) rayShader.Dispatch(rtKernel, numRTThreads, 1, 1);
            if (useISM) rayShader.Dispatch(ismKernel, numISMThreads, 1, 1);

            if (readBuffer)
            {
                UpdateCounter();
                rayDataBuffer.GetData(rayData);
            }
            frame++;

            // if (genHisto) histogramBuffer.GetData(histograms);
            // rayShader.Dispatch(clearKernel, numClearThreads, 1, 1);

            if (useDIRECT) CastDirectRays();
            if (useOCL) CastOcclusionRays();

            // Generate IR
            if (generateIR) audioController.UpdateIRs(rayData, rayDataCounter);
            // audioController.GIRP(histograms);
        }
    }

    private void UpdateCounter()
    {
        ComputeBuffer.CopyCount(rayDataBuffer, counterBuffer, 0);
        counterBuffer.GetData(counterArray);
        rayDataCounter = counterArray[0];
    }
    #endregion

    #region Init
    private void InitShader() // OnInitialized (SceneData)
    {
        InitData();

        // Init buffers
        directionBuffer = new ComputeBuffer(numRays, sizeof(float) * 3);
        sourceBuffer = new ComputeBuffer(sceneData.maxSources, sizeof(float) * 4 + sizeof(int));
        triangleBuffer = new ComputeBuffer(sceneData.triangles.Count, sizeof(float) * 3 * 6 + sizeof(uint));
        meshBuffer = new ComputeBuffer(sceneData.meshes.Count, sizeof(uint) * 2);
        bvhBuffer = new ComputeBuffer(sceneData.bvhNodes.Count, sizeof(float) * 6 + sizeof(int) * 2);
        rayDataBuffer = new ComputeBuffer(numRayData, sizeof(float) * 5 + sizeof(uint) * 2, ComputeBufferType.Append); // Output Buffer
        counterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
        materialBuffer = new ComputeBuffer(sceneData.materials.Count, sizeof(float) * 8);
        imageSourceBuffer = new ComputeBuffer(sceneData.maxSources * sceneData.triangles.Count, sizeof(float) * 3 + sizeof(int));
        // histogramBuffer = new ComputeBuffer(numBins * sceneData.maxSources, sizeof(float) * 4);

        // Fill buffers
        triangleBuffer.SetData(sceneData.triangles.ToArray());
        meshBuffer.SetData(sceneData.meshes.ToArray());
        bvhBuffer.SetData(sceneData.bvhNodes.ToArray());
        materialBuffer.SetData(sceneData.materials.ToArray());
        directionBuffer.SetData(directions);

        // Set RayShader
        rayShader.SetBuffer(rtKernel, "rayDataBuffer", rayDataBuffer);
        rayShader.SetBuffer(rtKernel, "triangleBuffer", triangleBuffer);
        rayShader.SetBuffer(rtKernel, "meshBuffer", meshBuffer);
        rayShader.SetBuffer(rtKernel, "bvhBuffer", bvhBuffer);
        rayShader.SetBuffer(rtKernel, "materialBuffer", materialBuffer);
        rayShader.SetBuffer(rtKernel, "directionBuffer", directionBuffer);

        // rayShader.SetBuffer(rtKernel, "histogramBuffer", histogramBuffer);
        // rayShader.SetBuffer(clearKernel, "histogramBuffer", histogramBuffer);

        rayShader.SetBuffer(ismKernel, "rayDataBuffer", rayDataBuffer);
        rayShader.SetBuffer(ismKernel, "triangleBuffer", triangleBuffer);
        rayShader.SetBuffer(ismKernel, "meshBuffer", meshBuffer);
        rayShader.SetBuffer(ismKernel, "bvhBuffer", bvhBuffer);
        rayShader.SetBuffer(ismKernel, "materialBuffer", materialBuffer);

        rayShader.SetInt("numTriangles", sceneData.triangles.Count);
        rayShader.SetInt("numMeshes", sceneData.meshes.Count);
        rayShader.SetInt("numBVHNodes", sceneData.bvhNodes.Count);
        rayShader.SetInt("maxRayDepth", maxRayDepth);
        rayShader.SetInt("numRays", numRays);
        rayShader.SetInt("numSources", sceneData.sources.Count);
        rayShader.SetInt("frame", 0);
        rayShader.SetVector("listenerPos", sceneData.listener.transform.position);
        rayShader.SetFloat("airAbsorptionsCoefficient", airAbsorptionsCoefficient);
        rayShader.SetFloat("energyThreshold", energyThreshold);
        rayShader.SetFloat("energyRatio", ismEnergyRatio);
        rayShader.SetBool("useBVH", useBVH);
        rayShader.SetBool("useVBS", useVBS);
        rayShader.SetBool("useRAIN", useRAIN);
        rayShader.SetBool("genHisto", genHisto);
        rayShader.SetFloat("sampleRate", 1 / audioController.binSize);
        rayShader.SetFloat("speedOfSound", AudioController.SpeedOfSound);
        rayShader.SetInt("numBins", numBins);

        // Set Sources if notEmpty
        if (sceneData.sources.Count > 0)
        {
            sourceBuffer.SetData(sceneData.sources.ToArray<Source>());
            rayShader.SetBuffer(rtKernel, "sourceBuffer", sourceBuffer);
            rayShader.SetBuffer(ismKernel, "sourceBuffer", sourceBuffer);

            SetImageSourceBuffer();

            sourceBufferEmpty = false;
        }

        shaderInitialized = true;

        // DEBUGGING
        string debug = sourceBufferEmpty ? "(Only Geometry)" : "(Geometry and Sources)";
        Debug.Log("SceneData: Initialized Shaders " + debug);
    }

    private void InitData()
    {
        // Arrays
        numRayData = numRays * maxRayDepth;
        rtKernel = rayShader.FindKernel("RT");
        ismKernel = rayShader.FindKernel("ISM");
        // clearKernel = rayShader.FindKernel("Clear");
        rayData = new RayData[numRayData];
        counterArray = new int[1];

        numBins = Mathf.RoundToInt(audioController.sampleLength / audioController.binSize);
        histograms = new EnergyBand[numBins * sceneData.maxSources];

        // Directions
        directions = new Vector3[numRays];
        float inc = Mathf.PI * (3 - Mathf.Sqrt(5));
        float off = 2f / numRays;
        for (int i = 0; i < numRays; i++)
        {
            float y = i * off - 1 + (off / 2);
            float r = Mathf.Sqrt(1 - y * y);
            float phi = i * inc;
            float x = Mathf.Cos(phi) * r;
            float z = Mathf.Sin(phi) * r;
            directions[i] = new Vector3(x, y, z);
        }

        numRTThreads = numRays / 32;
        numISMThreads = RoundUpToPower2(sceneData.triangles.Count) / 32;
        numClearThreads = numBins * sceneData.maxSources / 32;
    }
    #endregion

    #region Direct Ray
    private void CastDirectRays()
    {
        for (int i = 0; i < sceneData.sources.Count; i++)
        {
            Vector3 sourcePos = sceneData.sources[i].position;
            Vector3 listenerPos = sceneData.listener.transform.position;
            Vector3 directRay = sourcePos - listenerPos;
            float distance = directRay.magnitude;

            if (Physics.Raycast(listenerPos, directRay.normalized, distance, geometryLayer))
            {
                continue;
            }
            else
            {
                RayData directSound = new RayData(0, (uint)i, new EnergyBand(1f / distance / directEnergyRatio), distance); // save in RayData (!)
                rayData[rayDataCounter] = directSound;
                rayDataCounter++;
                continue;
            }
        }
    }
    #endregion

    #region Occlusion
    private void CastOcclusionRays()
    {
        for (int i = 0; i < sceneData.sources.Count; i++)
        {
            Source source = sceneData.sources[i];
            Vector3 sourcePos = source.position;
            Vector3 listenerPos = sceneData.listener.transform.position;
            Vector3 sourceOffsetDir = Vector3.Cross(sourcePos - listenerPos, Vector3.up).normalized; // facing left
            Vector3 listenerOffsetDir = Vector3.Cross(sourcePos - listenerPos, Vector3.up).normalized;

            // Adaptive Occlusion -> Adjust offset, so that they are not in a wall
            Vector3 listenerOffsetLeft = CastOffsetRay(listenerPos, listenerOffsetDir, listenerSideMaxOffset);
            Vector3 listenerOffsetRight = CastOffsetRay(listenerPos, -listenerOffsetDir, listenerSideMaxOffset);
            Vector3 sourceOffsetLeft = CastOffsetRay(sourcePos, sourceOffsetDir, sourceSideMaxOffset);
            Vector3 sourceOffsetRight = CastOffsetRay(sourcePos, -sourceOffsetDir, sourceSideMaxOffset);

            int sum = 0;
            if (CastOcclusionRay(listenerPos, sourcePos)) sum++; // Center-Center
            if (CastOcclusionRay(listenerPos, sourcePos + sourceOffsetRight)) sum++;  // Center-Right
            if (CastOcclusionRay(listenerPos, sourcePos + sourceOffsetLeft)) sum++;  // Center-Left
            if (CastOcclusionRay(listenerPos + listenerOffsetRight, sourcePos)) sum++;  // Right-Center
            if (CastOcclusionRay(listenerPos + listenerOffsetLeft, sourcePos)) sum++;  // Left-Center
            if (CastOcclusionRay(listenerPos + listenerOffsetRight, sourcePos + sourceOffsetRight)) sum++; // Right-Right
            if (CastOcclusionRay(listenerPos + listenerOffsetRight, sourcePos + sourceOffsetLeft)) sum++; // Right-Left
            if (CastOcclusionRay(listenerPos + listenerOffsetLeft, sourcePos + sourceOffsetLeft)) sum++; // Left-Left
            if (CastOcclusionRay(listenerPos + listenerOffsetLeft, sourcePos + sourceOffsetRight)) sum++; // Left-Right

            sceneData.UpdateLowpassFilter(source.mixerIndex, sum);
        }
    }

    private Vector3 CastOffsetRay(Vector3 origin, Vector3 direction, float offset)
    {
        float distance;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, offset, geometryLayer))
        {
            distance = hit.distance - 0.001f;
        }
        else
        {
            distance = offset;
        }
        return direction * distance;
    }

    private bool CastOcclusionRay(Vector3 origin, Vector3 target)
    {
        Vector3 rayVector = target - origin;
        if (Physics.Raycast(origin, rayVector.normalized, rayVector.magnitude, geometryLayer))
        {
            // if (drawOcclusion) Debug.DrawRay(origin, rayVector, debugOcclusionColor);
            return true;
        }
        else
        {
            // if (drawOcclusion) Debug.DrawRay(origin, rayVector, debugOcclusionColor2);
            return false;
        }
    }
    #endregion

    #region Image Source
    private void SetImageSourceBuffer()
    {
        NativeArray<ImageSource> imageSourcesFlat = new NativeArray<ImageSource>(sceneData.imageSources.Count * sceneData.triangles.Count, Allocator.Temp);
        for (int i = 0; i < sceneData.imageSources.Count; i++)
        {
            NativeArray<ImageSource>.Copy(sceneData.imageSources[i], 0, imageSourcesFlat, i * sceneData.triangles.Count, sceneData.triangles.Count);
        }
        imageSourceBuffer.SetData(imageSourcesFlat);
        rayShader.SetBuffer(ismKernel, "imageSourceBuffer", imageSourceBuffer);

        imageSourcesFlat.Dispose();
    }

    private int RoundUpToPower2(int i)
    {
        if (i < 1)
            throw new ArgumentException("Number must be greater than 0.");

        int power = 1;
        while (power < i)
        {
            power *= 2;
        }
        return power;
    }
    #endregion

    #region Source, Listener
    private void RemoveSource() // Called by Action (SceneData)
    {
        if (sceneData.sources.Count < 1)
        {
            sourceBufferEmpty = true;

            // DEBUGGING
            // Debug.Log("SceneData: No AudioSources, cannot create Buffer!");
            return;
        }

        sourceBuffer.SetData(sceneData.sources.ToArray());
        rayShader.SetBuffer(rtKernel, "sourceBuffer", sourceBuffer);
        rayShader.SetInt("numSources", sceneData.sources.Count);

        SetImageSourceBuffer();

        RunShader();
    }

    private void AddSource() // Called by Action (SceneData)
    {
        sourceBuffer.SetData(sceneData.sources.ToArray());
        rayShader.SetBuffer(rtKernel, "sourceBuffer", sourceBuffer);
        rayShader.SetBuffer(ismKernel, "sourceBuffer", sourceBuffer);
        rayShader.SetInt("numSources", sceneData.sources.Count);

        SetImageSourceBuffer();

        sourceBufferEmpty = false;

        RunShader();
    }

    private void UpdateListener() // Called by Action (SceneData)
    {
        rayShader.SetVector("listenerPos", sceneData.listener.transform.position + listenerOffset);

        RunShader();
    }
    #endregion

    #region Destroy
    void OnDestroy()
    {
        rayDataBuffer?.Release();
        directionBuffer?.Release();
        sourceBuffer?.Release();
        meshBuffer?.Release();
        triangleBuffer?.Release();
        bvhBuffer?.Release();
        counterBuffer?.Release();
        materialBuffer?.Release();
        imageSourceBuffer?.Release();
        // histogramBuffer?.Release();
    }
    #endregion

    #region Debug
    private void DebugRays()
    {
        Vector3 lisPos = sceneData.listener.transform.position;

        for (int i = 0; i < rayDataCounter; i++)
        {
            Color rayColor = useBVH ? debugRayBVHColor : debugRayMeshColor;
            rayColor.a = rayData[i].energy.e4 * 10000 * debugRayOpacity;
            Debug.DrawRay(lisPos, directions[rayData[i].directionIndex] * rayData[i].energy.e4 * 10000 * debugRayLength, rayColor);
        }
    }
    #endregion
}
