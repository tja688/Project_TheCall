using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class ResultScreenView : MonoBehaviour
    {
        public TMP_Text title;
        public TMP_Text body;
        public MonsterPortrait portrait;
        public Button restartButton;

        public void Render(bool victory)
        {
            PresentationReadout.Text(title, victory ? "七关都交上去了" : "账单没有补上");
            PresentationReadout.Text(body, victory
                ? "这一局结束。怪物笼、金币和科技都已清空。"
                : "这一关的目标没有达到。这一局结束。");
            if (portrait != null) portrait.Show(MonsterAppearance.FromSeed(victory ? 73 : 29));
        }
    }
}
