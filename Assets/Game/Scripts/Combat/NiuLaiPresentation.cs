using UnityEngine;
using ArknightsFrontline.Skills;

namespace ArknightsFrontline.Combat
{
    public sealed class NiuLaiPresentation : MonoBehaviour
    {
        private Transform visual;
        private Vector3 previous;
        private CombatUnit owner;
        private BasicAttackController attacks;
        private Animator animator;
        private NiuLaiSkillController skills;
        private bool wasFlying;
        private void OnAttack(CombatUnit attacker, CombatUnit target) { if (animator && owner && !owner.IsDead && !skills.IsFlying) animator.SetTrigger("Attack"); }
        private void OnEnable() { if (attacks) attacks.AttackRequested += OnAttack; previous = transform.position; }
        private void OnDisable() { if (attacks) attacks.AttackRequested -= OnAttack; }
        private void Awake() { visual = transform.Find("NiuLaiVisual"); previous = transform.position; owner = GetComponent<CombatUnit>(); attacks = GetComponent<BasicAttackController>(); skills = GetComponent<NiuLaiSkillController>(); animator = GetComponentInChildren<Animator>(true); }
        private void LateUpdate()
        {
            Vector3 direction = transform.position - previous; previous = transform.position;
            if (animator) { animator.speed = owner.IsDead || !skills.CanShowIndicators ? 0 : 1; animator.SetBool("Flying", skills.IsFlying); animator.SetFloat("Speed", !skills.IsFlying && Time.deltaTime > 0 ? direction.magnitude / Time.deltaTime : 0); }
            if (animator && !owner.IsDead)
            {
                if (skills.IsFlying) { animator.Play("Flight", 0, skills.FlightProgress); animator.Update(0); }
                else if (wasFlying) { animator.Play("Idle", 0, 0); animator.Update(0); }
                wasFlying = skills.IsFlying;
            }
            if (!visual || owner.IsDead) return;
            if (attacks.CurrentTarget) direction = attacks.CurrentTarget.transform.position - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.00001f) visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.LookRotation(direction), 720 * Time.deltaTime);
        }
    }
}
