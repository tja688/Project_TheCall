using System.IO;
using UnityEngine;

namespace TheCall
{
    static class TheCallBookHost
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot()
        {
            TheCallApp.Reset();
            var path = Path.Combine(Application.streamingAssetsPath, "call-book.json");
            if (!File.Exists(path))
                return;

            ContentGate.Use(ContentBook.Parse(File.ReadAllText(path)));
        }
    }
}
