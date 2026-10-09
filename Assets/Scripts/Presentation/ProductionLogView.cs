using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// 计算记录窗口。句子由规则写好，这里只负责打开、关闭和滚到最近一次提交。
    /// 正文留在滚动视口里，不画出窗口。
    /// </summary>
    public sealed class ProductionLogView : MonoBehaviour
    {
        public TMP_Text body;
        public ScrollRect scroll;
        public Button closeButton;
        public Button shadeButton;

        public bool IsOpen => gameObject.activeSelf;

        public event Action Closed;

        void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
            if (shadeButton != null)
                shadeButton.onClick.AddListener(Hide);
            KeepTextInFrame();
        }

        public void Show(string text)
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            if (body != null)
            {
                body.richText = true;
                body.alignment = TextAlignmentOptions.TopLeft;
                body.textWrappingMode = TextWrappingModes.Normal;
                body.overflowMode = TextOverflowModes.Overflow;
                body.text = text ?? "";
                KeepTextInFrame();
            }

            StopAllCoroutines();
            StartCoroutine(PinToLatest());
        }

        public void Hide()
        {
            if (!gameObject.activeSelf)
                return;

            StopAllCoroutines();
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void KeepTextInFrame()
        {
            var viewport = scroll != null ? scroll.viewport : null;
            if (viewport == null && body != null)
                viewport = body.transform.parent as RectTransform;
            if (viewport == null)
                return;

            var graphic = viewport.GetComponent<Graphic>();
            if (graphic != null)
                graphic.enabled = true;

            var mask = viewport.GetComponent<Mask>();
            if (mask != null)
                mask.enabled = true;
            else if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();

            if (body == null)
                return;

            body.overflowMode = TextOverflowModes.Overflow;
            body.textWrappingMode = TextWrappingModes.Normal;
            var width = viewport.rect.width;
            if (width > 1f)
                body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        IEnumerator PinToLatest()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            KeepTextInFrame();
            if (body != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(body.rectTransform);
            if (scroll != null)
                scroll.verticalNormalizedPosition = 0f;
        }
    }
}
