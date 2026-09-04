using ARCharacter.Core;
using UnityEngine;

namespace ARCharacter
{
    /// <summary>
    /// Показывает сигнал вьюпорта: сырой, отфильтрованный и посчитанный самой Unity.
    /// Последний нужен как перекрёстная проверка: <see cref="ViewportMath"/> считает
    /// проекцию вручную, а ошибку в соглашении об осях компилятор не поймает —
    /// поймает только расхождение с эталоном.
    /// </summary>
    public sealed class PerceptionDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PerceptionProbe _probe;

        [Tooltip("Необязателен: если назначен, показываются поза, бег и высота.")]
        [SerializeField] private CharacterMotor _motor;

        [Tooltip("Камера для эталонного WorldToViewportPoint. Только для сверки.")]
        [SerializeField] private Camera _referenceCamera;

        [SerializeField] private bool _visible = true;

        private GUIStyle _style;

        private void OnGUI()
        {
            if (!_visible || !DebugDisplaySettings.ShowDebugUI)
                return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height * 0.022f),
                richText = true
            };

            GUILayout.BeginArea(new Rect(12f, 12f, Screen.width * 0.62f, Screen.height * 0.5f));
            // Молчаливый отказ при неназначенной ссылке неотличим от неработающего
            // скрипта, поэтому о неполной настройке сообщаем на экране.
            GUILayout.Label(_probe == null
                ? "<b>Перцепция:</b> <color=red>PerceptionProbe не назначен</color>"
                : BuildText(), _style);
            GUILayout.EndArea();
        }

        private string BuildText()
        {
            if (!_probe.HasValue)
                return "<b>Перцепция:</b> нет данных (не назначено окружение или цель)";

            CharacterPerception perception = _probe.Current;
            Vector2 drift = perception.RawViewport - perception.Viewport;

            string reference = "камера не назначена";
            if (_referenceCamera != null)
            {
                Vector3 unityViewport = _referenceCamera.WorldToViewportPoint(_probe.SamplePoint);
                float mismatch = Vector2.Distance(
                    new Vector2(unityViewport.x, unityViewport.y), perception.RawViewport);

                string verdict = mismatch < 0.001f ? "совпадает" : "<color=red>РАСХОЖДЕНИЕ</color>";
                reference = $"{unityViewport.x:F4}; {unityViewport.y:F4}  →  {verdict} ({mismatch:F5})";
            }

            string text =
                $"<b>сырой</b>      x {perception.RawViewport.x:F4}   y {perception.RawViewport.y:F4}\n" +
                $"<b>фильтр</b>     x {perception.Viewport.x:F4}   y {perception.Viewport.y:F4}\n" +
                $"<b>снято</b>      {drift.magnitude:F5}\n" +
                $"<b>дистанция</b>  {perception.Distance:F3} м     на экране: {(perception.OnScreen ? "да" : "нет")}\n" +
                $"<b>эталон</b>     {reference}";

            if (_motor == null)
                return text;

            return text +
                   $"\n\n<b>поза</b>       {Describe(_motor.State)}\n" +
                   $"<b>бег</b>        {_motor.RunIntensity:F2}\n" +
                   $"<b>высота</b>     {_motor.Height:F3} м";
        }

        private static string Describe(PostureState state) => state switch
        {
            PostureState.Standing => "стоит",
            PostureState.Flattening => "<color=yellow>падает</color>",
            PostureState.Flat => "лежит",
            PostureState.StandingUp => "<color=yellow>встаёт</color>",
            PostureState.Levitating => "парит",
            _ => state.ToString()
        };
    }
}
