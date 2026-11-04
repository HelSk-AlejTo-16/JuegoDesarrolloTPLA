#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de Editor para configurar:
/// 1. Los AudioImporters de las pistas de musica, prefab MusicManager y escenas del juego.
/// 2. Las 3 fotos de la introduccion (Bicicleta 1, 2 y 3) con nitidez pixel-art (Point, Uncompressed),
///    aspect ratio preservado y asignacion a las diapositivas de la escena 00_MenuIntro.
/// </summary>
public static class MusicSetupUtility
{
    private const string ChillMenuPath = "Assets/Music/ChillMenu_Loopable.wav";
    private const string CrashingOutPath = "Assets/Music/CrashingOut_Loopable.wav";
    private const string CriticalThemePath = "Assets/Music/CriticalTheme_Loopable.wav";

    private const string PrefabDir = "Assets/Prefabs";
    private const string ResourcesDir = "Assets/Resources";
    private const string PrefabPath = "Assets/Prefabs/MusicManager.prefab";
    private const string ResourcesPrefabPath = "Assets/Resources/MusicManager.prefab";

    private static readonly string[] GameScenes = new string[]
    {
        "Assets/Scenes/00_MenuIntro.unity",
        "Assets/Scenes/01_Nivel1_SantaFelipina.unity",
        "Assets/Scenes/02_Nivel2_Guarida.unity"
    };

    private static readonly string[] FondosPaths = new string[]
    {
        "Assets/Fondos/Bicicleta1.png",
        "Assets/Fondos/Bicicleta2.png",
        "Assets/Fondos/Bicicleta3.png"
    };

    [MenuItem("RPG Santa Felipina/Configurar Todo (Musica y Fotos)")]
    public static void SetupAll()
    {
        SetupPhotosSystemFull();
        SetupMusicSystemFull();
    }

    [MenuItem("RPG Santa Felipina/Configurar Fotos de Introduccion")]
    public static void SetupPhotosSystemFull()
    {
        Debug.Log("[PhotoSetupUtility] === Iniciando configuracion de fotos de introduccion ===");

        // 1. Asegurar carpeta Assets/Fotos sincronizada
        EnsureFotosFolderAndSync();

        // 2. Configurar TextureImporters con nitidez Pixel Art (Point Filter, Uncompressed, Single Sprite)
        foreach (string path in FondosPaths)
        {
            ConfigureTextureImporter(path);
        }

        if (Directory.Exists("Assets/Fotos"))
        {
            string[] fotosInDir = Directory.GetFiles("Assets/Fotos", "*.png");
            foreach (string p in fotosInDir)
            {
                string unityPath = p.Replace('\\', '/');
                ConfigureTextureImporter(unityPath);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Cargar los 3 Sprites
        Sprite sprite1 = LoadSprite(FondosPaths[0]);
        Sprite sprite2 = LoadSprite(FondosPaths[1]);
        Sprite sprite3 = LoadSprite(FondosPaths[2]);

        if (sprite1 == null || sprite2 == null || sprite3 == null)
        {
            Debug.LogError($"[PhotoSetupUtility] Error al cargar sprites: 1: {sprite1 != null}, 2: {sprite2 != null}, 3: {sprite3 != null}");
            return;
        }

        Debug.Log($"[PhotoSetupUtility] Sprites cargados con exito: Foto1={sprite1.name} ({sprite1.rect.width}x{sprite1.rect.height}), Foto2={sprite2.name} ({sprite2.rect.width}x{sprite2.rect.height}), Foto3={sprite3.name} ({sprite3.rect.width}x{sprite3.rect.height})");

        // 4. Abrir la escena 00_MenuIntro
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/00_MenuIntro.unity", OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[PhotoSetupUtility] No se pudo abrir la escena 00_MenuIntro.unity");
            return;
        }

        // 5. Configurar IntroSlideshowManager
        IntroSlideshowManager introManager = Object.FindAnyObjectByType<IntroSlideshowManager>();
        if (introManager != null)
        {
            Undo.RecordObject(introManager, "Asignar fotos de introduccion");
            introManager.photo1 = sprite1;
            introManager.photo2 = sprite2;
            introManager.photo3 = sprite3;

            if (introManager.slides != null)
            {
                if (introManager.slides.Length > 0) introManager.slides[0].slidePhoto = sprite1;
                if (introManager.slides.Length > 1) introManager.slides[1].slidePhoto = sprite1;
                if (introManager.slides.Length > 2) introManager.slides[2].slidePhoto = sprite2;

                for (int i = 3; i < introManager.slides.Length; i++)
                {
                    introManager.slides[i].slidePhoto = sprite3;
                }
            }
            EditorUtility.SetDirty(introManager);
            Debug.Log("[PhotoSetupUtility] Fotos asignadas a IntroSlideshowManager y a cada SlideData.");
        }

        // 6. Ajustar PhotoPlaceholderImage y marco PhotoBorderFrame
        AdjustPhotoUI(introManager, sprite1);

        // 7. Guardar escena
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PhotoSetupUtility] Escena 00_MenuIntro guardada con exito.");
    }

    private static void EnsureFotosFolderAndSync()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Fotos"))
        {
            AssetDatabase.CreateFolder("Assets", "Fotos");
        }

        string[] targetNames = new string[] { "Bicicleta1.png", "Bicicleta2.png", "Bicicleta3.png" };
        for (int i = 0; i < FondosPaths.Length; i++)
        {
            string source = FondosPaths[i];
            string dest = $"Assets/Fotos/{targetNames[i]}";
            if (File.Exists(source) && !File.Exists(dest))
            {
                AssetDatabase.CopyAsset(source, dest);
            }
        }
    }

    private static void ConfigureTextureImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[PhotoSetupUtility] TextureImporter actualizado para nitidez pixel-art: {path}");
        }
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            s = subAssets.OfType<Sprite>().FirstOrDefault();
        }
        return s;
    }

    private static void AdjustPhotoUI(IntroSlideshowManager introManager, Sprite defaultSprite)
    {
        if (introManager != null && introManager.photoPlaceholderImage != null)
        {
            Undo.RecordObject(introManager.photoPlaceholderImage, "Ajustar imagen de foto");
            introManager.photoPlaceholderImage.sprite = defaultSprite;
            introManager.photoPlaceholderImage.color = Color.white;
            introManager.photoPlaceholderImage.preserveAspect = true;
            EditorUtility.SetDirty(introManager.photoPlaceholderImage);
        }

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) return;

        Transform slideshowPanel = canvas.transform.Find("SlideshowPanel");
        if (slideshowPanel == null) return;

        Transform photoBorderFrame = slideshowPanel.Find("PhotoBorderFrame");
        if (photoBorderFrame != null)
        {
            Undo.RecordObject(photoBorderFrame, "Ajustar marco de foto");

            Image borderImage = photoBorderFrame.GetComponent<Image>();
            if (borderImage != null)
            {
                borderImage.color = new Color(0.06f, 0.07f, 0.09f, 1f);
            }

            Outline outline = photoBorderFrame.GetComponent<Outline>();
            if (outline == null)
            {
                outline = photoBorderFrame.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(0.35f, 0.36f, 0.42f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            EditorUtility.SetDirty(photoBorderFrame);
        }

        if (introManager != null && introManager.photoLabelText != null)
        {
            Undo.RecordObject(introManager.photoLabelText.gameObject, "Ocultar placeholder label");
            introManager.photoLabelText.gameObject.SetActive(false);
            EditorUtility.SetDirty(introManager.photoLabelText.gameObject);
        }
    }

    [MenuItem("RPG Santa Felipina/Configurar Musica y Audio Manager")]
    public static void SetupMusicSystemFull()
    {
        Debug.Log("[MusicSetupUtility] === Iniciando configuración del sistema de música y efectos de sonido (Assets/Sound) ===");

        // 0. Configurar AudioImporters en Assets/Sound (2D y Preload Audio Data)
        ConfigureSoundEffectImporters();

        // 1. Cargar los AudioClips directamente desde Assets/Music/
        AudioClip menuClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ChillMenuPath);
        AudioClip explorationClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CrashingOutPath);
        AudioClip battleClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CriticalThemePath);

        if (menuClip == null || explorationClip == null || battleClip == null)
        {
            Debug.LogError($"[MusicSetupUtility] No se pudieron cargar todos los clips de música. Menu: {menuClip != null}, Exploration: {explorationClip != null}, Battle: {battleClip != null}");
            return;
        }

        // 1B. Cargar todos los efectos de sonido y pistas adicionales desde Assets/Sound/
        AudioClip victoryMenuClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathMusicVictoryMenu);
        AudioClip gameOverClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathMusicGameOver);
        AudioClip sfxAtaqueAliado = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxAtaqueAliadoPropio);
        AudioClip sfxAtaqueEnemigo = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxAtaqueEnemigo);
        AudioClip sfxAtaqueJefe = MusicManager.LoadFirstAudioClipInFolderEditor(MusicManager.FolderSfxAtaqueJefe);
        AudioClip sfxBotonCombate = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxBotonEnCombate);
        AudioClip sfxBotonDialogo = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxBotonConfirmacionDialogo);
        AudioClip sfxCurarse = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxCurarse);
        AudioClip sfxEfecto = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxEfecto);
        AudioClip sfxEsquive = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxEsquive);
        AudioClip sfxFuego = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxFuego);
        AudioClip sfxDanoFinalMuerte = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxDanoFinalMuerte);
        AudioClip sfxEncuentro = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxEncuentro);
        AudioClip sfxPasos = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxPasos);
        AudioClip sfxSubidaDeNivel = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxSubidaDeNivel);
        AudioClip sfxMuerteEnemigo = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicManager.PathSfxMuerteEnemigo);

        Debug.Log($"[MusicSetupUtility] SFX cargados -> Aliado:{sfxAtaqueAliado != null}, Enemigo:{sfxAtaqueEnemigo != null}, Jefe:{sfxAtaqueJefe != null} (fallback si vacio), BotonCombate:{sfxBotonCombate != null}, BotonDialogo:{sfxBotonDialogo != null}, Curarse:{sfxCurarse != null}, Efecto:{sfxEfecto != null}, Esquive:{sfxEsquive != null}, Fuego(Molotov):{sfxFuego != null}, VictoryMenu:{victoryMenuClip != null}, GameOver:{gameOverClip != null}, DanoFinal:{sfxDanoFinalMuerte != null}, Encuentro:{sfxEncuentro != null}, Pasos:{sfxPasos != null}, SubidaNivel:{sfxSubidaDeNivel != null}, MuerteEnemigo:{sfxMuerteEnemigo != null}");

        // 2. Crear directorios Prefabs y Resources si no existen
        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(ResourcesDir))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // 3. Crear el GameObject base para el Prefab
        GameObject tempGO = new GameObject("MusicManager");
        MusicManager mm = tempGO.AddComponent<MusicManager>();
        mm.menuMusic = menuClip;
        mm.explorationMusic = explorationClip;
        mm.battleMusic = battleClip;
        mm.victoryMenuMusic = victoryMenuClip;
        mm.gameOverMusic = gameOverClip;
        mm.masterVolume = 0.8f;
        mm.sfxVolume = 0.90f;
        mm.crossfadeDuration = 0.8f;
        mm.autoHandleScenes = true;
        mm.menuSceneKeywords = new string[] { "00_MenuIntro", "Menu", "Intro" };

        mm.sfxAtaqueAliadoPropio = sfxAtaqueAliado;
        mm.sfxAtaqueEnemigo = sfxAtaqueEnemigo;
        mm.sfxAtaqueJefe = sfxAtaqueJefe;
        mm.sfxBotonEnCombate = sfxBotonCombate;
        mm.sfxBotonConfirmacionDialogo = sfxBotonDialogo;
        mm.sfxCurarse = sfxCurarse;
        mm.sfxEfecto = sfxEfecto;
        mm.sfxEsquive = sfxEsquive;
        mm.sfxFuego = sfxFuego;
        mm.sfxDanoFinalMuerte = sfxDanoFinalMuerte;
        mm.sfxEncuentro = sfxEncuentro;
        mm.sfxPasos = sfxPasos;
        mm.sfxSubidaDeNivel = sfxSubidaDeNivel;
        mm.sfxMuerteEnemigo = sfxMuerteEnemigo;

        AudioSource sourceA = tempGO.AddComponent<AudioSource>();
        sourceA.clip = menuClip;
        sourceA.loop = true;
        sourceA.playOnAwake = true;
        sourceA.spatialBlend = 0f;
        sourceA.priority = 0;
        sourceA.volume = 0.8f;

        AudioSource sourceB = tempGO.AddComponent<AudioSource>();
        sourceB.clip = null;
        sourceB.loop = true;
        sourceB.playOnAwake = false;
        sourceB.spatialBlend = 0f;
        sourceB.priority = 0;
        sourceB.volume = 0f;

        AudioSource sourceSfx = tempGO.AddComponent<AudioSource>();
        sourceSfx.clip = null;
        sourceSfx.loop = false;
        sourceSfx.playOnAwake = false;
        sourceSfx.spatialBlend = 0f;
        sourceSfx.priority = 64;
        sourceSfx.volume = 1f;

        // Guardar prefab en Assets/Prefabs y Assets/Resources
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(tempGO, PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(tempGO, ResourcesPrefabPath);
        Object.DestroyImmediate(tempGO);

        Debug.Log($"[MusicSetupUtility] Prefab MusicManager creado en '{PrefabPath}' y '{ResourcesPrefabPath}'.");

        // 4. Configurar MusicManager, BattleManager y gestores de diálogo en cada escena
        string currentActiveScenePath = SceneManager.GetActiveScene().path;

        foreach (string scenePath in GameScenes)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[MusicSetupUtility] Escena '{scenePath}' no encontrada en disco.");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            MusicManager sceneMM = Object.FindAnyObjectByType<MusicManager>();

            if (sceneMM == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
                instance.name = "MusicManager";
                sceneMM = instance.GetComponent<MusicManager>();
            }

            sceneMM.menuMusic = menuClip;
            sceneMM.explorationMusic = explorationClip;
            sceneMM.battleMusic = battleClip;
            sceneMM.victoryMenuMusic = victoryMenuClip;
            sceneMM.gameOverMusic = gameOverClip;
            sceneMM.masterVolume = 0.8f;
            sceneMM.sfxVolume = 0.90f;
            sceneMM.crossfadeDuration = 0.8f;
            sceneMM.autoHandleScenes = true;

            sceneMM.sfxAtaqueAliadoPropio = sfxAtaqueAliado;
            sceneMM.sfxAtaqueEnemigo = sfxAtaqueEnemigo;
            sceneMM.sfxAtaqueJefe = sfxAtaqueJefe;
            sceneMM.sfxBotonEnCombate = sfxBotonCombate;
            sceneMM.sfxBotonConfirmacionDialogo = sfxBotonDialogo;
            sceneMM.sfxCurarse = sfxCurarse;
            sceneMM.sfxEfecto = sfxEfecto;
            sceneMM.sfxEsquive = sfxEsquive;
            sceneMM.sfxFuego = sfxFuego;
            sceneMM.sfxDanoFinalMuerte = sfxDanoFinalMuerte;
            sceneMM.sfxEncuentro = sfxEncuentro;
            sceneMM.sfxPasos = sfxPasos;
            sceneMM.sfxSubidaDeNivel = sfxSubidaDeNivel;
            sceneMM.sfxMuerteEnemigo = sfxMuerteEnemigo;

            AudioSource[] sources = sceneMM.GetComponents<AudioSource>();
            if (sources.Length > 0)
            {
                if (scenePath.Contains("00_MenuIntro"))
                {
                    sources[0].clip = menuClip;
                    sources[0].volume = 0.8f;
                    sources[0].loop = true;
                    sources[0].playOnAwake = true;
                    sources[0].spatialBlend = 0f;
                }
                else
                {
                    sources[0].clip = explorationClip;
                    sources[0].volume = 0.8f;
                    sources[0].loop = true;
                    sources[0].playOnAwake = true;
                    sources[0].spatialBlend = 0f;
                }
            }

            if (scenePath.Contains("00_MenuIntro"))
            {
                IntroSlideshowManager introManager = Object.FindAnyObjectByType<IntroSlideshowManager>();
                if (introManager != null)
                {
                    introManager.menuMusicClip = menuClip;
                    introManager.dialogueConfirmSfx = sfxBotonDialogo;
                    EditorUtility.SetDirty(introManager);
                }
            }

            BattleManager battleManager = Object.FindAnyObjectByType<BattleManager>(FindObjectsInactive.Include);
            if (battleManager != null)
            {
                battleManager.sfxAtaqueAliadoPropio = sfxAtaqueAliado;
                battleManager.sfxAtaqueEnemigo = sfxAtaqueEnemigo;
                battleManager.sfxAtaqueJefe = sfxAtaqueJefe;
                battleManager.sfxBotonEnCombate = sfxBotonCombate;
                battleManager.sfxBotonConfirmacionDialogo = sfxBotonDialogo;
                battleManager.sfxCurarse = sfxCurarse;
                battleManager.sfxEfecto = sfxEfecto;
                battleManager.sfxEsquive = sfxEsquive;
                battleManager.sfxFuego = sfxFuego;
                battleManager.victoryMenuMusic = victoryMenuClip;
                battleManager.gameOverMusic = gameOverClip;
                battleManager.sfxDanoFinalMuerte = sfxDanoFinalMuerte;
                battleManager.sfxEncuentro = sfxEncuentro;
                battleManager.sfxSubidaDeNivel = sfxSubidaDeNivel;
                battleManager.sfxMuerteEnemigo = sfxMuerteEnemigo;
                EditorUtility.SetDirty(battleManager);
            }

            PlayerController2D playerController = Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);
            if (playerController != null)
            {
                playerController.footstepSfx = sfxPasos;
                EditorUtility.SetDirty(playerController);
            }

            ExplorationDialogueManager expDialogue = Object.FindAnyObjectByType<ExplorationDialogueManager>(FindObjectsInactive.Include);
            if (expDialogue != null)
            {
                expDialogue.dialogueConfirmSfx = sfxBotonDialogo;
                EditorUtility.SetDirty(expDialogue);
            }

            EditorUtility.SetDirty(sceneMM);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MusicSetupUtility] Escena '{scenePath}' guardada con éxito.");
        }

        if (!string.IsNullOrEmpty(currentActiveScenePath) && File.Exists(currentActiveScenePath))
        {
            EditorSceneManager.OpenScene(currentActiveScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[MusicSetupUtility] === ¡Configuración de Música y Efectos de Sonido completada con éxito! ===");
    }

    private static void ConfigureSoundEffectImporters()
    {
        AssetDatabase.Refresh();
        if (!AssetDatabase.IsValidFolder("Assets/Sound"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sound" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            bool changed = false;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (!settings.preloadAudioData || settings.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                settings.preloadAudioData = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings = settings;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }
    }

    [MenuItem("RPG Santa Felipina/Verificar Musica y Transiciones")]
    public static void VerifyMusicAndTransitions()
    {
        Debug.Log("[MusicVerification] === Verificando Sistema de Música ===");
        AudioClip menu = AssetDatabase.LoadAssetAtPath<AudioClip>(ChillMenuPath);
        AudioClip exploration = AssetDatabase.LoadAssetAtPath<AudioClip>(CrashingOutPath);
        AudioClip battle = AssetDatabase.LoadAssetAtPath<AudioClip>(CriticalThemePath);

        Debug.Log($"[MusicVerification] 1. Clips en disco: Menu={menu != null} ({menu?.name}), Exploration={exploration != null} ({exploration?.name}), Battle={battle != null} ({battle?.name})");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        MusicManager prefabMM = prefab != null ? prefab.GetComponent<MusicManager>() : null;
        Debug.Log($"[MusicVerification] 2. Prefab: Existe={prefabMM != null}, MenuClip={prefabMM?.menuMusic != null}, ExploClip={prefabMM?.explorationMusic != null}, BattleClip={prefabMM?.battleMusic != null}");

        foreach (string path in GameScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            MusicManager mm = Object.FindAnyObjectByType<MusicManager>();
            bool hasIntroManagerClip = false;
            if (path.Contains("00_MenuIntro"))
            {
                IntroSlideshowManager intro = Object.FindAnyObjectByType<IntroSlideshowManager>();
                hasIntroManagerClip = (intro != null && intro.menuMusicClip != null);
            }
            Debug.Log($"[MusicVerification] 3. Escena '{path}': MusicManager={mm != null}, MenuClip={mm?.menuMusic != null}, IntroSlideshowClip={hasIntroManagerClip}");
        }
        Debug.Log("[MusicVerification] === ¡Verificación Exitosa! ===");
    }
}
#endif
