using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace ARCharacter.EditorTools
{
    /// <summary>
    /// Настройки сборки под Android/ARCore, собранные в одном месте.
    /// Существует, чтобы конфигурацию можно было воспроизвести после клонирования
    /// репозитория или отката ProjectSettings, а не восстанавливать её по памяти.
    /// </summary>
    public static class AndroidPlayerSetup
    {
        private const string ApplicationIdentifier = "com.demio.artestmvp";

        [MenuItem("ARCharacter/Настроить Player Settings под Android")]
        public static void Configure()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApplicationIdentifier(android, ApplicationIdentifier);

            // Только ARM64: 64-битная сборка обязательна для Play Store, а добавление
            // ARMv7 удваивает время сборки IL2CPP, ничего не давая целевому устройству.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // ARCore с обязательным AR требует minSdk 24, но Unity 6000.3 не поддерживает
            // уровни ниже 25, поэтому берём нижнюю поддерживаемую границу.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;

            // GLES3 — вариант, с которым ARCore работает предсказуемо на всех драйверах.
            // Vulkan быстрее, но у него есть известные проблемы с отрисовкой фона камеры,
            // поэтому переключаемся на него только если упрёмся в производительность.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            AssetDatabase.SaveAssets();
            Debug.Log($"Player Settings: IL2CPP, ARM64, minSdk 25, GLES3, portrait, {ApplicationIdentifier}");
        }
    }
}
