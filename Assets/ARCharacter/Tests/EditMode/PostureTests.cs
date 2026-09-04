using ARCharacter.Core;
using NUnit.Framework;

namespace ARCharacter.Tests
{
    /// <summary>
    /// Полосы поз перекрываются, и это перекрытие — единственная защита от дребезга
    /// на границе. Тесты фиксируют именно его: без гистерезиса поза менялась бы на
    /// каждом дрожании сигнала, а глазами такое отличить от «так задумано» тяжело.
    /// </summary>
    public sealed class PostureResolverTests
    {
        private static PostureResolver CreateResolver()
        {
            var resolver = new PostureResolver();
            resolver.Configure(new Band(0f, 0.32f), new Band(0.26f, 0.80f), new Band(0.74f, 1f));
            return resolver;
        }

        [Test]
        public void TopOfScreen_ResolvesToFlat()
        {
            PostureResolver resolver = CreateResolver();
            Assert.That(resolver.Resolve(0.95f), Is.EqualTo(PostureTarget.Flat));
        }

        [Test]
        public void BottomOfScreen_ResolvesToLevitating()
        {
            PostureResolver resolver = CreateResolver();
            Assert.That(resolver.Resolve(0.05f), Is.EqualTo(PostureTarget.Levitating));
        }

        [Test]
        public void MiddleOfScreen_ResolvesToStanding()
        {
            PostureResolver resolver = CreateResolver();
            Assert.That(resolver.Resolve(0.5f), Is.EqualTo(PostureTarget.Standing));
        }

        [Test]
        public void InsideOverlap_KeepsPreviousPosture()
        {
            // 0.76 принадлежит и полосе стояния, и полосе лежания одновременно.
            const float ambiguous = 0.76f;

            PostureResolver standing = CreateResolver();
            standing.Reset(PostureTarget.Standing);
            Assert.That(standing.Resolve(ambiguous), Is.EqualTo(PostureTarget.Standing));

            PostureResolver flat = CreateResolver();
            flat.Reset(PostureTarget.Flat);
            Assert.That(flat.Resolve(ambiguous), Is.EqualTo(PostureTarget.Flat));
        }

        [Test]
        public void CrossingWholeOverlap_ChangesPosture()
        {
            PostureResolver resolver = CreateResolver();
            resolver.Reset(PostureTarget.Standing);

            resolver.Resolve(0.76f);
            Assert.That(resolver.Resolve(0.85f), Is.EqualTo(PostureTarget.Flat));
        }

        /// <summary>
        /// Регрессия: пока камера перемещается, персонаж на миг проецируется за пределы
        /// экрана (y вне [0, 1]). Раньше это молча трактовалось как «встал», и парящий
        /// или лежащий персонаж падал только из-за того, что вышел из кадра.
        /// </summary>
        [Test]
        public void AboveScreenTop_KeepsLevitatingPosture()
        {
            PostureResolver resolver = CreateResolver();
            resolver.Reset(PostureTarget.Levitating);

            Assert.That(resolver.Resolve(1.4f), Is.EqualTo(PostureTarget.Levitating));
        }

        [Test]
        public void BelowScreenBottom_KeepsFlatPosture()
        {
            PostureResolver resolver = CreateResolver();
            resolver.Reset(PostureTarget.Flat);

            Assert.That(resolver.Resolve(-0.2f), Is.EqualTo(PostureTarget.Flat));
        }

        [Test]
        public void OffScreen_KeepsStandingPosture()
        {
            PostureResolver resolver = CreateResolver();
            resolver.Reset(PostureTarget.Standing);

            Assert.That(resolver.Resolve(-0.1f), Is.EqualTo(PostureTarget.Standing));
            Assert.That(resolver.Resolve(1.1f), Is.EqualTo(PostureTarget.Standing));
        }

        [Test]
        public void LevitationAmount_GrowsTowardBottomOfScreen()
        {
            PostureResolver resolver = CreateResolver();

            float nearTopOfBand = resolver.LevitationAmount(0.32f);
            float atBottom = resolver.LevitationAmount(0f);

            Assert.That(nearTopOfBand, Is.EqualTo(0f).Within(0.001f));
            Assert.That(atBottom, Is.EqualTo(1f).Within(0.001f));
        }
    }

    /// <summary>
    /// Падение и вставание нельзя прерывать произвольно, иначе персонаж застынет
    /// в промежуточной позе, — кроме одного случая: вставание прерывается подъёмом
    /// камеры, персонажа подхватывают на середине. Отпущенный из парения персонаж
    /// не встаёт мягко на ноги, а падает до земли и только потом сбивается с ног,
    /// как от обычного падения.
    /// </summary>
    public sealed class PostureStateMachineTests
    {
        private const float FlattenDuration = 0.9f;
        private const float StandUpDuration = 1.2f;

        private static PostureStateMachine CreateMachine()
        {
            var machine = new PostureStateMachine();
            machine.Configure(FlattenDuration, StandUpDuration);
            return machine;
        }

        [Test]
        public void StandingToFlat_PassesThroughFlattening()
        {
            PostureStateMachine machine = CreateMachine();

            machine.Tick(PostureTarget.Flat, false, 0.016f);
            Assert.That(machine.State, Is.EqualTo(PostureState.Flattening));

            machine.Tick(PostureTarget.Flat, false, FlattenDuration);
            Assert.That(machine.State, Is.EqualTo(PostureState.Flat));
        }

        [Test]
        public void Flattening_IgnoresTargetChangeUntilFinished()
        {
            PostureStateMachine machine = CreateMachine();
            machine.Tick(PostureTarget.Flat, false, 0.016f);

            // Цель сменилась сразу, но клип падения ещё играет.
            machine.Tick(PostureTarget.Standing, false, FlattenDuration * 0.5f);
            Assert.That(machine.State, Is.EqualTo(PostureState.Flattening));
            Assert.That(machine.IsLocked, Is.True);
        }

        [Test]
        public void FlatToStanding_PassesThroughStandingUp()
        {
            PostureStateMachine machine = CreateMachine();
            machine.Reset(PostureState.Flat);

            machine.Tick(PostureTarget.Standing, false, 0.016f);
            Assert.That(machine.State, Is.EqualTo(PostureState.StandingUp));

            machine.Tick(PostureTarget.Standing, false, StandUpDuration);
            Assert.That(machine.State, Is.EqualTo(PostureState.Standing));
        }

        [Test]
        public void StandingUp_IgnoresNonLevitatingTargetsUntilFinished()
        {
            PostureStateMachine machine = CreateMachine();
            machine.Reset(PostureState.StandingUp);

            // Цель вернулась в Standing раньше времени — клип вставания всё равно доигрывает.
            machine.Tick(PostureTarget.Standing, false, StandUpDuration * 0.5f);
            Assert.That(machine.State, Is.EqualTo(PostureState.StandingUp));
        }

        /// <summary>
        /// Единственное исключение из блокировки: подняли камеру во время вставания —
        /// персонажа подхватывают на середине подъёма, не долистав клип до конца.
        /// </summary>
        [Test]
        public void StandingUp_InterruptedByLevitatingTarget()
        {
            PostureStateMachine machine = CreateMachine();
            machine.Reset(PostureState.StandingUp);

            machine.Tick(PostureTarget.Levitating, false, StandUpDuration * 0.1f);
            Assert.That(machine.State, Is.EqualTo(PostureState.Levitating));
        }

        [Test]
        public void Levitation_EntersWithoutLock()
        {
            PostureStateMachine machine = CreateMachine();

            machine.Tick(PostureTarget.Levitating, false, 0.016f);
            Assert.That(machine.State, Is.EqualTo(PostureState.Levitating));
            Assert.That(machine.IsLocked, Is.False);
        }

        /// <summary>
        /// Отпущенный персонаж не встаёт мягко на ноги — он падает до земли,
        /// независимо от того, куда в этот момент указывает цель.
        /// </summary>
        [Test]
        public void ReleasingLevitation_EntersFallingNotStanding()
        {
            PostureStateMachine machine = CreateMachine();
            machine.Reset(PostureState.Levitating);

            machine.Tick(PostureTarget.Standing, false, 0.016f);
            Assert.That(machine.State, Is.EqualTo(PostureState.Falling));
        }

        [Test]
        public void ReleasingLevitationTowardFlatTarget_AlsoEntersFalling()
        {
            PostureStateMachine machine = CreateMachine();
            machine.Reset(PostureState.Levitating);

            machine.Tick(PostureTarget.Flat, false, 0.016f);
            Assert.That(machine.State, Is.EqualTo(PostureState.Falling));
        }

        [Test]
        public void Falling_StaysUntilLanded()
        {
            PostureStateMachine machine = CreateMachine();
            machine.Reset(PostureState.Falling);

            machine.Tick(PostureTarget.Standing, hasLanded: false, deltaTime: 0.016f);
            Assert.That(machine.State, Is.EqualTo(PostureState.Falling));
        }

        [Test]
        public void Falling_LandingEntersFlattening()
        {
            PostureStateMachine machine = CreateMachine();
            machine.Reset(PostureState.Falling);

            machine.Tick(PostureTarget.Standing, hasLanded: true, deltaTime: 0.016f);
            Assert.That(machine.State, Is.EqualTo(PostureState.Flattening));
        }
    }
}
