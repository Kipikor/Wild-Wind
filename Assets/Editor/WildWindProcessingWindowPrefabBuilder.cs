using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class WildWindProcessingWindowPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/UI/Processing/BaseProcessingWindow.prefab";
    private const string SpriteFolder = "Assets/Resources/UI/Processing/Sprites";
    private const string FontAssetPath = "Assets/Resources/Fonts/Ubuntu SDF.asset";
    private const string UbuntuRegularPath = "Assets/Fonts/Ubuntu/Ubuntu-Regular.ttf";

    [MenuItem("Tools/Wild Wind/Rebuild Processing Window Prefab")]
    [MenuItem("Wild Wind/UI/Rebuild Processing Window Prefab")]
    public static void Rebuild()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/UI");
        EnsureFolder("Assets/Resources/UI/Processing");
        EnsureFolder(SpriteFolder);
        EnsureFolder("Assets/Resources/Fonts");

        AssetDatabase.Refresh();
        TMP_FontAsset font = EnsureUbuntuFontAsset();
        ProcessingSprites sprites = EnsureSprites();

        GameObject root = new GameObject("Processing Window Content", typeof(RectTransform), typeof(Image));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;
        Image rootImage = root.GetComponent<Image>();
        rootImage.sprite = sprites.rounded20;
        rootImage.type = Image.Type.Sliced;
        rootImage.color = new Color32(123, 138, 155, 115);
        rootImage.raycastTarget = false;

        Card(rootRect, "Processing Inner Panel", new Vector2(10f, -10f), new Vector2(1832f, 1004f), sprites.rounded20, new Color32(214, 220, 231, 255), false);

        RectTransform header = Card(rootRect, "Processing Header", new Vector2(10f, -10f), new Vector2(1832f, 76f), sprites.rounded10, new Color32(235, 237, 241, 255), false);
        Text(header, "Processing Header Title", "", 32, new Vector2(74.5f, -19.4f), new Vector2(520f, 37f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold, new Color32(58, 66, 83, 255), font);
        Text(header, "Processing Header Stats", "", 24, new Vector2(999f, -20f), new Vector2(790f, 34f), TextAlignmentOptions.MidlineRight, FontStyles.Normal, new Color32(58, 66, 83, 178), font);
        HeaderSeal(header, sprites);

        RectTransform storage = Warehouse(rootRect, "Processing Storage Warehouse", "Склад сырья", new Vector2(26f, -102f), new Vector2(520f, 896f), sprites, font);
        RectTransform bunker = Warehouse(rootRect, "Processing Bunker Warehouse", "Склад продукции", new Vector2(558f, -102f), new Vector2(726f, 896f), sprites, font);
        RectTransform outputs = Warehouse(rootRect, "Processing Output Warehouse", "Переработанная продукция", new Vector2(1296f, -102f), new Vector2(520f, 896f), sprites, font);

        for (int i = 0; i < 8; i++)
        {
            RectTransform row = ButtonRect(storage, "Input Row " + i, new Vector2(16f, -61f - i * 92f), new Vector2(484f, 80f), sprites.rounded10, new Color32(228, 230, 236, 255));
            Icon(row, "Resource Icon", new Vector2(4f, -7f), new Vector2(107f, 70f));
            Text(row, "Input Name", "", 24, new Vector2(116f, -29f), new Vector2(246f, 28f), TextAlignmentOptions.MidlineLeft, FontStyles.Normal, new Color32(58, 66, 83, 255), font);
            RectTransform amountPill = Card(row, "Amount Pill", new Vector2(392f, -22f), new Vector2(80f, 36f), sprites.rounded8, new Color32(216, 219, 224, 255), false);
            Text(amountPill, "Input Amount", "", 24, new Vector2(0f, -5f), new Vector2(80f, 26f), TextAlignmentOptions.Midline, FontStyles.Normal, new Color32(58, 66, 83, 255), font);
        }

        ScrollBar(storage, new Vector2(508f, -61f), new Vector2(2f, 752f), sprites);
        GradientButton(storage, "Load All Button", "Загрузить максимум", new Vector2(16f, -829f), new Vector2(484f, 50f), sprites.primaryButton, font);

        for (int i = 0; i < 9; i++)
        {
            int column = i % 3;
            int rowIndex = i / 3;
            RectTransform slot = Card(bunker, "Bunker Slot " + i, new Vector2(28f + column * 230f, -72f - rowIndex * 230f), new Vector2(210f, 210f), sprites.slot, Color.white, true);
            Icon(slot, "Resource Icon", new Vector2(55f, -28f), new Vector2(100f, 100f));
            Text(slot, "Slot Name", "", 20, new Vector2(14f, -132f), new Vector2(182f, 44f), TextAlignmentOptions.Top, FontStyles.Normal, new Color32(58, 66, 83, 255), font);
            RectTransform pill = Card(slot, "Slot Amount Pill", new Vector2(65f, -164f), new Vector2(80f, 36f), sprites.rounded8, new Color32(216, 219, 224, 255), false);
            Text(pill, "Slot Amount", "", 22, Vector2.zero, new Vector2(80f, 36f), TextAlignmentOptions.Midline, FontStyles.Normal, new Color32(58, 66, 83, 255), font);
            Text(slot, "Slot Progress", "", 18, new Vector2(12f, -12f), new Vector2(186f, 24f), TextAlignmentOptions.MidlineRight, FontStyles.Normal, new Color32(106, 120, 146, 255), font);
        }

        RectTransform stats = Card(rootRect, "Processing Bunker Stats", new Vector2(575f, -877f), new Vector2(692f, 104f), sprites.rounded10, new Color32(228, 230, 236, 255), false);
        Icon(stats, "Bulk Icon", new Vector2(7f, -20f), new Vector2(64f, 64f));
        Text(stats, "Bunker Capacity", "", 20, new Vector2(76f, -23f), new Vector2(150f, 24f), TextAlignmentOptions.MidlineLeft, FontStyles.Normal, new Color32(106, 120, 146, 255), font);
        Text(stats, "Bunker Slots", "", 20, new Vector2(305f, -23f), new Vector2(150f, 24f), TextAlignmentOptions.MidlineLeft, FontStyles.Normal, new Color32(106, 120, 146, 255), font);
        Line(stats, "Stats Divider A", new Vector2(230f, -8f), new Vector2(1f, 88f), new Color32(184, 197, 212, 255));
        Line(stats, "Stats Divider B", new Vector2(460f, -8f), new Vector2(1f, 88f), new Color32(184, 197, 212, 255));
        GradientButton(stats, "Clear Button", "Очистить", new Vector2(479f, -27f), new Vector2(194f, 50f), sprites.collectButton, font);
        Text(stats, "Transfer Title", "Выбери сырье", 18, new Vector2(76f, -54f), new Vector2(250f, 22f), TextAlignmentOptions.MidlineLeft, FontStyles.Normal, new Color32(58, 66, 83, 255), font);
        Text(stats, "Transfer Amount", "", 18, new Vector2(312f, -54f), new Vector2(145f, 22f), TextAlignmentOptions.MidlineRight, FontStyles.Normal, new Color32(106, 120, 146, 255), font);
        Slider(stats, "Transfer Slider", new Vector2(76f, -78f), new Vector2(300f, 14f), sprites);
        GradientButton(stats, "Transfer All Out Button", "0", new Vector2(384f, -70f), new Vector2(36f, 28f), sprites.collectButton, font, 16);
        GradientButton(stats, "Transfer All In Button", "MAX", new Vector2(426f, -70f), new Vector2(52f, 28f), sprites.primaryButton, font, 16);

        for (int i = 0; i < 5; i++)
        {
            RectTransform row = Card(outputs, "Output Row " + i, new Vector2(15f, -61f - i * 132f), new Vector2(484f, 120f), sprites.rounded10, new Color32(228, 230, 236, 255), false);
            Icon(row, "Resource Icon", new Vector2(8f, -27f), new Vector2(101f, 78f));
            Text(row, "Output Name", "", 24, new Vector2(119f, -16f), new Vector2(233f, 42f), TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color32(58, 66, 83, 255), font);
            Text(row, "Output Stored", "", 20, new Vector2(124f, -78f), new Vector2(150f, 24f), TextAlignmentOptions.Midline, FontStyles.Normal, new Color32(106, 120, 146, 255), font);
            Text(row, "Output Amount", "", 20, new Vector2(384f, -48f), new Vector2(75f, 24f), TextAlignmentOptions.MidlineRight, FontStyles.Normal, new Color32(106, 120, 146, 255), font);
            CircularProgress(row, new Vector2(372f, -10f), new Vector2(100f, 100f), sprites);
        }

        ScrollBar(outputs, new Vector2(508f, -61f), new Vector2(2f, 819f), sprites);
        GradientButton(outputs, "Collect Button", "Получить все", new Vector2(15f, -829f), new Vector2(484f, 50f), sprites.collectButton, font);
        Text(rootRect, "Processing Status", "", 20, new Vector2(588f, -820f), new Vector2(668f, 32f), TextAlignmentOptions.Midline, FontStyles.Normal, new Color32(106, 120, 146, 255), font);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rebuilt processing window prefab: " + PrefabPath);
    }

    private static TMP_FontAsset EnsureUbuntuFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null && IsFontAssetUsable(existing))
        {
            return existing;
        }

        if (existing != null)
        {
            AssetDatabase.DeleteAsset(FontAssetPath);
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(UbuntuRegularPath);
        if (font == null)
        {
            Debug.LogWarning("Ubuntu font is missing at " + UbuntuRegularPath + ". Falling back to TMP default font.");
            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        asset.name = "Ubuntu SDF";
        AssetDatabase.CreateAsset(asset, FontAssetPath);
        if (asset.material != null)
        {
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        Texture2D[] atlasTextures = asset.atlasTextures;
        for (int i = 0; atlasTextures != null && i < atlasTextures.Length; i++)
        {
            Texture2D atlasTexture = atlasTextures[i];
            if (atlasTexture == null)
            {
                continue;
            }

            atlasTexture.name = asset.name + " Atlas " + i;
            AssetDatabase.AddObjectToAsset(atlasTexture, asset);
        }

        EditorUtility.SetDirty(asset);
        if (asset.material != null)
        {
            EditorUtility.SetDirty(asset.material);
        }

        return asset;
    }

    private static bool IsFontAssetUsable(TMP_FontAsset asset)
    {
        if (asset == null || asset.material == null || asset.atlasTextures == null || asset.atlasTextures.Length == 0)
        {
            return false;
        }

        return asset.atlasTextures[0] != null;
    }

    private static ProcessingSprites EnsureSprites()
    {
        return new ProcessingSprites
        {
            rounded2 = EnsureRoundedSprite("Rounded_2", 96, 96, 2, new Color32(255, 255, 255, 255), new Vector4(4, 4, 4, 4)),
            rounded3 = EnsureRoundedSprite("Rounded_3", 96, 96, 3, new Color32(255, 255, 255, 255), new Vector4(6, 6, 6, 6)),
            rounded7 = EnsureRoundedSprite("Rounded_7", 96, 96, 7, new Color32(255, 255, 255, 255), new Vector4(14, 14, 14, 14)),
            rounded8 = EnsureRoundedSprite("Rounded_8", 96, 96, 8, new Color32(255, 255, 255, 255), new Vector4(16, 16, 16, 16)),
            rounded10 = EnsureRoundedSprite("Rounded_10", 128, 128, 10, new Color32(255, 255, 255, 255), new Vector4(20, 20, 20, 20)),
            rounded20 = EnsureRoundedSprite("Rounded_20", 192, 192, 20, new Color32(255, 255, 255, 255), new Vector4(40, 40, 40, 40)),
            primaryButton = EnsureGradientSprite("PrimaryButton", 256, 96, 8, new Color32(47, 59, 86, 255), new Color32(80, 104, 170, 255), new Vector4(16, 16, 16, 16)),
            collectButton = EnsureGradientSprite("CollectButton", 256, 96, 8, new Color32(166, 161, 130, 255), new Color32(195, 189, 164, 255), new Vector4(16, 16, 16, 16)),
            slot = EnsureGradientSprite("Slot", 256, 256, 10, new Color32(238, 244, 252, 255), new Color32(218, 228, 243, 255), new Vector4(20, 20, 20, 20)),
            circle = EnsureCircleSprite("Circle", 256)
        };
    }

    private static RectTransform Warehouse(RectTransform parent, string name, string title, Vector2 position, Vector2 size, ProcessingSprites sprites, TMP_FontAsset font)
    {
        RectTransform warehouse = Card(parent, name, position, size, sprites.rounded10, new Color32(233, 236, 242, 255), false);
        Card(warehouse, "Warehouse Header", new Vector2(1f, -1f), new Vector2(size.x - 2f, 44f), sprites.rounded10, new Color32(235, 237, 241, 255), false);
        Text(warehouse, "Warehouse Title", title, 24, new Vector2(13f, -8f), new Vector2(size.x - 26f, 34f), TextAlignmentOptions.MidlineLeft, FontStyles.Normal, new Color32(58, 66, 83, 255), font);
        Line(warehouse, "Warehouse Title Rule", new Vector2(13f, -45f), new Vector2(size.x - 26f, 1f), new Color32(184, 197, 212, 128));
        return warehouse;
    }

    private static RectTransform Card(RectTransform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Color color, bool preserveSpriteColor)
    {
        RectTransform rect = Rect(parent, name, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = preserveSpriteColor ? Color.white : color;
        image.raycastTarget = false;
        return rect;
    }

    private static RectTransform ButtonRect(RectTransform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Color color)
    {
        RectTransform rect = Rect(parent, name, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return rect;
    }

    private static Button GradientButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, Sprite sprite, TMP_FontAsset font, int fontSize = 20)
    {
        RectTransform rect = ButtonRect(parent, name, position, size, sprite, Color.white);
        TMP_Text text = Text(rect, "Label", label, fontSize, Vector2.zero, size, TextAlignmentOptions.Midline, FontStyles.Bold, new Color32(242, 244, 248, 255), font);
        text.raycastTarget = false;
        return rect.GetComponent<Button>();
    }

    private static RectTransform Rect(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text Text(RectTransform parent, string name, string value, int size, Vector2 position, Vector2 rectSize, TextAlignmentOptions alignment, FontStyles style, Color color, TMP_FontAsset font)
    {
        RectTransform rect = Rect(parent, name, position, rectSize);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static Image Icon(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(parent, name, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private static void Line(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = Rect(parent, name, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static void HeaderSeal(RectTransform parent, ProcessingSprites sprites)
    {
        RectTransform seal = Card(parent, "Processing Header Seal", new Vector2(10f, -13f), new Vector2(50f, 50f), sprites.rounded8, new Color32(33, 43, 64, 255), false);
        Card(seal, "Seal Shine", new Vector2(6f, -6f), new Vector2(38f, 16f), sprites.rounded8, new Color32(189, 199, 215, 54), false);
        RectTransform diamond = Card(seal, "Seal Diamond", new Vector2(13f, -13f), new Vector2(24f, 24f), sprites.rounded3, new Color32(206, 210, 225, 190), false);
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        RectTransform slashA = Rect(seal, "Seal Slash A", new Vector2(10f, -35f), new Vector2(30f, 2f));
        slashA.localRotation = Quaternion.Euler(0f, 0f, -18f);
        slashA.gameObject.AddComponent<Image>().color = new Color32(82, 93, 113, 255);
        RectTransform slashB = Rect(seal, "Seal Slash B", new Vector2(12f, -40f), new Vector2(16f, 2f));
        slashB.localRotation = Quaternion.Euler(0f, 0f, -18f);
        slashB.gameObject.AddComponent<Image>().color = new Color32(82, 93, 113, 210);
    }

    private static void ScrollBar(RectTransform parent, Vector2 position, Vector2 size, ProcessingSprites sprites)
    {
        RectTransform track = Card(parent, "Scroll Track", position, size, sprites.rounded2, new Color32(161, 169, 181, 102), false);
        Card(track, "Scroll Thumb", Vector2.zero, new Vector2(size.x, 50f), sprites.rounded2, new Color32(66, 109, 147, 255), false);
    }

    private static Slider Slider(RectTransform parent, string name, Vector2 position, Vector2 size, ProcessingSprites sprites)
    {
        RectTransform root = Rect(parent, name, position, size);
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = true;

        Card(root, "Background", Vector2.zero, size, sprites.rounded7, new Color32(216, 219, 224, 255), false);
        RectTransform fillArea = Rect(root, "Fill Area", new Vector2(2f, -2f), new Vector2(size.x - 4f, size.y - 4f));
        RectTransform fill = Card(fillArea, "Fill", Vector2.zero, fillArea.sizeDelta, sprites.primaryButton, Color.white, true);
        RectTransform handleArea = Rect(root, "Handle Slide Area", new Vector2(4f, 4f), new Vector2(size.x - 8f, size.y + 8f));
        RectTransform handle = Card(handleArea, "Handle", Vector2.zero, new Vector2(18f, 24f), sprites.rounded8, new Color32(58, 66, 83, 255), false);
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        return slider;
    }

    private static void CircularProgress(RectTransform parent, Vector2 position, Vector2 size, ProcessingSprites sprites)
    {
        RectTransform root = Rect(parent, "Circular Progress", position, size);
        Image background = root.gameObject.AddComponent<Image>();
        background.sprite = sprites.circle;
        background.color = new Color32(196, 200, 207, 255);
        background.raycastTarget = false;

        RectTransform fill = Rect(root, "Fill", Vector2.zero, size);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = sprites.circle;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
        fillImage.fillOrigin = (int)Image.Origin360.Top;
        fillImage.fillClockwise = true;
        fillImage.fillAmount = 0f;
        fillImage.color = new Color32(64, 86, 143, 255);

        RectTransform hole = Rect(root, "Hole", new Vector2(size.x * 0.14f, -size.y * 0.14f), size * 0.72f);
        Image holeImage = hole.gameObject.AddComponent<Image>();
        holeImage.sprite = sprites.circle;
        holeImage.color = new Color32(228, 230, 236, 255);
        holeImage.raycastTarget = false;
    }

    private static Sprite EnsureRoundedSprite(string name, int width, int height, float radius, Color color, Vector4 border)
    {
        return EnsureSprite(name, width, height, border, (x, y) =>
        {
            float alpha = RoundedRectCoverage(x + 0.5f, y + 0.5f, width, height, radius);
            Color result = color;
            result.a *= alpha;
            return result;
        });
    }

    private static Sprite EnsureGradientSprite(string name, int width, int height, float radius, Color left, Color right, Vector4 border)
    {
        return EnsureSprite(name, width, height, border, (x, y) =>
        {
            float alpha = RoundedRectCoverage(x + 0.5f, y + 0.5f, width, height, radius);
            Color result = Color.Lerp(left, right, width <= 1 ? 0f : x / (width - 1f));
            result.a *= alpha;
            return result;
        });
    }

    private static Sprite EnsureCircleSprite(string name, int size)
    {
        return EnsureSprite(name, size, size, Vector4.zero, (x, y) =>
        {
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) - (size * 0.5f - 1f);
            float alpha = Mathf.Clamp01(0.5f - distance);
            return new Color(1f, 1f, 1f, alpha);
        });
    }

    private static Sprite EnsureSprite(string name, int width, int height, Vector4 border, System.Func<float, float, Color> pixel)
    {
        string path = SpriteFolder + "/" + name + ".png";
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, pixel(x, y));
            }
        }

        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static float RoundedRectCoverage(float x, float y, float width, float height, float radius)
    {
        radius = Mathf.Max(0f, radius);
        if (radius <= 0.01f)
        {
            float xCoverage = Mathf.Min(x + 0.5f, width - x + 0.5f);
            float yCoverage = Mathf.Min(y + 0.5f, height - y + 0.5f);
            return Mathf.Clamp01(Mathf.Min(xCoverage, yCoverage));
        }

        float px = Mathf.Clamp(x, radius, width - radius);
        float py = Mathf.Clamp(y, radius, height - radius);
        float dx = x - px;
        float dy = y - py;
        float signedDistance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
        return Mathf.Clamp01(0.5f - signedDistance);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private sealed class ProcessingSprites
    {
        public Sprite rounded2;
        public Sprite rounded3;
        public Sprite rounded7;
        public Sprite rounded8;
        public Sprite rounded10;
        public Sprite rounded20;
        public Sprite primaryButton;
        public Sprite collectButton;
        public Sprite slot;
        public Sprite circle;
    }
}
