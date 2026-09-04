using UnityEngine;

namespace ARCharacter.Core
{
    /// <summary>
    /// Диапазон, в котором состояние считается устойчивым. Полосы соседних состояний
    /// намеренно перекрываются: пока значение остаётся в полосе текущего состояния,
    /// оно не меняется, и это перекрытие и есть гистерезис. Так порог выражен данными,
    /// а не ветвлением, и его можно крутить слайдером.
    /// </summary>
    [System.Serializable]
    public struct Band
    {
        [SerializeField] private float _min;
        [SerializeField] private float _max;

        public Band(float min, float max)
        {
            _min = Mathf.Min(min, max);
            _max = Mathf.Max(min, max);
        }

        public float Min => _min;

        public float Max => _max;

        public bool Contains(float value) => value >= _min && value <= _max;

        /// <summary>Положение значения внутри полосы: 0 у нижней границы, 1 у верхней.</summary>
        public float Normalize(float value) => Mathf.InverseLerp(_min, _max, value);
    }
}
