using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARCharacter.EditorTools
{
    /// <summary>
    /// Собирает сцену Sandbox кодом, а не руками. Сцена — расходный материал: её придётся
    /// пересобирать при смене масштаба и раскладки, и повторяемый скрипт надёжнее, чем
    /// правка YAML или восстановление расстановки по памяти. Сборка самого персонажа —
    /// в CharacterAssembly, общей с AR-сценой.
    /// </summary>
    public static class SandboxSceneBuilder
    {
        private const string ScenePath = "Assets/ARCharacter/Scenes/Sandbox.unity";
        private const string MaterialFolder = "Assets/ARCharacter/Art/Sandbox";
        private const float SurfaceSizeMeters = 2f;

        [MenuItem("ARCharacter/Собрать сцену Sandbox")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material groundMaterial = CreateMaterial("SandboxGround", new Color(0.42f, 0.44f, 0.47f));

            CreateLight();
            Transform surface = CreateSurface(groundMaterial);
            Camera camera = CreateCamera();
            MockEnvironment environment = CreateEnvironment(camera, surface);

            CharacterAssembly.Build(environment, camera);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"Сцена собрана: {ScenePath}");
        }

        private static void CreateLight()
        {
            var go = new GameObject("Directional Light");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static Transform CreateSurface(Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Surface";
            // Примитив Plane имеет размер 10 юнитов, поэтому масштаб приводит его к метрам.
            go.transform.localScale = Vector3.one * (SurfaceSizeMeters / 10f);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30f;
            // Поле зрения близко к типичной камере смартфона в портретной ориентации.
            camera.fieldOfView = 60f;
            go.AddComponent<AudioListener>();
            go.AddComponent<FreeLookCamera>();

            // Ракурс человека, держащего телефон и смотрящего на миниатюру у своих ног.
            go.transform.position = new Vector3(0f, 1.3f, -0.9f);
            go.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            return camera;
        }

        private static MockEnvironment CreateEnvironment(Camera camera, Transform surface)
        {
            var go = new GameObject("Environment");
            MockEnvironment environment = go.AddComponent<MockEnvironment>();

            var serialized = new SerializedObject(environment);
            serialized.FindProperty("_camera").objectReferenceValue = camera;
            serialized.FindProperty("_surface").objectReferenceValue = surface;
            serialized.FindProperty("_surfaceSize").vector2Value = Vector2.one * SurfaceSizeMeters;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return environment;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
