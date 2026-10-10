using ArknightsFrontline.Combat;
using ArknightsFrontline.Skills;
using UnityEngine;

namespace ArknightsFrontline.Commands
{
    public sealed class CommandFeedbackPresenter : MonoBehaviour
    {
        private const int RangeRingPointCount = 65;
        private const float RangeRingHeight = 0.05f;

        private CombatUnit combatUnit;
        private PlayerCommandController commandController;
        private GameObject rangeRingObject;
        private LineRenderer rangeRing;
        private Texture2D circleTexture;
        private bool isHoverCursorActive;
        private ExusiaiSkillController skillController;
        private NiuLaiSkillController niuLaiSkills;

        public bool IsAttackRangeVisible { get; private set; }

        public bool IsHoveringLegalTarget { get; private set; }

        public float RangeRingRadius => combatUnit == null ? 0f : combatUnit.AttackRange;

        private void Awake()
        {
            combatUnit = GetComponent<CombatUnit>();
            commandController = GetComponent<PlayerCommandController>();
            niuLaiSkills = GetComponent<NiuLaiSkillController>();
            CreateRangeRing();
            circleTexture = CreateCircleTexture();
        }

        public void ConfigureSkillController(ExusiaiSkillController controller)
        {
            skillController = controller;
        }

        private void Update()
        {
            UpdateRangeRing();
            UpdateHoverFeedback();
        }

        private void OnDisable()
        {
            IsAttackRangeVisible = false;
            IsHoveringLegalTarget = false;
            if (rangeRing != null)
            {
                rangeRing.enabled = false;
            }

            if (isHoverCursorActive)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                isHoverCursorActive = false;
            }
        }

        private void OnDestroy()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (circleTexture != null)
            {
                Destroy(circleTexture);
            }

            if (rangeRingObject != null)
            {
                Destroy(rangeRingObject);
            }
        }

        private void CreateRangeRing()
        {
            rangeRingObject = new GameObject("AttackRangeRing");
            rangeRing = rangeRingObject.AddComponent<LineRenderer>();
            rangeRing.positionCount = RangeRingPointCount;
            rangeRing.loop = true;
            rangeRing.useWorldSpace = true;
            rangeRing.startWidth = 0.06f;
            rangeRing.endWidth = 0.06f;
            rangeRing.startColor = Color.yellow;
            rangeRing.endColor = Color.yellow;
            rangeRing.enabled = false;
        }

        private void UpdateRangeRing()
        {
            bool blockedBySkill = skillController != null
                && (skillController.IsSelectingChargeTarget
                    || skillController.IsDashWindowOpen
                    || skillController.BlocksNormalCommands);
            IsAttackRangeVisible = commandController != null
                && commandController.IsAttackMoveHeld
                && !blockedBySkill
                && (niuLaiSkills == null || (niuLaiSkills.CanShowIndicators && !niuLaiSkills.BlocksAttackMove));
            rangeRing.enabled = IsAttackRangeVisible;
            if (!IsAttackRangeVisible)
            {
                return;
            }

            Vector3 center = transform.position;
            center.y = RangeRingHeight;
            for (int pointIndex = 0; pointIndex < RangeRingPointCount; pointIndex++)
            {
                float angle = pointIndex * Mathf.PI * 2f / (RangeRingPointCount - 1);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * RangeRingRadius;
                rangeRing.SetPosition(pointIndex, center + offset);
            }
        }

        private void UpdateHoverFeedback()
        {
            bool isLegalTarget = false;
            if (commandController != null
                && commandController.TryGetPointerHit(out RaycastHit hit)
                && hit.collider.gameObject.layer == LayerMask.NameToLayer("Targetable")
                && hit.collider.TryGetComponent(out CombatUnit target))
            {
                isLegalTarget = TargetRules.IsLegal(combatUnit, target);
            }

            IsHoveringLegalTarget = isLegalTarget;
            if (isHoverCursorActive == isLegalTarget)
            {
                return;
            }

            isHoverCursorActive = isLegalTarget;
            Cursor.SetCursor(
                isLegalTarget ? circleTexture : null,
                isLegalTarget ? new Vector2(12f, 12f) : Vector2.zero,
                CursorMode.Auto);
        }

        private static Texture2D CreateCircleTexture()
        {
            const int textureSize = 24;
            const float radius = 9f;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.name = "LegalTargetCursor";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(11.5f, 11.5f);
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, distance >= radius - 1f && distance < radius + 1f
                        ? Color.yellow
                        : Color.clear);
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
