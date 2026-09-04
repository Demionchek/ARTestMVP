using ARCharacter.Core;
using NUnit.Framework;
using UnityEngine;

namespace ARCharacter.Tests
{
    public sealed class LocomotionResolverTests
    {
        private const float StartOffset = 0.16f;
        private const float StopOffset = 0.06f;
        private const float MinIntensity = 0.35f;

        private static LocomotionResolver CreateResolver()
        {
            var resolver = new LocomotionResolver();
            resolver.Configure(StartOffset, StopOffset, MinIntensity);
            return resolver;
        }

        [Test]
        public void NearCentre_DoesNotRun()
        {
            LocomotionResolver resolver = CreateResolver();
            Assert.That(resolver.Resolve(0.5f), Is.EqualTo(0f));
            Assert.That(resolver.IsRunning, Is.False);
        }

        [Test]
        public void LeftOfCentre_RunsToTheRight()
        {
            LocomotionResolver resolver = CreateResolver();
            Assert.That(resolver.Resolve(0.05f), Is.GreaterThan(0f));
        }

        [Test]
        public void RightOfCentre_RunsToTheLeft()
        {
            LocomotionResolver resolver = CreateResolver();
            Assert.That(resolver.Resolve(0.95f), Is.LessThan(0f));
        }

        /// <summary>
        /// Регрессия: интенсивность отсчитывается от порога остановки, а не старта.
        /// При отсчёте от старта внутри зоны гистерезиса она обнулялась бы, и персонаж
        /// замирал бы, не добежав до центра, оставаясь в состоянии бега.
        /// </summary>
        [Test]
        public void InsideHysteresisBand_KeepsMovingWithNonZeroSpeed()
        {
            LocomotionResolver resolver = CreateResolver();
            resolver.Resolve(0.05f);

            // 0.40 даёт отклонение 0.10: меньше порога старта, но больше порога остановки.
            float intensity = resolver.Resolve(0.40f);

            Assert.That(resolver.IsRunning, Is.True);
            Assert.That(intensity, Is.GreaterThanOrEqualTo(MinIntensity));
        }

        [Test]
        public void BelowStopOffset_StopsRunning()
        {
            LocomotionResolver resolver = CreateResolver();
            resolver.Resolve(0.05f);

            Assert.That(resolver.Resolve(0.48f), Is.EqualTo(0f));
            Assert.That(resolver.IsRunning, Is.False);
        }

        [Test]
        public void JustBelowStartOffset_DoesNotStartRunning()
        {
            LocomotionResolver resolver = CreateResolver();

            // Отклонение 0.10 меньше порога старта, поэтому бег не начинается,
            // хотя для уже бегущего персонажа этого отклонения хватило бы.
            Assert.That(resolver.Resolve(0.40f), Is.EqualTo(0f));
        }

        [Test]
        public void IntensityGrowsTowardScreenEdge()
        {
            LocomotionResolver resolver = CreateResolver();

            float nearEdge = Mathf.Abs(resolver.Resolve(0.0f));
            resolver.Reset();
            float furtherIn = Mathf.Abs(resolver.Resolve(0.25f));

            Assert.That(nearEdge, Is.GreaterThan(furtherIn));
        }
    }
}
