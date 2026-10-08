namespace TheCall
{
    /// <summary>
    /// 本局内按生成顺序分配的收容编号（SCP 风格）。与技能无关；策划对照表见 Assets/Docs/4-数据汇总/怪物收容编号.md。
    /// </summary>
    internal static class MonsterCodenames
    {
        static readonly string[] Serial =
        {
            "SCP-173",
            "SCP-096",
            "SCP-682",
            "SCP-999",
            "SCP-049",
            "SCP-106",
            "SCP-939",
            "SCP-914",
            "SCP-2317",
            "SCP-079",
            "SCP-035",
            "SCP-087",
            "SCP-3008",
            "SCP-2521",
            "SCP-1313",
            "SCP-055",
            "SCP-178",
            "SCP-529",
            "SCP-966",
            "SCP-457",
            "SCP-1048",
            "SCP-1171",
            "SCP-1504",
            "SCP-1844",
            "SCP-1981",
            "SCP-2176",
            "SCP-2845",
            "SCP-3199",
            "SCP-3549",
            "SCP-4003",
            "SCP-5000",
            "SCP-001",
        };

        public static string ForSerial(int serial)
        {
            if (serial < 1)
                return "SCP-???";

            var index = serial - 1;
            if (index < Serial.Length)
                return Serial[index];

            return "SCP-" + (4000 + index - Serial.Length);
        }
    }
}
