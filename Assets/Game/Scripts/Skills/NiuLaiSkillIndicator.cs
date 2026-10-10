using UnityEngine;
using ArknightsFrontline.Commands;

namespace ArknightsFrontline.Skills
{
    [DefaultExecutionOrder(100)]
    public sealed class NiuLaiSkillIndicator : MonoBehaviour
    {
        private NiuLaiSkillController skills;
        private PlayerCommandController commands;
        private LineRenderer range, area, path;
        private Material material;
        public bool IsVisible => range && range.enabled;
        public bool IsValid { get; private set; }
        public Vector3 DisplayedEndpoint { get; private set; }
        private void Awake()
        {
            skills = GetComponent<NiuLaiSkillController>(); commands = GetComponent<PlayerCommandController>();
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            range = Create("SkillRange", 65); area = Create("SkillLandingArea", 65); path = Create("SkillPath", 2);
            Hide();
        }
        private LineRenderer Create(string name, int count)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>(); line.useWorldSpace = true;
            line.positionCount = count; line.startWidth = line.endWidth = 0.06f;
            line.sharedMaterial = material; line.loop = count > 2; return line;
        }
        private void Update()
        {
            bool hit = commands.TryGetPointerGroundPoint(out Vector3 point);
            RefreshPreview(point, hit);
        }
        public void RefreshPreview(Vector3 point, bool hasPoint)
        {
            Hide();
            if (!skills.CanShowIndicators || skills.TargetingSkill == 0) return;
            Ring(range, transform.position, skills.TargetingSkill == 2 ? NiuLaiSkillController.FlightRange : NiuLaiSkillController.MamaRange);
            material.color = new Color(0.2f, 0.8f, 1);
            if (!hasPoint) return;
            IsValid = skills.TryPreviewTarget(point, out Vector3 endpoint); DisplayedEndpoint = endpoint;
            material.color = IsValid ? new Color(0.2f, 0.8f, 1) : new Color(1, 0.2f, 0.15f);
            Ring(area, endpoint, skills.TargetingSkill == 2 ? 0.3f : NiuLaiSkillController.MamaRadius);
            path.enabled = true; path.SetPosition(0, Ground(transform.position)); path.SetPosition(1, Ground(endpoint));
        }
        private static Vector3 Ground(Vector3 point) => new Vector3(point.x, 0.06f, point.z);
        private static void Ring(LineRenderer line, Vector3 center, float radius)
        {
            line.enabled = true;
            for (int i = 0; i < 65; i++) { float angle = i * Mathf.PI * 2 / 64; line.SetPosition(i, Ground(center) + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius); }
        }
        private void Hide() { IsValid = false; if (range) range.enabled = area.enabled = path.enabled = false; }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (material) Destroy(material); }
    }
}
