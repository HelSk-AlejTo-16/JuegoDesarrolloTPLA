#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Constructor y actualizador de la Escena 00 (00_MenuIntro.unity).
/// Configura los 4 botones principales del menú:
/// 1. Comenzar la Aventura
/// 2. Configuración del Sistema (Volumen BGM y SFX con sliders y persistencia)
/// 3. Créditos (Ventana modal con información y agradecimientos)
/// 4. Salir del Juego
/// Mantiene la coherencia visual Noir y paleta de colores de las 4 personalidades de Enrique.
/// </summary>
public static class MainMenuSceneBuilder
{
    private const string MenuScenePath = "Assets/Scenes/00_MenuIntro.unity";

    private static readonly Color PureBlack = new Color(0.02f, 0.02f, 0.025f, 1f);
    private static readonly Color MatteBlack = new Color(0.05f, 0.05f, 0.06f, 1f);
    private static readonly Color SubtleBorder = new Color(0.26f, 0.26f, 0.30f, 1f);
    private static readonly Color BoneWhite = new Color(0.92f, 0.90f, 0.86f, 1f);
    private static readonly Color MutedGray = new Color(0.65f, 0.66f, 0.70f, 1f);

    private static readonly Color AccentAmberGold = new Color(0.88f, 0.60f, 0.20f, 1f); // Motricidad / Aventura
    private static readonly Color AccentIntelectoBlue = new Color(0.20f, 0.56f, 0.88f, 1f); // Intelecto / Configuración
    private static readonly Color AccentMundoViolet = new Color(0.58f, 0.26f, 0.78f, 1f); // Mundo Interno / Créditos
    private static readonly Color AccentCorporeoRed = new Color(0.72f, 0.18f, 0.18f, 1f); // Corpóreo / Salir

    [MenuItem("Tools/Reconstruir Menu Principal")]
    public static void RebuildMainMenuScene()
    {
        ItemSpriteInspector.InspectItemSprites();

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("[MainMenuSceneBuilder] Saliendo de Play Mode. Vuelve a ejecutar cuando esté en Edit Mode.");
            return;
        }

        // 1. Refrescar base de datos de activos
        AssetDatabase.Refresh();

        // 2. Abrir la escena 00_MenuIntro
        if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
        {
            if (File.Exists(MenuScenePath))
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogError($"[MainMenuSceneBuilder] No se encontró el archivo de escena '{MenuScenePath}'.");
                return;
            }
        }

        TMP_FontAsset pixelFont = LoadPixelFont();

        // 2. Localizar o crear Canvas y referencias principales
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[MainMenuSceneBuilder] No se encontró Canvas en 00_MenuIntro.");
            return;
        }

        IntroSlideshowManager introManager = UnityEngine.Object.FindAnyObjectByType<IntroSlideshowManager>();
        if (introManager == null)
        {
            Debug.LogError("[MainMenuSceneBuilder] No se encontró IntroSlideshowManager en 00_MenuIntro.");
            return;
        }

        // 3. Configurar resolución y EventSystem
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;

        EventSystem es = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esGO = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }

        // 4. Configurar StartScreenPanel y sus botones
        Transform startScreenT = canvas.transform.Find("StartScreenPanel");
        if (startScreenT == null)
        {
            GameObject newStartScreen = CreatePanel("StartScreenPanel", canvas.transform, PureBlack);
            startScreenT = newStartScreen.transform;
        }
        GameObject startScreenPanel = startScreenT.gameObject;
        startScreenPanel.SetActive(true);

        // 5. Configurar Textos de Título y Subtítulo
        SetupTitleTexts(startScreenT, pixelFont);

        // 6. Configurar los 4 Botones del Menú Principal
        SetupMainMenuButtons(startScreenT, introManager, pixelFont);

        // 7. Construir Panel Modal de Configuración
        SetupSettingsPanel(canvas.transform, introManager, pixelFont);

        // 8. Construir Panel Modal de Créditos
        SetupCreditsPanel(canvas.transform, introManager, pixelFont);

        // 9. Conectar eventos persistentes en IntroSlideshowManager
        ConnectIntroManagerEvents(introManager);

        // 10. Asegurar que SlideshowPanel, SettingsPanel y CreditsPanel arranquen cerrados
        if (introManager.slideshowPanel != null) introManager.slideshowPanel.SetActive(false);
        if (introManager.settingsPanel != null) introManager.settingsPanel.SetActive(false);
        if (introManager.creditsPanel != null) introManager.creditsPanel.SetActive(false);
        startScreenPanel.SetActive(true);

        EditorUtility.SetDirty(introManager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[MainMenuSceneBuilder] ¡Menú Principal de la Escena 00 reconstruido exitosamente con los 4 botones (Aventura, Configuración, Créditos, Salir) y paneles modales!");
    }

    [MenuItem("Tools/Configurar Menu de Pausa en Escenas")]
    public static void SetupPauseMenuInGameplayScenes()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("[MainMenuSceneBuilder] Saliendo de Play Mode antes de configurar Menú de Pausa.");
            return;
        }

        string[] gameplayScenes = new string[]
        {
            "Assets/Scenes/01_Nivel1_SantaFelipina.unity",
            "Assets/Scenes/02_Nivel2_Guarida.unity",
            "Assets/Scenes/Nivel_3.unity"
        };

        string originalScenePath = SceneManager.GetActiveScene().path;

        foreach (string scenePath in gameplayScenes)
        {
            if (!File.Exists(scenePath)) continue;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) continue;

            SetupPauseMenuInCurrentScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MainMenuSceneBuilder] Menú de Pausa configurado y guardado en: {scenePath}");
        }

        if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
    }

    public static void SetupPauseMenuInCurrentScene()
    {
        PauseMenuManager pauseManager = PauseMenuManager.EnsureInScene();
        if (pauseManager != null)
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            EditorUtility.SetDirty(pauseManager.gameObject);
        }
    }

    private static TMP_FontAsset LoadPixelFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Pixelzone_TMP.asset");
        if (font != null) return font;

        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts" });
        foreach (string guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            if (f != null) return f;
        }
        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    private static void SetupTitleTexts(Transform startScreenT, TMP_FontAsset pixelFont)
    {
        // TitleText
        Transform titleT = startScreenT.Find("TitleText");
        TextMeshProUGUI titleTMP;
        if (titleT == null)
        {
            titleTMP = CreateTMPText("TitleText", startScreenT, "PROYECTO: VOCES INTERNAS", 110, TextAlignmentOptions.Center, pixelFont);
        }
        else
        {
            titleTMP = titleT.GetComponent<TextMeshProUGUI>();
            titleTMP.text = "PROYECTO: VOCES INTERNAS";
            titleTMP.fontSize = 110;
            if (pixelFont != null) titleTMP.font = pixelFont;
        }
        titleTMP.color = BoneWhite;
        SetAnchors(titleTMP.rectTransform, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.89f));

        // ChapterText
        Transform chapterT = startScreenT.Find("ChapterText");
        TextMeshProUGUI chapterTMP;
        if (chapterT == null)
        {
            chapterTMP = CreateTMPText("ChapterText", startScreenT, "CRÓNICAS URBANAS: SANTA FELIPINA", 48, TextAlignmentOptions.Center, pixelFont);
        }
        else
        {
            chapterTMP = chapterT.GetComponent<TextMeshProUGUI>();
            chapterTMP.text = "CRÓNICAS URBANAS: SANTA FELIPINA";
            chapterTMP.fontSize = 48;
            if (pixelFont != null) chapterTMP.font = pixelFont;
        }
        chapterTMP.color = AccentAmberGold;
        SetAnchors(chapterTMP.rectTransform, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.72f));

        // SubtitleText
        Transform subtitleT = startScreenT.Find("SubtitleText");
        TextMeshProUGUI subtitleTMP;
        if (subtitleT == null)
        {
            subtitleTMP = CreateTMPText("SubtitleText", startScreenT, "Enrique Bosques y sus 4 Voces Interiores", 32, TextAlignmentOptions.Center, pixelFont);
        }
        else
        {
            subtitleTMP = subtitleT.GetComponent<TextMeshProUGUI>();
            subtitleTMP.text = "Enrique Bosques y sus 4 Voces Interiores";
            subtitleTMP.fontSize = 32;
            if (pixelFont != null) subtitleTMP.font = pixelFont;
        }
        subtitleTMP.color = MutedGray;
        SetAnchors(subtitleTMP.rectTransform, new Vector2(0.05f, 0.53f), new Vector2(0.95f, 0.61f));

        // Pie de página decorativo
        Transform footerT = startScreenT.Find("FooterText");
        TextMeshProUGUI footerTMP;
        if (footerT == null)
        {
            footerTMP = CreateTMPText("FooterText", startScreenT, "v1.0 • RPG Santa Felipina • Sistema de Diálogo y Combate Psíquico", 20, TextAlignmentOptions.Center, pixelFont);
        }
        else
        {
            footerTMP = footerT.GetComponent<TextMeshProUGUI>();
            if (pixelFont != null) footerTMP.font = pixelFont;
        }
        footerTMP.color = new Color(0.40f, 0.42f, 0.46f, 1f);
        SetAnchors(footerTMP.rectTransform, new Vector2(0.05f, 0.012f), new Vector2(0.95f, 0.045f));
    }

    private static void SetupMainMenuButtons(Transform startScreenT, IntroSlideshowManager introManager, TMP_FontAsset pixelFont)
    {
        // Eliminar botones viejos si existen para reconstruirlos de forma limpia
        DestroyChildIfExists(startScreenT, "StartGameButton");
        DestroyChildIfExists(startScreenT, "AdventureButton");
        DestroyChildIfExists(startScreenT, "SettingsButton");
        DestroyChildIfExists(startScreenT, "CreditsButton");
        DestroyChildIfExists(startScreenT, "QuitButton");

        // 1. Botón: Comenzar la Aventura
        Button advBtn = CreateNoirButton(
            "AdventureButton",
            startScreenT,
            "[ COMENZAR LA AVENTURA ]",
            MatteBlack,
            AccentAmberGold,
            38,
            pixelFont
        );
        SetAnchors(advBtn.GetComponent<RectTransform>(), new Vector2(0.30f, 0.39f), new Vector2(0.70f, 0.485f));

        // 2. Botón: Configuración del Sistema
        Button setBtn = CreateNoirButton(
            "SettingsButton",
            startScreenT,
            "[ CONFIGURACION DEL SISTEMA ]",
            MatteBlack,
            AccentIntelectoBlue,
            34,
            pixelFont
        );
        SetAnchors(setBtn.GetComponent<RectTransform>(), new Vector2(0.30f, 0.28f), new Vector2(0.70f, 0.375f));

        // 3. Botón: Créditos
        Button credBtn = CreateNoirButton(
            "CreditsButton",
            startScreenT,
            "[ CREDITOS ]",
            MatteBlack,
            AccentMundoViolet,
            34,
            pixelFont
        );
        SetAnchors(credBtn.GetComponent<RectTransform>(), new Vector2(0.30f, 0.17f), new Vector2(0.70f, 0.265f));

        // 4. Botón: Salir del Juego
        Button quitBtn = CreateNoirButton(
            "QuitButton",
            startScreenT,
            "[ SALIR DEL JUEGO ]",
            MatteBlack,
            AccentCorporeoRed,
            34,
            pixelFont
        );
        SetAnchors(quitBtn.GetComponent<RectTransform>(), new Vector2(0.30f, 0.06f), new Vector2(0.70f, 0.155f));

        // Asignar referencias en IntroSlideshowManager
        introManager.adventureButton = advBtn;
        introManager.startGameButton = advBtn;
        introManager.settingsButton = setBtn;
        introManager.creditsButton = credBtn;
        introManager.quitButton = quitBtn;
    }

    private static void SetupSettingsPanel(Transform canvasT, IntroSlideshowManager introManager, TMP_FontAsset pixelFont)
    {
        DestroyChildIfExists(canvasT, "SettingsPanel");

        // Fondo oscuro semitransparente que bloquea clicks atrás
        GameObject panelGO = CreatePanel("SettingsPanel", canvasT, new Color(0.01f, 0.01f, 0.015f, 0.90f));
        SetAnchors(panelGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        // Marco central
        GameObject windowBorder = CreatePanel("SettingsWindowBorder", panelGO.transform, AccentIntelectoBlue);
        SetAnchors(windowBorder.GetComponent<RectTransform>(), new Vector2(0.22f, 0.12f), new Vector2(0.78f, 0.88f));

        GameObject windowInner = CreatePanel("SettingsWindowInner", windowBorder.transform, new Color(0.04f, 0.04f, 0.05f, 0.99f));
        SetAnchors(windowInner.GetComponent<RectTransform>(), new Vector2(0.003f, 0.005f), new Vector2(0.997f, 0.995f));

        // Título de Configuración
        TextMeshProUGUI headerTMP = CreateTMPText("SettingsHeader", windowInner.transform, "CONFIGURACIÓN DEL SISTEMA", 48, TextAlignmentOptions.Center, pixelFont);
        headerTMP.color = BoneWhite;
        SetAnchors(headerTMP.rectTransform, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f));

        TextMeshProUGUI subHeaderTMP = CreateTMPText("SettingsSubHeader", windowInner.transform, "Ajuste acústico y niveles de audio para Santa Felipina", 24, TextAlignmentOptions.Center, pixelFont);
        subHeaderTMP.color = MutedGray;
        SetAnchors(subHeaderTMP.rectTransform, new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.86f));

        // --- FILA 1: VOLUMEN DE MÚSICA (BGM) ---
        TextMeshProUGUI musicLabel = CreateTMPText("MusicVolumeLabel", windowInner.transform, "VOLUMEN DE MÚSICA (BGM)", 30, TextAlignmentOptions.Left, pixelFont);
        musicLabel.color = BoneWhite;
        SetAnchors(musicLabel.rectTransform, new Vector2(0.10f, 0.65f), new Vector2(0.70f, 0.73f));

        TextMeshProUGUI musicPercent = CreateTMPText("MusicVolumePercentText", windowInner.transform, "80%", 30, TextAlignmentOptions.Right, pixelFont);
        musicPercent.color = AccentAmberGold;
        SetAnchors(musicPercent.rectTransform, new Vector2(0.70f, 0.65f), new Vector2(0.90f, 0.73f));

        Slider musicSlider = CreateNoirSlider("MusicVolumeSlider", windowInner.transform, AccentIntelectoBlue);
        SetAnchors(musicSlider.GetComponent<RectTransform>(), new Vector2(0.10f, 0.55f), new Vector2(0.90f, 0.63f));

        // --- FILA 2: VOLUMEN DE EFECTOS (SFX) ---
        TextMeshProUGUI sfxLabel = CreateTMPText("SfxVolumeLabel", windowInner.transform, "VOLUMEN DE EFECTOS (SFX)", 30, TextAlignmentOptions.Left, pixelFont);
        sfxLabel.color = BoneWhite;
        SetAnchors(sfxLabel.rectTransform, new Vector2(0.10f, 0.40f), new Vector2(0.70f, 0.48f));

        TextMeshProUGUI sfxPercent = CreateTMPText("SfxVolumePercentText", windowInner.transform, "90%", 30, TextAlignmentOptions.Right, pixelFont);
        sfxPercent.color = AccentAmberGold;
        SetAnchors(sfxPercent.rectTransform, new Vector2(0.70f, 0.40f), new Vector2(0.90f, 0.48f));

        Slider sfxSlider = CreateNoirSlider("SfxVolumeSlider", windowInner.transform, AccentAmberGold);
        SetAnchors(sfxSlider.GetComponent<RectTransform>(), new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.38f));

        // Botón Cerrar Configuración
        Button closeSettingsBtn = CreateNoirButton(
            "CloseSettingsButton",
            windowInner.transform,
            "VOLVER AL MENÚ PRINCIPAL (ESC)",
            MatteBlack,
            SubtleBorder,
            32,
            pixelFont
        );
        SetAnchors(closeSettingsBtn.GetComponent<RectTransform>(), new Vector2(0.22f, 0.08f), new Vector2(0.78f, 0.19f));

        // Asignar en IntroSlideshowManager
        introManager.settingsPanel = panelGO;
        introManager.musicVolumeSlider = musicSlider;
        introManager.sfxVolumeSlider = sfxSlider;
        introManager.musicVolumePercentText = musicPercent;
        introManager.sfxVolumePercentText = sfxPercent;
        introManager.closeSettingsButton = closeSettingsBtn;

        panelGO.SetActive(false);
    }

    private static void SetupCreditsPanel(Transform canvasT, IntroSlideshowManager introManager, TMP_FontAsset pixelFont)
    {
        DestroyChildIfExists(canvasT, "CreditsPanel");

        // Fondo oscuro semitransparente
        GameObject panelGO = CreatePanel("CreditsPanel", canvasT, new Color(0.01f, 0.01f, 0.015f, 0.90f));
        SetAnchors(panelGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        // Marco central violeta
        GameObject windowBorder = CreatePanel("CreditsWindowBorder", panelGO.transform, AccentMundoViolet);
        SetAnchors(windowBorder.GetComponent<RectTransform>(), new Vector2(0.16f, 0.08f), new Vector2(0.84f, 0.92f));

        GameObject windowInner = CreatePanel("CreditsWindowInner", windowBorder.transform, new Color(0.04f, 0.04f, 0.05f, 0.99f));
        SetAnchors(windowInner.GetComponent<RectTransform>(), new Vector2(0.003f, 0.005f), new Vector2(0.997f, 0.995f));

        // Título de Créditos
        TextMeshProUGUI headerTMP = CreateTMPText("CreditsHeader", windowInner.transform, "CREDITOS DEL PROYECTO", 48, TextAlignmentOptions.Center, pixelFont);
        headerTMP.color = new Color(0.80f, 0.48f, 0.98f, 1f);
        SetAnchors(headerTMP.rectTransform, new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.97f));

        TextMeshProUGUI subHeaderTMP = CreateTMPText("CreditsSubHeader", windowInner.transform, "PROYECTO: VOCES INTERNAS - CRONICAS URBANAS", 24, TextAlignmentOptions.Center, pixelFont);
        subHeaderTMP.color = AccentAmberGold;
        SetAnchors(subHeaderTMP.rectTransform, new Vector2(0.05f, 0.81f), new Vector2(0.95f, 0.87f));

        // Cuerpo de Créditos con formato enriquecido
        string creditsText =
            "<color=#E2E4EB><b>DIRECCION & DESARROLLO DE VIDEOJUEGO</b></color>\n" +
            "<color=#A0A3AB>Equipo de Desarrollo de RPG Santa Felipina</color>\n\n" +
            "<color=#E2E4EB><b>UNIVERSO NARRATIVO Y PSICOLOGIA DE PERSONAJES</b></color>\n" +
            "<color=#A0A3AB>Inspirado en la psique fragmentada de Enrique Bosques y sus 4 Voces:\n" +
            "<color=#D83A3A>Corporeo</color>  -  <color=#3399E8>Intelecto</color>  -  <color=#E5AC24>Motricidad</color>  -  <color=#A045E6>Mundo Interno</color></color>\n\n" +
            "<color=#E2E4EB><b>BANDA SONORA Y MUSICA ORIGINAL</b></color>\n" +
            "<color=#A0A3AB>Chill Menu Loopable  -  Crashing Out Loopable  -  Critical Theme Loopable\n" +
            "VictoryLap Loopable  -  80s Zombies Movie (Clement Panchout)</color>\n\n" +
            "<color=#E2E4EB><b>DISENO SONORO Y EFECTOS ACUSTICOS</b></color>\n" +
            "<color=#A0A3AB>Pixel UI SFX Pack (JDSherbert)  -  Efectos de Impacto, Habilidades y Pasos</color>\n\n" +
            "<color=#E2E4EB><b>ARTE 2D, RETRATOS & ESCENARIOS</b></color>\n" +
            "<color=#A0A3AB>PostApocalypse 2D Asset Pack  -  Ilustraciones y Retratos Originales</color>\n\n" +
            "<color=#E5AC24><b>GRACIAS POR EMBARCARTE EN ESTA AVENTURA!</b></color>";

        TextMeshProUGUI bodyTMP = CreateTMPText("CreditsBodyText", windowInner.transform, creditsText, 23, TextAlignmentOptions.Center, pixelFont);
        bodyTMP.lineSpacing = 10f;
        SetAnchors(bodyTMP.rectTransform, new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.80f));

        // Botón Cerrar Créditos
        Button closeCreditsBtn = CreateNoirButton(
            "CloseCreditsButton",
            windowInner.transform,
            "VOLVER AL MENÚ PRINCIPAL (ESC)",
            MatteBlack,
            SubtleBorder,
            32,
            pixelFont
        );
        SetAnchors(closeCreditsBtn.GetComponent<RectTransform>(), new Vector2(0.24f, 0.045f), new Vector2(0.76f, 0.145f));

        // Asignar en IntroSlideshowManager
        introManager.creditsPanel = panelGO;
        introManager.closeCreditsButton = closeCreditsBtn;

        panelGO.SetActive(false);
    }

    private static void ConnectIntroManagerEvents(IntroSlideshowManager introManager)
    {
        // 1. Botón Comenzar la Aventura
        if (introManager.adventureButton != null)
        {
            ClearPersistentListeners(introManager.adventureButton.onClick);
            UnityEventTools.AddPersistentListener(introManager.adventureButton.onClick, introManager.StartGame);
        }

        // 2. Botón Configuración
        if (introManager.settingsButton != null)
        {
            ClearPersistentListeners(introManager.settingsButton.onClick);
            UnityEventTools.AddPersistentListener(introManager.settingsButton.onClick, introManager.OpenSettings);
        }

        // 3. Botón Créditos
        if (introManager.creditsButton != null)
        {
            ClearPersistentListeners(introManager.creditsButton.onClick);
            UnityEventTools.AddPersistentListener(introManager.creditsButton.onClick, introManager.OpenCredits);
        }

        // 4. Botón Salir
        if (introManager.quitButton != null)
        {
            ClearPersistentListeners(introManager.quitButton.onClick);
            UnityEventTools.AddPersistentListener(introManager.quitButton.onClick, introManager.QuitGame);
        }

        // 5. Botón Cerrar Configuración
        if (introManager.closeSettingsButton != null)
        {
            ClearPersistentListeners(introManager.closeSettingsButton.onClick);
            UnityEventTools.AddPersistentListener(introManager.closeSettingsButton.onClick, introManager.CloseSettings);
        }

        // 6. Botón Cerrar Créditos
        if (introManager.closeCreditsButton != null)
        {
            ClearPersistentListeners(introManager.closeCreditsButton.onClick);
            UnityEventTools.AddPersistentListener(introManager.closeCreditsButton.onClick, introManager.CloseCredits);
        }

        // 7. Sliders de Audio
        if (introManager.musicVolumeSlider != null)
        {
            ClearPersistentListeners(introManager.musicVolumeSlider.onValueChanged);
            UnityEventTools.AddPersistentListener(introManager.musicVolumeSlider.onValueChanged, introManager.OnMusicVolumeChanged);
        }

        if (introManager.sfxVolumeSlider != null)
        {
            ClearPersistentListeners(introManager.sfxVolumeSlider.onValueChanged);
            UnityEventTools.AddPersistentListener(introManager.sfxVolumeSlider.onValueChanged, introManager.OnSfxVolumeChanged);
        }

        // Actualizar componentes en memoria
        introManager.SetupMenuButtonsAndModals();
    }

    private static void ClearPersistentListeners(UnityEngine.Events.UnityEventBase evt)
    {
        if (evt == null) return;
        while (evt.GetPersistentEventCount() > 0)
        {
            UnityEventTools.RemovePersistentListener(evt, 0);
        }
    }

    private static Button CreateNoirButton(string name, Transform parent, string label, Color innerBlackColor, Color accentBorderColor, float fontSize, TMP_FontAsset font)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        Image borderImg = btnGO.GetComponent<Image>();
        borderImg.color = accentBorderColor;

        GameObject innerGO = CreatePanel("InnerBlack", btnGO.transform, innerBlackColor);
        SetAnchors(innerGO.GetComponent<RectTransform>(), new Vector2(0.015f, 0.04f), new Vector2(0.985f, 0.96f));
        Image innerImg = innerGO.GetComponent<Image>();

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = innerImg;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        btn.colors = cb;

        TextMeshProUGUI txt = CreateTMPText("Label", innerGO.transform, label, fontSize, TextAlignmentOptions.Center, font);
        txt.color = BoneWhite;
        SetAnchors(txt.rectTransform, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f));

        // Sonido de hover usando el sfx de cursor de MusicManager
        UIButtonSoundFeedback feedback = btnGO.AddComponent<UIButtonSoundFeedback>();
        feedback.hoverVolumeScale = 0.55f;

        return btn;
    }

    private static Slider CreateNoirSlider(string name, Transform parent, Color fillColor)
    {
        GameObject sliderGO = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(parent, false);
        Slider slider = sliderGO.GetComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        // Fondo oscuro de la barra
        GameObject bgGO = CreatePanel("Background", sliderGO.transform, new Color(0.12f, 0.12f, 0.15f, 1f));
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        SetAnchors(bgRT, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f));

        // Área de relleno
        GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        SetAnchors(fillAreaRT, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f));

        GameObject fillGO = CreatePanel("Fill", fillAreaGO.transform, fillColor);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;
        fillRT.anchoredPosition = Vector2.zero;

        // Área de mango / handle
        GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
        SetAnchors(handleAreaRT, new Vector2(0f, 0f), new Vector2(1f, 1f));

        GameObject handleGO = CreatePanel("Handle", handleAreaGO.transform, BoneWhite);
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(24f, 0f);
        handleRT.anchorMin = new Vector2(0f, 0.08f);
        handleRT.anchorMax = new Vector2(0f, 0.92f);

        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleGO.GetComponent<Image>();

        return slider;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color bgColor)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image img = panel.GetComponent<Image>();
        img.color = bgColor;
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return panel;
    }

    private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string content, float fontSize, TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.characterSpacing = 2f;
        tmp.wordSpacing = 6f;
        return tmp;
    }

    private static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void DestroyChildIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }
}
#endif
