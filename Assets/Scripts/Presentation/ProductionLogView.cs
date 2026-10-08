using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// 计算记录窗口。句子由规则写好，这里只负责打开、关闭和滚到最近一次提交。
    /// </summary>
    public sealed class ProductionLogView : MonoBehaviour
    {
        public TMP_Text body;
        public ScrollRect scroll;
        public Button closeButton;
        public Button shadeButton;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
            if (shadeButton != null)
                shadeButton.onClick.AddListener(Hide);
        }

        public void Show(string text)
        {
            gameObject.SetActive(true);
            if (body != null)
            {
                body.richText = true;
                body.alignment = TextAlignmentOptions.TopLeft;
                body.text = text ?? "";
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
        }

        IEnumerator PinToLatest()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (body != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(body.rectTransform);
            if (scroll != null)
                scroll.verticalNormalizedPosition = 0f;
        }
    }
}
