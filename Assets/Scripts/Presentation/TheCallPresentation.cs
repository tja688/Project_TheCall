using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// Runtime presentation shell for Main.unity. The rules assembly remains the source of truth;
    /// this class only turns Queries into cards and routes clicks through Commands.
    /// </summary>
    public sealed class TheCallPresentation : MonoBehaviour
    {
        static readonly Color Ink = Hex("1D2633");
        static readonly Color InkSoft = Hex("53606A");
        static readonly Color Paper = Hex("F6F0E2");
        static readonly Color PaperDark = Hex("E8DEC9");
        static readonly Color Coral = Hex("E06B53");
        static readonly Color CoralDark = Hex("A9483D");
        static readonly Color Mint = Hex("B8D8C5");
        static readonly Color MintDark = Hex("5A8D7E");
        static readonly Color Gold = Hex("E0AA54");
        static readonly Color Blue = Hex("6B9DC2");
        static readonly Color Violet = Hex("8C79B4");

        Canvas _canvas;
        RectTransform _root;
        Transform _content;
        Transform _overlay;
        TMP_FontAsset _font;
        Sprite[] _body;
        Sprite[] _head;
        Sprite[] _eye;
        Sprite[] _mouth;
        Sprite[] _hand;
        Sprite[] _foot;
        Sprite[] _tail;
        Sprite[] _hat;
        Sprite[] _accessory;
        string _selectedMonster;
        string _selectedSkill;
        string _notice;
        float _noticeUntil;
        bool _isBusy;
        bool _showTech;
        bool _showSell;
        int _refreshFrame;
        readonly List<Graphic> _animated = new List<Graphic>();

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            LoadArt();
            BuildCanvas();
            Refresh();
        }

        void Update()
        {
            for (var i = 0; i < _animated.Count; i++)
            {
                if (_animated[i] == null)
                    continue;
                var t = Time.unscaledTime * 1.7f + i * 0.63f;
                var pulse = 1f + Mathf.Sin(t) * 0.018f;
                _animated[i].rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (Time.unscaledTime > _noticeUntil && _notice != null)
            {
                _notice = null;
                Refresh();
            }
        }

        void LoadArt()
        {
            _font = Resources.Load<TMP_FontAsset>("SmileySans-Oblique-3 SDF");
            if (_font == null)
                _font = Resources.Load<TMP_FontAsset>("LiberationSans SDF");

            _body = LoadSprites("MonsterParts/Body");
            _head = LoadSprites("MonsterParts/Head");
            _eye = LoadSprites("MonsterParts/Eye");
            _mouth = LoadSprites("MonsterParts/Mouth");
            _hand = LoadSprites("MonsterParts/Hand");
            _foot = LoadSprites("MonsterParts/Foot");
            _tail = LoadSprites("MonsterParts/Tail");
            _hat = LoadSprites("MonsterParts/Hat");
            _accessory = LoadSprites("MonsterParts/Accessory");
        }

        static Sprite[] LoadSprites(string path)
        {
            var sprites = Resources.LoadAll<Sprite>(path);
            if (sprites.Length > 0)
                return sprites;
            var textures = Resources.LoadAll<Texture2D>(path);
            var result = new Sprite[textures.Length];
            for (var i = 0; i < textures.Length; i++)
                result[i] = Sprite.Create(textures[i], new Rect(0, 0, textures[i].width, textures[i].height), new Vector2(.5f, .5f), 100f);
            return result;
        }

        void BuildCanvas()
        {
            var canvasObject = new GameObject("TheCallCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = false;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .55f;
            _root = canvasObject.GetComponent<RectTransform>();
            _content = new GameObject("Content", typeof(RectTransform)).transform;
            _content.SetParent(_root, false);
            Stretch(_content as RectTransform);
            _overlay = new GameObject("Overlay", typeof(RectTransform)).transform;
            _overlay.SetParent(_root, false);
            Stretch(_overlay as RectTransform);

            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, false);
            }
        }

        void Refresh()
        {
            if (_content == null || _isBusy)
                return;
            _refreshFrame++;
            Clear(_content);
            Clear(_overlay);
            _animated.Clear();
            var phase = TheCallApp.Interface.SendQuery(new RunPhaseQuery());
            switch (phase)
            {
                case RunPhase.Opening:
                    DrawOpening();
                    break;
                case RunPhase.LevelStart:
                    DrawLevelStart();
                    break;
                case RunPhase.Operation:
                    DrawOperation();
                    break;
                case RunPhase.Shop:
                    DrawShop();
                    break;
                case RunPhase.Victory:
                    DrawEnd(true);
                    break;
                case RunPhase.Failed:
                    DrawEnd(false);
                    break;
            }
        }

        void DrawOpening()
        {
            DrawBackdrop(new Color(.11f, .14f, .18f), new Color(.21f, .27f, .28f));
            var hero = Panel(_content, "OpeningHero", new Vector2(980f, 690f), new Vector2(0f, 10f), new Color(.96f, .93f, .84f, .97f), 0f);
            Decorate(hero, false);
            Label(hero, "THE CALL", 66, CoralDark, new Vector2(0f, 245f), TextAnchor.MiddleCenter, FontStyles.Bold);
            Label(hero, "奇异生物培育所", 24, InkSoft, new Vector2(0f, 185f), TextAnchor.MiddleCenter, FontStyles.Normal);
            Label(hero, "第 0 / 7 关  ·  选择你的第一只怪物", 15, MintDark, new Vector2(0f, 142f), TextAnchor.MiddleCenter, FontStyles.Bold);
            Label(hero, "它们不太擅长活着，但很擅长把能量变成问题。", 16, InkSoft, new Vector2(0f, -285f), TextAnchor.MiddleCenter, FontStyles.Italic);
            DrawCreature(hero, new Vector2(-215f, -40f), 1, .95f, Coral);
            DrawCreature(hero, new Vector2(0f, -20f), 2, 1.12f, MintDark);
            DrawCreature(hero, new Vector2(215f, -40f), 3, .95f, Violet);
            var candidates = TheCallApp.Interface.SendQuery(new OpeningCandidatesQuery());
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var x = -215f + i * 215f;
                var button = Button(hero, "KEEP_" + candidate.Id, "留下它", new Vector2(x, -205f), new Vector2(150f, 48f), Coral, 16);
                var id = candidate.Id;
                button.onClick.AddListener(() => Keep(id));
                Label(hero, candidate.Skills[0].Name, 13, Ink, new Vector2(x, -155f), TextAnchor.MiddleCenter, FontStyles.Bold);
                Label(hero, QuoteText(candidate.Skills[0].Quote) + "  " + RarityText(candidate.Skills[0].Rarity), 12, InkSoft, new Vector2(x, -177f), TextAnchor.MiddleCenter, FontStyles.Normal);
            }
            Label(hero, "拖动不存在，点击就好。内核会记住你的选择。", 12, InkSoft, new Vector2(0f, -325f), TextAnchor.MiddleCenter, FontStyles.Normal);
        }

        void DrawLevelStart()
        {
            DrawBackdrop(new Color(.12f, .17f, .21f), new Color(.24f, .31f, .3f));
            var run = TheCallApp.Interface.SendQuery(new RunLedgerQuery());
            var target = TheCallApp.Interface.SendQuery(new LevelTargetQuery());
            DrawHeader(target.LevelNumber, run, "本关开场");
            var card = Panel(_content, "LevelStartCard", new Vector2(720f, 420f), new Vector2(0f, 5f), Paper, 0f);
            Decorate(card, false);
            Label(card, "LEVEL " + target.LevelNumber.ToString("00"), 58, CoralDark, new Vector2(0f, 115f), TextAnchor.MiddleCenter, FontStyles.Bold);
            Label(card, "工作台已擦拭。欠下的能量也已擦拭。", 17, InkSoft, new Vector2(0f, 55f), TextAnchor.MiddleCenter, FontStyles.Italic);
            Label(card, "本关交款", 15, InkSoft, new Vector2(-130f, -30f), TextAnchor.MiddleCenter, FontStyles.Normal);
            Label(card, target.EnergyDue.ToString(), 44, Ink, new Vector2(-130f, -86f), TextAnchor.MiddleCenter, FontStyles.Bold);
            Label(card, "超额科技", 15, InkSoft, new Vector2(130f, -30f), TextAnchor.MiddleCenter, FontStyles.Normal);
            Label(card, target.ExcessEnergy.ToString() + "+", 44, Gold, new Vector2(130f, -86f), TextAnchor.MiddleCenter, FontStyles.Bold);
            var begin = Button(card, "BEGIN", "进入操作台", new Vector2(0f, -155f), new Vector2(220f, 54f), MintDark, 17);
            begin.onClick.AddListener(BeginLevel);
            Label(card, "怪物笼中的成员会在操作阶段等你安排。", 12, InkSoft, new Vector2(0f, -205f), TextAnchor.MiddleCenter, FontStyles.Normal);
        }

        void DrawOperation()
        {
            DrawBackdrop(new Color(.11f, .15f, .19f), new Color(.22f, .28f, .29f));
            var run = TheCallApp.Interface.SendQuery(new RunLedgerQuery());
            var target = TheCallApp.Interface.SendQuery(new LevelTargetQuery());
            var energy = TheCallApp.Interface.SendQuery(new LevelEnergyQuery());
            DrawHeader(target.LevelNumber, run, "操作阶段");
            DrawOperationBoard(target, energy);
            DrawCage(run);
            DrawBreeding(run);
            DrawSkillShelf(run);
            DrawOperationFooter(target, energy, run);
            if (_showTech)
                DrawTechPanel(run);
            if (_notice != null)
                DrawToast(_notice);
        }

        void DrawOperationBoard(LevelTarget target, int energy)
        {
            var board = Panel(_content, "ExtractionBoard", new Vector2(830f, 270f), new Vector2(105f, 90f), new Color(.95f, .91f, .82f, .98f), 0f);
            Decorate(board, true);
            Label(board, "提取轨", 22, Ink, new Vector2(-345f, 88f), TextAnchor.MiddleLeft, FontStyles.Bold);
            Label(board, "把会说话的部分排成一条不太可靠的生产线", 12, InkSoft, new Vector2(-330f, 60f), TextAnchor.MiddleLeft, FontStyles.Italic);
            var cells = TheCallApp.Interface.SendQuery(new ExtractionSlotsQuery());
            for (var i = 0; i < cells.Count; i++)
            {
                var x = -300f + i * 150f;
                var slot = Panel(board, "ExtractCell_" + i, new Vector2(132f, 140f), new Vector2(x, -24f), new Color(.88f, .83f, .7f, .65f), 18f);
                Label(slot, (i + 1).ToString("0"), 12, InkSoft, new Vector2(-51f, 51f), TextAnchor.MiddleCenter, FontStyles.Bold);
                var index = i;
                var trigger = Button(slot, "SLOT_" + i, cells[i].MonsterId == null ? "放入" : "取回", new Vector2(0f, -48f), new Vector2(74f, 26f), cells[i].MonsterId == null ? MintDark : CoralDark, 11);
                trigger.onClick.AddListener(() => ClickExtraction(index, cells[index].MonsterId));
                if (cells[i].MonsterId != null)
                    DrawCreature(slot, new Vector2(0f, 4f), StableSeed(cells[i].MonsterId), .51f, Ink);
            }
            Label(board, "每个格子都算数。空档也算数。", 11, InkSoft, new Vector2(0f, -116f), TextAnchor.MiddleCenter, FontStyles.Normal);
        }

        void DrawCage(RunLedger run)
        {
            var cage = Panel(_content, "Cage", new Vector2(300f, 540f), new Vector2(-540f, -100f), new Color(.90f, .85f, .74f, .94f), 18f);
            Decorate(cage, true);
            Label(cage, "怪物笼", 22, Ink, new Vector2(0f, 236f), TextAnchor.MiddleCenter, FontStyles.Bold);
            Label(cage, run.Tools.Count + " 件工具  ·  " + run.SkillSlots.Count + " 个技能", 11, InkSoft, new Vector2(0f, 211f), TextAnchor.MiddleCenter, FontStyles.Normal);
            var monsters = TheCallApp.Interface.SendQuery(new MonsterCageQuery());
            for (var i = 0; i < monsters.Count; i++)
            {
                var x = -92f + (i % 2) * 184f;
                var y = 150f - (i / 2) * 105f;
                var monster = monsters[i];
                var card = Panel(cage, "CageMonster_" + monster.Id, new Vector2(164f, 85f), new Vector2(x, y), _selectedMonster == monster.Id ? new Color(.96f, .79f, .56f, .97f) : Paper, 13f);
                var id = monster.Id;
                var button = Button(card, "SELECT_" + id, "选择", new Vector2(-52f, -25f), new Vector2(55f, 23f), _selectedMonster == id ? Coral : MintDark, 10);
                button.onClick.AddListener(() => SelectMonster(id));
                var discard = Button(card, "DISCARD_" + id, "废弃", new Vector2(52f, -25f), new Vector2(55f, 23f), CoralDark, 10);
                discard.onClick.AddListener(() => DiscardMonster(id));
                DrawCreature(card, new Vector2(35f, 9f), StableSeed(id), .26f, Ink);
                Label(card, monster.Skills.Count + "技能", 10, InkSoft, new Vector2(-52f, 22f), TextAnchor.MiddleCenter, FontStyles.Normal);
                Label(card, SkillShort(monster.Skills[0].Name), 10, Ink, new Vector2(-52f, 5f), TextAnchor.MiddleCenter, FontStyles.Bold);
            }
            Label(cage, "选一只，再点下方槽位。", 11, InkSoft, new Vector2(0f, -247f), TextAnchor.MiddleCenter, FontStyles.Italic);
        }

        void DrawBreeding(RunLedger run)
        {
            var breeding = Panel(_content, "Breeding", new Vector2(380f, 220f), new Vector2(410f, -250f), new Color(.83f, .91f, .84f, .97f), 16f);
            Decorate(breeding, true);
            Label(breeding, "培育台", 19, Ink, new Vector2(-135f, 79f), TextAnchor.MiddleLeft, FontStyles.Bold);
            Label(breeding, "亲本 · 亲本 · 可选技能", 11, MintDark, new Vector2(-134f, 53f), TextAnchor.MiddleLeft, FontStyles.Normal);
            var slots = TheCallApp.Interface.SendQuery(new BreedingSlotsQuery());
            for (var i = 0; i < slots.Count; i++)
            {
                var x = -86f + i * 86f;
                var index = i;
                var slot = Button(breeding, "BREED_" + i, slots[i].MonsterId == null ? "+" : "●", new Vector2(x, -8f), new Vector2(64f, 64f), slots[i].MonsterId == null ? MintDark : Coral, 22);
                slot.onClick.AddListener(() => ClickBreeding(index, slots[index].MonsterId));
                if (slots[i].MonsterId != null)
                    DrawCreature(breeding, new Vector2(x, -8f), StableSeed(slots[i].MonsterId), .22f, Ink);
            }
            var skill = Button(breeding, "BREED_SKILL", string.IsNullOrEmpty(TheCallApp.Interface.SendQuery(new BreedingPlansQuery())[0].SkillName) ? "投技能" : "已投技能", new Vector2(127f, -8f), new Vector2(100f, 42f), Violet, 12);
            skill.onClick.AddListener(PlaceBreedingSkill);
            Label(breeding, "锁定于结算 · 下一关出生", 10, InkSoft, new Vector2(0f, -78f), TextAnchor.MiddleCenter, FontStyles.Italic);
        }

        void DrawSkillShelf(RunLedger run)
        {
            var shelf = Panel(_content, "SkillShelf", new Vector2(380f, 190f), new Vector2(410f, -45f), new Color(.92f, .88f, .79f, .97f), 16f);
            Decorate(shelf, true);
            Label(shelf, "技能槽", 19, Ink, new Vector2(-135f, 68f), TextAnchor.MiddleLeft, FontStyles.Bold);
            Label(shelf, "废弃或暂存的能力", 11, InkSoft, new Vector2(-135f, 43f), TextAnchor.MiddleLeft, FontStyles.Normal);
            var skills = run.SkillSlots;
            for (var i = 0; i < skills.Count; i++)
            {
                var index = i;
                var skill = Button(shelf, "SKILL_" + i, SkillShort(skills[i]), new Vector2(-96f + i * 96f, -18f), new Vector2(86f, 53f), _selectedSkill == skills[i] ? Gold : Violet, 11);
                skill.onClick.AddListener(() => SelectSkill(skills[index]));
            }
            if (skills.Count == 0)
                Label(shelf, "这里目前空空如也，像一个很有前途的周一。", 12, InkSoft, new Vector2(0f, -20f), TextAnchor.MiddleCenter, FontStyles.Italic);
            if (_selectedMonster != null && skills.Count > 0)
            {
                var equip = Button(shelf, "EQUIP", "装进所选怪物", new Vector2(128f, -68f), new Vector2(120f, 28f), CoralDark, 10);
                equip.onClick.AddListener(EquipSkill);
            }
        }

        void DrawOperationFooter(LevelTarget target, int energy, RunLedger run)
        {
            var footer = Panel(_content, "Footer", new Vector2(1550f, 100f), new Vector2(0f, -385f), new Color(.08f, .11f, .14f, .96f), 0f);
            Label(footer, "ENERGY", 12, Mint, new Vector2(-600f, 17f), TextAnchor.MiddleCenter, FontStyles.Bold);
            Label(footer, energy + " / " + target.EnergyDue, 28, Paper, new Vector2(-520f, 15f), TextAnchor.MiddleLeft, FontStyles.Bold);
            Label(footer, "本局回响", 12, InkSoft, new Vector2(15f, 17f), TextAnchor.MiddleCenter, FontStyles.Normal);
            Label(footer, "金币 " + run.Gold + "     科技点 " + run.TechPoints, 17, Gold, new Vector2(135f, 15f), TextAnchor.MiddleLeft, FontStyles.Bold);
            var confirm = Button(footer, "CONFIRM", "开始结算", new Vector2(590f, 0f), new Vector2(200f, 56f), Coral, 17);
            confirm.onClick.AddListener(ConfirmSettlement);
            Label(footer, target.ExcessEnergy + "+ 能量将点亮科技", 11, Mint, new Vector2(360f, -26f), TextAnchor.MiddleCenter, FontStyles.Italic);
        }

        void DrawTechPanel(RunLedger run)
        {
            var panel = Panel(_overlay, "TechPanel", new Vector2(760f, 600f), new Vector2(0f, 5f), new Color(.12f, .15f, .18f, .98f), 20f);
            LabelSized(panel, "研究档案", 32, Paper, new Vector2(-250f, 245f), new Vector2(500f, 48f), TextAnchor.MiddleCenter, FontStyles.Bold);
            LabelSized(panel, "科技点只能由超额交款获得。每次解锁改变下一次培育的形状。", 12, Mint, new Vector2(0f, 205f), new Vector2(650f, 34f), TextAnchor.MiddleCenter, FontStyles.Italic);
            var close = Button(panel, "CLOSE_TECH", "关闭", new Vector2(305f, 246f), new Vector2(86f, 34f), CoralDark, 11);
            close.onClick.AddListener(ToggleTech);
            for (var i = 0; i < TechCatalog.Names.Length; i++)
            {
                var name = TechCatalog.Names[i];
                var unlocked = IndexOf(run.UnlockedTech, name) >= 0;
                var column = i % 2;
                var row = i / 2;
                var card = Panel(panel, "Tech_" + i, new Vector2(310f, 104f), new Vector2(-170f + column * 340f, 120f - row * 122f), unlocked ? new Color(.29f, .42f, .38f, .96f) : new Color(.18f, .22f, .26f, .98f), 12f);
                LabelSized(card, name, 16, unlocked ? Paper : Gold, new Vector2(-54f, 25f), new Vector2(190f, 32f), TextAnchor.MiddleLeft, FontStyles.Bold);
                LabelSized(card, TechDescription(name), 10, unlocked ? Mint : PaperDark, new Vector2(-54f, -20f), new Vector2(190f, 34f), TextAnchor.MiddleLeft, FontStyles.Normal);
                if (!unlocked)
                {
                    var techName = name;
                    var button = Button(card, "UNLOCK_" + i, "解锁 · 1", new Vector2(92f, 0f), new Vector2(92f, 32f), run.TechPoints > 0 ? Violet : InkSoft, 10);
                    button.onClick.AddListener(() => UnlockTech(techName));
                }
                else
                    LabelSized(card, "已解锁", 11, Mint, new Vector2(92f, 0f), new Vector2(92f, 32f), TextAnchor.MiddleCenter, FontStyles.Bold);
            }
        }

        void DrawSellPanel()
        {
            var panel = Panel(_overlay, "SellPanel", new Vector2(920f, 650f), new Vector2(0f, 0f), new Color(.13f, .15f, .17f, .99f), 20f);
            LabelSized(panel, "怪物笼 · 出售台", 30, Paper, new Vector2(0f, 270f), new Vector2(650f, 48f), TextAnchor.MiddleCenter, FontStyles.Bold);
            LabelSized(panel, "已锁定培育方案的亲本不能出售。其他成员会换成半价金币。", 12, Mint, new Vector2(0f, 228f), new Vector2(780f, 34f), TextAnchor.MiddleCenter, FontStyles.Italic);
            var close = Button(panel, "CLOSE_SELL", "返回货架", new Vector2(372f, 272f), new Vector2(100f, 36f), CoralDark, 11);
            close.onClick.AddListener(ToggleSell);
            var monsters = TheCallApp.Interface.SendQuery(new MonsterCageQuery());
            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                var column = i % 2;
                var row = i / 2;
                var card = Panel(panel, "Sell_" + monster.Id, new Vector2(390f, 112f), new Vector2(-205f + column * 410f, 135f - row * 124f), Paper, 12f);
                DrawCreature(card, new Vector2(-115f, 2f), StableSeed(monster.Id), .29f, Ink);
                LabelSized(card, SkillsText(monster.SkillNames), 11, Ink, new Vector2(18f, 15f), new Vector2(150f, 52f), TextAnchor.MiddleLeft, FontStyles.Bold);
                var id = monster.Id;
                var sell = Button(card, "SELL_" + id, "出售", new Vector2(130f, -24f), new Vector2(82f, 30f), Coral, 11);
                sell.onClick.AddListener(() => SellMonster(id));
            }
            if (monsters.Count == 0)
                LabelSized(panel, "怪物笼空了。它们终于获得了自由，或者获得了别的界面。", 14, PaperDark, new Vector2(0f, 0f), new Vector2(700f, 40f), TextAnchor.MiddleCenter, FontStyles.Italic);
        }

        static string TechDescription(string name)
        {
            switch (name)
            {
                case "基因实验": return "允许把技能投进培育方案";
                case "槽位扩容": return "培育方案增加一个槽位";
                case "变异学说": return "后代获得额外技能机会";
                case "大乱炖": return "每个培育槽容纳三只亲本";
                case "科学培育": return "培育后代获得产能修正";
                default: return "一项尚未命名的研究";
            }
        }

        void DrawShop()
        {
            DrawBackdrop(new Color(.14f, .12f, .16f), new Color(.27f, .23f, .26f));
            var run = TheCallApp.Interface.SendQuery(new RunLedgerQuery());
            var target = TheCallApp.Interface.SendQuery(new LevelTargetQuery());
            DrawHeader(target.LevelNumber, run, "商店阶段");
            var shelf = TheCallApp.Interface.SendQuery(new ShelfQuery());
            var board = Panel(_content, "ShopBoard", new Vector2(1120f, 530f), new Vector2(0f, -20f), new Color(.94f, .9f, .81f, .98f), 20f);
            Decorate(board, false);
            Label(board, "午夜货架", 40, CoralDark, new Vector2(0f, 195f), TextAnchor.MiddleCenter, FontStyles.Bold);
            Label(board, "不买也没关系。它们会在你眨眼时消失。", 14, InkSoft, new Vector2(0f, 153f), TextAnchor.MiddleCenter, FontStyles.Italic);
            for (var i = 0; i < shelf.Monsters.Count; i++)
            {
                var item = shelf.Monsters[i];
                var x = -390f + i * 260f;
                var card = Panel(board, "ShopMonster_" + item.Id, new Vector2(220f, 250f), new Vector2(x, -13f), Paper, 15f);
                DrawCreature(card, new Vector2(0f, 46f), StableSeed(item.Id), .58f, Ink);
                Label(card, "货物 " + (i + 1).ToString("00"), 11, InkSoft, new Vector2(0f, 105f), TextAnchor.MiddleCenter, FontStyles.Bold);
                Label(card, SkillsText(item.SkillNames), 12, Ink, new Vector2(0f, -65f), TextAnchor.MiddleCenter, FontStyles.Bold);
                Label(card, item.Price + " 金币", 17, Gold, new Vector2(0f, -94f), TextAnchor.MiddleCenter, FontStyles.Bold);
                var id = item.Id;
                var buy = Button(card, "BUY_" + id, "收容", new Vector2(0f, -130f), new Vector2(116f, 34f), MintDark, 12);
                buy.onClick.AddListener(() => BuyMonster(id));
            }
            var toolStart = -135f;
            for (var i = 0; i < shelf.Tools.Count; i++)
            {
                var tool = shelf.Tools[i];
                var x = toolStart + i * 270f;
                var card = Panel(board, "ShopTool_" + tool.Name, new Vector2(230f, 92f), new Vector2(x, -215f), new Color(.82f, .87f, .82f, .98f), 12f);
                Label(card, "工具", 10, MintDark, new Vector2(-83f, 24f), TextAnchor.MiddleLeft, FontStyles.Bold);
                Label(card, tool.Name, 13, Ink, new Vector2(-18f, 24f), TextAnchor.MiddleLeft, FontStyles.Bold);
                Label(card, tool.Price + "g", 14, Gold, new Vector2(-80f, -20f), TextAnchor.MiddleLeft, FontStyles.Bold);
                var name = tool.Name;
                var buy = Button(card, "TOOL_" + name, "购买", new Vector2(65f, -18f), new Vector2(78f, 30f), Violet, 11);
                buy.onClick.AddListener(() => BuyTool(name));
            }
            var sell = Button(board, "SELL_PANEL", "打开怪物笼", new Vector2(-430f, -215f), new Vector2(170f, 46f), MintDark, 13);
            sell.onClick.AddListener(ToggleSell);
            var leave = Button(board, "LEAVE_SHOP", target.LevelNumber >= 7 ? "签字 · 完成一局" : "离开货架", new Vector2(420f, -215f), new Vector2(190f, 50f), Coral, 15);
            leave.onClick.AddListener(LeaveShop);
            Label(board, "金币 " + run.Gold, 19, Gold, new Vector2(-430f, -165f), TextAnchor.MiddleLeft, FontStyles.Bold);
            Label(board, "工资已结算 · 工具不会跨局", 11, InkSoft, new Vector2(-430f, -242f), TextAnchor.MiddleLeft, FontStyles.Italic);
            if (_showSell)
                DrawSellPanel();
        }

        void DrawEnd(bool victory)
        {
            DrawBackdrop(victory ? new Color(.12f, .2f, .18f) : new Color(.18f, .12f, .15f), new Color(.25f, .33f, .29f));
            var card = Panel(_content, "EndCard", new Vector2(900f, 550f), new Vector2(0f, 0f), Paper, 0f);
            Decorate(card, false);
            Label(card, victory ? "你把七关都交上去了" : "这次的账单没有被说服", 42, victory ? MintDark : CoralDark, new Vector2(0f, 130f), TextAnchor.MiddleCenter, FontStyles.Bold);
            Label(card, victory ? "THE CALL ANSWERED" : "THE CALL WENT UNANSWERED", 15, InkSoft, new Vector2(0f, 82f), TextAnchor.MiddleCenter, FontStyles.Italic);
            DrawCreature(card, new Vector2(0f, -40f), victory ? 8 : 9, 1.06f, victory ? MintDark : CoralDark);
            Label(card, victory ? "怪物们获得了短暂的安静。你获得了更长的怪物清单。" : "失败也会清空一局，但不会清空这个荒谬世界。", 16, InkSoft, new Vector2(0f, -175f), TextAnchor.MiddleCenter, FontStyles.Normal);
            var restart = Button(card, "RESTART", victory ? "再来一局" : "重启实验", new Vector2(0f, -245f), new Vector2(190f, 52f), victory ? MintDark : Coral, 16);
            restart.onClick.AddListener(Restart);
        }

        void DrawHeader(int level, RunLedger run, string phase)
        {
            var header = Panel(_content, "Header", new Vector2(1550f, 82f), new Vector2(0f, 409f), new Color(.07f, .1f, .13f, .96f), 0f);
            LabelSized(header, "THE CALL", 24, Paper, new Vector2(-685f, 0f), new Vector2(180f, 42f), TextAnchor.MiddleLeft, FontStyles.Bold);
            LabelSized(header, "// 研究员工作台", 12, Mint, new Vector2(-510f, 0f), new Vector2(250f, 34f), TextAnchor.MiddleLeft, FontStyles.Italic);
            LabelSized(header, "第 " + level + " / 7 关", 18, Gold, new Vector2(-110f, 0f), new Vector2(160f, 40f), TextAnchor.MiddleCenter, FontStyles.Bold);
            LabelSized(header, phase, 14, Paper, new Vector2(80f, 0f), new Vector2(160f, 36f), TextAnchor.MiddleCenter, FontStyles.Normal);
            if (phase == "操作阶段")
            {
                var tech = Button(header, "TECH_PANEL", "科技 " + run.TechPoints, new Vector2(300f, 0f), new Vector2(150f, 42f), Violet, 12);
                tech.onClick.AddListener(ToggleTech);
            }
            LabelSized(header, "金币 " + run.Gold, 15, Gold, new Vector2(590f, 0f), new Vector2(270f, 40f), TextAnchor.MiddleRight, FontStyles.Bold);
        }

        void DrawBackdrop(Color top, Color bottom)
        {
            var bg = Image(_content, "Backdrop", Color.Lerp(top, bottom, .5f), Vector2.zero, new Vector2(1600f, 900f), 0f);
            var texture = MakePaperTexture(512, 512, top, bottom);
            bg.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            bg.type = UnityEngine.UI.Image.Type.Tiled;
            bg.color = new Color(1f, 1f, 1f, .94f);
            bg.raycastTarget = false;
            for (var i = 0; i < 14; i++)
            {
                var fleck = Image(_content, "InkFleck_" + i, new Color(1f, 1f, 1f, .03f), new Vector2(-720f + (i * 123) % 1450, -360f + (i * 211) % 720), new Vector2(3f + i % 4, 3f + i % 5), 2f);
                fleck.raycastTarget = false;
            }
        }

        void DrawToast(string message)
        {
            var toast = Panel(_overlay, "Toast", new Vector2(610f, 72f), new Vector2(0f, -320f), new Color(.08f, .1f, .12f, .96f), 16f);
            Label(toast, message, 16, Paper, Vector2.zero, TextAnchor.MiddleCenter, FontStyles.Bold);
        }

        void DrawCreature(GameObject parent, Vector2 position, int seed, float scale, Color accent) => DrawCreature(parent.transform, position, seed, scale, accent);

        void DrawCreature(Transform parent, Vector2 position, int seed, float scale, Color accent)
        {
            var holder = new GameObject("MonsterIllustration_" + seed, typeof(RectTransform)).transform;
            holder.SetParent(parent, false);
            var rect = holder as RectTransform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(180f, 180f);
            rect.localScale = Vector3.one * scale;
            var body = Pick(_body, seed);
            var head = Pick(_head, seed + 1);
            var eye = Pick(_eye, seed + 2);
            var mouth = Pick(_mouth, seed + 3);
            var tail = Pick(_tail, seed + 4);
            var hat = Pick(_hat, seed + 5);
            AddPart(holder, body, Vector2.zero, new Vector2(1.08f, 1.08f), Color.white);
            AddPart(holder, tail, new Vector2(57f, -10f), new Vector2(.82f, .82f), Color.white);
            AddPart(holder, head, new Vector2(-5f, 20f), new Vector2(.97f, .97f), Color.white);
            AddPart(holder, eye, new Vector2(0f, 26f), new Vector2(.85f, .85f), Color.white);
            AddPart(holder, mouth, new Vector2(0f, -2f), new Vector2(.9f, .9f), Color.white);
            AddPart(holder, hat, new Vector2(0f, 74f), new Vector2(.72f, .72f), accent);
            var halo = Image(holder, "Aura", new Color(accent.r, accent.g, accent.b, .10f), new Vector2(0f, 0f), new Vector2(158f, 158f), 1f);
            halo.transform.SetAsFirstSibling();
            halo.raycastTarget = false;
            _animated.Add(halo);
        }

        static void AddPart(Transform parent, Sprite sprite, Vector2 pos, Vector2 scale, Color color)
        {
            if (sprite == null)
                return;
            var image = Image(parent, sprite.name, Color.white, pos, new Vector2(150f, 150f), 3f);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.localScale = scale;
        }

        void Keep(string id)
        {
            RunCommand(new KeepOpeningMonsterCommand(id), "候选已收容。六只新成员正在抱怨。");
        }

        void BeginLevel()
        {
            RunCommand(new BeginLevelCommand(), "操作台已解锁。");
        }

        void SelectMonster(string id)
        {
            _selectedMonster = _selectedMonster == id ? null : id;
            _selectedSkill = null;
            Refresh();
        }

        void SelectSkill(string skill)
        {
            _selectedSkill = _selectedSkill == skill ? null : skill;
            Refresh();
        }

        void ClickExtraction(int index, string occupied)
        {
            if (occupied != null)
                RunCommand(new ReturnMonsterCommand(occupied), "怪物回到了笼中，并表示它早就想回去了。");
            else if (_selectedMonster != null)
                RunCommand(new PlaceMonsterCommand(_selectedMonster, OperationArea.Extraction, index), "已安排到提取轨。");
            else
                Notice("先在怪物笼选择一只怪物。");
        }

        void ClickBreeding(int index, string occupied)
        {
            if (occupied != null)
                RunCommand(new ReturnMonsterCommand(occupied), "亲本已撤回。");
            else if (_selectedMonster != null)
                RunCommand(new PlaceMonsterCommand(_selectedMonster, OperationArea.Breeding, index), "亲本已入座。");
            else
                Notice("培育槽需要一只被选中的亲本。");
        }

        void PlaceBreedingSkill()
        {
            if (string.IsNullOrEmpty(_selectedSkill))
            {
                Notice("先点选技能槽里的能力。");
                return;
            }
            var plans = TheCallApp.Interface.SendQuery(new BreedingPlansQuery());
            if (plans.Count == 0)
                return;
            var slot = plans[0].SkillName == null ? 0 : -1;
            if (slot < 0)
            {
                Notice("培育方案已经有技能了。");
                return;
            }
            var ledger = TheCallApp.Interface.SendQuery(new RunLedgerQuery());
            var index = IndexOf(ledger.SkillSlots, _selectedSkill);
            if (index >= 0)
                RunCommand(new PlaceBreedingSkillCommand(0, index), "培育技能已装入。");
        }

        void EquipSkill()
        {
            if (string.IsNullOrEmpty(_selectedMonster) || string.IsNullOrEmpty(_selectedSkill))
            {
                Notice("需要同时选择怪物和技能。");
                return;
            }
            var ledger = TheCallApp.Interface.SendQuery(new RunLedgerQuery());
            var index = IndexOf(ledger.SkillSlots, _selectedSkill);
            if (index >= 0)
                RunCommand(new EquipSkillCommand(_selectedMonster, index), "技能已缝进怪物身上。它现在更像一个完整的句子了。");
        }

        void ConfirmSettlement()
        {
            if (_isBusy)
                return;
            _isBusy = true;
            TheCallApp.Interface.SendCommand(new ConfirmSettlementCommand());
            var record = TheCallApp.Interface.SendQuery(new SettlementRecordQuery());
            var gain = 0;
            for (var i = 0; i < record.Count; i++)
                if (record[i] is SettlementLanding landing)
                    gain += landing.Energy;
            _isBusy = false;
            var ledger = TheCallApp.Interface.SendQuery(new RunLedgerQuery());
            var phase = TheCallApp.Interface.SendQuery(new RunPhaseQuery());
            if (phase == RunPhase.Shop)
            {
                Notice("结算完成：+" + gain + " 能量。工资与商店已开门。");
                Refresh();
            }
            else if (phase == RunPhase.Operation)
            {
                var shortfall = TheCallApp.Interface.SendQuery(new LevelShortfallQuery());
                Notice("能量不足，欠额 " + shortfall + "。这是加班，不是重开。");
                Refresh();
            }
            else
            {
                Notice(phase == RunPhase.Victory ? "第七关完成。" : "加班仍未补足，实验结束。");
                Refresh();
            }
        }

        void DiscardMonster(string id)
        {
            RunCommand(new DiscardMonsterCommand(id), "怪物已废弃，随机技能被收进技能槽。");
        }

        void ToggleTech()
        {
            _showTech = !_showTech;
            Refresh();
        }

        void UnlockTech(string name)
        {
            RunCommand(new UnlockTechCommand(name), "科技已写入研究档案。");
        }

        void ToggleSell()
        {
            _showSell = !_showSell;
            Refresh();
        }

        void SellMonster(string id)
        {
            RunCommand(new SellMonsterCommand(id), "怪物已售出，所得金币已入账。");
        }

        void BuyMonster(string id)
        {
            RunCommand(new BuyMonsterCommand(id), "怪物已收容。它对价格没有意见，对笼子有意见。");
        }

        void BuyTool(string name)
        {
            RunCommand(new BuyToolCommand(name), "工具已加入工作台。");
        }

        void LeaveShop()
        {
            RunCommand(new LeaveShopCommand(), "离开商店，下一关正在翻页。");
        }

        void Restart()
        {
            TheCallApp.Reset();
            Refresh();
        }

        void RunCommand(ICommand command, string success)
        {
            if (_isBusy)
                return;
            _isBusy = true;
            TheCallApp.Interface.SendCommand(command);
            _isBusy = false;
            _selectedMonster = null;
            _selectedSkill = null;
            Notice(success);
            Refresh();
        }

        void Notice(string message)
        {
            _notice = message;
            _noticeUntil = Time.unscaledTime + 2.8f;
            Refresh();
        }

        static int StableSeed(string id)
        {
            if (string.IsNullOrEmpty(id))
                return 1;
            var seed = 17;
            for (var i = 0; i < id.Length; i++)
                seed = seed * 31 + id[i];
            return Mathf.Abs(seed);
        }

        static Sprite Pick(Sprite[] sprites, int seed)
        {
            if (sprites == null || sprites.Length == 0)
                return null;
            return sprites[Mathf.Abs(seed) % sprites.Length];
        }

        static int IndexOf(IReadOnlyList<string> values, string value)
        {
            if (values == null)
                return -1;
            for (var i = 0; i < values.Count; i++)
                if (values[i] == value)
                    return i;
            return -1;
        }

        static string SkillShort(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "未知部件";
            return name.Replace("器官", "").Replace("能量", "能").Replace("提取", "取");
        }

        static string SkillsText(IReadOnlyList<string> skills)
        {
            var text = "";
            for (var i = 0; i < skills.Count; i++)
            {
                if (i > 0)
                    text += "\n";
                text += SkillShort(skills[i]);
            }
            return text;
        }

        static string QuoteText(int quote) => quote > 0 ? "报价 " + quote : "效果型";

        static string RarityText(Rarity rarity) => rarity == Rarity.Gold ? "金" : rarity == Rarity.Blue ? "蓝" : "白";

        GameObject Panel(GameObject parent, string name, Vector2 size, Vector2 position, Color color, float radius) => Panel(parent.transform, name, size, position, color, radius);

        GameObject Panel(Transform parent, string name, Vector2 size, Vector2 position, Color color, float radius)
        {
            var image = Image(parent, name, color, position, size, 2f);
            image.sprite = MakeRoundedSprite(64, 64, radius / Mathf.Max(size.x, size.y));
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            return image.gameObject;
        }

        void Decorate(GameObject panel, bool small)
        {
            var rect = panel.GetComponent<RectTransform>();
            var line = Image(panel.transform, "EdgeInk", new Color(Coral.r, Coral.g, Coral.b, .42f), new Vector2(0f, rect.rect.height * .5f - 3f), new Vector2(rect.rect.width - 20f, 3f), 4f);
            line.raycastTarget = false;
            var dot = Image(panel.transform, "Pin", Gold, new Vector2(-rect.rect.width * .5f + 19f, rect.rect.height * .5f - 18f), new Vector2(10f, 10f), 6f);
            dot.raycastTarget = false;
        }

        Button Button(GameObject parent, string name, string text, Vector2 position, Vector2 size, Color color, int fontSize) => Button(parent.transform, name, text, position, size, color, fontSize);

        Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size, Color color, int fontSize)
        {
            var go = Panel(parent, name, size, position, color, 12f);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, .2f);
            colors.pressedColor = Color.Lerp(color, Ink, .2f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = .08f;
            button.colors = colors;
            Label(go.transform, text, fontSize, Paper, Vector2.zero, TextAnchor.MiddleCenter, FontStyles.Bold);
            return button;
        }

        TextMeshProUGUI Label(GameObject parent, string text, int size, Color color, Vector2 position, TextAnchor alignment, FontStyles style) => Label(parent.transform, text, size, color, position, alignment, style);

        TextMeshProUGUI Label(Transform parent, string text, int size, Color color, Vector2 position, TextAnchor alignment, FontStyles style) => LabelSized(parent, text, size, color, position, new Vector2(520f, Mathf.Max(28f, size * 2.1f)), alignment, style);

        TextMeshProUGUI LabelSized(GameObject parent, string text, int size, Color color, Vector2 position, Vector2 dimensions, TextAnchor alignment, FontStyles style) => LabelSized(parent.transform, text, size, color, position, dimensions, alignment, style);

        TextMeshProUGUI LabelSized(Transform parent, string text, int size, Color color, Vector2 position, Vector2 dimensions, TextAnchor alignment, FontStyles style)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = _font;
            label.fontSize = size;
            label.color = color;
            label.alignment = ToTmpAlignment(alignment);
            label.fontStyle = style;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        static UnityEngine.UI.Image Image(Transform parent, string name, Color color, Vector2 position, Vector2 size, float z)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, z);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TextAlignmentOptions ToTmpAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        static Texture2D MakePaperTexture(int width, int height, Color top, Color bottom)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            var random = new System.Random(17);
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                var gradient = y / (float)height;
                for (var x = 0; x < width; x++)
                {
                    var grain = ((float)random.NextDouble() - .5f) * .045f;
                    pixels[y * width + x] = Color.Lerp(top, bottom, gradient) + new Color(grain, grain, grain, 0f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Sprite MakeRoundedSprite(int width, int height, float normalizedRadius)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            var radius = Mathf.Clamp(Mathf.RoundToInt(width * normalizedRadius), 3, width / 2 - 1);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = Mathf.Max(radius - x, 0, x - (width - radius - 1));
                    var dy = Mathf.Max(radius - y, 0, y - (height - radius - 1));
                    var alpha = dx * dx + dy * dy <= radius * radius ? 1f : 0f;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
