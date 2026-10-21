using System;
namespace TheCall
{
    /// <summary>Presentation snapshot. Gameplay state never changes the drawing or its color.</summary>
    public readonly struct MonsterVisualTraits
    {
        MonsterVisualTraits(MonsterAppearance appearance)
        {
            Appearance = appearance;
            Signature = "monster-sprite-v3/" + appearance.Recipe + "/" + appearance.Palette;
        }
        public MonsterAppearance Appearance { get; }
        public string Signature { get; }
        public static MonsterVisualTraits From(MonsterView monster)
        {
            if (monster == null) throw new ArgumentNullException(nameof(monster));
            return From(monster.Appearance);
        }
        public static MonsterVisualTraits From(MonsterAppearance appearance) => new MonsterVisualTraits(appearance);
    }
}
