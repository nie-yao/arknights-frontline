using System.Collections;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Common;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArknightsFrontline.Tests.PlayMode
{
    public sealed class PlayerMovementPlayModeTests
    {
        [UnityTest]
        public IEnumerator MotorReachesClampedDestination()
        {
            GameObject go = new GameObject("Motor");
            UnitMotor motor = go.AddComponent<UnitMotor>();
            motor.Configure(5f, ArenaLayout.CreateDefault());
            motor.SetDestination(new Vector3(100f, 0f, 0f));

            for (int i = 0; i < 700; i++)
            {
                motor.Tick(0.02f);
            }

            Assert.That(go.transform.position.x, Is.EqualTo(50f).Within(0.05f));
            Assert.That(go.transform.position.z, Is.EqualTo(0f).Within(0.05f));
            Assert.That(motor.IsMoving, Is.False);
            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrototypeArenaContainsControllablePlayerAndCameraCenteringTarget()
        {
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;

            GameObject player = GameObject.Find("Player_Exusiai");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<UnitMotor>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerCommandController>(), Is.Not.Null);
            Assert.That(player.GetComponent<CommandFeedbackPresenter>(), Is.Not.Null);
            CombatUnit playerUnit = player.GetComponent<CombatUnit>();
            Assert.That(playerUnit, Is.Not.Null);
            Assert.That(playerUnit.Team, Is.EqualTo(TeamId.Blue));
            Assert.That(playerUnit.MaxHealth, Is.EqualTo(1000f));
            Assert.That(playerUnit.CurrentHealth, Is.EqualTo(1000f));
            Assert.That(playerUnit.IsDead, Is.False);
            Assert.That(player.GetComponent<CombatCommandResolver>(), Is.Not.Null);
            Assert.That(player.GetComponent<BasicAttackController>(), Is.Not.Null);
            Assert.That(UnityEngine.Camera.main.GetComponent<MobaCameraController>().CenteringTarget,
                Is.EqualTo(player.transform));
        }

        [UnityTest]
        public IEnumerator PrototypeArenaContainsRedGroundOperator()
        {
            DefaultCharacterSelection.LoadScene("PrototypeArena");
            yield return null;

            GameObject redOperator = GameObject.Find("Red_Exusiai");
            Assert.That(redOperator, Is.Not.Null);
            Assert.That(GameObject.Find("TrainingDummy_Red"), Is.Null);
            CombatUnit unit = redOperator.GetComponent<CombatUnit>();
            Assert.That(unit, Is.Not.Null);
            Assert.That(unit.Team, Is.EqualTo(TeamId.Red));
            Assert.That(unit.Altitude, Is.EqualTo(Altitude.Ground));
            Assert.That(unit.MaxHealth, Is.EqualTo(1000f));
            Assert.That(unit.CurrentHealth, Is.EqualTo(1000f));
            Assert.That(unit.IsDead, Is.False);
            Assert.That(redOperator.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(redOperator.layer, Is.EqualTo(LayerMask.NameToLayer("Targetable")));
            Assert.That(redOperator.GetComponent<OperatorIdentity>().OperatorType, Is.EqualTo(OperatorType.Exusiai));
            Assert.That(redOperator.GetComponent<SimpleOperatorAiController>(), Is.Not.Null);
            Assert.That(redOperator.GetComponent<ExusiaiSkillController>(), Is.Null);
        }
    }
}
