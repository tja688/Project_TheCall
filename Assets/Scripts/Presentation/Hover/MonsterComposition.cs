using System;
using System.Collections.Generic;
using UnityEngine;
namespace TheCall
{
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
            public readonly bool Mirror, Mask;
            public readonly int Group;
            public readonly Motion Role;
            public Sprite Sprite => Mask ? Art.fillMask : Art.sprite;
            public Piece(string name, MonsterArtCatalog.Part art, MonsterArtCatalog.Layer layer, Color color, bool mask)
            {
                Name = name; Art = art; Position = layer.position; Angle = layer.angle; Mirror = layer.mirror;
                Pivot = new Vector2(.5f, .5f); Group = layer.group; Color = color; Mask = mask;
                Role = layer.expression ? Motion.Eye : Motion.Body;
                var ratio = art.ink.width / art.ink.height;
                Size = layer.stretch ? layer.size : new Vector2(Mathf.Min(layer.size.x, layer.size.y * ratio), Mathf.Min(layer.size.y, layer.size.x / ratio));
            }
        }
        public static void Compose(MonsterArtCatalog catalog, MonsterVisualTraits traits, List<Piece> pieces)
        {
            pieces.Clear(); var appearance = traits.Appearance; var body = catalog.Body(appearance.Recipe); var palette = catalog.Colorway(appearance.Palette);
            void Paint(MonsterArtCatalog.Layer layer)
            {
                var art = catalog.Find(layer.expression ? EyeId(traits.EyeFamily, appearance.Variation) : layer.part);
                var color = palette.ColorFor(layer.color);
                var masked = art.fillMask != null && layer.color != MonsterArtCatalog.ColorRole.Original && layer.color != MonsterArtCatalog.ColorRole.Ink && layer.color != MonsterArtCatalog.ColorRole.Bone;
                pieces.Add(new Piece(layer.name, art, layer, masked ? Color.white : color, false));
                if (masked) pieces.Add(new Piece(layer.name + "Tint", art, layer, color, true));
            }
            if (appearance.BackAccessory != 0)
                foreach (var layer in Array.Find(body.back, a => a.id == appearance.BackAccessory).layers) Paint(layer);
            foreach (var layer in body.layers)
            {
                if (layer.name == "OralCavity")
                {
                    var edge = new MonsterArtCatalog.Layer { name="OralOutline",part=layer.part,position=layer.position,size=layer.size+Vector2.one*2,
                        color=MonsterArtCatalog.ColorRole.Ink,angle=layer.angle,mirror=layer.mirror,group=layer.group,stretch=layer.stretch };
                    Paint(edge);
                }
                Paint(layer);
            }
            if (appearance.HeadAccessory != 0)
                foreach (var layer in Array.Find(body.head, a => a.id == appearance.HeadAccessory).layers) Paint(layer);
        }
        static string EyeId(MonsterEyeFamily family, int variation)
        {
            switch (family)
            {
                case MonsterEyeFamily.Anxious: return variation % 2 == 0 ? "768" : "857";
                case MonsterEyeFamily.Indifferent: return variation % 2 == 0 ? "861" : "863";
                case MonsterEyeFamily.Manic: return variation % 2 == 0 ? "909" : "912";
                case MonsterEyeFamily.Affectionate: return variation % 2 == 0 ? "867" : "872";
                default: return "713";
            }
        }
    }
}
