using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArknightsFrontline.Editor
{
    public static class NiuLaiSetup
    {
        private const string Directory = "Assets/Game/Characters/NiuLai/Resources/";
        [MenuItem("Arknights Frontline/Prepare NiuLai and Character Selection")]
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/PrototypeArena.unity");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Directory + "texture_pbr_20250901.png");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Directory + "NiuLai.mat");
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, Directory + "NiuLai.mat");
            }
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.25f);
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(Directory + "texture_pbr_20250901_normal.png");
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Directory + "texture_pbr_20250901_normal.png"));
            material.EnableKeyword("_NORMALMAP");
            var root = new GameObject("NiuLaiPlayer");
            root.SetActive(false);
            root.layer = 8; // Match player Targetable layer in saved arena below.
            var unit = root.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Blue, Altitude.Ground, 1300, 80, 25, 2, 0.9f, true, false);
            root.AddComponent<UnitMotor>().Configure(5, ArenaLayout.CreateDefault());
            root.AddComponent<CapsuleCollider>().height = 2.4f;
            root.AddComponent<UnitStatModifiers>();
            var commands = root.AddComponent<PlayerCommandController>();
            var attacks = root.AddComponent<BasicAttackController>();
            attacks.Configure(unit);
            root.AddComponent<CombatCommandResolver>().Configure(unit, root.GetComponent<UnitMotor>(), commands, attacks);
            root.AddComponent<OperatorIdentity>().Configure("Player_Exusiai", TeamId.Blue, OperatorType.NiuLai);
            root.AddComponent<OperatorRetreatController>();
            root.AddComponent<HealthBarPresenter>();
            root.AddComponent<DeathCorpsePresenter>().Configure(unit, material, 6, UnitKind.Operator);
            root.AddComponent<NiuLaiSkillController>();
            root.AddComponent<NiuLaiHud>();
            root.AddComponent<NiuLaiPresentation>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Directory + "NiuLai.fbx");
            if (!model || !texture) throw new System.InvalidOperationException("NiuLai model or texture missing.");
            var visual = Object.Instantiate(model, root.transform);
            visual.name = "NiuLaiVisual";
            var renderer = visual.GetComponentInChildren<Renderer>();
            foreach (var mesh in visual.GetComponentsInChildren<Renderer>()) mesh.sharedMaterial = material;
            Bounds bounds = renderer.bounds;
            float scale = 2.4f / bounds.size.y;
            visual.transform.localScale *= scale;
            visual.transform.localPosition = new Vector3(0, -ArenaVisualMetrics.OperatorCenterHeight - bounds.min.y * scale, 0);
            var bootstrap = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ArenaRosterBootstrap>(true)).Single();
            var serialized = new SerializedObject(bootstrap);
            var slots = serialized.FindProperty("slots");
            GameObject player = null;
            for (int i = 0; i < slots.arraySize; i++)
                if (slots.GetArrayElementAtIndex(i).FindPropertyRelative("isPlayerControlled").boolValue)
                    player = (GameObject)slots.GetArrayElementAtIndex(i).FindPropertyRelative("template").objectReferenceValue;
            root.layer = player.layer;
            PrefabUtility.SaveAsPrefabAsset(root, Directory + "NiuLaiPlayer.prefab");
            Object.DestroyImmediate(root);
            if (!bootstrap.GetComponent<PlayerCharacterSelection>()) bootstrap.gameObject.AddComponent<PlayerCharacterSelection>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.DeleteAsset(Directory + "NiuLai.fbm");
            AssetDatabase.DeleteAsset(Directory + "texture_pbr_20250901_metallic-texture_pbr_20250901_roughness.png");
            if (System.IO.File.Exists("Assets/Game/Characters/NiuLai/Rigged/NiuLai_Rigged.fbx")) NiuLaiRigSetup.Prepare();
            NiuLaiAppearanceSetup.Prepare();
            Debug.Log("NiuLai prefab and pre-match selection ready.");
        }

        public static void RenderPreview()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Object.Instantiate(Resources.Load<GameObject>("NiuLaiPlayer"));
            root.transform.position = Vector3.up * ArenaVisualMetrics.OperatorCenterHeight;
            foreach (var behaviour in root.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(behaviour);
            root.SetActive(true);
            var light = new GameObject("PreviewLight").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(35, -30, 0);
            var camera = new GameObject("PreviewCamera").AddComponent<UnityEngine.Camera>();
            camera.transform.position = new Vector3(4, 2.8f, 5);
            camera.transform.LookAt(new Vector3(0, 1.2f, 0));
            camera.backgroundColor = new Color(0.12f, 0.17f, 0.23f); camera.clearFlags = CameraClearFlags.SolidColor;
            var target = new RenderTexture(800, 800, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(800, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 800, 800), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes("Logs/niulai-preview.png", image.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null;
            Object.DestroyImmediate(image); Object.DestroyImmediate(target);
        }
    }
}
