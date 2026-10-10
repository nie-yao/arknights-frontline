using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Skills;
namespace ArknightsFrontline.Editor
{
    public static class NiuLaiRigSetup
    {
        public static void Prepare()
        {
            const string modelPath = "Assets/Game/Characters/NiuLai/Rigged/NiuLai_Rigged.fbx";
            const string prefabPath = "Assets/Game/Characters/NiuLai/Resources/NiuLaiPlayer.prefab";
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = true;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips) { clip.name = clip.name.Split('|').Last(); clip.loopTime = clip.name == "Idle" || clip.name == "Run"; }
            importer.clipAnimations = clips; importer.SaveAndReimport();
            var animations = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            Debug.Log("Cow animations: " + string.Join(",", animations.Select(c => c.name)));
            const string controllerPath = "Assets/Game/Characters/NiuLai/Rigged/NiuLai.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Flying", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float); controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle"); idle.motion = animations.Single(c => c.name == "Idle"); machine.defaultState = idle;
            var run = machine.AddState("Run"); run.motion = animations.Single(c => c.name == "Run");
            var attack = machine.AddState("Attack"); attack.motion = animations.Single(c => c.name == "Attack");
            var moving = idle.AddTransition(run); moving.hasExitTime = false; moving.duration = 0.1f; moving.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var stopping = run.AddTransition(idle); stopping.hasExitTime = false; stopping.duration = 0.1f; stopping.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            var strike = machine.AddAnyStateTransition(attack); strike.hasExitTime = false; strike.duration = 0.05f; strike.canTransitionToSelf = false; strike.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            var flight = machine.AddState("Flight"); flight.motion = animations.Single(c => c.name == "Flight");
            var takeoff = machine.AddAnyStateTransition(flight); takeoff.hasExitTime = false; takeoff.duration = 0.04f; takeoff.canTransitionToSelf = false; takeoff.AddCondition(AnimatorConditionMode.If, 0, "Flying");
            var land = flight.AddTransition(idle); land.hasExitTime = false; land.duration = 0.08f; land.AddCondition(AnimatorConditionMode.IfNot, 0, "Flying");
            strike.AddCondition(AnimatorConditionMode.IfNot, 0, "Flying");
            var finish = attack.AddTransition(idle); finish.hasExitTime = true; finish.exitTime = 1; finish.duration = 0.1f;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var old = root.transform.Find("NiuLaiVisual");
            Object.DestroyImmediate(old.gameObject);
            var visual = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
            visual.name = "NiuLaiVisual";
            var animator = visual.GetComponent<Animator>(); if (!animator) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Characters/NiuLai/Resources/NiuLai.mat");
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = mat;
            var bounds = visual.GetComponentInChildren<Renderer>().bounds;
            float factor = 2.4f / bounds.size.y; visual.transform.localScale *= factor;
            visual.transform.localPosition = new Vector3(0, -1.2f - bounds.min.y * factor, 0);
            if (!root.GetComponent<NiuLaiSkillIndicator>()) root.AddComponent<NiuLaiSkillIndicator>();
            if (!root.GetComponent<CommandFeedbackPresenter>()) root.AddComponent<CommandFeedbackPresenter>();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath); PrefabUtility.UnloadPrefabContents(root); AssetDatabase.SaveAssets();
        }
    }
}
