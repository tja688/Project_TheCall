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
                "这个矩形承载规则组合的怪物部件。性格决定眼型，技能添加身体改造，心情改变动作强度。移动、缩放或改宽高，游戏中的画像和拖拽返回位置会一起跟随。外观由怪物数据生成，请在预览种子和部件目录中检查组合。",
                MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
