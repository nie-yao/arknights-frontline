using System.Collections;
using System.IO;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class ExusiaiArenaIntegrationPlayModeTests : InputTestFixture
    {
        private OperatorRosterController roster;
        private OperatorRosterSlot playerSlot;
        private CombatUnit player;
        private Mouse mouse;
        private Keyboard keyboard;
        private float oldScale;
        private int oldCapture;
        public override void Setup() { base.Setup(); mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>(); }
        public override void TearDown() { }
        [UnitySetUp] public IEnumerator LoadArena()
        {
            oldScale = Time.timeScale; oldCapture = Time.captureFramerate;
            Time.timeScale = 1; Time.captureFramerate = 60;
            DefaultCharacterSelection.LoadScene("PrototypeArena"); yield return null;
            roster = Object.FindFirstObjectByType<OperatorRosterController>();
            playerSlot = roster.Slots.Single(s => s.IsPlayerControlled); player = playerSlot.CurrentOperator;
            Assert.That(player.GetComponent<ExusiaiCombatPresentation>(), Is.Not.Null, "Saved player scene has no formal model bridge");
        }
        [UnityTearDown] public IEnumerator CleanupArena()
        {
            Time.timeScale = oldScale; Time.captureFramerate = oldCapture;
            Scene arena = SceneManager.GetSceneByName("PrototypeArena");
            var cleanup = SceneManager.CreateScene("ExusiaiIntegrationCleanup"); SceneManager.SetActiveScene(cleanup);
            if (arena.isLoaded) yield return SceneManager.UnloadSceneAsync(arena);
            base.TearDown();
        }
        private Animator Animator => player.GetComponentInChildren<Animator>();
        private IEnumerator ReloadAfterInputSetup()
        {
            // InputTestFixture replaces the Input System after UnitySetUp on some runners.
            // Recreate action maps against the fixture's devices, as the existing skill tests do.
            DefaultCharacterSelection.LoadScene("PrototypeArena"); yield return null;
            roster = Object.FindFirstObjectByType<OperatorRosterController>();
            playerSlot = roster.Slots.Single(s => s.IsPlayerControlled); player = playerSlot.CurrentOperator;
            Assert.That(player.GetComponent<PlayerCommandController>().InputActions.FindAction("Skill2").controls.Contains(keyboard.eKey), Is.True);
        }
        private CombatUnit EnemyAt(float offset)
        {
            var enemy = roster.Slots.Single(s => s.Team == TeamId.Red && s.OperatorType == OperatorType.Exusiai).CurrentOperator;
            enemy.GetComponent<SimpleOperatorAiController>().enabled = false;
            enemy.GetComponent<UnitMotor>().Stop();
            enemy.transform.position = player.transform.position + Vector3.right * offset;
            Physics.SyncTransforms(); return enemy;
        }
        private void PointAt(Vector3 point)
        {
            Vector3 screen = UnityEngine.Camera.main.WorldToScreenPoint(point);
            Set(mouse.position, new Vector2(screen.x, screen.y));
        }
        [UnityTest] public IEnumerator NaturalMoveStopPauseAt30_60_120Fps()
        {
            yield return ReloadAfterInputSetup();
            foreach (int rate in new[] { 30, 60, 120 })
            {
                Time.captureFramerate = rate;
                UnityEngine.Camera.main.GetComponent<MobaCameraController>().CenterOn(player.transform);
                Vector3 before = player.transform.position;
                Vector3 direction = rate == 60 ? Vector3.back : Vector3.forward;
                PointAt(new Vector3(before.x, 0, before.z + direction.z * 4)); yield return null;
                Assert.That(player.GetComponent<PlayerCommandController>().TryGetCachedPointerHit(out RaycastHit moveHit), Is.True);
                Assert.That(moveHit.collider.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ground")), "Movement click must hit ground rather than the nearby friendly tower");
                Press(mouse.rightButton); yield return null; Release(mouse.rightButton);
                for (int i = 0; i < rate / 2; i++) yield return null;
                Assert.That(Vector3.Dot(player.transform.position - before, direction), Is.InRange(2.3f, 2.9f), "Real mouse input did not move at 5 m/s at " + rate + " FPS");
                Assert.That(Animator.GetFloat("MoveSpeed"), Is.GreaterThan(0.9f));
                float expected = Mathf.Clamp(5 / player.GetComponent<ExusiaiCombatPresentation>().VisualRoot.lossyScale.x / 2.4f, 0.01f, 3);
                Assert.That(Animator.GetFloat("JogRate"), Is.EqualTo(expected).Within(0.03f));
                Capture("natural-jog-" + rate);
                Press(keyboard.sKey); yield return null; Release(keyboard.sKey);
                for (int i = 0; i < rate / 2; i++) yield return null;
                Assert.That(Animator.GetFloat("MoveSpeed"), Is.LessThan(0.02f));
                Vector3 stopped = player.transform.position; Time.timeScale = 0;
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That(player.transform.position, Is.EqualTo(stopped));
                Time.timeScale = 1; yield return null;
                Assert.That(float.IsFinite(Animator.GetFloat("JogRate")), Is.True);
            }
        }
        [UnityTest] public IEnumerator SavedSceneMovesShootsAndRedeploysPlayerModel()
        {
            yield return ReloadAfterInputSetup();
            Capture("deployment");
            var enemy = EnemyAt(3);
            var sequence = player.GetComponent<AttackSequenceExecutor>(); int shots = 0;
            sequence.ShotRequested += (_, __) =>
            {
                shots++;
                Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Any(p =>
                    Vector3.Distance(p.transform.position, player.GetComponent<ProjectileSpawnPoint>().Muzzle.position) < 0.001f), Is.True);
            };
            PointAt(enemy.transform.position); yield return null;
            Press(mouse.rightButton); yield return null; Release(mouse.rightButton);
            for (int i = 0; i < 25; i++) yield return null;
            Assert.That(shots, Is.GreaterThan(0)); Assert.That(Animator.GetLayerWeight(2), Is.GreaterThan(0.95f));
            Capture("natural-standing-shot-player-and-ai");
            player.GetComponent<PlayerCommandController>().Issue(UnitCommand.Move(player.transform.position + Vector3.forward * 2));
            for (int i = 0; i < 10; i++) yield return null;
            PointAt(enemy.transform.position); yield return null; Press(mouse.rightButton); yield return null; Release(mouse.rightButton);
            Capture("natural-moving-attack-input");
            var oldBridge = player.GetComponent<ExusiaiCombatPresentation>();
            var oldVisual = oldBridge.VisualRoot; var oldMuzzle = player.GetComponent<ProjectileSpawnPoint>().Muzzle;
            player.TakePhysicalDamage(100000); Capture("death"); yield return null; roster.Tick(30); yield return null;
            player = playerSlot.CurrentOperator;
            var bridge = player.GetComponent<ExusiaiCombatPresentation>();
            Assert.That(bridge.VisualRoot.IsChildOf(player.transform), Is.True); Assert.That(bridge.VisualRoot, Is.Not.SameAs(oldVisual));
            Assert.That(bridge.Presentation.transform.IsChildOf(player.transform), Is.True);
            Assert.That(player.GetComponent<ProjectileSpawnPoint>().Muzzle, Is.Not.SameAs(oldMuzzle));
            Assert.That(player.GetComponent<ProjectileSpawnPoint>().Muzzle.IsChildOf(player.transform), Is.True);
            Assert.That(player.GetComponentInChildren<ExusiaiPreviewDriver>(true), Is.Null);
            Capture("redeployed");
            enemy = EnemyAt(3); player.GetComponent<PlayerCommandController>().Issue(UnitCommand.Attack(enemy.gameObject));
            for (int i = 0; i < 25; i++) yield return null;
            Assert.That(Animator.GetLayerWeight(2), Is.GreaterThan(0.95f));
        }
        [UnityTest] public IEnumerator ChargeAndOverloadKeepOriginalGameplayWhileAnimating()
        {
            yield return ReloadAfterInputSetup();
            var enemy = EnemyAt(8);
            var skills = player.GetComponent<ExusiaiSkillController>(); int shots = 0;
            player.GetComponent<AttackSequenceExecutor>().ShotRequested += (_, __) => shots++;
            Press(keyboard.eKey); yield return null; Release(keyboard.eKey);
            PointAt(new Vector3(player.transform.position.x + 5, 0, player.transform.position.z)); yield return null;
            Press(mouse.leftButton); yield return null; Release(mouse.leftButton);
            Assert.That(player.GetComponent<SkillDashController>().IsDashing, Is.True);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(shots, Is.GreaterThanOrEqualTo(1)); Assert.That(skills.Snapshot.ChargeCooldown, Is.GreaterThan(19));
            Assert.That(Animator.GetLayerWeight(2), Is.GreaterThan(0.95f)); Capture("natural-charge-shot");
            skills.Tick(11); Press(keyboard.rKey); yield return null; Release(keyboard.rKey);
            Assert.That(skills.IsOverloadActive, Is.True);
            player.GetComponent<PlayerCommandController>().Issue(UnitCommand.Attack(enemy.gameObject));
            int before = shots; for (int i = 0; i < 30; i++) yield return null;
            Assert.That(shots - before, Is.GreaterThanOrEqualTo(5)); Capture("natural-overload");
        }
        [UnityTest] public IEnumerator RetreatPauseAndSettlementLeaveNoGhostShots()
        {
            yield return ReloadAfterInputSetup();
            var enemy = EnemyAt(3);
            var sequence = player.GetComponent<AttackSequenceExecutor>();
            sequence.TryStart(new AttackSequencePlan(AttackSequenceKind.Basic, 1, 0.1f, 50, 1, 0, 1, 0, true, false), enemy);
            Assert.That(player.GetComponent<OperatorRetreatController>().TryBegin(), Is.True);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(Animator.GetLayerWeight(2), Is.LessThan(0.01f), "Retreat retained a pending recoil");
            player.GetComponent<OperatorRetreatController>().Tick(2); yield return null;
            roster.Tick(30); yield return null; player = playerSlot.CurrentOperator;
            Assert.That(player.GetComponent<ExusiaiCombatPresentation>().VisualRoot.IsChildOf(player.transform), Is.True);
            enemy = EnemyAt(3); player.GetComponent<PlayerCommandController>().Issue(UnitCommand.Attack(enemy.gameObject));
            yield return null;
            var arena = Object.FindFirstObjectByType<ArenaBootstrap>(); arena.RedTower.GetComponent<CombatUnit>().TakePhysicalDamage(100000);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(Animator.GetLayerWeight(2), Is.LessThan(0.01f));
            Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length, Is.Zero);
        }

        [UnityTest] public IEnumerator ChargeCompletionAt30FpsPreservesFirstShotFeedback()
        {
            yield return ReloadAfterInputSetup();
            Time.captureFramerate = 30;
            var enemy = EnemyAt(2);
            enemy.transform.position += Vector3.forward * 6.5f;
            int shots = 0;
            player.GetComponent<AttackSequenceExecutor>().ShotRequested += (_, __) => shots++;
            var skills = player.GetComponent<ExusiaiSkillController>();
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(player.transform.position + Vector3.forward * 6.5f, null), Is.True);
            var dash = player.GetComponent<SkillDashController>();
            Vector3 previous = player.transform.position;
            float finalStep = 0;
            for (int i = 0; i < 15 && dash.IsDashing; i++)
            {
                previous = player.transform.position;
                yield return null;
                if (!dash.IsDashing) finalStep = Vector3.Distance(previous, player.transform.position);
            }
            Assert.That(finalStep, Is.GreaterThan(0.7f), "Fixture did not exercise the final-step teleport threshold");
            Assert.That(shots, Is.EqualTo(1), "Charge still emits its gameplay shot immediately");
            // Isolate the first real shot: later E bullets otherwise hide a lost first feedback.
            player.GetComponent<AttackSequenceExecutor>().Cancel();
            for (int i = 0; i < 15; i++) yield return null;
            Assert.That(Animator.GetLayerWeight(2), Is.GreaterThan(0.95f), "Dash completion discarded first-shot feedback");
            Assert.That(Vector3.Dot(Animator.transform.forward, Vector3.right), Is.GreaterThan(0.9f), "Dash completion discarded the shot target direction");
        }
        [UnityTest] public IEnumerator AiExusiaiKeepsOriginalCapsuleAndAttackBehavior()
        {
            yield return ReloadAfterInputSetup();
            var aiSlot = roster.Slots.Single(s => s.Team == TeamId.Red && s.OperatorType == OperatorType.Exusiai);
            var ai = EnemyAt(3); Assert.That(ai.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(ai.GetComponentInChildren<ExusiaiPresentation>(true), Is.Null);
            Assert.That(ai.GetComponent<ExusiaiCombatPresentation>(), Is.Null); Assert.That(ai.GetComponent<ProjectileSpawnPoint>(), Is.Null);
            int shots = 0;
            ai.GetComponent<BasicAttackController>().AttackRequested += (_, __) => shots++;
            ai.GetComponent<BasicAttackController>().SetTarget(player);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(shots, Is.GreaterThan(0));
            ai.TakePhysicalDamage(100000); yield return null; roster.Tick(30); yield return null;
            Assert.That(aiSlot.CurrentOperator.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(aiSlot.CurrentOperator.GetComponentInChildren<ExusiaiPresentation>(true), Is.Null);
        }

        [UnityTest] public IEnumerator ShortDashCompletingBetweenSamplesPreservesFirstShotFeedback()
        {
            yield return ReloadAfterInputSetup();
            Time.captureFramerate = 30;
            var enemy = EnemyAt(2); enemy.transform.position += Vector3.forward * 0.9f;
            var skills = player.GetComponent<ExusiaiSkillController>();
            Assert.That(skills.BeginChargeTargeting(), Is.True);
            Assert.That(skills.TryConfirmCharge(player.transform.position + Vector3.forward * 0.9f, null), Is.True);
            player.GetComponent<SkillDashController>().Tick(1f / 30);
            Assert.That(player.GetComponent<SkillDashController>().IsDashing, Is.False);
            player.GetComponent<ExusiaiCombatPresentation>().Tick(1f / 30);
            player.GetComponent<AttackSequenceExecutor>().Cancel();
            for (int i = 0; i < 15; i++) yield return null;
            Assert.That(Animator.GetLayerWeight(2), Is.GreaterThan(0.95f), "Dash was never sampled active and lost its real shot feedback");
        }

        private void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var camera = UnityEngine.Camera.main; var controller = camera.GetComponent<MobaCameraController>(); controller.CenterOn(player.transform);
            string directory = Path.GetFullPath("TestResults/exusiai-arena-integration/screenshots"); Directory.CreateDirectory(directory);
            var previous = camera.targetTexture; var active = RenderTexture.active; float aspect = camera.aspect;
            try
            {
                foreach (int height in new[] { 720, 1080 })
                {
                    int width = height * 16 / 9; var render = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = render; camera.aspect = 16f / 9; camera.Render(); RenderTexture.active = render;
                        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(directory, name + "-" + height + ".png"), image.EncodeToPNG());
                    }
                    finally { camera.targetTexture = previous; RenderTexture.active = active; Object.DestroyImmediate(image); Object.DestroyImmediate(render); }
                }
            }
            finally { camera.aspect = aspect; camera.targetTexture = previous; RenderTexture.active = active; }
        }
    }
}
