using UnityEngine;

namespace ARCharacter.Core
{
    /// <summary>
    /// Снимок того, что персонаж «знает» о себе относительно камеры за один кадр.
    /// Единственный вход резолверов поведения: они не обращаются ни к камере,
    /// ни к трансформам, поэтому проверяются на подставленных значениях.
    /// </summary>
    public readonly struct CharacterPerception
    {
        public CharacterPerception(Vector2 rawViewport, Vector2 viewport, float distance, bool onScreen)
        {
            RawViewport = rawViewport;
            Viewport = viewport;
            Distance = distance;
            OnScreen = onScreen;
        }

        /// <summary>Положение во вьюпорте до фильтрации. Нужно только для отладки.</summary>
        public Vector2 RawViewport { get; }

        /// <summary>Отфильтрованное положение: 0..1 от левого нижнего угла экрана.</summary>
        public Vector2 Viewport { get; }

        /// <summary>Расстояние вдоль взгляда камеры; отрицательное — персонаж позади неё.</summary>
        public float Distance { get; }

        public bool OnScreen { get; }
    }
}
