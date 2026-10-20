using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace TheCall.Editor
{
    /// <summary>Derived semantic fill masks. Source images and their highlights remain unchanged.</summary>
    static class MonsterTintMasks
    {
        static readonly HashSet<string> Masked = new HashSet<string> { "713", "768", "854", "857", "861", "863", "867", "872", "909", "912", "745", "752", "817", "799", "635", "704" };
        public static Sprite Import(string id, Texture2D source)
        {
            if (!Masked.Contains(id)) return null;
            const string root = "Assets/Resources/MonsterTintMasks";
            if (!AssetDatabase.IsValidFolder(root)) AssetDatabase.CreateFolder("Assets/Resources", "MonsterTintMasks");
            var w = source.width; var h = source.height; var pixels = source.GetPixels32();
            var fill = new bool[pixels.Length];
            if (id == "867" || id == "872")
            {
                // A colored inner rim around the black pupil, excluding white specular highlights.
                for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
                {
                    var i = y * w + x; var p = pixels[i];
                    if (p.a < 128 || p.r > 100) continue;
                    var nearOuter = false; var touchesOuter = false;
                    for (var dy = -4; dy <= 4; dy++) for (var dx = -4; dx <= 4; dx++)
                    {
                        var xx = x + dx; var yy = y + dy;
                        if (xx < 0 || xx >= w || yy < 0 || yy >= h || pixels[yy * w + xx].a < 128)
                        { nearOuter = true; if (dx * dx + dy * dy <= 2) touchesOuter = true; }
                    }
                    fill[i] = nearOuter && !touchesOuter;
                }
            }
            else
            {
                // Large white regions are flesh/sclera; small enclosed ones are nails/highlights.
                var visited = new bool[pixels.Length]; var components = new List<List<int>>(); var largest = 0;
                for (var start = 0; start < pixels.Length; start++)
                {
                    if (visited[start] || !White(pixels[start])) continue;
                    var component = new List<int>(); var queue = new Queue<int>(); queue.Enqueue(start); visited[start] = true;
                    while (queue.Count > 0)
                    {
                        var i = queue.Dequeue(); component.Add(i); var x = i % w; var y = i / w;
                        void Visit(int next) { if (!visited[next] && White(pixels[next])) { visited[next] = true; queue.Enqueue(next); } }
                        if (x > 0) Visit(i - 1); if (x < w - 1) Visit(i + 1); if (y > 0) Visit(i - w); if (y < h - 1) Visit(i + w);
                    }
                    largest = Math.Max(largest, component.Count); components.Add(component);
                }
                var eyes = int.Parse(id) < 800 && id != "745" && id != "752" && id != "799" && id != "635" || id == "854" || id == "857" || id == "861" || id == "863" || id == "909" || id == "912";
                foreach (var component in components)
                    if (component.Count >= Math.Max(8, largest * (eyes ? .18f : .60f))) foreach (var i in component) fill[i] = true;
            }
            var output = new Color32[pixels.Length];
            for (var i = 0; i < fill.Length; i++) output[i] = new Color32(255, 255, 255, fill[i] ? pixels[i].a : (byte)0);
            var path = root + "/Fill_" + id + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var fresh = texture == null;
            if (fresh) { texture = new Texture2D(w, h, TextureFormat.RGBA32, false); texture.name = "Fill_" + id; AssetDatabase.CreateAsset(texture, path); }
            else if (texture.width != w || texture.height != h) texture.Reinitialize(w, h);
            texture.SetPixels32(output); texture.Apply(false, false); texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Array.Find(AssetDatabase.LoadAllAssetsAtPath(path), a => a is Sprite) as Sprite;
            if (sprite == null)
            { sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect); sprite.name = "Fill_" + id; AssetDatabase.AddObjectToAsset(sprite, texture); }
            EditorUtility.SetDirty(texture); return sprite;
        }
        static bool White(Color32 p) => p.a > 32 && p.r > 175 && p.g > 175 && p.b > 175;
    }
}
