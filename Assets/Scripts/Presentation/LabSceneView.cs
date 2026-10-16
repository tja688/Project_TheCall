using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    // Painting and motion only. All capacity, energy and parent data comes from ScreenView queries.
    public sealed class LabSceneView : MonoBehaviour
    {
        public OperationScreenView operation;
        public Image[] lamps;
        public Image collectorGlow;
        public RectTransform needle;
        public CanvasGroup room2Interior;
        public GameObject room2Lock;
        public TMP_Text cageCount;
        public TMP_Text warehouseCount;
        public LabRecycler recycler;
        public ScrollRect cageScroll;
        public RectTransform[] motes;
        public TMP_Text[] parentCaptions;
        float _ratio;
        float _dial;
        float _pulse;
        float _roomAlpha;
        bool _roomOpen;
        int _energy;
        int _target = 1;
        float _moteTime = 10f;
        public int EnergyArrivals { get; private set; }

        public void Render(IReadOnlyList<BreedingPlanView> plans, int energy, int target, int cage, int genes)
        {
            _target = Mathf.Max(1, target);
            _energy = energy;
            _ratio = Mathf.Clamp01(energy / (float)_target);
            _roomOpen = plans.Count > 1;
            if (room2Lock != null) room2Lock.SetActive(!_roomOpen);
            if (cageCount != null) cageCount.text = "收容笼 · " + cage;
            if (warehouseCount != null) warehouseCount.text = "基因仓库 · " + genes + "/4";
            var cursor = 0;
            for (var room = 0; room < plans.Count; room++)
            {
                var count = plans[room].ParentIds.Count;
                var left = room == 0 ? 166f : 846f;
                var width = room == 0 ? 510f : 530f;
                var socketWidth = width / count;
                for (var seat = 0; seat < count; seat++, cursor++)
                {
                    if (cursor >= operation.breedingSlots.Length) break;
                    var slot = operation.breedingSlots[cursor];
                    var rect = (RectTransform)slot.transform;
                    Place(rect, left + seat * socketWidth, 254, socketWidth - 10, 218);
                    if (slot.portrait != null) Place((RectTransform)slot.portrait.transform, 4, 30, socketWidth - 18, 190);
                }
            }
            if (parentCaptions != null)
                for (var i = 0; i < parentCaptions.Length; i++)
                    if (parentCaptions[i] != null) parentCaptions[i].text = "培育 " + (i + 1) + (i < plans.Count ? "  /  " + plans[i].ParentIds.Count + " 亲本" : "  /  待解锁");
        }

        public void EnergyShown(int total)
        {
            if (total == _energy) return;
            _energy = total;
            _ratio = Mathf.Clamp01(total / (float)_target);
            _pulse = 1f;
            _moteTime = 0f;
            EnergyArrivals++;
        }

        public void PageCage(int direction)
        {
            if (cageScroll is LabCageLift lift) lift.Page(direction);
        }

        void Update()
        {
            var time = Time.unscaledTime;
            var dt = Time.unscaledDeltaTime;
            _pulse = Mathf.MoveTowards(_pulse, 0, dt * 2.5f);
            _dial = Mathf.Lerp(_dial, _ratio, 1 - Mathf.Exp(-dt * 7));
            if (needle != null) needle.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(62, -22, _dial) + _pulse * Mathf.Sin(time * 40) * 2);
            if (collectorGlow != null) collectorGlow.color = new Color(1, 1, 1, 0.22f + _pulse * 0.65f + Mathf.Sin(time * 2) * 0.04f);
            _roomAlpha = Mathf.MoveTowards(_roomAlpha, _roomOpen ? 1 : 0, dt * 2);
            if (room2Interior != null) room2Interior.alpha = _roomAlpha;
            if (lamps != null)
                for (var i = 0; i < lamps.Length; i++)
                {
                    if (lamps[i] == null) continue;
                    var cycle = Mathf.Repeat(time + i * 3.7f, 13.7f + i);
                    var flicker = cycle < 0.24f ? (Mathf.Sin(cycle * 97) > 0 ? 0.12f : 0.55f) : 0.5f + Mathf.Sin(time * 1.5f + i) * 0.045f;
                    if (i == 1 && !_roomOpen) flicker *= 0.2f;
                    lamps[i].color = new Color(1, 1, 1, flicker);
                }
            _moteTime += dt;
            if (motes == null) return;
            for (var i = 0; i < motes.Length; i++)
            {
                if (motes[i] == null) continue;
                var t = _moteTime * 1.6f - i * 0.055f;
                var live = t >= 0 && t <= 1;
                motes[i].gameObject.SetActive(live);
                if (!live) continue;
                var start = new Vector2(1180 - i * 35, -790 - (i % 3) * 20);
                var end = new Vector2(1355, -775);
                motes[i].anchoredPosition = Vector2.Lerp(start, end, t) + new Vector2(0, Mathf.Sin(t * Mathf.PI) * (45 + i * 8));
                motes[i].localScale = Vector3.one * Mathf.Lerp(1, 0.25f, t);
            }
        }

        void OnDisable()
        {
            _pulse = 0;
            _moteTime = 10;
            _roomAlpha = 0;
            if (room2Interior != null) room2Interior.alpha = 0;
            if (motes != null) foreach (var mote in motes) if (mote != null) mote.gameObject.SetActive(false);
        }

        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }
}
