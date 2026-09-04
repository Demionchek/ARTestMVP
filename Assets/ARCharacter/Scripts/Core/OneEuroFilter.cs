using UnityEngine;

namespace ARCharacter.Core
{
    /// <summary>
    /// Фильтр One Euro: частота среза зависит от скорости изменения сигнала.
    /// Обычное сглаживание с постоянным коэффициентом заставляет выбирать между
    /// дрожанием и запаздыванием; здесь в покое сглаживание сильное, а на быстром
    /// осознанном движении почти отключается, поэтому лага не возникает.
    /// </summary>
    public sealed class OneEuroFilter
    {
        private float _minCutoff;
        private float _beta;
        private float _derivativeCutoff;

        private float _lastValue;
        private float _lastDerivative;
        private bool _hasValue;

        public OneEuroFilter(float minCutoff = 1f, float beta = 0.5f, float derivativeCutoff = 1f)
            => Configure(minCutoff, beta, derivativeCutoff);

        public void Configure(float minCutoff, float beta, float derivativeCutoff)
        {
            _minCutoff = Mathf.Max(0.0001f, minCutoff);
            _beta = Mathf.Max(0f, beta);
            _derivativeCutoff = Mathf.Max(0.0001f, derivativeCutoff);
        }

        public void Reset() => _hasValue = false;

        public float Filter(float value, float deltaTime)
        {
            if (deltaTime <= 0f)
                return _hasValue ? _lastValue : value;

            if (!_hasValue)
            {
                _hasValue = true;
                _lastValue = value;
                _lastDerivative = 0f;
                return value;
            }

            float derivative = (value - _lastValue) / deltaTime;
            _lastDerivative = Mathf.Lerp(_lastDerivative, derivative, Alpha(_derivativeCutoff, deltaTime));

            // Чем быстрее меняется сигнал, тем выше срез и тем слабее сглаживание.
            float cutoff = _minCutoff + _beta * Mathf.Abs(_lastDerivative);
            _lastValue = Mathf.Lerp(_lastValue, value, Alpha(cutoff, deltaTime));
            return _lastValue;
        }

        private static float Alpha(float cutoff, float deltaTime)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / deltaTime);
        }
    }

    /// <summary>Пара независимых фильтров для двумерного сигнала.</summary>
    public sealed class OneEuroFilter2
    {
        private readonly OneEuroFilter _x = new OneEuroFilter();
        private readonly OneEuroFilter _y = new OneEuroFilter();

        public void Configure(float minCutoff, float beta, float derivativeCutoff)
        {
            _x.Configure(minCutoff, beta, derivativeCutoff);
            _y.Configure(minCutoff, beta, derivativeCutoff);
        }

        public void Reset()
        {
            _x.Reset();
            _y.Reset();
        }

        public Vector2 Filter(Vector2 value, float deltaTime)
            => new Vector2(_x.Filter(value.x, deltaTime), _y.Filter(value.y, deltaTime));
    }
}
