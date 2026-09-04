using System.IO;
using UnityEditor;
using UnityEngine;

namespace ARCharacter.EditorTools
{
    /// <summary>
    /// Собирает персонажа (пивот, визуал, перцепция, мотор, аниматор, взгляд на камеру)
    /// одинаково для Sandbox и AR-сцены — единственное, чем они отличаются, это реализация
    /// IEnvironment, а не устройство самого персонажа. Вынесено сюда, а не продублировано
    /// в обоих билдерах, потому что изменение состава компонентов персонажа иначе
    /// требовало бы правки в двух местах synchronно.
    /// </summary>
    public static class CharacterAssembly
    {
        public readonly struct Result
        {
            public Result(Transform character, PerceptionProbe probe, CharacterMotor motor)
            {
                Character = character;
                Probe = probe;
                Motor = motor;
            }

            public Transform Character { get; }

            public PerceptionProbe Probe { get; }

            public CharacterMotor Motor { get; }
        }

        /// <param name="environment">Источник позы камеры и поверхности.</param>
        /// <param name="referenceCamera">
        /// Опционально: вторая камера для перекрёстной проверки проекции в отладочном
        /// оверлее. В Sandbox это камера сцены; в AR сверять не с чем — оверлей сам
        /// покажет «камера не назначена» и просто не будет сверяться.
        /// </param>
        public static Result Build(EnvironmentBehaviour environment, Camera referenceCamera = null)
        {
            Transform character = CreatePlaceholder();
            PerceptionProbe probe = CreatePerception(environment, character, referenceCamera);
            CharacterMotor motor = CreateMotor(character, probe, environment);
            LinkHeightSourceToProbe(probe, motor);
            LinkMotorToOverlay(probe, motor);
            CreateAnimatorDriver(character, motor);
            CreateHeadLook(character, motor, environment);
            return new Result(character, probe, motor);
        }

        private static Transform CreatePlaceholder()
        {
            var pivot = new GameObject("Character");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAnimatorBuilder.PrefabPath);
            if (prefab != null)
            {
                // Префаб уже несёт нужный масштаб и Animator — собран CharacterAnimatorBuilder.
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Visual";
                instance.transform.SetParent(pivot.transform, false);
                return pivot.transform;
            }

            // Запасной вариант до сборки Animator и префаба: примитив на месте модели.
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "Visual";
            fallback.transform.SetParent(pivot.transform, false);
            fallback.transform.localScale = Vector3.one * (CharacterDimensions.Height / 2f);
            // Меш капсулы отцентрован, поэтому визуал приподнят внутри пустышки,
            // чтобы точка отсчёта совпадала с опорой на поверхность.
            fallback.transform.localPosition = Vector3.up * (CharacterDimensions.Height * 0.5f);
            fallback.GetComponent<MeshRenderer>().sharedMaterial = CreateFallbackMaterial();
            Object.DestroyImmediate(fallback.GetComponent<Collider>());

            return pivot.transform;
        }

        private static PerceptionProbe CreatePerception(
            EnvironmentBehaviour environment, Transform character, Camera referenceCamera)
        {
            var go = new GameObject("Perception");
            PerceptionProbe probe = go.AddComponent<PerceptionProbe>();
            PerceptionDebugOverlay overlay = go.AddComponent<PerceptionDebugOverlay>();

            var serializedProbe = new SerializedObject(probe);
            serializedProbe.FindProperty("_environment").objectReferenceValue = environment;
            serializedProbe.FindProperty("_target").objectReferenceValue = character;
            // Запасное значение на случай, если _heightSource не назначен.
            serializedProbe.FindProperty("_sampleHeight").floatValue = CharacterDimensions.Height * 0.5f;
            serializedProbe.ApplyModifiedPropertiesWithoutUndo();

            var serializedOverlay = new SerializedObject(overlay);
            serializedOverlay.FindProperty("_probe").objectReferenceValue = probe;
            serializedOverlay.FindProperty("_referenceCamera").objectReferenceValue = referenceCamera;
            serializedOverlay.ApplyModifiedPropertiesWithoutUndo();
            return probe;
        }

        private static CharacterMotor CreateMotor(
            Transform character, PerceptionProbe probe, EnvironmentBehaviour environment)
        {
            CharacterMotor motor = character.gameObject.AddComponent<CharacterMotor>();

            var serialized = new SerializedObject(motor);
            serialized.FindProperty("_probe").objectReferenceValue = probe;
            serialized.FindProperty("_environment").objectReferenceValue = environment;
            serialized.FindProperty("_visual").objectReferenceValue = character.GetChild(0);
            // 0.425 — подобранная на глаз доля роста (ближе к бёдрам, а не к груди).
            serialized.FindProperty("_standingSampleHeight").floatValue = CharacterDimensions.Height * 0.425f;
            // Миниатюра 40 см: метровые скорости для неё бессмысленны.
            serialized.FindProperty("_runSpeed").floatValue = CharacterDimensions.Height * 0.9f;
            // Высота подъёма сознательно не капается на рост персонажа — раньше 0.9x
            // было заметно тесно (почти вровень с собственным ростом), 3x даёт
            // ощутимый, «не упёртый в потолок» подъём над миниатюрой.
            serialized.FindProperty("_maxLevitationHeight").floatValue = CharacterDimensions.Height * 3f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return motor;
        }

        private static void CreateAnimatorDriver(Transform character, CharacterMotor motor)
        {
            // У запасной капсулы Animator нет — до сборки CharacterAnimatorBuilder
            // драйверу нечего показывать.
            Animator animator = character.GetComponentInChildren<Animator>();
            if (animator == null)
                return;

            CharacterAnimatorDriver driver = character.gameObject.AddComponent<CharacterAnimatorDriver>();
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("_motor").objectReferenceValue = motor;
            serialized.FindProperty("_animator").objectReferenceValue = animator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateHeadLook(Transform character, CharacterMotor motor, EnvironmentBehaviour environment)
        {
            Animator animator = character.GetComponentInChildren<Animator>();
            if (animator == null)
                return;

            // OnAnimatorIK вызывается только на объекте с самим Animator, поэтому
            // компонент вешается на Visual, а не на пивот с CharacterMotor.
            HeadLookController headLook = animator.gameObject.AddComponent<HeadLookController>();
            var serialized = new SerializedObject(headLook);
            serialized.FindProperty("_animator").objectReferenceValue = animator;
            serialized.FindProperty("_motor").objectReferenceValue = motor;
            serialized.FindProperty("_environment").objectReferenceValue = environment;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LinkMotorToOverlay(PerceptionProbe probe, CharacterMotor motor)
        {
            var overlay = probe.GetComponent<PerceptionDebugOverlay>();
            var serialized = new SerializedObject(overlay);
            serialized.FindProperty("_motor").objectReferenceValue = motor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LinkHeightSourceToProbe(PerceptionProbe probe, CharacterMotor motor)
        {
            var serialized = new SerializedObject(probe);
            serialized.FindProperty("_heightSource").objectReferenceValue = motor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material CreateFallbackMaterial()
        {
            const string folder = "Assets/ARCharacter/Art/Sandbox";
            const string path = folder + "/SandboxPlaceholder.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Directory.CreateDirectory(folder);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = new Color(0.85f, 0.55f, 0.25f) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
