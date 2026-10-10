using System.Collections;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class NiuLaiCharacterPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var scene = SceneManager.GetSceneByName("PrototypeArena");
            if (scene.isLoaded)
            {
                var cleanup = SceneManager.CreateScene("NiuLaiCleanup");
                SceneManager.SetActiveScene(cleanup);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            foreach (var unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None)) Object.Destroy(unit.gameObject);
            foreach (var effect in Object.FindObjectsByType<NiuLaiMamaImpact>(FindObjectsSortMode.None)) Object.Destroy(effect.gameObject);
            foreach (var corpse in Object.FindObjectsByType<CorpseLifetimeController>(FindObjectsSortMode.None)) Object.Destroy(corpse.gameObject);
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectionPausesAndDefaultPreservesExusiai()
        {
            SceneManager.LoadScene("PrototypeArena"); yield return null;
            var selection = Object.FindFirstObjectByType<PlayerCharacterSelection>();
            Assert.That(selection.IsPending, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            var roster = Object.FindFirstObjectByType<OperatorRosterController>();
            Assert.That(roster.Slots.Count, Is.Zero);
            Assert.That(selection.ConfirmSelection(false), Is.True);
            Assert.That(selection.ConfirmSelection(true), Is.False);
            Assert.That(roster.Slots.Count, Is.EqualTo(6));
            var player = roster.Slots.Single(s => s.IsPlayerControlled).CurrentOperator;
            Assert.That(player.GetComponent<ExusiaiSkillController>(), Is.Not.Null);
            Assert.That(player.GetComponent<ExusiaiCombatPresentation>(), Is.Not.Null);
            Assert.That(player.GetComponent<NiuLaiSkillController>(), Is.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CowSelectionRetainsFiveOtherSeatsAndRedeploysCow()
        {
            SceneManager.LoadScene("PrototypeArena"); yield return null;
            Assert.That(Object.FindFirstObjectByType<PlayerCharacterSelection>().ConfirmSelection(true), Is.True);
            var roster = Object.FindFirstObjectByType<OperatorRosterController>();
            Assert.That(roster.Slots.Count, Is.EqualTo(6));
            var slot = roster.Slots.Single(s => s.IsPlayerControlled);
            Assert.That(slot.OperatorType, Is.EqualTo(OperatorType.NiuLai));
            Assert.That(slot.CurrentOperator.GetComponent<NiuLaiSkillController>(), Is.Not.Null);
            Assert.That(slot.CurrentOperator.transform.Find("NiuLaiVisual"), Is.Not.Null);
            Assert.That(roster.Slots.Count(s => !s.IsPlayerControlled), Is.EqualTo(5));
            slot.CurrentOperator.TakePhysicalDamage(9999);
            roster.Tick(8);
            Assert.That(slot.CurrentOperator, Is.Not.Null);
            Assert.That(slot.CurrentOperator.MaxHealth, Is.EqualTo(1300));
            Assert.That(slot.CurrentOperator.GetComponent<NiuLaiSkillController>().RCooldown, Is.EqualTo(15));
        }

        [UnityTest]
        public IEnumerator RigAndRangePreviewMatchConfirmedLanding()
        {
            var cow = CreateCow();
            var animator = cow.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController.animationClips.Length, Is.EqualTo(4));
            Assert.That(animator.applyRootMotion, Is.False);
            animator.Play("Run"); animator.Update(0.2f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Run"), Is.True);
            var indicator = cow.GetComponent<NiuLaiSkillIndicator>();
            cow.HandleSkill2(); indicator.RefreshPreview(new Vector3(20, 0, 0), true);
            Assert.That(indicator.IsVisible && indicator.IsValid, Is.True);
            var endpoint = indicator.DisplayedEndpoint;
            CapturePreview(cow.gameObject, "Logs/niulai-unity-range.png");
            Assert.That(Vector3.Distance(cow.transform.position, endpoint), Is.EqualTo(5.5f).Within(0.01f));
            cow.TryHandleConfirm(new Vector3(20, 0, 0), null); cow.Tick(NiuLaiSkillController.FlightDuration);
            Assert.That(Vector3.Distance(cow.transform.position, endpoint), Is.LessThan(0.01f));
            indicator.RefreshPreview(endpoint, true); Assert.That(indicator.IsVisible, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidFlightPreviewAndMamaTargetOnUnit()
        {
            var cow = CreateCow(); var enemy = CreateEnemy(new Vector3(2, 0.75f, 0));
            enemy.gameObject.AddComponent<CapsuleCollider>(); Physics.SyncTransforms();
            var indicator = cow.GetComponent<NiuLaiSkillIndicator>();
            cow.HandleSkill2(); indicator.RefreshPreview(enemy.transform.position, true);
            Assert.That(indicator.IsValid, Is.False);
            cow.TryHandleConfirm(enemy.transform.position, enemy.gameObject);
            Assert.That(cow.ECooldown, Is.Zero); Assert.That(cow.IsFlying, Is.False);
            cow.TryHandleCancel(); indicator.RefreshPreview(enemy.transform.position, true);
            Assert.That(indicator.IsVisible, Is.False);
            cow.Tick(15); cow.HandleSkill3(); indicator.RefreshPreview(enemy.transform.position, true);
            Assert.That(indicator.IsValid, Is.True);
            cow.TryHandleConfirm(enemy.transform.position, enemy.gameObject);
            Assert.That(cow.RCooldown, Is.EqualTo(36));
            Object.FindFirstObjectByType<NiuLaiMamaImpact>().Tick(NiuLaiMamaImpact.FallDuration);
            Assert.That(enemy.CurrentHealth, Is.LessThan(1000));
            cow.HandleSkill2(); cow.GetComponent<CombatUnit>().TakePhysicalDamage(9999);
            indicator.RefreshPreview(Vector3.zero, true); Assert.That(indicator.IsVisible, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FlightPlaysDedicatedAnimationAndReturnsAfterLanding()
        {
            var cow = CreateCow(); var animator = cow.GetComponentInChildren<Animator>();
            animator.Play("Idle"); animator.Update(0);
            var rootBone = animator.GetComponentsInChildren<Transform>().Single(t => t.name == "Root");
            var standingRotation = rootBone.localRotation;
            cow.HandleSkill2(); cow.TryHandleConfirm(new Vector3(3, 0, 0), null);
            yield return new WaitForSeconds(0.15f);
            Assert.That(cow.IsFlying, Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Flight"), Is.True);
            yield return new WaitForSeconds(0.5f);
            Assert.That(cow.IsFlying, Is.True);
            Assert.That(Quaternion.Angle(standingRotation, rootBone.localRotation), Is.GreaterThan(60), "Mid-flight body must tilt horizontally.");
            CapturePreview(cow.gameObject, "Logs/niulai-flight-runtime.png");
            yield return new WaitForSeconds(0.55f);
            Assert.That(cow.IsFlying, Is.True, "Landing recovery should still be playing.");
            Assert.That(Vector3.Distance(cow.transform.position, new Vector3(3, 0.75f, 0)), Is.LessThan(0.01f));
            yield return new WaitForSeconds(0.5f);
            Assert.That(cow.IsFlying, Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Flight"), Is.False);
        }

        [UnityTest]
        public IEnumerator EmpowerGlowTracksConsumptionExpiryAndDeathAndMamaUsesOwnPalette()
        {
            var cow=CreateCow(); var visual=cow.GetComponent<NiuLaiEmpowerVisual>();
            Assert.That(visual,Is.Not.Null); visual.RefreshVisual(); Assert.That(visual.IsVisible,Is.False);
            cow.ActivateEmpower(); visual.RefreshVisual(); Assert.That(visual.IsVisible,Is.True);
            yield return null;
            CapturePreview(cow.gameObject,"Logs/niulai-w-glow.png");
            var target=CreateEnemy(new Vector3(1,0.75f,0)); var attacks=cow.GetComponent<BasicAttackController>();
            attacks.SetTarget(target); attacks.Tick(0.91f); visual.RefreshVisual(); Assert.That(visual.IsVisible,Is.False);
            attacks.ClearTarget(); cow.Tick(6); cow.ActivateEmpower(); cow.Tick(5.1f); visual.RefreshVisual(); Assert.That(visual.IsVisible,Is.False);
            cow.Tick(15); cow.HandleSkill3(); cow.TryHandleConfirm(new Vector3(3,0,0),null);
            var mama=Object.FindFirstObjectByType<NiuLaiMamaImpact>();
            var renderer=mama.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(renderer.sharedMaterial,Is.SameAs(Resources.Load<Material>("NiuLaiMama")));
            Assert.That(renderer.sharedMaterial.shader.name,Is.EqualTo("ArknightsFrontline/NiuLaiMama"));
            Assert.That(cow.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));
            mama.Tick(1.2f); CapturePreview(cow.gameObject,"Logs/niulai-mama-color.png");
            cow.Tick(6); cow.ActivateEmpower(); cow.GetComponent<CombatUnit>().TakePhysicalDamage(9999); visual.RefreshVisual(); Assert.That(visual.IsVisible,Is.False);
            yield return null;
        }

        private static void CapturePreview(GameObject cow, string destination)
        {
            var cameraObject = new GameObject("CowPreviewCamera");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.transform.position = new Vector3(9, 11, 12); camera.transform.LookAt(new Vector3(1, 0, 0));
            camera.backgroundColor = new Color(0.12f, 0.17f, 0.23f); camera.clearFlags = CameraClearFlags.SolidColor;
            var lightObject = new GameObject("CowPreviewLight"); var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(40, -30, 0);
            var target = new RenderTexture(960, 720, 24); camera.targetTexture = target; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(960, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); image.Apply();
            Assert.That(image.GetPixels().Count(c => c.r > 0.8f && c.b > 0.8f && c.g < 0.25f), Is.LessThan(10), "Preview contains shader-error magenta pixels.");
            System.IO.File.WriteAllBytes(destination, image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            Object.Destroy(image); Object.Destroy(target); Object.Destroy(cameraObject); Object.Destroy(lightObject);
        }

        private static NiuLaiSkillController CreateCow()
        {
            var prefab = Resources.Load<GameObject>("NiuLaiPlayer");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab, new Vector3(0, 0.75f, 0), Quaternion.identity);
            instance.SetActive(true);
            return instance.GetComponent<NiuLaiSkillController>();
        }
        private static CombatUnit CreateEnemy(Vector3 position, float defense = 10)
        {
            var enemy = new GameObject("CowTestEnemy"); enemy.transform.position = position;
            var unit = enemy.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Red, Altitude.Ground, 1000, 0, defense, 1, 1, true, false);
            return unit;
        }

        [UnityTest]
        public IEnumerator EmpoweredMeleeDealsOneHitAndExpires()
        {
            var cow = CreateCow(); var target = CreateEnemy(new Vector3(1, 0.75f, 0));
            cow.ActivateEmpower();
            var attacks = cow.GetComponent<BasicAttackController>();
            attacks.SetTarget(target); attacks.Tick(0);
            Assert.That(target.CurrentHealth, Is.EqualTo(810));
            Assert.That(cow.IsEmpowered, Is.False);
            Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None), Is.Empty);
            attacks.SetTarget(null); cow.Tick(6); cow.ActivateEmpower(); cow.Tick(5.1f);
            Assert.That(cow.IsEmpowered, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FlightCrossesObstacleAndRejectsOccupiedDestination()
        {
            var cow = CreateCow();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = ExusiaiSkillController.ReservedObstacleLayerIndex;
            wall.transform.position = new Vector3(2, 0.75f, 0);
            wall.transform.localScale = new Vector3(0.5f, 2, 3);
            Physics.SyncTransforms();
            cow.HandleSkill2(); cow.TryHandleConfirm(wall.transform.position, null);
            Assert.That(cow.IsFlying, Is.False);
            Assert.That(cow.ECooldown, Is.Zero);
            cow.TryHandleConfirm(new Vector3(5, 0, 0), null);
            Assert.That(cow.IsFlying, Is.True);
            Assert.That(cow.GetComponent<OperatorRetreatController>().TryBegin(), Is.False);
            cow.Tick(NiuLaiSkillController.FlightDuration);
            Assert.That(cow.transform.position.x, Is.EqualTo(5).Within(0.01f));
            Assert.That(cow.IsFlying, Is.False);
            Object.Destroy(wall);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MamaWaitsForWarningAndDamagesEnemyOnlyOnce()
        {
            var cow = CreateCow(); var target = CreateEnemy(new Vector3(2, 0.75f, 0));
            cow.Tick(15); cow.HandleSkill3(); cow.TryHandleConfirm(new Vector3(2, 0, 0), null);
            var effect = Object.FindFirstObjectByType<NiuLaiMamaImpact>();
            Assert.That(effect, Is.Not.Null);
            effect.Tick(0.8f); Assert.That(target.CurrentHealth, Is.EqualTo(1000));
            // Already released impacts survive the caster's death.
            cow.GetComponent<CombatUnit>().TakePhysicalDamage(9999);
            effect.Tick(0.9f); Assert.That(target.CurrentHealth, Is.EqualTo(1000));
            effect.Tick(0.11f); effect.Tick(1);
            Assert.That(target.CurrentHealth, Is.EqualTo(650));
            yield return null;
        }
    }
}
