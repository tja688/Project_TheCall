using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shop and satchel share one layout. Selecting a slot refreshes the detail pane and the slot rim.
/// </summary>
public sealed class HearthwoodCatalogPanel : MonoBehaviour
{
    [System.Serializable]
    public struct Item
    {
        public Sprite icon;
        public string title;
        public string description;
        public int price;
    }

    [SerializeField] Item[] items;
    [SerializeField] Image[] slotFrames;
    [SerializeField] Sprite slotNormal;
    [SerializeField] Sprite slotSelected;
    [SerializeField] Image detailIcon;
    [SerializeField] TMP_Text detailName;
    [SerializeField] TMP_Text detailDescription;
    [SerializeField] TMP_Text detailPrice;
    [SerializeField] int selected;

    public void Select(int index)
    {
        if (items == null || items.Length == 0)
            return;

        selected = Mathf.Clamp(index, 0, items.Length - 1);
        var item = items[selected];

        if (detailIcon != null)
            detailIcon.sprite = item.icon;
        if (detailName != null)
            detailName.text = item.title;
        if (detailDescription != null)
            detailDescription.text = item.description;
        if (detailPrice != null)
            detailPrice.text = item.price + " G";

        if (slotFrames == null)
            return;

        for (int i = 0; i < slotFrames.Length; i++)
        {
            if (slotFrames[i] == null)
                continue;
            slotFrames[i].sprite = i == selected ? slotSelected : slotNormal;
        }
    }
}
