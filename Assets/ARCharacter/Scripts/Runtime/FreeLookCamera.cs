using UnityEngine;
using UnityEngine.InputSystem;

namespace ARCharacter
{
    /// <summary>
    /// Свободная камера сцены Sandbox: имитирует смартфон в руках, чтобы наклонять,
    /// поворачивать и кренить «телефон» мышью, а не пересобирать APK ради каждой
    /// проверки поведения. Крен вынесен на колесо, потому что все три оси поворота
    /// нужны одновременно, а мышь даёт только две.
    /// </summary>
    public sealed class FreeLookCamera : MonoBehaviour
    {
        [Tooltip("Скорость перемещения, метров в секунду.")]
        [SerializeField, Min(0f)] private float _moveSpeed = 1.2f;

        [Tooltip("Множитель скорости при зажатом Shift.")]
        [SerializeField, Min(1f)] private float _boostMultiplier = 3f;

        [Tooltip("Чувствительность обзора, градусов на пиксель движения мыши.")]
        [SerializeField, Min(0f)] private float _lookSensitivity = 0.12f;

        [Tooltip("Градусов крена на одно деление колеса мыши.")]
        [SerializeField, Min(0f)] private float _rollSensitivity = 0.05f;

        private float _yaw;
        private float _pitch;
        private float _roll;

        private void OnEnable() => ReadRotationFromTransform();

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null)
                return;

            // Повороты только с зажатой правой кнопкой, иначе невозможно работать
            // с инспектором и отладочной панелью, не сбивая ракурс.
            if (mouse.rightButton.isPressed)
            {
                Look(mouse.delta.ReadValue());
                Roll(mouse.scroll.ReadValue().y);
            }

            // Крен легко потерять на глаз, поэтому нужен способ вернуть горизонт.
            if (keyboard.rKey.wasPressedThisFrame)
            {
                _roll = 0f;
                ApplyRotation();
            }

            Move(keyboard);
        }

        private void ReadRotationFromTransform()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = Normalize(angles.x);
            _roll = Normalize(angles.z);
        }

        private void Look(Vector2 delta)
        {
            if (delta == Vector2.zero)
                return;

            _yaw += delta.x * _lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - delta.y * _lookSensitivity, -89f, 89f);
            ApplyRotation();
        }

        private void Roll(float scroll)
        {
            if (Mathf.Approximately(scroll, 0f))
                return;

            _roll = Mathf.Clamp(_roll + scroll * _rollSensitivity, -180f, 180f);
            ApplyRotation();
        }

        private void ApplyRotation() => transform.rotation = Quaternion.Euler(_pitch, _yaw, _roll);

        private void Move(Keyboard keyboard)
        {
            Vector3 direction = Vector3.zero;
            if (keyboard.wKey.isPressed) direction += transform.forward;
            if (keyboard.sKey.isPressed) direction -= transform.forward;
            if (keyboard.dKey.isPressed) direction += transform.right;
            if (keyboard.aKey.isPressed) direction -= transform.right;
            if (keyboard.eKey.isPressed) direction += Vector3.up;
            if (keyboard.qKey.isPressed) direction -= Vector3.up;

            if (direction == Vector3.zero)
                return;

            float speed = _moveSpeed * (keyboard.leftShiftKey.isPressed ? _boostMultiplier : 1f);
            transform.position += direction.normalized * (speed * Time.deltaTime);
        }

        private static float Normalize(float angle) => angle > 180f ? angle - 360f : angle;
    }
}
