// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ghost\Assets\Scripts\Lodding\LoadingRuneEffects.cs
// Lines: full file

using UnityEngine;
using UnityEngine.UI;

public class LoadingRuneEffects : MonoBehaviour
{
    [Header("Progress")]
    public Slider progressSlider;

    [Header("Rune Gate")]
    public RectTransform outerRingTransform;
    public RectTransform innerRingTransform;
    public RectTransform coreTransform;
    public RectTransform particleRoot;
    public Image gateGlow;
    public Image outerRing;
    public Image innerRing;
    public Image core;
    public Image completionFlash;
    public Graphic[] orbitParticles;

    [Header("Motion")]
    public float outerRingSpeed = -22f;
    public float innerRingSpeed = 36f;
    public float particleOrbitSpeed = 48f;
    public float corePulseSpeed = 2.8f;
    public float corePulseAmount = 0.06f;

    [Header("Flash")]
    public float flashProgress = 0.985f;
    public float flashDuration = 0.45f;

    private Color gateGlowBaseColor;
    private Color outerRingBaseColor;
    private Color innerRingBaseColor;
    private Color coreBaseColor;
    private Vector3 coreBaseScale;
    private bool captured;
    private bool flashPlayed;
    private float flashTimer;

    private void Awake()
    {
        CaptureBaseValues();
        ApplyProgressVisual(GetProgress(), 0f);
    }

    private void OnEnable()
    {
        CaptureBaseValues();
        flashPlayed = false;
        flashTimer = 0f;
        if (completionFlash != null)
            completionFlash.color = WithAlpha(completionFlash.color, 0f);
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;
        float progress = GetProgress();

        Rotate(outerRingTransform, outerRingSpeed * deltaTime);
        Rotate(innerRingTransform, innerRingSpeed * deltaTime);
        Rotate(particleRoot, particleOrbitSpeed * deltaTime);

        if (coreTransform != null)
        {
            float pulse = 1f + Mathf.Sin(time * corePulseSpeed) * corePulseAmount;
            coreTransform.localScale = coreBaseScale * pulse;
        }

        ApplyProgressVisual(progress, time);
        UpdateCompletionFlash(progress, deltaTime);
    }

    private void CaptureBaseValues()
    {
        if (captured)
            return;

        gateGlowBaseColor = gateGlow != null ? gateGlow.color : Color.white;
        outerRingBaseColor = outerRing != null ? outerRing.color : Color.white;
        innerRingBaseColor = innerRing != null ? innerRing.color : Color.white;
        coreBaseColor = core != null ? core.color : Color.white;
        coreBaseScale = coreTransform != null ? coreTransform.localScale : Vector3.one;
        captured = true;
    }

    private float GetProgress()
    {
        if (progressSlider == null)
            return 0f;

        return Mathf.InverseLerp(progressSlider.minValue, progressSlider.maxValue, progressSlider.value);
    }

    private void ApplyProgressVisual(float progress, float time)
    {
        float glow = Mathf.SmoothStep(0.55f, 1.45f, progress);
        float breath = Mathf.Sin(time * 3.4f) * 0.08f;

        SetImageColor(gateGlow, gateGlowBaseColor, Mathf.Clamp01(0.2f + progress * 0.75f + breath));
        SetImageColor(outerRing, outerRingBaseColor, Mathf.Clamp01(0.65f + progress * 0.35f));
        SetImageColor(innerRing, innerRingBaseColor, Mathf.Clamp01(0.42f + progress * 0.58f));
        SetImageColor(core, coreBaseColor, Mathf.Clamp01(0.82f + glow * 0.18f));

        if (orbitParticles == null)
            return;

        for (int i = 0; i < orbitParticles.Length; i++)
        {
            Graphic particle = orbitParticles[i];
            if (particle == null)
                continue;

            float sparkle = (Mathf.Sin(time * 4.2f + i * 0.73f) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.18f, 0.85f, sparkle) * Mathf.Lerp(0.55f, 1f, progress);
            Color color = particle.color;
            color.a = alpha;
            particle.color = color;
        }
    }

    private void UpdateCompletionFlash(float progress, float deltaTime)
    {
        if (completionFlash == null)
            return;

        if (progress < flashProgress - 0.08f)
        {
            flashPlayed = false;
            flashTimer = 0f;
            completionFlash.color = WithAlpha(completionFlash.color, 0f);
            return;
        }

        if (progress >= flashProgress && !flashPlayed)
        {
            flashPlayed = true;
            flashTimer = 0f;
        }

        if (!flashPlayed || flashTimer > flashDuration)
            return;

        flashTimer += deltaTime;
        float normalized = Mathf.Clamp01(flashTimer / Mathf.Max(flashDuration, 0.01f));
        float alpha = Mathf.Sin(normalized * Mathf.PI) * 0.72f;
        completionFlash.color = WithAlpha(completionFlash.color, alpha);
    }

    private static void Rotate(RectTransform rectTransform, float zDelta)
    {
        if (rectTransform == null)
            return;

        Vector3 eulerAngles = rectTransform.localEulerAngles;
        eulerAngles.z += zDelta;
        rectTransform.localEulerAngles = eulerAngles;
    }

    private static void SetImageColor(Image image, Color baseColor, float intensity)
    {
        if (image == null)
            return;

        Color color = baseColor * intensity;
        color.a = baseColor.a * Mathf.Clamp01(intensity);
        image.color = color;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
