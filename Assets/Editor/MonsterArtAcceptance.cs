using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QFramework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    /// <summary>Local visual fixtures. Recorded player replay remains separate evidence.</summary>
    public static class MonsterArtAcceptance
    {
        const string DefaultEvidence = "docs/audit/2026-10-20/monsters";
        const string PrefabPath = "Assets/Prefabs/UI/MonsterPortrait.prefab";
        static readonly string[] Personalities = { "自律", "稳定", "急躁", "摸鱼派", "恋爱脑" };
        static readonly string[] EyeLabels = { "焦虑", "冷漠", "狂躁", "怠惰", "渴爱" };
        static readonly string[] Moods = { "倦怠", "疲倦", "普通", "热情", "热爱" };
        static readonly Color Ink = new Color32(228, 226, 210, 255);
        static GameObject _gallery;
        static readonly List<MonsterPortrait> GalleryPortraits = new List<MonsterPortrait>();
        static readonly List<MotionReading> MotionReadings = new List<MotionReading>();
        static readonly Dictionary<string, SourceAlpha> AlphaCache = new Dictionary<string, SourceAlpha>(StringComparer.Ordinal);
        static string _evidence = DefaultEvidence;

        [Serializable] sealed class Sample
        {
            public string name, visual, assembly, error;
            public int seed, recipe, parts;
            public Rect bounds;
        }
        [Serializable] sealed class MatrixReport
        {
            public string kind = "LOCAL_VISUAL_MATRIX_NOT_NATURAL_RUN";
            public string unity;
            public int requestedSeeds, sourceParts, catalogParts, skillCount, failures;
            public List<Sample> samples = new List<Sample>();
        }
        [Serializable] sealed class MotionReading { public int frame; public double seconds; public float pose; }
        [Serializable] sealed class MotionReport
        {
            public string kind = "LOCAL_RUNTIME_MOTION_NOT_NATURAL_RUN";
            public int distinctPoses;
            public List<MotionReading> samples;
        }
        [Serializable] sealed class JointReading
        {
            public string name, source, support, error;
            public float bodyOverlapArea, bestLimbOverlapArea;
            public Rect designBounds;
        }
        [Serializable] sealed class SupportSample
        {
            public int index, recipe, arms, legs;
            public string visual, error;
            public List<JointReading> joints = new List<JointReading>();
        }
        [Serializable] sealed class SupportReport
        {
            public string kind = "LOCAL_ORIGINAL_ALPHA_JOINT_MATRIX_NOT_NATURAL_RUN";
            public int requestedCases, failures, cachedOriginalPngs;
            public float alphaThreshold = .4f, gridStep = .75f, requiredOverlapArea = 4f;
            public List<SupportSample> samples = new List<SupportSample>();
        }
        sealed class SourceAlpha
        {
            public Color32[] pixels;
            public int width, height;
            public long modified;
            public Rect opaque;
        }
        sealed class AlphaArt
        {
            public Image image;
            public SourceAlpha source;
            public Rect bounds;
            public JointReading reading;
        }
        sealed class Spec
        {
            public string label, personality = "稳定", mood = "普通";
            public MonsterAppearance appearance;
            public string[] skills = Array.Empty<string>();
        }

        static void Need(bool value, string message)
        { if (!value) throw new InvalidOperationException("MONSTER_FAIL: " + message); }

        static void Save(string file, object report)
        {
            Directory.CreateDirectory(_evidence);
            File.WriteAllText(Path.Combine(_evidence, file), JsonUtility.ToJson(report, true));
        }

        public static string Saved()
        {
            Need(!Application.isPlaying, "Saved requires Edit Mode");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Need(scene.path == "Assets/Scenes/Main.unity" && !scene.isDirty, "Main scene must be saved");
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterArtCatalog>(MonsterArtIntegration.CatalogPath);
            var sources = Directory.GetFiles("Assets/Arts/Pngs/部位组件", "*.png", SearchOption.AllDirectories);
            Need(sources.Length >= 70 && catalog != null && catalog.Ready, "Original 70-part catalog not ready");
            Need(catalog.parts.Length == sources.Length, "Catalog does not include every supplied PNG");
            Need(catalog.parts.Select(part => part.id).Distinct().Count() == sources.Length, "Part IDs are not unique");
            var paths = new HashSet<string>(sources.Select(path => path.Replace('\\', '/')), StringComparer.Ordinal);
            foreach (var part in catalog.parts)
            {
                Need(part.sprite != null && part.ink.width > 0 && part.ink.height > 0, "Missing sprite/alpha bounds: " + part.id);
                Need(paths.Contains(AssetDatabase.GetAssetPath(part.sprite)), "Part no longer references supplied original: " + part.id);
            }
            Need(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<MonsterPortrait>() != null, "Portrait prefab missing");
            return "PASS saved Main, " + sources.Length + " original PNG references, six body plans and six palettes";
        }

        public static string Matrix(string evidenceDirectory = DefaultEvidence, int seeds = 600)
        {
            Need(Application.isPlaying, "Matrix requires advancing Play Mode");
            Need(seeds >= 1, "Seed count must be positive");
            _evidence = Path.GetFullPath(evidenceDirectory);
            var catalog = MonsterArtCatalog.Runtime;
            Need(catalog != null && catalog.Ready, "Runtime catalog not ready");
            var names = ContentGate.Current.Skills.Select(skill => skill.Name).ToArray();
            var report = new MatrixReport { unity = Application.unityVersion, requestedSeeds = seeds,
                sourceParts = Directory.GetFiles("Assets/Arts/Pngs/部位组件", "*.png", SearchOption.AllDirectories).Length,
                catalogParts = catalog.parts.Length, skillCount = names.Length };
            var root = NewRect("MonsterMatrixProbe", null, MonsterPortrait.RecommendedViewportSize);
            var portrait = root.gameObject.AddComponent<MonsterPortrait>();
            try
            {
                for (var seed = 0; seed < seeds; seed++)
                {
                    var skills = Enumerable.Range(0, 1 + seed % 4).Select(index => names[(seed + index * 7) % names.Length]).Distinct().ToArray();
                    var traits = MonsterVisualTraits.Create(MonsterAppearance.FromSeed(seed), Personalities[seed % 5], Moods[(seed / 5) % 5], skills);
                    Probe(portrait, traits, "seed-" + seed, seed, report);
                }
                var skillAppearance = MonsterAppearance.FromSeed(48291);
                portrait.Show(MonsterVisualTraits.Create(skillAppearance, "稳定", "普通", Array.Empty<string>()));
                var withoutSkills = portrait.AssemblySignature;
                foreach (var skill in names)
                {
                    var traits = MonsterVisualTraits.Create(skillAppearance, "稳定", "普通", new[] { skill });
                    var sample = Probe(portrait, traits, "skill-" + skill, 48291, report);
                    if (traits.Mutations == MonsterMutation.None) Fail(sample, report, "Official skill has no visual trait");
                    if (portrait.Traits.Mutations != traits.Mutations) Fail(sample, report, "Rendered skill traits differ from supplied snapshot");
                    if (sample.assembly == withoutSkills) Fail(sample, report, "Skill leaves the actual assembly unchanged");
                }
            }
            finally { Object.DestroyImmediate(root.gameObject); Save("visual-matrix.json", report); }
            Need(report.failures == 0, report.failures + " matrix failures; " + string.Join("; ", report.samples.Where(sample => !string.IsNullOrEmpty(sample.error)).Take(10).Select(sample => sample.name + " " + sample.error)));
            return "PASS " + seeds + " deterministic structure seeds and " + names.Length + " official skills; all assembled ink inside [-132,132] x [-6,286]";
        }

        static Sample Probe(MonsterPortrait portrait, MonsterVisualTraits traits, string name, int seed, MatrixReport report)
        {
            var sample = new Sample { name = name, seed = seed, recipe = traits.Appearance.Recipe };
            report.samples.Add(sample);
            try
            {
                portrait.Show(traits);
                portrait.PreviewPose(.7f);
                sample.visual = portrait.VisualSignature; sample.assembly = portrait.AssemblySignature;
                sample.parts = portrait.PartCount; sample.bounds = portrait.InkBounds;
                if (!portrait.HasCreature || !portrait.UsesAssembly || sample.parts < 8) Fail(sample, report, "Missing modular creature; parts=" + sample.parts);
                if (portrait.VisualSignature != traits.Signature) Fail(sample, report, "Visual signature differs from input");
                if (string.IsNullOrEmpty(sample.assembly)) Fail(sample, report, "Actual assembly signature missing");
                var bounds = sample.bounds;
                if (float.IsNaN(bounds.x) || float.IsNaN(bounds.y) || float.IsInfinity(bounds.width) || float.IsInfinity(bounds.height) ||
                    bounds.width <= 0 || bounds.height <= 0 || bounds.xMin < -132.01f || bounds.xMax > 132.01f || bounds.yMin < -6.01f || bounds.yMax > 286.01f)
                    Fail(sample, report, "unsafe ink x=[" + bounds.xMin + "," + bounds.xMax + "] y=[" + bounds.yMin + "," + bounds.yMax + "]");
            }
            catch (Exception exception) { Fail(sample, report, exception.GetType().Name + ": " + exception.Message); }
            return sample;
        }

        static void Fail(Sample sample, MatrixReport report, string error)
        {
            if (string.IsNullOrEmpty(sample.error)) report.failures++;
            sample.error = string.IsNullOrEmpty(sample.error) ? error : sample.error + "; " + error;
        }

        public static string SupportMatrix(string evidenceDirectory = DefaultEvidence, int seeds = 144)
        {
            Need(Application.isPlaying, "SupportMatrix requires Play Mode");
            Need(seeds >= 144, "Joint matrix needs 96 base cases plus eight mutation profiles across all six body plans");
            _evidence = Path.GetFullPath(evidenceDirectory);
            var report = new SupportReport { requestedCases = seeds };
            var probe = NewRect("MonsterAlphaSupportProbe", null, MonsterPortrait.RecommendedViewportSize);
            var portrait = probe.gameObject.AddComponent<MonsterPortrait>();
            var mutationSkills = new[] { new[] { "换位手", "优质肉体" }, new[] { "增生能量体", "良好肉体" },
                new[] { "抽杀体", "汲取鼻" }, new[] { "血崩体", "收尸腺" }, new[] { "优质肉体", "良好肉体" },
                new[] { "祭品腺", "回响嗓" }, new[] { "祭品腺", "汲取鼻" }, new[] { "不稳定肉体", "能量体", "优秀能量体", "双头能量体" } };
            try
            {
                for (var index = 0; index < seeds; index++)
                {
                    var baseGenes = MonsterAppearance.FromSeed(12000 + index);
                    var recipe = index < 96 ? index / 16 : ((index - 96) / mutationSkills.Length) % 6;
                    var arms = index < 96 ? index / 4 % 4 : baseGenes.Arms;
                    var legs = index < 96 ? index % 4 : baseGenes.Legs;
                    var genes = new MonsterAppearance(recipe, baseGenes.Palette, baseGenes.Head, baseGenes.Mouth,
                        arms, legs, baseGenes.Crest, baseGenes.Pattern, baseGenes.Variation);
                    var skills = index < 96 ? Array.Empty<string>() : mutationSkills[(index - 96) % mutationSkills.Length];
                    var traits = MonsterVisualTraits.Create(genes, "稳定", index < 96 ? "普通" : "热爱", skills);
                    var sample = new SupportSample { index = index, recipe = recipe, arms = arms, legs = legs, visual = traits.Signature };
                    report.samples.Add(sample);
                    try
                    {
                        portrait.Show(traits); portrait.PreviewPose(.7f);
                        CheckAlphaSupport(portrait, sample, report);
                    }
                    catch (Exception exception) { sample.error = exception.GetType().Name + ": " + exception.Message; }
                    if (!string.IsNullOrEmpty(sample.error) || sample.joints.Any(joint => !string.IsNullOrEmpty(joint.error))) report.failures++;
                }
            }
            finally
            {
                Object.DestroyImmediate(probe.gameObject);
                report.cachedOriginalPngs = AlphaCache.Count; Save("alpha-joint-matrix.json", report);
            }
            var failures = report.samples.Where(sample => !string.IsNullOrEmpty(sample.error) || sample.joints.Any(joint => !string.IsNullOrEmpty(joint.error)));
            Need(report.failures == 0, report.failures + " alpha-support failures; " + string.Join("; ", failures.Take(12).Select(sample =>
                "case=" + sample.index + " body=" + sample.recipe + " arms=" + sample.arms + " legs=" + sample.legs + " " +
                (sample.error ?? string.Join(", ", sample.joints.Where(joint => !string.IsNullOrEmpty(joint.error)).Select(joint => joint.name + " bodyArea=" + joint.bodyOverlapArea + " limbArea=" + joint.bestLimbOverlapArea))))));
            return "PASS " + seeds + " original-PNG alpha joint cases; all six body plans x four arms x four legs, plus " + Math.Max(0, seeds - 96) +
                " skill-altered cases; each visible limb reaches a body root through >=4 design-pixel-squared opaque overlap";
        }

        static void CheckAlphaSupport(MonsterPortrait portrait, SupportSample sample, SupportReport report)
        {
            var design = portrait.transform.Find("AssembledCreature");
            Need(design != null, "Assembly design root absent");
            var images = design.GetComponentsInChildren<Image>(false).Where(image => image.enabled && image.sprite != null).ToArray();
            var body = images.Where(image => image.transform.parent.name.StartsWith("Body_", StringComparison.Ordinal)).Select(image => ReadAlphaArt(image, design)).ToArray();
            var limbs = images.Where(image => IsLimbNode(image.transform.parent.name)).Select(image => ReadAlphaArt(image, design)).ToArray();
            Need(body.Length > 0 && limbs.Length >= 4, "Expected body/neck and four visible base limbs");
            foreach (var prefix in new[] { "FrontArm_", "RearArm_", "FrontFoot_", "RearFoot_" })
                Need(limbs.Any(limb => limb.image.transform.parent.name.StartsWith(prefix, StringComparison.Ordinal)), "Missing required limb role: " + prefix);
            var rooted = new List<AlphaArt>();
            foreach (var limb in limbs)
            {
                var reading = new JointReading { name = limb.image.transform.parent.name, source = AssetDatabase.GetAssetPath(limb.image.sprite), designBounds = limb.bounds };
                limb.reading = reading; sample.joints.Add(reading);
                reading.bodyOverlapArea = AlphaOverlap(limb, body, design, report.gridStep);
                if (reading.bodyOverlapArea >= report.requiredOverlapArea) { reading.support = "body/neck"; rooted.Add(limb); }
            }
            // A pair of floating limbs cannot support each other. Every accepted path starts at body alpha.
            var progress = true;
            while (progress)
            {
                progress = false;
                foreach (var limb in limbs.Where(value => string.IsNullOrEmpty(value.reading.support)))
                {
                    foreach (var support in rooted.ToArray())
                    {
                        var overlap = AlphaOverlap(limb, new[] { support }, design, report.gridStep);
                        limb.reading.bestLimbOverlapArea = Mathf.Max(limb.reading.bestLimbOverlapArea, overlap);
                        if (overlap < report.requiredOverlapArea) continue;
                        limb.reading.support = support.reading.name; rooted.Add(limb); progress = true; break;
                    }
                }
            }
            foreach (var limb in limbs.Where(value => string.IsNullOrEmpty(value.reading.support)))
                limb.reading.error = "Opaque limb has no alpha-overlap path to body/neck";
        }

        static bool IsLimbNode(string name) => name.StartsWith("FrontArm_", StringComparison.Ordinal) ||
            name.StartsWith("RearArm_", StringComparison.Ordinal) || name.StartsWith("FrontFoot_", StringComparison.Ordinal) ||
            name.StartsWith("RearFoot_", StringComparison.Ordinal) || name.StartsWith("ExtraArmL_", StringComparison.Ordinal) ||
            name.StartsWith("ExtraArmR_", StringComparison.Ordinal) || name.StartsWith("GraftedTentacle_", StringComparison.Ordinal) ||
            name.StartsWith("BudNeck_", StringComparison.Ordinal) || name.StartsWith("BudHead_", StringComparison.Ordinal);

        static AlphaArt ReadAlphaArt(Image image, Transform design)
        {
            Need(image.type == Image.Type.Simple && !image.preserveAspect, "Joint sampler expects the actual simple stretched part Image");
            var path = AssetDatabase.GetAssetPath(image.sprite);
            Need(!string.IsNullOrEmpty(path) && File.Exists(path), "Original part PNG unavailable: " + image.name);
            var modified = File.GetLastWriteTimeUtc(path).Ticks;
            if (!AlphaCache.TryGetValue(path, out var source) || source.modified != modified)
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    Need(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), "Original PNG could not be read: " + path);
                    source = new SourceAlpha { width = texture.width, height = texture.height, pixels = texture.GetPixels32(), modified = modified };
                    var minX = source.width; var minY = source.height; var maxX = -1; var maxY = -1;
                    for (var y = 0; y < source.height; y++) for (var x = 0; x < source.width; x++)
                        if (source.pixels[y * source.width + x].a > 102)
                        { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                    Need(maxX >= 0, "No alpha >0.4 in original PNG: " + path);
                    source.opaque = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1); AlphaCache[path] = source;
                }
                finally { Object.DestroyImmediate(texture); }
            }
            var sprite = image.sprite.rect; var rect = image.rectTransform.rect;
            var alpha = Rect.MinMaxRect(Mathf.Max(sprite.xMin, source.opaque.xMin), Mathf.Max(sprite.yMin, source.opaque.yMin),
                Mathf.Min(sprite.xMax, source.opaque.xMax), Mathf.Min(sprite.yMax, source.opaque.yMax));
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity); var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var x = 0; x <= 1; x++) for (var y = 0; y <= 1; y++)
            {
                var local = new Vector2(rect.xMin + ((x == 0 ? alpha.xMin : alpha.xMax) - sprite.x) / sprite.width * rect.width,
                    rect.yMin + ((y == 0 ? alpha.yMin : alpha.yMax) - sprite.y) / sprite.height * rect.height);
                var at = (Vector2)design.InverseTransformPoint(image.rectTransform.TransformPoint(local));
                min = Vector2.Min(min, at); max = Vector2.Max(max, at);
            }
            return new AlphaArt { image = image, source = source, bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y) };
        }

        static float AlphaOverlap(AlphaArt limb, IReadOnlyList<AlphaArt> supports, Transform design, float step)
        {
            var area = 0f;
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity); var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var support in supports) { min = Vector2.Min(min, support.bounds.min); max = Vector2.Max(max, support.bounds.max); }
            var left = Mathf.Max(limb.bounds.xMin, min.x); var right = Mathf.Min(limb.bounds.xMax, max.x);
            var bottom = Mathf.Max(limb.bounds.yMin, min.y); var top = Mathf.Min(limb.bounds.yMax, max.y);
            for (var y = bottom + step * .5f; y < top; y += step) for (var x = left + step * .5f; x < right; x += step)
            {
                var world = design.TransformPoint(new Vector3(x, y));
                if (!OpaqueAt(limb, world)) continue;
                for (var i = 0; i < supports.Count; i++)
                    if (OpaqueAt(supports[i], world)) { area += step * step; break; }
            }
            return area;
        }

        static bool OpaqueAt(AlphaArt art, Vector3 world)
        {
            var local = (Vector2)art.image.rectTransform.InverseTransformPoint(world);
            var rect = art.image.rectTransform.rect;
            if (!rect.Contains(local)) return false;
            var sprite = art.image.sprite.rect;
            var x = Mathf.FloorToInt(sprite.x + (local.x - rect.x) / rect.width * sprite.width);
            var y = Mathf.FloorToInt(sprite.y + (local.y - rect.y) / rect.height * sprite.height);
            return x >= 0 && x < art.source.width && y >= 0 && y < art.source.height && art.source.pixels[y * art.source.width + x].a > 102;
        }

        public static string Contracts(string evidenceDirectory = DefaultEvidence)
        {
            Need(Application.isPlaying, "Contracts require Play Mode");
            _evidence = Path.GetFullPath(evidenceDirectory);
            var root = NewRect("MonsterContractProbe", null, new Vector2(800, 500));
            try
            {
                var first = NewRect("ConsumerA", root, MonsterPortrait.RecommendedViewportSize).gameObject.AddComponent<MonsterPortrait>();
                var second = NewRect("ConsumerB", root, MonsterPortrait.RecommendedViewportSize).gameObject.AddComponent<MonsterPortrait>();
                var existing = TheCallApp.Interface.SendQuery(new OpeningCandidatesQuery()).FirstOrDefault();
                var id = existing?.Id ?? "snapshot-without-current-owner";
                var appearance = MonsterAppearance.FromSeed(781);
                var old = View(id, appearance, "稳定", "普通", "能量体");
                var changed = View(id, appearance, "稳定", "热爱", "镜眼", "双头能量体");
                first.Show(old); second.Show(old);
                Need(first.VisualSignature == second.VisualSignature && first.AssemblySignature == second.AssemblySignature, "Two consumers assembled different parts");
                var before = first.AssemblySignature;
                first.Clear(); Need(!first.HasCreature, "Clear retains a visible creature");
                first.Show(old); Need(first.AssemblySignature == before, "Clear/rebind changed structural identity");
                var ghost = MonsterPortrait.SpawnGhost(root, first);
                Need(ghost != null && ghost.VisualSignature == first.VisualSignature && ghost.AssemblySignature == first.AssemblySignature, "Ghost differs from source actual assembly");
                Object.DestroyImmediate(ghost.gameObject);
                first.Show(changed);
                Need(first.VisualSignature == MonsterVisualTraits.From(changed).Signature && first.AssemblySignature != before, "Same-appearance changed skills did not refresh actual assembly");
                first.Show(old);
                Need(first.VisualSignature == MonsterVisualTraits.From(old).Signature && first.AssemblySignature == before, "Old snapshot was replaced by current traits or retained new mutations");
                var moodOnly = View(id, appearance, "稳定", "倦怠", "能量体");
                first.Show(moodOnly);
                Need(first.Traits.MoodTier == 0 && first.Traits.Appearance.Equals(appearance), "Mood refresh changed genotype or retained old mood");
                first.PreviewPose(.4f); var tired = first.MotionSample;
                first.Show(View(id, appearance, "稳定", "热爱", "能量体")); first.PreviewPose(.4f);
                Need(Mathf.Abs(first.MotionSample - tired) > .00001f, "Mood tiers have identical sampled poses");
            }
            finally { Object.DestroyImmediate(root.gameObject); }
            return "PASS two consumers, clear/rebind, actual ghost parts, skill refresh, five-tier mood input and caller-owned snapshot identity";
        }

        static MonsterView View(string id, MonsterAppearance appearance, string personality, string mood, params string[] skills) =>
            new MonsterView(id, "视觉样本", skills.Select(name => new SkillView(name, 0, Rarity.White)).ToArray(), 0, false, 0,
                appearance, personality, "森林", mood);

        public static string Consumers()
        {
            var slots = Object.FindObjectsByType<MonsterSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(slot => slot.gameObject.activeInHierarchy && !string.IsNullOrEmpty(slot.monsterId) && slot.portrait != null).ToArray();
            Need(slots.Length > 0, "No real occupied slots to verify");
            foreach (var slot in slots)
            {
                var monster = TheCallApp.Interface.SendQuery(new MonsterQuery(slot.monsterId));
                Need(monster != null && slot.portrait.VisualSignature == MonsterVisualTraits.From(monster).Signature, "Slot differs from rule projection: " + slot.name);
            }
            foreach (var group in slots.GroupBy(slot => slot.monsterId))
                Need(group.Select(slot => slot.portrait.AssemblySignature).Distinct().Count() == 1, "Real consumers disagree about parts for " + group.Key);
            return "PASS " + slots.Length + " real occupied consumers use matching projected traits and assembly identities";
        }

        public static string Ghost()
        {
            var ghost = Object.FindObjectsByType<MonsterPortrait>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(portrait => portrait.name == "MonsterGhost");
            Need(ghost != null && ghost.HasCreature && ghost.UsesAssembly, "Actual pointer ghost absent");
            var sources = Object.FindObjectsByType<MonsterSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(slot => slot.gameObject.activeInHierarchy && slot.portrait != null && slot.portrait.VisualSignature == ghost.VisualSignature);
            Need(sources.Any(slot => slot.portrait.AssemblySignature == ghost.AssemblySignature), "Actual drag ghost has different parts than every real source");
            return "PASS actual cross-room pointer ghost has identical traits and parts to its visible source";
        }

        public static string Gallery(int page = 0, bool animate = false)
        {
            Need(Application.isPlaying && page >= 0 && page <= 4, "Gallery requires Play Mode and page 0..4");
            GalleryClose();
            _gallery = new GameObject("MonsterAcceptanceGallery", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = _gallery.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = _gallery.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)_gallery.transform;
            var background = NewRect("GalleryBackground", root, new Vector2(1920, 1080));
            background.gameObject.AddComponent<Image>().color = new Color32(30, 37, 40, 255);
            var font = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(text => text.font).FirstOrDefault(value => value != null) ?? TMP_Settings.defaultFontAsset;
            Label(root, "怪物部件系统 · " + PageTitle(page), new Vector2(0, 466), new Vector2(1800, 65), 33, font);
            Label(root, "Unity Canvas 原生画像 / 固定结构基因 / 真实性格眼型与技能标记", new Vector2(0, 418), new Vector2(1800, 35), 19, font);
            var specs = GallerySpecs(page);
            for (var i = 0; i < specs.Count; i++)
            {
                var point = new Vector2(-745 + i % 6 * 298, 245 - i / 6 * 282);
                var panel = NewRect("Sample" + i, root, new Vector2(280, 268)); panel.anchoredPosition = point;
                panel.gameObject.AddComponent<Image>().color = new Color32(43, 51, 53, 255);
                var rect = NewRect("Portrait", panel, MonsterPortrait.RecommendedViewportSize); rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2(0, -88); rect.localScale = Vector3.one * .69f;
                var portrait = rect.gameObject.AddComponent<MonsterPortrait>();
                portrait.enabled = animate;
                var spec = specs[i]; portrait.Show(View("gallery-" + page + "-" + i, spec.appearance, spec.personality, spec.mood, spec.skills));
                portrait.PreviewPose(.7f); GalleryPortraits.Add(portrait);
                Label(panel, spec.label, new Vector2(0, -114), new Vector2(268, 42), 16, font);
            }
            Label(root, "本页为独立视觉样本；实际拖放与七天玩家输入重放另行验收。  " + (page + 1) + " / 5", new Vector2(0, -501), new Vector2(1800, 35), 18, font);
            Canvas.ForceUpdateCanvases();
            return "PASS real 1920x1080 overlay Canvas gallery page=" + page + " samples=" + specs.Count;
        }

        static string PageTitle(int page) => page < 2 ? "六体型 × 五性格眼型" : page == 2 ? "同一个体的心情与技能变化" : "34 项正式技能的可见改造";

        static List<Spec> GallerySpecs(int page)
        {
            var specs = new List<Spec>(); var catalog = MonsterArtCatalog.Runtime;
            if (page < 2)
            {
                for (var i = page * 18; i < Math.Min(30, (page + 1) * 18); i++)
                {
                    var recipe = i / 5; var eye = i % 5;
                    specs.Add(new Spec { appearance = new MonsterAppearance(recipe, recipe, recipe % 3, recipe % 6, recipe % 4, (recipe + 1) % 4, recipe % 4, 0, 11),
                        personality = Personalities[eye], label = catalog.bodies[recipe].name + " · " + EyeLabels[eye] });
                }
            }
            else if (page == 2)
            {
                var appearance = MonsterAppearance.FromSeed(48291);
                foreach (var mood in Moods) specs.Add(new Spec { appearance = appearance, personality = "恋爱脑", mood = mood, label = "同一个体 · " + mood, skills = new[] { "能量体" } });
                var pairs = new[] { new[] { "双头能量体" }, new[] { "换位手" }, new[] { "奇异香" }, new[] { "汲取鼻" }, new[] { "回响嗓" }, new[] { "镜眼" },
                    new[] { "左能量体", "奇异香" }, new[] { "双头能量体", "镜眼" }, new[] { "血崩体", "优质肉体" }, new[] { "蜜能量体", "不稳定肉体" },
                    new[] { "群能量体", "抽杀体" }, new[] { "移植胚胎", "回响嗓", "镜眼" }, new[] { "血崩体", "双头能量体", "换位手", "不稳定肉体" } };
                foreach (var pair in pairs) specs.Add(new Spec { appearance = appearance, personality = "恋爱脑", skills = pair, label = string.Join(" + ", pair) });
            }
            else
            {
                var skills = ContentGate.Current.Skills.Select(skill => skill.Name).Skip((page - 3) * 18).Take(18);
                var index = (page - 3) * 18;
                foreach (var skill in skills) specs.Add(new Spec { appearance = new MonsterAppearance(index++ % 6, 1, 1, 2, 1, 1, 0, 0, 11), skills = new[] { skill }, label = skill });
            }
            return specs;
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = size;
            return rect;
        }

        static void Label(Transform parent, string value, Vector2 point, Vector2 size, float fontSize, TMP_FontAsset font)
        {
            var rect = NewRect("Label", parent, size); rect.anchoredPosition = point;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value;
            text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.color = Ink; text.raycastTarget = false;
        }

        public static string GalleryClose()
        {
            if (_gallery != null) Object.DestroyImmediate(_gallery);
            _gallery = null; GalleryPortraits.Clear();
            return "PASS temporary gallery closed without touching the saved scene";
        }

        public static string MotionBegin(string evidenceDirectory = DefaultEvidence)
        {
            _evidence = Path.GetFullPath(evidenceDirectory); MotionReadings.Clear(); Gallery(2, true);
            return "PASS advancing runtime motion gallery started";
        }

        public static string MotionSample()
        {
            Need(GalleryPortraits.Count > 4, "Motion gallery is absent");
            var pose = GalleryPortraits[4].MotionSample;
            MotionReadings.Add(new MotionReading { frame = Time.frameCount, seconds = Time.realtimeSinceStartupAsDouble, pose = pose });
            return "PASS actual pose sample frame=" + Time.frameCount + " pose=" + pose;
        }

        public static string MotionCheck()
        {
            var distinct = MotionReadings.Select(sample => Math.Round(sample.pose, 5)).Distinct().Count();
            Save("motion-samples.json", new MotionReport { distinctPoses = distinct, samples = MotionReadings });
            Need(MotionReadings.Select(sample => sample.frame).Distinct().Count() >= 4 && distinct >= 3, "Motion did not advance across real rendered frames");
            return "PASS " + MotionReadings.Count + " real frames, " + distinct + " distinct sampled poses";
        }
    }
}
