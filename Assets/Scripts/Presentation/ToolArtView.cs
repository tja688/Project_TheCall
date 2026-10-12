using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class ToolArtView : MonoBehaviour
    {
        public string toolName;
        public Sprite icon;
        public Image iconImage;
        public TMP_Text nameLabel;
        [TextArea] public string description;
    }
}
