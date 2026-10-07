using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class TechNodeView : MonoBehaviour
    {
        public string techName;
        [TextArea] public string summary;
        public Button button;
        public TMP_Text title;
        public TMP_Text costLabel;
        public GameObject selection;
    }
}
