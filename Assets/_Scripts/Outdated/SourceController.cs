using System.Collections;
using UnityEngine;

public class SourceController : MonoBehaviour
{
    [Header("Audio Settings")]
    [Range(0.1f, 20f)]
    [SerializeField] public float radius = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] private float interpolationSpeed = 0.2f;

    private AudioLowPassFilter audioLowPassFilter;

    void Awake()
    {
        audioLowPassFilter = GetComponent<AudioLowPassFilter>();
    }

    public void UpdateLowpassFilter(int collided)
    {
        float cutOff = 22000f - (2200f * collided);
        StartCoroutine(InterpolateFrequency(audioLowPassFilter.cutoffFrequency, cutOff));
    }

    // Interpolations
    private IEnumerator InterpolateFrequency(float start, float end)
    {
        float timeElapsed = 0;

        while (timeElapsed < interpolationSpeed)
        {
            float t = timeElapsed / interpolationSpeed;
            audioLowPassFilter.cutoffFrequency = Mathf.Lerp(start, end, t);
            timeElapsed += Time.deltaTime;

            yield return null;
        }

        audioLowPassFilter.cutoffFrequency = end;
    }
}
