using ARCharacter.Core;
using UnityEngine;

namespace ARCharacter
{
    /// <summary>
    /// Считает положение персонажа во вьюпорте и чистит сигнал до того, как его
    /// кто-либо прочитал. Порядок исполнения смещён вперёд намеренно: поведение
    /// должно видеть снимок, посчитанный в этом же кадре, а не в предыдущем.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class PerceptionProbe : MonoBehaviour
    {
        [SerializeField] private EnvironmentBehaviour _environment;

        [Tooltip("Трансформ персонажа. Точка отсчёта — его основание.")]
        [SerializeField] private Transform _target;

        [Tooltip("Высота точки замера над основанием, если не назначен _heightSource. У ног и у центра масс сигнал заметно разный.")]
        [SerializeField, Min(0f)] private float _sampleHeight = 0.2f;

        [Tooltip("Необязательно: если назначен, высота точки замера берётся из текущего визуала мотора (учитывает расплющивание), а не из фиксированного значения выше.")]
        [SerializeField] private CharacterMotor _heightSource;

        [Header("Фильтрация")]
        [Tooltip("Частота среза в покое, Гц. Меньше — сильнее сглаживание и больше запаздывание.")]
        [SerializeField, Min(0.0001f)] private float _minCutoff = 1f;

        [Tooltip("Насколько быстро сглаживание отпускает при разгоне сигнала.")]
        [SerializeField, Min(0f)] private float _beta = 0.5f;

        [Tooltip("Срез для производной. Меняется редко, служебный параметр фильтра.")]
        [SerializeField, Min(0.0001f)] private float _derivativeCutoff = 1f;

        private readonly OneEuroFilter2 _filter = new OneEuroFilter2();
        private float _appliedMinCutoff = float.NaN;
        private float _appliedBeta;
        private float _appliedDerivativeCutoff;

        public CharacterPerception Current { get; private set; }

        public bool HasValue { get; private set; }

        /// <summary>
        /// Точка замера считается от ОПОРЫ, а не от текущей высоты парения: цель —
        /// это трансформ мотора, который сам поднимается при левитации, и если мерить
        /// от него как есть, получается замкнутая петля обратной связи — чем выше
        /// персонаж, тем сильнее меняется его же проекция на экране, а от неё же
        /// зависит, на какую высоту его поднимать дальше. При маленьком потолке высоты
        /// эффект незаметен, но с ростом потолка усиление в этой петле растёт, и систему
        /// начинает раскачивать (взлетает — падает — взлетает). Вычитание текущей
        /// высоты размыкает петлю: сигнал зависит только от наклона камеры, как и должен.
        /// </summary>
        public Vector3 SamplePoint
        {
            get
            {
                if (_target == null)
                    return Vector3.zero;

                Vector3 groundPosition = _target.position;
                if (_heightSource != null)
                    groundPosition -= Vector3.up * _heightSource.Height;

                return groundPosition + Vector3.up * EffectiveSampleHeight;
            }
        }

        private float EffectiveSampleHeight => _heightSource != null ? _heightSource.VisualHeightOffset : _sampleHeight;

        private void OnEnable()
        {
            _filter.Reset();
            HasValue = false;
        }

        private void Update()
        {
            if (_environment == null || _target == null)
            {
                HasValue = false;
                return;
            }

            ApplyFilterSettingsIfChanged();

            Vector3 viewport = ViewportMath.WorldToViewport(
                _environment.CameraPose, _environment.ProjectionMatrix, SamplePoint);

            var raw = new Vector2(viewport.x, viewport.y);
            Vector2 filtered = _filter.Filter(raw, Time.deltaTime);

            Current = new CharacterPerception(raw, filtered, viewport.z, ViewportMath.IsOnScreen(viewport));
            HasValue = true;
        }

        /// <summary>
        /// Переносит правки из инспектора в фильтр на лету: параметры One Euro
        /// невозможно подобрать вслепую, их крутят при работающей сцене.
        /// </summary>
        private void ApplyFilterSettingsIfChanged()
        {
            if (Mathf.Approximately(_appliedMinCutoff, _minCutoff)
                && Mathf.Approximately(_appliedBeta, _beta)
                && Mathf.Approximately(_appliedDerivativeCutoff, _derivativeCutoff))
                return;

            _filter.Configure(_minCutoff, _beta, _derivativeCutoff);
            _appliedMinCutoff = _minCutoff;
            _appliedBeta = _beta;
            _appliedDerivativeCutoff = _derivativeCutoff;
        }
    }
}
