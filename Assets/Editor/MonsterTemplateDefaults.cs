using UnityEngine;
namespace TheCall.Editor
{
    static class MonsterTemplateDefaults
    {
        public static MonsterArtCatalog.Palette[] Palettes() => new[] {
            Palette(0,"苔绿","#BBD6C6","#536E64","#F4A7BC","#A35D76"),
            Palette(1,"暮紫","#BDB1D4","#62507F","#F5A3CD","#886299"),
            Palette(2,"莓红","#E7ACBC","#8B405B","#BFE9D5","#BD607F"),
            Palette(3,"奶油","#FFF0B9","#C4A56C","#F4BA62","#B58A4D"),
            Palette(4,"雾青","#D5EAE4","#719B96","#A5F4D5","#71958C"),
            Palette(5,"月蓝","#CADCEC","#667F9D","#EDC1DE","#8C78A4")
        };
        static MonsterArtCatalog.Palette Palette(int id,string name,string light,string shade,string eye,string mouth) =>
            new MonsterArtCatalog.Palette {id=(MonsterPalette)id,name=name,body=Hex(light),pattern=Hex(shade),eye=Hex(eye),mouth=Hex(mouth)};
        static Color Hex(string value) {ColorUtility.TryParseHtmlString(value,out var color);return color;}
    }
}
