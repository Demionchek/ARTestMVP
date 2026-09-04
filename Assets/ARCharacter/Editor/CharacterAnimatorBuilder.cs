using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ARCharacter.EditorTools
{
    /// <summary>
    /// Строит Animator Controller и префаб персонажа кодом: состояний и переходов
    /// достаточно много, чтобы собирать их руками в редакторе, — то же соображение,
    /// что и для SandboxSceneBuilder. Пересборка полностью перезаписывает оба ассета,
    /// поэтому ручные правки в Animator-окне между запусками не переживут повторный вызов.
    /// </summary>
    public static class CharacterAnimatorBuilder
    {
        private const string ArtFolder = "Assets/ARCharacter/Art/Mixamo";
        private const string OutputFolder = "Assets/ARCharacter/Prefabs";
        private const string ControllerPath = OutputFolder + "/CharacterAnimator.controller";

        public const string PrefabPath = OutputFolder + "/CharacterView.prefab";

        // Любой экспорт Mixamo подходит как источник меша и аватара — скелет одинаков
        // во всех файлах одного персонажа, конкретный выбор роли не играет.
        private const string ModelSourceClip = "Breathing Idle";

        private const float CrossfadeDuration = 0.15f;

        [MenuItem("ARCharacter/Собрать Animator и префаб персонажа")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputFolder);

            AnimatorController controller = BuildController();
            if (controller == null)
                return;

            BuildPrefab(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"Собраны {ControllerPath} и {PrefabPath}");
        }

        private static AnimatorController BuildController()
        {
            AnimationClip idle = LoadClip("Breathing Idle");
            AnimationClip running = LoadClip("Running");
            AnimationClip fallFlat = LoadClip("Fall Flat");
            AnimationClip gettingUp = LoadClip("Getting Up");
            AnimationClip falling = LoadClip("Falling");
            AnimationClip dwarfIdle = LoadClip("Dwarf Idle");
            AnimationClip happyIdle = LoadClip("Happy Idle");
            AnimationClip sadIdle = LoadClip("Sad Idle");

            if (idle == null || running == null || fallFlat == null || gettingUp == null
                || falling == null || dwarfIdle == null || happyIdle == null || sadIdle == null)
                return null;

            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Flatten", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("StandUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Levitate", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Stand", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IdleVariation", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IdleVariationIndex", AnimatorControllerParameterType.Int);

            // Без IK-пасса на слое Animator не вызывает OnAnimatorIK вообще —
            // взгляд на камеру (HeadLookController) молча не работал бы.
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;

            AnimatorStateMachine root = controller.layers[0].stateMachine;

            AnimatorState locomotion = AddLocomotionState(controller, root, idle, running);
            root.defaultState = locomotion;

            // Loop Time на этих клипах уже настроен верно при экспорте из Mixamo:
            // Fall Flat/Getting Up — одноразовые (Mecanim сам держит последний кадр
            // Fall Flat, то есть позу лёжа, без отдельного клипа под неё), Falling — цикл.
            AnimatorState fallFlatState = root.AddState("FallFlat");
            fallFlatState.motion = fallFlat;

            AnimatorState gettingUpState = root.AddState("GettingUp");
            gettingUpState.motion = gettingUp;

            AnimatorState levitateState = root.AddState("Levitate");
            levitateState.motion = falling;

            AddAnyStateTrigger(root, fallFlatState, "Flatten");
            AddAnyStateTrigger(root, gettingUpState, "StandUp");
            AddAnyStateTrigger(root, levitateState, "Levitate");
            AddAnyStateTrigger(root, locomotion, "Stand");

            // Помимо триггера Stand (на случай рассинхрона с длительностью клипа),
            // вставание доигрывает клип и возвращается в локомоцию само по себе.
            AddExitTimeTransition(gettingUpState, locomotion);

            AnimatorState dwarfState = root.AddState("IdleDwarf");
            dwarfState.motion = dwarfIdle;
            AnimatorState happyState = root.AddState("IdleHappy");
            happyState.motion = happyIdle;
            AnimatorState sadState = root.AddState("IdleSad");
            sadState.motion = sadIdle;

            AddIdleVariation(locomotion, dwarfState, 0);
            AddIdleVariation(locomotion, happyState, 1);
            AddIdleVariation(locomotion, sadState, 2);

            return controller;
        }

        private static AnimatorState AddLocomotionState(
            AnimatorController controller, AnimatorStateMachine root, AnimationClip idle, AnimationClip running)
        {
            var blendTree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed"
            };
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(running, 1f);

            AnimatorState state = root.AddState("Locomotion");
            state.motion = blendTree;
            return state;
        }

        private static void AddAnyStateTrigger(AnimatorStateMachine root, AnimatorState destination, string trigger)
        {
            AnimatorStateTransition transition = root.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.duration = CrossfadeDuration;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void AddExitTimeTransition(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = CrossfadeDuration;
        }

        /// <summary>
        /// Вариация простоя: короткий жест поверх Idle, запускается по триггеру
        /// с конкретным индексом и либо доигрывает сама, либо обрывается, стоит
        /// персонажу начать бег — иначе жест мог бы длиться поверх уже бегущего персонажа.
        /// </summary>
        private static void AddIdleVariation(AnimatorState locomotion, AnimatorState variation, int index)
        {
            AnimatorStateTransition into = locomotion.AddTransition(variation);
            into.hasExitTime = false;
            into.duration = CrossfadeDuration;
            into.AddCondition(AnimatorConditionMode.If, 0f, "IdleVariation");
            into.AddCondition(AnimatorConditionMode.Equals, index, "IdleVariationIndex");

            AnimatorStateTransition finish = variation.AddTransition(locomotion);
            finish.hasExitTime = true;
            finish.exitTime = 1f;
            finish.hasFixedDuration = true;
            finish.duration = CrossfadeDuration;

            AnimatorStateTransition interrupt = variation.AddTransition(locomotion);
            interrupt.hasExitTime = false;
            interrupt.duration = CrossfadeDuration;
            interrupt.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
        }

        private static void BuildPrefab(AnimatorController controller)
        {
            string modelPath = $"{ArtFolder}/Ch19_nonPBR@{ModelSourceClip}.fbx";
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"Не найдена модель по пути {modelPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = "CharacterView";

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            // Перемещение — целиком на CharacterMotor; root motion конфликтовал бы
            // и с прижатием к границе плоскости, и с якорем в AR-сцене.
            animator.applyRootMotion = false;

            // Модель приходит в масштабе человека (~1.8 м); приводим к росту миниатюры
            // и запекаем масштаб в префаб, чтобы он был канонического размера
            // везде, где его переиспользуют, без досчёта на месте использования.
            float measuredHeight = MeasureHeight(instance);
            float scale = CharacterDimensions.Height / Mathf.Max(0.01f, measuredHeight);
            instance.transform.localScale = Vector3.one * scale;

            if (File.Exists(PrefabPath))
                AssetDatabase.DeleteAsset(PrefabPath);

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
        }

        private static float MeasureHeight(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return 1f;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            return bounds.size.y;
        }

        private static AnimationClip LoadClip(string mixamoName)
        {
            string path = $"{ArtFolder}/Ch19_nonPBR@{mixamoName}.fbx";
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.Contains("__preview__"));

            if (clip == null)
                Debug.LogError($"Не найден AnimationClip в {path}");

            return clip;
        }
    }
}
