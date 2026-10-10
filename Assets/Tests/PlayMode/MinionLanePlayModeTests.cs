using System.Collections;
using System.Linq;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class MinionLanePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            Scene arena = SceneManager.GetSceneByName("PrototypeArena");
            if (!arena.isLoaded)
            {
                yield break;
            }

            Scene cleanup = SceneManager.CreateScene("MinionLaneCleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(arena);
        }

        [UnityTest]
        public IEnumerator PrototypeArenaSpawnsBalancedWaveThatAdvances()
        {
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;

            CombatUnit[] minions = Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None)
                .Where(unit => unit.name.Contains("Minion"))
                .ToArray();

            Assert.That(minions, Has.Length.EqualTo(8));
            AssertTeamWave(minions, TeamId.Blue);
            AssertTeamWave(minions, TeamId.Red);

            CombatUnit blueGroundMinion = minions.First(unit =>
                unit.Team == TeamId.Blue && unit.Altitude == Altitude.Ground);
            float initialX = blueGroundMinion.transform.position.x;

            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
            }

            BasicAttackController attack = blueGroundMinion.GetComponent<BasicAttackController>();
            Assert.That(
                blueGroundMinion.transform.position.x > initialX || attack.CurrentTarget != null,
                Is.True,
                "A blue ground minion should advance toward the red tower or engage a valid target.");
        }

        private static void AssertTeamWave(CombatUnit[] minions, TeamId team)
        {
            Assert.That(minions.Count(unit => unit.Team == team && unit.Altitude == Altitude.Ground), Is.EqualTo(3));
            Assert.That(minions.Count(unit => unit.Team == team && unit.Altitude == Altitude.Air), Is.EqualTo(1));
        }
    }
}
