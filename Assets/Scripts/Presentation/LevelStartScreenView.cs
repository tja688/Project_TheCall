using TMPro;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TheCall
{
    public sealed class LevelStartScreenView : MonoBehaviour
    {
        public TMP_Text levelLabel;
        public TMP_Text dueLabel;
        public TMP_Text excessLabel;
        public Button beginButton;

        public void Render(IArchitecture architecture)
        {
            var target = architecture.SendQuery(new LevelTargetQuery());
            PresentationReadout.Text(levelLabel, "第 " + target.LevelNumber + " 关");
            PresentationReadout.Text(dueLabel, target.EnergyDue.ToString());
            PresentationReadout.Label(transform, "DueCaption", "目标额度");
            if (excessLabel != null) excessLabel.gameObject.SetActive(false);
            PresentationReadout.Shown(transform, "ExcessCaption", false);
        }
    }
}
