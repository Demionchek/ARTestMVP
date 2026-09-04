using UnityEngine;

namespace ARCharacter.Core
{
    /// <summary>
    /// Переводит горизонталь вьюпорта в желание бежать к центру экрана.
    /// Пороги старта и остановки разнесены: с одним порогом персонаж у самой границы
    /// мёртвой зоны дёргался бы между бегом и покоем на каждом дрожании сигнала.
    /// </summary>
    public sealed class LocomotionResolver
    {
        private float _startOffset = 0.16f;
        private float _stopOffset = 0.06f;
        private float _minIntensity = 0.35f;

        public bool IsRunning { get; private set; }

        public void Configure(float startOffset, float stopOffset, float minIntensity)
        {
            _startOffset = Mathf.Clamp(startOffset, 0.01f, 0.5f);
            _stopOffset = Mathf.Clamp(stopOffset, 0f, _startOffset);
            _minIntensity = Mathf.Clamp01(minIntensity);
        }

        public void Reset() => IsRunning = false;

        /// <summary>
        /// Возвращает интенсивность бега со знаком: положительная — вправо по экрану,
        /// отрицательная — влево, ноль — стоим. Модуль растёт от 0 у порога старта
        /// от минимальной у порога остановки до 1 у края экрана.
        /// </summary>
        public float Resolve(float viewportX)
        {
            float offset = viewportX - 0.5f;
            float distance = Mathf.Abs(offset);

            IsRunning = IsRunning ? distance > _stopOffset : distance > _startOffset;
            if (!IsRunning)
                return 0f;

            // Отсчёт от порога остановки, а не старта: иначе внутри зоны гистерезиса
            // интенсивность обнулялась бы и персонаж замирал, не добежав до центра,
            // оставаясь при этом в состоянии бега.
            float ramp = Mathf.Clamp01(Mathf.InverseLerp(_stopOffset, 0.5f, distance));
            float intensity = Mathf.Lerp(_minIntensity, 1f, ramp);

            // Персонаж слева от центра — бежать вправо, и наоборот.
            return offset < 0f ? intensity : -intensity;
        }
    }
}
