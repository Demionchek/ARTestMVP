using ARCharacter;
using UnityEngine;

namespace ARCharacter.AR
{
    /// <summary>
    /// Временный пульт для ручной проверки размещения на устройстве: тумблер авто/
    /// ручного режима и кнопки скана/размещения. OnGUI, а не Canvas — это отладочная
    /// оснастка на время, пока размещение не проверено вживую, а не финальный UX
    /// (тот — отдельный поздний этап).
    /// </summary>
    public sealed class PlacementDebugUI : MonoBehaviour
    {
        [SerializeField] private ARPlacementController _placement;

        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;

        private void OnGUI()
        {
            if (_placement == null || !DebugDisplaySettings.ShowDebugUI)
                return;

            EnsureStyles();

            if (_placement.State != PlacementState.Placed)
                DrawReticle();

            float width = Screen.width - 40f;
            float height = 260f;
            GUILayout.BeginArea(new Rect(20f, Screen.height - height - 20f, width, height));

            bool isAuto = GUILayout.Toggle(
                _placement.Mode == PlacementMode.Auto, " Авто-размещение", _labelStyle);
            _placement.Mode = isAuto ? PlacementMode.Auto : PlacementMode.Manual;

            GUILayout.Space(16f);

            if (_placement.State == PlacementState.Placed)
            {
                // Пока размещён, сканирование выключено (см. HidePlanes) — рейкаст
                // по нему был бы ненадёжен. «Переставить» включает скан заново
                // явно, а не полагается на то, что он и так работает.
                if (GUILayout.Button("Переставить", _buttonStyle, GUILayout.Height(90f)))
                    _placement.Reposition();
            }
            else
            {
                using (new GUILayout.HorizontalScope())
                {
                    string scanLabel = _placement.IsScanning ? "Остановить скан" : "Сканировать";
                    if (GUILayout.Button(scanLabel, _buttonStyle, GUILayout.Height(90f)))
                        _placement.SetScanning(!_placement.IsScanning);

                    GUI.enabled = _placement.IsScanning;
                    if (GUILayout.Button("Разместить", _buttonStyle, GUILayout.Height(90f)))
                        _placement.PlaceAtScreenCenter();
                    GUI.enabled = true;
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label($"Состояние: {Describe(_placement.State)}", _labelStyle);

            GUILayout.EndArea();
        }

        /// <summary>
        /// Кнопка «Разместить» целится из центра экрана — без метки на экране
        /// пользователь не может понять, куда именно, и кнопка была бы вслепую.
        /// </summary>
        private void DrawReticle()
        {
            const float size = 24f;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - 1f, cy - size, 2f, size * 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - size, cy - 1f, size * 2f, 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (_buttonStyle != null)
                return;

            int fontSize = Mathf.RoundToInt(Screen.height * 0.024f);
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = fontSize };
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize, richText = true };
        }

        private static string Describe(PlacementState state) => state switch
        {
            PlacementState.Searching => "ищем поверхность",
            PlacementState.Placed => "размещён",
            _ => state.ToString()
        };
    }
}
