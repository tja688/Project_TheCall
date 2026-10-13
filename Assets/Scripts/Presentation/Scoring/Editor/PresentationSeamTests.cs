using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TheCall.Scoring
{
    public sealed class PresentationSeamTests
    {
        [TestCase(PayloadKind.SkillChip)]
        [TestCase(PayloadKind.BreedingSkill)]
        public void 删除按钮与标签仍能携带存在的基因(PayloadKind kind)
        {
            var root = new GameObject("Gene");
            try
            {
                var chip = root.AddComponent<SkillChipView>();
                var drag = root.AddComponent<OperationPayloadDrag>();
                drag.payloadKind = kind;
                if (kind == PayloadKind.SkillChip) chip.RenderGene(2, new GeneSlotView(2, "能量体", false, null), false);
                else chip.RenderBreeding(new BreedingPlanView(2, Array.Empty<string>(), "能量体"), false);
                Assert.That(drag.HasPayload(), Is.True);
                Assert.That(drag.ReadPayload().Index, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(PayloadKind.SkillChip)]
        [TestCase(PayloadKind.BreedingSkill)]
        public void 空基因不会因美术标签或按钮变成可拖动(PayloadKind kind)
        {
            var root = new GameObject("Gene", typeof(RectTransform), typeof(UnityEngine.UI.Button), typeof(TextMeshProUGUI));
            try
            {
                var chip = root.AddComponent<SkillChipView>();
                chip.button = root.GetComponent<UnityEngine.UI.Button>();
                chip.label = root.GetComponent<TextMeshProUGUI>();
                var drag = root.AddComponent<OperationPayloadDrag>();
                drag.payloadKind = kind;
                if (kind == PayloadKind.SkillChip) chip.RenderGene(0, null, false);
                else chip.RenderBreeding(new BreedingPlanView(0, Array.Empty<string>(), null), false);
                chip.button.interactable = true;
                chip.label.text = "新的美术占位文案";
                Assert.That(drag.HasPayload(), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void 两个操作界面各自绑定自己的拖动指针()
        {
            var first = new GameObject("First");
            var second = new GameObject("Second");
            try
            {
                var firstDrag = MakeScreen(first);
                var secondDrag = MakeScreen(second);
                var a = first.GetComponent<OperationScreenView>().pointer;
                var b = second.GetComponent<OperationScreenView>().pointer;
                firstDrag.session = b;
                a.Attach(null);
                b.Attach(null);
                Assert.That(firstDrag.session, Is.SameAs(a));
                Assert.That(secondDrag.session, Is.SameAs(b));
                secondDrag.session = a;
                secondDrag.OnPointerDown(new PointerEventData(null));
                Assert.That(secondDrag.session, Is.SameAs(b));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void 游离拖动控件不会借用别的界面的指针()
        {
            var root = new GameObject("Operation");
            var loose = new GameObject("Loose");
            try
            {
                MakeScreen(root);
                var drag = loose.AddComponent<OperationPayloadDrag>();
                drag.OnPointerDown(new PointerEventData(null));
                Assert.That(drag.session, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(loose);
            }
        }

        [Test]
        public void 界面移除所有可选控件仍可读取同一规则状态()
        {
            TheCallApp.Reset();
            ContentGate.Use(ContentBook.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "call-book.json"))));
            var root = new GameObject("OptionalControls");
            try
            {
                var app = TheCallApp.Interface;
                var phase = app.SendQuery(new RunPhaseQuery());
                root.AddComponent<OpeningScreenView>().Render(app, null);
                root.AddComponent<LevelStartScreenView>().Render(app);
                root.AddComponent<OperationScreenView>().Render(app, null);
                root.AddComponent<ShopScreenView>().Render(app, null, false, null);
                root.AddComponent<ResearchScreenView>().Render(app, "基因实验");
                root.AddComponent<ResultScreenView>().Render(false);
                Assert.That(app.SendQuery(new RunPhaseQuery()), Is.EqualTo(phase));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                TheCallApp.Reset();
            }
        }

        static OperationPayloadDrag MakeScreen(GameObject root)
        {
            var screen = root.AddComponent<OperationScreenView>();
            var pointerObject = new GameObject("Pointer");
            pointerObject.transform.SetParent(root.transform);
            screen.pointer = pointerObject.AddComponent<OperationPointer>();
            var gene = new GameObject("Gene");
            gene.transform.SetParent(root.transform);
            gene.AddComponent<SkillChipView>();
            return gene.AddComponent<OperationPayloadDrag>();
        }

        [Test]
        public void 缓存三十张卡片后仅剩一只怪物仍能出现在可见笼内()
        {
            TheCallApp.Reset();
            ContentGate.Use(ContentBook.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "call-book.json"))));
            var root = new GameObject("Inventory", typeof(RectTransform), typeof(UnityEngine.UI.ScrollRect));
            try
            {
                var viewport = (RectTransform)root.transform;
                viewport.sizeDelta = new Vector2(420, 358);
                var contentObject = new GameObject("Content", typeof(RectTransform));
                var content = (RectTransform)contentObject.transform;
                content.SetParent(viewport, false);
                content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
                var slotObject = new GameObject("Card", typeof(RectTransform));
                var slotRect = (RectTransform)slotObject.transform;
                slotRect.SetParent(content, false);
                slotRect.anchorMin = slotRect.anchorMax = slotRect.pivot = new Vector2(0, 1);
                slotRect.sizeDelta = new Vector2(192, 110);
                var scroll = root.GetComponent<UnityEngine.UI.ScrollRect>();
                scroll.content = content;
                scroll.viewport = viewport;
                var operation = root.AddComponent<OperationScreenView>();
                operation.cageSlots = new[] { slotObject.AddComponent<MonsterSlotView>() };
                var app = TheCallApp.Interface;
                var run = app.GetModel<RunModel>();
                for (var i = 0; i < 30; i++) run.AddToCage(run.CreateMonster(new[] { "能量体" }));
                operation.Render(app, null);
                content.anchoredPosition = new Vector2(0, 1420);
                foreach (var monster in run.Cage.Skip(1).ToArray()) run.TryRemoveFromCage(monster.Id, out _);
                operation.Render(app, null);
                Assert.That(operation.cageSlots.Length, Is.EqualTo(30), "Keep the reusable cards");
                Assert.That(content.rect.height, Is.LessThan(viewport.rect.height), "Hidden cards must not retain scroll extent");
                var center = (Vector2)viewport.InverseTransformPoint(slotRect.TransformPoint(slotRect.rect.center));
                Assert.That(viewport.rect.Contains(center), Is.True, "The remaining monster must be reachable immediately");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                TheCallApp.Reset();
            }
        }
    }
}
