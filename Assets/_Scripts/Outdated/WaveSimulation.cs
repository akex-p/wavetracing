using UnityEngine;
using UnityEngine.Rendering;

public class WaveSimulation : MonoBehaviour
{
    [Header("Waveguide Settings")]
    // TODO

    [Header("Debug Settings")]
    public float visualizationScale = 10.0f;
    public Gradient waveColorGradient;
    [Range(0, 49)]
    public int selectedY;

    [Header("References")]
    public ComputeShader waveShader;

    private ComputeBuffer waveBufferPrev;
    private ComputeBuffer waveBufferNext;
    private ComputeBuffer nodeTypeBuffer;
    private ComputeBuffer listenerOutputBuffer;

    private const float SpeedOfSound = 343.0f;
    private const float NodeDistance = 1f;
    private const float TimeStep = NodeDistance / (1.7320508f * SpeedOfSound);
    private const float D1 = 1f / 3f;

    private const int GridSize = 50;
    private const int TotalSize = GridSize * GridSize * GridSize;
    private const int MidIndex = GridSize / 2 * GridSize * GridSize + GridSize / 2 * GridSize + (GridSize / 2);
    private const int ThreadGroups = GridSize / 8;

    private int kernelID;
    private float timeStep = 0;
    private float[] waveData = new float[TotalSize];

    void Start()
    {
        InitializeShader();
    }

    private void InitializeShader()
    {
        waveBufferNext = new ComputeBuffer(TotalSize, sizeof(float));
        waveBufferPrev = new ComputeBuffer(TotalSize, sizeof(float));
        nodeTypeBuffer = new ComputeBuffer(TotalSize, sizeof(int));
        listenerOutputBuffer = new ComputeBuffer(1, sizeof(float));

        // Initialisiere Werte
        float[] initialWaveData = new float[TotalSize];
        int[] initialNodeTypes = new int[TotalSize];

        for (int z = 0; z < GridSize; z++)
        {
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    int index = x + y * GridSize + z * GridSize * GridSize;

                    // Standard-Wellenamplitude und Knotentyp
                    initialWaveData[index] = 0.0f;
                    initialNodeTypes[index] = 0; // Normaler Knoten

                    // Ränder definieren
                    if (x == 0 || y == 0 || z == 0 || x == GridSize - 1 || y == GridSize - 1 || z == GridSize - 1)
                    {
                        initialNodeTypes[index] = 1; // Absorbierender Randknoten
                    }
                }
            }
        }
        initialNodeTypes[MidIndex] = 2;

        waveBufferPrev.SetData(initialWaveData);
        waveBufferNext.SetData(initialWaveData);
        nodeTypeBuffer.SetData(initialNodeTypes);

        waveShader.SetBuffer(kernelID, "waveBufferPrev", waveBufferPrev);
        waveShader.SetBuffer(kernelID, "waveBufferNext", waveBufferNext);
        waveShader.SetBuffer(kernelID, "nodeTypeBuffer", nodeTypeBuffer);
        waveShader.SetBuffer(kernelID, "listenerOutputBuffer", listenerOutputBuffer);
        waveShader.SetInt("gridSize", GridSize);
        waveShader.SetFloat("waveSpeed", SpeedOfSound);
        waveShader.SetFloat("timeStep", TimeStep);
        waveShader.SetFloat("d1", D1);
    }

    void Update()
    {
        RunShader();
        DebugWave(selectedY);
    }

    private void RunShader()
    {
        float deltaTime = Time.deltaTime;
        timeStep += deltaTime;
        waveShader.SetFloat("timeStep", timeStep);

        waveShader.Dispatch(kernelID, ThreadGroups, ThreadGroups, ThreadGroups);

        waveBufferNext.GetData(waveData);

        RotateShader();

        waveShader.SetBuffer(kernelID, "waveBufferPrev", waveBufferPrev);
        waveShader.SetBuffer(kernelID, "waveBufferNext", waveBufferNext);
    }

    private void RotateShader()
    {
        var temp = waveBufferPrev;
        waveBufferPrev = waveBufferNext;
        waveBufferNext = temp;
    }

    void OnDestroy()
    {
        waveBufferPrev?.Release();
        waveBufferNext?.Release();
        nodeTypeBuffer?.Release();
        listenerOutputBuffer?.Release();
    }

    void DebugWave(int selectedY)
    {
        if (selectedY < 0 || selectedY >= GridSize)
        {
            Debug.LogError("Invalid selectedZ value. It must be between 0 and GridSize-1.");
            return;
        }

        for (int x = 0; x < GridSize; x++)
        {
            for (int z = 0; z < GridSize; z++)
            {
                int index = x + z * GridSize + selectedY * GridSize * GridSize;

                Vector3 startPosition = new Vector3(x * NodeDistance, selectedY, z * NodeDistance);
                Vector3 endPosition = new Vector3(x * NodeDistance, selectedY + waveData[index] * visualizationScale, z * NodeDistance);

                // Farbe basierend auf dem Wert
                float normalizedValue = Mathf.InverseLerp(-1.0f, 1.0f, waveData[index]);
                Color waveColor = waveColorGradient.Evaluate(normalizedValue);

                Debug.DrawLine(startPosition, endPosition, waveColor);
            }
        }
    }
}