using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    public static class LabCageRefinement
    {
        const string Art = "Assets/Arts/Lab/Cage/";

        [MenuItem("TheCall/Art/精修收容笼升降与遮罩")]
        public static void FromMenu() => Apply();

        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring cages.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity") throw new InvalidOperationException("Open Main first.");
            Directory.CreateDirectory("Temp/CageArt/rollback");
            if (!File.Exists("Temp/CageArt/rollback/Main.unity")) File.Copy(scene.path, "Temp/CageArt/rollback/Main.unity");
            Author(Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "PASS saved individual painted cages, masked aperture and smooth row lift";
        }

        public static void Author(OperationScreenView op)
        {
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(Art, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var old = op.lab.cageScroll;
            var content = old.content;
            var viewport = old.viewport;
            var node = old.gameObject;
            if (old.verticalScrollbar != null) Object.DestroyImmediate(old.verticalScrollbar.gameObject);
            old.verticalScrollbar = null;
            old.horizontalScrollbar = null;
            var lift = old as LabCageLift;
            if (lift == null)
            {
                Object.DestroyImmediate(old);
                lift = node.AddComponent<LabCageLift>();
            }
            lift.content = content;
            lift.viewport = viewport;
            lift.horizontal = false;
            lift.vertical = true;
            lift.inertia = false;
            lift.movementType = ScrollRect.MovementType.Clamped;
            lift.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            lift.rowBacks = new[] { Sprite("RowBack0"), Sprite("RowBack1") };
            lift.rowFronts = new[] { Sprite("RowFront0"), Sprite("RowFront1") };
            op.lab.cageScroll = lift;
            LabSceneView.Place((RectTransform)node.transform, 1538, 158, 264, 280);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            var mask = viewport.GetComponent<Mask>();
            if (mask == null) mask = viewport.gameObject.AddComponent<Mask>();
            mask.enabled = true;
            mask.showMaskGraphic = false;
            var aperture = viewport.GetComponent<Image>();
            aperture.color = Color.white;
            aperture.sprite = null;
            aperture.raycastTarget = true;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 280);
            op.cageColumns = 2;
            op.cageRowSpacing = LabCageLift.RowHeight;
            op.cagePadding = 0;
            for (var i = 0; i < op.cageSlots.Length; i++)
            {
                var slot = op.cageSlots[i];
                LabSceneView.Place((RectTransform)slot.transform, i % 2 * 132, i / 2 * 140, 132, 140);
                var clip = slot.GetComponent<RectMask2D>();
                if (clip == null) clip = slot.gameObject.AddComponent<RectMask2D>();
                clip.padding = new Vector4(7, 10, 7, 9);
                LabSceneView.Place((RectTransform)slot.portrait.transform, 14, 19, 104, 110);
                var shadow = Picture(slot.transform, "LabCageFootShadow", null, 27, 111, 78, 13);
                shadow.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                shadow.color = new Color(0, 0.015f, 0.01f, 0.5f);
                shadow.transform.SetAsFirstSibling();
                if (slot.selection != null)
                    LabSceneView.Place((RectTransform)slot.selection.transform, 9, 10, 114, 121);
                EditorUtility.SetDirty(slot);
            }
            var root = op.transform;
            var rim = Picture(root, "LabCageRim", "CabinetRim", 1526, 138, 288, 324);
            rim.transform.SetSiblingIndex(node.transform.GetSiblingIndex() + 1);
            var top = Picture(viewport, "LabCageTopShadow", "EdgeShade", 0, 0, 264, 40);
            top.color = new Color(0.015f, 0.035f, 0.03f, 0.9f);
            var bottom = Picture(viewport, "LabCageBottomShadow", "EdgeShade", 0, 240, 264, 40);
            bottom.color = top.color;
            bottom.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            bottom.rectTransform.anchoredPosition = new Vector2(132, -260);
            bottom.rectTransform.localEulerAngles = new Vector3(0, 0, 180);
            var caption = (RectTransform)op.lab.cageCount.transform.parent;
            LabSceneView.Place(caption, 1546, 119, 246, 30);
            caption.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.045f, 0.45f);
            LabSceneView.Place(op.lab.cageCount.rectTransform, 6, 0, 234, 30);
            op.lab.cageCount.fontSize = 17;
            // Move the existing controls and persistent listeners next to the
            // cabinet. Retire the painted arrows in the unrelated gene panel.
            lift.previousButton = root.Find("LabPreviousCagePage").GetComponent<Button>();
            lift.nextButton = root.Find("LabNextCagePage").GetComponent<Button>();
            Navigation(lift.previousButton, "ArrowUp", 1764, 118);
            Navigation(lift.nextButton, "ArrowDown", 1764, 448);
            Picture(root, "LabRetiredCageArrows", "OldNavigationPanel", 1538, 872, 264, 64);
            var oldCaption = root.Find("LabCagePageCaption");
            if (oldCaption != null) oldCaption.gameObject.SetActive(false);
            var range = Node(root, "LabCageRange");
            LabSceneView.Place(range, 1544, 449, 218, 28);
            var label = range.GetComponent<TextMeshProUGUI>();
            if (label == null) label = range.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = op.lab.cageCount.font;
            label.fontSize = 16;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(157, 190, 170, 255);
            label.raycastTarget = false;
            label.text = "滚轮 · 上下切换";
            lift.rangeLabel = label;
            op.dragLayer.SetAsLastSibling();
            EditorUtility.SetDirty(lift);
            EditorUtility.SetDirty(op.lab);
            EditorUtility.SetDirty(op);
        }

        static void Navigation(Button button, string sprite, float x, float y)
        {
            LabSceneView.Place((RectTransform)button.transform, x, y, 32, 30);
            var image = button.GetComponent<Image>();
            image.sprite = Sprite(sprite);
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = true;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = new Color(0.7f, 0.9f, 0.78f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.4f, 0.65f, 0.5f);
            colors.disabledColor = new Color(0.35f, 0.45f, 0.38f, 0.3f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            EditorUtility.SetDirty(button);
        }

        static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
        static RectTransform Node(Transform parent, string name)
        {
            var node = parent.Find(name) as RectTransform;
            if (node != null) return node;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        static Image Picture(Transform parent, string name, string sprite, float x, float y, float w, float h)
        {
            var rect = Node(parent, name);
            LabSceneView.Place(rect, x, y, w, h);
            var image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite == null ? null : Sprite(sprite);
            image.color = Color.white;
            image.raycastTarget = false;
            image.enabled = true;
            return image;
        }
    }
}
