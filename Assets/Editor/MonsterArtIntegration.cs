using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;
namespace TheCall.Editor
{
    /// <summary>Reproducible extraction of the artist's JPG, with a single flat fill per colorway.</summary>
    public static class MonsterArtIntegration
    {
        public const string CatalogPath = "Assets/Resources/MonsterArtCatalog.asset";
        public const string Output = "Assets/Arts/Monsters/Whole";
        const string Source = "Assets/示意图/怪物实例.jpg";
        // Artist coordinates in the unchanged 1491x1109 sheet, origin at top left.
        static readonly RectInt[] Crops = {
            new RectInt(25,24,341,270), new RectInt(480,5,285,307),
            new RectInt(45,311,370,187), new RectInt(497,331,309,249),
            new RectInt(45,543,295,243), new RectInt(471,609,225,214),
            new RectInt(704,601,179,190), new RectInt(44,828,359,146),
            new RectInt(496,826,159,195), new RectInt(675,801,165,220),
            new RectInt(856,779,183,245), new RectInt(1039,755,338,270)
        };
        static readonly string[] Names = {"双首爬行者","缝合脑兽","双子星","双首触须","群星","脑袋兽","纽扣猫","长耳星纹兽","铆钉兽","独角兽","触角兽","章鱼兽"};
        [MenuItem("TheCall/Monster/从参考图提取完整怪物")]
        public static void Apply() => Apply(false);
        public static void Apply(bool resetPalettes)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing monster art.");
            Directory.CreateDirectory(Output);
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterArtCatalog>(CatalogPath);
            var fresh = catalog == null;
            if (fresh) catalog = ScriptableObject.CreateInstance<MonsterArtCatalog>();
            if (fresh || resetPalettes || catalog.schemaVersion != 3) catalog.palettes = MonsterTemplateDefaults.Palettes();
            var sheet = new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                ImageConversion.LoadImage(sheet,File.ReadAllBytes(Source));
                if (sheet.width != 1491 || sheet.height != 1109) throw new InvalidOperationException("Reference dimensions changed. Update crop definitions first.");
                catalog.bodies = new MonsterArtCatalog.BodyPlan[12];
                var all = sheet.GetPixels32();
                for (var id=0;id<12;id++)
                {
                    var crop=Crops[id]; var cut=Extract(all,sheet.width,sheet.height,crop);
                    var body=new MonsterArtCatalog.BodyPlan { id=(MonsterTemplate)id,name=Names[id],
                        frame=new Rect(-crop.width*.5f,0,crop.width,crop.height),colorways=new Sprite[6] };
                    body.source=Write("Source-"+id,cut,crop.width,crop.height);
                    for(var p=0;p<6;p++) body.colorways[p]=Write("Monster-"+id+"-"+p,Colorize(cut,crop.width,crop.height,catalog.Colorway(p)),crop.width,crop.height);
                    catalog.bodies[id]=body;
                }
            }
            finally {Object.DestroyImmediate(sheet);}
            catalog.schemaVersion=3;catalog.MarkChanged();
            if(!catalog.Ready)throw new InvalidOperationException(catalog.ValidationError());
            if(fresh)AssetDatabase.CreateAsset(catalog,CatalogPath);
            EditorUtility.SetDirty(catalog);
            const string atlasPath="Assets/Resources/WholeMonstersAtlas.spriteatlas";
            var atlas=AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if(atlas==null){atlas=new SpriteAtlas();AssetDatabase.CreateAsset(atlas,atlasPath);}
            var old=SpriteAtlasExtensions.GetPackables(atlas);
            if(old.Length>0)SpriteAtlasExtensions.Remove(atlas,old);
            SpriteAtlasExtensions.Add(atlas,catalog.bodies.SelectMany(b=>b.colorways).Cast<Object>().ToArray());
            SpriteAtlasExtensions.SetPackingSettings(atlas,new SpriteAtlasPackingSettings {padding=4,enableRotation=false,enableTightPacking=false});
            SpriteAtlasExtensions.SetTextureSettings(atlas,new SpriteAtlasTextureSettings {readable=false,generateMipMaps=false,sRGB=true,filterMode=FilterMode.Bilinear});
            SpriteAtlasExtensions.SetIncludeInBuild(atlas,true);EditorUtility.SetDirty(atlas);
            // Retired composition resources are no longer dependencies of the player.
            AssetDatabase.DeleteAsset("Assets/Resources/MonsterPartsAtlas.spriteatlas");
            AssetDatabase.DeleteAsset("Assets/Resources/MonsterTintMasks");
            AssetDatabase.SaveAssets();
            SpriteAtlasUtility.PackAtlases(new[]{atlas},EditorUserBuildSettings.activeBuildTarget);
            Debug.Log("Whole monster art ready: 12 faithful cutouts, 72 flat colorways, one Image per portrait.");
        }
        static Color32[] Extract(Color32[] sheet,int sw,int sh,RectInt crop)
        {
            var w=crop.width;var h=crop.height;var pixels=new Color32[w*h];
            for(var y=0;y<h;y++)for(var x=0;x<w;x++)pixels[y*w+x]=sheet[(sh-crop.y-h+y)*sw+crop.x+x];
            var background=new bool[pixels.Length];var visited=new bool[pixels.Length];
            bool Gray(Color32 c)=>Math.Max(c.r,Math.Max(c.g,c.b))-Math.Min(c.r,Math.Min(c.g,c.b))<=10&&Math.Abs(c.r-159)<=10;
            for(var start=0;start<pixels.Length;start++)
            {
                if(visited[start]||!Gray(pixels[start]))continue;
                var component=new List<int>();var q=new Queue<int>();q.Enqueue(start);visited[start]=true;var edge=false;
                while(q.Count>0)
                {
                    var i=q.Dequeue();component.Add(i);var x=i%w;var y=i/w;edge|=x==0||x==w-1||y==0||y==h-1;
                    void Visit(int n){if(!visited[n]&&Gray(pixels[n])){visited[n]=true;q.Enqueue(n);}}
                    if(x>0)Visit(i-1);if(x<w-1)Visit(i+1);if(y>0)Visit(i-w);if(y<h-1)Visit(i+w);
                }
                // Equal-gray paint inside a closed body is still flesh. Only explicit negative-space
                // seeds, or regions connected to the crop edge, may be removed.
                var hole = component.Any(i => Hole(crop.x+i%w,crop.y+h-1-i/w));
                if(edge||hole)foreach(var i in component)background[i]=true;
            }
            for(var y=0;y<h;y++)for(var x=0;x<w;x++)
            {
                var i=y*w+x;
                if(background[i]){pixels[i]=new Color32(0,0,0,0);continue;}
                var c=pixels[i];var near=false;
                for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++)
                {var xx=x+dx;var yy=y+dy;if(xx>=0&&xx<w&&yy>=0&&yy<h&&background[yy*w+xx])near=true;}
                if(!near)continue;
                // Unmatte antialiased outline/white claws against the original gray, avoiding a gray halo.
                if(Math.Max(c.r,Math.Max(c.g,c.b))-Math.Min(c.r,Math.Min(c.g,c.b))<=12)
                {
                    var v=(c.r+c.g+c.b)/3f;
                    var white=v>170;var a=white?(v-159)/96f:(159-v)/159f;
                    var k=(byte)(white?255:0);pixels[i]=new Color32(k,k,k,(byte)Mathf.RoundToInt(Mathf.Clamp01(a)*255));
                }
            }
            return pixels;
        }
        static bool Hole(int x,int y) => x==230&&y==407 || x==548&&y==466 || x==749&&y==471 ||
            x==1080&&y==929 || x==1146&&y==963 || x==1268&&y==963 || x==1328&&y==928;
        static Color32[] Colorize(Color32[] original,int w,int h,MonsterArtCatalog.Palette palette)
        {
            var result=(Color32[])original.Clone();
            var fill=Color.Lerp(palette.pattern,palette.body,.60f);
            int Value(Color32 c)=>Math.Max(c.r,Math.Max(c.g,c.b));
            bool Neutral(Color32 c)=>Value(c)-Math.Min(c.r,Math.Min(c.g,c.b))<=12;
            for(var y=0;y<h;y++)for(var x=0;x<w;x++)
            {
                var i=y*w+x;var source=original[i];
                if(source.a==0||Value(source)<30)continue;
                // All eyes, teeth, bandages and flesh share one solid fill for this temporary patch.
                var color=fill;var nearInk=false;
                for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++)
                {
                    var xx=x+dx;var yy=y+dy;
                    if(xx<0||xx>=w||yy<0||yy>=h)continue;
                    var neighbor=original[yy*w+xx];
                    if(neighbor.a>0&&(Value(neighbor)<30||Value(neighbor)<64&&Neutral(neighbor)))nearInk=true;
                }
                // Retain antialiasing beside the original dark linework without a white matte.
                if(nearInk&&Neutral(source))color*=Value(source)/255f;
                color.a=source.a/255f;result[i]=color;
            }
            return result;
        }
        static Sprite Write(string name,Color32[] pixels,int w,int h)
        {
            var path=Output+"/"+name+".png";var texture=new Texture2D(w,h,TextureFormat.RGBA32,false);
            try {texture.SetPixels32(pixels);texture.Apply();var bytes=texture.EncodeToPNG();if(!File.Exists(path)||!File.ReadAllBytes(path).SequenceEqual(bytes))File.WriteAllBytes(path,bytes);}
            finally {Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;
            importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=1024;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
