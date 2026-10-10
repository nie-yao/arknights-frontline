using System.Collections;
using System.IO;
using System.Linq;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class MobaViewPresentationPlayModeTests
    {
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;
            Time.timeScale = 0f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            Scene arena = SceneManager.GetSceneByName("PrototypeArena");
            Scene cleanup = SceneManager.CreateScene("MobaViewCleanup");
            SceneManager.SetActiveScene(cleanup);
            if (arena.isLoaded) yield return SceneManager.UnloadSceneAsync(arena);
        }

        [UnityTest]
        public IEnumerator SavedScaledOperatorsRemainSelectableAndGroundedAfterRedeployment()
        {
            var roster = Object.FindFirstObjectByType<OperatorRosterController>();
            var camera = UnityEngine.Camera.main;
            var controller = camera.GetComponent<MobaCameraController>();
            controller.enabled = false;
            foreach (OperatorRosterSlot slot in roster.Slots)
            {
                CombatUnit unit = slot.CurrentOperator;
                controller.CenterOn(unit.transform);
                Physics.SyncTransforms();
                Bounds bounds = VisibleBodyBounds(unit);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.02f), slot.StableKey);
                Assert.That(unit.GetComponent<Collider>().bounds.min.y, Is.EqualTo(0f).Within(0.01f));
                Ray ray = camera.ViewportPointToRay(camera.WorldToViewportPoint(bounds.center));
                Assert.That(Physics.Raycast(ray, out RaycastHit hit, 100f, 1 << unit.gameObject.layer), Is.True);
                Assert.That(hit.collider.GetComponent<CombatUnit>(), Is.SameAs(unit), "visible body must be selectable");
                unit.GetComponent<HealthBarPresenter>().Tick();
                Assert.That(unit.transform.Find("HealthBar").position.y, Is.GreaterThan(bounds.max.y));
            }

            OperatorRosterSlot player = roster.Slots.Single(slot => slot.IsPlayerControlled);
            player.CurrentOperator.TakePhysicalDamage(100000f);
            yield return null;
            roster.Tick(30f);
            Assert.That(player.CurrentOperator, Is.Not.Null);
            yield return null;
            Assert.That(VisibleBodyBounds(player.CurrentOperator).min.y, Is.EqualTo(0f).Within(0.02f));
            Assert.That(player.CurrentOperator.GetComponent<Collider>().bounds.min.y, Is.EqualTo(0f).Within(0.01f));

            // Real scene render evidence is available when the test runner has a graphics device.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) yield break;
            controller.CenterOn(player.CurrentOperator.transform);
            Capture(camera, "deployment");
            GameObject focus = new GameObject("ViewCaptureFocus");
            ArrangeEncounter(roster, 0f);
            controller.CenterOn(focus.transform);
            Capture(camera, "midlane");
            ArrangeEncounter(roster, 30f);
            focus.transform.position = new Vector3(32f, 0f, 0f);
            controller.CenterOn(focus.transform);
            Capture(camera, "tower");
            Object.Destroy(focus);
        }

        private static Bounds VisibleBodyBounds(CombatUnit unit)
        {
            if (unit.GetComponent<Renderer>().enabled) return unit.GetComponent<Renderer>().bounds;
            var skins = unit.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled).ToArray();
            Assert.That(skins.Length, Is.GreaterThan(0));
            Bounds bounds = default; bool first = true;
            // Imported skinned bounds remain conservative; validate the geometry rendered now.
            foreach (var skin in skins)
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    foreach (var vertex in mesh.vertices)
                    {
                        Vector3 point = skin.transform.TransformPoint(vertex);
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            return bounds;
        }

        private static void ArrangeEncounter(OperatorRosterController roster, float centerX)
        {
            foreach (OperatorRosterSlot slot in roster.Slots)
            {
                float side = slot.Team == TeamId.Blue ? -1f : 1f;
                Vector3 position = slot.CurrentOperator.transform.position;
                position.x = centerX + side * 4.5f;
                position.z = slot.DeploymentPosition.z;
                slot.CurrentOperator.transform.position = position;
            }
            foreach (LaneMinionController minion in Object.FindObjectsByType<LaneMinionController>(FindObjectsSortMode.None))
            {
                var unit = minion.GetComponent<CombatUnit>();
                Vector3 position = minion.transform.position;
                position.x = centerX + (unit.Team == TeamId.Blue ? -1.5f : 1.5f);
                minion.transform.position = position;
            }
        }

        private static void Capture(UnityEngine.Camera camera, string view)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../.superpowers/sdd/2026-10-04-moba-view/screenshots"));
            Directory.CreateDirectory(directory);
            var overlays = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            try
            {
                foreach (Canvas canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                }
                foreach (int height in new[] { 1080, 720 })
                {
                    int width = height * 16 / 9;
                    var target = new RenderTexture(width, height, 24);
                    var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = target;
                        camera.aspect = 16f / 9f;
                        foreach (HealthBarPresenter bar in Object.FindObjectsByType<HealthBarPresenter>(FindObjectsSortMode.None)) bar.Tick();
                        Canvas.ForceUpdateCanvases();
                        camera.Render();
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        pixels.Apply();
                        File.WriteAllBytes(Path.Combine(directory, view + "-" + height + ".png"), pixels.EncodeToPNG());
                    }
                    finally
                    {
                        camera.targetTexture = previousTarget;
                        RenderTexture.active = previousActive;
                        Object.DestroyImmediate(pixels);
                        target.Release();
                        Object.DestroyImmediate(target);
                    }
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                foreach (Canvas canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                }
            }
        }
    }
}
