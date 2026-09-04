using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace ARCharacter.AR
{
    /// <summary>
    /// Подстраивает направленный свет и ambient-освещение сцены под реальную комнату
    /// по данным ARCore. Без этого персонаж освещён одинаково независимо от того,
    /// светло вокруг или темно, — а это одно из первых, что выдаёт «наклейку поверх
    /// видео» вместо объекта, действительно стоящего в комнате.
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public sealed class LightEstimationSetup : MonoBehaviour
    {
        [SerializeField] private Light _targetLight;

        [Tooltip("Интенсивность света при оценке яркости сцены, равной 1.0 (условная единица ARCore, не люксы).")]
        [SerializeField, Min(0f)] private float _baseIntensity = 1.2f;

        [Tooltip("Скорость сглаживания смены яркости/цвета — оценка ARCore шумит от кадра к кадру.")]
        [SerializeField, Min(0f)] private float _smoothing = 3f;

        private ARCameraManager _cameraManager;
        private float _intensity;
        private Color _color = Color.white;

        private void Awake() => _cameraManager = GetComponent<ARCameraManager>();

        private void OnEnable()
        {
            _cameraManager.requestedLightEstimation = LightEstimation.MainLightDirection
                | LightEstimation.MainLightIntensity
                | LightEstimation.AmbientSphericalHarmonics;
            _cameraManager.frameReceived += OnFrameReceived;
        }

        private void OnDisable() => _cameraManager.frameReceived -= OnFrameReceived;

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (_targetLight == null)
                return;

            ARLightEstimationData estimation = args.lightEstimation;
            float lerp = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);

            if (estimation.averageMainLightBrightness.HasValue)
            {
                float targetIntensity = estimation.averageMainLightBrightness.Value * _baseIntensity;
                _intensity = Mathf.Lerp(_intensity, targetIntensity, lerp);
                _targetLight.intensity = _intensity;
            }

            if (estimation.mainLightColor.HasValue)
            {
                _color = Color.Lerp(_color, estimation.mainLightColor.Value, lerp);
                _targetLight.color = _color;
            }

            if (estimation.mainLightDirection.HasValue)
            {
                // mainLightDirection — «откуда светит», а свет должен смотреть «куда светит».
                // Если тени лягут не в ту сторону — знак здесь первое, что нужно поменять.
                _targetLight.transform.rotation = Quaternion.LookRotation(-estimation.mainLightDirection.Value);
            }

            if (estimation.ambientSphericalHarmonics.HasValue)
            {
                RenderSettings.ambientMode = AmbientMode.Custom;
                RenderSettings.ambientProbe = estimation.ambientSphericalHarmonics.Value;
            }
        }
    }
}
