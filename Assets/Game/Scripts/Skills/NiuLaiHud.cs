using UnityEngine;

namespace ArknightsFrontline.Skills
{
    public sealed class NiuLaiHud : MonoBehaviour
    {
        private NiuLaiSkillController skills;
        private Font font;
        private void Awake() { skills = GetComponent<NiuLaiSkillController>(); font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 18); }
        private void OnGUI()
        {
            if (!skills || !skills.isActiveAndEnabled || GetComponent<ArknightsFrontline.Combat.CombatUnit>().IsDead) return;
            var style = new GUIStyle(GUI.skin.box) { font = font, fontSize = 18 };
            string w = skills.IsEmpowered ? "已强化" : Format(skills.WCooldown);
            float left = Screen.width / 2f - 310;
            GUI.Box(new Rect(left, Screen.height - 86, 200, 58), $"W 牛劲十足\n{w}", style);
            GUI.Box(new Rect(left + 210, Screen.height - 86, 200, 58), $"E 中国牛能飞\n{Format(skills.ECooldown)}", style);
            GUI.Box(new Rect(left + 420, Screen.height - 86, 200, 58), $"R 妈妈\n{Format(skills.RCooldown)}", style);
            GUI.Label(new Rect(Screen.width / 2f - 240, Screen.height - 115, 480, 25), "E / R 后左键选点，Esc 取消；B 撤退", new GUIStyle(GUI.skin.label) { font = font, alignment = TextAnchor.MiddleCenter });
        }
        private static string Format(float value) => value <= 0 ? "就绪" : value.ToString("0.0") + "s";
        private void OnDestroy() { if (font) Destroy(font); }
    }
}
