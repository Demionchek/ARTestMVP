using UnityEngine;

namespace ARCharacter.Core
{
    /// <summary>
    /// Поверхность, на которой живёт персонаж: поза плоскости и выпуклый полигон её границы.
    /// Граница задаётся в локальных координатах плоскости (X и Z, нормаль — локальный Y),
    /// чтобы структура повторяла формат ARPlane и не требовала конверсии на стороне AR.
    /// </summary>
    public readonly struct SurfacePlane
    {
        private readonly Vector2[] _boundary;

        public SurfacePlane(Pose pose, Vector2[] boundary)
        {
            Pose = pose;
            _boundary = boundary;
        }

        public Pose Pose { get; }

        public bool IsValid => _boundary != null && _boundary.Length >= 3;

        /// <summary>Проецирует точку на плоскость, сохраняя её положение по горизонтали.</summary>
        public Vector3 Project(Vector3 worldPoint)
        {
            Vector2 local = ToLocal(worldPoint);
            return ToWorld(local);
        }

        public bool Contains(Vector3 worldPoint) => IsValid && ContainsLocal(ToLocal(worldPoint));

        /// <summary>
        /// Возвращает ближайшую точку внутри границы. Нужна, чтобы персонаж, бегущий
        /// к центру экрана, останавливался на краю распознанной плоскости, а не уходил в воздух.
        /// </summary>
        public Vector3 ClampToBoundary(Vector3 worldPoint)
        {
            if (!IsValid)
                return worldPoint;

            Vector2 local = ToLocal(worldPoint);
            if (ContainsLocal(local))
                return ToWorld(local);

            return ToWorld(ClosestPointOnBoundary(local));
        }

        private Vector2 ToLocal(Vector3 worldPoint)
        {
            Vector3 local = Quaternion.Inverse(Pose.rotation) * (worldPoint - Pose.position);
            return new Vector2(local.x, local.z);
        }

        private Vector3 ToWorld(Vector2 local)
            => Pose.position + Pose.rotation * new Vector3(local.x, 0f, local.y);

        private bool ContainsLocal(Vector2 point)
        {
            // Луч вправо: точка внутри, если пересекает границу нечётное число раз.
            bool inside = false;
            for (int i = 0, j = _boundary.Length - 1; i < _boundary.Length; j = i++)
            {
                Vector2 a = _boundary[i];
                Vector2 b = _boundary[j];
                if (a.y > point.y == b.y > point.y)
                    continue;

                float crossX = (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (point.x < crossX)
                    inside = !inside;
            }

            return inside;
        }

        private Vector2 ClosestPointOnBoundary(Vector2 point)
        {
            Vector2 best = _boundary[0];
            float bestDistance = float.MaxValue;

            for (int i = 0, j = _boundary.Length - 1; i < _boundary.Length; j = i++)
            {
                Vector2 candidate = ClosestPointOnSegment(_boundary[j], _boundary[i], point);
                float distance = (candidate - point).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 point)
        {
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < Mathf.Epsilon)
                return a;

            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
            return a + segment * t;
        }
    }
}
