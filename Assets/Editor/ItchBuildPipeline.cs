using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Prepara el catálogo de recursos en runtime, configura PlayerSettings para Itch.io
/// y compila las builds de Windows (Win64) y WebGL (HTML5).
/// </summary>
public static class ItchBuildPipeline
{
    private static readonly string[] ScenePaths = new string[]
    {
        "Assets/Scenes/00_MenuIntro.unity",
        "Assets/Scenes/01_Nivel1_SantaFelipina.unity",
        "Assets/Scenes/02_Nivel2_Guarida.unity",
        "Assets/Scenes/Nivel_3.unity"
    };

    [MenuItem("Tools/Itch.io/1. Actualizar RuntimeAssetCatalog y Ajustes")]
    public static void PrepareProjectForItch()
    {
        PopulateRuntimeAssetCatalog();
        ConfigureProjectForItch();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ItchBuildPipeline] Proyecto preparado para compilación de Itch.io.");
    }

    [MenuItem("Tools/Itch.io/2. Compilar Windows + WebGL")]
    public static void BuildAllForItch()
    {
        PrepareProjectForItch();

        BuildWindowsInternal();
        BuildWebGLInternal();

        Debug.Log("[ItchBuildPipeline] Compilaciones Windows y WebGL completadas exitosamente.");
    }

    public static void BuildWindowsForItch()
    {
        PrepareProjectForItch();
        BuildWindowsInternal();
    }

    public static void BuildWebGLForItch()
    {
        PrepareProjectForItch();
        BuildWebGLInternal();
    }

    private static void BuildWindowsInternal()
    {
        string outputDir = Path.GetFullPath("Builds/Windows");
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }
        Directory.CreateDirectory(outputDir);

        string exePath = Path.Combine(outputDir, "VocesInternas.exe");
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = ScenePaths,
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"[ItchBuildPipeline] Falló la compilación Windows: {report.summary.result} (errores: {report.summary.totalErrors})");
        }

        Debug.Log($"[ItchBuildPipeline] Windows build OK en {exePath} ({report.summary.totalSize / (1024f * 1024f):F2} MB)");
    }

    private static void BuildWebGLInternal()
    {
        string outputDir = Path.GetFullPath("Builds/WebGL");
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }
        Directory.CreateDirectory(outputDir);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = ScenePaths,
            locationPathName = outputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"[ItchBuildPipeline] Falló la compilación WebGL: {report.summary.result} (errores: {report.summary.totalErrors})");
        }

        Debug.Log($"[ItchBuildPipeline] WebGL build OK en {outputDir} ({report.summary.totalSize / (1024f * 1024f):F2} MB)");
    }

    public static void ConfigureProjectForItch()
    {
        PlayerSettings.productName = "VocesInternas";
        PlayerSettings.runInBackground = true;

        // Escritorio (Windows)
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.allowFullscreenSwitch = true;
        PlayerSettings.resizableWindow = true;

        // WebGL (Itch.io)
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.threadsSupport = false;

        if (Directory.Exists("Assets/WebGLTemplates/ItchIO"))
        {
            PlayerSettings.WebGL.template = "PROJECT:ItchIO";
        }
        else
        {
            PlayerSettings.WebGL.template = "APPLICATION:Default";
        }

        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
        foreach (string scenePath in ScenePaths)
        {
            if (File.Exists(scenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            else
            {
                Debug.LogError($"[ItchBuildPipeline] Escena faltante en BuildSettings: {scenePath}");
            }
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();
    }

    public static void PopulateRuntimeAssetCatalog()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        const string catalogPath = "Assets/Resources/RuntimeAssetCatalog.asset";
        RuntimeAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<RuntimeAssetCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }

        catalog.ClearAndResetCache();

        // 1. AudioClips (Music + Sound)
        RegisterAssetsInFolder<AudioClip>(catalog, "Assets/Music");
        RegisterAssetsInFolder<AudioClip>(catalog, "Assets/Sound");

        // 2. Items, Recetas y Enemigos
        RegisterAssetsInFolder<ItemData>(catalog, "Assets/Data/Items");
        RegisterAssetsInFolder<CraftingRecipeData>(catalog, "Assets/Data/Items");
        RegisterAssetsInFolder<EnemyData>(catalog, "Assets/Data/Enemies");

        // 3. Controladores de animación de enemigos
        RegisterAssetsInFolder<RuntimeAnimatorController>(catalog, "Assets/Animations/Homeless_2");
        RegisterAssetsInFolder<RuntimeAnimatorController>(catalog, "Assets/Animations/Homeless_3");
        RegisterAssetsInFolder<RuntimeAnimatorController>(catalog, "Assets/Animations/Enemy_Punk");

        // 4. Fuentes
        RegisterAsset<TMP_FontAsset>(catalog, "Assets/Fonts/Pixelzone_TMP.asset");
        RegisterAsset<Font>(catalog, "Assets/Fonts/Pixelzone_Font_1_10/Pixelzone.ttf");

        // 5. Retratos y Sprites individuales usados por código en runtime
        RegisterSpritesInFolder(catalog, "Assets/Portraits");
        RegisterSpritesInFolder(catalog, "Assets/Sprites/Detective");
        RegisterSpritesInFolder(catalog, "Assets/Sprites/Personaje");
        RegisterSpritesInFolder(catalog, "Assets/Sprites/Player - Cardo/Idle");
        RegisterSprite(catalog, "Assets/Sprites/PlaceholderSquare.png");

        // Nivel 2 & Nivel 3 sprites cargados por ruta
        RegisterSprite(catalog, "Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Trash-can_1.png");
        RegisterSprite(catalog, "Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Trash-can_2.png");
        RegisterSprite(catalog, "Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Trash-bag_1.png");
        RegisterSprite(catalog, "Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Trash-bag_2.png");
        RegisterSprite(catalog, "Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Barrel_red_1.png");
        RegisterSprite(catalog, "Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Ammo-crate_Blue.png");
        RegisterSprite(catalog, "Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Ammo-crate_Green.png");
        RegisterSprite(catalog, "Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Ammo-crate_Red.png");

        RegisterSprite(catalog, "Assets/NewStuff/gothicvania church files/Assets/ENVIRONMENT/column.png");
        RegisterSprite(catalog, "Assets/NewStuff/torch_1.png");
        RegisterSprite(catalog, "Assets/NewStuff/torch_2.png");
        RegisterSprite(catalog, "Assets/NewStuff/torch_3.png");
        RegisterSprite(catalog, "Assets/NewStuff/torch_4.png");
        RegisterSpritesInFolder(catalog, "Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk");
        RegisterSpritesInFolder(catalog, "Assets/NewStuff/gothicvania church files/Assets/SPRITES/player/Idle/sprites");
        RegisterSpritesInFolder(catalog, "Assets/NewStuff/gothicvania church files/Assets/SPRITES/player/Walk/sprites");
        RegisterSpritesInFolder(catalog, "Assets/NewStuff/gothicvania church files/Assets/SPRITES/player/Punch/sprites");
        RegisterSpritesInFolder(catalog, "Assets/NewStuff/gothicvania church files/Assets/SPRITES/player/Kick/sprites");
        RegisterSpritesInFolder(catalog, "Assets/NewStuff/gothicvania church files/Assets/SPRITES/player/Flying Kick/sprites");
        RegisterSpritesInFolder(catalog, "Assets/NewStuff/gothicvania church files/Assets/SPRITES/player/crouch-kick/sprites");
        RegisterSpritesInFolder(catalog, "Assets/NewStuff/gothicvania church files/Assets/SPRITES/player/Hurt/sprites");

        // 6. Sprite Sheets (LoadAllAssetsAtPath)
        RegisterSpriteSheet(catalog, "Assets/Scripts/AnimacionAtaque.png");
        RegisterSpriteSheet(catalog, "Assets/Scripts/AtaqueExplosion.png");
        RegisterSpriteSheetsInFolder(catalog, "Assets/Sprites/Homeless_2");
        RegisterSpriteSheetsInFolder(catalog, "Assets/Sprites/Homeless_3");

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("[ItchBuildPipeline] RuntimeAssetCatalog actualizado correctamente.");
    }

    private static void RegisterAsset<T>(RuntimeAssetCatalog catalog, string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            catalog.RegisterAsset(path, asset);
        }
        else
        {
            Debug.LogWarning($"[ItchBuildPipeline] No se encontró {typeof(T).Name} en: {path}");
        }
    }

    private static void RegisterAssetsInFolder<T>(RuntimeAssetCatalog catalog, string folderPath) where T : Object
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) return;

        string filter = typeof(T) == typeof(AudioClip) ? "t:AudioClip" :
                        typeof(T) == typeof(RuntimeAnimatorController) ? "t:RuntimeAnimatorController" :
                        $"t:{typeof(T).Name}";

        string[] guids = AssetDatabase.FindAssets(filter, new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                catalog.RegisterAsset(path, asset);
            }
        }
    }

    private static void RegisterSprite(RuntimeAssetCatalog catalog, string path)
    {
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] is Sprite candidate)
                    {
                        sp = candidate;
                        break;
                    }
                }
            }
        }

        if (sp != null)
        {
            catalog.RegisterAsset(path, sp);
        }
        else
        {
            Debug.LogWarning($"[ItchBuildPipeline] No se encontró Sprite en: {path}");
        }
    }

    private static void RegisterSpritesInFolder(RuntimeAssetCatalog catalog, string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RegisterSprite(catalog, path);
        }
    }

    private static void RegisterSpriteSheet(RuntimeAssetCatalog catalog, string path)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all == null) return;

        List<Sprite> sprites = new List<Sprite>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is Sprite s)
            {
                sprites.Add(s);
            }
        }

        if (sprites.Count > 0)
        {
            catalog.RegisterSpriteSheet(path, sprites.ToArray());
            catalog.RegisterAsset(path, sprites[0]);
        }
        else
        {
            Debug.LogWarning($"[ItchBuildPipeline] No se encontraron sprites en sheet: {path}");
        }
    }

    private static void RegisterSpriteSheetsInFolder(RuntimeAssetCatalog catalog, string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RegisterSpriteSheet(catalog, path);
        }
    }
}
