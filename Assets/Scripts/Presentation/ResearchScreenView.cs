using TMPro;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TheCall
{
    public sealed class ResearchScreenView : MonoBehaviour
    {
        public TMP_Text techPointLabel;
        public TMP_Text goldLabel;
        public TMP_Text detailTitle;
        public TMP_Text detailBody;
        public TMP_Text detailCost;
        public Button researchButton;
        public TMP_Text researchLabel;
        public Button backButton;
        public TechNodeView[] nodes;

        public string Render(IArchitecture architecture, string selectedTech)
        {
            var run = architecture.SendQuery(new RunLedgerQuery());
            PresentationReadout.Text(techPointLabel, run.TechPoints.ToString());
            PresentationReadout.Text(goldLabel, run.Gold.ToString());
            TechNodeView selected = null;
            if (nodes != null)
                foreach (var node in nodes)
                {
                    if (node == null) continue;
                    var unlocked = PresentationReadout.Contains(run.UnlockedTech, node.techName);
                    PresentationReadout.Text(node.costLabel, unlocked ? "已研究" : "消耗 1 科技点");
                    if (node.selection != null) node.selection.SetActive(node.techName == selectedTech);
                    if (selected == null || node.techName == selectedTech) selected = node;
                }
            if (selected == null) return selectedTech;
            PresentationReadout.Text(detailTitle, selected.title != null ? selected.title.text : selected.techName);
            PresentationReadout.Text(detailBody, selected.summary);
            var researched = PresentationReadout.Contains(run.UnlockedTech, selected.techName);
            PresentationReadout.Text(detailCost, researched ? "已经研究" : "科技点消耗：1");
            PresentationReadout.Text(researchLabel, researched ? "已研究" : "研究");
            if (researchButton != null) researchButton.interactable = !researched && run.TechPoints >= 1;
            return selected.techName;
        }
    }
}
