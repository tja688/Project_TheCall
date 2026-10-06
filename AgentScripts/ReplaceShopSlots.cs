using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

internal static class ReplaceShopSlots
{
    static readonly (string slotName, string prefabPath, int catalogIndex)[] Replacements =
    {
        ("Slot0", "Assets/Prefabs/MonsterSlotTemplate.prefab", 0),
        ("Slot1", "Assets/Prefabs/MonsterSlotTemplate.prefab", 1),
        ("Slot2", "Assets/Prefabs/MonsterSlotTemplate.prefab", 2),
        ("Slot3", "Assets/Prefabs/MonsterSlotTemplate.prefab", 3),
        ("Slot4", "Assets/Prefabs/MonsterSlotTemplate.prefab", 4),
        ("Slot5", "Assets/Prefabs/MonsterSlotTemplate.prefab", 5),
        ("Slot12", "Assets/Prefabs/ItemSlotTemplate.prefab", 12),
        ("Slot13", "Assets/Prefabs/ItemSlotTemplate.prefab", 13),
        ("Slot14", "Assets/Prefabs/ItemSlotTemplate.prefab", 14),
        ("Slot15", "Assets/Prefabs/ItemSlotTemplate.prefab", 15),
        ("Slot16", "Assets/Prefabs/ItemSlotTemplate.prefab", 16),
        ("Slot17", "Assets/Prefabs/ItemSlotTemplate.prefab", 17),
    };

    public static string Main()
    {
        var sb = new StringBuilder();
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return "Canvas not found";

        var window = canvas.transform.Find("Demo/Shop/Window");
        if (window == null)
            return "Canvas/Demo/Shop/Window not found";

        var panel = window.GetComponent<HearthwoodCatalogPanel>();
        var panelSo = panel != null ? new SerializedObject(panel) : null;
        var slotFramesProp = panelSo?.FindProperty("slotFrames");

        Undo.SetCurrentGroupName("Replace shop slots");
        var undoGroup = Undo.GetCurrentGroup();

        foreach (var (slotName, prefabPath, catalogIndex) in Replacements)
        {
            var oldSlot = window.Find(slotName);
            if (oldSlot == null)
            {
                sb.AppendLine("MISSING " + slotName);
                continue;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                sb.AppendLine("PREFAB MISSING " + prefabPath);
                continue;
            }

            var oldRect = oldSlot as RectTransform;
            var worldPos = oldSlot.position;
            var siblingIndex = oldSlot.GetSiblingIndex();
            var parent = oldSlot.parent;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Replace " + slotName);

            instance.name = slotName;
            var newRect = (RectTransform)instance.transform;
            if (oldRect != null)
                CopyRectTransform(oldRect, newRect);
            else
                newRect.position = worldPos;

            newRect.SetSiblingIndex(siblingIndex);

            var frameImage = instance.GetComponent<Image>();
            if (panelSo != null && slotFramesProp != null && frameImage != null
                && catalogIndex >= 0 && catalogIndex < slotFramesProp.arraySize)
            {
                slotFramesProp.GetArrayElementAtIndex(catalogIndex).objectReferenceValue = frameImage;
            }

            var button = instance.GetComponent<Button>();
            if (button != null && panel != null)
            {
                while (button.onClick.GetPersistentEventCount() > 0)
                    UnityEventTools.RemovePersistentListener(button.onClick, 0);

                UnityEventTools.AddIntPersistentListener(button.onClick, panel.Select, catalogIndex);
                EditorUtility.SetDirty(button);
            }

            Undo.DestroyObjectImmediate(oldSlot.gameObject);

            sb.AppendLine(slotName
                + " -> " + prefab.name
                + " pos=" + newRect.position.ToString()
                + " anchored=" + newRect.anchoredPosition.ToString());
        }

        if (panelSo != null)
            panelSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(window.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);

        sb.AppendLine("done");
        return sb.ToString();
    }

    static void CopyRectTransform(RectTransform source, RectTransform dest)
    {
        dest.anchorMin = source.anchorMin;
        dest.anchorMax = source.anchorMax;
        dest.pivot = source.pivot;
        dest.anchoredPosition = source.anchoredPosition;
        dest.sizeDelta = source.sizeDelta;
        dest.localRotation = source.localRotation;
        dest.localScale = source.localScale;
        dest.anchoredPosition3D = source.anchoredPosition3D;
    }
}
