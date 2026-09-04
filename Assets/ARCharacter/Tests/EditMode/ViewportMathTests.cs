using ARCharacter.Core;
using NUnit.Framework;
using UnityEngine;

namespace ARCharacter.Tests
{
    /// <summary>
    /// Проекция считается вручную, и ошибка в соглашении об осях не ловится
    /// компилятором: она проявляется как зеркалирование точки относительно центра
    /// экрана. Тесты ниже фиксируют направления, поэтому такое зеркало ломает их сразу.
    /// </summary>
    public sealed class ViewportMathTests
    {
        private const float Tolerance = 0.0005f;

        // Портретный экран телефона с типичным полем зрения.
        private static readonly Matrix4x4 Projection =
            Matrix4x4.Perspective(60f, 9f / 16f, 0.05f, 30f);

        private static readonly Pose CameraAtOrigin = new Pose(Vector3.zero, Quaternion.identity);

        [Test]
        public void PointOnForwardAxis_MapsToScreenCentre()
        {
            Vector3 viewport = ViewportMath.WorldToViewport(
                CameraAtOrigin, Projection, new Vector3(0f, 0f, 2f));

            Assert.That(viewport.x, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(viewport.y, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(viewport.z, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void PointToTheRight_MapsToRightHalf()
        {
            Vector3 viewport = ViewportMath.WorldToViewport(
                CameraAtOrigin, Projection, new Vector3(0.5f, 0f, 2f));

            Assert.That(viewport.x, Is.GreaterThan(0.5f));
        }

        [Test]
        public void PointAbove_MapsToUpperHalf()
        {
            Vector3 viewport = ViewportMath.WorldToViewport(
                CameraAtOrigin, Projection, new Vector3(0f, 0.5f, 2f));

            Assert.That(viewport.y, Is.GreaterThan(0.5f));
        }

        [Test]
        public void PointBehindCamera_HasNegativeDistance()
        {
            Vector3 viewport = ViewportMath.WorldToViewport(
                CameraAtOrigin, Projection, new Vector3(0f, 0f, -2f));

            Assert.That(viewport.z, Is.LessThan(0f));
            Assert.That(ViewportMath.IsOnScreen(viewport), Is.False);
        }

        [Test]
        public void RotatedCamera_KeepsRelativeGeometry()
        {
            var camera = new Pose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

            // Камера повёрнута вправо, поэтому её «вперёд» — это мировая ось +X.
            Vector3 centre = ViewportMath.WorldToViewport(camera, Projection, new Vector3(2f, 0f, 0f));
            Assert.That(centre.x, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(centre.z, Is.EqualTo(2f).Within(Tolerance));

            // А её «вправо» — мировая ось -Z.
            Vector3 right = ViewportMath.WorldToViewport(camera, Projection, new Vector3(2f, 0f, -0.5f));
            Assert.That(right.x, Is.GreaterThan(0.5f));
        }
    }
}
