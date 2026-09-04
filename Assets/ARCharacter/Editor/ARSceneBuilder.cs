using System.IO;
using ARCharacter.AR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace ARCharacter.EditorTools
{
    /// <summary>
    /// Собирает AR-сцену: сессия, XR Origin с трекингом телефона, менеджеры плоскостей,
    /// рейкастов и якорей, и тот же персонаж, что и в Sandbox — отличается только
    /// источник IEnvironment. XR Origin создаётся через штатный пункт меню AR Foundation,
    /// а не руками: внутри он привязывает TrackedPoseDriver и к XRHMD, и к
    /// HandheldARInputDevice, и переписывать эту привязку самому — верный способ
    /// получить камеру, которая не двигается на телефоне.
    /// </summary>
    public static class ARSceneBuilder
    {
        private const string ScenePath = "Assets/ARCharacter/Scenes/AR.unity";
        private const string PlanePrefabPath = "Assets/ARCharacter/Prefabs/ARPlaneVisual.prefab";

        [MenuItem("ARCharacter/Собрать сцену AR")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateSession();

            XROrigin origin = CreateOrigin();
            if (origin == null)
                return;

            var planeManager = origin.gameObject.AddComponent<ARPlaneManager>();
            var raycastManager = origin.gameObject.AddComponent<ARRaycastManager>();
            var anchorManager = origin.gameObject.AddComponent<ARAnchorManager>();
            // Ручной режим по умолчанию: сканирование стартует по кнопке, а не сразу.
            planeManager.enabled = false;

            GameObject planePrefab = GetOrCreatePlanePrefab();
            if (planePrefab != null)
                planeManager.planePrefab = planePrefab;

            ARFoundationEnvironment environment = CreateEnvironment(origin.Camera);
            // RequireComponent сам добавит AROcclusionManager.
            origin.Camera.gameObject.AddComponent<DepthOcclusionSetup>();

            Light light = CreateLight();
            CreateLightEstimation(origin.Camera, light);

            CharacterAssembly.Result character = CharacterAssembly.Build(environment);
            CreateShadowCatcher(character.Character);
            ConfigureGroundClearance(character.Motor);
            // Ждёт размещения: включает его ARPlacementController после выбора поверхности.
            character.Character.gameObject.SetActive(false);

            ARPlacementController placement =
                CreatePlacementController(planeManager, raycastManager, anchorManager, environment, character.Character);
            CreatePlacementDebugUI(placement);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"Сцена собрана: {ScenePath}");
        }

        private static void CreateSession()
            => new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));

        private static XROrigin CreateOrigin()
        {
            Selection.activeGameObject = null;
            EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (Mobile AR)");

            GameObject created = Selection.activeGameObject;
            XROrigin origin = created != null ? created.GetComponent<XROrigin>() : null;
            if (origin == null)
            {
                Debug.LogError(
                    "Не удалось создать XR Origin через меню GameObject/XR/XR Origin (Mobile AR). " +
                    "Проверьте версию AR Foundation — путь пункта меню мог измениться.");
            }

            return origin;
        }

        private static GameObject GetOrCreatePlanePrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath);
            if (existing != null)
                return existing;

            Selection.activeGameObject = null;
            EditorApplication.ExecuteMenuItem("GameObject/XR/AR Default Plane");

            GameObject created = Selection.activeGameObject;
            if (created == null)
            {
                Debug.LogWarning(
                    "Не удалось создать визуализатор плоскости через GameObject/XR/AR Default Plane — " +
                    "плоскости будут обнаруживаться, но не отображаться на экране.");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PlanePrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(created, PlanePrefabPath);
            Object.DestroyImmediate(created);
            return prefab;
        }

        private static Light CreateLight()
        {
            var go = new GameObject("Directional Light");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            // Стартовые значения — LightEstimationSetup перепишет их, как только
            // придёт первый кадр с оценкой освещения; до этого сцена не должна быть чёрной.
            light.intensity = 1.2f;
            go.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            return light;
        }

        private static void CreateLightEstimation(Camera arCamera, Light light)
        {
            LightEstimationSetup lightEstimation = arCamera.gameObject.AddComponent<LightEstimationSetup>();
            var serialized = new SerializedObject(lightEstimation);
            serialized.FindProperty("_targetLight").objectReferenceValue = light;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Только для AR-сцены: в Sandbox уже есть непрозрачный пол, принимающий
        /// обычные тени Unity, а тут «пол» — это видео с камеры, ловить тень
        /// на него нечем без специального прозрачного шейдера.
        /// </summary>
        private static void CreateShadowCatcher(Transform character)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ShadowCatcher";
            quad.transform.SetParent(character, false);
            // Quad по умолчанию стоит вертикально — кладём лицом вверх, на поверхность.
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float size = CharacterDimensions.Height * 4f;
            quad.transform.localScale = new Vector3(size, size, 1f);
            // Небольшой отступ от нуля — чтобы не пересекаться с визуалом персонажа у основания.
            quad.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            Object.DestroyImmediate(quad.GetComponent<Collider>());

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = GetOrCreateShadowCatcherMaterial();
        }

        private static Material GetOrCreateShadowCatcherMaterial()
        {
            const string folder = "Assets/ARCharacter/Art";
            const string path = folder + "/ShadowCatcher.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("ARCharacter/ShadowCatcher");
            if (shader == null)
            {
                Debug.LogError("Шейдер ARCharacter/ShadowCatcher не найден — тень-контактник не будет виден.");
                return null;
            }

            Directory.CreateDirectory(folder);
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// 1.5 см — компенсация шума оценки глубины у самой земли: без зазора окклюзия
        /// иногда «срезает» стоящего персонажа, будто он проваливается в пол. В Sandbox
        /// этого не нужно (там настоящая геометрия пола), поэтому правится точечно здесь,
        /// а не в общем CharacterAssembly.
        /// </summary>
        private static void ConfigureGroundClearance(CharacterMotor motor)
        {
            var serialized = new SerializedObject(motor);
            serialized.FindProperty("_groundClearance").floatValue = 0.015f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ARFoundationEnvironment CreateEnvironment(Camera arCamera)
        {
            ARFoundationEnvironment environment = arCamera.gameObject.AddComponent<ARFoundationEnvironment>();

            var serialized = new SerializedObject(environment);
            serialized.FindProperty("_camera").objectReferenceValue = arCamera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return environment;
        }

        private static ARPlacementController CreatePlacementController(
            ARPlaneManager planeManager, ARRaycastManager raycastManager, ARAnchorManager anchorManager,
            ARFoundationEnvironment environment, Transform characterRoot)
        {
            var go = new GameObject("Placement");
            ARPlacementController placement = go.AddComponent<ARPlacementController>();

            var serialized = new SerializedObject(placement);
            serialized.FindProperty("_planeManager").objectReferenceValue = planeManager;
            serialized.FindProperty("_raycastManager").objectReferenceValue = raycastManager;
            serialized.FindProperty("_anchorManager").objectReferenceValue = anchorManager;
            serialized.FindProperty("_environment").objectReferenceValue = environment;
            serialized.FindProperty("_characterRoot").objectReferenceValue = characterRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return placement;
        }

        private static void CreatePlacementDebugUI(ARPlacementController placement)
        {
            PlacementDebugUI ui = placement.gameObject.AddComponent<PlacementDebugUI>();
            var serialized = new SerializedObject(ui);
            serialized.FindProperty("_placement").objectReferenceValue = placement;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
