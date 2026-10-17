using System;
using System.Collections.Generic;
using System.Linq;
using QFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    // UI fixtures use injected resources; the separate recorded seven-day replay never does.
    public static class FacilityArtAcceptance
    {
        static OperationScreenView Op => Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
        static FacilitySceneView View => Op.lab.facility;
        static TheCallPresentation Presentation => Object.FindAnyObjectByType<TheCallPresentation>();
        static IArchitecture App => TheCallApp.Interface;
        static OperationPayloadDrag _source;
        static PointerEventData _gesture;
        static string _id;
        static int _count;
        static int _genes;
        static int _consumed;
        static int _gold;
        static int _ownedTools;
        static int _refreshes;
        static Vector3 _rejectedHome;
        static float _rejectedClosest;
        static int _rejectedSamples;
        static readonly List<float> Samples = new List<float>();

        static void Need(bool value, string why) { if (!value) throw new InvalidOperationException("FACILITY_FAIL: " + why); }
        static Vector2 Center(Transform t)
        { var r = (RectTransform)t; return RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center)); }
        static RaycastResult Hit(Vector2 point)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Need(hits.Count > 0, "No actual UI ray hit at " + point);
            return hits[0];
        }

        public static string Saved()
        {
            Need(!Application.isPlaying, "Saved check requires Edit Mode");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Need(scene.path == "Assets/Scenes/Main.unity" && !scene.isDirty, "Main not saved");
            Need(Op.pointer.panorama != null && View.dormSlots.Length == 6 && View.upgrades.Length == 4, "Incomplete bindings");
            Need(ShadowMonsterCatalog.Runtime.silhouettes.Length == 5, "Five delivered silhouettes required");
            Need(Object.FindObjectsByType<MonsterPortrait>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(p => p.UsesShadow), "Scene still uses assembly artwork");
            Need(Object.FindObjectsByType<FacilityLamp>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(l => l.lightSource != null), "Lamp binding lost");
            Need(Object.FindAnyObjectByType<Camera>().orthographic, "Camera is not orthographic");
            return "PASS saved Main, five silhouettes, six dorm seats, four deferred upgrades, 2D lamp bindings";
        }

        public static string Fixture(int count = 14)
        {
            PresentationFixtureAcceptance.Begin("capacity", count, "docs/audit/2026-10-17/facility/fixtures");
            var moods = new[] { "倦怠", "疲倦", "普通", "热情", "热爱" };
            var run = App.GetModel<RunModel>();
            for (var i = 0; i < run.Cage.Count; i++) run.Cage[i].SetMood(moods[i % moods.Length]);
            View.panorama.Focus(true, true);
            Presentation.Refresh();
            Canvas.ForceUpdateCanvases();
            Need(View.MonitoredCount == count && App.SendQuery(new FacilityStatusQuery()).Count == count, "Monitor count differs from ownership");
            Need(View.upgrades.All(u => !u.artwork.activeSelf), "Unspecified upgrade appeared by default");
            Need(Op.cageSlots.Where(s => s.gameObject.activeInHierarchy).All(s => s.portrait.HasCreature && s.portrait.UsesShadow), "Cage portrait missing");
            return "PASS fixture ownership, five mood states, shadow portraits, deferred upgrades stay hidden";
        }

        public static string PanBegin()
        {
            Samples.Clear();
            var point = new Vector2(Screen.width * 0.46f, Screen.height * 0.39f);
            _gesture = new PointerEventData(EventSystem.current) { position = point, pressPosition = point, button = PointerEventData.InputButton.Left };
            var target = Hit(point).gameObject;
            ExecuteEvents.ExecuteHierarchy(target, _gesture, ExecuteEvents.pointerDownHandler);
            _gesture.position += new Vector2(Screen.width * 0.19f, 0);
            ExecuteEvents.Execute(Op.gameObject, _gesture, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(Op.gameObject, _gesture, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(Op.gameObject, _gesture, ExecuteEvents.endDragHandler);
            Need(View.panorama.Moving && !View.panorama.Production, "Background swipe did not select dorm");
            EditorApplication.update -= SamplePan; EditorApplication.update += SamplePan;
            return "PASS actual background ray and horizontal drag initiate continuous snap to dorm";
        }
        static void SamplePan()
        {
            if (!Application.isPlaying) { EditorApplication.update -= SamplePan; return; }
            Samples.Add(View.panorama.content.anchoredPosition.x);
            if (!View.panorama.Moving) EditorApplication.update -= SamplePan;
        }
        public static string PanCheck(bool production)
        {
            Need(!View.panorama.Moving && View.panorama.Production == production, "Pan did not dock");
            Need(Mathf.Abs(View.panorama.content.anchoredPosition.x - (production ? -1920 : 0)) < 0.01f, "Intermediate resting camera state");
            if (Samples.Count > 0) Need(Samples.Distinct().Count() > 3, "Transition did not advance through multiple rendered frames");
            return "PASS camera docks exactly at " + (production ? "production" : "dorm") + "; sampled " + Samples.Count + " frames";
        }

        public static string CrossBegin(bool fromDorm)
        {
            var slot = fromDorm ? View.dormSlots.First(s => s.gameObject.activeInHierarchy) : Op.extractionSlots.First(s => !string.IsNullOrEmpty(s.monsterId));
            BeginDrag(slot);
            _count = App.SendQuery(new FacilityStatusQuery()).Count;
            _gesture.position = new Vector2(Screen.width * (fromDorm ? 0.99f : 0.01f), Screen.height * 0.45f);
            _source.OnDrag(_gesture);
            var ghost = Op.dragLayer.Find("MonsterGhost").GetComponent<MonsterPortrait>();
            Need(ghost.UsesShadow && ghost.ShadowSprite == slot.portrait.ShadowSprite && ghost.GetComponent<MonsterLightBody>() == null, "Ghost uses old ragdoll or wrong silhouette");
            return "PASS real monster pointer drag creates matching whole silhouette and waits at room edge";
        }

        static void BeginDrag(MonsterSlotView slot)
        {
            var point = Center(slot.transform);
            var actual = Hit(point).gameObject.GetComponentInParent<MonsterSlotView>();
            Need(actual == slot, "Source is not exposed to actual pointer rays: " + slot.name);
            _source = slot.GetComponent<OperationPayloadDrag>(); _id = slot.monsterId;
            _gesture = new PointerEventData(EventSystem.current) { position = point, pressPosition = point, button = PointerEventData.InputButton.Left };
            _source.OnPointerDown(_gesture); _source.OnBeginDrag(_gesture);
        }

        public static string CrossFinish(bool toProduction)
        {
            Need(!View.panorama.Moving && View.panorama.Production == toProduction, "Edge hold did not switch rooms");
            _gesture.position = toProduction ? Center(Op.extractionSlots[0].transform) : new Vector2(Screen.width * 0.91f, Screen.height * 0.65f);
            Need(Hit(_gesture.position).gameObject.GetComponentInParent<OperationZone>() != null, "Destination lacks semantic landing");
            _source.OnDrag(_gesture); _source.OnEndDrag(_gesture);
            var status = App.SendQuery(new FacilityStatusQuery()).Single(r => r.Monster.Id == _id);
            Need(status.Location == (toProduction ? ResidentLocation.Extraction : ResidentLocation.Resting), "Cross-room drop failed in rules");
            Need(View.MonitoredCount == _count && App.SendQuery(new FacilityStatusQuery()).Count == _count, "Moving monster duplicated or lost monitor ownership");
            return "PASS cross-room drag commits through actual landing and updates monitor without duplicating ownership";
        }

        public static string RejectCrossDrop()
        {
            Need(View.panorama.Production && !View.panorama.Moving, "Cross to production before rejecting the drop");
            var sourceRect = (RectTransform)_source.GetComponent<MonsterSlotView>().portrait.transform;
            _rejectedHome = sourceRect.TransformPoint(sourceRect.rect.center);
            _rejectedClosest = float.MaxValue; _rejectedSamples = 0;
            _gesture.position = new Vector2(Screen.width*0.47f, Screen.height*0.46f);
            Need(Hit(_gesture.position).gameObject.GetComponentInParent<OperationZone>().Place == LandingPlace.EmptySpace, "Reject fixture is not empty space");
            _source.OnDrag(_gesture); _source.OnEndDrag(_gesture);
            Need(App.SendQuery(new FacilityStatusQuery()).Single(r=>r.Monster.Id==_id).Location == ResidentLocation.Resting, "Rejected drop changed ownership");
            EditorApplication.update -= SampleRejected; EditorApplication.update += SampleRejected;
            return "PASS invalid cross-room drop leaves the monster resting and starts return toward its actual moved origin";
        }
        static void SampleRejected()
        {
            if (!Application.isPlaying) { EditorApplication.update -= SampleRejected; return; }
            var ghost = Op.dragLayer.Find("MonsterGhost");
            if (ghost == null) { EditorApplication.update -= SampleRejected; return; }
            _rejectedClosest = Mathf.Min(_rejectedClosest, Vector3.Distance(ghost.position, _rejectedHome));
            _rejectedSamples++;
        }
        public static string RejectedCheck()
        {
            Need(_rejectedSamples > 2 && _rejectedClosest < 5, "Ghost returned to the old screen coordinate instead of its moved source: " + _rejectedClosest);
            Need(Op.dragLayer.Find("MonsterGhost") == null, "Rejected ghost leaked");
            var group = _source.GetComponent<MonsterSlotView>().portrait.GetComponent<CanvasGroup>();
            Need(group == null || Mathf.Approximately(group.alpha, 1), "Source stayed dim after rejected drop");
            return "PASS rejected ghost follows the moved origin, fades out and restores its source; " + _rejectedSamples + " frames sampled";
        }

        public static string FillProduction()
        {
            View.panorama.Focus(true, true);
            for (var i = 0; i < Op.extractionSlots.Length; i++)
            {
                if (!string.IsNullOrEmpty(Op.extractionSlots[i].monsterId)) continue;
                var cage = App.SendQuery(new MonsterCageQuery());
                if (cage.Count == 0) break;
                App.SendCommand(new PlaceMonsterCommand(cage[0].Id, OperationArea.Extraction, i));
            }
            Presentation.Refresh();
            Need(Op.extractionSlots.All(s => s.portrait.HasCreature), "Production silhouettes missing");
            return "PASS fixture production seats display six owned whole silhouettes";
        }

        public static string RecyclerHover()
        {
            var point = Center(Op.lab.recycler.transform);
            Need(Hit(point).gameObject.GetComponentInParent<LabRecycler>() == Op.lab.recycler, "Mouth not raycastable");
            Op.lab.recycler.OnPointerEnter(new PointerEventData(EventSystem.current) { position = point });
            return "PASS actual recycler ray reaches mouth";
        }
        public static string RecyclerCheck()
        { Need(Op.lab.recycler.Openness > 0.95f, "Recycler did not open"); return "PASS independent jaws animate open"; }
        public static string Recycle()
        {
            var slot = Op.cageSlots.First(s => s.gameObject.activeInHierarchy && !string.IsNullOrEmpty(s.monsterId));
            _genes = App.SendQuery(new GeneSlotsQuery()).Count; _count = App.SendQuery(new FacilityStatusQuery()).Count;
            _consumed = Op.lab.recycler.ConsumedCount;
            BeginDrag(slot);
            _gesture.position = Center(Op.lab.recycler.transform); _source.OnDrag(_gesture); _source.OnEndDrag(_gesture);
            Need(App.SendQuery(new GeneSlotsQuery()).Count == _genes+1, "No extracted gene");
            Need(App.SendQuery(new FacilityStatusQuery()).Count == _count-1 && Op.lab.recycler.ConsumedCount == _consumed+1, "Recycle ownership or animation mismatch");
            return "PASS real recycle drop extracts one gene, retires one monster and triggers one chew";
        }

        public static string Expanded()
        {
            var plans = App.SendQuery(new BreedingPlansQuery());
            Need(plans.Count == 2 && !View.roomLock.activeSelf && View.shutter.localScale.y < 0.01f, "Expanded room still blocked");
            Need(Op.breedingSlots.Count(s => s.gameObject.activeInHierarchy) == plans.Sum(p => p.ParentIds.Count), "Parent slot mapping differs from rules");
            foreach (var slot in Op.breedingSlots.Where(s => s.gameObject.activeInHierarchy))
                Need(Hit(Center(slot.transform)).gameObject.GetComponentInParent<MonsterSlotView>() == slot, "Parent seat obscured");
            return "PASS actual research opens shutter and exposes all correctly mapped parent seats";
        }

        public static string PreviewUpgrades(bool enabled)
        {
            foreach (var gate in View.upgrades) gate.previewUnlocked = enabled;
            Presentation.Refresh();
            Need(View.upgrades.All(u => u.artwork.activeSelf == enabled), "Upgrade artwork visibility mismatch");
            return "PASS authoring-only upgrade preview " + enabled;
        }

        public static string PrepareShop()
        {
            Fixture(8);
            var level = App.GetModel<LevelModel>();
            level.BeginLevel(1, 2); level.FitExtraction(6); level.ApplyShape(1, 2);
            var id = App.GetModel<RunModel>().Cage[0].Id;
            App.GetModel<RunModel>().Find(id).SetMood("普通");
            App.SendCommand(new PlaceMonsterCommand(id, OperationArea.Extraction, 0));
            App.GetModel<RunModel>().AddGold(150);
            View.panorama.Focus(true, true); Presentation.Refresh();
            return HandbookAcceptance.ClickVisible(Op.nextDayButton.name);
        }
        public static string ShopCheck()
        {
            Need(App.SendQuery(new RunPhaseQuery()) == RunPhase.Shop, "Settlement did not reach shop");
            var shop = Object.FindAnyObjectByType<ShopScreenView>();
            Need(shop.facility != null && shop.cards.Any(c => c.gameObject.activeInHierarchy), "Shop stock invisible");
            return "PASS settlement opens authored shop with actual stock";
        }
        public static string BuyTool()
        {
            var shop = Object.FindAnyObjectByType<ShopScreenView>();
            Need(shop.facility.ToolsSelected, "Select the tool category and let it render first");
            var card = shop.cards.First(c => c.gameObject.activeInHierarchy && !string.IsNullOrEmpty(c.toolName));
            var name = card.toolName;
            Need(Hit(Center(card.buyButton.transform)).gameObject.GetComponentInParent<Button>() == card.buyButton, "Buy button occluded");
            _ownedTools = App.SendQuery(new RunLedgerQuery()).Tools.Count;
            ExecuteEvents.Execute(card.buyButton.gameObject, new PointerEventData(EventSystem.current) { position=Center(card.buyButton.transform), button=PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            Need(App.SendQuery(new RunLedgerQuery()).Tools.Count == _ownedTools+1 && App.SendQuery(new RunLedgerQuery()).Tools.Contains(name), "Tool purchase failed");
            return "PASS tool category, actual exposed buy control, purchase and owned inventory refresh";
        }
        public static string RefreshShop()
        {
            var shop = Object.FindAnyObjectByType<ShopScreenView>();
            _gold = App.SendQuery(new RunLedgerQuery()).Gold;
            var price = App.SendQuery(new ShelfQuery()).NextRefreshPrice;
            _refreshes = shop.facility.RefreshAnimations;
            HandbookAcceptance.ClickVisible(shop.refreshButton.name);
            Need(App.SendQuery(new RunLedgerQuery()).Gold == _gold-price && shop.facility.RefreshAnimations == _refreshes+1, "Paid refresh did not animate once");
            return "PASS refresh lever spends exact shelf quote and starts one physical animation";
        }
        public static string SellMonster()
        {
            var shop = Object.FindAnyObjectByType<ShopScreenView>();
            Need(shop.sellPage.activeInHierarchy, "Select the sell page and let it render first");
            var slot = shop.sellSlots.First(s => s.gameObject.activeInHierarchy);
            _count = App.SendQuery(new MonsterCageQuery()).Count;
            _id = slot.monsterId;
            Need(Hit(Center(slot.transform)).gameObject.GetComponentInParent<MonsterSlotView>() == slot, "Sell card hidden");
            Need(Hit(Center(slot.transform)).gameObject.GetComponentInParent<Button>() == slot.button, "Visible sell card does not route to the sale button");
            ExecuteEvents.Execute(slot.button.gameObject, new PointerEventData(EventSystem.current) { position=Center(slot.transform), button=PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            Need(App.SendQuery(new MonsterCageQuery()).Count == _count-1 && !App.SendQuery(new MonsterCageQuery()).Any(m => m.Id == _id), "Sale failed");
            return "PASS original sell command retires selected monster from real inventory";
        }

        public static string TailDrop()
        {
            var slot = Op.cageSlots.Last(s => s.gameObject.activeInHierarchy && !string.IsNullOrEmpty(s.monsterId));
            var count = App.SendQuery(new FacilityStatusQuery()).Count;
            BeginDrag(slot);
            _gesture.position = Center(Op.extractionSlots[0].transform);
            _source.OnDrag(_gesture); _source.OnEndDrag(_gesture);
            Need(Op.extractionSlots[0].monsterId == _id && View.MonitoredCount == count, "Tail inventory drop failed");
            return "PASS 31-monster tail is clipped, visible to rays and drags through the original command";
        }

        public static string UpgradeBindings()
        {
            var run = App.SendQuery(new RunLedgerQuery());
            var tool = View.upgrades[0]; tool.toolName = run.Tools[0];
            var tech = View.upgrades[1]; tech.techName = run.UnlockedTech[0];
            Presentation.Refresh();
            Need(tool.artwork.activeSelf && tech.artwork.activeSelf, "Owned tool / technology did not reveal bound artwork");
            tool.toolName = tech.techName = "";
            Presentation.Refresh();
            Need(!tool.artwork.activeSelf && !tech.artwork.activeSelf, "Clearing fixture gates left unlocked artwork");
            return "PASS real owned-tool and researched-technology projections reveal their configured art";
        }

        public static string Lamps(bool enabled)
        {
            foreach (var lamp in Op.GetComponentsInChildren<FacilityLamp>(true)) lamp.enabled = enabled;
            return "PASS real Light2D sources " + (enabled ? "enabled" : "disabled");
        }
    }
}
