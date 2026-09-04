using UnityEngine;

namespace ARCharacter.Core
{
    public enum EnvironmentTracking
    {
        None,
        Limited,
        Tracking
    }

    /// <summary>
    /// Всё, что логика поведения знает о внешнем мире: где камера, как она проецирует
    /// и есть ли поверхность. Намеренно узкий интерфейс — от него зависит, что механику
    /// можно гонять в редакторе на моке и воспроизводить из записи, а не только на телефоне.
    /// Проекция отдаётся матрицей, а не ссылкой на Camera, чтобы запись кадра была
    /// самодостаточной и воспроизведение не зависело от настроек сцены.
    /// </summary>
    public interface IEnvironment
    {
        Pose CameraPose { get; }

        Matrix4x4 ProjectionMatrix { get; }

        EnvironmentTracking Tracking { get; }

        bool TryGetSurface(out SurfacePlane surface);
    }
}
