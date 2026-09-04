using UnityEngine;

namespace ARCharacter.Core
{
    public enum PostureTarget
    {
        Levitating,
        Standing,
        Flat
    }

    /// <summary>
    /// Переводит вертикаль вьюпорта в желаемую позу. Верх экрана — камера опущена,
    /// персонаж лежит; низ — камера поднята, персонаж парит.
    /// </summary>
    public sealed class PostureResolver
    {
        private Band _levitating;
        private Band _standing;
        private Band _flat;

        public PostureResolver()
            => Configure(new Band(0f, 0.32f), new Band(0.26f, 0.80f), new Band(0.74f, 1f));

        public PostureTarget Current { get; private set; } = PostureTarget.Standing;

        public void Configure(Band levitating, Band standing, Band flat)
        {
            _levitating = levitating;
            _standing = standing;
            _flat = flat;
        }

        public void Reset(PostureTarget target) => Current = target;

        public PostureTarget Resolve(float viewportY)
        {
            // Три полосы вместе покрывают ровно [0, 1], поэтому за его пределами
            // персонаж просто временно вне кадра (камера сдвинулась или накренилась) —
            // это не сигнал «встал», и позу трогать не нужно, иначе перемещение камеры
            // сбрасывает парение или лежание в Standing только из-за ухода за край экрана.
            if (viewportY < 0f || viewportY > 1f)
                return Current;

            // Пока сигнал не покинул полосу текущей позы, менять нечего.
            if (BandOf(Current).Contains(viewportY))
                return Current;

            if (_flat.Contains(viewportY))
                Current = PostureTarget.Flat;
            else if (_levitating.Contains(viewportY))
                Current = PostureTarget.Levitating;
            else
                Current = PostureTarget.Standing;

            return Current;
        }

        /// <summary>
        /// Насколько глубоко сигнал зашёл в зону парения: 0 у верхней границы полосы,
        /// 1 у нижнего края экрана. Высота — величина непрерывная, а не состояние,
        /// поэтому считается отдельно от позы.
        /// </summary>
        public float LevitationAmount(float viewportY)
            => Mathf.Clamp01(1f - _levitating.Normalize(viewportY));

        private Band BandOf(PostureTarget target) => target switch
        {
            PostureTarget.Flat => _flat,
            PostureTarget.Levitating => _levitating,
            _ => _standing
        };
    }
}
