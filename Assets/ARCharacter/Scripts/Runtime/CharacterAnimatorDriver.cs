using ARCharacter.Core;
using UnityEngine;

namespace ARCharacter
{
    /// <summary>
    /// Переводит состояние CharacterMotor в параметры Animator. Держится строго после
    /// мотора по порядку исполнения, чтобы читать состояние, посчитанное в этом же кадре.
    /// Триггер шлётся только на смену PostureState, а не каждый кадр, — иначе Any State
    /// переход в Mecanim перезапускал бы клип заново, пока условие остаётся истинным.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class CharacterAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private CharacterMotor _motor;
        [SerializeField] private Animator _animator;

        [Header("Сглаживание")]
        [Tooltip("Максимальная скорость изменения параметра Speed, единиц в секунду.")]
        [SerializeField, Min(0f)] private float _speedDamping = 4f;

        [Header("Вариации простоя")]
        [Tooltip("Через сколько секунд бездействия запускается случайный жест — нижняя граница диапазона.")]
        [SerializeField, Min(0f)] private float _idleVariationMinDelay = 5f;

        [Tooltip("Верхняя граница диапазона.")]
        [SerializeField, Min(0f)] private float _idleVariationMaxDelay = 10f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int FlattenParam = Animator.StringToHash("Flatten");
        private static readonly int StandUpParam = Animator.StringToHash("StandUp");
        private static readonly int LevitateParam = Animator.StringToHash("Levitate");
        private static readonly int StandParam = Animator.StringToHash("Stand");
        private static readonly int IdleVariationParam = Animator.StringToHash("IdleVariation");
        private static readonly int IdleVariationIndexParam = Animator.StringToHash("IdleVariationIndex");

        private PostureState _lastPosture;
        private bool _hasLastPosture;
        private float _speedValue;
        private float _idleTimer;
        private float _idleThreshold;

        private void OnEnable() => RollNextIdleThreshold();

        private void Update()
        {
            if (_motor == null || _animator == null)
                return;

            PostureState current = _motor.State;
            if (!_hasLastPosture)
            {
                _lastPosture = current;
                _hasLastPosture = true;
            }
            else if (current != _lastPosture)
            {
                FireTransitionTrigger(current);
                _lastPosture = current;
            }

            UpdateSpeed();
            UpdateIdleVariation(current);
        }

        private void FireTransitionTrigger(PostureState state)
        {
            switch (state)
            {
                case PostureState.Flattening:
                    _animator.SetTrigger(FlattenParam);
                    break;
                case PostureState.StandingUp:
                    _animator.SetTrigger(StandUpParam);
                    break;
                case PostureState.Standing:
                    _animator.SetTrigger(StandParam);
                    break;
                case PostureState.Levitating:
                    _animator.SetTrigger(LevitateParam);
                    break;
                case PostureState.Flat:
                    // Flattening и Flat — один и тот же аниматорный стейт: Mecanim сам
                    // держит последний кадр одноразового клипа падения, отдельная
                    // поза лежания не нужна и повторный триггер её не запускал бы.
                    break;
                case PostureState.Falling:
                    // Тот же стейт, что и Levitating: персонаж отпущен и падает,
                    // но визуально это тот же зацикленный клип парения/барахтанья.
                    break;
            }
        }

        private void UpdateSpeed()
        {
            float target = Mathf.Abs(_motor.RunIntensity);
            _speedValue = Mathf.MoveTowards(_speedValue, target, _speedDamping * Time.deltaTime);
            _animator.SetFloat(SpeedParam, _speedValue);
        }

        private void UpdateIdleVariation(PostureState current)
        {
            bool eligible = current == PostureState.Standing && _speedValue < 0.01f;
            if (!eligible)
            {
                _idleTimer = 0f;
                return;
            }

            _idleTimer += Time.deltaTime;
            if (_idleTimer < _idleThreshold)
                return;

            _animator.SetInteger(IdleVariationIndexParam, Random.Range(0, 3));
            _animator.SetTrigger(IdleVariationParam);
            _idleTimer = 0f;
            RollNextIdleThreshold();
        }

        private void RollNextIdleThreshold()
            => _idleThreshold = Random.Range(_idleVariationMinDelay, _idleVariationMaxDelay);
    }
}
