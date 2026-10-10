using ArknightsFrontline.Arena;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using UnityEngine;

namespace ArknightsFrontline.Skills
{
    public sealed class NiuLaiMamaImpact : MonoBehaviour
    {
        public const float FallDuration = 1.8f;
        private CombatUnit caster;
        private TeamId team;
        private Vector3 point;
        private float attack, elapsed;
        private MatchOutcomeController match;
        private Transform fallingModel;
        private Material warningMaterial;
        private bool resolved;
        public void Initialize(CombatUnit owner, Vector3 destination, float power, Transform model)
        {
            caster = owner; team = owner.Team; point = destination; attack = power;
            match = FindFirstObjectByType<MatchOutcomeController>();
            transform.position = point;
            var warning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            warning.name = "ImpactWarning";
            warning.transform.SetParent(transform, false);
            warning.transform.localPosition = Vector3.up * 0.025f;
            warning.transform.localScale = new Vector3(5.6f, 0.015f, 5.6f);
            Destroy(warning.GetComponent<Collider>());
            warningMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            warningMaterial.color = new Color(1, 0.35f, 0.1f);
            warning.GetComponent<Renderer>().sharedMaterial = warningMaterial;
            if (model)
            {
                fallingModel = Instantiate(model.gameObject, transform).transform;
                foreach (var animator in fallingModel.GetComponentsInChildren<Animator>()) animator.enabled = false;
                var mamaMaterial = Resources.Load<Material>("NiuLaiMama");
                if (mamaMaterial) foreach (var renderer in fallingModel.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = mamaMaterial;
                fallingModel.localScale = model.lossyScale * 1.5f;
                fallingModel.localPosition = Vector3.up * 8;
            }
        }
        private void Update()
        {
            Tick(Time.deltaTime);
        }
        public void Tick(float deltaTime)
        {
            if (resolved) return;
            if (match && match.IsEnding) { Destroy(gameObject); return; }
            elapsed += Mathf.Max(0, deltaTime);
            if (fallingModel) fallingModel.localPosition = Vector3.up * Mathf.Lerp(8, 0, Mathf.Clamp01(elapsed / FallDuration));
            if (elapsed < FallDuration) return;
            resolved = true;
            foreach (CombatUnit target in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            {
                if (target.IsDead || target.Team == team || target.Altitude != Altitude.Ground) continue;
                Vector3 offset = target.transform.position - point; offset.y = 0;
                if (offset.magnitude > 2.8f) continue;
                Vector3 origin = point + Vector3.up * 0.5f;
                bool blocked = false;
                Vector3 path = target.transform.position - origin;
                foreach (RaycastHit hit in Physics.RaycastAll(origin, path.normalized, path.magnitude))
                {
                    if (hit.collider.transform.IsChildOf(target.transform)) continue;
                    if (hit.collider.gameObject.layer == ExusiaiSkillController.ReservedObstacleLayerIndex
                        || hit.collider.GetComponentInParent<TowerCombatController>()) { blocked = true; break; }
                }
                if (blocked) continue;
                float scale = target.GetComponent<TowerCombatController>() ? 0.5f : 1f;
                target.TakePhysicalDamage(Mathf.Max(1, attack * 4.5f * scale - target.Defense), caster);
            }
            Destroy(gameObject);
        }
        private void OnDestroy() { if (warningMaterial) Destroy(warningMaterial); }
    }
}
