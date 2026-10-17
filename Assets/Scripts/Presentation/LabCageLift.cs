using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheCall
{
    // Only presentation position lives here. Monster IDs and input stay on the existing slots.
    public sealed class LabCageLift : ScrollRect
    {
        public const float RowHeight = 140f;
        public float rowHeight = RowHeight;
        public float rowWidth = 264;
        public Sprite[] rowBacks;
        public Sprite[] rowFronts;
        public TMP_Text rangeLabel;
        public Button previousButton;
        public Button nextButton;
        readonly List<RectTransform> _backs = new List<RectTransform>();
        readonly List<RectTransform> _fronts = new List<RectTransform>();
        float _target;
        float _speed;
        bool _moving;
        bool _draggingLift;
        int _count;
        int _rows = 2;
        public bool IsMoving => _moving;
        public float TargetOffset => _target;
        float Span => Mathf.Max(0, content.rect.height - viewport.rect.height);

        public void Render(int count)
        {
            _count = count;
            _rows = Mathf.Max(2, Mathf.CeilToInt(count / 2f));
            content.sizeDelta = new Vector2(0, _rows * rowHeight);
            for (var i = 0; i < _rows; i++)
            {
                EnsureRow(_backs, i, "CageInterior", rowBacks);
                EnsureRow(_fronts, i, "CageIron", rowFronts);
            }
            for (var i = 0; i < _backs.Count; i++)
            {
                _backs[i].gameObject.SetActive(i < _rows);
                _fronts[i].gameObject.SetActive(i < _rows);
                _backs[i].SetAsFirstSibling();
                _fronts[i].SetAsLastSibling();
            }
            _target = Mathf.Clamp(_target, 0, Span);
            if (content.anchoredPosition.y > Span) SetOffset(Span);
            PaintNavigation();
        }

        void EnsureRow(List<RectTransform> pool, int row, string prefix, Sprite[] sprites)
        {
            if (row < pool.Count) return;
            var node = new GameObject(prefix + row, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(content, false);
            var rect = (RectTransform)node.transform;
            LabSceneView.Place(rect, 0, row * rowHeight, rowWidth, rowHeight);
            var image = node.GetComponent<Image>();
            image.sprite = sprites[row % sprites.Length];
            image.raycastTarget = false;
            pool.Add(rect);
        }

        public void Page(int direction) => MoveRows(direction * 2);

        void MoveRows(int rows)
        {
            if (content == null || viewport == null) return;
            StopMovement();
            var origin = _moving ? _target : content.anchoredPosition.y;
            _target = Mathf.Clamp((Mathf.Round(origin / rowHeight) + rows) * rowHeight, 0, Span);
            _moving = Mathf.Abs(_target - content.anchoredPosition.y) > 0.05f;
            PaintNavigation();
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (!IsActive() || eventData.scrollDelta.y == 0) return;
            MoveRows(eventData.scrollDelta.y < 0 ? 1 : -1);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _moving = false;
            _speed = 0;
            _draggingLift = true;
            base.OnBeginDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            base.OnEndDrag(eventData);
            _draggingLift = false;
            MoveRows(0);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (content == null || viewport == null) return;
            if (_moving)
            {
                _target = Mathf.Clamp(_target, 0, Span);
                var position = Mathf.SmoothDamp(content.anchoredPosition.y, _target, ref _speed, 0.15f,
                    1600f, Mathf.Min(Time.unscaledDeltaTime, 0.05f));
                if (Mathf.Abs(position - _target) < 0.08f && Mathf.Abs(_speed) < 1f)
                {
                    position = _target;
                    _speed = 0;
                    _moving = false;
                }
                SetOffset(position);
            }
            else if (!_draggingLift) _target = content.anchoredPosition.y;
            PaintNavigation();
        }

        void SetOffset(float offset)
        {
            var position = content.anchoredPosition;
            position.y = offset;
            SetContentAnchoredPosition(position);
        }

        void PaintNavigation()
        {
            var offset = content.anchoredPosition.y;
            var row = Mathf.Clamp(Mathf.RoundToInt(offset / rowHeight), 0, _rows - 2);
            if (rangeLabel != null) rangeLabel.text = _count == 0 ? "空置" : (row * 2 + 1) + "-" + Mathf.Min(_count, row * 2 + 4) + " / " + _count;
            var destination = _moving ? _target : offset;
            if (previousButton != null) previousButton.interactable = destination > 0.1f;
            if (nextButton != null) nextButton.interactable = destination < Span - 0.1f;
        }

        protected override void OnDisable()
        {
            _moving = false;
            _draggingLift = false;
            _speed = 0;
            if (content != null) content.anchoredPosition = Vector2.zero;
            _target = 0;
            base.OnDisable();
        }
    }
}
