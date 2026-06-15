using System.Collections.Generic;
using UnityEngine;

public static class WildWindResourceIconCatalog
{
    public const string ResourcesRoot = "UI/ResourceIcons";
    public const string AssetFolder = "Assets/Resources/UI/ResourceIcons";

    private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

    public static string GetResourcePath(string itemId)
    {
        string normalized = NormalizeItemId(itemId);
        return string.IsNullOrWhiteSpace(normalized) ? ResourcesRoot + "/missing" : ResourcesRoot + "/" + normalized;
    }

    public static string GetAssetPathForTests(string itemId)
    {
        string normalized = NormalizeItemId(itemId);
        return string.IsNullOrWhiteSpace(normalized) ? "" : AssetFolder + "/" + normalized + ".png";
    }

    public static bool HasIconTexture(string itemId)
    {
        return Resources.Load<Texture2D>(GetResourcePath(itemId)) != null;
    }

    public static Sprite LoadSprite(string itemId)
    {
        string normalized = NormalizeItemId(itemId);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (SpriteCache.TryGetValue(normalized, out Sprite cached))
        {
            return cached;
        }

        Texture2D texture = Resources.Load<Texture2D>(GetResourcePath(normalized));
        if (texture == null)
        {
            return null;
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(texture.width, texture.height));
        sprite.name = "Icon_" + normalized;
        SpriteCache[normalized] = sprite;
        return sprite;
    }

    public static string NormalizeItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return "";
        }

        return itemId.Trim().ToLowerInvariant();
    }
}
