using ARCharacter.Core;
using UnityEngine;

namespace ARCharacter
{
    /// <summary>
    /// Персонаж смотрит на камеру через встроенный Humanoid IK Unity: один вызов
    /// SetLookAtWeight разом задаёт вклад головы, глаз и тела — доворот тела небольшой,
    /// чтобы не было эффекта «совиной головы». Отключается на Flat/Flattening/StandingUp:
    /// лежащему или встающему персонажу смотреть в камеру незачем, а IK поверх скелета,
    /// сжатого неравномерным масштабом при расплющивании, выглядел бы криво.
    /// </summary>
    public sealed class HeadLookController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private CharacterMotor _motor;
        [SerializeField] private EnvironmentBehaviour _environment;

        [Header("Веса IK")]
        [Range(0f, 1f)] [SerializeField] private float _headWeight = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float _eyesWeight = 0.4f;
        [Range(0f, 1f)] [SerializeField] private float _bodyWeight = 0.15f;

        [Tooltip("Ограничивает поворот при экстремальных углах, чтобы шея не выворачивалась.")]
        [Range(0f, 1f)] [SerializeField] private float _clampWeight = 0.5f;

        [Tooltip("Скорость нарастания/спада общего веса взгляда при включении и отключении.")]
        [SerializeField, Min(0f)] private float _fadeSpeed = 5f;

        private float _weight;

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || _environment == null)
                return;

            bool active = _motor != null && IsLookEligible(_motor.State);
            float target = active ? 1f : 0f;
            _weight = Mathf.Lerp(_weight, target, 1f - Mathf.Exp(-_fadeSpeed * Time.deltaTime));

            _animator.SetLookAtWeight(_weight, _bodyWeight, _headWeight, _eyesWeight, _clampWeight);

            if (_weight > 0.001f)
                _animator.SetLookAtPosition(_environment.CameraPose.position);
        }

        private static bool IsLookEligible(PostureState state)
            => state == PostureState.Standing || state == PostureState.Levitating;
    }
}
