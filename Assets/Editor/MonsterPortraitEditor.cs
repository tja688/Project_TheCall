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
                "这个矩形里画的是游戏运行时那套拼接怪物。移动、缩放或改宽高，游戏里的怪物和拖拽松手后的位置都会落在同一个矩形里。换下面的预览种子可以看另一只怪物。不要去拖里面的零件图，那些图在游戏里不显示。",
                MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
