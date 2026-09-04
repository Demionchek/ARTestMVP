using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARCharacter.AR
{
    public enum PlacementState
    {
        Searching,
        Placed
    }

    public enum PlacementMode
    {
        Manual,
        Auto
    }

    /// <summary>
    /// Размещает персонажа двумя равноправными способами: автоматически на первой
    /// достаточно большой распознанной плоскости (как AR+ в Pokémon GO) или вручную —
    /// сканирование и размещение по явной команде (кнопки на время отладки на устройстве,
    /// где ещё не видно, что происходит по касанию экрана вслепую). Любое повторное
    /// размещение ведёт в тот же Place — переставить персонажа так же просто, как
    /// поставить впервые.
    /// </summary>
    public sealed class ARPlacementController : MonoBehaviour
    {
        [SerializeField] private ARPlaneManager _planeManager;
        [SerializeField] private ARRaycastManager _raycastManager;
        [SerializeField] private ARAnchorManager _anchorManager;
        [SerializeField] private ARFoundationEnvironment _environment;
        [SerializeField] private Transform _characterRoot;

        [SerializeField] private PlacementMode _mode = PlacementMode.Manual;

        [Tooltip("Минимальная площадь плоскости для автоматического размещения, м².")]
        [SerializeField, Min(0f)] private float _minAutoPlaceArea = 0.09f;

        [Tooltip("Скрывать визуализацию плоскостей после размещения.")]
        [SerializeField] private bool _hidePlanesAfterPlacement = true;

        private readonly List<ARRaycastHit> _raycastHits = new List<ARRaycastHit>();
        private ARAnchor _anchor;

        public PlacementState State { get; private set; } = PlacementState.Searching;

        public bool IsScanning => _planeManager != null && _planeManager.enabled;

        public PlacementMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                // Ручной режим оставляет сканирование как было — им управляет кнопка;
                // авто без сканирования бессмысленен, поэтому включаем сразу.
                if (_mode == PlacementMode.Auto)
                    SetScanning(true);
            }
        }

        private void OnEnable()
        {
            if (_planeManager != null)
                _planeManager.trackablesChanged.AddListener(OnPlanesChanged);
        }

        private void OnDisable()
        {
            if (_planeManager != null)
                _planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        }

        public void SetScanning(bool scanning)
        {
            if (_planeManager != null)
                _planeManager.enabled = scanning;
        }

        /// <summary>
        /// Раскаст из центра экрана — reticle-паттерн для кнопки «Разместить»:
        /// пользователь наводит телефон так, чтобы нужная поверхность была
        /// в центре кадра, и подтверждает явным нажатием, а не произвольным тапом.
        /// </summary>
        public void PlaceAtScreenCenter()
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (!_raycastManager.Raycast(center, _raycastHits, TrackableType.PlaneWithinPolygon))
            {
                Debug.LogWarning("Раскаст из центра экрана не попал ни в одну плоскость.");
                return;
            }

            if (_raycastHits[0].trackable is ARPlane plane)
                Place(plane, _raycastHits[0].pose);
        }

        private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            if (State == PlacementState.Placed)
            {
                // Сканирование продолжает работать в фоне (см. HidePlanes), поэтому
                // новые плоскости будут появляться и после размещения — прячем каждую
                // сразу по обнаружению, а не только те, что были на момент Place.
                foreach (ARPlane plane in args.added)
                    plane.gameObject.SetActive(false);
                return;
            }

            if (_mode != PlacementMode.Auto)
                return;

            foreach (ARPlane plane in args.added)
            {
                if (plane.trackingState != TrackingState.Tracking)
                    continue;
                if (plane.size.x * plane.size.y < _minAutoPlaceArea)
                    continue;

                Place(plane, new Pose(plane.center, plane.transform.rotation));
                return;
            }
        }

        private void Place(ARPlane plane, Pose pose)
        {
            ARAnchor anchor = _anchorManager.AttachAnchor(plane, pose);
            if (anchor == null)
            {
                Debug.LogWarning("Не удалось создать якорь для размещения на плоскости.");
                return;
            }

            if (_anchor != null)
                _anchorManager.TryRemoveAnchor(_anchor);
            _anchor = anchor;

            _characterRoot.SetParent(_anchor.transform, worldPositionStays: false);
            _characterRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _characterRoot.gameObject.SetActive(true);

            _environment.SetActivePlane(plane);
            State = PlacementState.Placed;

            if (_hidePlanesAfterPlacement)
                HidePlanes();
        }

        private void HidePlanes()
        {
            // Сканирование НЕ останавливаем: если его выключать и включать заново
            // при каждой перестановке, ARCore приходится набирать данные о плоскости
            // заново «с холодного старта», и рейкаст в центр экрана сразу после
            // включения ненадёжно бьёт мимо — ровно источник нестабильного «Переставить».
            foreach (ARPlane plane in _planeManager.trackables)
                plane.gameObject.SetActive(false);
        }

        /// <summary>
        /// Возвращает в режим поиска поверхности: показывает уже найденные плоскости
        /// заново. Сканирование при этом никогда не останавливалось (см. HidePlanes),
        /// поэтому раскаст в центр экрана работает сразу, без задержки на повторный
        /// набор данных, — «Переставить» → «Разместить» вместо четырёх нажатий.
        /// </summary>
        public void Reposition()
        {
            State = PlacementState.Searching;
            ShowPlanes();
        }

        private void ShowPlanes()
        {
            if (_planeManager == null)
                return;

            foreach (ARPlane plane in _planeManager.trackables)
                plane.gameObject.SetActive(true);
        }
    }
}
