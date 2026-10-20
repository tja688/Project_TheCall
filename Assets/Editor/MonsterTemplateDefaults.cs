using System;
using System.Collections.Generic;
using UnityEngine;
using C = TheCall.MonsterArtCatalog.ColorRole;
using L = TheCall.MonsterArtCatalog.Layer;
using B = TheCall.MonsterArtCatalog.BodyPlan;
using A = TheCall.MonsterArtCatalog.Accessory;
using G = TheCall.MonsterArtCatalog.MotionGroup;
namespace TheCall.Editor
{
    // Authored compositions transcribed from 怪物实例.jpg. Apply() preserves later Inspector edits.
    static class MonsterTemplateDefaults
    {
        static L P(string name, string art, float x, float y, float w, float h, C color = C.Body, float angle = 0, bool flip = false, int group = 0, bool stretch = false) =>
            new L { name = name, part = art, position = new Vector2(x,y), size = new Vector2(w,h), color = color, angle = angle, mirror = flip, group = group, stretch = stretch };
        static L E(string name, float x, float y, float w = 23, float h = 25, int group = 0, bool flip = false)
        { var l = P(name, "713", x,y,w,h,C.Eye,0,flip,group); l.expression = true; return l; }
        static L Stitch(string name, float x, float y, float w = 32, float angle = 0, int group = 0) => P(name,"674",x,y,w,w,C.Pattern,angle,false,group);
        static B Plan(int id, string name, Vector2 crown, Vector2 back, params L[] layers)
        {
            var heads = new List<A>(); var backs = new List<A>();
            for (var h = 1; h < 4; h++) if (MonsterAppearance.Compatible(id,h,0))
                heads.Add(new A { id = h, layers = new[] { P("HeadAccessory", h == 1 ? "649" : h == 2 ? "639" : "658", crown.x,crown.y,h == 3 ? 28 : 23,h == 3 ? 21 : 32, h == 3 ? C.Pattern : C.Bone) } });
            for (var b = 1; b < 3; b++) if (MonsterAppearance.Compatible(id,0,b))
                backs.Add(new A { id = b, layers = new[] {
                    P("BackAccessoryL",b == 1 ? "552" : "551",back.x-44,back.y,66,55,C.RearBody,22,true,5),
                    P("BackAccessoryR",b == 1 ? "552" : "551",back.x+44,back.y,66,55,C.RearBody,-22,false,5) } });
            return new B { id = (MonsterTemplate)id, name = name, layers = layers, head = heads.ToArray(), back = backs.ToArray(),
                frame = new Rect(-142,15,284,272), groups = new[] {
                    new G { pivot = new Vector2(0,110), swing = .18f },
                    new G { pivot = new Vector2(-43,180), swing = .65f },
                    new G { pivot = new Vector2(43,180), swing = .65f, phase = .5f },
                    new G { pivot = new Vector2(-45,125), swing = 1.2f, phase = .2f },
                    new G { pivot = new Vector2(45,125), swing = -1.2f, phase = .2f },
                    new G { pivot = back, swing = 1.8f, phase = -.4f } } };
        }
        public static B[] Plans()
        {
            var crawler = Plan(0,"双头虫足",new Vector2(-44,229),new Vector2(0,149),
                P("LegRearL","822",-83,108,63,112,C.Ink,-18), P("LegRearR","822",83,108,63,112,C.Ink,18,true),
                P("LegMiddleL","822",-57,76,62,115,C.Ink,-4), P("LegMiddleR","822",57,76,62,115,C.Ink,4,true),
                P("Core","943",0,130,124,105,C.Body,0,false,0,true),
                P("HeadLeft","936",-44,199,75,60,C.Body,0,false,1), P("HeadRight","936",44,205,75,60,C.Body,0,false,2),
                P("EarL","552",-79,227,42,35,C.Ink,14,false,1), P("EarR","552",-7,229,42,35,C.Ink,-14,true,1),
                P("FixedHorn","406",62,226,25,53,C.Ink,-19,false,2),
                P("LegFrontL","822",-23,63,57,105,C.Ink,20), P("LegFrontR","822",23,63,57,105,C.Ink,-20,true),
                P("SpineL","730",-35,157,41,19,C.Pattern,-36), P("SpineR","730",51,149,42,20,C.Pattern,18),
                P("SpineLower","730",4,102,40,20,C.Pattern,-28),
                Stitch("HeadSeamL",-39,214,29,-8,1), Stitch("HeadSeamR",45,186,35,18,2), Stitch("BellySeam",-12,112,36,27),
                P("Grin","835",-44,183,60,27,C.Bone,0,false,1), P("ClosedMouth","538",46,190,48,19,C.Ink,0,false,2),
                E("EyeLeftA",-61,203,22,20,1), E("EyeLeftB",-29,203,22,20,1,true),
                P("ButtonEyeFrame","704",31,210,29,29,C.Eye,0,false,2), E("EyeRight",63,214,17,18,2));
            var patch = Plan(1,"露脑缀眼",new Vector2(-60,252),new Vector2(0,135),
                P("RearFoot","817",38,45,37,42,C.RearBody,0,true,4), P("RearHand","752",74,127,37,48,C.RearBody,-65,true,4),
                P("Core","520",9,124,143,151),
                P("StalkLeft","918",-43,218,83,76,C.RearBody,-40), P("StalkTop","918",0,252,76,69,C.RearBody,20,true),
                P("FrontHand","745",-59,102,58,93,C.RearBody,-13,false,3), P("FrontFoot","817",-20,45,39,43,C.RearBody),
                P("Brain","635",12,207,110,77,C.Body),
                Stitch("BellySeam",31,111,41), Stitch("ShoulderSeam",-45,208,29,17), Stitch("SideSeam",-36,92,29,38),
                P("ButtonTop","912",-3,267,24,25,C.Eye), P("ButtonSide","912",-57,224,24,25,C.Eye),
                P("ButtonBrow","912",18,237,26,27,C.Eye), P("ButtonBelly","912",19,105,26,27,C.Eye),
                E("Eye",48,222,11,12), P("ClosedMouth","538",30,170,62,23,C.Pattern));
            patch.frame = new Rect(-142,15,284,290);
            var stars = Plan(2,"双星连体",new Vector2(-91,202),new Vector2(0,151),
                P("StarRight","990",57,151,158,129,C.Body), P("StarLeft","990",-57,151,158,129,C.Body),
                E("EyeLeftA",-77,160,21,26), E("EyeLeftB",-45,173,21,26),
                E("EyeRightA",38,169,22,23), E("EyeRightB",78,166,22,23,0,true),
                P("Smile","844",-59,137,64,27,C.Ink,8), P("Grimace","837",58,135,64,27,C.Bone,-9));
            stars.frame = new Rect(-145,63,290,207);
            var tendrils = Plan(3,"双头触须",new Vector2(-48,237),new Vector2(0,140),
                P("TendrilUpperL","833",-73,151,83,40,C.Body,-15,false,3), P("TendrilUpperR","833",73,151,83,40,C.Body,15,true,4),
                P("TendrilMiddleL","833",-74,106,83,40,C.Body,8,true,3), P("TendrilMiddleR","833",74,106,83,40,C.Body,-8,false,4),
                P("TendrilLowerL","833",-58,53,89,43,C.Body,15,false,3), P("TendrilLowerR","833",58,53,89,43,C.Body,-15,true,4),
                P("CrossLeft","997",-18,130,102,155,C.Body,0), P("CrossRight","997",18,130,102,155,C.Body,0,true),
                P("HeadLeft","936",-48,210,80,64,C.Body,15,false,1), P("HeadRight","936",48,210,80,64,C.Body,-15,true,2),
                P("EarLeft","639",-70,232,31,33,C.Body,23,false,1), P("EarRight","639",70,232,31,33,C.Body,-23,true,2),
                Stitch("CoreSeam",0,137,58), Stitch("SeamLeft",-51,194,25,13,1), Stitch("SeamRight",51,194,25,-13,2),
                P("RivetL","545",-84,209,27,30,C.Bone,-45,false,1), P("RivetR","545",65,240,27,30,C.Bone,25,false,2),
                P("RivetTop","545",-24,245,27,30,C.Bone,13,false,1), P("RivetBelly","545",34,100,24,27,C.Bone,30),
                P("ButtonFrameL","704",-48,213,30,30,C.Eye,0,false,1), P("ButtonFrameR","704",48,213,30,30,C.Eye,0,false,2),
                E("EyeLeft",-48,213,16,17,1), E("EyeRight",48,213,16,17,2));
            var cluster = Plan(4,"多眼星团",new Vector2(-38,199),new Vector2(0,144),
                P("FeelLeft","705",-45,201,23,66,C.Ink,18), P("FeelTop","705",31,218,23,68,C.Ink,-24,true),
                P("FeelRight","705",78,173,22,63,C.Ink,-67,true), P("FeelLower","705",70,104,21,58,C.Ink,-115),
                P("FootL","812",-26,87,35,30,C.Ink), P("FootR","812",53,58,35,30,C.Ink),
                P("StarRearTop","990",47,173,135,111,C.Body,14), P("StarRearBottom","990",48,115,135,111,C.Body,-12),
                P("StarFront","990",-38,146,171,140,C.Body),
                E("RearEyeA",34,190,17,19), E("RearEyeB",72,181,17,19),
                E("LowerEyeA",38,115,17,19), E("LowerEyeB",74,102,17,19),
                E("EyeLeft",-76,154,24,22), E("EyeRight",-20,161,24,22));
            var brain = RoundOpen(5,"露脑圆身",true);
            var cat = Plan(6,"猫耳缝合",new Vector2(-39,212),new Vector2(0,151),
                P("RearArm","812",62,130,33,38,C.Ink,-13), P("RearFoot","812",27,45,34,28,C.Ink),
                P("Core","520",0,127,146,154),
                P("CatEarL","639",-39,201,39,43,C.Body,20), P("CatEarR","639",44,203,39,43,C.Body,-20,true),
                P("FrontArm","812",-60,128,32,45,C.Ink,-12), P("FrontFoot","812",-25,46,35,28,C.Ink),
                P("Grin","835",32,168,68,28,C.Bone,-6),
                Stitch("ForeheadSeam",27,207,32,30), Stitch("CheekSeam",-28,175,31,44), Stitch("BellySeam",39,93,31,-18),
                P("EyeFrame","704",-4,194,40,40,C.Eye), E("Eye",-4,194,20,21));
            var slug = Plan(7,"横卧翼体",new Vector2(-77,158),new Vector2(31,141),
                P("Core","951",34,106,187,82,C.Body,0,false,0,true), P("Head","936",-77,122,83,67,C.Body,0,false,1),
                P("HeadWingL","552",-106,148,40,31,C.RearBody,16,false,1), P("HeadWingR","552",-49,150,40,31,C.RearBody,-13,true,1),
                P("BackWing","552",39,151,53,44,C.RearBody,10,false,5),
                P("StarMarkA","710",-7,112,20,20,C.Pattern,14), P("StarMarkB","710",61,92,27,27,C.Pattern,-16),
                P("StarMarkC","710",89,117,23,23,C.Pattern,15), P("StarMarkD","710",21,83,24,24,C.Pattern,18),
                P("Bow","658",-44,79,29,21,C.Pattern), P("ClosedMouth","538",-83,102,58,20,C.Ink,0,false,1),
                E("Eye",-73,130,23,24,1));
            slug.frame = new Rect(-148,60,296,185); slug.groups[1].pivot = new Vector2(-71,117);
            var rivet = RoundOpen(8,"钉饰短肢",false);
            var horn = RoundOpen(9,"独角黑肢",false);
            var feelers = Plan(10,"须角白肢",new Vector2(32,214),new Vector2(0,151),
                P("FeelTop","705",-24,224,21,61,C.Ink,20), P("FeelMid","705",-50,207,20,58,C.Ink,45), P("FeelLow","705",-58,182,19,57,C.Ink,74),
                P("RearArm","801",48,145,39,70,C.Body,14,true,4), P("RearFoot","801",30,67,35,45,C.Body,-10,true),
                P("Core","508",0,139,128,149), P("FrontArm","801",-48,108,40,77,C.Body,-18,false,3),
                P("FrontFoot","801",-5,47,35,48,C.Body,8), P("ClosedMouth","848",30,172,50,20,C.Ink,15), E("Eye",-4,201,31,28));
            var octopus = Plan(11,"蝠耳触手",new Vector2(-17,247),new Vector2(0,165),
                P("RearTentacleL","799",-74,128,82,72,C.RearBody,18,false,3), P("RearTentacleR","799",74,128,82,72,C.RearBody,-18,true,4),
                P("MidTentacleL","799",-47,109,80,71,C.Body,-10,false,3), P("MidTentacleR","799",47,109,80,71,C.Body,10,true,4),
                P("OralCavity","484",17,189,73,37,C.Mouth,0,false,0,true), P("InnerTongue","506",20,180,57,18,C.Tongue),
                P("UpperTeeth","718",20,200,66,23,C.Bone), P("Core","464",0,176,111,130),
                P("Tongue","407",22,169,51,33,C.Tongue,-5),
                P("BatEarL","552",-48,234,40,33,C.Body,12), P("BatEarR","552",-7,250,40,33,C.Body,-14,true),
                P("FrontTentacleL","799",-18,100,74,66,C.Body,-28,false,3), P("FrontTentacleR","799",24,100,74,66,C.Body,28,true,4),
                E("Eye",27,222,23,24));
            return new[] { crawler,patch,stars,tendrils,cluster,brain,cat,slug,rivet,horn,feelers,octopus };
        }
        static B RoundOpen(int id, string name, bool brain)
        {
            var dark = id == 9; var layers = new List<L> {
                P("RearArm",dark ? "806" : "752",48,145,37,58,dark ? C.Ink : C.RearBody,-18,true,4),
                P("RearFoot",dark ? "806" : "817",25,65,35,44,dark ? C.Ink : C.RearBody,0,true),
                P("OralCavity","484",20,brain ? 154 : 150,brain ? 84 : 83,42,C.Mouth,0,false,0,true),
                P("InnerTongue","506",23,brain ? 146 : 143,64,19,C.Tongue), P("UpperTeeth","718",23,brain ? 166 : 162,73,26,C.Bone),
                P("LowerTeeth","719",20,brain ? 144 : 140,65,22,C.Bone,180),
                P("Core",brain ? "516" : "464",0,133,brain ? 147 : 124,155),
                P("FrontArm",brain ? "745" : dark ? "806" : "729",-52,115,brain ? 55 : 36,brain ? 77 : 69,dark ? C.Ink : C.Body,-13,false,3),
                P("FrontFoot",dark ? "806" : "817",-20,63,36,44,dark ? C.Ink : C.Body,8) };
            if (brain) { layers.Add(P("Brain","635",-6,216,113,80,C.Body)); layers.Add(E("Eye",32,231,12,13)); }
            else
            {
                layers.Add(P("Tongue","407",24,138,49,38,C.Tongue,-8)); layers.Add(E("Eye",-5,185,29,27));
                if (dark) layers.Add(P("FixedHorn","406",34,216,28,42,C.Ink,-26));
                else { layers.Add(P("RivetTop","545",29,218,29,30,C.Bone,28)); layers.Add(P("RivetSide","545",-50,192,28,29,C.Bone,-58)); layers.Add(Stitch("SideSeam",31,103,34,30)); }
            }
            return Plan(id,name,new Vector2(-13,211),new Vector2(0,146),layers.ToArray());
        }
        public static MonsterArtCatalog.Palette[] Palettes()
        {
            return new[] {
                Colorway(0,"苔绿","96B3A2","738E7E","F2CA75","8B536C"), Colorway(1,"莓紫","A792C2","826F9A","A9D8B0","C97864"),
                Colorway(2,"灰玫瑰","C38F9C","9B6C78","92CDD2","596A9D"), Colorway(3,"奶黄","DEC384","B69D63","7DAFC4","9A5A78"),
                Colorway(4,"雾蓝","8FAEC2","6D899B","E9C174","AF6675"), Colorway(5,"骨白","D5CDB5","AEA791","9A8CC4","9B5C70") };
        }
        static MonsterArtCatalog.Palette Colorway(int id,string name,string body,string pattern,string eye,string mouth) =>
            new MonsterArtCatalog.Palette { id = (MonsterPalette)id, name = name, body = Hex(body), pattern = Hex(pattern), eye = Hex(eye), mouth = Hex(mouth) };
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#"+hex,out var color); return color; }
    }
}
