using UnityEditor;
using UnityEngine;

namespace TheCall.Editor
{
    [CustomEditor(typeof(MonsterPortrait))]
    public sealed class MonsterPortraitEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "成品模板画像。性格决定固定眼位内的眼型，心情改变动作，技能不改变造型。预览模板 -1 使用种子，否则填写 0–11；色卡 0–5，头饰 0–3，背饰 0–2。非法预览饰品使用无饰品版本。正式外观在出生时确定。",
                MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
