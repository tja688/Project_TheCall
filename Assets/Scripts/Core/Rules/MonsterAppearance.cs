using System;

namespace TheCall
{
    /// <summary>
    /// Stable, engine-independent appearance selection.
    /// Recipe/Palette remain as compatibility projections for existing rules and tests.
    /// Empty part IDs mean "use the selected template's authored slot".
    /// </summary>
    public readonly struct MonsterAppearance
    {
        public const int RecipeCount = 6;
        public const int PaletteCount = 6;

        static readonly string[] TemplateIds =
        {
            "template_01",
            "template_02",
            "template_03",
            "template_04",
            "template_05",
            "template_06",
        };

        static readonly string[] PaletteIds =
        {
            "palette_01",
            "palette_02",
            "palette_03",
            "palette_04",
            "palette_05",
            "palette_06",
        };

        public MonsterAppearance(int recipe, int palette)
            : this(
                TemplateIdForRecipe(recipe),
                PositiveModulo(palette, PaletteCount),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null)
        {
        }

        public MonsterAppearance(
            string templateId,
            int palette,
            string bodyId,
            string headId,
            string eyeId,
            string mouthId,
            string handId,
            string footId,
            string tailId,
            string hatId,
            string accessoryId)
        {
            TemplateId = string.IsNullOrEmpty(templateId) ? TemplateIds[0] : templateId;
            Palette = PositiveModulo(palette, PaletteCount);
            PaletteId = PaletteIds[Palette];
            BodyId = bodyId ?? string.Empty;
            HeadId = headId ?? string.Empty;
            EyeId = eyeId ?? string.Empty;
            MouthId = mouthId ?? string.Empty;
            HandId = handId ?? string.Empty;
            FootId = footId ?? string.Empty;
            TailId = tailId ?? string.Empty;
            HatId = hatId ?? string.Empty;
            AccessoryId = accessoryId ?? string.Empty;
            Recipe = RecipeForTemplate(TemplateId);
        }

        public int Recipe { get; }

        public int Palette { get; }

        public string TemplateId { get; }

        public string PaletteId { get; }

        public string BodyId { get; }

        public string HeadId { get; }

        public string EyeId { get; }

        public string MouthId { get; }

        public string HandId { get; }

        public string FootId { get; }

        public string TailId { get; }

        public string HatId { get; }

        public string AccessoryId { get; }

        public string PartId(MonsterPartKind kind)
        {
            switch (kind)
            {
                case MonsterPartKind.Body: return BodyId;
                case MonsterPartKind.Head: return HeadId;
                case MonsterPartKind.Eye: return EyeId;
                case MonsterPartKind.Mouth: return MouthId;
                case MonsterPartKind.Hand: return HandId;
                case MonsterPartKind.Foot: return FootId;
                case MonsterPartKind.Tail: return TailId;
                case MonsterPartKind.Hat: return HatId;
                case MonsterPartKind.Accessory: return AccessoryId;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public static MonsterAppearance FromSeed(int seed)
        {
            var mixed = seed * 397 + 173;
            var recipe = PositiveModulo(seed, RecipeCount);
            var tailIndex = Choice(seed, 0x24F1, 3);
            var hatIndex = Choice(seed, 0x72A9, 4);
            var accessoryIndex = Choice(seed, 0x391D, 3);
            return new MonsterAppearance(
                TemplateIdForRecipe(recipe),
                PositiveModulo(mixed, PaletteCount),
                "body_" + (recipe + 1).ToString("00"),
                "head_" + (Choice(seed, 0x17A3, 4) + 1).ToString("00"),
                "eye_" + (Choice(seed, 0x58CD, 5) + 1).ToString("00"),
                "mouth_" + (Choice(seed, 0x0D67, 6) + 1).ToString("00"),
                "hand_" + (Choice(seed, 0x6B21, 5) + 1).ToString("00"),
                "foot_" + (Choice(seed, 0x43B9, 3) + 1).ToString("00"),
                Choice(seed, 0x2E15, 4) == 0 ? "tail_" + (tailIndex + 1).ToString("00") : string.Empty,
                Choice(seed, 0x1C87, 4) == 0 ? "hat_" + (hatIndex + 1).ToString("00") : string.Empty,
                Choice(seed, 0x7D31, 3) == 0 ? "accessory_" + (accessoryIndex + 1).ToString("00") : string.Empty);
        }

        public static MonsterAppearance Breed(MonsterAppearance first, MonsterAppearance second)
        {
            return new MonsterAppearance(
                first.TemplateId,
                second.Palette,
                first.BodyId,
                first.HeadId,
                first.EyeId,
                first.MouthId,
                first.HandId,
                first.FootId,
                first.TailId,
                first.HatId,
                first.AccessoryId);
        }

        public static string TemplateIdForRecipe(int recipe) =>
            TemplateIds[PositiveModulo(recipe, RecipeCount)];

        static int RecipeForTemplate(string templateId)
        {
            for (var i = 0; i < TemplateIds.Length; i++)
            {
                if (string.Equals(TemplateIds[i], templateId, StringComparison.Ordinal))
                    return i;
            }

            return 0;
        }

        static int PositiveModulo(int value, int modulus)
        {
            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        static int Choice(int seed, int salt, int count)
        {
            unchecked
            {
                var value = seed ^ salt;
                value ^= value >> 16;
                value *= (int)0x7feb352d;
                value ^= value >> 15;
                value *= (int)0x846ca68b;
                value ^= value >> 16;
                return PositiveModulo(value, count);
            }
        }
    }
}
