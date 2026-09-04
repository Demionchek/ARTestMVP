using UnityEngine;

namespace ARCharacter.Core
{
    /// <summary>
    /// Перевод мировой точки в координаты вьюпорта без обращения к Camera.
    /// Вынесено отдельно, потому что это единственный вход всей механики: и живой AR,
    /// и мок, и воспроизведение записи считают его одинаково.
    /// </summary>
    public static class ViewportMath
    {
        /// <summary>
        /// Возвращает позицию в координатах вьюпорта: X и Y в диапазоне 0..1 от левого
        /// нижнего угла, Z — расстояние вдоль взгляда камеры. Отрицательный Z означает,
        /// что точка позади камеры и X/Y смысла не имеют.
        /// </summary>
        public static Vector3 WorldToViewport(Pose cameraPose, Matrix4x4 projection, Vector3 worldPoint)
        {
            Matrix4x4 worldToLocal = Matrix4x4.TRS(cameraPose.position, cameraPose.rotation, Vector3.one).inverse;

            // В пространстве трансформа камера смотрит вдоль +Z, а матрица проекции
            // построена под противоположное соглашение. Инверсия нужна ровно одна:
            // двойная зеркалит точку относительно центра экрана.
            Vector3 local = worldToLocal.MultiplyPoint3x4(worldPoint);
            var view = new Vector4(local.x, local.y, -local.z, 1f);

            Vector4 clip = projection * view;
            if (Mathf.Approximately(clip.w, 0f))
                return new Vector3(0.5f, 0.5f, local.z);

            return new Vector3(
                (clip.x / clip.w) * 0.5f + 0.5f,
                (clip.y / clip.w) * 0.5f + 0.5f,
                local.z);
        }

        /// <summary>Точка видна на экране и находится перед камерой.</summary>
        public static bool IsOnScreen(Vector3 viewport)
            => viewport.z > 0f
               && viewport.x >= 0f && viewport.x <= 1f
               && viewport.y >= 0f && viewport.y <= 1f;
    }
}
