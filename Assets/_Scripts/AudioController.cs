using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class AudioController : MonoBehaviour
{
    [Header("IR Settings")]
    [Range(0f, 5f)]
    [SerializeField] public float sampleLength = 1f;
    [Range(0, 48000)]
    [SerializeField] private int sampleRate = 44100;
    [SerializeField] public float binSize = 0.005f; // in s

    [Header("Weight Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float weightLo;
    [Range(0f, 1f)]
    [SerializeField] private float weightMidLo;
    [Range(0f, 1f)]
    [SerializeField] private float weightMidHi;
    [Range(0f, 1f)]
    [SerializeField] private float weightHi;

    [Header("Debug Settings")]
    [Range(0, 10000)]
    [SerializeField] private int resolutionForCoolDebugRender = 10000;

    [Header("References")]
    [SerializeField] private SceneData sceneData;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private GraphRenderer graphRenderer;

    public const float SpeedOfSound = 343.0f;
    private const int windowSize = 32;

    private const float BW1 = 110;
    private const float BW2 = 520;
    private const float BW3 = 2870;
    private const float BW4 = 18550;
    private float rightRoot1;
    private float rightRoot2;
    private float rightRoot3;
    private float rightRoot4;

    private double[] poisson1;
    private double[] poisson2;
    private double[] poisson3;
    private double[] poisson4;
    private double[] ir;

    private bool updated = false;
    private int numBins;

    private double[][] irs;
    private float[] hannWindow;
    private ZeroTrackedArray[] irs2;
    private float[] renderData;

    #region Init
    void Start()
    {
        InitFrequencyBands();
        InitRespones();
        InitWindows();
        // InitPoisson();

        renderData = new float[0];
    }

    private void InitFrequencyBands()
    {
        rightRoot1 = Mathf.Sqrt(BW1 / (float)(sampleRate / 2));
        rightRoot2 = Mathf.Sqrt(BW2 / (float)(sampleRate / 2));
        rightRoot3 = Mathf.Sqrt(BW3 / (float)(sampleRate / 2));
        rightRoot4 = Mathf.Sqrt(BW4 / (float)(sampleRate / 2));
    }

    private void InitRespones()
    {
        // numBins = Mathf.CeilToInt(sampleLength * sampleRate);
        numBins = 65536;
        irs2 = new ZeroTrackedArray[sceneData.maxSources];
        for (int i = 0; i < sceneData.maxSources; i++)
        {
            irs2[i] = new ZeroTrackedArray(numBins);
        }
    }

    private void InitWindows()
    {
        hannWindow = new float[windowSize];
        for (int n = 0; n < windowSize; n++)
        {
            hannWindow[n] = 0.5f * (1 - Mathf.Cos(2 * Mathf.PI * n / (windowSize - 1)));
        }
    }

    private void InitPoisson()
    {
        NativeArray<double> poisson = new NativeArray<double>(GeneratePoissonProcess(10), Allocator.Temp);
        poisson1 = FftSharp.Filter.BandPass(poisson.ToArray(), sampleRate, 0, 110);
        poisson2 = FftSharp.Filter.BandPass(poisson.ToArray(), sampleRate, 110, 630);
        poisson3 = FftSharp.Filter.BandPass(poisson.ToArray(), sampleRate, 630, 3500);
        poisson4 = FftSharp.Filter.BandPass(poisson.ToArray(), sampleRate, 3500, 22050);
        poisson.Dispose();
        ir = new double[FixedLength];
    }
    #endregion

    #region Refresh
    void Update()
    {
        RefreshParameters(updated);
        updated = false;

        // if (graphRenderer) graphRenderer.PlotGraph(renderData, 0, resolutionForCoolDebugRender);
    }

    private void RefreshParameters(bool updated)
    {
        if (updated)
        {
            foreach (Source source in sceneData.sources)
            {
                audioMixer.SetFloat("Refresh0" + source.mixerIndex, 0.1f);
            }
        }
        else
        {
            foreach (Source source in sceneData.sources)
            {
                audioMixer.SetFloat("Refresh0" + source.mixerIndex, 0.0f);
            }
        }
    }

    public void DrawHistograms(EnergyBand[] histograms)
    {
        PlotGraph(histograms, new Vector3(0f, 1.0f, 0f), 0, histograms.Length, Color.red, 1000f, 0.01f);
    }
    #endregion

    #region Impulse Response
    public void UpdateIRs(RayData[] rayInfo, int counter)
    {
        if (sceneData.sources.Count > 0)
        {
            ClearIRs2();
            foreach (Source source in sceneData.sources)
            {
                int index = sceneData.sources.IndexOf(source);
                GWIRZ(rayInfo, counter, index, source.mixerIndex);

                if (irs2[source.mixerIndex].IsZeroed()) continue; // skip empty IRs

                UpdateConvolutionReverb(source.mixerIndex, irs2[source.mixerIndex].ToFloatArray(), 1, "IR for Source " + source.mixerIndex);
            }
            // renderData = SampleDownTarget(irs2[0].ToFloatArray(), (int)sampleLength * resolutionForCoolDebugRender);
        }
    }

    private void ClearIRs() // Clear Double Array
    {
        for (int mixerIndex = 0; mixerIndex < irs.Length; mixerIndex++)
        {
            Array.Clear(irs[mixerIndex], 0, numBins);
        }
    }

    private void ClearIRs2() // Clear ZeroTrackedArray
    {
        for (int mixerIndex = 0; mixerIndex < irs2.Length; mixerIndex++)
        {
            irs2[mixerIndex].Clear();
        }
    }

    private void GIRD(RayData[] rays, int counter, int index, int mixerIndex) // generate IR for Double Array
    {
        for (int i = 0; i < counter; i++)
        {
            RayData ray = rays[i];

            if (ray.sourceIndex != index) continue;

            double arrivalTime = ray.distance / SpeedOfSound;
            int binIndex = (int)(arrivalTime * sampleRate);

            if (binIndex >= numBins) continue;

            double amplitude = (double)Mathf.Sqrt(ray.energy.e1);
            double phase = (double)UnityEngine.Random.Range(0f, 2f * Mathf.PI);

            for (int w = 0; w < windowSize; w++)
            {
                int idx = binIndex + w;
                if (idx < numBins)
                {
                    double value = amplitude * hannWindow[w] * Math.Cos(phase);
                    irs[mixerIndex][binIndex] += value;
                }
            }
        }
    }

    private void GIRZ(RayData[] rays, int counter, int index, int mixerIndex) // generate IR for ZeroTrackedArray
    {
        for (int i = 0; i < counter; i++)
        {
            RayData ray = rays[i];

            if (ray.sourceIndex != index) continue;

            float arrivalTime = ray.distance / SpeedOfSound;
            int binIndex = (int)(arrivalTime * sampleRate);

            if (binIndex >= numBins) continue;

            float amplitude = Mathf.Sqrt(ray.energy.e1);
            float phase = UnityEngine.Random.Range(0f, 2f * Mathf.PI);

            for (int w = 0; w < windowSize; w++)
            {
                int idx = binIndex + w;
                if (idx < numBins)
                {
                    float value = amplitude * hannWindow[w] * Mathf.Cos(phase);
                    irs2[mixerIndex][binIndex] += value;
                }
            }
        }
    }

    private void GWIRZ(RayData[] rays, int counter, int index, int mixerIndex) // generate simply weighted IR for ZeroTrackedArray
    {
        for (int i = 0; i < counter; i++)
        {
            RayData ray = rays[i];

            if (ray.sourceIndex != index) continue;

            float arrivalTime = ray.distance / SpeedOfSound;
            int binIndex = (int)(arrivalTime * sampleRate);

            if (binIndex >= numBins) continue;

            // amplitude
            float amplitude1 = Mathf.Sqrt(ray.energy.e1);
            float amplitude2 = Mathf.Sqrt(ray.energy.e2);
            float amplitude3 = Mathf.Sqrt(ray.energy.e3);
            float amplitude4 = Mathf.Sqrt(ray.energy.e4);

            float phase = Mathf.Cos(UnityEngine.Random.Range(0f, 2f * Mathf.PI));

            float value = amplitude1 * rightRoot1 * weightLo
                        + amplitude2 * rightRoot2 * weightMidLo
                        + amplitude3 * rightRoot3 * weightMidHi
                        + amplitude4 * rightRoot4 * weightHi;

            for (int w = 0; w < windowSize; w++)
            {
                int idx = binIndex + w;
                if (idx < numBins)
                {
                    float phased = value * phase * hannWindow[w];
                    irs2[mixerIndex][binIndex] += phased;
                }
            }
        }
    }
    #endregion

    #region Poisson
    private const int FixedLength = 65536; // Fixed output length

    public double[] GeneratePoissonProcess(double roomVolume)
    {
        double totalTime = FixedLength / sampleRate; // Derived from fixed length
        double t0 = Math.Pow((2 * Math.Pow(2, Math.Log(2))) / (4 * Math.PI * Math.Pow(343, 3)), 1.0 / 3.0);
        NativeArray<double> randSeq = new NativeArray<double>(FixedLength, Allocator.Temp);

        double t = t0;
        System.Random rand = new System.Random();

        while (t < totalTime)
        {
            int index = (int)Math.Round(t * sampleRate);
            if (index < FixedLength)
            {
                double polarity = (Math.Round(t * sampleRate) - t * sampleRate) < 0 ? 1 : -1;
                randSeq[index] = polarity;
            }

            // Event occurrence rate
            double mu = Math.Min(1e4, 4 * Math.PI * Math.Pow(343, 2) * t / (2 * roomVolume));
            // Interval size
            double deltaTA = (1.0 / mu) * Math.Log(1.0 / rand.NextDouble());

            t += deltaTA;
        }
        return randSeq.ToArray();
    }

    public void GIRP(EnergyBand[] histograms)
    {
        float size = binSize * sampleRate;
        double epsilon = 1e-8;

        for (int i = 0; i < poisson1.Length; i++)
        {
            int k = Mathf.FloorToInt(((float)i) / size);
            if (k >= Mathf.RoundToInt(sampleLength / binSize)) break;

            int gk0 = (int)Math.Floor(k * sampleRate * binSize);
            int gk1 = (int)Math.Floor((k + 1) * sampleRate * binSize);
            gk1 = Math.Min(gk1, poisson1.Length);

            double vSquare1 = 0, vSquare2 = 0, vSquare3 = 0, vSquare4 = 0;

            for (int n = gk0; n < gk1; n++)
            {
                vSquare1 += poisson1[n] * poisson1[n];
                vSquare2 += poisson2[n] * poisson2[n];
                vSquare3 += poisson3[n] * poisson3[n];
                vSquare4 += poisson4[n] * poisson4[n];
            }

            ir[i] = (poisson1[i] * Math.Sqrt(histograms[k].e1 / (vSquare1 + epsilon)) * rightRoot1) +
                    (poisson2[i] * Math.Sqrt(histograms[k].e2 / (vSquare2 + epsilon)) * rightRoot2) +
                    (poisson3[i] * Math.Sqrt(histograms[k].e3 / (vSquare3 + epsilon)) * rightRoot3) +
                    (poisson4[i] * Math.Sqrt(histograms[k].e4 / (vSquare4 + epsilon)) * rightRoot4);
        }
    }
    #endregion

    #region Sampling
    public float[] SampleDown(float[] input, int fac)
    {
        float[] downsampled = new float[input.Length / fac];
        for (int i = 0; i < downsampled.Length; i++)
        {
            downsampled[i] = input[i * fac];
        }

        return downsampled;
    }

    public float[] SampleDownTarget(float[] input, int targetSize)
    {
        if (input == null || input.Length <= targetSize)
            return input;

        float[] downsampled = new float[targetSize];
        int step = input.Length / targetSize;

        for (int i = 0; i < targetSize; i++)
        {
            downsampled[i] = input[i * step];
        }

        return downsampled;
    }

    public float[] SampleUpLinear(float[] input, int fac)
    {
        float[] upsampled = new float[input.Length * fac];
        for (int i = 0; i < input.Length - 1; i++)
        {
            for (int j = 0; j < fac; j++)
            {
                float t = j / (float)fac;
                upsampled[i * fac + j] = Mathf.Lerp(input[i], input[i + 1], t);
            }
        }

        return upsampled;
    }
    #endregion

    #region Fourier
    public float[] IFFT(float[] real, float[] imag)
    {
        uint windowSize = (uint)real.Length;
        uint n = windowSize;
        uint j = n / 2;
        float temp;

        for (uint i = 1; i < windowSize - 2; i++)
        {
            imag[i] = -imag[i];

            if (i < j)
            {
                temp = real[j];
                real[j] = real[i];
                real[i] = temp;

                temp = imag[j];
                imag[j] = imag[i];
                imag[i] = temp;
            }

            uint k = windowSize >> 1;
            while (k <= j)
            {
                j -= k;
                k >>= 1;
            }
            j += k;
        }

        uint windowEnd = 1;

        uint bitCount = (uint)Mathf.Log(windowSize, 2);
        for (uint lp = 0; lp < bitCount; lp++)
        {
            float re = 1.0f;
            float im = 0.0f;

            float c = Mathf.Cos(Mathf.PI / windowEnd);
            float s = -Mathf.Sin(Mathf.PI / windowEnd);
            float tsr, tsi;

            for (j = 0; j < windowEnd; j++)
            {
                for (uint i = j; i < n; i += windowEnd * 2)
                {
                    uint k = i + windowEnd;

                    tsr = real[k] * re - imag[k] * im;
                    tsi = real[k] * im + imag[k] * re;

                    real[k] = real[i] - tsr;
                    imag[k] = imag[i] - tsi;
                    real[i] = real[i] + tsr;
                    imag[i] = imag[i] + tsi;
                }

                tsr = re;
                re = tsr * c - im * s;
                im = tsr * s + im * c;
            }

            windowEnd <<= 1;
        }

        for (uint i = 0; i < n; i++)
        {
            real[i] = real[i] / n;
        }

        return real;
    }
    #endregion

    #region AudioPlugin
    [DllImport("AudioPluginDemo")]
    private static extern bool ConvolutionReverb_UploadSample(int index, float[] data, int numsamples, int numchannels, int samplerate, [MarshalAs(UnmanagedType.LPStr)] string name);

    public void UpdateConvolutionReverb(int index, float[] ir, int channels, string name)
    {
        // Debug.Log("AudioController: Convolution Filter updated.");
        ConvolutionReverb_UploadSample(index, ir, ir.Length / channels, channels, sampleRate, name);
        updated = true;
    }
    #endregion

    #region Debug
    void PlotGraph<T>(T[] input, UnityEngine.Vector3 offset, int startValue, int maxValue, Color color, float scale, float step = 0.001f)
    where T : struct, IComparable
    {
        for (int i = startValue; i < maxValue; i++)
        {
            float yOffset = Convert.ToSingle(input[i]) * scale; // convert to float for compatibility
            Debug.DrawLine(
                new UnityEngine.Vector3(i * step + offset.x, offset.y, offset.z),
                new UnityEngine.Vector3(i * step + offset.x, offset.y + yOffset, offset.z),
                color
            );
        }
    }

    void PlotGraph(EnergyBand[] input, UnityEngine.Vector3 offset, int startValue, int maxValue, Color color, float scale, float step = 0.001f) // takes e1
    {
        for (int i = startValue; i < maxValue; i++)
        {
            float yOffset = Convert.ToSingle(input[i].e1) * scale; // convert to float for compatibility
            Debug.DrawLine(
                new UnityEngine.Vector3(i * step + offset.x, offset.y, offset.z),
                new UnityEngine.Vector3(i * step + offset.x, offset.y + yOffset, offset.z),
                color
            );
        }
    }
    #endregion
}
