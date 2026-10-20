using System.Collections.Generic;
using UnityEngine;

namespace TheCall
{
    /// <summary>Resolves genes and acquired traits into compatible, bounded attachment slots.</summary>
    internal static class MonsterComposition
    {
        internal enum Motion { Body, Head, Eye, Mouth, LeftArm, RightArm, LeftFoot, RightFoot, Ornament }
        internal readonly struct Piece
        {
            public readonly string Name;
            public readonly MonsterArtCatalog.Part Art;
            public readonly Vector2 Position, Size, Pivot;
            public readonly Color Color;
            public readonly float Angle;
            public readonly bool Mirror;
            public readonly Motion Role;
            public Piece(string name, MonsterArtCatalog.Part art, Vector2 pos, Vector2 size, Color color, Motion role,
                float angle, bool mirror, Vector2 pivot)
            { Name = name; Art = art; Position = pos; Size = size; Color = color; Role = role; Angle = angle; Mirror = mirror; Pivot = pivot; }
        }

        public static void Compose(MonsterArtCatalog catalog, MonsterVisualTraits traits, List<Piece> pieces)
        {
            pieces.Clear();
            var genes = traits.Appearance;
            var body = catalog.bodies[genes.Recipe];
            var colors = catalog.palettes[genes.Palette];
            bool Has(MonsterMutation flag) => (traits.Mutations & flag) != 0;
            void Add(string name, string id, Vector2 at, Vector2 box, Color color, Motion motion = Motion.Body,
                float angle = 0, bool flip = false, Vector2? pivot = null, bool stretch = false)
            {
                var art = catalog.Find(id);
                if (art == null || art.sprite == null) return;
                var ratio = art.ink.width / art.ink.height;
                var fit = new Vector2(Mathf.Min(box.x, box.y * ratio), Mathf.Min(box.y, box.x / ratio));
                if (stretch) fit = box;
                pieces.Add(new Piece(name, art, at, fit, color, motion, angle, flip, pivot ?? new Vector2(.5f, .5f)));
            }
            var rear = Color.Lerp(colors.limbs, new Color(.16f,.18f,.24f,1), .26f);
            var pale = new Color(.94f,.89f,.77f,1);
            var dark = new Color(.23f,.25f,.33f,1);
            var arm = new[] { "729", "754", "801", "799" }[genes.Arms];
            var foot = new[] { "810", "468", "807", "817" }[genes.Legs];
            if (Has(MonsterMutation.Tentacles)) { arm = "799"; foot = "833"; }
            if (Has(MonsterMutation.DarkLimbs)) { arm = "806"; foot = "812"; }
            var handColor = Has(MonsterMutation.DarkLimbs) ? Color.white : colors.limbs;
            var footColor = Has(MonsterMutation.DarkLimbs) ? Color.white : colors.limbs;
            var extraHead = Has(MonsterMutation.ExtraHead) && !body.twin;
            var budHead = Has(MonsterMutation.ExtraHead) && body.twin;
            var budHeadAt = new Vector2(0, 119);
            var extraHeadAt = body.integratedHead ? new Vector2(-80, 226) :
                new Vector2(body.face.x < -15 ? 50 : -62, 185);

            // Back appendages, then oral cavity, then body: the supplied PSB uses this same occlusion.
            if (Has(MonsterMutation.Wings))
            {
                Add("WingL", "552", body.leftShoulder + new Vector2(-14,20), new Vector2(80, 65), rear, Motion.Ornament, 22, true);
                Add("WingR", "552", body.rightShoulder + new Vector2(14,20), new Vector2(80, 65), rear, Motion.Ornament, -22);
            }
            // Bury the extra neck in the body; its head is painted later, before the main face.
            if (extraHead)
                Add("ExtraNeck", "942", extraHeadAt + new Vector2(0, -29), new Vector2(28, 57), colors.body, Motion.Head, -12);
            if (budHead)
                Add("BudNeck", "942", budHeadAt + new Vector2(0, -16), new Vector2(20, 34), colors.body, Motion.Head);
            var rearArm = Has(MonsterMutation.LargeHand) && Has(MonsterMutation.SmallHand) ? "752" : arm;
            Add("RearArm", rearArm, body.rightShoulder + new Vector2(1, -2), new Vector2(48, 78), rear,
                Motion.RightArm, -24, true, new Vector2(.5f,.88f));
            Add("RearFoot", foot, body.rightHip, new Vector2(39, 49), rear, Motion.RightFoot, -10, true, new Vector2(.5f,.82f));

            var open = body.integratedHead && (genes.Mouth == 0 || genes.Mouth == 1 || Has(MonsterMutation.LongTongue) || Has(MonsterMutation.ForkTongue)) && !Has(MonsterMutation.StitchedMouth);
            if (open)
            {
                var oral = body.oral;
                Add("OralCavity", "484", oral.center, oral.size, new Color(.22f,.35f,.42f,1), stretch:true);
                Add("InnerTongue", "506", oral.center + new Vector2(2,-oral.height*.23f), oral.size * .76f, new Color(.90f,.50f,.57f,1));
                Add("UpperTeeth", genes.Mouth == 1 ? "719" : "718", oral.center + new Vector2(0, oral.height*.27f), new Vector2(oral.width*.93f,oral.height*.46f), pale);
            }
            foreach (var piece in body.pieces)
            {
                var id = piece.part == "head" ? new[] { "936", "951", "990" }[genes.Head] : piece.part;
                if (open && id == "508") id = genes.Mouth == 1 ? "507" : "464";
                if (open && id == "520") id = genes.Mouth == 1 ? "517" : "516";
                Add("Body_" + pieces.Count, id, piece.position, piece.size, colors.body,
                    piece.part == "head" ? Motion.Head : Motion.Body, piece.angle, piece.mirror);
            }

            // Every acquired mark has a readable slot. Inherited marks never evict acquired ones.
            var belly = new Vector2(body.pieces[0].position.x, 91);
            var marks = new List<int>(3);
            if (Has(MonsterMutation.BodyStitches)) marks.Add(1);
            if (Has(MonsterMutation.Stars)) marks.Add(3);
            if (Has(MonsterMutation.Spots)) marks.Add(2);
            void Pattern(int kind, Vector2 position, float scale, string suffix)
            {
                if (kind == 1)
                    Add("BodyStitch" + suffix, "674", position + new Vector2(2, 4) * scale,
                        new Vector2(38, 38) * scale, pale, Motion.Body, -15);
                else if (kind == 2)
                    Add("BodySpots" + suffix, "709", position, new Vector2(40, 28) * scale, colors.accent);
                else if (kind == 3)
                    Add("BodyStar" + suffix, "710", position, new Vector2(24, 24) * scale, colors.accent, Motion.Ornament, 12);
            }
            void PatternSlot(int kind, int slot)
            {
                if (slot == 0) Pattern(kind, belly, 1, "");
                else if (slot == 1) Pattern(kind, belly + new Vector2(29, -17), .55f, "Secondary");
                else Pattern(kind, belly + new Vector2(-24, -14), .5f, "Tertiary");
            }
            for (var mark = 0; mark < marks.Count; mark++) PatternSlot(marks[mark], mark);
            if (genes.Pattern != 0)
            {
                if (marks.Contains(genes.Pattern))
                    Pattern(genes.Pattern, belly + new Vector2(6, -25), .25f, "Inherited");
                else PatternSlot(genes.Pattern, marks.Count);
            }

            Add("FrontFoot", foot, body.leftHip, new Vector2(39,51), footColor, Motion.LeftFoot, 8, false, new Vector2(.5f,.82f));
            var frontArm = Has(MonsterMutation.LargeHand) ? "745" : Has(MonsterMutation.SmallHand) ? "752" : arm;
            Add("FrontArm", frontArm, body.leftShoulder, new Vector2(Has(MonsterMutation.LargeHand) ? 65 : 48,82),
                handColor, Motion.LeftArm, 16, false, new Vector2(.62f,.9f));
            if (Has(MonsterMutation.DarkLimbs) && Has(MonsterMutation.Tentacles))
                Add("GraftedTentacle", "799", body.leftShoulder + new Vector2(8, -8), new Vector2(30, 52),
                    colors.limbs, Motion.LeftArm, -12, false, new Vector2(.5f, .9f));
            if (Has(MonsterMutation.ExtraArms))
            {
                Add("ExtraArmL", "754", body.leftShoulder + new Vector2(-4,-24), new Vector2(30,54), colors.limbs,
                    Motion.LeftArm, -32, false, new Vector2(.5f,.9f));
                Add("ExtraArmR", "754", body.rightShoulder + new Vector2(4,-24), new Vector2(30,54), rear,
                    Motion.RightArm, 32, true, new Vector2(.5f,.9f));
            }
            if (Has(MonsterMutation.Spikes))
            {
                Add("BackSpineL", "730", body.leftShoulder + new Vector2(-5,14), new Vector2(30,20), pale, Motion.Ornament, 40);
                Add("BackSpineR", "730", body.rightShoulder + new Vector2(4,12), new Vector2(30,20), pale, Motion.Ornament, -45, true);
            }
            if (extraHead)
                Add("ExtraHead", "936", extraHeadAt, new Vector2(67, 53), colors.body, Motion.Head);
            if (budHead)
                Add("BudHead", "936", budHeadAt, new Vector2(43, 34), colors.body, Motion.Head);

            string EyeId()
            {
                var variant = genes.Variation % 2;
                switch (traits.EyeFamily)
                {
                    case MonsterEyeFamily.Anxious: return variant == 0 ? "768" : "857";
                    case MonsterEyeFamily.Indifferent: return variant == 0 ? "861" : "863";
                    case MonsterEyeFamily.Manic: return variant == 0 ? "909" : "912";
                    case MonsterEyeFamily.Affectionate: return variant == 0 ? "867" : "872";
                    default: return "713";
                }
            }
            void Face(Vector2 center, float width, Vector2 mouth, float mouthWidth, bool oralOpen, string suffix)
            {
                var eye = EyeId();
                var eyeW = width * .38f;
                var spread = width * .27f;
                Add("EyeL"+suffix, Has(MonsterMutation.ButtonEye) ? "704" : eye,
                    center + new Vector2(-spread,0), new Vector2(eyeW, eyeW*1.15f), Color.white, Motion.Eye,
                    Has(MonsterMutation.AsymmetricEyes) ? 8 : 0);
                Add("EyeR"+suffix, eye, center + new Vector2(spread, Has(MonsterMutation.AsymmetricEyes) ? 3 : 0),
                    new Vector2(eyeW,eyeW*1.15f), Color.white, Motion.Eye, 0, true);
                if (!oralOpen)
                {
                    var mouthId = Has(MonsterMutation.StitchedMouth) ? "538" :
                        traits.MoodTier <= 1 ? "837" : traits.MoodTier >= 3 ? "844" :
                        new[] { "835", "848", "538", "835", "844", "848" }[genes.Mouth];
                    Add("Mouth"+suffix, mouthId, mouth, new Vector2(mouthWidth, 30), Color.white, Motion.Mouth);
                }
                if (Has(MonsterMutation.Blush) || traits.MoodTier == 4)
                {
                    Add("BlushL"+suffix,"712",center+new Vector2(-width*.43f,-9),new Vector2(17,12),new Color(.95f,.51f,.55f,.65f),Motion.Head);
                    Add("BlushR"+suffix,"712",center+new Vector2(width*.43f,-9),new Vector2(17,12),new Color(.95f,.51f,.55f,.65f),Motion.Head);
                }
            }
            if (extraHead)
                Face(extraHeadAt + new Vector2(0, 6), 40, extraHeadAt + new Vector2(0, -13), 33, false, "Extra");
            if (budHead)
            {
                Add("BudEyeL", Has(MonsterMutation.ButtonEye) ? "704" : EyeId(), budHeadAt + new Vector2(-6, 4),
                    new Vector2(9, 12), Color.white, Motion.Eye);
                Add("BudEyeR", EyeId(), budHeadAt + new Vector2(6, 4), new Vector2(9, 12), Color.white, Motion.Eye, 0, true);
            }
            var facialFit = !body.integratedHead && genes.Head == 2 ? .84f : 1f;
            Face(body.face,body.faceWidth*facialFit,body.mouth,body.mouthWidth*facialFit,open,"");
            if (body.twin)
                Face(body.secondFace,body.faceWidth*facialFit,new Vector2(body.secondFace.x,body.mouth.y),body.mouthWidth*facialFit,false,"Twin");
            // A star head has a central valley, so its crown joint is below the bounding-box top.
            var crown = body.crown - new Vector2(0,!body.integratedHead && genes.Head == 2 ? 14 : 3);
            if (Has(MonsterMutation.ExposedBrain))
                Add("CrownBrain","634",crown + new Vector2(0,6),new Vector2(57,29),new Color(.94f,.65f,.69f,1),Motion.Head);
            else if (genes.Crest != 0)
                Add("CrownHorn",new[]{"406","406","639","649"}[genes.Crest],crown + new Vector2(2,-2),
                    new Vector2(27,26),pale,Motion.Head,-12,false,new Vector2(.5f,.15f));
            if (Has(MonsterMutation.LongTongue) || Has(MonsterMutation.ForkTongue))
            {
                if (Has(MonsterMutation.StitchedMouth))
                {
                    var sideMouth = body.mouth + new Vector2(0, -20);
                    Add("SideMouth", "848", sideMouth, new Vector2(26, 12), Color.white, Motion.Mouth);
                    Add("GraftedTongue", Has(MonsterMutation.ForkTongue) ? "482" : "407", sideMouth + new Vector2(10, -7),
                        new Vector2(30, 20), new Color(.95f, .57f, .62f, 1), Motion.Mouth, -10);
                }
                else
                    Add("OuterTongue",Has(MonsterMutation.ForkTongue) ? "482" : "407",body.mouth + new Vector2(15,-9),
                        new Vector2(42,25),new Color(.95f,.57f,.62f,1),Motion.Mouth,-10);
            }
            if (Has(MonsterMutation.HollowEyes))
                Add("EyePatch","705",body.face + new Vector2(body.faceWidth*.53f,-11),new Vector2(17,23),Color.white,Motion.Eye,-15);
            _ = dark;
        }
    }
}
