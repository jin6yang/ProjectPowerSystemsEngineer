using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectProtocol.Rendering
{
    [ExecuteAlways]
    public sealed class PowerToonEnvironmentLightingController : MonoBehaviour
    {
        [Header("Sun")]
        public Light sun;
        public Color sunColor = new Color(1.0f, 0.91f, 0.72f, 1f);
        [Range(0f, 3f)] public float sunIntensity = 1.15f;
        [Range(0f, 1f)] public float shadowStrength = 0.58f;
        public Vector3 sunEulerAngles = new Vector3(50f, -35f, 0f);

        [Header("Ambient")]
        public AmbientMode ambientMode = AmbientMode.Flat;
        public Color ambientColor = new Color(0.28f, 0.34f, 0.40f, 1f);
        [Range(0f, 1f)] public float reflectionIntensity = 0.18f;

        [Header("Fog")]
        public bool enableFog = true;
        public FogMode fogMode = FogMode.Linear;
        public Color fogColor = new Color(0.60f, 0.70f, 0.78f, 1f);
        public float fogStartDistance = 24f;
        public float fogEndDistance = 90f;
        [Range(0f, 0.1f)] public float fogDensity = 0.01f;

        [Header("Apply")]
        public bool applyInEditMode = true;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            if (applyInEditMode)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null) Apply();
                };
#else
                Apply();
#endif
            }
        }

        public void Apply()
        {
            if (sun == null)
            {
                sun = RenderSettings.sun;
            }

            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = shadowStrength;
                sun.transform.rotation = Quaternion.Euler(sunEulerAngles);
                RenderSettings.sun = sun;
            }

            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.fog = enableFog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = Mathf.Max(fogStartDistance + 0.01f, fogEndDistance);
            RenderSettings.fogDensity = fogDensity;
        }
    }
}
