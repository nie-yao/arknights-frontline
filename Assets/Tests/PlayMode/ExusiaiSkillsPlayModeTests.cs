using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class ExusiaiSkillsPlayModeTests : InputTestFixture
    {
        private const int ReservedObstacleLayerIndex = ExusiaiSkillController.ReservedObstacleLayerIndex;
        private readonly List<GameObject> runtimeObjects = new List<GameObject>();
        private readonly List<CombatUnit> shotTargets = new List<CombatUnit>();
        private readonly List<PhysicalDamagePayload> shotPayloads = new List<PhysicalDamagePayload>();
        private static int cleanupSceneCount;

        private GameObject player;
        private CombatUnit owner;
        private UnitMotor motor;
        private PlayerCommandController commands;
        private CombatCommandResolver resolver;
        private BasicAttackController attacks;
        private AttackSequenceExecutor sequence;
        private UnitStatModifiers modifiers;
        private SkillDashController dash;
        private ExusiaiSkillController skills;
        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            PlayerPrefs.DeleteKey("af.input.bindings.v1");
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
        }

        [UnitySetUp]
        public IEnumerator SetUpArena()
        {
            runtimeObjects.Clear();
            shotTargets.Clear();
            shotPayloads.Clear();
            Time.timeScale = 0f;
            Scene priorArena = SceneManager.GetSceneByName("PrototypeArena");
            if (priorArena.isLoaded)
            {
                Scene staging = SceneManager.CreateScene("ExusiaiSkillsSetup_" + cleanupSceneCount++);
                SceneManager.SetActiveScene(staging);
                AsyncOperation unloadPriorArena = SceneManager.UnloadSceneAsync(priorArena);
                while (!unloadPriorArena.isDone)
                {
                    yield return null;
                }
            }
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownAfterRuntimeObjectsAreDestroyed()
        {
            Time.timeScale = 0f;
            if (sequence != null)
            {
                sequence.ShotRequested -= RecordShot;
            }

            foreach (GameObject runtimeObject in runtimeObjects)
            {
                if (runtimeObject != null)
                {
                    Object.Destroy(runtimeObject);
                }
            }
            runtimeObjects.Clear();
            yield return null;

            Scene arena = SceneManager.GetSceneByName("PrototypeArena");
            if (!arena.isLoaded)
            {
                Time.timeScale = 1f;
                PlayerPrefs.DeleteKey("af.input.bindings.v1");
                base.TearDown();
                yield break;
            }

            Scene cleanup = SceneManager.CreateScene("ExusiaiSkillsCleanup_" + cleanupSceneCount++);
            SceneManager.SetActiveScene(cleanup);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(arena);
            while (!unload.isDone)
            {
                yield return null;
            }

            player = null;
            owner = null;
            motor = null;
            commands = null;
            resolver = null;
            attacks = null;
            sequence = null;
            modifiers = null;
            dash = null;
            skills = null;
            Time.timeScale = 1f;
            PlayerPrefs.DeleteKey("af.input.bindings.v1");
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator LoadedSceneRestoresSkillInputHudIndicatorAndFeedbackWiring()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            yield return ReloadArenaAfterInputFixtureSetup();
            BindStageFivePlayer();
            SkillHudPresenter hud = Object.FindFirstObjectByType<SkillHudPresenter>();
            ExusiaiSkillIndicator indicator = player.GetComponent<ExusiaiSkillIndicator>();
            CommandFeedbackPresenter feedback = player.GetComponent<CommandFeedbackPresenter>();
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            Assert.That(hud, Is.Not.Null);
            Assert.That(indicator, Is.Not.Null);
            Assert.That(feedback, Is.Not.Null);
            Assert.That(mainCamera, Is.Not.Null);
            InputAction attackMove = commands.InputActions.FindAction("AttackMove");
            Assert.That(attackMove, Is.Not.Null);
            Assert.That(commands.isActiveAndEnabled, Is.True);
            Assert.That(attackMove.enabled, Is.True, "PlayerCommandController must enable its Gameplay action map after scene load.");
            bool keyboardBindingResolved = false;
            string resolvedControls = string.Empty;
            foreach (InputControl control in attackMove.controls)
            {
                resolvedControls += control.path + ";";
                if (control == keyboard.aKey) keyboardBindingResolved = true;
            }
            string availableDevices = string.Empty;
            foreach (InputDevice device in InputSystem.devices)
            {
                availableDevices += device.layout + "#" + device.deviceId + " added=" + device.added + ";";
            }
            Assert.That(keyboardBindingResolved, Is.True,
                "The saved controller's AttackMove binding should resolve to this test keyboard. "
                + "bindings=" + string.Join(",", attackMove.bindings.Select(binding => binding.path))
                + ", resolved=" + resolvedControls + ", devices=" + availableDevices
                + ", keyboard=" + keyboard.deviceId + " added=" + keyboard.added);

            Press(keyboard.aKey);
            yield return null;
            Assert.That(attackMove.IsPressed(), Is.True, "The virtual A key should reach the enabled AttackMove action.");
            Assert.That(commands.IsAttackMoveHeld, Is.True, "The AttackMove callback should set held state.");
            Assert.That(feedback.IsAttackRangeVisible, Is.True);

            Press(keyboard.eKey);
            yield return null;

            Assert.That(skills.IsSelectingChargeTarget, Is.True,
                "The saved PlayerCommandController must restore its Exusiai skill input handler.");
            Assert.That(feedback.IsAttackRangeVisible, Is.False,
                "The command feedback range ring must hide while the same skills block ordinary input.");
            Assert.That(hud.ELabel, Is.EqualTo("E  SELECT DEST"));
            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.ChargeTargeting));
            Assert.That(hud.transform.childCount, Is.EqualTo(4),
                "The loaded HUD should contain W/E/R slots plus one deployment feedback row.");
            Assert.That(hud.transform.Find("W"), Is.Not.Null);
            Assert.That(hud.transform.Find("E"), Is.Not.Null);
            Assert.That(hud.transform.Find("R"), Is.Not.Null);
            Assert.That(hud.transform.Find("DeploymentStatus"), Is.Not.Null);
            Assert.That(indicator.GetComponentsInChildren<LineRenderer>(true).Length, Is.EqualTo(2),
                "The direct-dash preview contains only the landing range and path arrow.");

            Release(keyboard.eKey);
            Vector3 landingPointer = mainCamera.WorldToScreenPoint(
                new Vector3(owner.transform.position.x + 5f, 0f, owner.transform.position.z));
            Assert.That(landingPointer.z, Is.GreaterThan(0f));
            Set(mouse.position, new Vector2(landingPointer.x, landingPointer.y));
            yield return null;
            Assert.That(commands.TryGetCachedPointerHit(out RaycastHit landingHit), Is.True,
                "The loaded scene must have a real ground hit for a single left-click E confirmation.");
            Assert.That(landingHit.collider, Is.Not.Null);

            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
            Vector3 confirmedEndpoint = skills.GetComponent<SkillDashController>().Destination;
            Assert.That(skills.IsSelectingChargeTarget, Is.False);
            Assert.That(skills.Snapshot.ChargeCooldown, Is.InRange(19.8f, 20f),
                "One left click should start E cooldown immediately.");
            Assert.That(skills.GetComponent<SkillDashController>().IsDashing, Is.True,
                "One left click in the loaded scene must begin the dash immediately.");
            StringAssert.StartsWith("E  ", hud.ELabel);
            StringAssert.Contains(".", hud.ELabel,
                "The E slot should show its cooldown after confirmation.");
            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.DashPath));
            Assert.That(indicator.DisplayedEndpoint, Is.EqualTo(confirmedEndpoint));

            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);
            yield return null;
            Assert.That(skills.GetComponent<SkillDashController>().IsDashing, Is.True,
                "Right click during the initiated dash must not trigger a second E step.");
            Assert.That(skills.GetComponent<SkillDashController>().Destination, Is.EqualTo(confirmedEndpoint));
            Release(keyboard.aKey);
        }

        private IEnumerator ReloadArenaAfterInputFixtureSetup()
        {
            Scene priorArena = SceneManager.GetSceneByName("PrototypeArena");
            if (priorArena.isLoaded)
            {
                Scene staging = SceneManager.CreateScene("ExusiaiSkillsInputSetup_" + cleanupSceneCount++);
                SceneManager.SetActiveScene(staging);
                AsyncOperation unloadPriorArena = SceneManager.UnloadSceneAsync(priorArena);
                while (!unloadPriorArena.isDone)
                {
                    yield return null;
                }
            }

            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreeCompletedBasicSequencesChargeWThenSpawnThreeVisibleShotsAtPointZeroFiveSeconds()
        {
            BindStageFivePlayer();
            CombatUnit target = CreateEnemy("SweepTarget", owner.transform.position + Vector3.right * 3f);
            RecordShots();
            attacks.SetTarget(target);
            attacks.Tick(0f);
            attacks.Tick(0.5f);
            attacks.Tick(0.5f);
            Assert.That(skills.Snapshot.SweepProgress, Is.EqualTo(3));
            Assert.That(skills.Snapshot.IsSweepReady, Is.True);

            int visibleBeforeVolley = CountVisibleProjectiles();
            shotPayloads.Clear();
            attacks.Tick(0.5f);
            TrackProjectiles();
            Assert.That(sequence.IsRunning, Is.True);
            Assert.That(shotPayloads.Count, Is.EqualTo(1));
            Assert.That(CountVisibleProjectiles() - visibleBeforeVolley, Is.EqualTo(1));

            sequence.Tick(0.049f);
            TrackProjectiles();
            Assert.That(shotPayloads.Count, Is.EqualTo(1));
            sequence.Tick(0.001f);
            TrackProjectiles();
            Assert.That(shotPayloads.Count, Is.EqualTo(2));
            sequence.Tick(0.05f);
            TrackProjectiles();

            Assert.That(shotPayloads.Count, Is.EqualTo(3));
            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(CountVisibleProjectiles() - visibleBeforeVolley, Is.EqualTo(3));
            Assert.That(skills.Snapshot.SweepProgress, Is.EqualTo(1),
                "A completed W volley starts the next basic-attack charge cycle.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverloadMakesFiveShotBasicsAtFiftyFiveAndReturnsToBaseAfterTenSeconds()
        {
            BindStageFivePlayer();
            CombatUnit target = CreateEnemy("OverloadTarget", owner.transform.position + Vector3.right * 3f);
            RecordShots();
            skills.Tick(10f);
            Assert.That(skills.TryActivateOverload(), Is.True);
            Assert.That(owner.AttackPower, Is.EqualTo(55f).Within(0.001f));
            Assert.That(owner.AttackInterval, Is.EqualTo(0.28f).Within(0.001f));

            attacks.SetTarget(target);
            shotPayloads.Clear();
            attacks.Tick(0f);
            TrackProjectiles();
            Assert.That(sequence.IsRunning, Is.True);
            sequence.Tick(0.2f);
            TrackProjectiles();

            Assert.That(shotPayloads.Count, Is.EqualTo(5));
            Assert.That(shotPayloads.TrueForAll(payload => Mathf.Approximately(payload.AttackPower, 55f)), Is.True);
            Assert.That(skills.IsOverloadActive, Is.True);
            skills.Tick(10f);
            Assert.That(skills.IsOverloadActive, Is.False);
            Assert.That(owner.AttackPower, Is.EqualTo(50f).Within(0.001f));
            Assert.That(owner.AttackInterval, Is.EqualTo(0.5f).Within(0.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverloadSweepUsesFiveShotsWithFiftyFivePowerAndOnlyLastShotHasMissingHealthPayload()
        {
            BindStageFivePlayer();
            CombatUnit target = CreateEnemy("OverloadSweepTarget", owner.transform.position + Vector3.right * 3f);
            RecordShots();
            attacks.SetTarget(target);
            attacks.Tick(0f);
            attacks.Tick(0.5f);
            attacks.Tick(0.5f);
            Assert.That(skills.Snapshot.IsSweepReady, Is.True);

            skills.Tick(10f);
            Assert.That(skills.TryActivateOverload(), Is.True);
            shotPayloads.Clear();
            attacks.Tick(0.28f);
            TrackProjectiles();
            sequence.Tick(0.2f);
            TrackProjectiles();

            Assert.That(shotPayloads.Count, Is.EqualTo(5));
            for (int i = 0; i < shotPayloads.Count; i++)
            {
                Assert.That(shotPayloads[i].AttackPower, Is.EqualTo(55f).Within(0.001f));
                Assert.That(shotPayloads[i].DamageMultiplier, Is.EqualTo(1.45f).Within(0.001f));
                Assert.That(shotPayloads[i].MissingHealthRatio, Is.EqualTo(i == 4 ? 0.08f : 0f).Within(0.001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverloadChargeUsesFiveShotsAndDoesNotChangeSweepProgress()
        {
            BindStageFivePlayer();
            CombatUnit target = CreateEnemy("OverloadChargeTarget", owner.transform.position + Vector3.right * 3f);
            attacks.SetTarget(target);
            attacks.Tick(0f);
            Assert.That(skills.Snapshot.SweepProgress, Is.EqualTo(1));

            skills.Tick(10f);
            Assert.That(skills.TryActivateOverload(), Is.True);
            RecordShots();
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(target.transform.position, target), Is.True);
            Assert.That(sequence.IsRunning, Is.False,
                "E must wait for the dash to arrive before starting its volley.");
            Assert.That(shotPayloads, Is.Empty,
                "E must not request or deal a shot during the dash.");
            dash.Tick(1f);
            TrackProjectiles();
            Assert.That(sequence.IsRunning, Is.True);
            sequence.Tick(0.2f);
            TrackProjectiles();

            Assert.That(shotPayloads, Has.Count.EqualTo(5));
            foreach (PhysicalDamagePayload payload in shotPayloads)
            {
                Assert.That(payload.AttackPower, Is.EqualTo(55f).Within(0.001f));
                Assert.That(payload.DamageMultiplier, Is.EqualTo(1.25f).Within(0.001f));
            }
            Assert.That(skills.Snapshot.SweepProgress, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChargeSelectsNearestEnemyAtLandingAndIgnoresClickedEnemy()
        {
            BindStageFivePlayer();
            Vector3 origin = owner.transform.position;
            CombatUnit east = CreateEnemy("EastTarget", origin + Vector3.right * 4f);
            CombatUnit north = CreateEnemy("NorthTarget", origin + Vector3.forward * 3f);

            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(origin + Vector3.forward * 2.5f, east), Is.True);
            Assert.That(dash.IsDashing, Is.True);
            Assert.That(skills.SelectedChargeTarget, Is.Null,
                "A clicked enemy is not selected while E is still dashing.");
            Assert.That(sequence.IsRunning, Is.False);
            dash.Tick(1f);
            TrackProjectiles();
            Assert.That(skills.SelectedChargeTarget, Is.SameAs(north));
            Assert.That(sequence.IsRunning, Is.True);

            skills.ResetForDeployment();
            owner.transform.position = origin;
            motor.Stop();
            east.transform.position = origin + Vector3.right * 20f;
            north.transform.position = origin + Vector3.forward * 20f;
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(origin + Vector3.right, null), Is.True);
            dash.Tick(1f);
            Assert.That(skills.SelectedChargeTarget, Is.Null);
            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(skills.Snapshot.ChargeCooldown, Is.EqualTo(20f).Within(0.001f),
                "E still enters cooldown when no legal enemy is in range at arrival.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChargeDashUsesOneClickWaitsUntilArrivalAndClampsBeyondSevenMeters()
        {
            BindStageFivePlayer();
            Vector3 start = owner.transform.position;
            CombatUnit target = CreateEnemy("DashTarget", start + Vector3.right * 3f);
            RecordShots();
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(start + Vector3.right * 4f, null), Is.True);
            Assert.That(dash.IsDashing, Is.True);
            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(shotTargets, Is.Empty,
                "No E projectile may be requested before the dash reaches its landing point.");

            dash.Tick(0.1f);
            Assert.That(Vector2.Distance(
                new Vector2(owner.transform.position.x, owner.transform.position.z),
                new Vector2(start.x + 2.8f, start.z)), Is.EqualTo(0f).Within(0.001f),
                "A 28 meter-per-second dash covers 2.8 meters in 0.1 seconds.");
            Assert.That(shotTargets, Is.Empty);
            dash.Tick(0.1f);
            TrackProjectiles();
            Assert.That(owner.transform.position.x, Is.EqualTo(start.x + 4f).Within(0.001f));
            Assert.That(sequence.IsRunning, Is.True,
                "The E volley should start when the dash reaches its destination.");
            sequence.Tick(0.15f);
            TrackProjectiles();
            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(shotTargets.Count, Is.EqualTo(4));

            skills.ResetForDeployment();
            owner.transform.position = start;
            motor.Stop();
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(start + Vector3.right * 12f, null), Is.True);
            Assert.That(Vector2.Distance(
                new Vector2(start.x, start.z),
                new Vector2(dash.Destination.x, dash.Destination.z)), Is.EqualTo(7f).Within(0.001f));
            dash.Tick(1f);
            Assert.That(Vector2.Distance(
                new Vector2(start.x, start.z),
                new Vector2(owner.transform.position.x, owner.transform.position.z)), Is.EqualTo(7f).Within(0.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChargeSingleConfirmationStartsDashWithoutASecondRightClick()
        {
            BindStageFivePlayer();
            Vector3 start = owner.transform.position;
            CombatUnit target = CreateEnemy("NoDashTarget", start + Vector3.right * 3f);
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(target.transform.position, target), Is.True);
            Assert.That(skills.Snapshot.ChargeCooldown, Is.EqualTo(20f).Within(0.001f));
            Assert.That(dash.IsDashing, Is.True,
                "One valid landing confirmation should start the dash immediately.");
            Assert.That(owner.transform.position, Is.EqualTo(start));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DashShortensItsPathAtColliderOnReservedObstacleLayerTen()
        {
            BindStageFivePlayer();
            Vector3 start = owner.transform.position;
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "LayerTenObstacle";
            obstacle.layer = ReservedObstacleLayerIndex;
            obstacle.transform.position = start + Vector3.right * 2f;
            obstacle.transform.localScale = new Vector3(0.5f, 3f, 1f);
            runtimeObjects.Add(obstacle);
            Physics.SyncTransforms();

            CreateEnemy("ObstacleDashTarget", start + Vector3.right * 3f);
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(start + Vector3.right * 5f, null), Is.True);
            TrackProjectiles();

            Assert.That(dash.Destination.x, Is.EqualTo(start.x + 1.5f).Within(0.01f));
            Assert.That(dash.IsDashing, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChargeRechecksNearestTargetAtLandingAfterClickedEnemyDies()
        {
            BindStageFivePlayer();
            Vector3 origin = owner.transform.position;
            CombatUnit selected = CreateEnemy("SelectedChargeTarget", origin + Vector3.right * 3f);
            CombatUnit replacement = CreateEnemy("ReplacementChargeTarget", origin + Vector3.right * 4f);
            RecordShots();

            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(selected.transform.position, selected), Is.True);
            Assert.That(skills.SelectedChargeTarget, Is.Null,
                "The clicked enemy is not selected before the landing point is reached.");
            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(shotTargets, Is.Empty);
            selected.TakePhysicalDamage(selected.MaxHealth);
            dash.Tick(1f);
            TrackProjectiles();
            Assert.That(skills.SelectedChargeTarget, Is.SameAs(replacement));
            sequence.Tick(0.15f);
            TrackProjectiles();
            Assert.That(shotTargets.Count, Is.EqualTo(4));
            Assert.That(shotTargets.TrueForAll(target => target == replacement), Is.True,
                "All E shots should go to the nearest legal enemy at arrival; the dead click target receives none.");
            Assert.That(sequence.IsRunning, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LivingTargetsLeavingRangeInterruptWAndROrdinaryCommands()
        {
            BindStageFivePlayer();
            AssertManualVolleyCancelsCommandAfterRangeExit("SweepRangeTarget", completeSweep: true, overload: false);

            skills.ResetForDeployment();
            owner.transform.position = ArenaLayout.CreateDefault().BlueDeployment + Vector3.up * 2f;
            AssertManualVolleyCancelsCommandAfterRangeExit("OverloadRangeTarget", completeSweep: false, overload: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LivingNormalTargetLeavingRangeClearsManualAttackCommand()
        {
            BindStageFivePlayer();
            CombatUnit target = CreateEnemy("NormalRangeTarget", owner.transform.position + Vector3.right * 3f);
            commands.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);
            attacks.Tick(0f);

            Assert.That(sequence.IsRunning, Is.False, "A normal basic attack is a single-shot sequence.");
            Assert.That(attacks.CurrentTarget, Is.SameAs(target));
            Assert.That(commands.CurrentCommand.HasValue, Is.True);

            target.transform.position = owner.transform.position + Vector3.right * 20f;
            attacks.Tick(0f);
            resolver.Tick(0f);

            Assert.That(attacks.CurrentTarget, Is.Null);
            Assert.That(commands.CurrentCommand.HasValue, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettlementFreezesLastSkillSnapshotAndHudText()
        {
            BindStageFivePlayer();
            SkillHudPresenter hud = Object.FindFirstObjectByType<SkillHudPresenter>();
            Assert.That(hud, Is.Not.Null);
            CombatUnit target = CreateEnemy("SettlementTarget", owner.transform.position + Vector3.right * 3f);
            attacks.SetTarget(target);
            attacks.Tick(0f);
            TrackProjectiles();
            Assert.That(skills.Snapshot.SweepProgress, Is.EqualTo(1));
            skills.Tick(10f);
            Assert.That(skills.TryActivateOverload(), Is.True);
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            hud.Refresh();
            Assert.That(hud.WLabel, Is.EqualTo("W  1/3"));
            Assert.That(hud.RLabel, Is.EqualTo("R  ACTIVE 10.0"));
            Assert.That(hud.ELabel, Is.EqualTo("E  SELECT DEST"));

            CombatUnit redTower = Object.FindFirstObjectByType<ArenaBootstrap>()
                .RedTower.GetComponent<CombatUnit>();
            redTower.TakePhysicalDamage(redTower.MaxHealth);
            yield return null;

            Assert.That(Object.FindFirstObjectByType<MatchOutcomeController>().IsMatchOver, Is.True);
            Assert.That(skills.IsOverloadActive, Is.False, "Match cleanup must remove the live R modifier.");
            Assert.That(owner.AttackPower, Is.EqualTo(50f).Within(0.001f));
            Assert.That(skills.Snapshot.IsOverloadActive, Is.True,
                "The public HUD snapshot should preserve the active state reached at settlement.");
            Assert.That(skills.Snapshot.OverloadDuration, Is.EqualTo(10f).Within(0.001f));
            Assert.That(skills.Snapshot.OverloadCooldown, Is.EqualTo(30f).Within(0.001f));
            Assert.That(hud.RLabel, Is.EqualTo("R  ACTIVE 10.0"));
            Assert.That(hud.WLabel, Is.EqualTo("W  1/3"));
            Assert.That(hud.ELabel, Is.EqualTo("E  READY"),
                "Settlement must preserve W/R and cooldown values without retaining the temporary E targeting prompt.");

            skills.Tick(30f);
            hud.Refresh();
            yield return null;
            Assert.That(skills.Snapshot.IsOverloadActive, Is.True);
            Assert.That(skills.Snapshot.OverloadDuration, Is.EqualTo(10f).Within(0.001f));
            Assert.That(skills.Snapshot.OverloadCooldown, Is.EqualTo(30f).Within(0.001f));
            Assert.That(hud.RLabel, Is.EqualTo("R  ACTIVE 10.0"));
            Assert.That(hud.WLabel, Is.EqualTo("W  1/3"));
            Assert.That(hud.ELabel, Is.EqualTo("E  READY"));
        }

        [UnityTest]
        public IEnumerator SettlementDuringDashPreservesCooldownAndHidesPathPreview()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            yield return ReloadArenaAfterInputFixtureSetup();
            BindStageFivePlayer();
            SkillHudPresenter hud = Object.FindFirstObjectByType<SkillHudPresenter>();
            ExusiaiSkillIndicator indicator = player.GetComponent<ExusiaiSkillIndicator>();
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            Assert.That(mainCamera, Is.Not.Null);

            Vector3 pointerWorld = new Vector3(owner.transform.position.x + 5f, 0f, owner.transform.position.z);
            Vector3 pointerScreen = mainCamera.WorldToScreenPoint(pointerWorld);
            Assert.That(pointerScreen.z, Is.GreaterThan(0f), "The lane pointer target must be in front of the saved scene camera.");
            Set(mouse.position, new Vector2(pointerScreen.x, pointerScreen.y));
            yield return null;
            Assert.That(commands.TryGetCachedPointerHit(out RaycastHit pointerHit), Is.True,
                "The actual mouse position must produce a cached physics hit before the dash window is opened.");
            Assert.That(pointerHit.collider, Is.Not.Null);

            CombatUnit target = CreateEnemy("SettlementDashWindowTarget", owner.transform.position + Vector3.right * 3f);
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            hud.Refresh();
            indicator.Refresh();
            Assert.That(hud.ELabel, Is.EqualTo("E  SELECT DEST"));
            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.ChargeTargeting));
            Assert.That(indicator.RangeRadius, Is.EqualTo(7f).Within(0.001f));
            Assert.That(indicator.IsVisible, Is.True,
                "The E targeting state must show the seven-meter landing preview before confirmation.");

            Assert.That(skills.TryConfirmCharge(target.transform.position, target), Is.True);
            hud.Refresh();
            indicator.Refresh();

            Assert.That(skills.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.Cooldown));
            float cooldownAtConfirmation = skills.Snapshot.ChargeCooldown;
            string cooldownLabelAtConfirmation = hud.ELabel;
            Assert.That(cooldownAtConfirmation, Is.InRange(19.8f, 20f),
                "Confirming a valid landing should start the cooldown immediately.");
            StringAssert.StartsWith("E  ", cooldownLabelAtConfirmation);
            StringAssert.Contains(".", cooldownLabelAtConfirmation,
                "The E HUD should display cooldown without a second-step prompt.");
            Assert.That(dash.IsDashing, Is.True);
            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.DashPath));
            Assert.That(indicator.DisplayedEndpoint, Is.EqualTo(dash.Destination));
            Assert.That(indicator.IsVisible, Is.True,
                "The path preview should follow the confirmed landing while the dash runs.");

            CombatUnit redTower = Object.FindFirstObjectByType<ArenaBootstrap>()
                .RedTower.GetComponent<CombatUnit>();
            redTower.TakePhysicalDamage(redTower.MaxHealth);
            yield return null;
            hud.Refresh();

            Assert.That(Object.FindFirstObjectByType<MatchOutcomeController>().IsMatchOver, Is.True);
            Assert.That(skills.Snapshot.ChargePhase, Is.EqualTo(ExusiaiChargePhase.Cooldown));
            Assert.That(skills.Snapshot.ChargeCooldown,
                Is.EqualTo(cooldownAtConfirmation).Within(0.001f),
                "Settlement must freeze the E cooldown at the value reached when the match ended.");
            Assert.That(hud.ELabel, Is.EqualTo(cooldownLabelAtConfirmation),
                "Settlement must preserve the visible E cooldown label.");
            Assert.That(indicator.Mode, Is.EqualTo(ExusiaiSkillIndicatorMode.None));
            Assert.That(indicator.IsVisible, Is.False,
                "Settlement must hide the live dash pointer preview immediately rather than starting its normal fade.");
        }

        private void BindStageFivePlayer()
        {
            player = GameObject.Find("Player_Exusiai");
            Assert.That(player, Is.Not.Null, "PrototypeArena must contain Player_Exusiai.");
            Assert.That(player.GetComponent<UnitStatModifiers>(), Is.Not.Null, "Player_Exusiai is missing UnitStatModifiers scene wiring.");
            Assert.That(player.GetComponent<AttackSequenceExecutor>(), Is.Not.Null, "Player_Exusiai is missing AttackSequenceExecutor scene wiring.");
            Assert.That(player.GetComponent<SkillDashController>(), Is.Not.Null, "Player_Exusiai is missing SkillDashController scene wiring.");
            Assert.That(player.GetComponent<ExusiaiSkillController>(), Is.Not.Null, "Player_Exusiai is missing ExusiaiSkillController scene wiring.");

            owner = player.GetComponent<CombatUnit>();
            motor = player.GetComponent<UnitMotor>();
            commands = player.GetComponent<PlayerCommandController>();
            resolver = player.GetComponent<CombatCommandResolver>();
            attacks = player.GetComponent<BasicAttackController>();
            sequence = player.GetComponent<AttackSequenceExecutor>();
            modifiers = player.GetComponent<UnitStatModifiers>();
            dash = player.GetComponent<SkillDashController>();
            skills = player.GetComponent<ExusiaiSkillController>();
            Assert.That(owner, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(commands, Is.Not.Null);
            Assert.That(resolver, Is.Not.Null);
            Assert.That(attacks, Is.Not.Null);
            Assert.That(sequence, Is.Not.Null);
            Assert.That(modifiers, Is.Not.Null);
            Assert.That(dash, Is.Not.Null);
            Assert.That(skills, Is.Not.Null);
        }

        private void AssertManualVolleyCancelsCommandAfterRangeExit(string targetName, bool completeSweep, bool overload)
        {
            CombatUnit target = CreateEnemy(targetName, owner.transform.position + Vector3.right * 3f);
            commands.Issue(UnitCommand.Attack(target.gameObject));
            resolver.Tick(0f);
            attacks.Tick(0f);

            if (completeSweep)
            {
                attacks.Tick(0.5f);
                attacks.Tick(0.5f);
                Assert.That(skills.Snapshot.IsSweepReady, Is.True);
            }

            if (overload)
            {
                skills.Tick(10f);
                Assert.That(skills.TryActivateOverload(), Is.True);
            }

            attacks.Tick(owner.AttackInterval);
            TrackProjectiles();
            Assert.That(sequence.IsRunning, Is.True);
            target.transform.position = owner.transform.position + Vector3.right * 20f;
            sequence.Tick(0.05f);
            resolver.Tick(0f);

            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(attacks.CurrentTarget, Is.Null);
            Assert.That(commands.CurrentCommand.HasValue, Is.False);
        }

        private CombatUnit CreateEnemy(string name, Vector3 position)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.position = position;
            runtimeObjects.Add(gameObject);
            CombatUnit unit = gameObject.AddComponent<CombatUnit>();
            unit.Configure(TeamId.Red, Altitude.Ground, 2000f, 0f, 0f, 0f, 0.5f, false, false);
            return unit;
        }

        private int CountVisibleProjectiles()
        {
            int count = 0;
            foreach (Projectile projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                Renderer projectileRenderer = projectile.GetComponent<Renderer>();
                if (projectile.gameObject.activeInHierarchy && projectileRenderer != null && projectileRenderer.enabled)
                {
                    count++;
                }
            }
            return count;
        }

        private void RecordShots()
        {
            shotTargets.Clear();
            shotPayloads.Clear();
            sequence.ShotRequested -= RecordShot;
            sequence.ShotRequested += RecordShot;
        }

        private void RecordShot(CombatUnit target, PhysicalDamagePayload payload)
        {
            shotTargets.Add(target);
            shotPayloads.Add(payload);
            TrackProjectiles();
        }

        private void TrackProjectiles()
        {
            foreach (Projectile projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (!runtimeObjects.Contains(projectile.gameObject))
                {
                    runtimeObjects.Add(projectile.gameObject);
                }
            }
        }
    }
}
