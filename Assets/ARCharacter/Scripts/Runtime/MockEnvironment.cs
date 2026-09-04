using ARCharacter.Core;
using UnityEngine;

namespace ARCharacter
{
    /// <summary>
    /// Подмена AR для работы в редакторе: прямоугольная поверхность фиксированного размера
    /// и обычная камера сцены. Позволяет отлаживать и настраивать всю механику без телефона,
    /// потому что логике безразлично, откуда пришли поза камеры и границы плоскости.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MockEnvironment : EnvironmentBehaviour
    {
        [SerializeField] private Camera _camera;

        [Tooltip("Трансформ, задающий позу поверхности. Локальный Y считается нормалью.")]
        [SerializeField] private Transform _surface;

        [Tooltip("Размер прямоугольной поверхности в метрах.")]
        [SerializeField] private Vector2 _surfaceSize = new Vector2(2f, 2f);

        private readonly Vector2[] _boundary = new Vector2[4];
        private Vector2 _boundarySize = Vector2.zero;

        public override Pose CameraPose => _camera == null
            ? Pose.identity
            : new Pose(_camera.transform.position, _camera.transform.rotation);

        public override Matrix4x4 ProjectionMatrix => _camera == null
            ? Matrix4x4.identity
            : _camera.projectionMatrix;

        public override EnvironmentTracking Tracking => _camera != null && _surface != null
            ? EnvironmentTracking.Tracking
            : EnvironmentTracking.None;

        public override bool TryGetSurface(out SurfacePlane surface)
        {
            if (_surface == null)
            {
                surface = default;
                return false;
            }

            RebuildBoundaryIfNeeded();
            surface = new SurfacePlane(new Pose(_surface.position, _surface.rotation), _boundary);
            return true;
        }

        private void RebuildBoundaryIfNeeded()
        {
            if (_boundarySize == _surfaceSize)
                return;

            float x = _surfaceSize.x * 0.5f;
            float z = _surfaceSize.y * 0.5f;
            _boundary[0] = new Vector2(-x, -z);
            _boundary[1] = new Vector2(x, -z);
            _boundary[2] = new Vector2(x, z);
            _boundary[3] = new Vector2(-x, z);
            _boundarySize = _surfaceSize;
        }

        private void OnDrawGizmos()
        {
            if (_surface == null)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS(_surface.position, _surface.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_surfaceSize.x, 0f, _surfaceSize.y));
        }
    }
}
