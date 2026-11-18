using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Catálogo serializado en Resources para resolver assets por su ruta de proyecto en builds
/// (Windows Standalone y WebGL) sin depender de UnityEditor.AssetDatabase ni de System.IO.File.
/// </summary>
public class RuntimeAssetCatalog : ScriptableObject
{
    public const string ResourceName = "RuntimeAssetCatalog";

    [Serializable]
    public class AssetEntry
    {
        public string path;
        public Object asset;
    }

    [Serializable]
    public class SpriteSheetEntry
    {
        public string path;
        public Sprite[] sprites;
    }

    [SerializeField] private List<AssetEntry> assets = new List<AssetEntry>();
    [SerializeField] private List<SpriteSheetEntry> spriteSheets = new List<SpriteSheetEntry>();

    private static RuntimeAssetCatalog s_instance;
    private Dictionary<string, Object> _assetLookup;
    private Dictionary<string, Sprite[]> _sheetLookup;

    public static RuntimeAssetCatalog Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = Resources.Load<RuntimeAssetCatalog>(ResourceName);
            }
            return s_instance;
        }
    }

    public static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').Trim();
    }

    private void EnsureLookupInitialized()
    {
        if (_assetLookup == null)
        {
            _assetLookup = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
            if (assets != null)
            {
                for (int i = 0; i < assets.Count; i++)
                {
                    AssetEntry entry = assets[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.path) && entry.asset != null)
                    {
                        _assetLookup[NormalizePath(entry.path)] = entry.asset;
                    }
                }
            }
        }

        if (_sheetLookup == null)
        {
            _sheetLookup = new Dictionary<string, Sprite[]>(StringComparer.OrdinalIgnoreCase);
            if (spriteSheets != null)
            {
                for (int i = 0; i < spriteSheets.Count; i++)
                {
                    SpriteSheetEntry entry = spriteSheets[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.path) && entry.sprites != null && entry.sprites.Length > 0)
                    {
                        _sheetLookup[NormalizePath(entry.path)] = entry.sprites;
                    }
                }
            }
        }
    }

    public void ClearAndResetCache()
    {
        assets.Clear();
        spriteSheets.Clear();
        _assetLookup = null;
        _sheetLookup = null;
    }

    public void RegisterAsset(string path, Object asset)
    {
        string normalized = NormalizePath(path);
        if (string.IsNullOrEmpty(normalized) || asset == null) return;

        for (int i = 0; i < assets.Count; i++)
        {
            if (string.Equals(NormalizePath(assets[i].path), normalized, StringComparison.OrdinalIgnoreCase))
            {
                assets[i].path = normalized;
                assets[i].asset = asset;
                _assetLookup = null;
                return;
            }
        }

        assets.Add(new AssetEntry { path = normalized, asset = asset });
        _assetLookup = null;
    }

    public void RegisterSpriteSheet(string path, Sprite[] sprites)
    {
        string normalized = NormalizePath(path);
        if (string.IsNullOrEmpty(normalized) || sprites == null || sprites.Length == 0) return;

        for (int i = 0; i < spriteSheets.Count; i++)
        {
            if (string.Equals(NormalizePath(spriteSheets[i].path), normalized, StringComparison.OrdinalIgnoreCase))
            {
                spriteSheets[i].path = normalized;
                spriteSheets[i].sprites = sprites;
                _sheetLookup = null;
                return;
            }
        }

        spriteSheets.Add(new SpriteSheetEntry { path = normalized, sprites = sprites });
        _sheetLookup = null;
    }

    public static T GetAsset<T>(string path) where T : Object
    {
        string normalized = NormalizePath(path);
        if (string.IsNullOrEmpty(normalized)) return null;

        RuntimeAssetCatalog catalog = Instance;
        if (catalog != null)
        {
            catalog.EnsureLookupInitialized();
            if (catalog._assetLookup.TryGetValue(normalized, out Object found) && found is T typed)
            {
                return typed;
            }
        }

#if UNITY_EDITOR
        T editorAsset = AssetDatabase.LoadAssetAtPath<T>(normalized);
        if (editorAsset != null) return editorAsset;
#endif
        return null;
    }

    public static Sprite GetSprite(string path)
    {
        Sprite direct = GetAsset<Sprite>(path);
        if (direct != null) return direct;

        Sprite[] sheet = GetSprites(path);
        if (sheet != null && sheet.Length > 0) return sheet[0];

#if UNITY_EDITOR
        string normalized = NormalizePath(path);
        if (!string.IsNullOrEmpty(normalized))
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(normalized);
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] is Sprite s) return s;
                }
            }
        }
#endif
        return null;
    }

    public static Sprite[] GetSprites(string path)
    {
        string normalized = NormalizePath(path);
        if (string.IsNullOrEmpty(normalized)) return null;

        RuntimeAssetCatalog catalog = Instance;
        if (catalog != null)
        {
            catalog.EnsureLookupInitialized();
            if (catalog._sheetLookup.TryGetValue(normalized, out Sprite[] found) && found != null && found.Length > 0)
            {
                return found;
            }
        }

#if UNITY_EDITOR
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(normalized);
        if (all != null && all.Length > 0)
        {
            List<Sprite> sprites = new List<Sprite>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is Sprite s) sprites.Add(s);
            }
            if (sprites.Count > 0) return sprites.ToArray();
        }
#endif
        return null;
    }

    public static AudioClip GetAudioClip(string path) => GetAsset<AudioClip>(path);
    public static ItemData GetItem(string path) => GetAsset<ItemData>(path);
    public static CraftingRecipeData GetRecipe(string path) => GetAsset<CraftingRecipeData>(path);
    public static EnemyData GetEnemy(string path) => GetAsset<EnemyData>(path);
    public static RuntimeAnimatorController GetAnimatorController(string path) => GetAsset<RuntimeAnimatorController>(path);
    public static Font GetFont(string path) => GetAsset<Font>(path);
    public static TMP_FontAsset GetTMPFont(string path) => GetAsset<TMP_FontAsset>(path);
}
