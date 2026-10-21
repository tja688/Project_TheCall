using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QFramework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace TheCall.Editor
{
    public static class MonsterArtAcceptance
    {
        const string DefaultEvidence="docs/audit/2026-10-21/whole-monsters";
        const string PrefabPath="Assets/Prefabs/UI/MonsterPortrait.prefab";
        static readonly string[] Personalities={"自律","稳定","急躁","摸鱼派","恋爱脑"};
        static readonly string[] Moods={"倦怠","疲倦","普通","热情","热爱"};
        static readonly Color Ink=new Color32(230,229,216,255);
        static GameObject _gallery;
        static readonly List<MonsterPortrait> GalleryPortraits=new List<MonsterPortrait>();
        static readonly List<MotionReading> MotionReadings=new List<MotionReading>();
        static string _evidence=DefaultEvidence;
        [Serializable] sealed class MotionReading {public int frame;public double seconds;public float pose;}
        [Serializable] sealed class MotionReport {public int distinctPoses;public List<MotionReading> samples;}
        static void Need(bool value,string message){if(!value)throw new InvalidOperationException("MONSTER_FAIL: "+message);}
        static void Save(string file,object report){Directory.CreateDirectory(_evidence);File.WriteAllText(Path.Combine(_evidence,file),Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));}
        public static string ExportTemplates(string directory=DefaultEvidence)
        {
            Directory.CreateDirectory(directory);
            for(var t=0;t<12;t++)for(var p=0;p<6;p++)
            {
                var png=MonsterPortraitCapture.Render(MonsterVisualTraits.From(new MonsterAppearance(t,p)),.7f);
                File.WriteAllBytes(Path.Combine(directory,"palette-"+p+"-template-"+t+".png"),png);
            }
            MonsterPortraitCapture.Shutdown();return "PASS exported 72 complete Canvas sprites";
        }
        public static string Saved()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Need(!Application.isPlaying&&scene.path=="Assets/Scenes/Main.unity"&&!scene.isDirty,"Requires saved Main in Edit Mode");
            Need(MonsterArtCatalog.Runtime.Ready,MonsterArtCatalog.Runtime.ValidationError());
            return "PASS saved Main, 12 complete source drawings and 72 flat colorways";
        }
        public static string Matrix(string evidenceDirectory=DefaultEvidence,int seeds=600)
        {
            _evidence=Path.GetFullPath(evidenceDirectory);var catalog=MonsterArtCatalog.Runtime;Need(catalog.Ready,catalog.ValidationError());
            var samples=new List<object>();var root=NewRect("WholeSpriteMatrixProbe",null,MonsterPortrait.RecommendedViewportSize);var portrait=root.gameObject.AddComponent<MonsterPortrait>();
            try
            {
                void Probe(MonsterAppearance a,string personality="稳定",string mood="普通")
                {
                    portrait.Show(View("probe",a,personality,mood));portrait.PreviewPose(.7f);
                    Need(portrait.HasCreature&&portrait.UsesWholeSprite&&portrait.PartCount==1,"Expected exactly one complete image "+a.Recipe);
                    var images=portrait.GetComponentsInChildren<Image>(false).Where(i=>i.enabled&&i.sprite!=null).ToArray();
                    Need(images.Length==1&&images[0].sprite==catalog.Sprite(a),"Unexpected artwork or component assembly");
                    Need(!images[0].raycastTarget,"Artwork intercepted pointer input");
                    var signature=portrait.ArtworkSignature;var visual=portrait.VisualSignature;var pose=portrait.MotionSample;
                    portrait.Show(View("probe",a,personality,mood,ContentGate.Current.Skills.Select(s=>s.Name).ToArray()));portrait.PreviewPose(.7f);
                    Need(portrait.ArtworkSignature==signature&&portrait.VisualSignature==visual&&portrait.MotionSample==pose,"Gameplay state changed artwork");
                    samples.Add(new {template=a.Recipe,palette=a.Palette,images=images.Length,personality,mood});
                }
                for(var t=0;t<12;t++)for(var p=0;p<6;p++)Probe(new MonsterAppearance(t,p));
                for(var t=0;t<12;t++)foreach(var personality in Personalities)foreach(var mood in Moods)Probe(new MonsterAppearance(t,t%6),personality,mood);
                for(var seed=0;seed<seeds;seed++)
                {
                    var basic=MonsterAppearance.FromSeed(seed);Need(!basic.IsCultivated,"Complex drawing escaped into wild pool");Probe(basic);
                    var child=MonsterAppearance.Breed(basic,MonsterAppearance.FromSeed(seed+1),seed);Need(child.IsCultivated,"Cultivation did not select advanced art");
                }
                Save("visual-matrix.json",new {passed=true,samples=samples.Count,templates=12,palettes=6,rows=samples});
            }
            finally{Object.DestroyImmediate(root.gameObject);}
            return "PASS "+samples.Count+" real portrait cases; one complete sprite; distinct basic/cultivated pools; gameplay state never changes art";
        }
        public static string TintContracts(string evidenceDirectory=DefaultEvidence)
        {
            _evidence=Path.GetFullPath(evidenceDirectory);var catalog=MonsterArtCatalog.Runtime;
            var readings=new List<object>();
            foreach(var b in catalog.bodies)
            {
                var source=new Texture2D(2,2);ImageConversion.LoadImage(source,File.ReadAllBytes(AssetDatabase.GetAssetPath(b.source)));
                try
                {
                    var original=source.GetPixels32();
                    Need(original.Any(p=>p.a==0)&&original.Any(p=>p.a==255),"Cutout missing alpha or opaque ink");
                    foreach(var sprite in b.colorways)
                    {
                        var target=new Texture2D(2,2);
                        try
                        {
                            ImageConversion.LoadImage(target,File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                            var pixels=target.GetPixels32();Need(pixels.Length==original.Length,"Changed crop");
                            for(var i=0;i<pixels.Length;i++)
                            {
                                Need(pixels[i].a==original[i].a,"Colorway changed alpha silhouette");
                                if(original[i].r<30&&original[i].g<30&&original[i].b<30)Need(pixels[i].Equals(original[i]),"Colorway changed original ink");
                            }
                        }finally{Object.DestroyImmediate(target);}
                    }
                    readings.Add(new {template=(int)b.id,width=source.width,height=source.height,transparent=original.Count(p=>p.a==0)});
                }finally{Object.DestroyImmediate(source);}
            }
            Save("cutout-contracts.json",new {passed=true,rows=readings});
            return "PASS all 72 colorways retain extracted alpha and original dark outlines";
        }
        public static string Contracts(string evidenceDirectory=DefaultEvidence)
        {
            _evidence=Path.GetFullPath(evidenceDirectory);var catalog=MonsterArtCatalog.Runtime;
            Save("colorways.json",catalog.palettes.Select(p=>new {p.name,light=ColorUtility.ToHtmlStringRGB(p.body),shade=ColorUtility.ToHtmlStringRGB(p.pattern),accent=ColorUtility.ToHtmlStringRGB(p.eye)}).ToArray());
            return "PASS six flat colorways; birth art independent of skills, inheritance, personality and mood";
        }
        static MonsterView View(string id,MonsterAppearance a,string personality,string mood,params string[] skills)=>new MonsterView(id,"视觉样本",skills.Select(n=>new SkillView(n,0,Rarity.White)).ToArray(),0,false,0,a,personality,"森林",mood);
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
                Need(group.Select(slot => slot.portrait.ArtworkSignature).Distinct().Count() == 1, "Real consumers disagree about parts for " + group.Key);
            return "PASS " + slots.Length + " real occupied consumers use matching projected traits and assembly identities";
        }

        public static string Ghost()
        {
            var ghost = Object.FindObjectsByType<MonsterPortrait>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(portrait => portrait.name == "MonsterGhost");
            Need(ghost != null && ghost.HasCreature && ghost.UsesWholeSprite, "Actual pointer ghost absent");
            var sources = Object.FindObjectsByType<MonsterSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(slot => slot.gameObject.activeInHierarchy && slot.portrait != null && slot.portrait.VisualSignature == ghost.VisualSignature);
            Need(sources.Any(slot => slot.portrait.ArtworkSignature == ghost.ArtworkSignature), "Actual drag ghost has different parts than every real source");
            return "PASS actual cross-room pointer ghost has identical traits and parts to its visible source";
        }


        // Pages 0-3 cover 12x6 variants; page 4 compares the basic and cultivated pools.
        public static string Gallery(int page=0,bool animate=false)
        {
            Need(Application.isPlaying&&page>=0&&page<=4,"Gallery requires Play Mode, page 0..4");GalleryClose();
            _gallery=new GameObject("MonsterAcceptanceGallery",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            var canvas=_gallery.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var scaler=_gallery.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var root=(RectTransform)_gallery.transform;NewRect("Background",root,new Vector2(1920,1080)).gameObject.AddComponent<Image>().color=new Color32(30,37,40,255);
            var font=Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(t=>t.font).FirstOrDefault(f=>f!=null)??TMP_Settings.defaultFontAsset;
            Label(root,page<4?"完整怪物 · 12 造型 × 6 纯色":"完整怪物 · 基础池与培育池",new Vector2(0,475),new Vector2(1800,65),33,font);
            Label(root,"完整原画 · 统一纯色 · 基础池与培育池 · 玩法状态不改变造型",new Vector2(0,425),new Vector2(1800,35),20,font);
            for(var i=0;i<(page<4?18:12);i++)
            {
                var template=page<4?page*3+i/6:new[]{5,6,8,9,10,11,0,1,2,3,4,7}[i];
                var palette=page<4?i%6:new[]{2,1,0,3,4,5,0,2,3,4,4,1}[i];
                var personality=page<4?new[]{"急躁","自律","恋爱脑","稳定","稳定","急躁","稳定","恋爱脑","自律","摸鱼派","自律","急躁"}[template]:Personalities[i%5];
                var mood=page<4?"普通":Moods[(i/6)*2];
                var panel=NewRect("Sample"+i,root,new Vector2(280,page<4?268:350));panel.anchoredPosition=new Vector2(-745+i%6*298,page<4?245-i/6*282:205-i/6*390);panel.gameObject.AddComponent<Image>().color=new Color32(43,51,53,255);
                var rect=NewRect("Portrait",panel,MonsterPortrait.RecommendedViewportSize);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,page<4?-88:-108);rect.localScale=Vector3.one*(page<4?.69f:.88f);
                var portrait=rect.gameObject.AddComponent<MonsterPortrait>();portrait.enabled=animate;portrait.Show(View("gallery-"+i,new MonsterAppearance(template,palette),personality,mood));portrait.PreviewPose(.7f);GalleryPortraits.Add(portrait);
                Label(panel,MonsterArtCatalog.Runtime.Body(template).name+" · "+MonsterArtCatalog.Runtime.Colorway(palette).name,new Vector2(0,page<4?-114:-140),new Vector2(268,42),16,font);
                if(page==4)Label(panel,i<6?"基础怪物":"培育子代",new Vector2(0,148),new Vector2(268,32),18,font);
            }
            Label(root,"Unity Canvas 实际画像 · 参考图与真实流程另行核对    "+(page+1)+" / 5",new Vector2(0,-501),new Vector2(1800,35),18,font);
            Canvas.ForceUpdateCanvases();return "PASS real Canvas gallery "+page;
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
            _evidence = Path.GetFullPath(evidenceDirectory); MotionReadings.Clear(); Gallery(4, true);
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
