using UnityEngine;

namespace ARCharacter.Core
{
    public enum PostureState
    {
        Standing,
        Flattening,
        Flat,
        StandingUp,
        Levitating,
        Falling
    }

    /// <summary>
    /// Достраивает к желаемой позе переходы, которые нельзя прерывать произвольно.
    /// Падение и приземление — одноразовые клипы: оборвать их на середине значит
    /// оставить персонажа в позе полуприседа, поэтому на время их проигрывания смена
    /// позы блокируется. Одно исключение: вставание можно прервать подъёмом камеры —
    /// персонажа подхватывают на середине, как будто взяли рукой. Парение и падение
    /// такой защиты не требуют — вход и выход из зацикленного клипа делаются обычным
    /// кроссфейдом, а не одноразовым переходом.
    /// </summary>
    public sealed class PostureStateMachine
    {
        private float _flattenDuration = 0.9f;
        private float _standUpDuration = 5f;
        private float _elapsed;

        public PostureState State { get; private set; } = PostureState.Standing;

        /// <summary>Идёт непрерываемый переход (кроме прерывания вставания подъёмом).</summary>
        public bool IsLocked => State == PostureState.Flattening || State == PostureState.StandingUp;

        public void Configure(float flattenDuration, float standUpDuration)
        {
            _flattenDuration = Mathf.Max(0.01f, flattenDuration);
            _standUpDuration = Mathf.Max(0.01f, standUpDuration);
        }

        public void Reset(PostureState state)
        {
            State = state;
            _elapsed = 0f;
        }

        /// <param name="hasLanded">
        /// Высота персонажа опустилась до земли. Имеет значение только в состоянии
        /// Falling — вне его игнорируется. Считается снаружи (CharacterMotor знает
        /// высоту), поэтому машина состояний остаётся чистой и не зависит от Unity.
        /// </param>
        public void Tick(PostureTarget target, bool hasLanded, float deltaTime)
        {
            // Единственное прерывание: подняли камеру во время вставания — персонажа
            // подхватывают на середине подъёма, не долистав клип до конца.
            if (State == PostureState.StandingUp && target == PostureTarget.Levitating)
            {
                Enter(PostureState.Levitating);
                return;
            }

            if (State == PostureState.Flattening || State == PostureState.StandingUp)
            {
                _elapsed += deltaTime;
                float duration = State == PostureState.Flattening ? _flattenDuration : _standUpDuration;
                if (_elapsed < duration)
                    return;

                Enter(State == PostureState.Flattening ? PostureState.Flat : PostureState.Standing);
                return;
            }

            switch (State)
            {
                case PostureState.Flat when target != PostureTarget.Flat:
                    Enter(PostureState.StandingUp);
                    break;

                case PostureState.Standing when target == PostureTarget.Flat:
                    Enter(PostureState.Flattening);
                    break;

                case PostureState.Standing when target == PostureTarget.Levitating:
                    Enter(PostureState.Levitating);
                    break;

                case PostureState.Levitating when target != PostureTarget.Levitating:
                    // Отпустили персонажа — не мягкая посадка на ноги, а падение до земли.
                    Enter(PostureState.Falling);
                    break;

                case PostureState.Falling when hasLanded:
                    // Долетел до земли: приземление сбивает с ног так же, как обычное падение.
                    Enter(PostureState.Flattening);
                    break;
            }
        }

        private void Enter(PostureState state)
        {
            State = state;
            _elapsed = 0f;
        }
    }
}
