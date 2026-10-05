using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ApplyHearthwoodAtlasSlices
{
    const string TexturePath = "Assets/Arts/Atlas/hearthwood.png";
    const string JsonPath = "Assets/Arts/Atlas/hearthwood.json";

    public static string Main()
    {
        if (!File.Exists(JsonPath))
            throw new Exception("Missing atlas manifest: " + JsonPath);

        var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
            throw new Exception("TextureImporter not found: " + TexturePath);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
            throw new Exception("No sprite data provider for " + TexturePath);
        dataProvider.InitSpriteEditorDataProvider();

        var editCapability = dataProvider.GetDataProvider<ISpriteFrameEditCapability>();
        if (editCapability == null)
            throw new Exception("Edit capability not supported.");
        var capability = editCapability.GetEditCapability();
        if (!capability.HasCapability(EEditCapability.CreateAndDeleteSprite)
            || !capability.HasCapability(EEditCapability.EditSpriteRect)
            || !capability.HasCapability(EEditCapability.EditBorder)
            || !capability.HasCapability(EEditCapability.EditSpriteName))
            throw new Exception("Importer missing required sprite edit capabilities.");

        var sourceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (sourceTex == null)
            throw new Exception("Could not load texture at " + TexturePath);
        var textureHeight = sourceTex.height;

        var root = JObject.Parse(File.ReadAllText(JsonPath));
        var origin = root.Value<string>("origin") ?? "top-left";
        var assets = root["assets"] as JArray;
        if (assets == null || assets.Count == 0)
            throw new Exception("Atlas manifest has no assets.");

        var spriteRects = new List<SpriteRect>(assets.Count);
        foreach (var token in assets)
        {
            var asset = (JObject)token;
            var name = asset.Value<string>("name");
            var atlasRect = asset["atlas_rect"] as JObject;
            if (string.IsNullOrEmpty(name) || atlasRect == null)
                continue;

            var x = atlasRect.Value<int>("x");
            var yTop = atlasRect.Value<int>("y");
            var w = atlasRect.Value<int>("w");
            var h = atlasRect.Value<int>("h");

            var y = origin == "top-left" ? textureHeight - yTop - h : yTop;

            var sr = new SpriteRect
            {
                name = name,
                spriteID = GUID.Generate(),
                rect = new Rect(x, y, w, h),
                alignment = (SpriteAlignment)0,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero
            };

            var nineSlice = asset["nine_slice"] as JObject;
            if (nineSlice != null)
            {
                var left = nineSlice.Value<float>("left");
                var top = nineSlice.Value<float>("top");
                var right = nineSlice.Value<float>("right");
                var bottom = nineSlice.Value<float>("bottom");
                sr.border = new Vector4(left, bottom, right, top);
            }

            spriteRects.Add(sr);
        }

        dataProvider.SetSpriteRects(spriteRects.ToArray());

        var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameFileIdProvider != null)
        {
            var pairs = new List<SpriteNameFileIdPair>(spriteRects.Count);
            foreach (var sr in spriteRects)
                pairs.Add(new SpriteNameFileIdPair(sr.name, GUID.Generate()));
            nameFileIdProvider.SetNameFileIdPairs(pairs.ToArray());
        }

        dataProvider.Apply();
        importer.SaveAndReimport();

        return $"Applied {spriteRects.Count} sprites to {TexturePath} (height={textureHeight}).";
    }
}
