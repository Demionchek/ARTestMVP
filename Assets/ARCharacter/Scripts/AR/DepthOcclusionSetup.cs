using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARCharacter.AR
{
    /// <summary>
    /// Включает окклюзию по глубине там, где устройство её поддерживает, и мягко
    /// откатывается на «без окклюзии», если нет — S21 Ultra Depth API поддерживает,
    /// но код не должен падать или зависать на менее производительных устройствах.
    /// Дескриптор подсистемы появляется только после старта AR-сессии, поэтому
    /// поддержка проверяется каждый кадр до первого успеха, а не один раз в Start.
    /// </summary>
    [RequireComponent(typeof(AROcclusionManager))]
    public sealed class DepthOcclusionSetup : MonoBehaviour
    {
        [Tooltip("Best даёт больше деталей, но и больше шума на границах окклюзии («рябь») на слабом железе — Medium стабильнее.")]
        [SerializeField] private EnvironmentDepthMode _preferredMode = EnvironmentDepthMode.Medium;
        [SerializeField] private bool _preferTemporalSmoothing = true;

        private AROcclusionManager _occlusion;
        private bool _configured;

        private void Awake() => _occlusion = GetComponent<AROcclusionManager>();

        private void Update()
        {
            if (_configured)
                return;

            XROcclusionSubsystemDescriptor descriptor = _occlusion.descriptor;
            if (descriptor == null)
                return;

            bool depthSupported = descriptor.environmentDepthImageSupported == Supported.Supported;

            _occlusion.requestedEnvironmentDepthMode = depthSupported
                ? _preferredMode
                : EnvironmentDepthMode.Disabled;

            _occlusion.requestedOcclusionPreferenceMode = depthSupported
                ? OcclusionPreferenceMode.PreferEnvironmentOcclusion
                : OcclusionPreferenceMode.NoOcclusion;

            // Сеттер сам не даст запросить сглаживание, если платформа его не поддерживает
            // (проверяет descriptor внутри), поэтому здесь достаточно попытаться всегда.
            bool smoothingSupported = descriptor.environmentDepthTemporalSmoothingSupported == Supported.Supported;
            if (depthSupported)
                _occlusion.environmentDepthTemporalSmoothingRequested = _preferTemporalSmoothing;

            _configured = true;

            Debug.Log(depthSupported
                ? $"Depth API поддерживается, окклюзия включена: {_preferredMode}, " +
                  $"временное сглаживание {(smoothingSupported ? "поддерживается" : "НЕ поддерживается этим устройством")}"
                : "Depth API не поддерживается этим устройством — окклюзия отключена.");
        }
    }
}
