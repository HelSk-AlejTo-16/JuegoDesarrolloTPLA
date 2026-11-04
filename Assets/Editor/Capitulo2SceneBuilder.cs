#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Constructor de escena para el Capítulo 2: El Callejón de las Sombras (Paso Peatonal hacia El Calacas).
/// Genera el callejón urbano encajonado entre edificios de la ciudad donde solo se puede pasar a pie,
/// el Investigador Privado (Héctor Vargas) que habla con Enrique al inicio y lo sigue durante todo el nivel,
/// los cuadros de diálogo noir con retratos y la transición temática entre Escena 1 y Escena 2.
/// </summary>
[InitializeOnLoad]
public static class Capitulo2SceneBuilder
{
    private const string Capitulo2ScenePath = "Assets/Scenes/02_Nivel2_Guarida.unity";
    private const string SampleScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";
    private const string AutoBuildSessionKey = "CallejonNivel2_AutoBuilt_v5";

    static Capitulo2SceneBuilder()
    {
        EditorApplication.delayCall += TryAutoUpdateScenesOnce;
    }

    private static void TryAutoUpdateScenesOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (SessionState.GetBool(AutoBuildSessionKey, false))
            return;

        SessionState.SetBool(AutoBuildSessionKey, true);

        // Si el archivo de la escena 2 aún no contiene la versión armónica V5, actualizar automáticamente
        if (File.Exists(Capitulo2ScenePath))
        {
            string content = File.ReadAllText(Capitulo2ScenePath);
            if (content.Contains("Almacen_Clandestino_Root") || !content.Contains("Callejon_Armonico_V5"))
            {
                BuildCapitulo2AndConfigureBoss();
            }
        }
    }

    [MenuItem("RPG Santa Felipina/Construir Escenario Capitulo 2 (Callejon y Detective)")]
    public static void BuildCapitulo2AndConfigureBoss()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("[Capitulo2SceneBuilder] Saliendo de Play Mode antes de construir.");
            return;
        }

        string previousScenePath = SceneManager.GetActiveScene().path;

        if (!Directory.Exists("Assets/Scenes"))
        {
            Directory.CreateDirectory("Assets/Scenes");
        }

        TMP_FontAsset pixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Pixelzone_TMP.asset");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Main Camera con CameraFollow2D (mismo color y tamaño ortográfico que Escena 1)
        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(-7.5f, -0.4f, -10f);
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        camGO.AddComponent<AudioListener>();

        CameraFollow2D camFollow = camGO.AddComponent<CameraFollow2D>();
        camFollow.smoothTime = 0.18f;
        camFollow.useBounds = true;
        camFollow.minBounds = new Vector2(-7.5f, -18.5f);
        camFollow.maxBounds = new Vector2(16.5f, 3.5f);

        // 2. EventSystem
        GameObject esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        esGO.AddComponent<StandaloneInputModule>();
#endif

        // 3. InventoryManager (conserva el inventario, recetas y armas de la Escena 1)
        GameObject invGO = new GameObject("InventoryManager");
        InventoryManager invMgr = invGO.AddComponent<InventoryManager>();
        invMgr.knownRecipes = new List<CraftingRecipeData>();
        CraftingRecipeData rec1 = AssetDatabase.LoadAssetAtPath<CraftingRecipeData>("Assets/Data/Items/Receta_BombaChile.asset");
        CraftingRecipeData rec2 = AssetDatabase.LoadAssetAtPath<CraftingRecipeData>("Assets/Data/Items/Receta_VendajeRudo.asset");
        if (rec1 != null) invMgr.knownRecipes.Add(rec1);
        if (rec2 != null) invMgr.knownRecipes.Add(rec2);

        invMgr.preparedWeaponCatalog = new List<ItemData>();
        ItemData wep1 = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_PunosCallejeros.asset");
        ItemData wep2 = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_TuboPlomo.asset");
        ItemData wep3 = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_BatCallejero.asset");
        if (wep1 != null) invMgr.preparedWeaponCatalog.Add(wep1);
        if (wep2 != null) invMgr.preparedWeaponCatalog.Add(wep2);
        if (wep3 != null) invMgr.preparedWeaponCatalog.Add(wep3);

        // 4. Construir el Mapa del Callejón Urbano Peatonal entre Edificios
        GameObject alleyRoot = Capitulo2IntroNarrative.BuildUrbanAlleyEnvironment();

        // 5. Crear al Investigador Privado (Héctor Vargas) al inicio del callejón con CompanionFollowAI
        CompanionFollowAI investigator = Capitulo2IntroNarrative.CreateInvestigatorCompanion(alleyRoot.transform);

        // 6. Crear a Enrique_Player en la entrada peatonal oeste del callejón
        Sprite cardoSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Personaje/stand_down.png");
        RuntimeAnimatorController playerCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player/Player_Controller.controller");

        GameObject enriqueGO = new GameObject("Enrique_Player");
        enriqueGO.tag = "Player";
        enriqueGO.transform.position = new Vector3(-9.6f, -0.4f, 0f);
        enriqueGO.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        Rigidbody2D enriqueRB = enriqueGO.AddComponent<Rigidbody2D>();
        enriqueRB.bodyType = RigidbodyType2D.Dynamic;
        enriqueRB.gravityScale = 0f;
        enriqueRB.freezeRotation = true;
        enriqueRB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D enriqueCol = enriqueGO.AddComponent<BoxCollider2D>();
        enriqueCol.isTrigger = false;
        enriqueCol.offset = new Vector2(0f, 0.72f);
        enriqueCol.size = new Vector2(0.7f, 1.44f);

        PlayerStats pStats = enriqueGO.AddComponent<PlayerStats>();
        pStats.maxHP = 100;
        pStats.currentHP = 100;
        pStats.corporeo = 12;
        pStats.intelecto = 14;
        pStats.motricidad = 12;
        pStats.mundoInterno = 12;

        GameObject visualGO = new GameObject("Visual");
        visualGO.transform.SetParent(enriqueGO.transform, false);
        visualGO.transform.localPosition = Vector3.zero;
        visualGO.transform.localRotation = Quaternion.identity;
        visualGO.transform.localScale = Vector3.one;

        SpriteRenderer enriqueSR = visualGO.AddComponent<SpriteRenderer>();
        enriqueSR.sprite = (cardoSprite != null) ? cardoSprite : Capitulo2IntroNarrative.GetWhiteSquareSprite();
        enriqueSR.color = Color.white;
        enriqueSR.sortingOrder = 5;

        Animator enriqueAnim = visualGO.AddComponent<Animator>();
        if (playerCtrl != null) enriqueAnim.runtimeAnimatorController = playerCtrl;

        PlayerController2D playerController = enriqueGO.AddComponent<PlayerController2D>();
        playerController.moveSpeed = 5.5f;
        playerController.runSpeed = 8.0f;
        playerController.canMove = true;
        playerController.visualTransform = visualGO.transform;
        playerController.animator = enriqueAnim;
        playerController.spriteRenderer = enriqueSR;
        string footstepPath = AssetDatabase.GUIDToAssetPath("45408f0f9c11e634991ccade749c70c8");
        if (!string.IsNullOrEmpty(footstepPath))
        {
            playerController.footstepSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(footstepPath);
        }

        camFollow.target = enriqueGO.transform;

        // 7. Canvas de Diálogo de Exploración (con retratos noir y las 4 voces)
        GameObject diagCanvasGO = new GameObject("ExplorationDialogueCanvas");
        Canvas diagCanvas = diagCanvasGO.AddComponent<Canvas>();
        diagCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        diagCanvas.sortingOrder = 50;

        CanvasScaler diagScaler = diagCanvasGO.AddComponent<CanvasScaler>();
        diagScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        diagScaler.referenceResolution = new Vector2(1920, 1080);
        diagScaler.matchWidthOrHeight = 0.5f;

        diagCanvasGO.AddComponent<GraphicRaycaster>();

        GameObject diagPanelGO = new GameObject("ExplorationDialoguePanel");
        diagPanelGO.transform.SetParent(diagCanvasGO.transform, false);
        RectTransform diagPanelRect = diagPanelGO.AddComponent<RectTransform>();
        diagPanelRect.anchorMin = new Vector2(0.08f, 0.04f);
        diagPanelRect.anchorMax = new Vector2(0.92f, 0.30f);
        diagPanelRect.offsetMin = Vector2.zero;
        diagPanelRect.offsetMax = Vector2.zero;

        Image diagPanelImg = diagPanelGO.AddComponent<Image>();
        diagPanelImg.color = new Color(0.04f, 0.04f, 0.06f, 0.96f);

        GameObject diagTextGO = new GameObject("SpeakerAndText");
        diagTextGO.transform.SetParent(diagPanelGO.transform, false);
        RectTransform diagTextRect = diagTextGO.AddComponent<RectTransform>();
        diagTextRect.anchorMin = new Vector2(0.205f, 0.20f);
        diagTextRect.anchorMax = new Vector2(0.95f, 0.70f);
        diagTextRect.offsetMin = Vector2.zero;
        diagTextRect.offsetMax = Vector2.zero;

        TextMeshProUGUI diagTMP = diagTextGO.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) diagTMP.font = pixelFont;
        diagTMP.fontSize = 26;
        diagTMP.color = new Color(0.92f, 0.90f, 0.86f, 1f);
        diagTMP.text = "";
        PlayerHealthBarUI.EnsureCleanPixelFont(diagTMP);

        GameObject continueGO = new GameObject("ContinueIndicator");
        continueGO.transform.SetParent(diagPanelGO.transform, false);
        RectTransform contRect = continueGO.AddComponent<RectTransform>();
        contRect.anchorMin = new Vector2(0.205f, 0.05f);
        contRect.anchorMax = new Vector2(0.96f, 0.19f);
        contRect.offsetMin = Vector2.zero;
        contRect.offsetMax = Vector2.zero;

        TextMeshProUGUI contTMP = continueGO.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) contTMP.font = pixelFont;
        contTMP.fontSize = 20;
        contTMP.alignment = TextAlignmentOptions.BottomRight;
        contTMP.color = new Color(0.92f, 0.68f, 0.22f, 0.9f);
        contTMP.text = "[ ESPACIO / E / CLIC PARA CONTINUAR ]";
        PlayerHealthBarUI.EnsureCleanPixelFont(contTMP);

        ExplorationDialogueManager expManager = diagCanvasGO.AddComponent<ExplorationDialogueManager>();
        expManager.explorationDialoguePanel = diagPanelGO;
        expManager.speakerAndText = diagTMP;
        expManager.continueIndicatorText = contTMP;
        expManager.playerController = playerController;
        string confirmSfxPath = AssetDatabase.GUIDToAssetPath("d99e35508bc1444caec592a24ad13b8d");
        if (!string.IsNullOrEmpty(confirmSfxPath))
        {
            expManager.dialogueConfirmSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(confirmSfxPath);
        }
        Capitulo2IntroNarrative.UpgradeDialoguePanelToNoirStyle(expManager);
        diagPanelGO.SetActive(false);

        // 8. Canvas de Introducción y Fade del Capítulo 2
        GameObject introCanvasGO = new GameObject("Capitulo2IntroCanvas");
        Canvas introCanvas = introCanvasGO.AddComponent<Canvas>();
        introCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        introCanvas.sortingOrder = 90;

        CanvasScaler introScaler = introCanvasGO.AddComponent<CanvasScaler>();
        introScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        introScaler.referenceResolution = new Vector2(1920, 1080);
        introScaler.matchWidthOrHeight = 0.5f;

        introCanvasGO.AddComponent<GraphicRaycaster>();

        GameObject fadeGO = new GameObject("BlackoutFadePanel");
        fadeGO.transform.SetParent(introCanvasGO.transform, false);
        RectTransform fadeRect = fadeGO.AddComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        Image fadeImg = fadeGO.AddComponent<Image>();
        fadeImg.color = Color.black;
        CanvasGroup fadeCG = fadeGO.AddComponent<CanvasGroup>();
        fadeCG.alpha = 1f;

        GameObject bannerGO = new GameObject("ChapterBannerPanel");
        bannerGO.transform.SetParent(fadeGO.transform, false);
        RectTransform bannerRect = bannerGO.AddComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.1f, 0.35f);
        bannerRect.anchorMax = new Vector2(0.9f, 0.65f);
        bannerRect.offsetMin = Vector2.zero;
        bannerRect.offsetMax = Vector2.zero;

        GameObject titleGO = new GameObject("ChapterTitleText");
        titleGO.transform.SetParent(bannerGO.transform, false);
        RectTransform titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.45f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) titleTMP.font = pixelFont;
        titleTMP.fontSize = 44;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0.92f, 0.68f, 0.22f, 1f);
        titleTMP.text = "CAPÍTULO 2: EL CALLEJÓN DE LAS SOMBRAS";
        PlayerHealthBarUI.EnsureCleanPixelFont(titleTMP);

        GameObject subGO = new GameObject("ChapterSubtitleText");
        subGO.transform.SetParent(bannerGO.transform, false);
        RectTransform subRect = subGO.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 0.45f);
        subRect.offsetMin = Vector2.zero;
        subRect.offsetMax = Vector2.zero;

        TextMeshProUGUI subTMP = subGO.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) subTMP.font = pixelFont;
        subTMP.fontSize = 24;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = new Color(0.85f, 0.85f, 0.85f, 0.85f);
        subTMP.text = "Paso Peatonal entre los Edificios — En Busca de El Calacas";
        PlayerHealthBarUI.EnsureCleanPixelFont(subTMP);

        Capitulo2IntroNarrative introNarrative = introCanvasGO.AddComponent<Capitulo2IntroNarrative>();
        introNarrative.fadeCanvasGroup = fadeCG;
        introNarrative.bannerPanel = bannerGO;
        introNarrative.chapterTitleText = titleTMP;
        introNarrative.chapterSubtitleText = subTMP;
        introNarrative.explorationDialogueManager = expManager;
        introNarrative.investigatorCompanion = investigator;

        // 9. Guardar la escena 02_Nivel2_Guarida.unity
        EditorSceneManager.SaveScene(scene, Capitulo2ScenePath);

        // Configurar barra de vida de exploración y menú de pausa en la escena 2
        PlayerPersonajeSetup.SetupPlayerHealthBarsVisual();

        EnsureSceneInBuildSettings(Capitulo2ScenePath);

        // 10. Configurar la transición en la Escena 1 (01_Nivel1_SantaFelipina.unity)
        if (File.Exists(SampleScenePath))
        {
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            ConfigureBossAndTransitionInSampleScene(pixelFont);
        }

        if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousScenePath))
        {
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }

        Debug.Log("[Capitulo2SceneBuilder] ¡Callejón del Nivel 2, Investigador Privado y transición configurados con éxito!");
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool exists = false;
        foreach (var s in scenes)
        {
            if (s.path == scenePath)
            {
                exists = true;
                s.enabled = true;
                break;
            }
        }
        if (!exists)
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static void ConfigureBossAndTransitionInSampleScene(TMP_FontAsset pixelFont)
    {
        string[] alleyTransitionLines = new string[]
        {
            "NARRADOR: \"El líder callejero cae de rodillas sobre el asfalto húmedo. Entre sus ropas encuentras un croquis manchado que señala un estrecho pasaje entre los edificios del centro.\"",
            "ENRIQUE BOSQUES: \"(Guardando el croquis) Así que 'El Calacas' está detrás de toda esta trampa... El rastro sigue por ese callejón.\"",
            "VOZ [INTELECTO]: \"Las calles principales están bloqueadas con autos calcinados. Solo existe una ruta hacia su guarida: cruzar a pie por el callejón entre los edificios.\"",
            "VOZ [MUNDO INTERNO]: \"Entre los muros estrechos alguien más sigue el mismo rastro de ceniza y pólvora. Un investigador privado nos espera bajo la sombra del farol...\"",
            "[ CAPÍTULO 2: EL CALLEJÓN DE LAS SOMBRAS ]"
        };

        GameObject bossGO = GameObject.Find("Lacayo_Segundo");
        if (bossGO != null)
        {
            EnemyEncounterTrigger bossTrigger = bossGO.GetComponent<EnemyEncounterTrigger>();
            if (bossTrigger != null)
            {
                bossTrigger.isBossEncounter = true;
                bossTrigger.nextSceneName = "02_Nivel2_Guarida";
                bossTrigger.victoryNarrativeLines = alleyTransitionLines;
                EditorUtility.SetDirty(bossTrigger);
                EditorUtility.SetDirty(bossGO);
            }
        }

        // Crear o actualizar el punto de transición peatonal al final de la calle en Escena 1
        GameObject existingGate = GameObject.Find("Entrada_Callejon_Nivel2");
        if (existingGate != null)
        {
            Object.DestroyImmediate(existingGate);
        }

        Vector3 gatePos = (bossGO != null)
            ? bossGO.transform.position + new Vector3(0f, -2.2f, 0f)
            : new Vector3(20.44f, -24.7f, 0f);

        GameObject transitionGO = new GameObject("Entrada_Callejon_Nivel2");
        transitionGO.transform.position = gatePos;

        BoxCollider2D col = transitionGO.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2.2f, 3.6f);

        SceneTransitionTrigger trigger = transitionGO.AddComponent<SceneTransitionTrigger>();
        trigger.targetSceneName = "02_Nivel2_Guarida";
        trigger.triggerOnEnter = false;
        trigger.requireBossDefeatedIfPresent = true;
        trigger.bossObjectToCheck = bossGO;
        trigger.transitionNarrativeLines = alleyTransitionLines;

        GameObject promptGO = new GameObject("PromptUI_E");
        promptGO.transform.SetParent(transitionGO.transform, false);
        promptGO.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        TextMeshPro tmp = promptGO.AddComponent<TextMeshPro>();
        if (pixelFont != null) tmp.font = pixelFont;
        tmp.text = "[E] ENTRAR AL CALLEJÓN PEATONAL";
        tmp.fontSize = 2.6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.78f, 0.32f, 1f);
        tmp.sortingOrder = 7;
        PlayerHealthBarUI.EnsureCleanPixelFont(tmp);
        promptGO.SetActive(false);

        trigger.promptUI = promptGO;

        BattleManager bm = Object.FindAnyObjectByType<BattleManager>();
        if (bm != null)
        {
            if (bm.bossVictoryTransitionUI == null)
            {
                bm.bossVictoryTransitionUI = BossVictoryTransitionUI.EnsureInScene(pixelFont);
                EditorUtility.SetDirty(bm);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }
}
#endif
