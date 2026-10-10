using System.Collections;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class ArenaSceneSmokeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene prototypeArena = SceneManager.GetSceneByName("PrototypeArena");
            if (!prototypeArena.isLoaded)
            {
                yield break;
            }

            Scene cleanupScene = SceneManager.CreateScene("ArenaSceneSmokeCleanup");
            SceneManager.SetActiveScene(cleanupScene);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(prototypeArena);
            while (!unload.isDone)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SavedArenaBootstrapsSixOperatorsFromInactiveTemplates()
        {
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;

            OperatorRosterController roster = Object.FindFirstObjectByType<OperatorRosterController>();
            Assert.That(roster, Is.Not.Null,
                "The saved arena must rebuild its non-serialized roster when the scene loads.");
            Assert.That(roster.Slots.Count, Is.EqualTo(6));

            int activeOperatorCount = 0;
            int playerSkillControllerCount = 0;
            int computerControllerCount = 0;
            foreach (CombatUnit unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            {
                if (unit.GetComponent<OperatorIdentity>() == null)
                {
                    continue;
                }

                activeOperatorCount++;
                if (unit.GetComponent<ExusiaiSkillController>() != null)
                {
                    playerSkillControllerCount++;
                    Assert.That(unit.GetComponent<OperatorIdentity>().StableKey, Is.EqualTo("Player_Exusiai"));
                }

                if (unit.GetComponent<SimpleOperatorAiController>() != null)
                {
                    computerControllerCount++;
                }
            }

            Assert.That(activeOperatorCount, Is.EqualTo(6));
            Assert.That(playerSkillControllerCount, Is.EqualTo(1));
            Assert.That(computerControllerCount, Is.EqualTo(5));
            Assert.That(GameObject.Find("TrainingDummy_Red"), Is.Null);

            int playerControlledSlots = 0;
            foreach (OperatorRosterSlot slot in roster.Slots)
            {
                Assert.That(slot.Template, Is.Not.Null);
                Assert.That(slot.Template.activeSelf, Is.False);
                CombatUnit templateUnit = slot.Template.GetComponent<CombatUnit>();
                Assert.That(templateUnit, Is.Not.Null);
                Assert.That(templateUnit.Team, Is.EqualTo(slot.Team));
                Assert.That(templateUnit.Altitude, Is.EqualTo(Altitude.Ground));
                Assert.That(templateUnit.CanAttackGround, Is.True);

                float expectedHealth;
                float expectedAttack;
                float expectedDefense;
                float expectedRange;
                float expectedInterval;
                float expectedSpeed;
                bool expectedAirAttack;
                switch (slot.OperatorType)
                {
                    case OperatorType.Exusiai:
                        expectedHealth = 1000f;
                        expectedAttack = 50f;
                        expectedDefense = 2f;
                        expectedRange = 6f;
                        expectedInterval = 0.5f;
                        expectedSpeed = 5f;
                        expectedAirAttack = true;
                        break;
                    case OperatorType.Eyjafjalla:
                        expectedHealth = 950f;
                        expectedAttack = 75f;
                        expectedDefense = 10f;
                        expectedRange = 6f;
                        expectedInterval = 1.2f;
                        expectedSpeed = 4.8f;
                        expectedAirAttack = true;
                        break;
                    case OperatorType.SilverAsh:
                        expectedHealth = 1400f;
                        expectedAttack = 85f;
                        expectedDefense = 30f;
                        expectedRange = 2.2f;
                        expectedInterval = 1.1f;
                        expectedSpeed = 4.8f;
                        expectedAirAttack = false;
                        break;
                    default:
                        Assert.Fail($"Unexpected operator type {slot.OperatorType}.");
                        yield break;
                }

                Assert.That(templateUnit.MaxHealth, Is.EqualTo(expectedHealth));
                Assert.That(templateUnit.BaseAttackPower, Is.EqualTo(expectedAttack));
                Assert.That(templateUnit.Defense, Is.EqualTo(expectedDefense));
                Assert.That(templateUnit.AttackRange, Is.EqualTo(expectedRange));
                Assert.That(templateUnit.BaseAttackInterval, Is.EqualTo(expectedInterval));
                Assert.That(templateUnit.CanAttackAir, Is.EqualTo(expectedAirAttack));
                Assert.That(slot.Template.GetComponent<UnitMotor>().BaseMovementSpeed, Is.EqualTo(expectedSpeed));

                float expectedX = slot.Team == TeamId.Blue ? -46f : 46f;
                float expectedZ = slot.OperatorType == OperatorType.Eyjafjalla ? -2f
                    : slot.OperatorType == OperatorType.SilverAsh ? 2f : 0f;
                Assert.That(slot.DeploymentPosition.x, Is.EqualTo(expectedX));
                Assert.That(slot.DeploymentPosition.y, Is.EqualTo(1.2f));
                Assert.That(slot.DeploymentPosition.z, Is.EqualTo(expectedZ));
                Assert.That(slot.Template.transform.position, Is.EqualTo(slot.DeploymentPosition));
                Assert.That(slot.CurrentOperator, Is.Not.Null);
                Assert.That(slot.CurrentOperator.gameObject, Is.Not.SameAs(slot.Template));
                Assert.That(slot.CurrentOperator.gameObject.activeInHierarchy, Is.True);
                Assert.That(slot.CurrentOperator.transform.position, Is.EqualTo(slot.DeploymentPosition));
                Assert.That(slot.CurrentOperator.Team, Is.EqualTo(slot.Team));
                Assert.That(slot.CurrentOperator.Altitude, Is.EqualTo(Altitude.Ground));
                Assert.That(slot.CurrentOperator.MaxHealth, Is.EqualTo(expectedHealth));
                Assert.That(slot.CurrentOperator.BaseAttackPower, Is.EqualTo(expectedAttack));
                Assert.That(slot.CurrentOperator.Defense, Is.EqualTo(expectedDefense));
                Assert.That(slot.CurrentOperator.AttackRange, Is.EqualTo(expectedRange));
                Assert.That(slot.CurrentOperator.BaseAttackInterval, Is.EqualTo(expectedInterval));
                Assert.That(slot.CurrentOperator.CanAttackGround, Is.True);
                Assert.That(slot.CurrentOperator.CanAttackAir, Is.EqualTo(expectedAirAttack));
                Assert.That(slot.CurrentOperator.GetComponent<UnitMotor>().BaseMovementSpeed,
                    Is.EqualTo(expectedSpeed));
                Assert.That(slot.CurrentOperator.GetComponent<OperatorIdentity>().StableKey, Is.EqualTo(slot.StableKey));
                if (slot.IsPlayerControlled)
                {
                    playerControlledSlots++;
                    Assert.That(slot.CurrentOperator.GetComponent<ExusiaiSkillController>(), Is.Not.Null);
                    Assert.That(slot.Template.GetComponent<ExusiaiSkillController>(), Is.Not.Null);
                }
                else
                {
                    Assert.That(slot.CurrentOperator.GetComponent<ExusiaiSkillController>(), Is.Null);
                    Assert.That(slot.CurrentOperator.GetComponent<SimpleOperatorAiController>(), Is.Not.Null);
                    Assert.That(slot.Template.GetComponent<ExusiaiSkillController>(), Is.Null);
                    Assert.That(slot.Template.GetComponent<SimpleOperatorAiController>(), Is.Not.Null);
                }
            }

            Assert.That(playerControlledSlots, Is.EqualTo(1));
            PlayerDeploymentPresenter playerDeployment = Object.FindFirstObjectByType<PlayerDeploymentPresenter>();
            Assert.That(playerDeployment, Is.Not.Null);
            Assert.That(playerDeployment.CurrentOperator, Is.SameAs(GameObject.Find("Player_Exusiai").GetComponent<CombatUnit>()));
            SkillHudPresenter hud = Object.FindFirstObjectByType<SkillHudPresenter>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.BoundController,
                Is.SameAs(playerDeployment.CurrentOperator.GetComponent<ExusiaiSkillController>()));

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            Assert.That(mainCamera, Is.Not.Null);
            Vector3 playerGroundPosition = playerDeployment.CurrentOperator.transform.position;
            playerGroundPosition.y = 0f;
            Vector3 playerViewport = mainCamera.WorldToViewportPoint(playerGroundPosition);
            Assert.That(playerViewport.z, Is.GreaterThan(0f));
            Assert.That(playerViewport.x, Is.EqualTo(0.5f).Within(0.03f));
            Assert.That(playerViewport.y, Is.EqualTo(0.5f).Within(0.03f));
            Vector3 laneAhead = mainCamera.WorldToViewportPoint(playerGroundPosition + Vector3.right * 6f);
            Assert.That(laneAhead.x, Is.GreaterThan(playerViewport.x));
            Assert.That(laneAhead.y, Is.GreaterThan(playerViewport.y));
        }

        [UnityTest]
        public IEnumerator PrototypeArenaContainsRequiredRoots()
        {
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;
            ArenaBootstrap arena = Object.FindFirstObjectByType<ArenaBootstrap>();
            Assert.That(arena, Is.Not.Null);
            Assert.That(arena.BlueTower, Is.Not.Null);
            Assert.That(arena.RedTower, Is.Not.Null);
            AssertTowerIsCombatReady(arena.BlueTower);
            AssertTowerIsCombatReady(arena.RedTower);

            MatchOutcomeController outcome = Object.FindFirstObjectByType<MatchOutcomeController>();
            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome.Outcome, Is.EqualTo(MatchOutcome.None));
            Assert.That(Object.FindFirstObjectByType<MinionWaveSpawner>(), Is.Not.Null);
            GameObject player = GameObject.Find("Player_Exusiai");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<DeathCorpsePresenter>().UnitKind, Is.EqualTo(UnitKind.Operator));
            CombatUnit playerUnit = player.GetComponent<CombatUnit>();
            Assert.That(playerUnit, Is.Not.Null);
            Assert.That(playerUnit.MaxHealth, Is.EqualTo(1000f));
            Assert.That(playerUnit.AttackPower, Is.EqualTo(50f));
            Assert.That(playerUnit.AttackInterval, Is.EqualTo(0.5f));
            Assert.That(playerUnit.AttackRange, Is.EqualTo(6f));
            Assert.That(player.transform.localScale, Is.EqualTo(new Vector3(1.2f, 1.2f, 1.2f)));
            Assert.That(player.GetComponent<UnitMotor>().BaseMovementSpeed, Is.EqualTo(5f));
            Assert.That(player.GetComponent<UnitStatModifiers>(), Is.Not.Null);
            Assert.That(player.GetComponent<AttackSequenceExecutor>(), Is.Not.Null);
            Assert.That(player.GetComponent<SkillDashController>(), Is.Not.Null);
            Assert.That(player.GetComponent<ExusiaiSkillController>(), Is.Not.Null);
            Assert.That(player.GetComponent<ExusiaiSkillIndicator>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SkillHudPresenter>(), Is.Not.Null);
            Assert.That(player.transform.position.y, Is.EqualTo(1.2f));
            Assert.That(player.GetComponent<HealthBarPresenter>(), Is.Not.Null);
            GameObject enemyOperator = GameObject.Find("Red_Exusiai");
            Assert.That(enemyOperator, Is.Not.Null);
            Assert.That(enemyOperator.transform.localScale, Is.EqualTo(player.transform.localScale));
            Assert.That(enemyOperator.transform.position.y, Is.EqualTo(player.transform.position.y));
            Assert.That(enemyOperator.GetComponent<CapsuleCollider>(), Is.Not.Null);
            Assert.That(enemyOperator.GetComponent<OperatorIdentity>().Team, Is.EqualTo(TeamId.Red));
            Assert.That(enemyOperator.GetComponent<HealthBarPresenter>(), Is.Not.Null);
            Assert.That(enemyOperator.GetComponent<DeathCorpsePresenter>(), Is.Not.Null);
            Assert.That(enemyOperator.GetComponent<DeathCorpsePresenter>().UnitKind,
                Is.EqualTo(UnitKind.Operator));
            Assert.That(enemyOperator.transform.Find("HealthBar"), Is.Not.Null);
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            Assert.That(mainCamera, Is.Not.Null);
            Vector3 groundPosition = player.transform.position;
            groundPosition.y = 0f;
            Vector3 playerViewport = mainCamera.WorldToViewportPoint(groundPosition);
            Assert.That(playerViewport.z, Is.GreaterThan(0f));
            Assert.That(playerViewport.x, Is.EqualTo(0.5f).Within(0.03f));
            Assert.That(playerViewport.y, Is.EqualTo(0.5f).Within(0.03f));
            Vector3 laneAhead = mainCamera.WorldToViewportPoint(groundPosition + Vector3.right * 6f);
            Assert.That(laneAhead.x, Is.GreaterThan(playerViewport.x));
            Assert.That(laneAhead.y, Is.GreaterThan(playerViewport.y));
        }

        [UnityTest]
        public IEnumerator SavedTowerControllerRebindsAndTargetsNearbyEnemy()
        {
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;

            ArenaBootstrap arena = Object.FindFirstObjectByType<ArenaBootstrap>();
            CombatUnit blueTower = arena.BlueTower.GetComponent<CombatUnit>();
            BasicAttackController blueTowerAttack = arena.BlueTower.GetComponent<BasicAttackController>();
            GameObject enemyObject = new GameObject("TowerRebindEnemy");
            enemyObject.transform.position = blueTower.transform.position + Vector3.right;
            CombatUnit enemy = enemyObject.AddComponent<CombatUnit>();
            enemy.Configure(TeamId.Red, Altitude.Ground, 1000f, 0f, 0f, 0f, 0f, false, false);

            yield return null;

            Assert.That(blueTowerAttack.CurrentTarget, Is.EqualTo(enemy));
        }

        [UnityTest]
        public IEnumerator SavedOutcomeControllerResolvesDestroyedTower()
        {
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;

            ArenaBootstrap arena = Object.FindFirstObjectByType<ArenaBootstrap>();
            MatchOutcomeController outcome = Object.FindFirstObjectByType<MatchOutcomeController>();
            GameObject minion = GameObject.Find("BlueGroundMinion_1_1");
            Assert.That(minion, Is.Not.Null);
            minion.GetComponent<CombatUnit>().TakePhysicalDamage(minion.GetComponent<CombatUnit>().MaxHealth);
            yield return null;
            GameObject minionCorpse = GameObject.Find("BlueGroundMinion_1_1_Corpse");
            Assert.That(minionCorpse, Is.Not.Null);

            GameObject redTowerObject = arena.RedTower.gameObject;
            CombatUnit redTower = redTowerObject.GetComponent<CombatUnit>();
            Renderer redTowerRenderer = redTowerObject.transform.Find("RedTowerVisual").GetComponent<Renderer>();
            Material redMaterial = redTowerRenderer.sharedMaterial;
            Transform healthBar = redTowerObject.transform.Find("HealthBar");
            Assert.That(healthBar, Is.Not.Null);

            redTower.TakePhysicalDamage(redTower.MaxHealth);
            yield return null;

            Assert.That(outcome.IsMatchOver, Is.True);
            Assert.That(outcome.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
            Assert.That(redTowerObject == null, Is.True);
            Assert.That(healthBar == null, Is.True);
            GameObject corpse = GameObject.Find("RedTower_Corpse");
            Assert.That(corpse, Is.Not.Null);
            Assert.That(corpse.transform.localScale, Is.EqualTo(new Vector3(0.3f, 1f, 0.3f)));
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(redMaterial));
            Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
            Assert.That(corpse.GetComponent<HealthBarPresenter>(), Is.Null);
            Assert.That(corpse.GetComponent<BasicAttackController>(), Is.Null);
            Assert.That(corpse.GetComponent<TowerCombatController>(), Is.Null);
            Assert.That(corpse.layer, Is.EqualTo(LayerMask.NameToLayer("Default")));
            Assert.That(corpse.GetComponent<Collider>(), Is.Null);
            CorpseLifetimeController towerLifetime = corpse.GetComponent<CorpseLifetimeController>();
            Assert.That(towerLifetime, Is.Not.Null);
            Assert.That(towerLifetime.UnitKind, Is.EqualTo(UnitKind.Tower));
            towerLifetime.Tick(60f);
            yield return null;
            Assert.That(corpse == null, Is.False);

            yield return new WaitForSeconds(CorpseLifetimeController.MinionLifetimeSeconds + 0.2f);
            Assert.That(minionCorpse == null, Is.True);
            Assert.That(corpse == null, Is.False);
        }

        [UnityTest]
        public IEnumerator SavedPlayerDeathCreatesGroundedCorpseWithoutRuntimeConfigure()
        {
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;

            GameObject player = GameObject.Find("Player_Exusiai");
            CombatUnit playerUnit = player.GetComponent<CombatUnit>();
            Renderer playerRenderer = player.GetComponent<Renderer>();
            Vector3 playerPosition = player.transform.position;
            Material playerMaterial = playerRenderer.sharedMaterial;
            Assert.That(player.transform.Find("HealthBar"), Is.Not.Null);

            playerUnit.TakePhysicalDamage(playerUnit.MaxHealth);

            Assert.That(playerRenderer.enabled, Is.False);
            yield return null;

            Assert.That(player == null, Is.True);
            GameObject corpse = GameObject.Find("Player_Exusiai_Corpse");
            Assert.That(corpse, Is.Not.Null);
            CorpseLifetimeController lifetime = corpse.GetComponent<CorpseLifetimeController>();
            Assert.That(lifetime, Is.Not.Null);
            Assert.That(lifetime.UnitKind, Is.EqualTo(UnitKind.Operator));
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(playerMaterial));
            Assert.That(corpse.transform.position.x, Is.EqualTo(playerPosition.x));
            Assert.That(corpse.transform.position.y, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(corpse.transform.position.z, Is.EqualTo(playerPosition.z));
            Assert.That(corpse.transform.Find("HealthBar"), Is.Null);
            Collider collider = corpse.GetComponent<Collider>();
            Assert.That(collider == null || !collider.enabled, Is.True);
            lifetime.Tick(60f);
            yield return null;
            Assert.That(corpse == null, Is.False);
        }

        [UnityTest]
        public IEnumerator SavedEnemyOperatorDeathCreatesGroundedCorpse()
        {
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;

            GameObject enemyOperator = GameObject.Find("Red_Exusiai");
            CombatUnit unit = enemyOperator.GetComponent<CombatUnit>();
            Material material = enemyOperator.GetComponent<Renderer>().sharedMaterial;
            Vector3 position = enemyOperator.transform.position;

            unit.TakePhysicalDamage(unit.MaxHealth);
            yield return null;

            Assert.That(enemyOperator == null, Is.True);
            GameObject corpse = GameObject.Find("Red_Exusiai_Corpse");
            Assert.That(corpse, Is.Not.Null);
            CorpseLifetimeController lifetime = corpse.GetComponent<CorpseLifetimeController>();
            Assert.That(lifetime, Is.Not.Null);
            Assert.That(lifetime.UnitKind, Is.EqualTo(UnitKind.Operator));
            Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
            Assert.That(corpse.transform.position, Is.EqualTo(new Vector3(position.x, 0.01f, position.z)));
            Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
            Assert.That(corpse.transform.Find("HealthBar"), Is.Null);
            Collider collider = corpse.GetComponent<Collider>();
            Assert.That(collider == null || !collider.enabled, Is.True);
            lifetime.Tick(60f);
            yield return null;
            Assert.That(corpse == null, Is.False);
        }

        [UnityTest]
        public IEnumerator OperatorRegistryDoesNotRetainCorpseAcrossSceneReload()
        {
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;

            GameObject player = GameObject.Find("Player_Exusiai");
            player.GetComponent<CombatUnit>().TakePhysicalDamage(player.GetComponent<CombatUnit>().MaxHealth);
            yield return null;
            Assert.That(GameObject.Find("Player_Exusiai_Corpse"), Is.Not.Null);

            Scene prototypeArena = SceneManager.GetSceneByName("PrototypeArena");
            Scene cleanupScene = SceneManager.CreateScene("OperatorRegistryReloadCleanup");
            SceneManager.SetActiveScene(cleanupScene);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(prototypeArena);
            while (!unload.isDone)
            {
                yield return null;
            }

            DefaultCharacterSelection.LoadScene("PrototypeArena", LoadSceneMode.Single);
            yield return null;
            GameObject reloadedPlayer = GameObject.Find("Player_Exusiai");
            Assert.That(reloadedPlayer, Is.Not.Null);
            Assert.That(() => CorpseLifetimeController.ClearOperatorCorpse("Player_Exusiai"), Throws.Nothing);
            Assert.That(reloadedPlayer == null, Is.False);
        }

        private static void AssertTowerIsCombatReady(Transform tower)
        {
            Assert.That(tower.GetComponent<CombatUnit>(), Is.Not.Null, $"{tower.name} needs a CombatUnit.");
            Assert.That(tower.GetComponent<BasicAttackController>(), Is.Not.Null,
                $"{tower.name} needs a BasicAttackController.");
            Assert.That(tower.GetComponent<TowerCombatController>(), Is.Not.Null,
                $"{tower.name} needs a TowerCombatController.");
            Assert.That(tower.GetComponent<DeathCorpsePresenter>(), Is.Not.Null,
                $"{tower.name} needs a DeathCorpsePresenter.");
            Assert.That(tower.GetComponent<DeathCorpsePresenter>().UnitKind, Is.EqualTo(UnitKind.Tower),
                $"{tower.name} corpse category should be Tower.");
            Assert.That(tower.GetComponent<BoxCollider>(), Is.Not.Null,
                $"{tower.name} needs a root BoxCollider.");
            CombatUnit combatUnit = tower.GetComponent<CombatUnit>();
            Assert.That(combatUnit.MaxHealth, Is.EqualTo(500f),
                $"{tower.name} should have 500 max health.");
            Assert.That(combatUnit.AttackPower, Is.EqualTo(20f),
                $"{tower.name} should have 20 attack power.");
            HealthBarPresenter healthBar = tower.GetComponent<HealthBarPresenter>();
            Assert.That(healthBar, Is.Not.Null,
                $"{tower.name} needs a HealthBarPresenter.");
            Assert.That(healthBar.IsVisible, Is.True,
                $"{tower.name} health bar should be visible.");
            Assert.That(tower.Find("HealthBar"), Is.Not.Null,
                $"{tower.name} should contain a HealthBar child.");
        }
    }
}
