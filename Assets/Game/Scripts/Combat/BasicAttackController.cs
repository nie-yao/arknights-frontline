using System;
using UnityEngine;

namespace ArknightsFrontline.Combat
{
    public sealed class BasicAttackController : MonoBehaviour
    {
        private CombatUnit owner;
        private CombatUnit target;
        private float elapsedSinceAttack;
        private bool hasRequestedFirstAttack;
        private AttackSequenceExecutor executor;
        private AttackSequencePlan activePlan;
        private Func<AttackSequencePlan> planProvider;
        private bool requestingSequence;
        private int sequenceInterruptionVersion;

        public CombatUnit CurrentTarget => target;
        public bool UsesExternalDamage { get; set; }
        public bool IsOwnedSequenceRunning => activePlan != null && executor != null && executor.IsRunning;
        public int SequenceInterruptionVersion => sequenceInterruptionVersion;

        public event Action<CombatUnit, CombatUnit> AttackRequested;

        public bool IsConfiguredFor(
            CombatUnit combatOwner,
            AttackSequenceExecutor sequenceExecutor)
        {
            return combatOwner != null
                && sequenceExecutor != null
                && owner == combatOwner
                && executor == sequenceExecutor;
        }

        private void Awake()
        {
            AttackRequested += SpawnProjectile;
            CombatUnit combatUnit = GetComponent<CombatUnit>();
            if (combatUnit != null)
            {
                Configure(combatUnit);
            }
        }

        private void OnDestroy()
        {
            ClearTarget();
            DetachExecutor();
            AttackRequested -= SpawnProjectile;
            if (owner != null)
            {
                owner.Died -= OnOwnerDied;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Configure(CombatUnit combatOwner)
        {
            if (combatOwner == null)
            {
                throw new ArgumentNullException(nameof(combatOwner));
            }

            if (owner != combatOwner)
            {
                if (owner != null)
                {
                    owner.Died -= OnOwnerDied;
                }

                owner = combatOwner;
                owner.Died += OnOwnerDied;
            }

            if (owner.IsDead)
            {
                ClearTarget();
            }
        }

        public void Configure(CombatUnit combatOwner, AttackSequenceExecutor sequenceExecutor)
        {
            ClearTarget();
            DetachExecutor();
            Configure(combatOwner);
            executor = sequenceExecutor;
            if (executor != null)
            {
                executor.SequenceFinished += OnSequenceFinished;
                executor.ShotRequested += OnSequenceShot;
            }
        }

        public void SetPlanProvider(Func<AttackSequencePlan> provider)
        {
            planProvider = provider;
        }

        public void SetTarget(CombatUnit combatTarget)
        {
            if (target == combatTarget)
            {
                return;
            }

            ClearTarget();
            target = combatTarget;
            elapsedSinceAttack = 0f;
            hasRequestedFirstAttack = false;
        }

        public void ClearTarget()
        {
            bool cancelOwnedSequence = activePlan != null;
            activePlan = null;
            target = null;
            elapsedSinceAttack = 0f;
            hasRequestedFirstAttack = false;
            if (cancelOwnedSequence && executor != null) executor.Cancel();
        }

        public void Tick(float deltaTime)
        {
            if (executor != null)
            {
                TickSequence(deltaTime);
                return;
            }

            if (!HasLegalTargetInRange())
            {
                ClearTarget();
                return;
            }

            if (!hasRequestedFirstAttack)
            {
                hasRequestedFirstAttack = true;
                RequestAttack();
                return;
            }

            float interval = owner.AttackInterval;
            if (interval <= 0f)
            {
                RequestAttack();
                return;
            }

            elapsedSinceAttack += Mathf.Max(0f, deltaTime);
            if (elapsedSinceAttack < interval)
            {
                return;
            }

            elapsedSinceAttack = 0f;
            RequestAttack();
        }

        private void TickSequence(float deltaTime)
        {
            if (requestingSequence) return;
            if (hasRequestedFirstAttack) elapsedSinceAttack += Mathf.Max(0f, deltaTime);
            if (executor.IsRunning) return;
            if (!HasLegalTargetInRange())
            {
                ClearTarget();
                return;
            }

            if (hasRequestedFirstAttack && elapsedSinceAttack + 0.0000001d < owner.AttackInterval) return;
            requestingSequence = true;
            try
            {
                AttackSequencePlan nextPlan = planProvider?.Invoke() ?? new AttackSequencePlan(
                    AttackSequenceKind.Basic, 1, 0.05f, owner.AttackPower, 1f, 0f, 1f, 0f, true, false);
                CombatUnit initialTarget = target;
                activePlan = nextPlan;
                elapsedSinceAttack = 0f;
                hasRequestedFirstAttack = true;
                if (!executor.TryStart(nextPlan, initialTarget))
                {
                    ClearTarget();
                    return;
                }

                AttackRequested?.Invoke(owner, initialTarget);
                if (!executor.IsRunning && !HasLegalTargetInRange()) ClearTarget();
            }
            finally
            {
                requestingSequence = false;
            }
        }

        private void OnSequenceFinished(AttackSequencePlan finished, bool completed)
        {
            if (activePlan != finished) return;
            activePlan = null;
            if (!completed)
            {
                sequenceInterruptionVersion++;
                ClearTarget();
            }
        }

        private void OnSequenceShot(CombatUnit shotTarget, PhysicalDamagePayload _)
        {
            if (activePlan != null) target = shotTarget;
        }

        private void DetachExecutor()
        {
            if (executor == null) return;
            executor.SequenceFinished -= OnSequenceFinished;
            executor.ShotRequested -= OnSequenceShot;
            executor = null;
        }

        private bool RequestAttack()
        {
            AttackRequested?.Invoke(owner, target);
            if (HasLegalTargetInRange())
            {
                return true;
            }

            ClearTarget();
            return false;
        }

        private bool HasLegalTargetInRange()
        {
            if (!TargetRules.IsLegal(owner, target))
            {
                return false;
            }

            Vector3 ownerPosition = owner.transform.position;
            Vector3 targetPosition = target.transform.position;
            float horizontalDistance = Vector2.Distance(
                new Vector2(ownerPosition.x, ownerPosition.z),
                new Vector2(targetPosition.x, targetPosition.z));
            return horizontalDistance <= owner.AttackRange;
        }

        private void OnOwnerDied(CombatUnit _)
        {
            ClearTarget();
        }

        private void SpawnProjectile(CombatUnit attacker, CombatUnit attackTarget)
        {
            if (!Application.isPlaying || executor != null || UsesExternalDamage)
            {
                return;
            }

            GameObject projectileObject = new GameObject("Projectile");
            Projectile projectile = projectileObject.AddComponent<Projectile>();
            projectile.Initialize(attacker, attackTarget, attacker.AttackPower, 16f);
        }
    }
}
