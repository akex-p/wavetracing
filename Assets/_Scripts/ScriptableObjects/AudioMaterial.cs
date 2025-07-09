using UnityEngine;

[CreateAssetMenu(fileName = "AudioMaterial", menuName = "Scriptable Objects/AudioMaterial")]
public class AudioMaterial : ScriptableObject
{
    // Similar to Carl Schissler, Dinesh Maocha (2016)
    [Header("0-110Hz")]
    [Range(0, 1)]
    public float absorptionCoefficient110 = 0.0f;
    [Range(0, 1)]
    public float scatteringCoefficient110 = 0.0f;

    [Header("110-630Hz")]
    [Range(0, 1)]
    public float absorptionCoefficient630 = 0.0f;
    [Range(0, 1)]
    public float scatteringCoefficient630 = 0.0f;

    [Header("630-3500Hz")]
    [Range(0, 1)]
    public float absorptionCoefficient3500 = 0.0f;
    [Range(0, 1)]
    public float scatteringCoefficient3500 = 0.0f;

    [Header("3500-22050Hz")]
    [Range(0, 1)]
    public float absorptionCoefficient22050 = 0.0f;
    [Range(0, 1)]
    public float scatteringCoefficient22050 = 0.0f;

    public float getAbsorption(int frequency)
    {
        if (frequency < 0) return 0.0f;
        if (frequency <= 110) return absorptionCoefficient110;
        if (frequency <= 630) return absorptionCoefficient630;
        if (frequency <= 3500) return absorptionCoefficient3500;
        if (frequency <= 22050) return absorptionCoefficient22050;
        return 0.0f;
    }

    public float getScattering(int frequency)
    {
        if (frequency < 0) return 0.0f;
        if (frequency <= 110) return scatteringCoefficient110;
        if (frequency <= 630) return scatteringCoefficient630;
        if (frequency <= 3500) return scatteringCoefficient3500;
        if (frequency <= 22050) return scatteringCoefficient22050;
        return 0.0f;
    }
}
