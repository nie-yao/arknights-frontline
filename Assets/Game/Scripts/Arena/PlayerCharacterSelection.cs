using System;
using UnityEngine;

namespace ArknightsFrontline.Arena
{
    // Only attached to the playable arena. Code-built test arenas retain their old startup path.
    public sealed class PlayerCharacterSelection : MonoBehaviour
    {
        private Action<GameObject> beginMatch;
        private GameObject cowTemplate;
        private float previousTimeScale;
        private bool pending;
        private int selected;
        private Font font;
        public bool IsPending => pending;

        public void Configure(Action<GameObject> start)
        {
            beginMatch = start;
            cowTemplate = Resources.Load<GameObject>("NiuLaiPlayer");
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            pending = true;
            font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 20);
        }

        public bool ConfirmSelection(bool niuLai)
        {
            if (!pending || niuLai && cowTemplate == null) return false;
            pending = false;
            Time.timeScale = previousTimeScale;
            beginMatch?.Invoke(niuLai ? cowTemplate : null);
            return true;
        }

        private void OnGUI()
        {
            if (!pending) return;
            GUIStyle title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 22, alignment = TextAnchor.MiddleCenter };
            GUIStyle button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18 };
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 220, Screen.height / 2f - 140, 440, 280), GUI.skin.box);
            GUILayout.Space(16);
            GUILayout.Label("选择本局角色", title);
            GUILayout.Space(20);
            if (GUILayout.Button((selected == 0 ? "✓ " : "") + "能天使 · 远程连射", button, GUILayout.Height(44))) selected = 0;
            GUI.enabled = cowTemplate != null;
            if (GUILayout.Button((selected == 1 ? "✓ " : "") + "牛来 · 越障突进（原型）", button, GUILayout.Height(44))) selected = 1;
            GUI.enabled = true;
            GUILayout.Space(12);
            GUILayout.Label("本局角色固定，死亡和撤退后仍部署同一角色。", new GUIStyle(GUI.skin.label) { font = font, alignment = TextAnchor.MiddleCenter });
            if (GUILayout.Button("开始游戏", button, GUILayout.Height(40))) ConfirmSelection(selected == 1);
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (pending) Time.timeScale = previousTimeScale;
            if (font) Destroy(font);
        }
    }
}
