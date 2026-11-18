using UnityEngine;

public class SkyboxDayNightController : MonoBehaviour
{
    [Header("Lights")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;

    [Header("Day / Night Gradients (0 = Midnight, 0.25 = 6AM, 0.5 = Noon, 0.75 = 6PM)")]
    [SerializeField] private Gradient skyBackgroundColor;
    [SerializeField] private Gradient ambientLightColor;
    [SerializeField] private Gradient sunLightColor;

    [Header("Light Intensity Curves")]
    [SerializeField] private AnimationCurve sunIntensity = new AnimationCurve(
        new Keyframe(0.00f, 0.00f),
        new Keyframe(0.23f, 0.00f),
        new Keyframe(0.30f, 1.15f),
        new Keyframe(0.70f, 1.15f),
        new Keyframe(0.77f, 0.00f),
        new Keyframe(1.00f, 0.00f)
    );

    [SerializeField] private AnimationCurve moonIntensity = new AnimationCurve(
        new Keyframe(0.00f, 0.40f),
        new Keyframe(0.22f, 0.40f),
        new Keyframe(0.28f, 0.00f),
        new Keyframe(0.72f, 0.00f),
        new Keyframe(0.78f, 0.40f),
        new Keyframe(1.00f, 0.40f)
    );

    private Camera mainCam;

    private void Reset()
    {
        // Populate sensible defaults so Unity doesn't leave gradients all-white
        skyBackgroundColor = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.0f, 0.0f, 0.0f), 0.00f)
            }
        };

        ambientLightColor = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.0f, 0.0f, 0.0f), 0.0f)
            }
        };

        sunLightColor = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.0f, 0.0f, 0.0f), 0.0f)
            }
        };
    }

    private void Awake()
    {
        mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.SolidColor;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    }

    private void Update()
    {
        if (TimeManager.Instance == null) return;

        // 0.0 = 00:00, 0.25 = 06:00, 0.5 = 12:00, 0.75 = 18:00
        float t = Mathf.Repeat(TimeManager.Instance.getHour() / TimeManager.DAY_LENGTH, 1f);
        bool isDay = TimeManager.Instance.IsDayTime();

        Color skyCol = skyBackgroundColor.Evaluate(t);
        if (mainCam != null)
        {
            mainCam.backgroundColor = skyCol;
        }

        RenderSettings.ambientLight = ambientLightColor.Evaluate(t);
        if (RenderSettings.fog)
        {
            RenderSettings.fogColor = skyCol;
        }

        if (sunLight != null)
        {
            float sInt = sunIntensity.Evaluate(t);
            sunLight.intensity = sInt;
            sunLight.color = sunLightColor.Evaluate(t);
            sunLight.shadows = sInt > 0.05f ? LightShadows.Soft : LightShadows.None;
        }

        if (moonLight != null)
        {
            float mInt = moonIntensity.Evaluate(t);
            moonLight.intensity = mInt;
            moonLight.shadows = (!isDay && mInt > 0.05f) ? LightShadows.Soft : LightShadows.None;
        }
    }
}