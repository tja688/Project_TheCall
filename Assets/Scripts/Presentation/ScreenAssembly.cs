using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheCall
{
    /// <summary>界面分部就位。只临时偏移美术，不持有规则，也不改变子对象的显隐。</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class ScreenAssembly : MonoBehaviour
    {
        [Serializable]
        public sealed class Part
        {
            public RectTransform target;
            public Vector2 offset;
            [Min(0)] public float delay;
            [Min(0.05f)] public float seconds = 0.48f;
            [Range(0.9f, 1)] public float scale = 1;
        }

        [Tooltip("换主界面/科研素材时，将新分层拖到对应 target；整组控件只绑定父级，避免重复移动。")]
        public Part[] parts = Array.Empty<Part>();
        public AssemblySurface[] surfaces = Array.Empty<AssemblySurface>();
        [Min(0.08f)] public float exitSeconds = 0.22f;

        readonly List<Pose> _poses = new List<Pose>();
        CanvasGroup _root;
        float _rootAlpha;
        bool _interactable;
        bool _raycasts;
        bool _prepared;
        bool _entering;
        float _time;
        public float Duration { get; private set; }
        public bool Prepared => _prepared;
        public int LivePartCount => _poses.Count;

        sealed class Pose
        {
            public Part part;
            public CanvasGroup group;
            public Vector3 position;
            public Vector3 scale;
            public float alpha;
        }

        public void Prepare(bool entering)
        {
            Restore();
            _root = GetComponent<CanvasGroup>();
            if (_root == null) _root = gameObject.AddComponent<CanvasGroup>();
            _rootAlpha = _root.alpha;
            _interactable = _root.interactable;
            _raycasts = _root.blocksRaycasts;
            _root.interactable = _root.blocksRaycasts = false;
            _entering = entering;
            _prepared = true;
            Duration = entering ? 0.15f : exitSeconds;
            foreach (var part in parts) Capture(part);
            foreach (var surface in surfaces)
                if (surface != null) foreach (var part in surface.Open()) Capture(part);
            Sample(0);
        }

        void Capture(Part part)
        {
            if (part == null || part.target == null || !part.target.gameObject.activeInHierarchy) return;
            var group = part.target.GetComponent<CanvasGroup>();
            if (group == null) group = part.target.gameObject.AddComponent<CanvasGroup>();
            _poses.Add(new Pose { part = part, group = group, position = part.target.localPosition,
                scale = part.target.localScale, alpha = group.alpha });
            if (_entering) Duration = Mathf.Max(Duration, part.delay + part.seconds);
        }

        public void Sample(float seconds)
        {
            if (!_prepared) return;
            _time = seconds;
            Apply();
        }

        // Run after view Update, before FacilitySprite/FacilityLamp project the authored rects.
        void LateUpdate() { if (_prepared) Apply(); }

        void Apply()
        {
            var rootProgress = Mathf.Clamp01(_entering ? _time / 0.14f : (_time / exitSeconds - 0.35f) / 0.65f);
            _root.alpha = _rootAlpha * (_entering ? Ease(rootProgress) : 1 - Ease(rootProgress));
            foreach (var pose in _poses)
            {
                var part = pose.part;
                if (part.target == null || pose.group == null) continue;
                var exitDelay = Mathf.Clamp01(1 - part.delay / 0.4f) * exitSeconds * 0.2f;
                var t = _entering ? Mathf.Clamp01((_time - part.delay) / part.seconds) :
                    Mathf.Clamp01((_time - exitDelay) / (exitSeconds - exitDelay));
                var remaining = _entering ? Mathf.Pow(1 - t, 4) : Ease(t) * 0.32f;
                // Zero-offset tracks fade only: their existing idle/needle/shutter controller owns motion.
                if (part.offset != Vector2.zero)
                    part.target.localPosition = pose.position + (Vector3)(part.offset * remaining);
                if (part.scale != 1)
                    part.target.localScale = pose.scale * Mathf.Lerp(part.scale, 1, 1 - remaining);
                pose.group.alpha = pose.alpha * (_entering ? Ease(Mathf.Clamp01(t * 1.65f)) : 1 - Ease(t));
            }
        }

        static float Ease(float t) => t * t * (3 - 2 * t);

        public void Restore()
        {
            if (!_prepared) return;
            _prepared = false;
            foreach (var pose in _poses)
            {
                if (pose.part.target != null)
                {
                    if (pose.part.offset != Vector2.zero) pose.part.target.localPosition = pose.position;
                    if (pose.part.scale != 1) pose.part.target.localScale = pose.scale;
                }
                if (pose.group != null) pose.group.alpha = pose.alpha;
            }
            _poses.Clear();
            foreach (var surface in surfaces) if (surface != null) surface.Close();
            if (_root != null)
            {
                _root.alpha = _rootAlpha;
                _root.interactable = _interactable;
                _root.blocksRaycasts = _raycasts;
            }
        }

        void OnDisable() => Restore();
    }
}
