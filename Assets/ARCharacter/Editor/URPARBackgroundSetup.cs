using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;

namespace ARCharacter.EditorTools
{
    /// <summary>
    /// Без AR Background Rendering Feature на Renderer Data URP не подключает видео
    /// с камеры к пайплайну: ARCameraBackground исправно обновляет свой материал,
    /// но рисовать его в кадре некому, и вместо трансляции виден фон рендерера как есть.
    /// Это известная особенность связки AR Foundation + URP, а не баг конкретной сцены,
    /// поэтому чинится один раз на уровне ассета, а не в билдере сцены.
    /// </summary>
    public static class URPARBackgroundSetup
    {
        private const string RendererPath = "Assets/Settings/Mobile_Renderer.asset";

        [MenuItem("ARCharacter/Добавить AR Background Rendering Feature")]
        public static void Apply()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (renderer == null)
            {
                Debug.LogError($"Не найден рендерер по пути {RendererPath}.");
                return;
            }

            if (renderer.rendererFeatures.Any(f => f is ARBackgroundRendererFeature))
            {
                Debug.Log("AR Background Rendering Feature уже добавлена, ничего не делаю.");
                return;
            }

            var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            feature.name = "AR Background Rendering Feature";

            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            renderer.SetDirty();

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RendererPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"AR Background Rendering Feature добавлена в {RendererPath}.");
        }
    }
}
