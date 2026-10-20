using UnityEngine;

namespace TheCall
{
    /// <summary>
    /// UI one-shots from <see cref="GameAudioCatalog"/>.
    /// Generic clicks rotate S_UI_通用UI01–03. Purchase, error, and clock-out replace that click.
    /// </summary>
    public static class UiSfx
    {
        const string CatalogKey = "GameAudioCatalog";
        static GameAudioCatalog _catalog;
        static int _click;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _catalog = null;
            _click = 0;
        }

        public static void PlayClick() => Play(NextClick());

        public static void PlayPurchase() => Play(Catalog()?.purchase);

        public static void PlayError() => Play(Catalog()?.error);

        public static void PlayClockOut() => Play(Catalog()?.clockOut);

        static AudioClip NextClick()
        {
            var clips = Catalog()?.uiClicks;
            if (clips == null || clips.Length == 0)
                return null;

            for (var attempt = 0; attempt < clips.Length; attempt++)
            {
                var clip = clips[_click % clips.Length];
                _click++;
                if (clip != null)
                    return clip;
            }

            return null;
        }

        static void Play(AudioClip clip)
        {
            if (!Application.isPlaying || clip == null)
                return;

            GameSfx.PlayOneShot(clip);
        }

        static GameAudioCatalog Catalog()
        {
            if (_catalog == null)
                _catalog = Resources.Load<GameAudioCatalog>(CatalogKey);
            return _catalog;
        }
    }
}
