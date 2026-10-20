using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    /// <summary>只为现有分层添加演出配置，可重复运行；不重建设施或更改玩法引用。</summary>
    public static class ScreenTransitionIntegration
    {
        [MenuItem("TheCall/Presentation/配置分部转场")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring transitions.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity") throw new InvalidOperationException("Open Main first.");
            var opening = Find<OpeningScreenView>();
            var level = Find<LevelStartScreenView>();
            var op = Find<OperationScreenView>();
            var research = Find<ResearchScreenView>();
            var shop = Find<ShopScreenView>();
            var result = Find<ResultScreenView>();
            Simple(opening, "BackgroundImage", "Title", "Subtitle", "Candidate_0", "Candidate_1", "Candidate_2", "SettingsButton");
            Simple(level, "BackgroundImage", "PanelImage", "SettingsButton");
            Simple(result, "BackgroundImage", "PanelImage", "SettingsButton");
            Operation(op);
            Research(research);
            Shop(shop);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static T Find<T>() where T : Component => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        static T Get<T>(GameObject node) where T : Component
        {
            var component = node.GetComponent<T>();
            return component != null ? component : node.AddComponent<T>();
        }
        static RectTransform At(Component screen, string path) => screen.transform.Find(path) as RectTransform;
        static ScreenAssembly.Part Part(RectTransform target, Vector2 offset, float delay, float seconds = 0.42f, float scale = 1) =>
            new ScreenAssembly.Part { target = target, offset = offset, delay = delay, seconds = seconds, scale = scale };
        static void Save(Component screen, List<ScreenAssembly.Part> parts, params AssemblySurface[] surfaces)
        {
            var assembly = Get<ScreenAssembly>(screen.gameObject);
            assembly.parts = parts.Where(p => p.target != null).ToArray();
            assembly.surfaces = surfaces.Where(s => s != null).ToArray();
            assembly.exitSeconds = screen is ResearchScreenView ? 0.20f : 0.22f;
            Get<CanvasGroup>(screen.gameObject);
            foreach (var part in assembly.parts) Get<CanvasGroup>(part.target.gameObject);
            EditorUtility.SetDirty(assembly);
        }

        static void Simple(Component screen, params string[] paths)
        {
            // 主界面换图：保留 ScreenView/玩法槽位，只将新美术分层加到这里或 Inspector.parts。
            var parts = new List<ScreenAssembly.Part>();
            for (var i = 0; i < paths.Length; i++)
                parts.Add(Part(At(screen, paths[i]), i == 0 ? Vector2.zero : new Vector2(0, -32), i * 0.045f));
            Save(screen, parts);
        }

        static AssemblySurface Surface(RectTransform target, bool shop)
        {
            if (target == null) return null;
            var surface = Get<AssemblySurface>(target.gameObject);
            surface.seconds = 0.58f;
            surface.regions = shop ? new[] {
                Tile(0,0,1920,108,0,28,0), Tile(0,108,472,864,-80,0,0.035f),
                Tile(472,108,819,316,0,66,0.075f), Tile(472,424,819,257,0,-52,0.115f),
                Tile(472,681,819,291,0,-72,0.065f), Tile(1291,108,629,540,80,0,0.095f),
                Tile(1291,648,629,324,65,-35,0.135f), Tile(0,972,1920,108,0,-30,0)
            } : new[] {
                Tile(0,0,1920,108,0,26,0), Tile(0,108,724,430,-64,34,0.035f),
                Tile(724,108,724,430,0,60,0.08f), Tile(1448,108,472,430,74,25,0.125f),
                Tile(0,538,1144,434,-48,-55,0.095f), Tile(1144,538,304,434,0,-66,0.14f),
                Tile(1448,538,472,434,76,-28,0.17f), Tile(0,972,1920,108,0,-28,0)
            };
            EditorUtility.SetDirty(surface);
            return surface;
        }
        static AssemblySurface.Region Tile(float x,float y,float w,float h,float dx,float dy,float delay) =>
            new AssemblySurface.Region { area = new Rect(x,y,w,h), offset = new Vector2(dx,dy), delay = delay };

        static void Operation(OperationScreenView screen)
        {
            var parts = new List<ScreenAssembly.Part>();
            var content = At(screen, "FacilityViewport/FacilityContent");
            foreach (RectTransform child in content)
            {
                var name = child.name;
                if (name == "DormShell" || name == "ProductionShell" || child.GetComponent<FacilityLamp>() != null || name.StartsWith("FacilityEnergyMote")) continue;
                var x = child.anchoredPosition.x;
                var localX = x >= 1920 ? x - 1920 : x;
                var delay = 0.16f + Mathf.Clamp01(localX / 1920) * 0.20f;
                var ownsMotion = name == "Shutter" || name == "CollectorCore";
                var offset = ownsMotion ? Vector2.zero : new Vector2(localX > 1420 ? 46 : 0, -34);
                parts.Add(Part(child, offset, delay));
            }
            parts.Add(Part(At(screen, "FacilityHud"), new Vector2(0,-18), 0.34f, 0.36f));
            parts.Add(Part(At(screen, "OwnedTools"), new Vector2(0,-18), 0.35f));
            Save(screen, parts, Surface(content.Find("ProductionShell") as RectTransform, false),
                Surface(content.Find("DormShell") as RectTransform, false));
        }

        static void Research(ResearchScreenView screen)
        {
            // 科研换素材：节点保持 TechNodeView，不把文字/选中框单独做位移；改外层 target 即可。
            var parts = new List<ScreenAssembly.Part>();
            foreach (RectTransform child in screen.transform)
            {
                if (child.GetComponent<TechNodeView>() != null) continue;
                var background = child.name.Contains("Shell") || child.name.Contains("Shade");
                var detail = child.name == "详情";
                parts.Add(Part(child, background ? Vector2.zero : detail ? new Vector2(48,0) : new Vector2(0,22),
                    background ? 0 : detail ? 0.15f : 0.04f, background ? 0.28f : 0.36f));
            }
            for (var i = 0; i < screen.nodes.Length; i++)
                parts.Add(Part(screen.nodes[i].transform as RectTransform, new Vector2(-26,-22),
                    0.065f + i / 2 * 0.055f + i % 2 * 0.035f, 0.36f, 0.98f));
            Save(screen, parts);
        }

        static void Shop(ShopScreenView screen)
        {
            var parts = new List<ScreenAssembly.Part>();
            var stage = At(screen, "FacilityShopStage");
            foreach (RectTransform child in stage)
            {
                if (child.name == "ShopShell" || child.gameObject == screen.buyPage) continue;
                var x = child.anchoredPosition.x;
                var doctor = child.name == "DoctorPortrait";
                parts.Add(Part(child, doctor ? Vector2.zero : new Vector2(x < 470 ? -36 : x > 1290 ? 40 : 0, -22),
                    doctor ? 0.26f : 0.18f + Mathf.Clamp01(x / 1920) * 0.13f));
            }
            for (var i = 0; i < screen.cards.Length; i++)
                parts.Add(Part(screen.cards[i].transform as RectTransform, new Vector2(0,-32), 0.23f + i * 0.025f, 0.40f));
            parts.Add(Part(At(screen, "FacilityShopHud"), new Vector2(0,20), 0.33f, 0.36f));
            Save(screen, parts, Surface(stage.Find("ShopShell") as RectTransform, true));
        }
    }
}
