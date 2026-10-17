using UnityEngine;

namespace TheCall
{
    [CreateAssetMenu(menuName = "TheCall/Shadow monster catalog")]
    public sealed class ShadowMonsterCatalog : ScriptableObject
    {
        public Sprite[] silhouettes;
        static ShadowMonsterCatalog _runtime;
        public static ShadowMonsterCatalog Runtime => _runtime != null ? _runtime :
            _runtime = Resources.Load<ShadowMonsterCatalog>("ShadowMonsterCatalog");

        public Sprite Pick(int seed) => silhouettes == null || silhouettes.Length == 0 ? null :
            silhouettes[(int)((uint)seed % (uint)silhouettes.Length)];
    }
}
