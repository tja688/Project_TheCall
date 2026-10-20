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
                "这个矩形里目前只画整张剪影。旧拼接零件已删除。移动、缩放或改宽高，游戏里的怪物和拖拽松手后的位置都会落在同一个矩形里。新怪物系统见 Assets/Notes/新怪物系统交接.md。",
                MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
