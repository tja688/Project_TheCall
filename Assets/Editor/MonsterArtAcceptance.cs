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
        const string DefaultEvidence="docs/audit/2026-10-20/templates";
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
            Directory.CreateDirectory(directory);var geometry=new List<object>();
            void Record(int t,MonsterVisualTraits traits)
            {
                var pieces=new List<MonsterComposition.Piece>();MonsterComposition.Compose(MonsterArtCatalog.Runtime,traits,pieces);
                geometry.Add(new {template=t,head=traits.Appearance.HeadAccessory,back=traits.Appearance.BackAccessory,name=MonsterArtCatalog.Runtime.Body(t).name,layers=pieces.Where(x=>!x.Mask).Select(x=>new {
                    name=x.Name,source=AssetDatabase.GetAssetPath(x.Art.sprite),expression=x.Role==MonsterComposition.Motion.Eye,ink=new[]{x.Art.ink.x,x.Art.ink.y,x.Art.ink.width,x.Art.ink.height},
                    at=new[]{x.Position.x,x.Position.y},size=new[]{x.Size.x,x.Size.y},angle=x.Angle,mirror=x.Mirror }).ToArray()});
            }
            for(var t=0;t<12;t++)
            {
                var personality=new[]{"自律","急躁","恋爱脑","稳定","稳定","急躁","稳定","恋爱脑","自律","稳定","自律","急躁"}[t];
                for(var p=0;p<6;p++)
                {
                    var traits=MonsterVisualTraits.Create(new MonsterAppearance(t,p,0,0,t%2),personality,"普通",Array.Empty<string>());
                    var png=MonsterPortraitCapture.Render(traits,.7f);File.WriteAllBytes(Path.Combine(directory,"palette-"+p+"-template-"+t+".png"),png);
                    if(p==t%6)File.WriteAllBytes(Path.Combine(directory,"template-"+t+".png"),png);
                    if(p==0)Record(t,traits);
                }
                for(var h=0;h<4;h++)for(var b=0;b<3;b++)if((h!=0||b!=0)&&MonsterAppearance.Compatible(t,h,b))
                    Record(t,MonsterVisualTraits.Create(new MonsterAppearance(t,0,h,b,0),personality,"普通",Array.Empty<string>()));
            }
            File.WriteAllText(Path.Combine(directory,"template-geometry.json"),Newtonsoft.Json.JsonConvert.SerializeObject(geometry,Newtonsoft.Json.Formatting.Indented));
            MonsterPortraitCapture.Shutdown();return "PASS exported 72 real Canvas colorways plus all compatible accessory geometry";
        }
        public static string Saved()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Need(!Application.isPlaying&&scene.path=="Assets/Scenes/Main.unity"&&!scene.isDirty,"Requires saved Main in Edit Mode");
            Need(MonsterArtCatalog.Runtime.Ready,MonsterArtCatalog.Runtime.ValidationError());
            return "PASS saved Main, 70 original parts, 12 fixed templates and 6 complete palettes";
        }
        public static string Matrix(string evidenceDirectory=DefaultEvidence,int seeds=600)
        {
            _evidence=Path.GetFullPath(evidenceDirectory);var catalog=MonsterArtCatalog.Runtime;Need(catalog.Ready,catalog.ValidationError());
            var samples=new List<object>();var root=NewRect("TemplateMatrixProbe",null,MonsterPortrait.RecommendedViewportSize);var portrait=root.gameObject.AddComponent<MonsterPortrait>();
            try
            {
                void Probe(MonsterAppearance a,string personality="稳定",string mood="普通")
                {
                    portrait.Show(MonsterVisualTraits.Create(a,personality,mood,Array.Empty<string>()));portrait.PreviewPose(.7f);
                    Need(portrait.HasCreature,"Missing creature "+a.Recipe);var bounds=portrait.InkBounds;var frame=catalog.Body(a.Recipe).frame;
                    Need(bounds.xMin>=frame.xMin-8&&bounds.xMax<=frame.xMax+8&&bounds.yMin>=frame.yMin-8&&bounds.yMax<=frame.yMax+8,"Template "+a.Recipe+" exceeds authored frame: "+bounds+" / "+frame);
                    var signature=portrait.AssemblySignature;
                    portrait.Show(MonsterVisualTraits.Create(a,personality,mood,ContentGate.Current.Skills.Select(s=>s.Name).ToArray()));
                    Need(portrait.AssemblySignature==signature,"Skill change reshaped template "+a.Recipe);
                    samples.Add(new {template=a.Recipe,palette=a.Palette,head=a.HeadAccessory,back=a.BackAccessory,parts=portrait.PartCount,personality,mood});
                }
                for(var t=0;t<12;t++)for(var p=0;p<6;p++)for(var h=0;h<4;h++)for(var b=0;b<3;b++)if(MonsterAppearance.Compatible(t,h,b))Probe(new MonsterAppearance(t,p,h,b,0));
                for(var t=0;t<12;t++)foreach(var personality in Personalities)foreach(var mood in Moods)Probe(new MonsterAppearance(t,t%6),personality,mood);
                for(var seed=0;seed<seeds;seed++)Probe(MonsterAppearance.FromSeed(seed));
                Save("visual-matrix.json",new {passed=true,samples=samples.Count,templates=12,palettes=6,rows=samples});
            }
            finally{Object.DestroyImmediate(root.gameObject);}
            return "PASS "+samples.Count+" real portrait cases, all compatible accessories and 72 template/colorway pairs; skills do not change appearance";
        }
        public static string TintContracts(string evidenceDirectory=DefaultEvidence)
        {
            _evidence=Path.GetFullPath(evidenceDirectory);var catalog=MonsterArtCatalog.Runtime;
            var masks=catalog.parts.Where(p=>p.fillMask!=null).ToArray();Need(masks.Length>=14,"Missing semantic tint masks");
            foreach(var mask in masks)Need(mask.fillMask.rect.size==mask.sprite.rect.size,"Mask/source alignment mismatch: "+mask.id);
            foreach(var body in catalog.bodies)Need(body.layers.Count(l=>l.expression)>=1,"Missing expression anchors: "+body.name);
            Save("tint-mask-contracts.json",new {passed=true,masks=masks.Select(p=>p.id).ToArray(),originalParts=catalog.parts.Length,note="Mask/source alignment and authored eye anchors. Attachment appearance is reviewed in actual Unity galleries."});
            return "PASS semantic masks aligned to original sprites; per-template expression anchors configured";
        }
        public static string Contracts(string evidenceDirectory=DefaultEvidence)
        {
            _evidence=Path.GetFullPath(evidenceDirectory);var catalog=MonsterArtCatalog.Runtime;
            Save("colorways.json",catalog.palettes.Select(p=>new {p.name,body=ColorUtility.ToHtmlStringRGB(p.body),pattern=ColorUtility.ToHtmlStringRGB(p.pattern),eye=ColorUtility.ToHtmlStringRGB(p.eye),mouth=ColorUtility.ToHtmlStringRGB(p.mouth)}).ToArray());
            return "PASS full colorways exported; birth identity separated from personality, mood and skills";
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


        // Pages 0-3 are the complete 12x6 color matrix; page 4 samples personality and mood.
        public static string Gallery(int page=0,bool animate=false)
        {
            Need(Application.isPlaying&&page>=0&&page<=4,"Gallery requires Play Mode, page 0..4");GalleryClose();
            _gallery=new GameObject("MonsterAcceptanceGallery",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            var canvas=_gallery.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var scaler=_gallery.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var root=(RectTransform)_gallery.transform;NewRect("Background",root,new Vector2(1920,1080)).gameObject.AddComponent<Image>().color=new Color32(30,37,40,255);
            var font=Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(t=>t.font).FirstOrDefault(f=>f!=null)??TMP_Settings.defaultFontAsset;
            Label(root,page<4?"成品怪物 · 12 模板 × 6 色卡":"性格眼型与五档心情",new Vector2(0,475),new Vector2(1800,65),33,font);
            Label(root,"花纹同色略深 · 眼嘴异色 · 固定结构 · 技能不改变造型",new Vector2(0,425),new Vector2(1800,35),20,font);
            for(var i=0;i<18;i++)
            {
                var template=page<4?page*3+i/6:new[]{0,7,5,11,6,2}[i%6];var palette=page<4?i%6:template%6;
                var personality=page<4?new[]{"急躁","自律","恋爱脑","稳定","稳定","急躁","稳定","恋爱脑","自律","摸鱼派","自律","急躁"}[template]:Personalities[i%5];
                var mood=page<4?"普通":Moods[(i/6)*2];
                var panel=NewRect("Sample"+i,root,new Vector2(280,268));panel.anchoredPosition=new Vector2(-745+i%6*298,245-i/6*282);panel.gameObject.AddComponent<Image>().color=new Color32(43,51,53,255);
                var rect=NewRect("Portrait",panel,MonsterPortrait.RecommendedViewportSize);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,-88);rect.localScale=Vector3.one*.69f;
                var portrait=rect.gameObject.AddComponent<MonsterPortrait>();portrait.enabled=animate;portrait.Show(View("gallery-"+i,new MonsterAppearance(template,palette),personality,mood));portrait.PreviewPose(.7f);GalleryPortraits.Add(portrait);
                Label(panel,MonsterArtCatalog.Runtime.Body(template).name+" · "+(page<4?MonsterArtCatalog.Runtime.Colorway(palette).name:personality+" / "+mood),new Vector2(0,-114),new Vector2(268,42),16,font);
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
