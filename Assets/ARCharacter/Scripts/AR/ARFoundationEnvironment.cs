using ARCharacter.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace ARCharacter.AR
{
    /// <summary>
    /// Реализация IEnvironment поверх AR Foundation: поза берётся с камеры, которую уже
    /// двигает TrackedPoseDriver, а поверхность — с плоскости, которую выбрал
    /// ARPlacementController в момент размещения. Плоскость перечитывается заново на
    /// каждый запрос, а не кешируется: ARCore уточняет её позу и границу по мере
    /// сканирования, и закешированная копия быстро бы устарела.
    /// </summary>
    public sealed class ARFoundationEnvironment : EnvironmentBehaviour
    {
        [SerializeField] private Camera _camera;

        private ARPlane _activePlane;

        public override Pose CameraPose => _camera == null
            ? Pose.identity
            : new Pose(_camera.transform.position, _camera.transform.rotation);

        public override Matrix4x4 ProjectionMatrix => _camera == null
            ? Matrix4x4.identity
            : _camera.projectionMatrix;

        public override EnvironmentTracking Tracking => ARSession.state switch
        {
            ARSessionState.SessionTracking => EnvironmentTracking.Tracking,
            ARSessionState.SessionInitializing or ARSessionState.Ready => EnvironmentTracking.Limited,
            _ => EnvironmentTracking.None
        };

        /// <summary>Вызывается ARPlacementController в момент размещения или переноса.</summary>
        public void SetActivePlane(ARPlane plane) => _activePlane = plane;

        public override bool TryGetSurface(out SurfacePlane surface)
        {
            if (_activePlane == null)
            {
                surface = default;
                return false;
            }

            var pose = new Pose(_activePlane.transform.position, _activePlane.transform.rotation);
            NativeArray<Vector2> boundary = _activePlane.boundary;
            surface = new SurfacePlane(pose, boundary.ToArray());
            return true;
        }
    }
}
