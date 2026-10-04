using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class SkillCatalog : IUtility
    {
        public IReadOnlyList<string> Names { get; } = new[]
        {
            "能量吐息",
            "左能量体",
            "右能量体",
            "增量小手",
            "增量大手",
            "残留提取腺体",
            "孤独心",
            "吞噬大嘴",
            "双重吐息",
            "时间操控器官",
            "再回首头",
            "分享之手",
            "太阳能头",
            "换位手",
            "鼓励嘴",
        };
    }
}
