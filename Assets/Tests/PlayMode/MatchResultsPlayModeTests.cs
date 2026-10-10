using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class MatchResultsPlayModeTests : InputTestFixture
    {
        private const string ScenePath = "Assets/Game/Scenes/PrototypeArena.unity";
        private const string PlayerStableKey = "Player_Exusiai";
        private const string RedExusiaiStableKey = "Red_Exusiai";

        private static int cleanupSceneCount;

        private float originalTimeScale;
        private Mouse testMouse;
        private InputActionAsset ownedUiActionsAsset;
        private readonly List<InputActionReference> ownedUiActionReferences = new List<InputActionReference>();

        public override void Setup()
        {
            base.Setup();
            originalTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        // The asynchronous teardown below must run before InputTestFixture restores the real
        // Input System runtime, so mirror the established project test-fixture pattern.
        public override void TearDown()
        {
        }

        [UnityTest]
        public IEnumerator SavedArenaBootstrapsLiveHudAndKeepsRunningPastFifteenMinutes()
        {
            yield return LoadSavedArena();
            SavedArena arena = FindSavedArena();

            Assert.That(arena.Presentation, Is.Not.Null);
            Assert.That(arena.Statistics.GetRows(), Has.Count.EqualTo(6));
            Assert.That(arena.Votes.IsConfigured, Is.True);
            Assert.That(arena.Hud.Canvas, Is.SameAs(arena.Canvas));
            Assert.That(arena.SkillHudTransform, Is.Not.Null,
                "The match presentation should share the existing Canvas without replacing the skill HUD.");
            Assert.That(arena.Hud.transform.parent, Is.SameAs(arena.Canvas.transform));
            Assert.That(arena.Hud.IsResultVisible, Is.False);
            Assert.That(arena.Statistics.Snapshot, Is.Null);

            float blueTowerMaxHealth = arena.BlueTower.MaxHealth;
            float redTowerMaxHealth = arena.RedTower.MaxHealth;
            arena.BlueTower.TakePhysicalDamage(35f);
            arena.RedTower.TakePhysicalDamage(20f);

            arena.Match.Tick(901f);
            arena.Hud.Refresh();

            Assert.That(arena.Match.ElapsedSeconds, Is.EqualTo(901f).Within(0.001f));
            Assert.That(arena.Match.IsMatchOver, Is.False,
                "The saved match must continue past 900 seconds without a timeout result.");
            Assert.That(arena.Hud.ClockTextComponent.text, Does.Contain("15:01"));
            Assert.That(arena.Hud.BlueTowerTextComponent.text,
                Does.Contain(FormatHealth(arena.BlueTower.CurrentHealth, arena.BlueTower.MaxHealth)));
            Assert.That(arena.Hud.RedTowerTextComponent.text,
                Does.Contain(FormatHealth(arena.RedTower.CurrentHealth, arena.RedTower.MaxHealth)));
            Assert.That(arena.BlueTower.CurrentHealth, Is.EqualTo(blueTowerMaxHealth - 35f).Within(0.001f));
            Assert.That(arena.RedTower.CurrentHealth, Is.EqualTo(redTowerMaxHealth - 20f).Within(0.001f));
            Assert.That(arena.Votes.RequestExit(PlayerStableKey), Is.False,
                "A live match cannot begin its exit vote.");
            Assert.That(arena.Votes.HasRequestedExit, Is.False);
        }

        [UnityTest]
        public IEnumerator SavedArenaFreezesResolvedTowerHealthAndRendersSixRowsFromSnapshot()
        {
            yield return LoadSavedArena();
            SavedArena arena = FindSavedArena();
            ResolveBlueVictory(arena, 137f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            arena.Hud.Refresh();

            MatchResultSnapshot snapshot = arena.Statistics.Snapshot;
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(arena.Statistics.IsFrozen, Is.True);
            Assert.That(snapshot.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
            Assert.That(snapshot.ElapsedSeconds, Is.EqualTo(137f).Within(0.001f));
            Assert.That(snapshot.RedTowerCurrentHealth, Is.Zero);
            MatchResultRow playerRow = snapshot.Rows.Single(row => row.StableKey == PlayerStableKey);
            MatchResultRow defeatedOperatorRow = snapshot.Rows.Single(row => row.StableKey == RedExusiaiStableKey);
            Assert.That(playerRow.Kills, Is.EqualTo(1));
            Assert.That(playerRow.TowerDamage, Is.EqualTo(snapshot.RedTowerMaxHealth).Within(0.001f));
            Assert.That(defeatedOperatorRow.Deaths, Is.EqualTo(1));
            Assert.That(arena.Hud.IsResultVisible, Is.True);
            Assert.That(arena.Hud.ResultTitleComponent.text, Does.Contain("胜利"));
            Assert.That(arena.Hud.ResultElapsedText, Is.EqualTo("02:17"));
            Assert.That(arena.Hud.ResultRowsRoot.childCount, Is.EqualTo(6));
            Text[] renderedRows = arena.Hud.ResultRowsRoot.GetComponentsInChildren<Text>(true);
            Assert.That(renderedRows, Has.Length.EqualTo(6));
            string playerRowText = renderedRows.Single(text => text.text.Contains("蓝队") && text.text.Contains("能天使")).text;
            string defeatedOperatorRowText = renderedRows
                .Single(text => text.text.Contains("红队") && text.text.Contains("能天使")).text;
            Assert.That(playerRowText, Does.Contain("击杀 1"));
            Assert.That(playerRowText, Does.Contain($"对塔伤害 {FormatNumber(snapshot.RedTowerMaxHealth)}"));
            Assert.That(defeatedOperatorRowText, Does.Contain("死亡 1"));
            Assert.That(arena.Hud.ExitButton, Is.Not.Null);
            Assert.That(arena.Hud.ExitButton.gameObject.activeInHierarchy, Is.True);

            arena.Match.Tick(500f);
            arena.Hud.Refresh();

            Assert.That(arena.Match.ElapsedSeconds, Is.EqualTo(137f).Within(0.001f));
            Assert.That(arena.Statistics.Snapshot, Is.SameAs(snapshot));
            Assert.That(arena.Statistics.Snapshot.BlueTowerCurrentHealth,
                Is.EqualTo(snapshot.BlueTowerCurrentHealth).Within(0.001f));

            arena.BlueTower.TakePhysicalDamage(1f);
            Assert.That(arena.BlueTower.CurrentHealth,
                Is.EqualTo(snapshot.BlueTowerCurrentHealth - 1f).Within(0.001f));

            arena.Match.Tick(500f);
            arena.Hud.Refresh();
            Assert.That(arena.Statistics.Snapshot, Is.SameAs(snapshot));
            Assert.That(arena.Statistics.Snapshot.BlueTowerCurrentHealth,
                Is.EqualTo(snapshot.BlueTowerCurrentHealth).Within(0.001f));
            Assert.That(arena.Statistics.Snapshot.Rows.Single(row => row.StableKey == PlayerStableKey).TowerDamage,
                Is.EqualTo(playerRow.TowerDamage).Within(0.001f));
            Assert.That(arena.Hud.ResultElapsedText, Is.EqualTo("02:17"));
            Assert.That(arena.Hud.ResultRowsRoot.childCount, Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator RealMouseClickOnSavedResultButtonApprovesThreeVotesAndInvokesInjectedExitOnce()
        {
            yield return LoadSavedArena();
            SavedArena arena = FindSavedArena();
            InputSystemUIInputModule savedUiModule = arena.EventSystem.GetComponent<InputSystemUIInputModule>();
            Assert.That(savedUiModule, Is.Not.Null);
            Assert.That(arena.EventSystem.currentInputModule, Is.SameAs(savedUiModule));
            InputActionAsset serializedUiActionsAsset = savedUiModule.actionsAsset;
            Assert.That(serializedUiActionsAsset, Is.Not.Null);
            Assert.That(serializedUiActionsAsset.name, Is.EqualTo("DefaultInputActions"));
#if UNITY_EDITOR
            // Package versions move this asset between InputSystem/Runtime/Plugins and
            // InputSystem/Plugins. Its GUID is the stable identity saved in the scene.
            Assert.That(
                UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(serializedUiActionsAsset)),
                Is.EqualTo("ca9f5fa95ffab41fb9a615ab714db018"),
                "The test must start from the exact package asset serialized into the saved scene.");
#endif

            InputActionReference[] serializedActionReferences = GetUiActionReferences(savedUiModule);
            string[] expectedActionNames =
            {
                "Point",
                "Navigate",
                "Submit",
                "Cancel",
                "Click",
                "MiddleClick",
                "RightClick",
                "ScrollWheel",
                "TrackedDevicePosition",
                "TrackedDeviceOrientation"
            };
            Assert.That(serializedActionReferences, Has.Length.EqualTo(expectedActionNames.Length));
            for (int index = 0; index < serializedActionReferences.Length; index++)
            {
                InputActionReference reference = serializedActionReferences[index];
                Assert.That(reference, Is.Not.Null, $"Saved UI action reference {expectedActionNames[index]} is missing.");
                Assert.That(reference.asset, Is.SameAs(serializedUiActionsAsset));
                Assert.That(reference.action, Is.Not.Null);
                Assert.That(reference.action.name, Is.EqualTo(expectedActionNames[index]));
                Assert.That(reference.action.actionMap.name, Is.EqualTo("UI"));
            }

            ownedUiActionsAsset = Object.Instantiate(serializedUiActionsAsset);
            ownedUiActionsAsset.name = serializedUiActionsAsset.name;
            Assert.That(ownedUiActionsAsset, Is.Not.SameAs(serializedUiActionsAsset));
            Assert.That(ownedUiActionsAsset.ToJson(), Is.EqualTo(serializedUiActionsAsset.ToJson()),
                "The per-test runtime copy must preserve the serialized DefaultInputActions configuration and IDs.");
            string[] serializedActionIds = serializedActionReferences
                .Select(reference => reference.action.id.ToString())
                .ToArray();

            // Keep the real saved EventSystem and UI module. Reassigning a clone of its serialized asset
            // isolates this InputTestFixture runtime from mutable imported InputActionAsset state while
            // the module's supported setter remaps and re-hooks the same serialized UI actions.
            savedUiModule.actionsAsset = ownedUiActionsAsset;
            InputActionReference[] reboundActionReferences = GetUiActionReferences(savedUiModule);
            foreach (InputActionReference reference in reboundActionReferences)
            {
                if (reference != null && !serializedActionReferences.Contains(reference))
                {
                    ownedUiActionReferences.Add(reference);
                }
            }

            Assert.That(ownedUiActionReferences, Has.Count.EqualTo(expectedActionNames.Length),
                "The real module should create and retain one test-owned reference for each remapped serialized action.");
            Assert.That(savedUiModule.actionsAsset, Is.SameAs(ownedUiActionsAsset));
            Assert.That(arena.EventSystem.currentInputModule, Is.SameAs(savedUiModule));
            Assert.That(ownedUiActionsAsset.ToJson(), Is.EqualTo(serializedUiActionsAsset.ToJson()));
            for (int index = 0; index < reboundActionReferences.Length; index++)
            {
                InputActionReference reference = reboundActionReferences[index];
                Assert.That(reference, Is.Not.Null);
                Assert.That(reference.asset, Is.SameAs(ownedUiActionsAsset));
                Assert.That(reference.action, Is.Not.Null);
                Assert.That(reference.action.id.ToString(), Is.EqualTo(serializedActionIds[index]));
                Assert.That(reference.action.name, Is.EqualTo(expectedActionNames[index]));
                Assert.That(reference.action.actionMap.name, Is.EqualTo("UI"));
            }

            int exitCallCount = 0;
            arena.Hud.Configure(
                arena.Match,
                arena.Statistics,
                arena.Votes,
                arena.BlueTower,
                arena.RedTower,
                PlayerStableKey,
                () => exitCallCount++);

            Assert.That(arena.Votes.RequestExit(PlayerStableKey), Is.False,
                "An exit request must be refused before a result exists.");
            ResolveBlueVictory(arena, 42f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            arena.Hud.Refresh();

            Assert.That(arena.Votes.AgreeCount, Is.Zero);
            Assert.That(exitCallCount, Is.Zero);
            Assert.That(arena.Hud.ExitButton.interactable, Is.True);
            Assert.That(arena.Canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(arena.EventSystem.enabled, Is.True);
            Assert.That(arena.EventSystem.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);

            testMouse = InputSystem.AddDevice<Mouse>();
            InputAction resolvedPointAction = savedUiModule.point.action;
            InputAction resolvedClickAction = savedUiModule.leftClick.action;
            Assert.That(resolvedPointAction.enabled, Is.True);
            Assert.That(resolvedClickAction.enabled, Is.True);
            Assert.That(resolvedPointAction.controls.Any(control => control.device == testMouse), Is.True,
                "The cloned saved Point action should resolve its serialized Pointer binding to this test Mouse.");
            Assert.That(resolvedClickAction.controls.Any(control => control.device == testMouse), Is.True,
                "The cloned saved Click action should resolve its serialized Pointer binding to this test Mouse.");
            yield return ClickAt(arena.Hud.ExitButton, arena.EventSystem, testMouse);

            InputActionReference pointReference = savedUiModule == null ? null : savedUiModule.point;
            InputActionReference clickReference = savedUiModule == null ? null : savedUiModule.leftClick;
            InputAction pointAction = pointReference == null ? null : pointReference.action;
            InputAction clickAction = clickReference == null ? null : clickReference.action;
            string pointControls = pointAction == null
                ? "<missing>"
                : string.Join(", ", pointAction.controls.Select(control => control.path));
            string clickControls = clickAction == null
                ? "<missing>"
                : string.Join(", ", clickAction.controls.Select(control => control.path));
            string pointValue = pointAction == null || !pointAction.enabled
                ? "<unavailable>"
                : pointAction.ReadValueAsObject()?.ToString() ?? "<null>";
            string clickValue = clickAction == null || !clickAction.enabled
                ? "<unavailable>"
                : clickAction.ReadValueAsObject()?.ToString() ?? "<null>";
            string diagnostics =
                "The physical click should start the player's exit vote. "
                + "EventSystem.currentIsSaved=" + (EventSystem.current == arena.EventSystem)
                + ", currentModule=" + (EventSystem.current == null || EventSystem.current.currentInputModule == null
                    ? "<none>"
                    : EventSystem.current.currentInputModule.GetType().FullName)
                + ", savedModule=" + (savedUiModule == null ? "<missing>" : savedUiModule.GetType().FullName)
                + ", savedModuleEnabled=" + (savedUiModule != null && savedUiModule.enabled)
                + ", savedModuleActive=" + (savedUiModule != null && savedUiModule.isActiveAndEnabled)
                + ", savedEventSystemEnabled=" + arena.EventSystem.enabled
                + ", eventSystemFocused=" + arena.EventSystem.isFocused
                + ", applicationFocused=" + Application.isFocused
                + ", backgroundBehavior=" + InputSystem.settings.backgroundBehavior
                + ", inputRunInBackground=" + InputSystem.runInBackground
                + ", pointAction=" + (pointAction == null ? "<missing>" : pointAction.name)
                + ", pointEnabled=" + (pointAction != null && pointAction.enabled)
                + ", pointControlCount=" + (pointAction == null ? 0 : pointAction.controls.Count)
                + ", pointControls=[" + pointControls + "]"
                + ", pointValue=" + pointValue
                + ", clickAction=" + (clickAction == null ? "<missing>" : clickAction.name)
                + ", clickEnabled=" + (clickAction != null && clickAction.enabled)
                + ", clickControlCount=" + (clickAction == null ? 0 : clickAction.controls.Count)
                + ", clickControls=[" + clickControls + "]"
                + ", clickValue=" + clickValue
                + ", moduleActionsAsset=" + (savedUiModule == null || savedUiModule.actionsAsset == null
                    ? "<missing>"
                    : savedUiModule.actionsAsset.name)
                + ", pointReferenceAssetMatchesModule=" + (savedUiModule != null
                    && pointReference != null
                    && pointReference.asset == savedUiModule.actionsAsset)
                + ", clickReferenceAssetMatchesModule=" + (savedUiModule != null
                    && clickReference != null
                    && clickReference.asset == savedUiModule.actionsAsset)
                + ", testMouseCurrent=" + (Mouse.current == testMouse)
                + ", testMousePosition=" + testMouse.position.ReadValue()
                + ", devices=[" + string.Join(", ", InputSystem.devices.Select(device => device.displayName)) + "]";
            Assert.That(arena.Votes.HasRequestedExit, Is.True, diagnostics);
            Assert.That(arena.Votes.AgreeCount, Is.EqualTo(3),
                "The player click and both automatic computer approvals should fill the three blue seats.");
            Assert.That(arena.Votes.IsExitApproved, Is.True);
            Assert.That(arena.Hud.ExitVoteText, Does.Contain("3/3"));
            Assert.That(exitCallCount, Is.EqualTo(1));
            Assert.That(arena.PlayerCommands.enabled, Is.False,
                "The resolved match should disable combat input while leaving EventSystem active.");
            Assert.That(arena.Hud.IsResultVisible, Is.True,
                "An injected test exit callback should leave the results panel in the scene.");
            Assert.That(arena.EventSystem.enabled, Is.True,
                "The match freeze must not disable the EventSystem needed by the results UI.");

            Assert.That(arena.Votes.RequestExit(PlayerStableKey), Is.False,
                "The same player cannot begin a second exit vote.");
            Assert.That(arena.Votes.CastVote(PlayerStableKey, true), Is.False,
                "The player's existing approval cannot be counted twice.");
            Assert.That(arena.Votes.CastVote(RedExusiaiStableKey, true), Is.False,
                "An enemy seat cannot cast a vote in the blue team's exit decision.");
            Assert.That(arena.Votes.AgreeCount, Is.EqualTo(3));
            yield return ClickAt(arena.Hud.ExitButton, arena.EventSystem, testMouse);
            Assert.That(exitCallCount, Is.EqualTo(1),
                "Repeated physical clicks after approval must not invoke the exit callback again.");
        }

        [UnityTest]
        public IEnumerator ReloadingSavedArenaCreatesFreshClockStatisticsVotesAndHud()
        {
            yield return LoadSavedArena();
            SavedArena previousArena = FindSavedArena();
            int exitCallCount = 0;
            previousArena.Hud.Configure(
                previousArena.Match,
                previousArena.Statistics,
                previousArena.Votes,
                previousArena.BlueTower,
                previousArena.RedTower,
                PlayerStableKey,
                () => exitCallCount++);
            Font previousOwnedFont = previousArena.Hud.ClockTextComponent.font;
            Assert.That(previousOwnedFont, Is.Not.Null);
            Assert.That(previousOwnedFont,
                Is.Not.SameAs(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")));

            ResolveBlueVictory(previousArena, 91f);
            Assert.That(previousArena.Statistics.Snapshot, Is.Not.Null);
            Assert.That(previousArena.Statistics.Snapshot.Rows.Single(row => row.StableKey == PlayerStableKey).Kills,
                Is.EqualTo(1));
            Assert.That(previousArena.Statistics.Snapshot.Rows.Single(row => row.StableKey == PlayerStableKey).TowerDamage,
                Is.GreaterThan(0f));
            Assert.That(previousArena.Statistics.Snapshot.Rows.Single(row => row.StableKey == RedExusiaiStableKey).Deaths,
                Is.EqualTo(1));
            Assert.That(previousArena.Votes.RequestExit(PlayerStableKey), Is.True);
            Assert.That(exitCallCount, Is.EqualTo(1));
            Assert.That(previousArena.Votes.IsExitApproved, Is.True);

            AsyncOperation reload = DefaultCharacterSelection.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(reload, Is.Not.Null);
            while (!reload.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return null;
            SavedArena freshArena = FindSavedArena();

            Assert.That(previousArena.Match == null, Is.True,
                "A normal single-scene reload should destroy the previous match controller.");
            Assert.That(previousOwnedFont == null, Is.True,
                "Unloading the previous HUD must release its owned dynamic font.");
            Assert.That(freshArena.Match.ElapsedSeconds, Is.Zero);
            Assert.That(freshArena.Match.IsMatchOver, Is.False);
            Assert.That(freshArena.Match.Outcome, Is.EqualTo(MatchOutcome.None));
            Assert.That(freshArena.Statistics.Snapshot, Is.Null);
            Assert.That(freshArena.Statistics.IsFrozen, Is.False);
            Assert.That(freshArena.Statistics.GetRows(), Has.Count.EqualTo(6));
            foreach (MatchResultRow row in freshArena.Statistics.GetRows())
            {
                Assert.That(row.Kills, Is.Zero);
                Assert.That(row.Deaths, Is.Zero);
                Assert.That(row.TowerDamage, Is.Zero);
            }

            Assert.That(freshArena.Votes.IsConfigured, Is.True);
            Assert.That(freshArena.Votes.HasRequestedExit, Is.False);
            Assert.That(freshArena.Votes.AgreeCount, Is.Zero);
            Assert.That(freshArena.Votes.IsExitApproved, Is.False);
            Assert.That(freshArena.Hud.IsResultVisible, Is.False);
            Assert.That(freshArena.Hud.ExitVoteText, Does.Contain("0/3"));
            Assert.That(freshArena.Hud.ClockTextComponent.text, Does.Contain("00:00"));
            Assert.That(freshArena.Roster.Slots, Has.Count.EqualTo(6));
            Assert.That(freshArena.Roster.Slots.All(slot => slot.CurrentOperator != null && !slot.CurrentOperator.IsDead),
                Is.True, "A freshly reloaded match should deploy six live roster members.");
        }

        [UnityTearDown]
        public IEnumerator RestoreSceneTimeAndTestMouse()
        {
            try
            {
                Time.timeScale = 0f;
                if (testMouse != null && testMouse.added)
                {
                    InputSystem.RemoveDevice(testMouse);
                }

                testMouse = null;
                Scene prototypeArena = SceneManager.GetSceneByName("PrototypeArena");
                if (prototypeArena.isLoaded)
                {
                    Scene cleanupScene = SceneManager.CreateScene(
                        "MatchResultsPlayModeCleanup_" + cleanupSceneCount++);
                    SceneManager.SetActiveScene(cleanupScene);
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(prototypeArena);
                    while (unload != null && !unload.isDone)
                    {
                        yield return null;
                    }
                }

                for (int index = ownedUiActionReferences.Count - 1; index >= 0; index--)
                {
                    if (ownedUiActionReferences[index] != null)
                    {
                        Object.Destroy(ownedUiActionReferences[index]);
                    }
                }

                ownedUiActionReferences.Clear();
                if (ownedUiActionsAsset != null)
                {
                    Object.Destroy(ownedUiActionsAsset);
                    ownedUiActionsAsset = null;
                }

                yield return null;
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                base.TearDown();
            }
        }

        private static IEnumerator LoadSavedArena()
        {
            AsyncOperation load = DefaultCharacterSelection.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Could not load saved scene '{ScenePath}'.");
            while (!load.isDone)
            {
                yield return null;
            }

            // MatchPresentationBootstrap configures statistics and voting in Start, after the
            // roster's earlier Awake deployment has populated all six stable seats.
            yield return null;
        }

        private static SavedArena FindSavedArena()
        {
            GameObject arenaRoot = GameObject.Find("ArenaBootstrap");
            Assert.That(arenaRoot, Is.Not.Null, "The saved PrototypeArena root is missing.");

            ArenaBootstrap arenaBootstrap = arenaRoot.GetComponent<ArenaBootstrap>();
            Assert.That(arenaBootstrap, Is.Not.Null);
            OperatorRosterController roster = arenaRoot.GetComponent<OperatorRosterController>();
            MatchOutcomeController match = arenaRoot.GetComponent<MatchOutcomeController>();
            MatchStatisticsController statistics = arenaRoot.GetComponent<MatchStatisticsController>();
            TeamExitVoteController votes = arenaRoot.GetComponent<TeamExitVoteController>();
            MatchPresentationBootstrap presentation = arenaRoot.GetComponent<MatchPresentationBootstrap>();
            Assert.That(roster, Is.Not.Null);
            Assert.That(match, Is.Not.Null);
            Assert.That(statistics, Is.Not.Null);
            Assert.That(votes, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);

            GameObject canvasObject = GameObject.Find("Canvas");
            Assert.That(canvasObject, Is.Not.Null);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Transform skillHudTransform = canvas.transform.Find("SkillHud");
            Assert.That(skillHudTransform, Is.Not.Null);
            Transform hudTransform = canvas.transform.Find("MatchHud");
            Assert.That(hudTransform, Is.Not.Null);
            MatchHudPresenter hud = hudTransform.GetComponent<MatchHudPresenter>();
            Assert.That(hud, Is.Not.Null);

            GameObject eventSystemObject = GameObject.Find("EventSystem");
            Assert.That(eventSystemObject, Is.Not.Null);
            EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();
            Assert.That(eventSystem, Is.Not.Null);

            return new SavedArena(
                roster,
                match,
                statistics,
                votes,
                presentation,
                hud,
                canvas,
                skillHudTransform,
                eventSystem,
                arenaBootstrap.BlueTower.GetComponent<CombatUnit>(),
                arenaBootstrap.RedTower.GetComponent<CombatUnit>());
        }

        private static InputActionReference[] GetUiActionReferences(InputSystemUIInputModule module)
        {
            return new[]
            {
                module.point,
                module.move,
                module.submit,
                module.cancel,
                module.leftClick,
                module.middleClick,
                module.rightClick,
                module.scrollWheel,
                module.trackedDevicePosition,
                module.trackedDeviceOrientation
            };
        }

        private static void ResolveBlueVictory(SavedArena arena, float elapsedSeconds)
        {
            arena.Match.Tick(elapsedSeconds);
            arena.RedExusiai.TakePhysicalDamage(arena.RedExusiai.MaxHealth, arena.PlayerOperator);
            arena.RedTower.TakePhysicalDamage(25f, arena.PlayerOperator);
            arena.RedTower.TakePhysicalDamage(arena.RedTower.CurrentHealth, arena.PlayerOperator);
            arena.Match.Tick();
        }

        private IEnumerator ClickAt(Button button, EventSystem eventSystem, Mouse mouse)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, center);

            PointerEventData pointer = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };
            List<RaycastResult> results = new List<RaycastResult>();
            Canvas canvas = button.GetComponentInParent<Canvas>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, results);
            Image image = button.GetComponent<Image>();
            string raycastNames = string.Join(
                ", ",
                results.Select(result => result.gameObject.name + " (depth=" + result.depth + ")"));
            string diagnostic =
                "Expected the exit button to be present in the GraphicRaycaster results. "
                + "Screen=" + Screen.width + "x" + Screen.height
                + ", screenPosition=" + screenPosition
                + ", canvasRenderMode=" + canvas.renderMode
                + ", canvasScaleFactor=" + canvas.scaleFactor
                + ", canvasPixelRect=" + canvas.pixelRect
                + ", buttonRect=" + rect.rect
                + ", buttonWorldCorners=[" + string.Join(", ", corners.Select(corner => corner.ToString())) + "]"
                + ", imageDepth=" + (image == null ? "<missing>" : image.depth.ToString())
                + ", imageCull=" + (image == null ? "<missing>" : image.canvasRenderer.cull.ToString())
                + ", imageRaycastTarget=" + (image == null ? "<missing>" : image.raycastTarget.ToString())
                + ", buttonActiveSelf=" + button.gameObject.activeSelf
                + ", buttonActiveInHierarchy=" + button.gameObject.activeInHierarchy
                + ", buttonEnabled=" + button.enabled
                + ", raycastResults=[" + raycastNames + "]";
            Assert.That(results.Any(result => result.gameObject == button.gameObject), Is.True, diagnostic);

            Set(mouse.position, screenPosition);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
        }

        private static string FormatNumber(float value)
        {
            return Mathf.Max(0f, value).ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string FormatHealth(float currentHealth, float maxHealth)
        {
            return currentHealth.ToString("0.#", CultureInfo.InvariantCulture)
                + "/"
                + maxHealth.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private sealed class SavedArena
        {
            public SavedArena(
                OperatorRosterController roster,
                MatchOutcomeController match,
                MatchStatisticsController statistics,
                TeamExitVoteController votes,
                MatchPresentationBootstrap presentation,
                MatchHudPresenter hud,
                Canvas canvas,
                Transform skillHudTransform,
                EventSystem eventSystem,
                CombatUnit blueTower,
                CombatUnit redTower)
            {
                Roster = roster;
                Match = match;
                Statistics = statistics;
                Votes = votes;
                Presentation = presentation;
                Hud = hud;
                Canvas = canvas;
                SkillHudTransform = skillHudTransform;
                EventSystem = eventSystem;
                BlueTower = blueTower;
                RedTower = redTower;
                PlayerSlot = roster.Slots.Single(slot => slot.StableKey == PlayerStableKey);
                RedExusiaiSlot = roster.Slots.Single(slot => slot.StableKey == RedExusiaiStableKey);
                PlayerOperator = PlayerSlot.CurrentOperator;
                RedExusiai = RedExusiaiSlot.CurrentOperator;
                PlayerCommands = PlayerOperator.GetComponent<PlayerCommandController>();
            }

            public OperatorRosterController Roster { get; }

            public MatchOutcomeController Match { get; }

            public MatchStatisticsController Statistics { get; }

            public TeamExitVoteController Votes { get; }

            public MatchPresentationBootstrap Presentation { get; }

            public MatchHudPresenter Hud { get; }

            public Canvas Canvas { get; }

            public Transform SkillHudTransform { get; }

            public EventSystem EventSystem { get; }

            public CombatUnit BlueTower { get; }

            public CombatUnit RedTower { get; }

            public OperatorRosterSlot PlayerSlot { get; }

            public OperatorRosterSlot RedExusiaiSlot { get; }

            public CombatUnit PlayerOperator { get; }

            public CombatUnit RedExusiai { get; }

            public PlayerCommandController PlayerCommands { get; }
        }
    }
}
