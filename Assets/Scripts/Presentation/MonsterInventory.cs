using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheCall
{
    internal static class MonsterInventory
    {
        public static MonsterSlotView[] Render(MonsterSlotView[] slots, IReadOnlyList<MonsterView> monsters,
            string selectedId, PresentationSpriteCatalog catalog, string prefix, int columns, float rowSpacing,
            float padding, Action<MonsterSlotView> bindNewSlot = null)
        {
            if (slots == null || slots.Length == 0 || slots[0] == null) return slots;
            var template = slots[0];
            var parent = template.transform.parent as RectTransform;
            var pool = new List<MonsterSlotView>(slots);
            while (pool.Count < monsters.Count)
            {
                var copy = UnityEngine.Object.Instantiate(template, parent);
                copy.name = prefix + pool.Count;
                copy.monsterId = null;
                bindNewSlot?.Invoke(copy);
                pool.Add(copy);
            }

            // An artist-authored LayoutGroup takes authority over placement.
            var layout = parent != null ? parent.GetComponent<UnityEngine.UI.LayoutGroup>() : null;
            if (layout == null || !layout.isActiveAndEnabled)
            {
                columns = Mathf.Max(1, columns);
                var first = (RectTransform)template.transform;
                var second = slots.Length > 1 && slots[1] != null ? (RectTransform)slots[1].transform : null;
                var origin = first.anchoredPosition;
                var xStep = second != null ? second.anchoredPosition.x - origin.x : first.rect.width + padding;
                for (var i = 0; i < pool.Count; i++)
                    if (pool[i] != null)
                        ((RectTransform)pool[i].transform).anchoredPosition = origin + new Vector2(i % columns * xStep, -(i / columns) * rowSpacing);
                if (parent != null)
                {
                    var height = Mathf.Ceil(monsters.Count / (float)columns) * rowSpacing + padding;
                    var shrank = height < parent.rect.height;
                    parent.sizeDelta = new Vector2(parent.sizeDelta.x, height);
                    var scroll = parent.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                    if (shrank && scroll != null && scroll.content == parent && scroll.viewport != null)
                    {
                        // Manual inventories are top-anchored. Retire the old scroll range immediately.
                        scroll.StopMovement();
                        var position = parent.anchoredPosition;
                        position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, height - scroll.viewport.rect.height));
                        parent.anchoredPosition = position;
                    }
                }
            }

            for (var i = 0; i < pool.Count; i++)
            {
                var slot = pool[i];
                if (slot == null) continue;
                var monster = i < monsters.Count ? monsters[i] : null;
                slot.gameObject.SetActive(monster != null);
                PresentationReadout.Monster(slot, monster, monster != null && monster.Id == selectedId, catalog);
            }
            return pool.ToArray();
        }
    }
}
