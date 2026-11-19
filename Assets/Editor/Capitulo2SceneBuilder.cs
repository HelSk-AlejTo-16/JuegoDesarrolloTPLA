#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Constructor de escena para el Capítulo 2: El Callejón de las Sombras (Paso Peatonal hacia El Calacas)
/// y escena destino Nivel_3.
/// Genera:
/// - El callejón urbano encajonado entre edificios de la ciudad.
/// - El Investigador Privado (Héctor Vargas) con CompanionFollowAI + DetectiveStats.
/// - El BattleManager con DetectiveActionMenu integrado para combates en grupo.
/// - Los 3 encuentros dobles de enemigos del Nivel 2 y el Jefe Final (El Capataz Blindado con Level2BossController).
/// - Los contenedores de basura interactuables (InteractableLoot).
/// - La escena `Assets/Scenes/Nivel_3.unity` registrada en Build Settings para la transición `OnBossDefeated`.
/// </summary>
[InitializeOnLoad]
public static class Capitulo2SceneBuilder
{
    private const string Capitulo2ScenePath = "Assets/Scenes/02_Nivel2_Guarida.unity";
    private const string Nivel3ScenePath = "Assets/Scenes/Nivel_3.unity";
    private const string SampleScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";
    private const string AutoBuildSessionKey = "CallejonNivel2_AutoBuilt_v9";

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

        bool needsRebuild = !File.Exists(Nivel3ScenePath);
        if (File.Exists(Capitulo2ScenePath))
        {
            string content = File.ReadAllText(Capitulo2ScenePath);
            if (content.Contains("Almacen_Clandestino_Root") || !content.Contains("Callejon_Armonico_V8"))
            {
                needsRebuild = true;
            }
        }
        else
        {
            needsRebuild = true;
        }

        if (needsRebuild)
        {
            BuildCapitulo2AndConfigureBoss();
        }
    }

    [MenuItem("RPG Santa Felipina/Construir Escenario Capitulo 3 (Pasillo y El Calacas)")]
    public static void BuildCapitulo3Guarida()
    {
        TMP_FontAsset pixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Sprites/Fonts/DungeonFont SDF.asset");
        Sprite cardoSprite = Capitulo2IntroNarrative.GetWhiteSquareSprite();
        var allSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Player - Cardo/Idle/1.png");
        if (allSprites != null && allSprites.Length > 0)
        {
            foreach (var s in allSprites) { if (s is Sprite sp) { cardoSprite = sp; break; } }
        }
        RuntimeAnimatorController playerCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Cardo/Cardo_Controller.controller");

        BuildNivel3DestinationScene(pixelFont, cardoSprite, playerCtrl);
        EnsureSceneInBuildSettings(Nivel3ScenePath);
        Debug.Log("[Capitulo2SceneBuilder] ¡Escenario Capítulo 3: Pasillo y Jefe Final El Calacas reconstruido desde cero y guardado con éxito!");
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

        // 1. Main Camera con CameraFollow2D
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
        ItemData wep3 = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_BatMadera.asset");
        if (wep1 != null) invMgr.preparedWeaponCatalog.Add(wep1);
        if (wep2 != null) invMgr.preparedWeaponCatalog.Add(wep2);
        if (wep3 != null) invMgr.preparedWeaponCatalog.Add(wep3);

        // 3b. Generar animaciones y controlador de Enemy-Punk para el Jefe del Nivel 2
        EnemyPunkAnimationBuilder.BuildEnemyPunkAnimationsAndController();

        // 4. Construir el Mapa del Callejón Urbano Peatonal + Enemigos Dobles N2 + Jefe N2 + Contenedores de Basura
        GameObject alleyRoot = Capitulo2IntroNarrative.BuildUrbanAlleyEnvironment();

        // 5. Crear al Investigador Privado (Héctor Vargas) con CompanionFollowAI + DetectiveStats
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

        // 7. Construir Canvas unificado y BattleManager idéntico al Nivel 1 (Fondo, 4 Voces, Intelecto, Mundo Interno, Game Over)
        DetectiveStats detStats = (investigator != null) ? investigator.GetComponent<DetectiveStats>() : null;
        BattleManager bm = BuildCompleteLevel2CombatUI(pStats, playerController, detStats, pixelFont);
        if (bm != null)
        {
            EnemyEncounterTrigger[] triggers = Object.FindObjectsByType<EnemyEncounterTrigger>(FindObjectsInactive.Include);
            foreach (EnemyEncounterTrigger tr in triggers)
            {
                if (tr != null) tr.battleManager = bm;
            }
        }

        // 8. Canvas de Diálogo de Exploración (con retratos noir y las 4 voces)
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

        // 9. Canvas de Introducción y Fade del Capítulo 2
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

        // 10. Guardar la escena 02_Nivel2_Guarida.unity
        EditorSceneManager.SaveScene(scene, Capitulo2ScenePath);

        PlayerPersonajeSetup.SetupPlayerHealthBarsVisual();
        EnsureSceneInBuildSettings(Capitulo2ScenePath);

        // 11. Construir y registrar Nivel_3.unity para la transición OnBossDefeated
        BuildNivel3DestinationScene(pixelFont, cardoSprite, playerCtrl);
        EnsureSceneInBuildSettings(Nivel3ScenePath);

        // 12. Configurar la transición en la Escena 1 (01_Nivel1_SantaFelipina.unity)
        if (File.Exists(SampleScenePath))
        {
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            ConfigureBossAndTransitionInSampleScene(pixelFont);
        }

        if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousScenePath))
        {
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Capitulo2SceneBuilder] ¡Nivel 2 (Detective, Enemigos Dobles, Jefe N2, Loot de Basura) y Nivel_3 configurados con éxito!");
    }

    private static void BuildNivel3DestinationScene(TMP_FontAsset pixelFont, Sprite cardoSprite, RuntimeAnimatorController playerCtrl)
    {
        UnityEngine.SceneManagement.Scene scene3 = EditorSceneManager.OpenScene(Nivel3ScenePath, OpenSceneMode.Single);

        // Destruir cualquier raíz antigua de guarida, punks o jefes huérfanos
        foreach (var r in scene3.GetRootGameObjects())
        {
            if (r != null && (r.name.Contains("Guarida") || r.name.Contains("Punk") || r.name.Contains("Calacas")))
            {
                Object.DestroyImmediate(r);
            }
        }

        // 1. Cámara y seguimiento
        GameObject camGO = GameObject.Find("Main Camera");
        if (camGO == null)
        {
            camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }
        camGO.transform.position = new Vector3(-2.0f, 0.20f, -10f);
        Camera cam = camGO.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 4.8f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);

        // 2. EventSystem
        GameObject esGO = GameObject.Find("EventSystem");
        if (esGO == null)
        {
            esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }

        // 3. InventoryManager
        GameObject invGO = GameObject.Find("InventoryManager");
        if (invGO == null)
        {
            invGO = new GameObject("InventoryManager");
            invGO.AddComponent<InventoryManager>();
        }

        // 4. Protagonista Enrique Bosques
        GameObject enriqueGO = GameObject.Find("Enrique_Player");
        if (enriqueGO == null)
        {
            enriqueGO = new GameObject("Enrique_Player");
            enriqueGO.tag = "Player";
            Rigidbody2D rb = enriqueGO.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            BoxCollider2D col = enriqueGO.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.7f, 1.44f);
            enriqueGO.AddComponent<PlayerStats>();

            GameObject visGO = new GameObject("Visual");
            visGO.transform.SetParent(enriqueGO.transform, false);
            SpriteRenderer sr = visGO.AddComponent<SpriteRenderer>();
            sr.sprite = cardoSprite != null ? cardoSprite : Capitulo2IntroNarrative.GetWhiteSquareSprite();
            sr.sortingOrder = 5;
            Animator anim = visGO.AddComponent<Animator>();
            if (playerCtrl != null) anim.runtimeAnimatorController = playerCtrl;

            PlayerController2D pc = enriqueGO.AddComponent<PlayerController2D>();
            pc.visualTransform = visGO.transform;
            pc.spriteRenderer = sr;
            pc.animator = anim;
        }
        enriqueGO.transform.position = new Vector3(-2.0f, 0.0f, 0f);
        enriqueGO.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        PlayerController2D playerCtrlComp = enriqueGO.GetComponent<PlayerController2D>();

        // Cámara sigue a Enrique con límites horizontales del pasillo
        CameraFollow2D camFollow = camGO.GetComponent<CameraFollow2D>();
        if (camFollow == null) camFollow = camGO.AddComponent<CameraFollow2D>();
        camFollow.target = enriqueGO.transform;
        camFollow.useBounds = true;
        camFollow.minBounds = new Vector2(-1.0f, 0.20f);
        camFollow.maxBounds = new Vector2(21.0f, 0.20f);
        camFollow.smoothTime = 0.15f;

        // 5. Detective acompañante
        CompanionFollowAI det = Object.FindAnyObjectByType<CompanionFollowAI>();
        if (det == null)
        {
            det = Capitulo2IntroNarrative.CreateInvestigatorCompanion(null);
        }
        det.transform.position = new Vector3(-3.2f, 0.0f, 0f);
        det.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        det.isFollowingPlayer = true;
        Capitulo2IntroNarrative.EnsureGenericInvestigatorAppearance(det);

        // 6. UI Canvas y Banner
        GameObject canvasGO = GameObject.Find("Nivel3HeaderCanvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("Nivel3HeaderCanvas");
            Canvas c = canvasGO.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 40;
            CanvasScaler cs = canvasGO.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        GameObject banner = GameObject.Find("Nivel3Banner");
        if (banner == null)
        {
            banner = new GameObject("Nivel3Banner", typeof(RectTransform), typeof(Image));
            banner.transform.SetParent(canvasGO.transform, false);
            RectTransform bRT = banner.GetComponent<RectTransform>();
            bRT.anchorMin = new Vector2(0.15f, 0.76f);
            bRT.anchorMax = new Vector2(0.85f, 0.94f);
            bRT.offsetMin = Vector2.zero;
            bRT.offsetMax = Vector2.zero;
            banner.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        }

        GameObject titleGO = GameObject.Find("TitleText");
        if (titleGO == null)
        {
            titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(banner.transform, false);
            RectTransform tRT = titleGO.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(16f, 8f);
            tRT.offsetMax = new Vector2(-16f, -8f);
        }
        TextMeshProUGUI tmp = titleGO.GetComponent<TextMeshProUGUI>();
        if (pixelFont != null) tmp.font = pixelFont;
        tmp.text = "CAPÍTULO 3: LA GUARIDA DE EL CALACAS\n<size=65%>El Búnker Subterráneo — El Enfrentamiento Final</size>";
        tmp.fontSize = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.78f, 0.28f, 1f);
        PlayerHealthBarUI.EnsureCleanPixelFont(tmp);

        // 7. ExplorationDialogueManager
        GameObject dGO = GameObject.Find("ExplorationDialogueManager");
        if (dGO == null)
        {
            dGO = new GameObject("ExplorationDialogueManager");
            dGO.AddComponent<ExplorationDialogueManager>();
        }
        ExplorationDialogueManager dm = dGO.GetComponent<ExplorationDialogueManager>();
        if (playerCtrlComp != null) dm.playerController = playerCtrlComp;

        // 8. Capitulo3GuaridaNarrative
        GameObject narrativeGO = GameObject.Find("Capitulo3GuaridaNarrative");
        if (narrativeGO == null)
        {
            narrativeGO = new GameObject("Capitulo3GuaridaNarrative");
            narrativeGO.AddComponent<Capitulo3GuaridaNarrative>();
        }
        Capitulo3GuaridaNarrative narrative = narrativeGO.GetComponent<Capitulo3GuaridaNarrative>();
        narrative.bannerPanel = banner;
        narrative.chapterTitleText = tmp;
        narrative.investigatorCompanion = det;
        narrative.explorationDialogueManager = dm;

        CanvasGroup cg = canvasGO.GetComponent<CanvasGroup>();
        if (cg == null) cg = canvasGO.AddComponent<CanvasGroup>();
        narrative.fadeCanvasGroup = cg;

        // 9. Construir el pasillo arquitectónico limpio y colocar a El Calacas al final
        GameObject newRoot = Capitulo3GuaridaNarrative.BuildGuaridaEnvironment();

        PlayerPersonajeSetup.SetupPlayerHealthBarsVisual();

        EditorSceneManager.MarkSceneDirty(scene3);
        Debug.Log($"[Capitulo2SceneBuilder] scene3.path antes de guardar: '{scene3.path}'");
        bool saved = EditorSceneManager.SaveScene(scene3);
        if (!saved) saved = EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Capitulo2SceneBuilder] SaveScene Nivel_3 resultado: {saved}, path={scene3.path}");
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

    /// <summary>
    /// Construye el Canvas unificado y completo para el Nivel 2 con la misma estructura, estética y opciones
    /// del Nivel 1 (Fondo de combate, 4 voces, Intelecto con crafteo/pociones, Mundo Interno, Diálogos y Game Over),
    /// e integra al Detective Vargas como tarjeta aliada y compañero de escuadrón.
    /// </summary>
    public static BattleManager BuildCompleteLevel2CombatUI(PlayerStats pStats, PlayerController2D playerController, DetectiveStats detStats, TMP_FontAsset pixelFont)
    {
        // 1. Destruir Canvas obsoleto CombatCanvas si existía
        GameObject oldCombatCanvas = GameObject.Find("CombatCanvas");
        if (oldCombatCanvas != null)
        {
            Object.DestroyImmediate(oldCombatCanvas);
        }

        // 2. Crear o encontrar el Canvas principal unificado (idéntico al Nivel 1)
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("Canvas");
        }
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGO.GetComponent<GraphicRaycaster>() == null)
        {
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Colores de la paleta Noir de Santa Felipina
        Color pureBlack = new Color(0.02f, 0.02f, 0.025f, 1f);
        Color matteBlack = new Color(0.05f, 0.05f, 0.06f, 1f);
        Color subtleBorder = new Color(0.26f, 0.26f, 0.30f, 1f);
        Color targetGoldBorder = new Color(0.92f, 0.68f, 0.22f, 1f);
        Color boneWhite = new Color(0.90f, 0.88f, 0.84f, 1f);
        Color crimsonBorder = new Color(0.68f, 0.14f, 0.14f, 1f);
        Color violetBorder = new Color(0.58f, 0.26f, 0.78f, 1f);

        // 3. Crear BattleManager en la escena
        GameObject bmGO = GameObject.Find("BattleManager");
        if (bmGO == null)
        {
            bmGO = new GameObject("BattleManager");
        }
        BattleManager bm = bmGO.GetComponent<BattleManager>();
        if (bm == null) bm = bmGO.AddComponent<BattleManager>();

        bm.playerStats = pStats;
        bm.playerController = playerController;
        bm.playerMovementScript = playerController;
        bm.detectiveStats = detStats;
        bm.bossVictoryTransitionUI = BossVictoryTransitionUI.EnsureInScene(pixelFont);

        // 4. Panel de Combate (CombatPanel)
        Transform existingCombatPanel = canvasGO.transform.Find("CombatPanel");
        if (existingCombatPanel != null)
        {
            Object.DestroyImmediate(existingCombatPanel.gameObject);
        }

        GameObject combatPanel = CreatePanel("CombatPanel", canvasGO.transform, pureBlack);
        bm.battlePanel = combatPanel;

        // Fondo de Combate (escena1Combate en viewport superior)
        string combatBgPath = "Assets/Sprites/escena1Combate.png";
        Sprite combatBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(combatBgPath);
        if (combatBgSprite == null)
        {
            var subs = AssetDatabase.LoadAllAssetsAtPath(combatBgPath);
            if (subs != null)
            {
                foreach (var o in subs) { if (o is Sprite s) { combatBgSprite = s; break; } }
            }
        }

        GameObject viewportGO = new GameObject("CombatBackgroundViewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGO.transform.SetParent(combatPanel.transform, false);
        SetAnchors(viewportGO.GetComponent<RectTransform>(), new Vector2(0f, 0.44f), new Vector2(1f, 1f));

        GameObject combatBgGO = CreatePanel("CombatBackground", viewportGO.transform, Color.white);
        RectTransform bgRT = combatBgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0.5f, 0.5f);
        bgRT.anchorMax = new Vector2(0.5f, 0.5f);
        bgRT.pivot = new Vector2(0.5f, 0.5f);
        bgRT.sizeDelta = new Vector2(1920f, 1440f);

        Image bgImg = combatBgGO.GetComponent<Image>();
        if (combatBgSprite != null)
        {
            bgImg.sprite = combatBgSprite;
            bgImg.color = Color.white;
            bgImg.preserveAspect = false;
        }
        viewportGO.transform.SetAsFirstSibling();
        bm.combatBackgroundImage = bgImg;

        GameObject dividerGO = CreatePanel("CombatViewportDivider", combatPanel.transform, subtleBorder);
        SetAnchors(dividerGO.GetComponent<RectTransform>(), new Vector2(0f, 0.437f), new Vector2(1f, 0.442f));
        dividerGO.transform.SetSiblingIndex(1);

        // 5. Retrato de Enrique a la izquierda (x: 0.02 a 0.175, y: 0.03 a 0.44)
        GameObject frameGO = CreatePanel("EnriqueCombatPortraitFrame", combatPanel.transform, subtleBorder);
        SetAnchors(frameGO.GetComponent<RectTransform>(), new Vector2(0.02f, 0.03f), new Vector2(0.175f, 0.44f));

        GameObject imgGO = CreatePanel("EnriqueCombatPortraitImage", frameGO.transform, Color.white);
        SetAnchors(imgGO.GetComponent<RectTransform>(), new Vector2(0.015f, 0.015f), new Vector2(0.985f, 0.985f));
        Image playerPortraitImg = imgGO.GetComponent<Image>();
        Sprite enriquePortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Portraits/Enrique.png");
        if (enriquePortrait == null)
        {
            enriquePortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Personaje/stand_down.png");
        }
        playerPortraitImg.sprite = enriquePortrait;
        playerPortraitImg.color = Color.white;
        playerPortraitImg.preserveAspect = false;
        bm.playerCombatPortraitImage = playerPortraitImg;
        bm.enriquePortrait = enriquePortrait;

        // 6. Configuración del Detective Vargas como miembro del grupo (usa el MISMO menú inferior de combate de 4 botones)
        Transform existingDetSlot = combatPanel.transform.Find("Detective_AllyCombatSlot");
        if (existingDetSlot != null)
        {
            Object.DestroyImmediate(existingDetSlot.gameObject);
        }
        Sprite detSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Portraits/Detective.png");
        if (detSprite == null) detSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Detective/detective_stand_down.png");
        if (detSprite == null) detSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Player - Cardo/Idle/1.png");
        if (detSprite != null)
        {
            bm.detectiveCombatPortrait = detSprite;
        }
        bm.detectiveStats = detStats;

        // 7. Slots para los 2 enemigos en pantalla (x: 0.19 a 0.56 y x: 0.59 a 0.96)
        EnemyUISlot slot0 = CreateEnemyUISlot(
            "EnemySlot_0",
            combatPanel.transform,
            new Vector2(0.19f, 0.44f),
            new Vector2(0.56f, 0.94f),
            "> [OBJETIVO] LACAYO RAPIDO (MOT: 10)",
            "HP: 45 / 45",
            new Color(0.75f, 0.38f, 0.10f, 1f),
            "[CLIC PARA APUNTAR]",
            targetGoldBorder,
            matteBlack,
            subtleBorder,
            pixelFont
        );

        EnemyUISlot slot1 = CreateEnemyUISlot(
            "EnemySlot_1",
            combatPanel.transform,
            new Vector2(0.59f, 0.44f),
            new Vector2(0.96f, 0.94f),
            "JEFE CALLEJERO (BOSS) (MOT: 7)",
            "HP: 120 / 120",
            new Color(0.55f, 0.10f, 0.10f, 1f),
            "[CLIC PARA APUNTAR]",
            subtleBorder,
            matteBlack,
            subtleBorder,
            pixelFont
        );

        slot0.selectTargetButton.onClick.RemoveAllListeners();
        slot0.selectTargetButton.onClick.AddListener(() => bm.SelectTargetEnemy(0));
        slot1.selectTargetButton.onClick.RemoveAllListeners();
        slot1.selectTargetButton.onClick.AddListener(() => bm.SelectTargetEnemy(1));

        bm.enemySlots = new EnemyUISlot[] { slot0, slot1 };
        slot0.targetIndicator.SetActive(true);
        slot1.targetIndicator.SetActive(false);

        // 8. Registro de Combate y Panel de GIFs (x: 0.19 a 0.97, y: 0.28 a 0.43)
        GameObject battleLogBorder = CreatePanel("BattleLogBorder", combatPanel.transform, subtleBorder);
        SetAnchors(battleLogBorder.GetComponent<RectTransform>(), new Vector2(0.19f, 0.28f), new Vector2(0.97f, 0.43f));

        GameObject battleLogBox = CreatePanel("BattleLogBox", battleLogBorder.transform, matteBlack);
        SetAnchors(battleLogBox.GetComponent<RectTransform>(), new Vector2(0.003f, 0.012f), new Vector2(0.997f, 0.988f));

        TextMeshProUGUI battleLogText = CreateTMPText(
            "BattleLogText",
            battleLogBox.transform,
            "TU TURNO — ENRIQUE | Cola de turnos preparada.",
            22,
            TextAlignmentOptions.MidlineLeft,
            pixelFont
        );
        battleLogText.color = boneWhite;
        SetAnchors(battleLogText.rectTransform, new Vector2(0.02f, 0.08f), new Vector2(0.98f, 0.92f));
        bm.battleLogText = battleLogText;

        GameObject actionGifPanel = CreatePanel("ActionGifPanel", combatPanel.transform, pureBlack);
        SetAnchors(actionGifPanel.GetComponent<RectTransform>(), new Vector2(0.19f, 0.28f), new Vector2(0.97f, 0.43f));

        GameObject actionGifBorder = CreatePanel("ActionGifBorder", actionGifPanel.transform, new Color(0.65f, 0.62f, 0.55f, 1f));
        SetAnchors(actionGifBorder.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        GameObject actionGifImageGO = CreatePanel("ActionGifImage", actionGifBorder.transform, new Color(0.18f, 0.08f, 0.08f, 1f));
        Image actionGifImage = actionGifImageGO.GetComponent<Image>();
        SetAnchors(actionGifImageGO.GetComponent<RectTransform>(), new Vector2(0.004f, 0.015f), new Vector2(0.996f, 0.985f));

        TextMeshProUGUI actionGifLabelText = CreateTMPText(
            "ActionGifLabelText",
            actionGifImageGO.transform,
            "[GIF: CORPOREO - Ataque Fisico]",
            32,
            TextAlignmentOptions.Center,
            pixelFont
        );
        actionGifLabelText.color = boneWhite;
        SetAnchors(actionGifLabelText.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
        actionGifPanel.SetActive(false);

        bm.actionGifPanel = actionGifPanel;
        bm.actionGifImage = actionGifImage;
        bm.actionGifLabelText = actionGifLabelText;

        // 9. Franja de Estado de Enrique (x: 0.19 a 0.97, y: 0.198 a 0.282)
        GameObject playerStatusStrip = CreatePanel("PlayerStatusStrip", combatPanel.transform, matteBlack);
        SetAnchors(playerStatusStrip.GetComponent<RectTransform>(), new Vector2(0.19f, 0.198f), new Vector2(0.97f, 0.282f));

        TextMeshProUGUI playerHPText = CreateTMPText(
            "PlayerHPText",
            playerStatusStrip.transform,
            "ENRIQUE HP: 100 / 100",
            22,
            TextAlignmentOptions.MidlineLeft,
            pixelFont
        );
        playerHPText.color = boneWhite;
        SetAnchors(playerHPText.rectTransform, new Vector2(0.015f, 0.48f), new Vector2(0.68f, 0.96f));
        bm.playerHPText = playerHPText;

        TextMeshProUGUI playerHPPctText = CreateTMPText(
            "PlayerHPPctText",
            playerStatusStrip.transform,
            "100%",
            22,
            TextAlignmentOptions.MidlineRight,
            pixelFont
        );
        playerHPPctText.color = new Color(0.22f, 0.85f, 0.38f, 1f);
        SetAnchors(playerHPPctText.rectTransform, new Vector2(0.68f, 0.48f), new Vector2(0.985f, 0.96f));

        // Sub-elementos de la barra de vida animada de Enrique
        GameObject healthBarBorder = CreatePanel("HealthBarBorder", playerStatusStrip.transform, new Color(0.35f, 0.35f, 0.35f, 1f));
        SetAnchors(healthBarBorder.GetComponent<RectTransform>(), new Vector2(0.015f, 0.08f), new Vector2(0.985f, 0.38f));

        GameObject healthBarBg = CreatePanel("HealthBarBackground", healthBarBorder.transform, new Color(0.08f, 0.08f, 0.08f, 1f));
        SetAnchors(healthBarBg.GetComponent<RectTransform>(), new Vector2(0.002f, 0.08f), new Vector2(0.998f, 0.92f));

        GameObject damageFillGO = CreatePanel("DamageFill", healthBarBg.transform, new Color(0.88f, 0.14f, 0.14f, 0.75f));
        SetAnchors(damageFillGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        GameObject mainFillGO = CreatePanel("MainFill", healthBarBg.transform, new Color(0.22f, 0.85f, 0.38f, 1f));
        SetAnchors(mainFillGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        PlayerHealthBarUI pHealthBarUI = playerStatusStrip.AddComponent<PlayerHealthBarUI>();
        pHealthBarUI.playerStats = pStats;
        pHealthBarUI.barBorderImage = healthBarBorder.GetComponent<Image>();
        pHealthBarUI.barBackgroundImage = healthBarBg.GetComponent<Image>();
        pHealthBarUI.damageFillRect = damageFillGO.GetComponent<RectTransform>();
        pHealthBarUI.damageFillImage = damageFillGO.GetComponent<Image>();
        pHealthBarUI.mainFillRect = mainFillGO.GetComponent<RectTransform>();
        pHealthBarUI.mainFillImage = mainFillGO.GetComponent<Image>();
        pHealthBarUI.hpText = playerHPText;
        pHealthBarUI.percentageText = playerHPPctText;
        pHealthBarUI.healthyColor = new Color(0.22f, 0.85f, 0.38f, 1f);
        pHealthBarUI.percentageColor = new Color(0.22f, 0.85f, 0.38f, 1f);
        bm.playerHealthBarUI = pHealthBarUI;

        // 10. Los 4 Botones de las Voces de Enrique (x: 0.19 a 0.97, y: 0.03 a 0.19)
        Button corporeoButton = CreateNoirButton(
            "CorporeoButton",
            combatPanel.transform,
            "CORPOREO\nAtacar (Fisico)",
            matteBlack,
            new Color(0.65f, 0.18f, 0.18f, 1f),
            28,
            pixelFont
        );
        SetAnchors(corporeoButton.GetComponent<RectTransform>(), new Vector2(0.19f, 0.03f), new Vector2(0.375f, 0.19f));

        Button intelectoButton = CreateNoirButton(
            "IntelectoButton",
            combatPanel.transform,
            "INTELECTO\nObjeto / Improvisar",
            matteBlack,
            new Color(0.18f, 0.42f, 0.68f, 1f),
            28,
            pixelFont
        );
        SetAnchors(intelectoButton.GetComponent<RectTransform>(), new Vector2(0.388f, 0.03f), new Vector2(0.573f, 0.19f));

        Button motricidadButton = CreateNoirButton(
            "MotricidadButton",
            combatPanel.transform,
            "MOTRICIDAD\nEsquivar / Defender",
            matteBlack,
            new Color(0.68f, 0.52f, 0.14f, 1f),
            28,
            pixelFont
        );
        SetAnchors(motricidadButton.GetComponent<RectTransform>(), new Vector2(0.587f, 0.03f), new Vector2(0.772f, 0.19f));

        Button mundoInternoButton = CreateNoirButton(
            "MundoInternoButton",
            combatPanel.transform,
            "MUNDO INTERNO\nEfecto de Estado",
            matteBlack,
            new Color(0.48f, 0.20f, 0.65f, 1f),
            28,
            pixelFont
        );
        SetAnchors(mundoInternoButton.GetComponent<RectTransform>(), new Vector2(0.785f, 0.03f), new Vector2(0.97f, 0.19f));

        UnityEventTools.AddPersistentListener(corporeoButton.onClick, bm.OnCorporeoButton);
        UnityEventTools.AddPersistentListener(intelectoButton.onClick, bm.OnIntelectoButton);
        UnityEventTools.AddPersistentListener(motricidadButton.onClick, bm.OnMotricidadButton);
        UnityEventTools.AddPersistentListener(mundoInternoButton.onClick, bm.OnMundoInternoButton);

        bm.corporeoButton = corporeoButton;
        bm.intelectoButton = intelectoButton;
        bm.motricidadButton = motricidadButton;
        bm.mundoInternoButton = mundoInternoButton;

        // 11. Sub-panel de Intelecto y Objetos Curativos (IntelectoAndItemsPanel)
        GameObject intelectoAndItemsPanel = CreatePanel("IntelectoAndItemsPanel", combatPanel.transform, new Color(0.20f, 0.48f, 0.82f, 1f));
        SetAnchors(intelectoAndItemsPanel.GetComponent<RectTransform>(), new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.53f));

        GameObject intelectoInnerBox = CreatePanel("IntelectoInnerBox", intelectoAndItemsPanel.transform, new Color(0.03f, 0.03f, 0.04f, 0.99f));
        SetAnchors(intelectoInnerBox.GetComponent<RectTransform>(), new Vector2(0.003f, 0.008f), new Vector2(0.997f, 0.992f));

        TextMeshProUGUI intelectoTitleText = CreateTMPText(
            "IntelectoTitleText",
            intelectoInnerBox.transform,
            "SECCION 1: USAR OBJETOS CURATIVOS  |  SECCION 2: CRAFTEAR CON INTELECTO",
            26,
            TextAlignmentOptions.Center,
            pixelFont
        );
        intelectoTitleText.color = new Color(0.32f, 0.66f, 0.98f, 1f);
        SetAnchors(intelectoTitleText.rectTransform, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.98f));

        TextMeshProUGUI intelectoInventorySummaryText = CreateTMPText(
            "InventorySummaryText",
            intelectoInnerBox.transform,
            "INVENTARIO: Objetos curativos, herramientas y armas de Enrique",
            24,
            TextAlignmentOptions.Center,
            pixelFont
        );
        intelectoInventorySummaryText.color = boneWhite;
        SetAnchors(intelectoInventorySummaryText.rectTransform, new Vector2(0.02f, 0.74f), new Vector2(0.98f, 0.85f));

        GameObject dynamicButtonsGO = new GameObject("DynamicButtonsContainer", typeof(RectTransform));
        dynamicButtonsGO.transform.SetParent(intelectoInnerBox.transform, false);
        RectTransform dynamicButtonsRT = dynamicButtonsGO.GetComponent<RectTransform>();
        SetAnchors(dynamicButtonsRT, new Vector2(0.02f, 0.19f), new Vector2(0.98f, 0.73f));

        Button cancelIntelectoButton = CreateNoirButton(
            "CancelIntelectoButton",
            intelectoInnerBox.transform,
            "CANCELAR / VOLVER A LAS 4 VOCES (NO GASTA TURNO)",
            matteBlack,
            subtleBorder,
            24,
            pixelFont
        );
        SetAnchors(cancelIntelectoButton.GetComponent<RectTransform>(), new Vector2(0.22f, 0.025f), new Vector2(0.78f, 0.17f));
        UnityEventTools.AddPersistentListener(cancelIntelectoButton.onClick, bm.CloseIntelectoPanel);

        CombatIntelectoUI intelectoUI = intelectoAndItemsPanel.AddComponent<CombatIntelectoUI>();
        intelectoUI.battleManager = bm;
        intelectoUI.panelRoot = intelectoAndItemsPanel;
        intelectoUI.titleText = intelectoTitleText;
        intelectoUI.inventorySummaryText = intelectoInventorySummaryText;
        intelectoUI.dynamicButtonsContainer = dynamicButtonsRT;
        intelectoUI.cancelButton = cancelIntelectoButton;
        intelectoUI.customFont = pixelFont;

        bm.intelectoAndItemsPanel = intelectoAndItemsPanel;
        bm.intelectoCraftingPanel = intelectoAndItemsPanel;
        bm.intelectoCraftingUI = intelectoUI;
        intelectoAndItemsPanel.SetActive(false);

        // 12. Sub-panel de Mundo Interno (MundoInternoPanel)
        GameObject mundoInternoPanel = CreatePanel("MundoInternoPanel", combatPanel.transform, violetBorder);
        SetAnchors(mundoInternoPanel.GetComponent<RectTransform>(), new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.53f));

        GameObject mundoInternoInnerBox = CreatePanel("MundoInternoInnerBox", mundoInternoPanel.transform, new Color(0.03f, 0.03f, 0.04f, 0.99f));
        SetAnchors(mundoInternoInnerBox.GetComponent<RectTransform>(), new Vector2(0.003f, 0.008f), new Vector2(0.997f, 0.992f));

        TextMeshProUGUI mundoInternoTitleText = CreateTMPText(
            "MundoInternoTitleText",
            mundoInternoInnerBox.transform,
            "MUNDO INTERNO: PSICOLOGIA Y EMOCIONES DE ENRIQUE",
            26,
            TextAlignmentOptions.Center,
            pixelFont
        );
        mundoInternoTitleText.color = new Color(0.78f, 0.45f, 0.98f, 1f);
        SetAnchors(mundoInternoTitleText.rectTransform, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.98f));

        TextMeshProUGUI mundoInternoDescText = CreateTMPText(
            "MundoInternoDescriptionText",
            mundoInternoInnerBox.transform,
            "Elige como influir en la psique del combate (no gasta turno si cancelas):",
            22,
            TextAlignmentOptions.Center,
            pixelFont
        );
        mundoInternoDescText.color = boneWhite;
        SetAnchors(mundoInternoDescText.rectTransform, new Vector2(0.02f, 0.73f), new Vector2(0.98f, 0.85f));

        Button insultarBtn = CreateNoirButton(
            "InsultarButton",
            mundoInternoInnerBox.transform,
            "[1. INSULTAR] - Provoca al enemigo reduciendo su ataque (-4 ATQ)",
            new Color(0.07f, 0.04f, 0.06f, 1f),
            new Color(0.85f, 0.25f, 0.45f, 1f),
            24,
            pixelFont
        );
        SetAnchors(insultarBtn.GetComponent<RectTransform>(), new Vector2(0.05f, 0.53f), new Vector2(0.95f, 0.70f));

        Button animarseBtn = CreateNoirButton(
            "AnimarseButton",
            mundoInternoInnerBox.transform,
            "[2. ANIMARSE] - Enrique se auto-sugestiona (+5 ATQ para el proximo golpe Corporeo)",
            new Color(0.07f, 0.06f, 0.03f, 1f),
            new Color(0.92f, 0.72f, 0.22f, 1f),
            24,
            pixelFont
        );
        SetAnchors(animarseBtn.GetComponent<RectTransform>(), new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.51f));

        Button delirioBtn = CreateNoirButton(
            "DelirioButton",
            mundoInternoInnerBox.transform,
            "[3. DELIRIO PSIQUICO] - Confunde al rival haciendole perder su proximo turno",
            new Color(0.05f, 0.03f, 0.07f, 1f),
            violetBorder,
            24,
            pixelFont
        );
        SetAnchors(delirioBtn.GetComponent<RectTransform>(), new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.33f));

        Button cancelMundoInternoButton = CreateNoirButton(
            "CancelMundoInternoButton",
            mundoInternoInnerBox.transform,
            "CANCELAR / VOLVER A LAS 4 VOCES (NO GASTA TURNO)",
            matteBlack,
            subtleBorder,
            22,
            pixelFont
        );
        SetAnchors(cancelMundoInternoButton.GetComponent<RectTransform>(), new Vector2(0.22f, 0.02f), new Vector2(0.78f, 0.14f));

        CombatMundoInternoUI mundoInternoUI = mundoInternoPanel.AddComponent<CombatMundoInternoUI>();
        mundoInternoUI.battleManager = bm;
        mundoInternoUI.panelRoot = mundoInternoPanel;
        mundoInternoUI.titleText = mundoInternoTitleText;
        mundoInternoUI.descriptionText = mundoInternoDescText;
        mundoInternoUI.insultarButton = insultarBtn;
        mundoInternoUI.animarseButton = animarseBtn;
        mundoInternoUI.delirioButton = delirioBtn;
        mundoInternoUI.cancelButton = cancelMundoInternoButton;
        mundoInternoUI.insultarText = insultarBtn.GetComponentInChildren<TextMeshProUGUI>();
        mundoInternoUI.animarseText = animarseBtn.GetComponentInChildren<TextMeshProUGUI>();
        mundoInternoUI.delirioText = delirioBtn.GetComponentInChildren<TextMeshProUGUI>();

        UnityEventTools.AddPersistentListener(insultarBtn.onClick, mundoInternoUI.OnInsultarClicked);
        UnityEventTools.AddPersistentListener(animarseBtn.onClick, mundoInternoUI.OnAnimarseClicked);
        UnityEventTools.AddPersistentListener(delirioBtn.onClick, mundoInternoUI.OnDelirioClicked);
        UnityEventTools.AddPersistentListener(cancelMundoInternoButton.onClick, mundoInternoUI.OnCancelClicked);

        bm.mundoInternoPanel = mundoInternoPanel;
        bm.mundoInternoUI = mundoInternoUI;
        mundoInternoPanel.SetActive(false);

        // 13. Panel de Diálogos en Combate (CombatDialoguePanel)
        GameObject combatDialoguePanel = CreatePanel("CombatDialoguePanel", combatPanel.transform, subtleBorder);
        SetAnchors(combatDialoguePanel.GetComponent<RectTransform>(), new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.36f));

        GameObject combatDialogueInner = CreatePanel("CombatDialogueBoxBG", combatDialoguePanel.transform, new Color(0.04f, 0.04f, 0.05f, 0.99f));
        SetAnchors(combatDialogueInner.GetComponent<RectTransform>(), new Vector2(0.002f, 0.008f), new Vector2(0.998f, 0.992f));

        GameObject combatPortraitContainer = CreatePanel("CombatPortraitContainer", combatDialogueInner.transform, subtleBorder);
        SetAnchors(combatPortraitContainer.GetComponent<RectTransform>(), new Vector2(0.01f, 0.04f), new Vector2(0.195f, 1.55f));

        GameObject combatPortraitGO = CreatePanel("CombatPortraitImage", combatPortraitContainer.transform, new Color(0.38f, 0.40f, 0.43f, 1f));
        Image combatPortraitImage = combatPortraitGO.GetComponent<Image>();
        SetAnchors(combatPortraitGO.GetComponent<RectTransform>(), new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.97f));

        TextMeshProUGUI combatPortraitLabelText = CreateTMPText("CombatPortraitLabelText", combatPortraitGO.transform, "RETRATO", 32, TextAlignmentOptions.Center, pixelFont);
        combatPortraitLabelText.color = Color.white;
        SetAnchors(combatPortraitLabelText.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));

        TextMeshProUGUI combatSpeakerNameText = CreateTMPText("CombatSpeakerNameText", combatDialogueInner.transform, "VOZ: CORPOREO", 44, TextAlignmentOptions.BottomLeft, pixelFont);
        combatSpeakerNameText.color = boneWhite;
        SetAnchors(combatSpeakerNameText.rectTransform, new Vector2(0.205f, 0.73f), new Vector2(0.815f, 0.95f));

        TextMeshProUGUI combatDialogueText = CreateTMPText("CombatDialogueText", combatDialogueInner.transform, "Texto de dialogo en combate...", 36, TextAlignmentOptions.TopLeft, pixelFont);
        combatDialogueText.color = boneWhite;
        SetAnchors(combatDialogueText.rectTransform, new Vector2(0.205f, 0.06f), new Vector2(0.815f, 0.70f));

        Button combatNextDialogueButton = CreateNoirButton("CombatNextDialogueButton", combatDialogueInner.transform, "SIGUIENTE >>\n(ESPACIO)", matteBlack, subtleBorder, 28, pixelFont);
        SetAnchors(combatNextDialogueButton.GetComponent<RectTransform>(), new Vector2(0.83f, 0.18f), new Vector2(0.985f, 0.82f));
        UnityEventTools.AddPersistentListener(combatNextDialogueButton.onClick, bm.AdvanceCombatDialogue);

        bm.combatDialoguePanel = combatDialoguePanel;
        bm.combatPortraitContainer = combatPortraitContainer;
        bm.combatPortraitImage = combatPortraitImage;
        bm.combatPortraitLabelText = combatPortraitLabelText;
        bm.combatSpeakerNameText = combatSpeakerNameText;
        bm.combatDialogueText = combatDialogueText;
        bm.combatNextDialogueButton = combatNextDialogueButton;
        combatDialoguePanel.SetActive(false);

        // 14. Panel de Game Over (GameOverPanel)
        Transform existingGameOver = canvasGO.transform.Find("GameOverPanel");
        if (existingGameOver != null) Object.DestroyImmediate(existingGameOver.gameObject);

        GameObject gameOverPanel = CreatePanel("GameOverPanel", canvasGO.transform, pureBlack);
        GameObject innerBorder = CreatePanel("GameOverBorderFrame", gameOverPanel.transform, crimsonBorder);
        SetAnchors(innerBorder.GetComponent<RectTransform>(), new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));

        GameObject innerBox = CreatePanel("GameOverInnerBox", innerBorder.transform, matteBlack);
        SetAnchors(innerBox.GetComponent<RectTransform>(), new Vector2(0.005f, 0.008f), new Vector2(0.995f, 0.992f));

        TextMeshProUGUI gameOverTitle = CreateTMPText("GameOverTitleText", innerBox.transform, "GAME OVER", 96, TextAlignmentOptions.Center, pixelFont);
        gameOverTitle.color = new Color(0.85f, 0.18f, 0.18f, 1f);
        SetAnchors(gameOverTitle.rectTransform, new Vector2(0.06f, 0.66f), new Vector2(0.94f, 0.90f));

        TextMeshProUGUI gameOverSubtitle = CreateTMPText("GameOverSubtitleText", innerBox.transform, "FIN DEL RECORRIDO EN EL CALLEJÓN DE LAS SOMBRAS", 38, TextAlignmentOptions.Center, pixelFont);
        gameOverSubtitle.color = boneWhite;
        SetAnchors(gameOverSubtitle.rectTransform, new Vector2(0.06f, 0.53f), new Vector2(0.94f, 0.66f));

        TextMeshProUGUI gameOverNarrative = CreateTMPText(
            "GameOverNarrativeText",
            innerBox.transform,
            "Enrique y el Detective Vargas cayeron sobre el asfalto sucio de la guarida.\nEl Capataz impuso su ley de hierro y el rastro de El Calacas se perdio para siempre en las sombras.",
            26,
            TextAlignmentOptions.Center,
            pixelFont
        );
        gameOverNarrative.color = new Color(0.68f, 0.68f, 0.72f, 1f);
        SetAnchors(gameOverNarrative.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.52f));

        Button retryButton = CreateNoirButton("RetryBattleButton", innerBox.transform, "REINTENTAR EN EL MAPA\n(ESPACIO / R)", matteBlack, crimsonBorder, 26, pixelFont);
        SetAnchors(retryButton.GetComponent<RectTransform>(), new Vector2(0.10f, 0.10f), new Vector2(0.47f, 0.27f));

        Button menuButton = CreateNoirButton("ReturnToMenuButton", innerBox.transform, "VOLVER AL MENU PRINCIPAL\n(M / ESC)", matteBlack, subtleBorder, 26, pixelFont);
        SetAnchors(menuButton.GetComponent<RectTransform>(), new Vector2(0.53f, 0.10f), new Vector2(0.90f, 0.27f));

        UnityEventTools.AddPersistentListener(retryButton.onClick, bm.RetryFromGameOver);
        UnityEventTools.AddPersistentListener(menuButton.onClick, bm.ReturnToMainMenu);

        bm.gameOverPanel = gameOverPanel;
        bm.retryBattleButton = retryButton;
        bm.returnToMenuButton = menuButton;
        bm.menuSceneName = "00_MenuIntro";
        gameOverPanel.SetActive(false);

        // 15. Cargar retratos de personalidades para diálogos y combate
        LoadPersonalityPortraits(out Sprite cPort, out Sprite iPort, out Sprite mPort, out Sprite muPort);
        bm.corporeoPortrait = cPort;
        bm.intelectoPortrait = iPort;
        bm.motricidadPortrait = mPort;
        bm.mundoInternoPortrait = muPort;

        // 16. Cargar frames de animaciones GIF
        ConfigureActionGifs(bm);

        // 17. Barra de estado en exploración (ExplorationPlayerHUD)
        BuildExplorationPlayerHUD(canvasGO, pStats, pixelFont);

        // 18. Asegurar que sub-paneles se ordenen al frente
        if (actionGifPanel != null) actionGifPanel.transform.SetAsLastSibling();
        if (intelectoAndItemsPanel != null) intelectoAndItemsPanel.transform.SetAsLastSibling();
        if (mundoInternoPanel != null) mundoInternoPanel.transform.SetAsLastSibling();
        if (combatDialoguePanel != null) combatDialoguePanel.transform.SetAsLastSibling();

        combatPanel.SetActive(false);

        EditorUtility.SetDirty(bm);
        EditorUtility.SetDirty(intelectoUI);
        EditorUtility.SetDirty(mundoInternoUI);

        DetectiveActionMenu oldDetMenu = bm.GetComponentInChildren<DetectiveActionMenu>(true);
        if (oldDetMenu != null)
        {
            if (oldDetMenu.menuRootPanel != null) oldDetMenu.menuRootPanel.SetActive(false);
            if (oldDetMenu.detectiveAllySlotRoot != null) Object.DestroyImmediate(oldDetMenu.detectiveAllySlotRoot);
            Object.DestroyImmediate(oldDetMenu.gameObject);
        }

        return bm;
    }

    private static void BuildExplorationPlayerHUD(GameObject canvasGO, PlayerStats pStats, TMP_FontAsset pixelFont)
    {
        Transform existingHUD = canvasGO.transform.Find("ExplorationPlayerHUD");
        if (existingHUD != null)
        {
            Object.DestroyImmediate(existingHUD.gameObject);
        }

        GameObject hudGO = CreatePanel("ExplorationPlayerHUD", canvasGO.transform, new Color(0.35f, 0.35f, 0.35f, 0.90f));
        SetAnchors(hudGO.GetComponent<RectTransform>(), new Vector2(0.02f, 0.88f), new Vector2(0.24f, 0.975f));

        GameObject hudBg = CreatePanel("Background", hudGO.transform, new Color(0.07f, 0.07f, 0.07f, 0.92f));
        SetAnchors(hudBg.GetComponent<RectTransform>(), new Vector2(0.01f, 0.04f), new Vector2(0.99f, 0.96f));

        TextMeshProUGUI nameText = CreateTMPText("NameText", hudBg.transform, "ENRIQUE BOSQUES", 20, TextAlignmentOptions.MidlineLeft, pixelFont);
        nameText.color = new Color(0.92f, 0.90f, 0.86f, 1f);
        SetAnchors(nameText.rectTransform, new Vector2(0.04f, 0.52f), new Vector2(0.65f, 0.94f));

        TextMeshProUGUI pctText = CreateTMPText("PctText", hudBg.transform, "100%", 22, TextAlignmentOptions.MidlineRight, pixelFont);
        pctText.color = new Color(0.22f, 0.85f, 0.38f, 1f);
        SetAnchors(pctText.rectTransform, new Vector2(0.65f, 0.52f), new Vector2(0.96f, 0.94f));

        GameObject barContainer = CreatePanel("BarContainer", hudBg.transform, new Color(0.15f, 0.15f, 0.15f, 1f));
        SetAnchors(barContainer.GetComponent<RectTransform>(), new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.38f));

        GameObject hudDamageFill = CreatePanel("DamageFill", barContainer.transform, new Color(0.88f, 0.14f, 0.14f, 0.75f));
        SetAnchors(hudDamageFill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        GameObject hudMainFill = CreatePanel("MainFill", barContainer.transform, new Color(0.22f, 0.85f, 0.38f, 1f));
        SetAnchors(hudMainFill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        PlayerHealthBarUI hudHealthBar = hudGO.AddComponent<PlayerHealthBarUI>();
        hudHealthBar.playerStats = pStats;
        hudHealthBar.barBorderImage = hudGO.GetComponent<Image>();
        hudHealthBar.barBackgroundImage = barContainer.GetComponent<Image>();
        hudHealthBar.damageFillRect = hudDamageFill.GetComponent<RectTransform>();
        hudHealthBar.damageFillImage = hudDamageFill.GetComponent<Image>();
        hudHealthBar.mainFillRect = hudMainFill.GetComponent<RectTransform>();
        hudHealthBar.mainFillImage = hudMainFill.GetComponent<Image>();
        hudHealthBar.percentageText = pctText;
        hudHealthBar.healthyColor = new Color(0.22f, 0.85f, 0.38f, 1f);
        hudHealthBar.percentageColor = new Color(0.22f, 0.85f, 0.38f, 1f);
        hudHealthBar.damageLagColor = new Color(0.88f, 0.14f, 0.14f, 0.75f);
        hudHealthBar.autoSyncWithPlayerStats = true;
        hudHealthBar.hideWhenCombatActive = true;
        if (pStats != null)
        {
            hudHealthBar.SetHealthInstant(pStats.currentHP, pStats.maxHP);
        }
    }

    private static void ConfigureActionGifs(BattleManager bm)
    {
        if (bm == null) return;
        List<Sprite> attackFrames = new List<Sprite>();
        var allAtk = AssetDatabase.LoadAllAssetsAtPath("Assets/Scripts/AnimacionAtaque.png");
        if (allAtk != null)
        {
            foreach (var o in allAtk) { if (o is Sprite s) attackFrames.Add(s); }
        }

        if (attackFrames.Count > 0)
        {
            attackFrames.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            if (bm.corporeoGif == null) bm.corporeoGif = new ActionGifData();
            bm.corporeoGif.gifFrames = attackFrames.ToArray();

            if (bm.enemyAttackGif == null) bm.enemyAttackGif = new ActionGifData();
            bm.enemyAttackGif.gifFrames = attackFrames.ToArray();
        }

        List<Sprite> expFrames = new List<Sprite>();
        var allExp = AssetDatabase.LoadAllAssetsAtPath("Assets/Scripts/AtaqueExplosion.png");
        if (allExp != null)
        {
            foreach (var o in allExp) { if (o is Sprite s) expFrames.Add(s); }
        }

        if (expFrames.Count > 0)
        {
            expFrames.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            if (bm.explosionGif == null) bm.explosionGif = new ActionGifData();
            bm.explosionGif.gifFrames = expFrames.ToArray();
        }
    }

    private static void LoadPersonalityPortraits(
        out Sprite corporeoPortrait,
        out Sprite intelectoPortrait,
        out Sprite motricidadPortrait,
        out Sprite mundoInternoPortrait)
    {
        corporeoPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Portraits/Musculatura.png");
        intelectoPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Portraits/Intelecto.png");
        motricidadPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Portraits/Motricidad.png");
        mundoInternoPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Portraits/MundoInterno.png");
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
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
        tmp.enableAutoSizing = false;
        tmp.characterSpacing = 2f;
        tmp.wordSpacing = 8f;
        tmp.lineSpacing = 10f;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        PlayerHealthBarUI.EnsureCleanPixelFont(tmp);
        return tmp;
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
        txt.color = new Color(0.92f, 0.90f, 0.86f, 1f);
        SetAnchors(txt.rectTransform, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.94f));

        return btn;
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

    private static EnemyUISlot CreateEnemyUISlot(
        string slotName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string initialTitle,
        string initialHP,
        Color avatarColor,
        string avatarLabel,
        Color initialBorderColor,
        Color matteBlack,
        Color subtleBorder,
        TMP_FontAsset font)
    {
        GameObject slotRoot = new GameObject(slotName, typeof(RectTransform), typeof(Image), typeof(Button));
        slotRoot.transform.SetParent(parent, false);
        SetAnchors(slotRoot.GetComponent<RectTransform>(), anchorMin, anchorMax);

        Image clickCatcher = slotRoot.GetComponent<Image>();
        clickCatcher.color = new Color(0f, 0f, 0f, 0f);
        clickCatcher.raycastTarget = true;

        Button selectBtn = slotRoot.GetComponent<Button>();
        selectBtn.targetGraphic = clickCatcher;

        GameObject ringGO = CreatePanel("GroundIndicatorRing", slotRoot.transform, new Color(0f, 0f, 0f, 0.32f));
        SetAnchors(ringGO.GetComponent<RectTransform>(), new Vector2(0.15f, 0.00f), new Vector2(0.85f, 0.10f));
        ringGO.GetComponent<Image>().raycastTarget = false;

        GameObject avatarGO = CreatePanel("EnemyPlaceholderImage", slotRoot.transform, Color.white);
        SetAnchors(avatarGO.GetComponent<RectTransform>(), new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.77f));
        Image placeholderImage = avatarGO.GetComponent<Image>();
        placeholderImage.preserveAspect = true;
        placeholderImage.raycastTarget = false;

        TextMeshProUGUI labelText = CreateTMPText(
            "EnemyAvatarLabel",
            avatarGO.transform,
            avatarLabel,
            30,
            TextAlignmentOptions.Center,
            font
        );
        labelText.color = Color.white;
        SetAnchors(labelText.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
        labelText.gameObject.SetActive(false);

        GameObject nameplateBorder = CreatePanel("EnemyNameplateBorder", slotRoot.transform, subtleBorder);
        SetAnchors(nameplateBorder.GetComponent<RectTransform>(), new Vector2(0.06f, 0.77f), new Vector2(0.94f, 0.89f));
        nameplateBorder.GetComponent<Image>().raycastTarget = false;

        GameObject nameplateBg = CreatePanel("EnemyNameplateBg", nameplateBorder.transform, new Color(0.06f, 0.06f, 0.09f, 0.85f));
        SetAnchors(nameplateBg.GetComponent<RectTransform>(), new Vector2(0.008f, 0.03f), new Vector2(0.992f, 0.97f));
        nameplateBg.GetComponent<Image>().raycastTarget = false;

        TextMeshProUGUI nameText = CreateTMPText(
            "EnemyNameText",
            nameplateBg.transform,
            initialTitle,
            24,
            TextAlignmentOptions.Center,
            font
        );
        nameText.color = new Color(0.90f, 0.88f, 0.84f, 1f);
        SetAnchors(nameText.rectTransform, new Vector2(0.02f, 0.50f), new Vector2(0.98f, 0.98f));

        TextMeshProUGUI hpText = CreateTMPText(
            "EnemyHPText",
            nameplateBg.transform,
            initialHP,
            22,
            TextAlignmentOptions.Center,
            font
        );
        hpText.color = new Color(0.90f, 0.38f, 0.38f, 1f);
        SetAnchors(hpText.rectTransform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.50f));

        GameObject targetIndicatorGO = new GameObject("TargetIndicator", typeof(RectTransform));
        targetIndicatorGO.transform.SetParent(slotRoot.transform, false);
        RectTransform indRT = targetIndicatorGO.GetComponent<RectTransform>();
        SetAnchors(indRT, new Vector2(0.22f, 0.89f), new Vector2(0.78f, 1.00f));

        GameObject badgeGO = CreatePanel("TargetBadge", targetIndicatorGO.transform, new Color(0.92f, 0.68f, 0.22f, 0.95f));
        SetAnchors(badgeGO.GetComponent<RectTransform>(), new Vector2(0.02f, 0.26f), new Vector2(0.98f, 0.98f));
        badgeGO.GetComponent<Image>().raycastTarget = false;

        TextMeshProUGUI badgeText = CreateTMPText("BadgeText", badgeGO.transform, "[ OBJETIVO ]", 20, TextAlignmentOptions.Center, font);
        badgeText.color = new Color(0.12f, 0.08f, 0.02f, 1f);
        badgeText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI arrowText = CreateTMPText("ArrowText", targetIndicatorGO.transform, "▼", 26, TextAlignmentOptions.Center, font);
        arrowText.color = new Color(0.96f, 0.78f, 0.22f, 1f);
        SetAnchors(arrowText.rectTransform, new Vector2(0.35f, 0.00f), new Vector2(0.65f, 0.32f));

        targetIndicatorGO.SetActive(false);

        return new EnemyUISlot
        {
            slotRoot = slotRoot,
            borderImage = nameplateBorder.GetComponent<Image>(),
            groundIndicatorRing = ringGO.GetComponent<Image>(),
            enemyPlaceholderImage = placeholderImage,
            enemyAvatarLabelText = labelText,
            enemyNameText = nameText,
            enemyHPText = hpText,
            targetIndicator = targetIndicatorGO,
            selectTargetButton = selectBtn
        };
    }
}
#endif
