using ARCharacter.Core;
using UnityEngine;

namespace ARCharacter
{
    /// <summary>
    /// Сводит воедино две независимые оси поведения: вертикаль вьюпорта задаёт позу,
    /// горизонталь — бег к центру экрана. Позиция считается кодом и прижимается к границе
    /// поверхности, поэтому персонаж не убегает за пределы распознанной плоскости.
    /// </summary>
    [DefaultExecutionOrder(0)]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private PerceptionProbe _probe;
        [SerializeField] private EnvironmentBehaviour _environment;

        [Tooltip("Дочерний трансформ с визуалом. Масштабируется при расплющивании.")]
        [SerializeField] private Transform _visual;

        [Header("Полосы поз (вертикаль вьюпорта)")]
        [Tooltip("Низ экрана — камера поднята, персонаж парит.")]
        [SerializeField] private Band _levitatingBand = new Band(0f, 0.32f);

        [SerializeField] private Band _standingBand = new Band(0.26f, 0.80f);

        [Tooltip("Верх экрана — камера опущена, персонаж лежит.")]
        [SerializeField] private Band _flatBand = new Band(0.74f, 1f);

        [Tooltip("Длительность непрерываемого падения, секунд.")]
        [SerializeField, Min(0.01f)] private float _flattenDuration = 0.9f;

        [Tooltip("Длительность непрерываемого вставания, секунд.")]
        [SerializeField, Min(0.01f)] private float _standUpDuration = 5f;

        [Header("Бег (горизонталь вьюпорта)")]
        [Tooltip("Отклонение от центра, при котором персонаж начинает бежать.")]
        [SerializeField, Range(0.01f, 0.5f)] private float _startOffset = 0.16f;

        [Tooltip("Отклонение, при котором он останавливается. Разница со стартом — гистерезис.")]
        [SerializeField, Range(0f, 0.5f)] private float _stopOffset = 0.06f;

        [Tooltip("Минимальная доля скорости у порога остановки.")]
        [SerializeField, Range(0f, 1f)] private float _minIntensity = 1f;

        [Tooltip("Скорость бега, метров в секунду. Для миниатюры 40 см это доли метра.")]
        [SerializeField, Min(0f)] private float _runSpeed = 0.35f;

        [SerializeField, Min(0f)] private float _turnSpeed = 720f;

        [Header("Парение")]
        [SerializeField, Min(0f)] private float _maxLevitationHeight = 1.2f;
        [SerializeField, Min(0f)] private float _heightSmoothing = 6f;

        [Tooltip("Ускорение падения, м/с². Реальная гравитация, а не сглаживание к нулю: падение должно набирать скорость, а не тормозить перед землёй.")]
        [SerializeField, Min(0f)] private float _fallGravity = 4f;

        [Header("Расплющивание")]
        [Tooltip("Во сколько раз сжимается визуал по высоте в позе лёжа.")]
        [SerializeField, Range(0.05f, 1f)] private float _squashScale = 1f;

        [SerializeField, Min(0f)] private float _squashSmoothing = 8f;

        [Tooltip("Высота точки замера перцепции над опорой при полном росте (не зависит от того, как визуально выровнена модель). Сжимается пропорционально расплющиванию.")]
        [SerializeField, Min(0f)] private float _standingSampleHeight = 0.17f;

        [Tooltip("Постоянный зазор над поверхностью, метров. В AR компенсирует шум оценки глубины у самой земли: без него окклюзия иногда «срезает» стоящего персонажа, будто он проваливается в пол. В Sandbox не нужен — там пол настоящая геометрия, оставляйте 0.")]
        [SerializeField, Min(0f)] private float _groundClearance = 0f;

        // Ниже этой высоты падение (Falling) считается завершённым и сбивает персонажа
        // с ног — величина техническая, не связана с ощущениями, поэтому не в инспекторе.
        private const float LandingHeightThreshold = 0.01f;

        private readonly PostureResolver _posture = new PostureResolver();
        private readonly PostureStateMachine _stateMachine = new PostureStateMachine();
        private readonly LocomotionResolver _locomotion = new LocomotionResolver();

        private Vector3 _groundPosition;
        private Vector3 _lastPlaneRight = Vector3.right;
        private Vector3 _lastHeldForward = Vector3.forward;
        private Vector3 _lastHeldRight = Vector3.right;
        private float _heldForwardOffset;
        private float _heldRightOffset;
        private float _fallVelocity;
        private bool _hasHeldOffset;
        private float _height;
        private float _squash = 1f;
        private Vector3 _visualBaseScale = Vector3.one;
        private Vector3 _visualBasePosition;

        public PostureState State => _stateMachine.State;

        public float RunIntensity { get; private set; }

        public float Height => _height;

        /// <summary>
        /// Высота точки замера перцепции с учётом расплющивания. Она не связана
        /// с тем, как визуально выровнена модель (<see cref="_visualBasePosition"/> —
        /// это смещение меша, для гуманоида с ногами в нуле оно попросту нулевое):
        /// без отдельного отслеживания точка замера оставалась бы на высоте стоящего
        /// персонажа, и камера, опустившаяся ниже неё, проецировала бы её вырожденно —
        /// резким скачком выбрасывая сигнал за пределы полосы Flat.
        /// </summary>
        public float VisualHeightOffset => _standingSampleHeight * _squash;

        private void Awake()
        {
            _groundPosition = transform.position;
            if (_visual == null)
                return;

            _visualBaseScale = _visual.localScale;
            _visualBasePosition = _visual.localPosition;
        }

        private void Update()
        {
            if (_probe == null || !_probe.HasValue || _environment == null)
                return;

            ApplySettings();

            CharacterPerception perception = _probe.Current;
            float deltaTime = Time.deltaTime;

            PostureTarget target = _posture.Resolve(perception.Viewport.y);
            // Высота предыдущего кадра: UpdateHeight для этого кадра ещё не считалась,
            // и это тот же однокадровый лаг, на котором уже строится вся перцепция.
            bool hasLanded = _height <= LandingHeightThreshold;
            _stateMachine.Tick(target, hasLanded, deltaTime);

            FollowCameraWhileLevitating();
            UpdateRun(perception, deltaTime);
            UpdateHeight(perception, deltaTime);
            UpdateSquash(deltaTime);
            ApplyTransform();
        }

        /// <summary>
        /// Пока персонаж парит, он держится на фиксированном расстоянии и направлении
        /// относительно камеры — как будто в самом деле подвешен под ней. Смещение
        /// считается заново каждый кадр из текущей позы камеры, а не накоплением дельт
        /// позиции, поэтому реагирует и на перемещение телефона, и на его поворот:
        /// оба неизбежно уводили бы проекцию персонажа к краю кадра, выталкивая сигнал
        /// за пределы полосы и роняя его — то, что чинит <see cref="PostureResolver"/>
        /// для мгновенных случаев, а здесь устраняется на корню. Лежащий персонаж,
        /// в отличие от парящего, не «несут» — эта логика на него не действует.
        /// </summary>
        private void FollowCameraWhileLevitating()
        {
            if (_stateMachine.State != PostureState.Levitating)
            {
                _hasHeldOffset = false;
                return;
            }

            if (!_environment.TryGetSurface(out SurfacePlane surface))
                return;

            Vector3 planeUp = surface.Pose.rotation * Vector3.up;
            Pose cameraPose = _environment.CameraPose;

            Vector3 forward = ProjectOnPlaneWithFallback(
                cameraPose.rotation * Vector3.forward, planeUp, ref _lastHeldForward);
            Vector3 right = ProjectOnPlaneWithFallback(
                cameraPose.rotation * Vector3.right, planeUp, ref _lastHeldRight);

            if (!_hasHeldOffset)
            {
                // Первый кадр парения: запоминаем текущее относительное положение,
                // а не переносим персонажа в канонический «держак» — иначе он
                // дёрнется в момент входа в состояние.
                Vector3 offset = _groundPosition - cameraPose.position;
                _heldForwardOffset = Vector3.Dot(offset, forward);
                _heldRightOffset = Vector3.Dot(offset, right);
                _hasHeldOffset = true;
                return;
            }

            Vector3 candidate = cameraPose.position + forward * _heldForwardOffset + right * _heldRightOffset;
            _groundPosition = surface.ClampToBoundary(candidate);
        }

        /// <summary>
        /// При взгляде почти строго вдоль нормали плоскости проекция направления
        /// вырождается — тогда используем предыдущее валидное значение вместо него.
        /// </summary>
        private static Vector3 ProjectOnPlaneWithFallback(Vector3 direction, Vector3 planeUp, ref Vector3 fallback)
        {
            Vector3 projected = Vector3.ProjectOnPlane(direction, planeUp);
            if (projected.sqrMagnitude < 0.0001f)
                return fallback;

            projected.Normalize();
            fallback = projected;
            return projected;
        }

        private void UpdateRun(CharacterPerception perception, float deltaTime)
        {
            // Поза побеждает: лежачий и парящий персонаж не бегает.
            if (_stateMachine.State != PostureState.Standing)
            {
                _locomotion.Reset();
                RunIntensity = 0f;
                return;
            }

            RunIntensity = _locomotion.Resolve(perception.Viewport.x);
            if (Mathf.Approximately(RunIntensity, 0f))
                return;

            if (!_environment.TryGetSurface(out SurfacePlane surface))
                return;

            Vector3 planeUp = surface.Pose.rotation * Vector3.up;
            Vector3 planeRight = ProjectOnPlaneWithFallback(
                _environment.CameraPose.rotation * Vector3.right, planeUp, ref _lastPlaneRight);

            Vector3 step = planeRight * (RunIntensity * _runSpeed * deltaTime);
            _groundPosition = surface.ClampToBoundary(_groundPosition + step);

            Vector3 facing = planeRight * Mathf.Sign(RunIntensity);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(facing, planeUp),
                _turnSpeed * deltaTime);
        }

        private void UpdateHeight(CharacterPerception perception, float deltaTime)
        {
            if (_stateMachine.State == PostureState.Falling)
            {
                // Настоящая гравитация, а не сглаживание к нулю: то же затухающее
                // приближение, что и для подъёма, у земли выглядело бы неестественно —
                // падение должно набирать скорость, а не тормозить перед приземлением.
                _fallVelocity += _fallGravity * deltaTime;
                _height = Mathf.Max(0f, _height - _fallVelocity * deltaTime);
                return;
            }

            _fallVelocity = 0f;

            float targetHeight = _stateMachine.State == PostureState.Levitating
                ? _posture.LevitationAmount(perception.Viewport.y) * _maxLevitationHeight
                : 0f;

            _height = Mathf.Lerp(_height, targetHeight, 1f - Mathf.Exp(-_heightSmoothing * deltaTime));
        }

        private void UpdateSquash(float deltaTime)
        {
            bool flattened = _stateMachine.State == PostureState.Flat
                             || _stateMachine.State == PostureState.Flattening;

            float target = flattened ? _squashScale : 1f;
            _squash = Mathf.Lerp(_squash, target, 1f - Mathf.Exp(-_squashSmoothing * deltaTime));
        }

        private void ApplyTransform()
        {
            Vector3 up = Vector3.up;
            if (_environment.TryGetSurface(out SurfacePlane surface))
            {
                up = surface.Pose.rotation * Vector3.up;
                _groundPosition = surface.Project(_groundPosition);
            }

            transform.position = _groundPosition + up * (_height + _groundClearance);

            if (_visual == null)
                return;

            // Объём сохраняем приблизительно: сжатие по высоте компенсируется
            // расширением в плане, иначе расплющивание читается как уменьшение.
            float spread = 1f / Mathf.Sqrt(Mathf.Max(0.01f, _squash));
            _visual.localScale = new Vector3(
                _visualBaseScale.x * spread,
                _visualBaseScale.y * _squash,
                _visualBaseScale.z * spread);

            // Визуал отцентрован внутри пустышки, поэтому его смещение вверх
            // должно сжиматься вместе с ним, иначе персонаж повиснет над опорой.
            _visual.localPosition = new Vector3(
                _visualBasePosition.x,
                _visualBasePosition.y * _squash,
                _visualBasePosition.z);
        }

        private void ApplySettings()
        {
            _posture.Configure(_levitatingBand, _standingBand, _flatBand);
            _stateMachine.Configure(_flattenDuration, _standUpDuration);
            _locomotion.Configure(_startOffset, _stopOffset, _minIntensity);
        }
    }
}
