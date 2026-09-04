using ARCharacter.Core;
using UnityEngine;

namespace ARCharacter
{
    /// <summary>
    /// Базовый компонент для реализаций <see cref="IEnvironment"/>.
    /// Существует потому, что Unity не сериализует ссылки на интерфейсы: без общего
    /// предка источник окружения пришлось бы искать в рантайме и терять проверку типов.
    /// </summary>
    public abstract class EnvironmentBehaviour : MonoBehaviour, IEnvironment
    {
        public abstract Pose CameraPose { get; }

        public abstract Matrix4x4 ProjectionMatrix { get; }

        public abstract EnvironmentTracking Tracking { get; }

        public abstract bool TryGetSurface(out SurfacePlane surface);
    }
}
