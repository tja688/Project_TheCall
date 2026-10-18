using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TheCall.Scoring
{
    public sealed partial class ScoringShow
    {
        const float CaptionRise = 0.22f;
        const float CaptionHold = 0.58f;
        const float ShatterSeconds = 0.46f;
        const float TransferSeconds = 0.78f;
        static readonly Color EnergyInk = new Color32(190, 255, 235, 255);
        static readonly Color SupportInk = new Color32(255, 213, 145, 255);
        int _pendingSeat = -1;
        int _pendingEnergy;
        float _pendingFill;
        float _seatCapacity = 1;
        bool _draining;
        Caption _energyCaption;

        public string EnergyPhase { get; private set; } = "idle";
        public int ActiveSlot { get; private set; } = -1;
        public int DisplayedSkillEnergy { get; private set; }
        public float TransferProgress { get; private set; }
        public int ShardCount { get; private set; }
        public float PlayPace => _pace;

        sealed class Caption
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Value;
            public TextMeshProUGUI Prefix;
        }

        sealed class Shard
        {
            public ScoringGlyphShard Graphic;
            public Vector2 Start;
            public Vector2 Scatter;
        }

        void BeginEnergy()
        {
            _shown = _stage.InitialEnergy;
            _stage.Facility?.BeginEnergyPlayback(_shown);
            var totals = new Dictionary<int, int>();
            foreach (var item in _tape.Cues)
                if (item is PopCue pop)
                {
                    totals.TryGetValue(pop.SlotIndex, out var sum);
                    totals[pop.SlotIndex] = sum + pop.Energy;
                    _seatCapacity = Mathf.Max(_seatCapacity, sum + pop.Energy);
                }
        }

        void EndEnergy()
        {
            _stage.Facility?.EndEnergyPlayback();
            _energyCaption = null;
            _draining = false;
        }

        IEnumerator Pop(PopCue cue)
        {
            ActiveSlot = cue.SlotIndex;
            DisplayedSkillEnergy = cue.QuotedEnergy;
            if (SkipWait())
            {
                StoreEnergy(cue.SlotIndex, cue.Energy);
                yield break;
            }

            EnergyPhase = "skill";
            var column = Column(cue.SlotIndex);
            if (column != null) column.Show(_pendingFill, Mathf.Max(column.Glow, 0.18f));
            var portrait = PortraitAt(cue.SlotIndex);
            portrait?.SetSpringMotion(-12, 8);
            _energyCaption = MakeCaption(cue.SkillName, "能量 " + Signed(cue.QuotedEnergy), HeadOf(cue.SlotIndex), EnergyInk, true);
            yield return Raise(_energyCaption);
            portrait?.RestoreMotion();
            yield return Wait(CaptionHold);

            foreach (var beat in cue.Beats)
            {
                if (SkipWait()) break;
                if (beat.Role == BeatRole.Quote || beat.Role == BeatRole.Energy || beat.Role == BeatRole.Writeback) continue;
                yield return MergeEffect(cue.SlotIndex, beat);
            }

            DisplayedSkillEnergy = cue.Energy;
            _energyCaption.Value.text = EnergySigned(cue.Energy);
            if (cue.Energy == 0)
            {
                EnergyPhase = "zero";
                yield return FadeCaption(_energyCaption);
                if (column != null && _pendingEnergy == 0) column.Show(0, 0);
            }
            else
            {
                EnergyPhase = "charge";
                var before = _pendingFill;
                var after = FillFor(_pendingEnergy + cue.Energy);
                var target = column != null ? Local(column.Surface) : HeadOf(cue.SlotIndex) + new Vector2(-60, -155);
                yield return Shatter(_energyCaption, target, t => column?.Show(Mathf.Lerp(before, after, t), Mathf.Lerp(0.2f, 0.88f, t)));
                StoreEnergy(cue.SlotIndex, cue.Energy);
                EnergyPhase = "charged";
                yield return Wait(0.12f);
            }
            _energyCaption = null;
            if (cue.Writeback != 0 && !SkipWait())
            {
                var writeback = MakeCaption(cue.SkillName + " / 永久", "能量值 " + Signed(cue.Writeback), HeadOf(cue.SlotIndex), new Color32(176, 204, 255, 255));
                yield return Raise(writeback);
                yield return Wait(0.2f);
                yield return FadeCaption(writeback);
            }
            RestoreSlot(cue.SlotIndex);
        }

        IEnumerator MergeEffect(int subject, ScoreBeat beat)
        {
            var actor = string.IsNullOrEmpty(beat.SourceToolId) ? ActorOf(subject, beat.SourceMonsterId) : -1;
            var at = actor >= 0 ? HeadOf(actor) : HeadOf(subject) + new Vector2(0, 110);
            if (!string.IsNullOrEmpty(beat.SourceToolId))
                foreach (var tool in _stage.BoardRoot.GetComponentsInChildren<ToolArtView>())
                {
                    if (tool.toolName != beat.SourceToolId) continue;
                    var rect = (RectTransform)tool.transform;
                    at = Local(rect.TransformPoint(new Vector2(rect.rect.center.x, rect.rect.yMin))) + Vector2.down * 64;
                    break;
                }
            if (actor >= 0) PortraitAt(actor)?.SetSpringMotion(-9, 6);
            var text = beat.Role == BeatRole.Side ? (beat.Caption ?? "侧向") + "怪物 " + beat.Text :
                (actor >= 0 && actor != subject ? "其他怪物能量值 " : "能量值 ") + beat.Text;
            var ink = beat.Text.StartsWith("-", StringComparison.Ordinal) ? (Color)new Color32(255, 159, 146, 255) : SupportInk;
            var effect = MakeCaption(string.IsNullOrEmpty(beat.Caption) ? "效果" : beat.Caption, text, at, ink);
            EnergyPhase = "support";
            yield return Raise(effect);
            yield return Wait(CaptionHold);
            if (actor >= 0) PortraitAt(actor)?.RestoreMotion();
            EnergyPhase = "merge";
            yield return Shatter(effect, NumberPoint(_energyCaption.Value));
            DisplayedSkillEnergy = beat.EnergyAfter;
            _energyCaption.Value.text = EnergySigned(DisplayedSkillEnergy);
            EnergyPhase = "number";
            var value = _energyCaption.Value.rectTransform;
            yield return Animate(0.23f, t => value.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * 0.24f));
            value.localScale = Vector3.one;
            yield return Wait(0.12f);
        }

        void StoreEnergy(int seat, int energy)
        {
            if (energy == 0) return;
            _pendingSeat = seat;
            _pendingEnergy += energy;
            _pendingFill = FillFor(_pendingEnergy);
            Column(seat)?.Show(_pendingFill, 0.88f);
        }

        float FillFor(int energy) => energy <= 0 ? 0 : Mathf.Clamp01(0.18f + 0.82f * energy / _seatCapacity);

        void BeginDrain()
        {
            if (_pendingEnergy == 0) return;
            var seat = _pendingSeat;
            var amount = _pendingEnergy;
            var fill = _pendingFill;
            _pendingSeat = -1;
            _pendingEnergy = 0;
            _pendingFill = 0;
            _draining = true;
            StartCoroutine(Drain(seat, amount, fill));
        }

        IEnumerator FlushEnergy()
        {
            while (_draining) yield return null;
            BeginDrain();
            while (_draining) yield return null;
        }

        IEnumerator Drain(int seat, int amount, float fill)
        {
            var column = Column(seat);
            var from = _shown;
            var start = column != null ? Local(column.fluid.TransformPoint(new Vector3(column.fluid.rect.center.x, 0, 0))) : HeadOf(seat) + new Vector2(-50, -185);
            var end = _stage.Facility != null && _stage.Facility.collectorFluid != null ?
                Local(_stage.Facility.collectorFluid.TransformPoint(new Vector3(_stage.Facility.collectorFluid.rect.center.x, 0, 0))) : CurrentPoint();
            var particles = new UnityEngine.UI.Image[18];
            for (var i = 0; i < particles.Length; i++)
            {
                var mote = CreateImage("ScoringTransferMote", _blocker);
                mote.rectTransform.sizeDelta = new Vector2(i % 3 == 0 ? 8 : 4, 4);
                mote.color = EnergyInk;
                particles[i] = mote;
            }
            yield return Animate(TransferSeconds, t =>
            {
                TransferProgress = t;
                // An initial travelling front reaches the tank before the first energy is received.
                var received = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.25f, 1, t));
                column?.Show(fill * (1 - received), 0.88f * (1 - received));
                var total = from + amount * received;
                var flow = t > 0.25f && t < 1 ? Mathf.Sin(received * Mathf.PI) : 0;
                _stage.Facility?.ReceiveEnergy(total, flow);
                if (_current != null) _current.text = Mathf.RoundToInt(total).ToString();
                for (var i = 0; i < particles.Length; i++)
                {
                    var delay = i * 0.75f / (particles.Length - 1);
                    var p = Mathf.InverseLerp(delay, delay + 0.25f, t);
                    particles[i].gameObject.SetActive(p > 0 && p < 1);
                    var path = Vector2.Lerp(start, end, p);
                    path.y += Mathf.Sin(p * Mathf.PI) * (7 + i % 3 * 4);
                    particles[i].rectTransform.anchoredPosition = path;
                    particles[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1, 0.35f, p);
                }
            });
            foreach (var mote in particles) if (mote != null) Destroy(mote.gameObject);
            column?.Show(0, 0);
            AddShown(amount);
            _stage.Facility?.ReceiveEnergy(_shown, 0);
            _draining = false;
        }

        IEnumerator Animate(float seconds, Action<float> sample)
        {
            var elapsed = 0f;
            sample(0);
            while (elapsed < seconds && !SkipWait())
            {
                elapsed += Time.unscaledDeltaTime * _pace;
                sample(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            sample(1);
        }

        Caption MakeCaption(string title, string value, Vector2 at, Color ink, bool main = false)
        {
            var root = new GameObject(main ? "ScoringEnergyCaption" : "ScoringEffectCaption", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(_blocker, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(292, 82);
            // Leave the right tank and controls free. The first seat clears the compact gauge.
            at = PlaceCaption(at, main);
            rect.anchoredPosition = at;
            var back = CreateImage("CaptionBacking", rect);
            Stretch(back.rectTransform);
            back.color = new Color(0.025f, 0.075f, 0.09f, 0.92f);
            var rule = CreateImage("CaptionRule", rect);
            rule.rectTransform.anchoredPosition = new Vector2(0, -41);
            rule.rectTransform.sizeDelta = new Vector2(288, 2);
            rule.color = new Color(ink.r, ink.g, ink.b, 0.65f);
            var name = Spawn(title, Vector2.zero);
            name.transform.SetParent(rect, false);
            name.rectTransform.anchoredPosition = new Vector2(0, 21);
            name.rectTransform.sizeDelta = new Vector2(280, 28);
            name.fontSize = 23;
            name.color = new Color32(203, 226, 224, 255);
            var number = Spawn(value, Vector2.zero);
            number.transform.SetParent(rect, false);
            number.rectTransform.anchoredPosition = new Vector2(0, -14);
            number.rectTransform.sizeDelta = new Vector2(284, 44);
            TextMeshProUGUI prefix = null;
            if (main)
            {
                prefix = Spawn("能量", Vector2.zero);
                prefix.transform.SetParent(rect, false);
                prefix.rectTransform.anchoredPosition = new Vector2(-43, -14);
                prefix.rectTransform.sizeDelta = new Vector2(68, 44);
                prefix.fontSize = 26;
                prefix.color = ink;
                number.text = EnergySigned(DisplayedSkillEnergy);
                number.rectTransform.anchoredPosition = new Vector2(43, -14);
                number.rectTransform.sizeDelta = new Vector2(104, 44);
            }
            number.fontSize = main ? 36 : 23;
            if (main) { number.enableAutoSizing = true; number.fontSizeMin = 18; number.fontSizeMax = 36; }
            number.color = ink;
            return new Caption { Root = rect, Group = root.GetComponent<CanvasGroup>(), Title = name, Value = number, Prefix = prefix };
        }

        Vector2 PlaceCaption(Vector2 preferred, bool main)
        {
            var left = _blocker.rect.xMin + 160;
            var right = Mathf.Max(left, _blocker.rect.xMax - 800);
            var bottom = _blocker.rect.yMin + 155;
            var top = Mathf.Max(bottom, _blocker.rect.yMax - 195);
            preferred.x = Mathf.Clamp(preferred.x, left, right);
            preferred.y = Mathf.Clamp(preferred.y, bottom, top);
            if (main || _energyCaption == null) return preferred;
            var primary = new Rect(_energyCaption.Root.anchoredPosition - new Vector2(152, 47), new Vector2(304, 94));
            var gauge = new Rect();
            if (_stage.Facility != null && _stage.Facility.needle != null)
            {
                var corners = new Vector3[4];
                ((RectTransform)_stage.Facility.needle.parent).GetWorldCorners(corners);
                var min = Local(corners[0]);
                var max = Local(corners[2]);
                gauge = Rect.MinMaxRect(min.x - 6, min.y - 6, max.x + 6, max.y + 6);
            }
            var candidates = new[] { preferred, preferred + Vector2.up * 104,
                preferred + Vector2.right * 312, preferred + Vector2.left * 312,
                _energyCaption.Root.anchoredPosition + Vector2.right * 312,
                _energyCaption.Root.anchoredPosition + Vector2.left * 312 };
            foreach (var candidate in candidates)
            {
                var at = new Vector2(Mathf.Clamp(candidate.x, left, right), Mathf.Clamp(candidate.y, bottom, top));
                var bounds = new Rect(at - new Vector2(146, 41), new Vector2(292, 82));
                if (!bounds.Overlaps(primary) && !bounds.Overlaps(gauge)) return at;
            }
            return preferred;
        }

        IEnumerator Raise(Caption caption)
        {
            var end = caption.Root.anchoredPosition;
            yield return Animate(CaptionRise, t =>
            {
                var ease = 1 - Mathf.Pow(1 - t, 3);
                caption.Root.anchoredPosition = end + Vector2.down * (1 - ease) * 24;
                caption.Root.localScale = Vector3.one * Mathf.Lerp(0.92f, 1, ease);
                caption.Group.alpha = ease;
            });
        }

        IEnumerator FadeCaption(Caption caption)
        {
            var start = caption.Root.anchoredPosition;
            yield return Animate(0.3f, t =>
            {
                caption.Root.anchoredPosition = start + Vector2.up * t * 30;
                caption.Group.alpha = 1 - t;
            });
            Destroy(caption.Root.gameObject);
        }

        IEnumerator Shatter(Caption caption, Vector2 target, Action<float> arrival = null)
        {
            var shards = new List<Shard>();
            BreakGlyphs(caption.Title, shards);
            if (caption.Prefix != null) BreakGlyphs(caption.Prefix, shards);
            BreakGlyphs(caption.Value, shards);
            caption.Group.alpha = 0;
            ShardCount += shards.Count;
            yield return Animate(ShatterSeconds, t =>
            {
                var gather = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.18f, 1, t));
                foreach (var shard in shards)
                {
                    var scatter = shard.Start + shard.Scatter * Mathf.Sin(Mathf.Min(t / 0.3f, 1) * Mathf.PI * 0.5f);
                    shard.Graphic.rectTransform.anchoredPosition = Vector2.Lerp(scatter, target, gather) + Vector2.up * Mathf.Sin(gather * Mathf.PI) * 25;
                    shard.Graphic.rectTransform.localScale = Vector3.one * Mathf.Lerp(1, 0.04f, gather);
                    shard.Graphic.rectTransform.localEulerAngles = new Vector3(0, 0, shard.Scatter.x * t * 2);
                    shard.Graphic.color = Color.Lerp(Color.white, EnergyInk, gather);
                }
                arrival?.Invoke(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.48f, 1, t)));
            });
            foreach (var shard in shards) if (shard.Graphic != null) Destroy(shard.Graphic.gameObject);
            ShardCount -= shards.Count;
            Destroy(caption.Root.gameObject);
        }

        void BreakGlyphs(TMP_Text label, List<Shard> shards)
        {
            label.ForceMeshUpdate();
            var info = label.textInfo;
            for (var i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
                if (!character.isVisible) continue;
                var mesh = info.meshInfo[character.materialReferenceIndex];
                var start = character.vertexIndex;
                for (var triangle = 0; triangle < 2; triangle++)
                {
                    var order = triangle == 0 ? new[] { 0, 1, 2 } : new[] { 2, 3, 0 };
                    var vertices = new UnityEngine.UIVertex[3];
                    var center = Vector2.zero;
                    for (var v = 0; v < 3; v++) center += Local(label.rectTransform.TransformPoint(mesh.vertices[start + order[v]])) / 3;
                    for (var v = 0; v < 3; v++)
                    {
                        var index = start + order[v];
                        var vertex = UnityEngine.UIVertex.simpleVert;
                        vertex.position = Local(label.rectTransform.TransformPoint(mesh.vertices[index])) - center;
                        vertex.uv0 = mesh.uvs0[index];
                        vertex.uv1 = mesh.uvs2[index];
                        vertex.color = mesh.colors32[index];
                        vertices[v] = vertex;
                    }
                    var host = new GameObject("ScoringGlyphShard", typeof(RectTransform));
                    host.transform.SetParent(_blocker, false);
                    var graphic = host.AddComponent<ScoringGlyphShard>();
                    graphic.SetGlyph(vertices, mesh.material != null ? mesh.material : label.fontSharedMaterial);
                    graphic.rectTransform.anchoredPosition = center;
                    var seed = shards.Count;
                    shards.Add(new Shard { Graphic = graphic, Start = center, Scatter = new Vector2(Mathf.Sin(seed * 2.399f) * 34, 8 + Mathf.Cos(seed * 1.7f) * 25) });
                }
            }
        }

        Vector2 NumberPoint(TMP_Text value)
        {
            value.ForceMeshUpdate();
            var info = value.textInfo;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < info.characterCount; i++)
            {
                if (!info.characterInfo[i].isVisible) continue;
                min = Vector2.Min(min, info.characterInfo[i].bottomLeft);
                max = Vector2.Max(max, info.characterInfo[i].topRight);
            }
            return Local(value.rectTransform.TransformPoint((min + max) * 0.5f));
        }

        Vector2 HeadOf(int seat)
        {
            var root = RootAt(seat);
            if (root == null) return BesideTotal();
            return Local(root.TransformPoint(new Vector2(root.rect.center.x, root.rect.yMax))) + Vector2.up * 53;
        }

        Vector2 Local(Vector3 world) => CanvasMap.LocalPoint(_blocker, world, CanvasMap.EventCamera(_stage.BoardRoot));
        static string EnergySigned(int amount) => amount >= 0 ? "+" + amount : amount.ToString();
        FacilityEnergyColumn Column(int seat) => _stage.Facility != null && _stage.Facility.energyColumns != null && seat >= 0 && seat < _stage.Facility.energyColumns.Length ? _stage.Facility.energyColumns[seat] : null;
    }
}
