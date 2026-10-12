/****************************************************************************
 * Copyright (c) 2015 - 2025 liangxiegame UNDER MIT LICENSE
 *
 * https://qframework.cn
 * https://github.com/liangxiegame/QFramework
 * https://gitee.com/liangxiegame/QFramework
 * AudioKit v1.0: use QFramework.cs architecture
 ****************************************************************************/

namespace QFramework
{
    internal static class StopMusicCommand
    {
        internal static void Execute()
        {
            // Otherwise AudioManager's music-on observer can resurrect a previously stopped
            // clip when users toggle Music after changing screens.
            var manager = AudioManager.Instance;
            manager.CurrentMusicName = null;
            manager.MusicPlayer.Stop();
        }
    }
}