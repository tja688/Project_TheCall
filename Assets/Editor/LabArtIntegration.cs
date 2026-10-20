using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using Button = UnityEngine.UI.Button;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    // Re-runnable authoring operation, invoked through the running Editor's CLI eval.
    public static class LabArtIntegration
    {
        const string Art = "Assets/Arts/Lab/";
        const string Pipeline = "Assets/Settings/Lab2D_RPAsset.asset";
        static readonly Color Ink = new Color32(211, 245, 223, 255);
        static readonly Color Mint = new Color32(83, 239, 155, 255);
        static readonly Color Panel = new Color32(10, 25, 25, 235);
        static TMP_FontAsset _font;

        [MenuItem("TheCall/Art/接入实验室原画并设置 URP 2D")]
        public static void FromMenu() => Apply();

        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring the lab.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity") throw new InvalidOperationException("Open the saved Main scene first.");
            Backup();
            ImportArt();
            var operation = Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
            if (operation == null) throw new InvalidOperationException("Main has no OperationScreenView.");
            _font = operation.dayLabel.font;
            Configure2D();
            AuthorOperation(operation);
            AuthorResearch(Object.FindAnyObjectByType<ResearchScreenView>(FindObjectsInactive.Include));
            var presentation = Object.FindAnyObjectByType<TheCallPresentation>(FindObjectsInactive.Include);
            var serialized = new SerializedObject(presentation);
            serialized.FindProperty("_workspace").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Both abandoned world workspaces have been superseded by the existing semantic UI slots.
            foreach (var root in scene.GetRootGameObjects().Where(root => root.name == "WorkspaceWorld" || root.name == "DragPreview"))
                Object.DestroyImmediate(root);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.name == "UICanvas")
                    {
                        var scaler = canvas.GetComponent<CanvasScaler>();
                        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                    }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return ValidateSaved();
        }

        static void Backup()
        {
            var directory = "Temp/LabArt/rollback";
            Directory.CreateDirectory(directory);
            foreach (var path in new[] { "Assets/Scenes/Main.unity", "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset", "ProjectSettings/EditorSettings.asset" })
            {
                var target = Path.Combine(directory, Path.GetFileName(path));
                if (!File.Exists(target)) File.Copy(path, target);
            }
        }

        static void ImportArt()
        {
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(Art, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType == TextureImporterType.Sprite && importer.textureCompression == TextureImporterCompression.Uncompressed && !importer.mipmapEnabled) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        static void Configure2D()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset");
            if (renderer == null) throw new InvalidOperationException("Renderer2D.asset missing.");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, Pipeline);
            }
            var data = new SerializedObject(pipeline);
            var list = data.FindProperty("m_RendererDataList");
            list.arraySize = 1; list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            data.FindProperty("m_DefaultRendererIndex").intValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.msaaSampleCount = 1;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (var i = 0; i < levels.arraySize; i++) levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            quality.ApplyModifiedPropertiesWithoutUndo();
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.sun = null;
            Lightmapping.lightingDataAsset = null;
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                camera.orthographic = true;
                camera.orthographicSize = 5.4f;
                camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(4, 9, 8, 255);
                var extra = Component<UniversalAdditionalCameraData>(camera.gameObject);
                extra.SetRenderer(0);
                extra.renderPostProcessing = false;
                extra.requiresColorTexture = false;
                extra.requiresDepthTexture = false;
            }
            foreach (var sceneView in SceneView.sceneViews.Cast<SceneView>())
            {
                sceneView.in2DMode = true;
                var state = sceneView.sceneViewState;
                state.showSkybox = false;
            }
            var global = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(light => light.lightType == Light2D.LightType.Global);
            if (global == null) global = new GameObject("GlobalLight2D").AddComponent<Light2D>();
            global.gameObject.SetActive(true);
            global.lightType = Light2D.LightType.Global;
            global.color = Color.white; global.intensity = 1;
            EditorUtility.SetDirty(pipeline);
        }

        static void AuthorOperation(OperationScreenView op)
        {
            var root = op.transform;
            FixedScreen((RectTransform)root);
            var stage = Node(root, "LabArtStage");
            At(stage, 0, 0, 1920, 1080); stage.SetAsFirstSibling();
            var shell = Picture(stage, "LabShell", "LabShell", 0, 0, 1920, 1080);
            shell.raycastTarget = true;
            var zone = Component<OperationZone>(shell.gameObject);
            zone.Place = LandingPlace.EmptySpace;
            var lab = Component<LabSceneView>(op.gameObject);
            op.lab = lab; lab.operation = op;
            var room = Picture(stage, "Room2Interior", "Room2Interior", 836, 200, 640, 278);
            lab.room2Interior = Component<CanvasGroup>(room.gameObject);
            lab.room2Interior.alpha = 0;
            lab.room2Interior.blocksRaycasts = false;
            var lightNames = new[] { "LightBreeding1", "LightBreeding2", "LightProduction1", "LightProduction2" };
            var boxes = new[] { new Rect(-34,164,926,391), new Rect(669,151,831,391), new Rect(-80,574,930,391), new Rect(337,572,1137,384) };
            lab.lamps = new Image[4];
            for (var i = 0; i < 4; i++) lab.lamps[i] = Picture(stage, lightNames[i], lightNames[i], boxes[i].x, boxes[i].y, boxes[i].width, boxes[i].height);
            lab.collectorGlow = Picture(stage, "CollectorGlow", "CollectorGlow", 1280, 686, 158, 220);
            // The base painting includes a fixed pointer. A small UI dial face covers it;
            // the delivered separate red pointer can then report actual energy.
            Solid(stage, "CollectorDialFace", new Color32(38,66,62,255), 1244, 550, 227, 112);
            for (var i = 0; i < 9; i++)
            {
                var angle = Mathf.Lerp(160,20,i/8f) * Mathf.Deg2Rad;
                var tick = Solid(stage,"CollectorTick"+i,new Color32(126,157,138,255), 1354 + Mathf.Cos(angle)*91, 650-Mathf.Sin(angle)*84, 3, 12);
                tick.rectTransform.localEulerAngles = new Vector3(0,0,-angle*Mathf.Rad2Deg+90);
            }
            lab.needle = Picture(stage, "CollectorNeedle", "CollectorNeedle", 1360, 555, 78, 78).rectTransform;
            lab.needle.pivot = Vector2.zero; lab.needle.anchoredPosition = new Vector2(1354, -650); lab.needle.sizeDelta = new Vector2(58,58);
            lab.needle.SetAsLastSibling();
            var hub = Solid(stage,"CollectorHub",new Color32(13,31,28,255),1339,635,30,30);
            hub.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            hub.transform.SetAsLastSibling();
            Picture(stage, "BossReserved", "BossReserved", -384, 177, 1677, 1192).gameObject.SetActive(false);
            Picture(stage,"LabFrame","LabFrame",0,0,1920,1080).transform.SetAsLastSibling();
            // Existing semantic objects are moved before retiring their old painted containers.
            foreach (var slot in op.extractionSlots.Concat(op.breedingSlots)) slot.transform.SetParent(root, false);
            var scroll = op.cageSlots[0].GetComponentInParent<ScrollRect>(true);
            scroll.transform.SetParent(root, false);
            At(scroll.transform, 1546, 185, 248, 258);
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(false);
            Stretch(scroll.viewport, 0);
            var viewportImage = scroll.viewport.GetComponent<Image>();
            if (viewportImage != null) { viewportImage.color = new Color(0, 0, 0, 0.025f); viewportImage.raycastTarget = true; }
            scroll.content.anchorMin = new Vector2(0, 1); scroll.content.anchorMax = new Vector2(1, 1); scroll.content.pivot = new Vector2(0, 1);
            scroll.content.anchoredPosition = Vector2.zero; scroll.content.sizeDelta = new Vector2(0, 190);
            op.cageColumns = 2; op.cageRowSpacing = 94; op.cagePadding = 6;
            for (var i = 0; i < op.cageSlots.Length; i++)
            {
                SkinSlot(op.cageSlots[i], 4 + i % 2 * 120, 4 + i / 2 * 94, 112, 90, false);
                At(op.cageSlots[i].portrait.transform, 6, 3, 100, 84);
            }
            lab.cageScroll = scroll;
            lab.cageCount = Plaque(root, "LabCageCount", "收容笼", 1550, 140, 230, 34, 18);
            for (var i = 0; i < op.extractionSlots.Length; i++)
            {
                SkinSlot(op.extractionSlots[i], 158 + i * 172, 668, 166, 239, true);
                At(op.extractionSlots[i].portrait.transform, 3, 44, 160, 208);
                var platform = Picture(op.extractionSlots[i].transform,"LabProductionPlatform","ProductionPlatform",8,203,150,38);
                platform.color = new Color(0.72f,0.94f,0.84f,1); platform.transform.SetAsFirstSibling();
                Label(op.extractionSlots[i].transform,"LabCellIndex",(i+1).ToString("00"),16,217,40,20,14);
            }
            for (var i = 0; i < op.breedingSlots.Length; i++) SkinSlot(op.breedingSlots[i], 170 + i * 170, 254, 170, 218, true);
            lab.parentCaptions = new[] { Plaque(root, "LabBreeding1", "培育 1 / 2 亲本", 162, 202, 264, 34, 18), Plaque(root, "LabBreeding2", "培育 2 / 待解锁", 848, 202, 264, 34, 18) };
            Plaque(root, "LabProductionCaption", "提取产能区  /  按格子顺序结算", 164, 626, 510, 32, 18);
            var locked = Plaque(root, "LabRoom2Lock", "暂未开放\n科研 · 槽位扩容", 1010, 316, 300, 94, 23);
            lab.room2Lock = locked.transform.parent.gameObject;
            // The lock is a real research entry, including when the player has no science yet.
            var lockButton = Component<Button>(lab.room2Lock);
            lockButton.targetGraphic = lab.room2Lock.GetComponent<Image>();
            lab.room2Lock.GetComponent<Image>().raycastTarget = true;
            Action(lockButton, PresentationAction.OpenResearch);
            for (var i = 0; i < op.breedingSkills.Length; i++)
            {
                var chip = op.breedingSkills[i]; chip.transform.SetParent(root, false);
                SkinChip(chip, i == 0 ? 707 : 1392, 278, 78, 174, 16);
            }
            for (var i = 0; i < op.skillChips.Length; i++)
            {
                var chip = op.skillChips[i]; chip.transform.SetParent(root, false);
                SkinChip(chip, 1550 + i % 2 * 121, 727 + i / 2 * 63, 114, 55, 16);
            }
            lab.warehouseCount = Plaque(root, "LabWarehouseCount", "基因仓库 · 3/4", 1550, 698, 235, 23, 16);
            op.addGeneSlotButton.transform.SetParent(root, false);
            SkinButton(op.addGeneSlotButton, "+", 708, 166, 78, 70, 32);
            op.discardButton.transform.SetParent(root, false);
            At(op.discardButton.transform, 1540, 580, 258, 104);
            op.discardButton.GetComponent<Image>().color = Color.clear;
            op.discardButton.GetComponent<Image>().sprite = null;
            foreach (Transform child in op.discardButton.transform) child.gameObject.SetActive(false);
            var recycler = Component<LabRecycler>(op.discardButton.gameObject);
            var mouth = Solid(op.discardButton.transform, "MouthInterior", new Color32(16,10,10,255), -3, 24, 262, 66);
            recycler.upperJaw = Picture(op.discardButton.transform, "UpperJaw", "RecyclerUpperJaw", -3, 30, 257, 37).rectTransform;
            recycler.lowerJaw = Picture(op.discardButton.transform, "LowerJaw", "RecyclerLowerJaw", -3, 67, 257, 37).rectTransform;
            // Mouth transforms use a local bottom-left pivot for symmetric opening.
            foreach (var jaw in new[] { recycler.upperJaw, recycler.lowerJaw }) { jaw.anchorMin = jaw.anchorMax = new Vector2(0, 0); jaw.pivot = new Vector2(0, 1); }
            recycler.glow = Picture(op.discardButton.transform, "RecyclerGlow", "RecyclerGlow", -3, 41, 257, 57);
            lab.recycler = recycler;
            Plaque(root, "LabDiscardCaption", "回收 / 拖入废弃", 1550, 541, 235, 29, 17);
            var previous = NewButton(root, "LabPreviousCagePage", "", 1587, 877, 49, 58);
            var next = NewButton(root, "LabNextCagePage", "", 1720, 877, 49, 58);
            previous.GetComponent<Image>().color = next.GetComponent<Image>().color = Color.clear;
            PersistentPage(previous, lab, -1); PersistentPage(next, lab, 1);
            Plaque(root, "LabCagePageCaption", "怪物笼翻页", 1545, 946, 250, 27, 15);
            op.currentNumber.transform.SetParent(root, false);
            At(op.currentNumber.transform, 1250, 713, 222, 78);
            TextStyle(op.currentNumber, 39, Ink, TextAlignmentOptions.Center);
            Plaque(root, "LabCollectorCaption", "能量收集", 1250, 791, 222, 28, 17);
            op.toolsBar.SetParent(root, false); At(op.toolsBar, 160, 938, 1048, 44);
            var toolsImage = op.toolsBar.GetComponent<Image>(); if (toolsImage != null) toolsImage.enabled = false;
            var caption = op.toolsBar.Find("ToolsCaption"); if (caption != null) caption.gameObject.SetActive(false);
            for (var i = 0; i < op.ownedToolViews.Length; i++)
            {
                var tool = op.ownedToolViews[i]; At(tool.transform, i * 57, 0, 52, 44);
                At(tool.iconImage.transform, 6, 0, 38, 32);
                At(tool.nameLabel.transform, -3, 30, 58, 14); TextStyle(tool.nameLabel, 10, Ink, TextAlignmentOptions.Center);
            }
            Hud(op);
            lab.motes = new RectTransform[8];
            for (var i = 0; i < lab.motes.Length; i++)
            {
                var mote = Solid(root, "LabEnergyMote" + i, Mint, 0, 0, 5 + i % 3 * 2, 5 + i % 3 * 2).rectTransform;
                mote.localEulerAngles = new Vector3(0, 0, 45); mote.gameObject.SetActive(false); lab.motes[i] = mote;
            }
            foreach (var name in new[] { "BackgroundImage", "Title", "FloorRail", "培育室", "生产区", "收容笼", "技能槽", "EnergyWord", "TargetCaption", "ExcessCaption", "GoldCaption" })
            {
                var old = root.Find(name);
                if (old == null) continue;
                var archive = Node(root,"PrototypeArchive");
                old.SetParent(archive,false); old.gameObject.SetActive(false); archive.gameObject.SetActive(false);
            }
            LabCageRefinement.Author(op);
            op.dragLayer.SetAsLastSibling();
            EditorUtility.SetDirty(op); EditorUtility.SetDirty(lab);
        }

        static void Hud(OperationScreenView op)
        {
            var root = op.transform;
            Solid(root, "LabHudBack", Panel, 145, 24, 1645, 46).transform.SetSiblingIndex(1);
            At(op.dayLabel.transform, 164, 29, 128, 35); TextStyle(op.dayLabel, 22, Ink);
            At(op.targetLabel.transform, 372, 29, 82, 35); TextStyle(op.targetLabel, 22, Mint);
            Label(root, "LabTargetCaption", "需求", 305, 31, 62, 30, 18);
            At(op.energyLabel.transform, 463, 30, 555, 34); TextStyle(op.energyLabel, 17, Ink);
            At(op.excessLabel.transform, 1110, 30, 76, 34); TextStyle(op.excessLabel, 19, Mint);
            Label(root, "LabExcessCaption", "超额", 1050, 31, 59, 30, 17);
            At(op.goldLabel.transform, 1262, 30, 65, 34); TextStyle(op.goldLabel, 21, new Color32(255,214,125,255));
            Label(root, "LabGoldCaption", "金币", 1195, 31, 64, 30, 17);
            At(op.overtimeLabel.transform, 162, 993, 120, 42); TextStyle(op.overtimeLabel, 18, Mint);
            SkinButton(op.researchButton, "科研", 1355, 27, 105, 39, 19);
            SkinButton(op.settingsButton, "设置", 1471, 27, 105, 39, 19);
            SkinButton(op.titleButton, "返回标题", 1587, 27, 189, 39, 18);
            op.shopButton.gameObject.SetActive(false);
            SkinButton(op.nextDayButton, "开始结算", 1537, 993, 260, 53, 23);
            Label(root, "LabDragHint", "拖动怪物至培育或产能区 · 右侧回收可提取基因", 303, 995, 710, 38, 17);
        }

        static void AuthorResearch(ResearchScreenView view)
        {
            if (view == null) throw new InvalidOperationException("Research screen missing.");
            var root = view.transform; FixedScreen((RectTransform)root);
            var background = Picture(root, "LabResearchShell", "LabShell", 0, 0, 1920, 1080); background.transform.SetAsFirstSibling();
            Solid(root, "LabResearchShade", new Color(0.015f,0.04f,0.035f,0.8f), 0, 0, 1920, 1080).transform.SetSiblingIndex(1);
            var tree = root.Find("科研树");
            if (tree != null) foreach (Transform child in tree) if (child.GetComponent<TechNodeView>() == null) child.gameObject.SetActive(false);
            for (var i = 0; i < view.nodes.Length; i++)
            {
                var node = view.nodes[i];
                node.transform.SetParent(root, false);
                SkinButton(node.button, node.techName, 185 + i % 2 * 420, 222 + i / 2 * 225, 390, 194, 25);
                At(node.title.transform, 24, 32, 342, 48); TextStyle(node.title, 25, Ink, TextAlignmentOptions.Left);
                At(node.costLabel.transform, 24, 129, 342, 32); TextStyle(node.costLabel, 18, Mint, TextAlignmentOptions.Left);
                var summary=Label(node.transform,"LabSummary",node.summary.Split('。')[0] + "。",24,82,342,43,16);
                summary.textWrappingMode=TextWrappingModes.Normal;
                summary.overflowMode=TextOverflowModes.Ellipsis;
                foreach (var image in node.GetComponentsInChildren<Image>(true)) if (image.gameObject != node.gameObject && image.gameObject != node.selection) image.gameObject.SetActive(false);
                if (node.selection != null) { At(node.selection.transform, 0, 0, 6, 194); node.selection.GetComponent<Image>().color = Mint; }
            }
            var detail = view.detailTitle.transform.parent; detail.gameObject.SetActive(true); At(detail, 1070, 222, 655, 644);
            detail.GetComponent<Image>().color = Panel;
            At(view.detailTitle.transform, 40, 40, 575, 60); TextStyle(view.detailTitle, 32, Ink);
            At(view.detailBody.transform, 40, 138, 575, 300); TextStyle(view.detailBody, 24, Ink, TextAlignmentOptions.TopLeft);
            At(view.detailCost.transform, 40, 453, 575, 40); TextStyle(view.detailCost, 21, Mint);
            SkinButton(view.researchButton, "研究", 40, 531, 575, 66, 25);
            SkinButton(view.backButton, "返回实验室", 1505, 115, 220, 57, 23);
            At(view.techPointLabel.transform, 1100, 124, 100, 40); TextStyle(view.techPointLabel, 26, Mint);
            At(view.goldLabel.transform, 1370, 124, 110, 40); TextStyle(view.goldLabel, 26, Ink);
            view.techPointLabel.gameObject.SetActive(true); view.goldLabel.gameObject.SetActive(true);
            var backgroundOld = root.Find("BackgroundImage"); if (backgroundOld != null) backgroundOld.gameObject.SetActive(false);
            foreach (var name in new[] { "Title", "Section", "TechCaption", "GoldCaption", "ResearchTab", "Ambience" }) { var old = root.Find(name); if (old != null) old.gameObject.SetActive(false); }
            Label(root, "LabResearchTitle", "科研 / 实验室扩建", 185, 120, 640, 60, 34);
            Label(root, "LabTechCaption", "科技点", 990, 128, 106, 36, 20);
            Label(root, "LabResearchGoldCaption", "金币", 1270, 128, 95, 36, 20);
            Label(root, "LabResearchFootnote", "研究后返回实验室，扩建与基因容量立即生效。", 185, 917, 1200, 48, 20);
            var settings = root.Find("SettingsButton");
            if (settings != null) SkinButton(settings.GetComponent<Button>(),"设置",1352,29,138,44,19);
            EditorUtility.SetDirty(view);
        }

        static void SkinSlot(MonsterSlotView slot, float x, float y, float width, float height, bool work)
        {
            At(slot.transform, x, y, width, height);
            foreach (var graphic in slot.GetComponentsInChildren<Graphic>(true))
                if (slot.portrait == null || !graphic.transform.IsChildOf(slot.portrait.transform)) graphic.enabled = false;
            var root = slot.GetComponent<Image>(); root.enabled = true; root.color = Color.clear; root.sprite = null; root.raycastTarget = true;
            if (slot.button != null) slot.button.transition = Selectable.Transition.None;
            if (slot.emptyMark != null) slot.emptyMark.SetActive(false);
            var empty = Label(slot.transform, "LabEmptySocket", work ? "＋" : "", 0, height - 54, width, 38, 24);
            empty.color = new Color(0.43f,0.95f,0.66f,0.45f); empty.alignment = TextAlignmentOptions.Center;
            slot.emptyMark = empty.gameObject;
            var line = Solid(slot.transform, "LabSocketLine", new Color(0.25f,0.85f,0.58f,work ? 0.32f : 0), 12, height - 8, width - 24, 2);
            var selection = Solid(slot.transform, "LabSelection", new Color(0.4f,1,0.7f,0.17f), 0, 0, width, height);
            slot.selection = selection.gameObject; selection.gameObject.SetActive(false); selection.transform.SetAsFirstSibling();
            if (slot.portrait != null)
            {
                At(slot.portrait.transform, 3, 10, width - 6, height - 31);
                // 旧布娃娃灯光镜像已删除。画像只保留剪影。
            }
            foreach (var badge in new[] { slot.personalityIcon, slot.raceIcon, slot.moodIcon }) if (badge != null) badge.gameObject.SetActive(false);
            if (slot.title != null) slot.title.gameObject.SetActive(false);
            if (slot.subtitle != null) slot.subtitle.gameObject.SetActive(false);
            EditorUtility.SetDirty(slot);
        }

        static void SkinChip(SkillChipView chip, float x, float y, float width, float height, int size)
        {
            At(chip.transform, x, y, width, height);
            var image = chip.GetComponent<Image>(); image.color = new Color(0.01f,0.05f,0.04f,0.65f); image.sprite = null;
            chip.button.transition = Selectable.Transition.ColorTint;
            At(chip.label.transform, 4, 4, width - 8, height - 12); TextStyle(chip.label, size, Ink, TextAlignmentOptions.Center);
            if (chip.reservedLabel != null) { At(chip.reservedLabel.transform, 2, height - 19, width - 4, 17); TextStyle(chip.reservedLabel, 10, Mint); }
        }

        static void FixedScreen(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f,0.5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1920,1080); rect.localScale = Vector3.one;
        }
        static RectTransform Node(Transform parent, string name)
        {
            var child = parent.Find(name) as RectTransform;
            if (child != null) { child.gameObject.SetActive(true); return child; }
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        static Image Picture(Transform parent, string name, string spriteName, float x, float y, float w, float h)
        {
            var image = Solid(parent, name, Color.white, x,y,w,h);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + spriteName + ".png");
            if (image.sprite == null) throw new InvalidOperationException("Missing art " + spriteName);
            image.type = Image.Type.Simple; return image;
        }
        static Image Solid(Transform parent, string name, Color color, float x,float y,float w,float h)
        {
            var rect = Node(parent, name); At(rect,x,y,w,h);
            var image = Component<Image>(rect.gameObject);
            image.enabled = true; image.color = color; image.raycastTarget = false; return image;
        }
        static TMP_Text Label(Transform parent, string name,string text,float x,float y,float w,float h,int size)
        {
            var rect = Node(parent,name); At(rect,x,y,w,h);
            var label = Component<TextMeshProUGUI>(rect.gameObject);
            label.text = text; label.font = _font; label.raycastTarget = false; TextStyle(label,size,Ink); return label;
        }
        static TMP_Text Plaque(Transform parent,string name,string text,float x,float y,float w,float h,int size)
        {
            var back = Solid(parent,name,Panel,x,y,w,h);
            return Label(back.transform,"Caption",text,6,0,w-12,h,size);
        }
        static void TextStyle(TMP_Text label,float size,Color color,TextAlignmentOptions align = TextAlignmentOptions.Left)
        { label.enabled = true; label.fontSize = size; label.enableAutoSizing = false; label.color = color; label.alignment = align; label.raycastTarget = false; }
        static void At(Transform transform,float x,float y,float w,float h) => LabSceneView.Place((RectTransform)transform,x,y,w,h);
        static void Stretch(RectTransform rect,float inset)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(inset,inset); rect.offsetMax = new Vector2(-inset,-inset); }
        static Button NewButton(Transform parent,string name,string text,float x,float y,float w,float h)
        {
            var image = Solid(parent,name,Panel,x,y,w,h); image.raycastTarget = true;
            var button = Component<Button>(image.gameObject); button.targetGraphic = image;
            Label(image.transform,"Label",text,5,0,w-10,h,18).alignment = TextAlignmentOptions.Center; return button;
        }
        static void SkinButton(Button button,string text,float x,float y,float w,float h,int size)
        {
            button.gameObject.SetActive(true); At(button.transform,x,y,w,h);
            var image = button.GetComponent<Image>(); image.enabled = true; image.color = Panel; image.raycastTarget = true;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.gameObject.SetActive(true); label.text = text; At(label.transform,8,0,w-16,h); TextStyle(label,size,Ink,TextAlignmentOptions.Center); }
        }
        static void Action(Button button,PresentationAction action)
        { Component<PresentationActionButton>(button.gameObject).Configure(null,action); }
        static T Component<T>(GameObject go) where T : Component
        { var component = go.GetComponent<T>(); return component != null ? component : go.AddComponent<T>(); }
        static void PersistentPage(Button button, LabSceneView lab,int direction)
        {
            while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick,0);
            UnityEventTools.AddIntPersistentListener(button.onClick,lab.PageCage,direction);
        }

        public static string ValidateSaved()
        {
            var op = Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
            if (op == null || op.lab == null || op.lab.recycler == null || op.lab.needle == null) throw new InvalidOperationException("Lab references missing.");
            if (op.extractionSlots.Length != 6 || op.breedingSlots.Length != 6 || op.skillChips.Length != 4) throw new InvalidOperationException("Semantic slot capacity changed.");
            if (op.extractionSlots.Concat(op.breedingSlots).Any(slot => slot == null || slot.portrait == null || slot.GetComponent<OperationZone>() == null || slot.GetComponent<OperationPayloadDrag>() == null)) throw new InvalidOperationException("A work slot lost its input contract.");
            if (op.extractionSlots.Any(slot=> !slot.transform.Find("LabProductionPlatform").GetComponent<Image>().enabled)) throw new InvalidOperationException("A repeated authoring pass disabled a production platform.");
            if (op.lab.needle.GetSiblingIndex() < op.lab.needle.parent.Find("CollectorDialFace").GetSiblingIndex()) throw new InvalidOperationException("Dial face occludes the moving needle.");
            if (Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(canvas => canvas.renderMode == RenderMode.WorldSpace)) throw new InvalidOperationException("Legacy world canvas remains.");
            if (GraphicsSettings.defaultRenderPipeline == null || AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline) != Pipeline) throw new InvalidOperationException("Graphics pipeline mismatch.");
            var qualities = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]).FindProperty("m_QualitySettings");
            for (var i=0;i<qualities.arraySize;i++) if (AssetDatabase.GetAssetPath(qualities.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue) != Pipeline) throw new InvalidOperationException("Quality pipeline mismatch.");
            if (RenderSettings.skybox != null || !Camera.main.orthographic || Camera.main.clearFlags != CameraClearFlags.SolidColor) throw new InvalidOperationException("2D camera / skybox mismatch.");
            return "PASS saved lab: art, 6 extraction / 6 parent / 4 gene slots, recycler, meter, one UI space, Graphics and all Quality levels on URP 2D, orthographic camera, no skybox.";
        }
    }
}
