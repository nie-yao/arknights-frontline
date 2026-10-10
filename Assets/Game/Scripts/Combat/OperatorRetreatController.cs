using System;
using ArknightsFrontline.Arena;
using ArknightsFrontline.Commands;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    [DisallowMultipleComponent]
    public sealed class OperatorRetreatController : MonoBehaviour
    {
        public const float GuidanceDuration = 1.5f;

        private CombatUnit owner;
        private UnitMotor motor;
        private PlayerCommandController commands;
        private BasicAttackController attacks;
        private AttackSequenceExecutor sequence;
        private ExusiaiSkillController skills;
        private SkillDashController dash;
        private MatchOutcomeController match;
        private bool wasAttackEnabled;
        private bool wasSequenceEnabled;

        public bool IsGuiding { get; private set; }

        public bool IsStopped { get; private set; }

        public float RemainingSeconds { get; private set; }

        public event Action<CombatUnit> GuidanceStarted;

        public event Action<CombatUnit> GuidanceInterrupted;

        public event Action<CombatUnit> GuidanceCompleted;

        private void Awake()
        {
            Configure();
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            UnsubscribeFromOwner();
            if (match != null) match.MatchEnding -= OnMatchEnding;
        }

        public void Configure(MatchOutcomeController match = null)
        {
            if (this.match == null && match != null) BindMatch(match);

            CombatUnit resolvedOwner = GetComponent<CombatUnit>();
            if (owner != resolvedOwner)
            {
                UnsubscribeFromOwner();
                owner = resolvedOwner;
                SubscribeToOwner();
            }

            motor = GetComponent<UnitMotor>();
            commands = GetComponent<PlayerCommandController>();
            attacks = GetComponent<BasicAttackController>();
            sequence = GetComponent<AttackSequenceExecutor>();
            skills = GetComponent<ExusiaiSkillController>();
            dash = GetComponent<SkillDashController>();

            if (this.match == null)
            {
                BindMatch(FindFirstObjectByType<MatchOutcomeController>());
            }
        }

        public bool TryBegin()
        {
            Configure();
            if (!CanBegin() || dash != null && dash.IsDashing
                || GetComponent<NiuLaiSkillController>() is NiuLaiSkillController cow && cow.IsFlying)
            {
                return false;
            }

            IsGuiding = true;
            RemainingSeconds = GuidanceDuration;
            wasAttackEnabled = attacks != null && attacks.enabled;
            wasSequenceEnabled = sequence != null && sequence.enabled;

            if (commands != null)
            {
                commands.CancelForRetreat();
            }
            else
            {
                motor?.Stop();
            }

            attacks?.ClearTarget();
            sequence?.Cancel();
            skills?.CancelPendingTargetingForRetreat();

            if (attacks != null) attacks.enabled = false;
            if (sequence != null) sequence.enabled = false;

            GuidanceStarted?.Invoke(owner);
            return true;
        }

        public void StopForMatch()
        {
            if (IsStopped)
            {
                return;
            }

            IsStopped = true;
            EndGuidance(true, false);
        }

        public void Tick(float deltaTime)
        {
            if (!IsGuiding)
            {
                return;
            }

            if (owner == null || owner.IsDead || IsMatchEnding())
            {
                EndGuidance(true, false);
                return;
            }

            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Mathf.Max(0f, deltaTime));
            if (RemainingSeconds > 0.0001f)
            {
                return;
            }

            RemainingSeconds = 0f;
            CompleteGuidance();
        }

        private bool CanBegin()
        {
            return isActiveAndEnabled
                && !IsStopped
                && owner != null
                && !owner.IsDead
                && !IsGuiding
                && !IsMatchEnding();
        }

        private bool IsMatchEnding()
        {
            if (match == null)
            {
                BindMatch(FindFirstObjectByType<MatchOutcomeController>());
            }

            return match != null && match.IsEnding;
        }

        private void CompleteGuidance()
        {
            if (!IsGuiding)
            {
                return;
            }

            CombatUnit retreatingOperator = owner;
            IsGuiding = false;
            RemainingSeconds = 0f;
            if (retreatingOperator == null || retreatingOperator.IsDead || IsMatchEnding())
            {
                GuidanceInterrupted?.Invoke(retreatingOperator);
                return;
            }

            GuidanceCompleted?.Invoke(retreatingOperator);
        }

        private void OnDamageTaken(CombatUnit attacker, float actualDamage)
        {
            if (!IsGuiding || owner == null)
            {
                return;
            }

            if (owner.IsDead)
            {
                EndGuidance(true, false);
                return;
            }

            if (actualDamage > 0f && IsEnemyOperatorOrTower(attacker))
            {
                EndGuidance(true, !IsMatchEnding());
            }
        }

        private bool IsEnemyOperatorOrTower(CombatUnit attacker)
        {
            if (attacker == null || attacker.Team == owner.Team)
            {
                return false;
            }

            return attacker.GetComponent<OperatorIdentity>() != null
                || attacker.GetComponent<TowerCombatController>() != null;
        }

        private void OnOwnerDied(CombatUnit _)
        {
            EndGuidance(true, false);
        }

        private void OnMatchEnding()
        {
            StopForMatch();
        }

        private void BindMatch(MatchOutcomeController matchController)
        {
            if (match == matchController)
            {
                return;
            }

            if (match != null) match.MatchEnding -= OnMatchEnding;
            match = matchController;
            if (match != null) match.MatchEnding += OnMatchEnding;
        }

        private void EndGuidance(bool interrupted, bool restoreCombat)
        {
            if (!IsGuiding)
            {
                return;
            }

            IsGuiding = false;
            RemainingSeconds = 0f;
            if (restoreCombat && owner != null && !owner.IsDead && !IsMatchEnding())
            {
                if (attacks != null) attacks.enabled = wasAttackEnabled;
                if (sequence != null) sequence.enabled = wasSequenceEnabled;
            }

            if (interrupted)
            {
                GuidanceInterrupted?.Invoke(owner);
            }
        }

        private void SubscribeToOwner()
        {
            if (owner == null)
            {
                return;
            }

            owner.DamageTaken -= OnDamageTaken;
            owner.DamageTaken += OnDamageTaken;
            owner.Died -= OnOwnerDied;
            owner.Died += OnOwnerDied;
        }

        private void UnsubscribeFromOwner()
        {
            if (owner == null)
            {
                return;
            }

            owner.DamageTaken -= OnDamageTaken;
            owner.Died -= OnOwnerDied;
        }
    }
}
