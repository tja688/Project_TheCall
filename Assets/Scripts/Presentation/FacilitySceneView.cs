using System.Collections.Generic;
using System.Linq;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class FacilitySceneView : MonoBehaviour
    {
        public OperationScreenView operation;
        public FacilityPanorama panorama;
        public MonsterSlotView[] dormSlots;
        public TMP_Text dormCaption;
        public TMP_Text monitorSummary;
        public TMP_Text monitorEmpty;
        public TMP_Text monitorRow;
        public RectTransform monitorContent;
        public RectTransform shutter;
        public GameObject roomLock;
        public TMP_Text[] roomCaptions;
        public GameObject[] secondRoomArt;
        public RectTransform needle;
        public RectTransform collectorFluid;
        public FacilityLamp collectorLamp;
        public FacilityEnergyColumn[] energyColumns;
        public RectTransform[] motes;
        public Image[] seatLamps;
        public FacilityUpgradeView[] upgrades;
        public TMP_Text cageCaption;
        public TMP_Text geneCaption;
        public Button previousDorm;
        public Button nextDorm;
        public int EnergyArrivals { get; private set; }
        public int MonitoredCount { get; private set; }
        public int DormPage { get; private set; }
        readonly List<TMP_Text> _rows = new List<TMP_Text>();
        IReadOnlyList<ResidentStatus> _residents;
        PresentationSpriteCatalog _catalog;
        float _dial;
        float _targetDial;
        float _pulse;
        float _moteTime = 10;
        float _door = 1;
        bool _roomOpen;
        int _target = 1;
        int _shownEnergy;
        bool _energyPlayback;
        float _receiving;
        public float PresentedEnergy { get; private set; }
        public float DialProgress => _dial;

        public void Render(IArchitecture architecture, PresentationSpriteCatalog catalog)
        {
            _catalog = catalog;
            var run = architecture.SendQuery(new RunLedgerQuery());
            _residents = architecture.SendQuery(new FacilityStatusQuery());
            MonitoredCount = _residents.Count;
            var working = _residents.Count(r => r.Location == ResidentLocation.Extraction);
            var breeding = _residents.Count(r => r.Location == ResidentLocation.Breeding);
            var resting = _residents.Count(r => r.Location == ResidentLocation.Resting);
            var tired = _residents.Count(r => r.Monster.Mood == "疲倦" || r.Monster.Mood == "倦怠");
            monitorSummary.text = "全员状态 / " + MonitoredCount + "\n生产 " + working + "  培育 " + breeding + "  休息 " + resting + "\n疲劳提醒 " + tired;
            monitorEmpty.gameObject.SetActive(MonitoredCount == 0);
            if (_rows.Count == 0) _rows.Add(monitorRow);
            while (_rows.Count < _residents.Count)
            {
                var copy = Instantiate(monitorRow, monitorContent);
                copy.name = "ResidentStatus_" + _rows.Count;
                _rows.Add(copy);
            }
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                row.gameObject.SetActive(i < _residents.Count);
                if (i >= _residents.Count) continue;
                LabSceneView.Place(row.rectTransform, 10, i * 100, 286, 96);
                var status = _residents[i];
                var monster = status.Monster;
                var location = status.Location == ResidentLocation.Resting ? "宿舍休息" :
                    status.Location == ResidentLocation.Extraction ? "产能 " + (status.Seat+1).ToString("00") : "培育亲本";
                var history = monster.ConsecutiveWorkDays > 0 ? "连工 " + monster.ConsecutiveWorkDays + " 天" : "连休 " + monster.ConsecutiveRestDays + " 天";
                var color = monster.Mood == "倦怠" || monster.Mood == "疲倦" ? "#FFAC91" : "#B7E4CE";
                row.text = "<color=#FFFFFF>" + monster.DisplayName + "</color>\n<color=" + color + ">" + monster.Mood + "</color>  /  " + location + "\n" + history + "   心情能量 " + (monster.MoodEnergyDelta > 0 ? "+" : "") + monster.MoodEnergyDelta;
            }
            monitorContent.sizeDelta = new Vector2(monitorContent.sizeDelta.x, Mathf.Max(610, _residents.Count * 100));
            RenderDorm();
            foreach (var upgrade in upgrades) if (upgrade != null) upgrade.Render(run);
            var plans = architecture.SendQuery(new BreedingPlansQuery());
            LayoutBreeding(plans);
            _target = Mathf.Max(1, architecture.SendQuery(new LevelTargetQuery()).EnergyDue);
            _shownEnergy = architecture.SendQuery(new LevelEnergyQuery());
            _targetDial = Mathf.Clamp01(_shownEnergy / (float)_target);
            PresentedEnergy = _shownEnergy;
            LayoutExtraction();
            cageCaption.text = "收容 " + architecture.SendQuery(new MonsterCageQuery()).Count;
            geneCaption.text = "基因仓储  " + architecture.SendQuery(new GeneSlotsQuery()).Count + "/" + run.GeneCapacity;
            for (var i = 0; i < seatLamps.Length; i++)
            {
                var active = i < operation.extractionSlots.Length && operation.extractionSlots[i].gameObject.activeSelf;
                seatLamps[i].gameObject.SetActive(active);
                seatLamps[i].color = !string.IsNullOrEmpty(operation.extractionSlots[i].monsterId)
                    ? new Color32(165, 238, 233, 255) : new Color32(67, 104, 116, 255);
            }
        }

        public void LayoutExtraction()
        {
            var active = operation.extractionSlots.Count(s => s.gameObject.activeSelf);
            var step = 940f / Mathf.Max(1, active - 1);
            for (var i = 0; i < operation.extractionSlots.Length; i++)
                LabSceneView.Place((RectTransform)operation.extractionSlots[i].transform, 1990 + i * step, 687, 178, 290);
        }

        public void BeginEnergyPlayback(int initial)
        {
            _energyPlayback = true;
            ReceiveEnergy(initial, 0);
            if (energyColumns != null) foreach (var column in energyColumns) if (column != null) column.Show(0, 0);
        }

        public void ReceiveEnergy(float total, float receiving)
        {
            if (_receiving <= 0 && receiving > 0) EnergyArrivals++;
            PresentedEnergy = total;
            _shownEnergy = Mathf.RoundToInt(total);
            _targetDial = Mathf.Clamp01(total / _target);
            _dial = _targetDial;
            _receiving = receiving;
            if (collectorLamp != null) collectorLamp.Energy = receiving * 0.35f;
            UpdateEnergyInstruments();
        }

        public void EndEnergyPlayback()
        {
            _energyPlayback = false;
            _receiving = 0;
            if (collectorLamp != null) collectorLamp.Energy = 0;
            if (energyColumns != null) foreach (var column in energyColumns) if (column != null) column.Show(0, 0);
        }

        void UpdateEnergyInstruments()
        {
            if (needle != null) needle.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(70, -28, _dial));
            if (collectorFluid != null) collectorFluid.localScale = new Vector3(1, Mathf.Lerp(0.035f, 1, _dial), 1);
        }

        public void PageDorm(int delta)
        {
            DormPage += delta;
            RenderDorm();
        }

        void RenderDorm()
        {
            if (_residents == null) return;
            var resting = _residents.Where(r => r.Location == ResidentLocation.Resting).ToArray();
            var lastPage = Mathf.Max(0, (resting.Length - 1) / dormSlots.Length);
            DormPage = Mathf.Clamp(DormPage, 0, lastPage);
            for (var i = 0; i < dormSlots.Length; i++)
            {
                var index = DormPage * dormSlots.Length + i;
                var monster = index < resting.Length ? resting[index].Monster : null;
                var slot = dormSlots[i];
                slot.gameObject.SetActive(monster != null);
                var armed = operation.pointer.Armed;
                var selected = monster != null && armed.HasValue && armed.Value.Kind == PayloadKind.Monster && armed.Value.MonsterId == monster.Id;
                PresentationReadout.Monster(slot, monster, selected, _catalog);
                slot.GetComponent<OperationPayloadDrag>().session = operation.pointer;
                if (slot.title != null) slot.title.text = monster == null ? "" : monster.DisplayName + "  /  " + monster.Mood;
            }
            dormCaption.text = resting.Length == 0 ? "休息区暂时空置 / 将怪物拖回这里休息" :
                "休息 " + resting.Length + "  /  第 " + (DormPage+1) + " 页，共 " + (lastPage+1) + " 页   悬停查看，拖动调度";
            previousDorm.interactable = DormPage > 0;
            nextDorm.interactable = DormPage < lastPage;
        }

        void LayoutBreeding(IReadOnlyList<BreedingPlanView> plans)
        {
            _roomOpen = plans.Count > 1;
            roomLock.SetActive(!_roomOpen);
            foreach (var item in secondRoomArt) item.SetActive(_roomOpen);
            var cursor = 0;
            for (var room = 0; room < plans.Count; room++)
            {
                var count = plans[room].ParentIds.Count;
                var x = 1920 + (room == 0 ? 100 : 810);
                var step = count == 3 ? 190 : 325;
                for (var parent = 0; parent < count; parent++, cursor++)
                {
                    var slot = operation.breedingSlots[cursor];
                    LabSceneView.Place((RectTransform)slot.transform, x + parent * step, 234, count == 3 ? 172 : 210, 260);
                    LabSceneView.Place((RectTransform)slot.portrait.transform, 20, 43, count == 3 ? 132 : 170, 204);
                }
            }
            for (var i = 0; i < roomCaptions.Length; i++) roomCaptions[i].text = i < plans.Count ?
                "培育 " + (i+1) + " / " + plans[i].ParentIds.Count + " 亲本" : "槽位扩容 / 科研解锁";
        }

        public void EnergyShown(int total)
        {
            if (total == _shownEnergy) return;
            _shownEnergy = total;
            PresentedEnergy = total;
            _targetDial = Mathf.Clamp01(total / (float)_target);
            _pulse = 1;
            _moteTime = 0;
            if (collectorLamp != null) collectorLamp.Pulse = 1;
            EnergyArrivals++;
        }

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _pulse = Mathf.MoveTowards(_pulse, 0, dt * 2);
            if (!_energyPlayback) _dial = Mathf.Lerp(_dial, _targetDial, 1 - Mathf.Exp(-dt * 7));
            UpdateEnergyInstruments();
            _door = Mathf.MoveTowards(_door, _roomOpen ? 0 : 1, dt * 1.45f);
            shutter.localScale = new Vector3(1, Mathf.Max(0.001f, _door), 1);
            _moteTime += dt;
            for (var i = 0; i < motes.Length; i++)
            {
                var t = _moteTime * 1.4f - i * 0.06f;
                var live = !_energyPlayback && t >= 0 && t <= 1;
                motes[i].gameObject.SetActive(live);
                if (!live) continue;
                // Collected energy follows the authored horizontal pipe to the right-hand tank.
                motes[i].anchoredPosition = Vector2.Lerp(new Vector2(1920+420+i*75, -650), new Vector2(1920+1280, -715), t);
                motes[i].localScale = Vector3.one * Mathf.Lerp(1, 0.3f, t);
            }
        }

        void OnDisable()
        {
            EndEnergyPlayback();
            _pulse = 0;
            _moteTime = 10;
            foreach (var mote in motes) if (mote != null) mote.gameObject.SetActive(false);
        }
    }
}
