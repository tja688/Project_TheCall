using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Image = UnityEngine.UI.Image;
using Button = UnityEngine.UI.Button;

namespace TheCall.Editor
{
    // Author through Editor APIs, keeping the rule-facing slots, IDs, controls and listeners.
    public static class FacilityArtIntegration
    {
        const string ArtPath = "Assets/Arts/Facility/";
        static readonly Color Cold = new Color(0.72f, 1.02f, 1.12f, 1);
        static readonly Color Warm = new Color(1.22f, 0.68f, 0.64f, 1);
        static readonly Color Green = new Color(0.62f, 1.06f, 0.86f, 1);
        static readonly Color Ink = new Color32(214, 242, 237, 255);
        static readonly Color Back = new Color32(12, 27, 34, 240);
        [Serializable] sealed class Layer { public string name; public int[] bounds; }
        [Serializable] sealed class Manifest { public Layer[] layers; }
        static Dictionary<string, Layer> _layers;
        static TMP_FontAsset _font;
        static Material _lit;
        static Transform _lights;

        [MenuItem("TheCall/Art/接入横向地图正式素材")]
        public static void FromMenu() => Apply();

        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity") throw new InvalidOperationException("Open Main first.");
            Directory.CreateDirectory("Temp/FacilityArt/rollback");
            if (!File.Exists("Temp/FacilityArt/rollback/Main.unity")) File.Copy(scene.path, "Temp/FacilityArt/rollback/Main.unity");
            Import();
            _layers = JsonUtility.FromJson<Manifest>(File.ReadAllText(ArtPath + "manifest.json")).layers.ToDictionary(x => x.name);
            var op = Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
            _font = op.dayLabel.font;
            _lights = SceneNode("FacilityLighting").transform;
            var global = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(x => x.lightType == Light2D.LightType.Global);
            global.color = Color.white; global.intensity = 0.84f;
            var catalog = AssetDatabase.LoadAssetAtPath<ShadowMonsterCatalog>("Assets/Resources/ShadowMonsterCatalog.asset");
            if (catalog == null)
            { catalog = ScriptableObject.CreateInstance<ShadowMonsterCatalog>(); AssetDatabase.CreateAsset(catalog, "Assets/Resources/ShadowMonsterCatalog.asset"); }
            catalog.silhouettes = Enumerable.Range(1, 5).Select(i => Sprite("Monster" + i.ToString("00"))).ToArray();
            EditorUtility.SetDirty(catalog);
            AuthorOperation(op);
            AuthorShop(Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include));
            AuthorSupportingScreens();
            foreach (var portrait in Object.FindObjectsByType<MonsterPortrait>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { portrait.UseShadowArtwork(); EditorUtility.SetDirty(portrait); }
            // Runtime clones inherit the scene templates. The retained assembly workbench remains opt-in.
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponentInChildren<MonsterPortrait>(true) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var portrait in root.GetComponentsInChildren<MonsterPortrait>(true)) portrait.UseShadowArtwork();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "PASS saved horizontal facility, formal shop, five silhouettes, live URP 2D lamps and semantic slots";
        }

        static void Import()
        {
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(ArtPath, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            const string pathMaterial = "Assets/Arts/Facility/FacilityLit.mat";
            _lit = AssetDatabase.LoadAssetAtPath<Material>(pathMaterial);
            if (_lit == null)
            { _lit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")); AssetDatabase.CreateAsset(_lit, pathMaterial); }
        }

        static void AuthorOperation(OperationScreenView op)
        {
            var root = op.transform;
            var viewport = Node(root, "FacilityViewport", 0, 0, 1920, 1080);
            viewport.SetAsFirstSibling();
            Component<RectMask2D>(viewport.gameObject);
            var hit = Component<Image>(viewport.gameObject); hit.color = new Color(0, 0, 0, 0.001f); hit.raycastTarget = true;
            var empty = Component<OperationZone>(viewport.gameObject); empty.Place = LandingPlace.EmptySpace;
            var content = Node(viewport, "FacilityContent", -1920, 0, 3840, 1080);
            var panorama = Component<FacilityPanorama>(op.gameObject); panorama.content = content;
            op.pointer.panorama = panorama;
            var facility = Component<FacilitySceneView>(op.gameObject);
            facility.operation = op; facility.panorama = panorama; op.lab.facility = facility;
            WorldArt(content, "DormShell", Warm, 0);
            WorldArt(content, "ProductionShell", Cold, 0);

            // Move authoritative controls before retiring only the superseded paintings.
            for (var i = 0; i < op.extractionSlots.Length; i++)
            {
                var slot = op.extractionSlots[i]; slot.transform.SetParent(content, false);
                SkinSlot(slot, 1920 + 83 + i * 185, 687, 178, 280);
                At(slot.portrait.transform, 8, 17, 162, 235);
                var platform = Picture(slot.transform, "FacilityPlatform", "Platform", 0, 134, 178, 156, Cold);
                platform.transform.SetAsFirstSibling();
            }
            facility.seatLamps = new Image[op.extractionSlots.Length];
            for (var i = 0; i < facility.seatLamps.Length; i++)
            {
                var lamp = Solid(op.extractionSlots[i].transform, "FacilitySeatLamp", Ink, 66, 252, 38, 22);
                Label(lamp.transform, "SeatIndex", (i+1).ToString("00"), 0, 0, 38, 22, 13, new Color32(23, 53, 60, 255));
                facility.seatLamps[i] = lamp;
            }
            foreach (var slot in op.breedingSlots)
            {
                slot.transform.SetParent(content, false); SkinSlot(slot, 2040, 234, 210, 260);
                var pod = Picture(slot.transform, "FacilityPod", "BreedingPod", 0, 0, 210, 260, new Color(0.64f, 0.87f, 0.93f, 0.58f));
                pod.transform.SetAsLastSibling();
            }
            facility.roomCaptions = new[] {
                Label(content, "FacilityBreeding1", "培育 1", 2035, 147, 410, 36, 21, Ink),
                Label(content, "FacilityBreeding2", "槽位扩容 / 科研解锁", 2740, 147, 475, 36, 21, Ink)
            };
            var roomArt = new List<GameObject>();
            for (var room = 0; room < 2; room++)
            {
                var offset = room * 710;
                var tube = WorldArt(content, "FacilityChild"+room, "BreedingChild", Cold, 8, offset);
                roomArt.Add(tube.gameObject);
                WorldArt(content, "FacilityChildRim"+room, "BreedingChildRim", Cold, 9, offset);
                var gene = op.breedingSkills[room]; gene.transform.SetParent(content, false);
                SkinChip(gene, 1920 + 584 + offset, 126, 96, 194);
                Picture(gene.transform, "FacilityGenePlus", "GenePlus", 7, 13, 82, 78, Cold).transform.SetAsFirstSibling();
                At(gene.label.transform, 5, 93, 86, 90); Style(gene.label, 16, Ink);
            }
            facility.secondRoomArt = roomArt.Skip(1).ToArray();
            var shutter = WorldArt(content, "Shutter", Cold, 12);
            facility.shutter = (RectTransform)shutter.transform;
            var lockButton = NewButton(content, "FacilityRoomLock", "扩建培育室\n科研 / 槽位扩容", 2835, 302, 360, 106, 23);
            Action(lockButton, PresentationAction.OpenResearch); facility.roomLock = lockButton.gameObject;

            var lift = (LabCageLift)op.lab.cageScroll;
            lift.transform.SetParent(content, false);
            At(lift.transform, 3378, 189, 344, 308);
            lift.rowHeight = 154; lift.rowWidth = 344;
            lift.rowBacks = new[] { Sprite("CageRowBack"), Sprite("CageRowBack") };
            lift.rowFronts = new[] { Sprite("CageRowFront"), Sprite("CageRowFront") };
            Stretch(lift.viewport, 0);
            foreach (Transform child in lift.viewport)
                if (child != lift.content) child.gameObject.SetActive(false);
            foreach (Transform child in lift.content)
                if (child.GetComponent<MonsterSlotView>() == null) child.gameObject.SetActive(false);
            op.cageColumns = 2; op.cageRowSpacing = 154; op.cagePadding = 0;
            for (var i = 0; i < op.cageSlots.Length; i++)
            {
                var slot = op.cageSlots[i];
                SkinSlot(slot, i%2*174, i/2*154, 170, 154);
                At(slot.portrait.transform, 22, 12, 126, 131);
            }
            lift.previousButton.transform.SetParent(content, false);
            lift.nextButton.transform.SetParent(content, false);
            SkinButton(lift.previousButton, "上", 3649, 126, 64, 44, 19);
            SkinButton(lift.nextButton, "下", 3650, 500, 64, 39, 19);
            facility.cageCaption = Label(content, "FacilityCageCount", "收容", 3378, 128, 250, 42, 19, Ink);
            lift.rangeLabel = Label(content, "FacilityCageRange", "", 3378, 500, 264, 35, 16, Ink);
            op.lab.cageCount = facility.cageCaption;

            var recycler = op.lab.recycler;
            recycler.transform.SetParent(content, false); At(recycler.transform, 3368, 556, 360, 128);
            foreach (Transform child in recycler.transform) child.gameObject.SetActive(false);
            var recyclerImage = Component<Image>(recycler.gameObject); recyclerImage.sprite = null; recyclerImage.color = new Color(0, 0, 0, 0.001f); recyclerImage.raycastTarget = true;
            recycler.useAuthoredJaws = true;
            recycler.upperJaw = Picture(recycler.transform, "FacilityUpperJaw", "RecyclerUpper", 0, 0, 360, 62, Cold).rectTransform;
            recycler.lowerJaw = Picture(recycler.transform, "FacilityLowerJaw", "RecyclerLower", 0, 62, 360, 63, Cold).rectTransform;
            recycler.upperRest = recycler.upperJaw.anchoredPosition; recycler.lowerRest = recycler.lowerJaw.anchoredPosition;
            recycler.glow = Solid(recycler.transform, "FacilityMouthGlow", new Color(0.2f, 0.8f, 0.75f, 0.12f), 0, 0, 360, 128);
            recycler.glow.transform.SetAsFirstSibling();
            Label(content, "FacilityRecyclerCaption", "基因回收 / 拖入废弃", 3380, 677, 332, 31, 17, Ink);

            for (var i = 0; i < op.skillChips.Length; i++)
            {
                var chip = op.skillChips[i]; chip.transform.SetParent(content, false);
                SkinChip(chip, 3376 + i%2*170, 724 + i/2*73, 161, 64);
            }
            facility.geneCaption = Label(content, "FacilityGeneCount", "基因仓储", 3380, 854, 245, 35, 17, Ink);
            op.addGeneSlotButton.transform.SetParent(content, false);
            SkinButton(op.addGeneSlotButton, "+", 3650, 850, 58, 42, 23);
            op.nextDayButton.transform.SetParent(content, false);
            SkinButton(op.nextDayButton, "开始结算", 3470, 900, 151, 78, 19);
            op.nextDayLabel = op.nextDayButton.GetComponentInChildren<TMP_Text>();
            op.currentNumber.transform.SetParent(content, false);
            At(op.currentNumber.transform, 1990, 687, 186, 44); Style(op.currentNumber, 25, Ink);
            facility.needle = (RectTransform)WorldArt(content, "GaugeNeedle", Cold, 15).transform;
            facility.needle.pivot = new Vector2(11f/47f, -12f/82f);
            facility.needle.anchoredPosition = new Vector2(2094, -661);
            facility.collectorFluid = (RectTransform)WorldArt(content, "CollectorCore", new Color(0.8f, 1.2f, 1.3f, 1), 10).transform;
            facility.collectorFluid.pivot = new Vector2(0.5f, 0);
            facility.collectorFluid.anchoredPosition = new Vector2(3196, -919);
            WorldArt(content, "CollectorGlow", new Color(0.5f, 0.95f, 1, 0.5f), 11);
            facility.motes = new RectTransform[10];
            for (var i = 0; i < facility.motes.Length; i++)
            {
                var mote = Solid(content, "FacilityEnergyMote"+i, new Color32(182, 255, 242, 230), 0, 0, 9, 9);
                mote.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                facility.motes[i] = mote.rectTransform; mote.gameObject.SetActive(false);
            }
            AuthorDorm(op, content, facility);
            var matte = Node(root, "FacilityMatte", 0, 0, 1920, 1080);
            matte.SetSiblingIndex(viewport.GetSiblingIndex()+1);
            var matteColor = new Color32(5, 12, 15, 255);
            Solid(matte, "OutsideLeft", matteColor, -4096, -4096, 4096, 9272);
            Solid(matte, "OutsideRight", matteColor, 1920, -4096, 4096, 9272);
            Solid(matte, "OutsideTop", matteColor, 0, -4096, 1920, 4096);
            Solid(matte, "OutsideBottom", matteColor, 0, 1080, 1920, 4096);
            AuthorHud(op, panorama);
            var lights = new[] { new Rect(604, 110, 365, 401), new Rect(607, 550, 365, 401),
                new Rect(1023, 550, 365, 401), new Rect(1440, 550, 365, 401),
                new Rect(2114, 117, 365, 401), new Rect(2808, 117, 365, 401),
                new Rect(2142, 550, 365, 401), new Rect(2670, 550, 365, 401) };
            for (var i = 0; i < lights.Length; i++)
                Lamp(content, "FacilityLamp"+i, lights[i], i < 4 ? new Color(1, 0.7f, 0.65f) : new Color(0.64f, 0.91f, 1), 0.55f, i*2.73f);
            facility.collectorLamp = Lamp(content, "CollectorLamp", new Rect(3045, 539, 288, 434), new Color(0.45f, 1, 0.97f), 0.32f, 5);
            foreach (Transform child in root.Cast<Transform>().ToArray())
                if (child.name.StartsWith("Lab", StringComparison.Ordinal) || child.name == "PrototypeArchive") child.gameObject.SetActive(false);
            op.dragLayer.SetAsLastSibling();
            EditorUtility.SetDirty(op); EditorUtility.SetDirty(op.lab); EditorUtility.SetDirty(op.pointer);
        }

        static void AuthorDorm(OperationScreenView op, Transform content, FacilitySceneView facility)
        {
            var floor = Solid(content, "FacilityRestArea", new Color(0, 0, 0, 0.001f), 450, 175, 1390, 793);
            floor.raycastTarget = true; Component<OperationZone>(floor.gameObject).Place = LandingPlace.Cage;
            facility.dormCaption = Label(content, "FacilityDormCaption", "休息区", 490, 160, 1070, 36, 21, new Color32(255, 220, 206, 255));
            var dormRoot = Node(content, "DormResidents", 0, 0, 1920, 1080);
            var slots = new List<MonsterSlotView>();
            for (var i = 0; i < 6; i++)
            {
                var name = "DormResident_"+i;
                var node = dormRoot.Find(name);
                var slot = node != null ? node.GetComponent<MonsterSlotView>() : Object.Instantiate(op.extractionSlots[0], dormRoot);
                slot.name = name;
                SkinSlot(slot, 570 + i%3*320, i<3 ? 277 : 719, 200, 260);
                foreach (Transform child in slot.transform)
                    if (child.name.StartsWith("Facility", StringComparison.Ordinal)) child.gameObject.SetActive(false);
                At(slot.portrait.transform, 13, 0, 174, 222);
                slot.portrait.SetShadowTint(new Color32(82, 25, 28, 255));
                slot.title = Label(slot.transform, "ResidentCaption", "", -20, 215, 240, 28, 16, new Color32(255, 216, 204, 255));
                var zone = Component<OperationZone>(slot.gameObject); zone.Place = LandingPlace.Cage; zone.monsterView = slot;
                var drag = Component<OperationPayloadDrag>(slot.gameObject); drag.payloadKind = PayloadKind.Monster; drag.session = op.pointer;
                slots.Add(slot);
            }
            facility.dormSlots = slots.ToArray();
            facility.previousDorm = NewButton(content, "FacilityDormPrevious", "上页", 1605, 157, 83, 37, 17);
            facility.nextDorm = NewButton(content, "FacilityDormNext", "下页", 1699, 157, 83, 37, 17);
            SetClick(facility.previousDorm, () => facility.PageDorm(-1));
            SetClick(facility.nextDorm, () => facility.PageDorm(1));
            // Persistent calls use zero-argument wrappers for repeatable serialized wiring.
            facility.previousDorm.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddIntPersistentListener(facility.previousDorm.onClick, facility.PageDorm, -1);
            facility.nextDorm.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddIntPersistentListener(facility.nextDorm.onClick, facility.PageDorm, 1);
            var monitor = Solid(content, "FacilityMonitorPanel", new Color32(22, 25, 30, 244), 98, 141, 318, 794);
            Label(monitor.transform, "MonitorTitle", "员工状态监控", 16, 18, 286, 35, 23, Ink);
            facility.monitorSummary = Label(monitor.transform, "MonitorSummary", "", 16, 63, 286, 93, 18, Ink);
            facility.monitorSummary.alignment = TextAlignmentOptions.TopLeft;
            var viewport = Node(monitor.transform, "MonitorViewport", 6, 172, 306, 598);
            var image = Component<Image>(viewport.gameObject); image.color = new Color(0, 0, 0, 0.001f); image.raycastTarget = true;
            Component<RectMask2D>(viewport.gameObject);
            var body = Node(viewport, "Content", 0, 0, 306, 610);
            var scroll = Component<ScrollRect>(viewport.gameObject); scroll.viewport = viewport; scroll.content = body;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 42;
            facility.monitorContent = body;
            facility.monitorRow = Label(body, "ResidentStatus_0", "", 10, 0, 286, 96, 17, Ink);
            facility.monitorRow.alignment = TextAlignmentOptions.TopLeft;
            facility.monitorEmpty = Label(viewport, "MonitorEmpty", "暂无持有怪物", 10, 20, 286, 50, 18, Ink);
            var upgrades = new List<FacilityUpgradeView>();
            foreach (var key in new[] { "DormGeometry", "DormTelevision", "DormCandy", "DormPlant" })
            {
                var anchor = Node(content, key+"Unlock", 0, 0, 1920, 1080);
                var art = WorldArt(anchor, key, Warm, 6);
                var gate = Component<FacilityUpgradeView>(anchor.gameObject); gate.artwork = art.gameObject;
                gate.motion = key == "DormGeometry" ? FacilityUpgradeView.AmbientMotion.Geometry : key == "DormTelevision" ? FacilityUpgradeView.AmbientMotion.Television :
                    key == "DormCandy" ? FacilityUpgradeView.AmbientMotion.Candy : FacilityUpgradeView.AmbientMotion.Plant;
                var artRect = (RectTransform)art.transform;
                var bounds = _layers[key].bounds;
                artRect.pivot = new Vector2(0.5f, 0.5f);
                artRect.anchoredPosition = new Vector2((bounds[0]+bounds[2])*0.5f, -(bounds[1]+bounds[3])*0.5f);
                // Empty keys are intentional. The delivered concepts have no approved content IDs yet.
                upgrades.Add(gate); art.gameObject.SetActive(gate.previewUnlocked);
            }
            facility.upgrades = upgrades.ToArray();
        }

        static void AuthorHud(OperationScreenView op, FacilityPanorama panorama)
        {
            var root = op.transform;
            var hud = Node(root, "FacilityHud", 0, 0, 1920, 1080);
            Solid(hud, "HeaderBack", new Color32(10, 22, 28, 250), 0, 0, 1920, 96);
            var labels = new[] { op.dayLabel, op.targetLabel, op.energyLabel, op.excessLabel, op.goldLabel, op.overtimeLabel };
            foreach (var label in labels) label.transform.SetParent(hud, false);
            At(op.dayLabel.transform, 38, 24, 160, 44); Style(op.dayLabel, 26, Ink);
            Label(hud, "DemandCaption", "需求", 212, 31, 64, 30, 17, Ink);
            At(op.targetLabel.transform, 280, 24, 90, 42); Style(op.targetLabel, 26, new Color32(165, 243, 231, 255));
            At(op.energyLabel.transform, 380, 30, 510, 36); Style(op.energyLabel, 17, Ink);
            Label(hud, "ExcessCaption", "超额", 920, 32, 62, 30, 17, Ink);
            At(op.excessLabel.transform, 983, 25, 86, 42); Style(op.excessLabel, 22, Ink);
            Label(hud, "GoldCaption", "金币", 1100, 32, 62, 30, 17, Ink);
            At(op.goldLabel.transform, 1170, 26, 85, 42); Style(op.goldLabel, 22, new Color32(255, 221, 155, 255));
            foreach (var button in new[] { op.researchButton, op.settingsButton, op.titleButton }) button.transform.SetParent(hud, false);
            SkinButton(op.researchButton, "科研", 1375, 25, 114, 44, 19);
            SkinButton(op.settingsButton, "设置", 1504, 25, 114, 44, 19);
            SkinButton(op.titleButton, "返回标题", 1633, 25, 235, 44, 19);
            op.shopButton.gameObject.SetActive(false);
            Solid(hud, "FooterBack", new Color32(10, 22, 28, 250), 0, 996, 1920, 84);
            var left = NewButton(hud, "FacilityDormTab", "宿舍区", 38, 1013, 172, 45, 20);
            var right = NewButton(hud, "FacilityProductionTab", "生产培育区", 224, 1013, 190, 45, 20);
            left.onClick = new Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(left.onClick, panorama.ShowDorm);
            right.onClick = new Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(right.onClick, panorama.ShowProduction);
            panorama.locationLabel = Label(hud, "FacilityLocation", "02  /  生产培育区", 457, 1015, 338, 41, 21, Ink);
            At(op.overtimeLabel.transform, 820, 1019, 128, 34); Style(op.overtimeLabel, 17, Ink);
            Label(hud, "FacilityDragHint", "拖动空白切换区域 / 拖怪物到屏幕边缘可跨区调度", 975, 1020, 865, 32, 17, Ink);
            op.toolsBar.SetParent(hud, false); At(op.toolsBar, 32, 92, 1800, 36);
            // Tool details stay available in their existing inventory. Avoid overlaying the authored rooms.
            for (var i = 0; i < op.ownedToolViews.Length; i++)
            {
                var view = op.ownedToolViews[i]; if (view == null) continue;
                At(view.transform, i * 96, 0, 86, 36);
                if (view.nameLabel != null) { At(view.nameLabel.transform, 0, 0, 86, 36); Style(view.nameLabel, 13, Ink); }
                if (view.iconImage != null) view.iconImage.gameObject.SetActive(false);
            }
        }

        static void AuthorShop(ShopScreenView shop)
        {
            var root = shop.transform;
            // Preserve references first; decorative children of the previous shop can then be retired.
            var stage = Node(root, "FacilityShopStage", 0, 0, 1920, 1080); stage.SetAsFirstSibling();
            WorldArt(stage, "ShopShell", Green, 0);
            var buy = (RectTransform)shop.buyPage.transform; buy.SetParent(stage, false); At(buy, 500, 230, 800, 575);
            var sell = (RectTransform)shop.sellPage.transform; sell.SetParent(stage, false); At(sell, 500, 230, 800, 575);
            foreach (var page in new[] { buy, sell })
            {
                var image = page.GetComponent<Image>(); if (image != null) image.color = Color.clear;
                foreach (var layout in page.GetComponents<LayoutGroup>()) layout.enabled = false;
            }
            var view = Component<FacilityShopView>(shop.gameObject); shop.facility = view; view.shop = shop;
            foreach (var card in shop.cards)
            {
                card.transform.SetParent(buy, false);
                var image = Component<Image>(card.gameObject); image.sprite = null; image.color = new Color32(16, 43, 40, 241); image.raycastTarget = false;
                foreach (var portrait in card.GetComponentsInChildren<MonsterPortrait>(true)) portrait.UseShadowArtwork();
                if (card.portrait != null)
                { At(card.portrait.transform, 18, 28, 136, 155); card.portrait.SetShadowTint(new Color32(113, 181, 165, 255)); }
                if (card.icon != null) At(card.icon.transform, 25, 39, 120, 132);
                At(card.title.transform, 163, 18, 182, 40); Style(card.title, 23, Ink);
                At(card.body.transform, 163, 67, 178, 96); Style(card.body, 16, Ink); card.body.alignment = TextAlignmentOptions.TopLeft;
                At(card.price.transform, 18, 186, 139, 35); Style(card.price, 20, new Color32(255, 222, 162, 255));
                card.stock.gameObject.SetActive(false);
                SkinButton(card.buyButton, "购买", 163, 182, 178, 43, 20);
                card.buyLabel = card.buyButton.GetComponentInChildren<TMP_Text>();
                foreach (var label in card.GetComponentsInChildren<TMP_Text>(true))
                    if (label != card.title && label != card.body && label != card.price && label != card.buyLabel && label != card.stock) label.gameObject.SetActive(false);
            }
            var sellScroll = shop.sellSlots[0].GetComponentInParent<ScrollRect>(true);
            if (sellScroll != null)
            {
                sellScroll.transform.SetParent(sell, false); At(sellScroll.transform, 0, 0, 800, 575);
                Stretch(sellScroll.viewport, 0); sellScroll.horizontal = false; sellScroll.vertical = true;
                var mask = Component<RectMask2D>(sellScroll.viewport.gameObject);
                var legacyMask = sellScroll.viewport.GetComponent<Mask>();
                if (legacyMask != null) legacyMask.enabled = false;
                var viewportImage = Component<Image>(sellScroll.viewport.gameObject); viewportImage.color = new Color(0, 0, 0, 0.001f);
                sellScroll.content.anchorMin = sellScroll.content.anchorMax = sellScroll.content.pivot = new Vector2(0, 1);
                sellScroll.content.sizeDelta = new Vector2(800, 600);
            }
            shop.sellColumns = 2; shop.sellRowSpacing = 192; shop.sellPadding = 12;
            for (var i = 0; i < shop.sellSlots.Length; i++)
            {
                var slot = shop.sellSlots[i]; SkinSlot(slot, i%2*394, i/2*192, 380, 180);
                slot.GetComponent<Image>().color = new Color32(18, 43, 39, 242);
                At(slot.portrait.transform, 10, 9, 136, 158);
                slot.portrait.SetShadowTint(new Color32(113, 181, 165, 255));
                slot.title.gameObject.SetActive(true); At(slot.title.transform, 153, 16, 212, 49); Style(slot.title, 19, Ink);
                slot.subtitle.gameObject.SetActive(true); At(slot.subtitle.transform, 153, 70, 212, 56); Style(slot.subtitle, 15, Ink);
                var bar = slot.transform.Find("ActionBar");
                if (bar != null)
                {
                    bar.gameObject.SetActive(true); At(bar, 153, 133, 212, 34);
                    var barImage = bar.GetComponent<Image>();
                    if (barImage != null) { barImage.sprite = null; barImage.color = new Color32(48, 108, 96, 255); barImage.raycastTarget = false; }
                    var caption = bar.GetComponentInChildren<TMP_Text>(true);
                    if (caption != null) { caption.gameObject.SetActive(true); At(caption.transform, 0, 0, 212, 34); Style(caption, 16, Ink); caption.text = "点击出售"; }
                }
            }
            var title = Label(stage, "FacilityShopTitle", "怪物育成公司 / 供应站", 1350, 132, 487, 88, 31, new Color32(18, 59, 47, 255));
            view.categoryText = Label(stage, "FacilityShopCategory", "怪物货架", 512, 159, 590, 45, 26, new Color32(21, 61, 50, 255));
            view.stockText = Label(stage, "FacilityShopStock", "", 512, 847, 770, 48, 19, Ink);
            var monster = NewButton(stage, "FacilityMonsterCategory", "怪物", 171, 151, 258, 79, 25);
            var tools = NewButton(stage, "FacilityToolCategory", "工具", 171, 252, 258, 79, 25);
            monster.onClick = new Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(monster.onClick, view.ShowMonsters);
            tools.onClick = new Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(tools.onClick, view.ShowTools);
            view.monsterIndicator = Solid(monster.transform, "CategoryLamp", Ink, 15, 26, 8, 27);
            view.toolIndicator = Solid(tools.transform, "CategoryLamp", Ink, 15, 26, 8, 27);
            if (shop.refreshButton == null)
            {
                shop.refreshButton = NewButton(stage, "RefreshShelf", "刷新", 177, 652, 244, 272, 22);
                Action(shop.refreshButton, PresentationAction.RefreshShelf);
            }
            foreach (var button in new[] { shop.buyTabButton, shop.sellTabButton, shop.researchButton, shop.leaveButton, shop.refreshButton })
                button.transform.SetParent(stage, false);
            SkinButton(shop.buyTabButton, "购入", 199, 382, 80, 77, 21);
            SkinButton(shop.sellTabButton, "出售", 309, 382, 80, 77, 21);
            SkinButton(shop.researchButton, "科研", 199, 486, 80, 77, 21);
            Action(NewButton(stage, "FacilityShopHandbook", "手册", 309, 486, 80, 77, 21), PresentationAction.OpenHandbook);
            SkinButton(shop.leaveButton, "下一天", 1578, 846, 210, 91, 25);
            SkinButton(shop.refreshButton, "刷新", 177, 652, 244, 272, 22);
            shop.refreshPriceLabel = shop.refreshButton.GetComponentInChildren<TMP_Text>();
            At(shop.refreshPriceLabel.transform, 12, 195, 220, 49); Style(shop.refreshPriceLabel, 21, Ink);
            var lever = Solid(shop.refreshButton.transform, "FacilityRefreshLever", new Color32(125, 197, 173, 255), 105, 42, 18, 135);
            lever.rectTransform.pivot = new Vector2(0.5f, 0.1f); view.lever = lever.rectTransform;
            lever.rectTransform.anchoredPosition = new Vector2(114, -163.5f);
            var knob = Solid(lever.transform, "LeverKnob", new Color32(194, 246, 223, 255), -17, -10, 52, 52);
            knob.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            shop.balanceLabel.transform.SetParent(stage, false); At(shop.balanceLabel.transform, 925, 929, 180, 46); Style(shop.balanceLabel, 28, Ink);
            Label(stage, "FacilityShopGoldCaption", "账户金币", 511, 932, 260, 42, 23, Ink);
            shop.portraitBalanceLabel.gameObject.SetActive(false);
            var screen = Solid(stage, "FacilityDoctorScreen", new Color32(8, 28, 24, 232), 1344, 508, 410, 68);
            view.screenText = Label(screen.transform, "Transmission", "", 12, 14, 386, 48, 18, new Color32(236, 255, 246, 255));
            view.screenText.alignment = TextAlignmentOptions.TopLeft;
            view.doctorSprite = Sprite("Doctor");
            view.signalBars = System.Array.Empty<RectTransform>();
            for (var i = screen.transform.childCount - 1; i >= 0; i--)
                if (screen.transform.GetChild(i).name.StartsWith("Signal")) screen.transform.GetChild(i).gameObject.SetActive(false);
            view.LayoutDoctor(stage);
            var talk = NewButton(stage, "FacilityDoctorTalk", "交谈", 1577, 726, 210, 91, 25);
            talk.onClick = new Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(talk.onClick, view.Talk);
            var hud = Node(root, "FacilityShopHud", 0, 0, 1920, 1080);
            Solid(hud, "Header", new Color32(10, 26, 22, 250), 0, 0, 1920, 96);
            Label(hud, "HeaderTitle", "供应站 / 售货终端", 42, 23, 690, 49, 30, Ink);
            shop.titleButton.transform.SetParent(hud, false); SkinButton(shop.titleButton, "返回标题", 1629, 25, 236, 44, 19);
            var settings = root.Find("SettingsButton");
            if (settings != null) { settings.SetParent(hud, false); SkinButton(settings.GetComponent<Button>(), "设置", 1498, 25, 114, 44, 19); }
            foreach (Transform child in root.Cast<Transform>().ToArray())
                if (child != stage && child != hud) child.gameObject.SetActive(false);
            Lamp(stage, "FacilityShopLamp", new Rect(1330, 100, 500, 650), new Color(0.67f, 1, 0.83f), 0.4f, 7);
            EditorUtility.SetDirty(shop);
        }

        static void SkinSlot(MonsterSlotView slot, float x, float y, float w, float h)
        {
            At(slot.transform, x, y, w, h);
            var image = Component<Image>(slot.gameObject); image.sprite = null; image.color = new Color(0, 0, 0, 0.001f); image.raycastTarget = true;
            slot.portrait.UseShadowArtwork();
            foreach (var text in slot.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(false);
            foreach (var badge in new[] { slot.personalityIcon, slot.raceIcon, slot.moodIcon }) if (badge != null) badge.gameObject.SetActive(false);
            foreach (Transform child in slot.transform)
                if (child.name.StartsWith("Lab", StringComparison.Ordinal)) child.gameObject.SetActive(false);
            if (slot.emptyMark != null) Component<CanvasGroup>(slot.emptyMark).alpha = 0;
            if (slot.selection != null)
            {
                At(slot.selection.transform, 3, 3, w-6, h-6);
                var mark = slot.selection.GetComponent<Image>(); if (mark != null) { mark.sprite = null; mark.color = new Color(0.5f, 1, 0.87f, 0.16f); mark.raycastTarget = false; }
            }
        }

        static void AuthorSupportingScreens()
        {
            var opening = Object.FindAnyObjectByType<OpeningScreenView>(FindObjectsInactive.Include);
            var level = Object.FindAnyObjectByType<LevelStartScreenView>(FindObjectsInactive.Include);
            var result = Object.FindAnyObjectByType<ResultScreenView>(FindObjectsInactive.Include);
            foreach (var root in new[] { opening.transform, level.transform, result.transform })
            {
                var background = root.Find("BackgroundImage")?.GetComponent<Image>();
                if (background != null)
                { background.sprite = Sprite("ProductionShell"); background.color = new Color32(40, 72, 80, 255); background.type = Image.Type.Simple; background.raycastTarget = false; }
                var ambience = root.Find("Ambience"); if (ambience != null) ambience.gameObject.SetActive(false);
            }
            foreach (var slot in opening.candidates) slot.portrait.SetShadowTint(new Color32(121, 190, 197, 255));
            result.portrait.SetShadowTint(new Color32(121, 190, 197, 255));
            var research = Object.FindAnyObjectByType<ResearchScreenView>(FindObjectsInactive.Include);
            var researchShell = research.transform.Find("LabResearchShell")?.GetComponent<Image>();
            if (researchShell != null) { researchShell.sprite = Sprite("ProductionShell"); researchShell.color = Cold; }
        }

        static void SkinChip(SkillChipView chip, float x, float y, float w, float h)
        {
            At(chip.transform, x, y, w, h);
            var image = Component<Image>(chip.gameObject); image.sprite = null; image.color = new Color32(26, 55, 64, 220); image.raycastTarget = true;
            At(chip.label.transform, 6, 7, w-12, h-14); Style(chip.label, 16, Ink);
            if (chip.icon != null) chip.icon.gameObject.SetActive(false);
            if (chip.reservedLabel != null) { At(chip.reservedLabel.transform, 3, h-18, w-6, 15); Style(chip.reservedLabel, 10, Ink); }
        }

        static FacilityLamp Lamp(Transform parent, string name, Rect bounds, Color color, float intensity, float phase)
        {
            var rect = Node(parent, name, bounds.x, bounds.y, bounds.width, bounds.height);
            var node = Node(_lights, name, 0, 0, 1, 1);
            var light = Component<Light2D>(node.gameObject); light.lightType = Light2D.LightType.Sprite; light.lightCookieSprite = Sprite("LampCone");
            light.blendStyleIndex = 0; light.color = color; light.intensity = intensity;
            var lamp = Component<FacilityLamp>(rect.gameObject); lamp.lightSource = light; lamp.color = color; lamp.intensity = intensity; lamp.phase = phase;
            light.enabled = parent.gameObject.activeInHierarchy;
            return lamp;
        }

        static FacilitySprite WorldArt(Transform parent, string key, Color tint, int order) => WorldArt(parent, key, key, tint, order, 0);
        static FacilitySprite WorldArt(Transform parent, string name, string key, Color tint, int order, float xOffset = 0)
        {
            var b = _layers[key].bounds;
            var node = Node(parent, name, b[0]+xOffset, b[1], b[2]-b[0], b[3]-b[1]);
            var view = Component<FacilitySprite>(node.gameObject); view.sprite = Sprite(key); view.tint = tint; view.order = order; view.material = _lit;
            return view;
        }

        static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath+name+".png");
        static T Component<T>(GameObject node) where T : UnityEngine.Component
        { var component = node.GetComponent<T>(); return component != null ? component : node.AddComponent<T>(); }
        static GameObject SceneNode(string name) => EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x => x.name == name) ?? new GameObject(name);
        static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
        {
            var child = parent.Find(name);
            if (child == null) { var node = new GameObject(name, typeof(RectTransform)); child = node.transform; child.SetParent(parent, false); }
            child.gameObject.SetActive(true); At(child, x, y, w, h); return (RectTransform)child;
        }
        static void At(Transform t, float x, float y, float w, float h) => LabSceneView.Place((RectTransform)t, x, y, w, h);
        static void Stretch(RectTransform rect, float padding)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one*padding; rect.offsetMax = Vector2.one*-padding; }
        static Image Solid(Transform parent, string name, Color color, float x, float y, float w, float h)
        {
            var node = Node(parent, name, x, y, w, h); var image = Component<Image>(node.gameObject); image.color = color; image.raycastTarget = false; return image;
        }
        static Image Picture(Transform parent, string name, string sprite, float x, float y, float w, float h, Color tint)
        { var image = Solid(parent, name, tint, x, y, w, h); image.sprite = Sprite(sprite); image.preserveAspect = false; return image; }
        static TMP_Text Label(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color)
        {
            var node = Node(parent, name, x, y, w, h); var text = Component<TextMeshProUGUI>(node.gameObject); Style(text, size, color); text.text = value; return text;
        }
        static void Style(TMP_Text text, float size, Color color)
        { text.font = _font; text.fontSize = size; text.color = color; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis; }
        static Button NewButton(Transform parent, string name, string title, float x, float y, float w, float h, float size)
        { var node = Node(parent, name, x, y, w, h); var button = Component<Button>(node.gameObject); SkinButton(button, title, x, y, w, h, size); return button; }
        static void SkinButton(Button button, string title, float x, float y, float w, float h, float size)
        {
            At(button.transform, x, y, w, h); button.gameObject.SetActive(true);
            var image = Component<Image>(button.gameObject); image.sprite = null; image.color = Back; image.raycastTarget = true; button.targetGraphic = image;
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(false);
            var label = button.transform.Find("FacilityLabel")?.GetComponent<TMP_Text>();
            if (label == null) label = Label(button.transform, "FacilityLabel", title, 3, 2, w-6, h-4, size, Ink);
            else { At(label.transform, 3, 2, w-6, h-4); Style(label, size, Ink); label.text = title; label.gameObject.SetActive(true); }
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f); colors.pressedColor = new Color(0.68f, 1, 0.91f); colors.disabledColor = new Color(0.45f, 0.6f, 0.6f, 0.7f); button.colors = colors;
        }
        static void Action(Button button, PresentationAction action) => Component<PresentationActionButton>(button.gameObject).Configure(null, action);
        static void SetClick(Button button, UnityEngine.Events.UnityAction action) { button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(action); }
    }
}
