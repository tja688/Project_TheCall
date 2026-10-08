using System.Collections.Generic;
using System.Linq;

namespace TheCall.Tests
{
    static class DrawAdapt
    {
        static readonly HashSet<string> Amplify = new HashSet<string>
        {
            "吞噬大嘴", "良好肉体", "优质肉体", "争锋基因", "潜藏基因", "重血脉心", "孤独心",
        };

        public static string[] PendingCage { get; private set; }

        public static void ClearPending() => PendingCage = null;

        static readonly string[] FixedOpening =
        {
            "吞噬大嘴", "能量体",
            "良好肉体", "左能量体",
            "优质肉体", "右能量体",
            "能量体", "左能量体", "右能量体", "双头能量体",
            "奇异香", "怪异香", "回响嗓", "镜眼",
        };

        public static string[] Adapt(string[] names)
        {
            PendingCage = null;
            if (names == null || names.Length == 0)
                return names ?? new string[0];

            var mapped = new string[names.Length];
            for (var i = 0; i < names.Length; i++)
                mapped[i] = Map(names[i]);

            if (IsTail(mapped[0]) || Already(mapped))
                return mapped;

            var split = 0;
            while (split < mapped.Length && split < 9 && !IsTail(mapped[split]))
                split++;

            PendingCage = Cage(mapped, split);
            var queue = new List<string>(FixedOpening);
            for (var i = split; i < mapped.Length; i++)
                queue.Add(mapped[i]);

            return queue.ToArray();
        }

        public static void Restore(QFramework.IArchitecture app)
        {
            if (PendingCage == null)
                return;

            var run = app.GetModel<RunModel>();
            var ids = app.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();
            for (var i = 0; i < ids.Length; i++)
                run.TryDestroy(ids[i]);

            for (var i = 0; i < PendingCage.Length; i++)
                run.AddToCage(run.CreateMonster(new[] { PendingCage[i] }));

            PendingCage = null;
        }

        static string[] Cage(string[] names, int split)
        {
            var cage = new List<string> { split > 0 ? names[0] : "能量体" };
            for (var i = 3; i < split; i++)
                cage.Add(names[i]);

            while (cage.Count < 7)
                cage.Add("能量体");

            return cage.ToArray();
        }

        static bool Already(string[] names) =>
            names.Length >= 6 &&
            Amplify.Contains(names[0]) && !Amplify.Contains(names[1]) &&
            Amplify.Contains(names[2]) && !Amplify.Contains(names[3]) &&
            Amplify.Contains(names[4]);

        static string Map(string name)
        {
            if (name == "1")
                return "单";
            if (name == "2")
                return "双";
            if (name == "独孤装置")
                return "劣胜装置";

            return name;
        }

        static bool IsTail(string name) =>
            name == "单" || name == "双" || name == "幅" || name == "白" || name == "蓝" || name == "金" ||
            name == "急急装置" || name == "上级员工证" || name == "劣胜装置";
    }
}
