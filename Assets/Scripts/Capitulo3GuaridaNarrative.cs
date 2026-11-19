using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Controlador y constructor del Capítulo 3: "La Guarida de El Calacas" (Nivel 3).
/// Construye un pasillo arquitectónico subterráneo limpio y lógico con paredes y losas de piedra,
/// columnas estructurales góticas espaciadas rítmicamente con antorchas animadas,
/// cajas de suministros y crafteo ordenadas contra las paredes, y al final del pasillo
/// exclusivamente al Jefe Final "El Calacas" (skin Gothicvania Player y máquina de combate de 3 fases).
/// </summary>
public class Capitulo3GuaridaNarrative : MonoBehaviour
{
    [Header("Referencias de UI")]
    public CanvasGroup fadeCanvasGroup;
    public GameObject bannerPanel;
    public TextMeshProUGUI chapterTitleText;
    public TextMeshProUGUI chapterSubtitleText;

    [Header("Compañero Investigador Privado")]
    public CompanionFollowAI investigatorCompanion;

    [Header("Gestor de Diálogo Noir")]
    public ExplorationDialogueManager explorationDialogueManager;

    [Header("Diálogo Inicial de Llegada a la Guarida")]
    [TextArea(2, 4)]
    public string[] arrivalDialogueLines = GetDefaultTutorialDialogueLines();

    public static string[] GetDefaultTutorialDialogueLines()
    {
        return new string[]
        {
            "NARRADOR: \"Tras derrotar al Capataz, la pesada compuerta blindada de acero se cierra a vuestras espaldas. Os encontráis en el búnker subterráneo de El Calacas.\"",
            "ENRIQUE BOSQUES: \"(Apretando los puños) El aire aquí abajo es denso... huele a pólvora, humedad y azufre.\"",
            "DETECTIVE VARGAS: \"Atento a los protocolos del búnker, Bosques. Este túnel es una trampa fortificada y no podremos llegar hasta El Calacas sin desactivar sus defensas.\"",
            "VOZ [MOTRICIDAD]: \"«TUTORIAL - PASO 1: SUMINISTROS Y CRAFTEO»\nRegistra con [E] las cajas y barriles pegados a los muros. Abre tu inventario con [I] para fabricar vendajes, armas o explosivos antes de cualquier combate.\"",
            "VOZ [INTELECTO]: \"«TUTORIAL - PASO 2: CENTINELA DEL CORREDOR»\nUn centinela custodia el pasillo hacia la terminal de seguridad. Derrótalo en combate o prepara tu equipo antes de acercarte a la compuerta.\"",
            "DETECTIVE VARGAS: \"«TUTORIAL - PASO 3: TERMINAL ÚNICA DE CABLES»\nSolo hay una terminal frente a la compuerta láser. Pulsa [E] en ella, conecta cada cable con el puerto del mismo color y corta los 3 disyuntores para abrir el paso.\"",
            "VOZ [MUNDO INTERNO]: \"«TUTORIAL - PASO 4: EL ENFRENTAMIENTO FINAL»\nAl desactivar la compuerta, la armería se abrirá y El Calacas estará esperándonos en el fondo del sanctum. Será la confrontación definitiva.\"",
            "DETECTIVE VARGAS: \"Tengo mi revólver .38 amartillado y listo. Despejemos el camino, desactivemos esa compuerta y acabemos con esta pesadilla juntos.\"",
            "[ TUTORIAL: 1. REVISA CAJAS CON [E] -> 2. VENCE AL CENTINELA -> 3. CONECTA CABLES Y DISYUNTORES [E] -> 4. DERROTA A EL CALACAS ]"
        };
    }

    private void Awake()
    {
        EnsureGuaridaMapAndSystemsReady();
    }

    private void Start()
    {
        PlayerHealthBarUI.EnsureCleanPixelFont(chapterTitleText);
        PlayerHealthBarUI.EnsureCleanPixelFont(chapterSubtitleText);
        StartCoroutine(IntroSequenceRoutine());
    }

    /// <summary>
    /// Garantiza que el mapa de la guarida, las colisiones, la cámara, Enrique, Vargas y El Calacas estén configurados.
    /// </summary>
    public void EnsureGuaridaMapAndSystemsReady()
    {
        arrivalDialogueLines = GetDefaultTutorialDialogueLines();

        GameObject root = GameObject.Find("Guarida_ElCalacas_Root");
        if (root != null && root.transform.Find("Guarida_Version_V6_Unified") == null)
        {
            // Desemparentar al Investigador Privado antes de reconstruir la raíz antigua
            CompanionFollowAI existingComp = root.GetComponentInChildren<CompanionFollowAI>(true);
            if (existingComp != null)
            {
                existingComp.transform.SetParent(null, true);
                investigatorCompanion = existingComp;
            }
            DestroyImmediate(root);
            root = null;
        }

        if (root == null)
        {
            root = BuildGuaridaEnvironment();
        }
        else
        {
            Transform puzzleZone = root.transform.Find("Zona_Puzzle_Seguridad_Guarida");
            if (puzzleZone != null)
            {
                Transform northNode = puzzleZone.Find("Terminal_Pista_AlfaBeta");
                if (northNode != null) DestroyImmediate(northNode.gameObject);
                Transform southNode = puzzleZone.Find("Terminal_Pista_GammaOmega");
                if (southNode != null) DestroyImmediate(southNode.gameObject);
            }
        }

        // Asegurar instancia del sistema UI del puzzle Noir
        GuaridaPuzzleMinigameUI.GetOrCreate();

        // Garantizar que todas las animaciones del jefe final usen exclusivamente el mismo sprite Gothicvania
        EnsureCalacasBossSpritesUnified(root);

        // Posicionar a Enrique al inicio del pasillo espacioso (x = -2.0, y = -0.75f) y ajustar su BoxCollider2D en los pies
        PlayerController2D player = FindAnyObjectByType<PlayerController2D>();
        if (player != null)
        {
            player.transform.position = new Vector3(-2.0f, -0.75f, 0f);
            player.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            BoxCollider2D pCol = player.GetComponent<BoxCollider2D>();
            if (pCol != null)
            {
                // Colisionador ajustado a los pies (mitad inferior) para movimiento libre y fluido en 2.5D
                pCol.offset = new Vector2(0f, -0.35f);
                pCol.size = new Vector2(0.50f, 0.40f);
                pCol.isTrigger = false;
            }
        }

        // Configurar cámara de seguimiento centrada en el pasillo amplio
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);
            mainCam.orthographicSize = 3.2f;

            CameraFollow2D camFollow = mainCam.GetComponent<CameraFollow2D>();
            if (camFollow == null) camFollow = mainCam.gameObject.AddComponent<CameraFollow2D>();

            camFollow.useBounds = true;
            camFollow.minBounds = new Vector2(0.0f, -0.75f);
            camFollow.maxBounds = new Vector2(20.0f, -0.75f);
            camFollow.smoothTime = 0.15f;
            if (player != null)
            {
                camFollow.target = player.transform;
                mainCam.transform.position = new Vector3(
                    Mathf.Clamp(player.transform.position.x, camFollow.minBounds.x, camFollow.maxBounds.x),
                    -0.75f,
                    -10f
                );
            }
        }

        // Configurar Detective acompañante
        if (investigatorCompanion == null) investigatorCompanion = FindAnyObjectByType<CompanionFollowAI>();
        if (investigatorCompanion != null)
        {
            investigatorCompanion.transform.position = new Vector3(-3.2f, -0.75f, 0f);
            investigatorCompanion.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            investigatorCompanion.isFollowingPlayer = true;
            investigatorCompanion.autoTriggerJoinDialogueOnApproach = false;
            investigatorCompanion.recruitmentDialogueLines = System.Array.Empty<string>();
            investigatorCompanion.followUpDialogueLines = System.Array.Empty<string>();
            DetectiveStats detStats = DetectiveStats.Instance;
            if (detStats != null)
            {
                detStats.hasDetectiveInParty = true;
            }
            Capitulo2IntroNarrative.EnsureGenericInvestigatorAppearance(investigatorCompanion);
        }

        // Configurar diálogo noir
        if (explorationDialogueManager == null) explorationDialogueManager = ExplorationDialogueManager.EnsureInScene();
        if (explorationDialogueManager != null)
        {
            if (player != null) explorationDialogueManager.playerController = player;
            Capitulo2IntroNarrative.UpgradeDialoguePanelToNoirStyle(explorationDialogueManager);
        }

        // Configurar Menú de Pausa Noir estandarizado (Escape / P)
        PauseMenuManager.EnsureInScene();

        // Configurar Canvases unificados de Inventario y Estadísticas
        PlayerInventoryAndWeaponsUI.EnsureInScene();
        PlayerLevelUpAndStatsUI.EnsureInScene();

        // Configurar inventario y recetas de crafteo (incluyendo todas las nuevas recetas de Intelecto)
        EnsureInventoryManagerContinuity();

        // Configurar BattleManager y estandarizar interfaz de combate
        BattleManager bm = BattleManager.EnsureInScene();
        if (bm != null)
        {
            bm.LoadSfxClipsIfMissing();
            bm.LoadPortraitsIfMissing();
            bm.EnforceUnifiedCombatUILayout();
            EnemyEncounterTrigger[] triggers = FindObjectsByType<EnemyEncounterTrigger>(FindObjectsInactive.Include);
            foreach (EnemyEncounterTrigger tr in triggers)
            {
                if (tr != null && tr.battleManager == null)
                {
                    tr.battleManager = bm;
                }
            }
        }

        // Ocultar cualquier texto o letrero flotante en el mapa
        PlayerController2D.HideAllWorldSpaceObjectLabels();
    }

    private static void EnsureInventoryManagerContinuity()
    {
        InventoryManager inv = FindAnyObjectByType<InventoryManager>();
        if (inv == null)
        {
            GameObject invGO = new GameObject("InventoryManager");
            inv = invGO.AddComponent<InventoryManager>();
        }

        inv.EnsureKnownRecipesLoaded();

        if (inv.preparedWeaponCatalog == null) inv.preparedWeaponCatalog = new List<ItemData>();
        if (inv.preparedWeaponCatalog.Count == 0)
        {
            ItemData w1 = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Arma_CadenaBicicleta.asset");
            ItemData w2 = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Arma_TuboPlomo.asset");
            ItemData w3 = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Arma_BatMadera.asset");
            if (w1 != null) inv.preparedWeaponCatalog.Add(w1);
            if (w2 != null) inv.preparedWeaponCatalog.Add(w2);
            if (w3 != null) inv.preparedWeaponCatalog.Add(w3);
        }
    }

    private IEnumerator IntroSequenceRoutine()
    {
        PlayerController2D player = FindAnyObjectByType<PlayerController2D>();
        if (player != null) player.SetCanMove(false);

        if (bannerPanel == null)
        {
            GameObject bp = GameObject.Find("Nivel3Banner");
            if (bp != null) bannerPanel = bp;
        }
        if ((chapterTitleText == null || chapterSubtitleText == null) && bannerPanel != null)
        {
            TextMeshProUGUI[] bannerTexts = bannerPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (chapterTitleText == null && bannerTexts.Length > 0)
            {
                chapterTitleText = bannerTexts[0];
            }
            if (chapterSubtitleText == null && bannerTexts.Length > 1)
            {
                chapterSubtitleText = bannerTexts[1];
            }
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true;
        }

        if (bannerPanel != null) bannerPanel.SetActive(true);
        if (chapterTitleText != null)
        {
            PlayerHealthBarUI.EnsureCleanPixelFont(chapterTitleText);
            chapterTitleText.text = "CAPÍTULO 3: LA GUARIDA DE EL CALACAS";
        }
        if (chapterSubtitleText != null)
        {
            PlayerHealthBarUI.EnsureCleanPixelFont(chapterSubtitleText);
            chapterSubtitleText.text = "El Búnker Subterráneo — El Enfrentamiento Final";
        }

        yield return new WaitForSeconds(2.0f);

        if (bannerPanel != null) bannerPanel.SetActive(false);

        float elapsed = 0f;
        float duration = 1.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
            }
            yield return null;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        if (explorationDialogueManager != null && arrivalDialogueLines != null && arrivalDialogueLines.Length > 0)
        {
            explorationDialogueManager.StartDialogue(arrivalDialogueLines, () =>
            {
                if (investigatorCompanion != null) investigatorCompanion.ActivateCompanionFollow();
                if (player != null) player.SetCanMove(true);
            });
        }
        else if (player != null)
        {
            if (investigatorCompanion != null) investigatorCompanion.ActivateCompanionFollow();
            player.SetCanMove(true);
        }
    }

    // =========================================================================================
    // CONSTRUCCIÓN ARQUITECTÓNICA DE LA GUARIDA: PASILLO DIRECTO AL JEFE FINAL
    // =========================================================================================

    public static GameObject BuildGuaridaEnvironment()
    {
        GameObject root = new GameObject("Guarida_ElCalacas_Root");

        // Marcador de versión para asegurar que las escenas con versiones previas se actualicen limpiamente
        GameObject versionMarker = new GameObject("Guarida_Version_V6_Unified");
        versionMarker.transform.SetParent(root.transform, false);

        // Sprites limpios y con resolución adecuada
        Sprite wallSprite = GetProceduralStoneWallSprite();
        Sprite floorSlabSprite = GetProceduralFloorSlabSprite();
        Sprite ceilingSlabSprite = GetProceduralCeilingSlabSprite();
        Sprite blastDoorSprite = GetProceduralBlastDoorSprite();
        Sprite colSprite = LoadSpriteResource("Assets/NewStuff/gothicvania church files/Assets/ENVIRONMENT/column.png", 32f);
        Sprite barrelSprite = LoadSpriteResource("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Barrel_red_1.png", 16f);

        // Sprites animados de antorchas
        Sprite[] torchFrames = new Sprite[]
        {
            LoadSpriteResource("Assets/NewStuff/torch_1.png", 16f),
            LoadSpriteResource("Assets/NewStuff/torch_2.png", 16f),
            LoadSpriteResource("Assets/NewStuff/torch_3.png", 16f),
            LoadSpriteResource("Assets/NewStuff/torch_4.png", 16f)
        };

        // -------------------------------------------------------------------------------------
        // 1. PARED DE FONDO CONTINUA (Mampostería de piedra subterránea)
        // Se eleva en 4 filas verticales desde el zócalo superior (y = 1.35f, 2.35f, 3.35f, 4.35f)
        // -------------------------------------------------------------------------------------
        GameObject bgGroup = new GameObject("Fondo_Paredes_Catacumbas");
        bgGroup.transform.SetParent(root.transform, false);

        float[] rowY = new float[] { 1.35f, 2.35f, 3.35f, 4.35f };
        for (float x = -6.0f; x <= 26.0f; x += 1.0f)
        {
            for (int r = 0; r < rowY.Length; r++)
            {
                GameObject bgBlock = new GameObject($"WallBlock_{x:F0}_{r}");
                bgBlock.transform.SetParent(bgGroup.transform, false);
                bgBlock.transform.position = new Vector3(x, rowY[r], 0f);
                bgBlock.transform.localScale = Vector3.one;

                SpriteRenderer bsr = bgBlock.AddComponent<SpriteRenderer>();
                bsr.sprite = wallSprite;
                bsr.color = (r == 0)
                    ? new Color(0.65f, 0.65f, 0.72f, 1f)
                    : new Color(0.85f, 0.85f, 0.90f, 1f);
                bsr.sortingOrder = 0;
            }
        }

        // -------------------------------------------------------------------------------------
        // 2. PISO TRANSITABLE AMPLIO (5 hileras de losas de piedra desde y = -2.40f hasta y = +0.60f)
        // Proporciona un carril peatonal amplio de ~2.9 unidades de altura libre
        // -------------------------------------------------------------------------------------
        GameObject floorGroup = new GameObject("Piso_Losas_Mazmorra");
        floorGroup.transform.SetParent(root.transform, false);

        float[] floorRowsY = new float[] { -3.90f, -3.15f, -2.40f, -1.65f, -0.90f, -0.15f, 0.60f };
        for (float x = -6.0f; x <= 26.0f; x += 1.0f)
        {
            for (int f = 0; f < floorRowsY.Length; f++)
            {
                GameObject slab = new GameObject($"FloorSlab_{f}_{x:F0}");
                slab.transform.SetParent(floorGroup.transform, false);
                slab.transform.position = new Vector3(x, floorRowsY[f], 0f);
                slab.transform.localScale = Vector3.one;

                SpriteRenderer fsr = slab.AddComponent<SpriteRenderer>();
                fsr.sprite = floorSlabSprite;
                if (f < 2) fsr.color = new Color(0.48f, 0.48f, 0.54f, 1f);
                else if (f == 2) fsr.color = new Color(0.70f, 0.70f, 0.75f, 1f);
                else if (f == 6) fsr.color = new Color(0.75f, 0.75f, 0.82f, 1f);
                else fsr.color = new Color(0.88f, 0.88f, 0.94f, 1f);
                fsr.sortingOrder = 1;
            }
        }

        // -------------------------------------------------------------------------------------
        // 3. TECHO SÓLIDO (Cornisa de bóveda de piedra en y = 4.35f)
        // -------------------------------------------------------------------------------------
        GameObject roofGroup = new GameObject("Techo_Boveda_Mazmorra");
        roofGroup.transform.SetParent(root.transform, false);

        for (float x = -6.0f; x <= 26.0f; x += 1.0f)
        {
            GameObject corn = new GameObject($"RoofCornice_{x:F0}");
            corn.transform.SetParent(roofGroup.transform, false);
            corn.transform.position = new Vector3(x, 4.35f, 0f);
            corn.transform.localScale = Vector3.one;

            SpriteRenderer rsr = corn.AddComponent<SpriteRenderer>();
            rsr.sprite = ceilingSlabSprite;
            rsr.color = Color.white;
            rsr.sortingOrder = 1;
        }

        // -------------------------------------------------------------------------------------
        // 4. COLISIONADORES SÓLIDOS DE ARQUITECTURA (BOXCOLLIDERS 2D AMPLIOS)
        // Carril peatonal espacioso:
        //   - Borde inferior (Col_Piso): y = -2.25f
        //   - Borde superior (Col_MuroSuperior_Pared): y = +0.70f
        //   - Altura libre para caminar: ~2.95 unidades
        //   - Borde izquierdo: x = -5.20f | Borde derecho: x = 25.80f
        // -------------------------------------------------------------------------------------
        GameObject collidersGroup = new GameObject("Colisiones_Guarida");
        collidersGroup.transform.SetParent(root.transform, false);

        // Colisionador inferior (Borde del Piso en y = -2.25f)
        GameObject colFloor = new GameObject("Col_Piso");
        colFloor.transform.SetParent(collidersGroup.transform, false);
        colFloor.transform.position = new Vector3(10.0f, -3.75f, 0f);
        BoxCollider2D boxFloor = colFloor.AddComponent<BoxCollider2D>();
        boxFloor.size = new Vector2(38.0f, 3.0f);
        boxFloor.isTrigger = false;

        // Colisionador superior (Zócalo de la Pared en y = +0.70f)
        GameObject colRoof = new GameObject("Col_MuroSuperior_Pared");
        colRoof.transform.SetParent(collidersGroup.transform, false);
        colRoof.transform.position = new Vector3(10.0f, 1.80f, 0f);
        BoxCollider2D boxRoof = colRoof.AddComponent<BoxCollider2D>();
        boxRoof.size = new Vector2(38.0f, 2.2f);
        boxRoof.isTrigger = false;

        // Muro izquierdo (Puerta de acero sellada tras el ingreso)
        GameObject colLeft = new GameObject("Col_MuroIzquierdo");
        colLeft.transform.SetParent(collidersGroup.transform, false);
        colLeft.transform.position = new Vector3(-5.20f, -0.75f, 0f);
        BoxCollider2D boxLeft = colLeft.AddComponent<BoxCollider2D>();
        boxLeft.size = new Vector2(2.0f, 8.0f);
        boxLeft.isTrigger = false;

        // Muro derecho (Fondo del Sanctum de El Calacas)
        GameObject colRight = new GameObject("Col_MuroDerecho");
        colRight.transform.SetParent(collidersGroup.transform, false);
        colRight.transform.position = new Vector3(25.80f, -0.75f, 0f);
        BoxCollider2D boxRight = colRight.AddComponent<BoxCollider2D>();
        boxRight.size = new Vector2(2.0f, 8.0f);
        boxRight.isTrigger = false;

        // -------------------------------------------------------------------------------------
        // 5. PUERTA BLINDADA DE ENTRADA (Izquierda, x = -4.20f)
        // -------------------------------------------------------------------------------------
        GameObject doorGO = new GameObject("Puerta_Acero_Entrada");
        doorGO.transform.SetParent(root.transform, false);
        doorGO.transform.position = new Vector3(-4.20f, -0.40f, 0f);
        doorGO.transform.localScale = new Vector3(1.2f, 1.6f, 1.0f);
        SpriteRenderer doorSR = doorGO.AddComponent<SpriteRenderer>();
        doorSR.sprite = blastDoorSprite;
        doorSR.sortingOrder = 3;

        // -------------------------------------------------------------------------------------
        // 6. COLUMNAS GÓTICAS RÍTMICAS E ILUMINACIÓN CON ANTORCHAS
        // -------------------------------------------------------------------------------------
        GameObject columnsGroup = new GameObject("Columnas_Y_Antorchas");
        columnsGroup.transform.SetParent(root.transform, false);

        float[] columnPositions = new float[] { -2.5f, 2.5f, 7.5f, 12.5f, 17.5f, 22.5f };
        for (int i = 0; i < columnPositions.Length; i++)
        {
            float cx = columnPositions[i];
            GameObject colGO = new GameObject($"ColumnaGotica_{cx:F1}");
            colGO.transform.SetParent(columnsGroup.transform, false);
            colGO.transform.position = new Vector3(cx, 1.35f, 0f);
            colGO.transform.localScale = new Vector3(0.55f, 0.65f, 1.0f);

            SpriteRenderer colSR = colGO.AddComponent<SpriteRenderer>();
            colSR.sprite = colSprite != null ? colSprite : GetProceduralColumnSprite();
            colSR.color = new Color(0.85f, 0.82f, 0.88f, 1f);
            colSR.sortingOrder = 2;

            // Antorcha animada sobre la columna
            if (torchFrames != null && torchFrames.Length > 0 && torchFrames[0] != null)
            {
                GameObject torchGO = new GameObject($"Antorcha_{cx:F1}");
                torchGO.transform.SetParent(colGO.transform, false);
                torchGO.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                torchGO.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

                SpriteRenderer torchSR = torchGO.AddComponent<SpriteRenderer>();
                torchSR.sprite = torchFrames[0];
                torchSR.sortingOrder = 6;

                SpriteFrameAnimator anim = torchGO.AddComponent<SpriteFrameAnimator>();
                anim.frames = torchFrames;
                anim.framesPerSecond = 8f;

                // Halo de iluminación cálida
                GameObject lightGlow = new GameObject("TorchLightGlow");
                lightGlow.transform.SetParent(torchGO.transform, false);
                lightGlow.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                lightGlow.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
                SpriteRenderer glowSR = lightGlow.AddComponent<SpriteRenderer>();
                glowSR.sprite = GetProceduralGlowSprite();
                glowSR.color = new Color(1f, 0.65f, 0.20f, 0.30f);
                glowSR.sortingOrder = 3;
            }
        }

        // -------------------------------------------------------------------------------------
        // 7. DECORACIÓN CON BOXCOLLIDER2D SÓLIDO (BARRILES CONTRA LAS ORILLAS)
        // Pegados a los bordes para dejar el carril central completamente despejado
        // -------------------------------------------------------------------------------------
        GameObject propsGroup = new GameObject("Props_Decoracion_Mazmorra");
        propsGroup.transform.SetParent(root.transform, false);

        Vector3[] barrelPositions = new Vector3[]
        {
            new Vector3(0.5f, 0.45f, 0f),
            new Vector3(6.2f, -1.95f, 0f),
            new Vector3(16.8f, -1.95f, 0f)
        };
        for (int i = 0; i < barrelPositions.Length; i++)
        {
            Vector3 bPos = barrelPositions[i];
            GameObject barrelGO = new GameObject($"BarrilSuelo_{i + 1}");
            barrelGO.transform.SetParent(propsGroup.transform, false);
            barrelGO.transform.position = bPos;
            barrelGO.transform.localScale = Vector3.one;

            SpriteRenderer bsr = barrelGO.AddComponent<SpriteRenderer>();
            bsr.sprite = barrelSprite != null ? barrelSprite : GetProceduralStoneSprite(new Color(0.35f, 0.25f, 0.18f, 1f));
            bsr.sortingOrder = 4;
            float sprH = Mathf.Max(0.05f, bsr.sprite.bounds.size.y);
            float barScale = 0.90f / sprH;
            barrelGO.transform.localScale = new Vector3(barScale, barScale, 1f);

            // BoxCollider2D sólido únicamente en los pies del barril
            BoxCollider2D barrelCol = barrelGO.AddComponent<BoxCollider2D>();
            barrelCol.isTrigger = false;
            barrelCol.offset = new Vector2(0f, -0.22f / barScale);
            barrelCol.size = new Vector2(0.50f / barScale, 0.25f / barScale);
        }

        // -------------------------------------------------------------------------------------
        // 8. CAJAS DE SUMINISTROS Y CRAFTEO (Loot ordenado sobre el piso con colisión sólida + trigger)
        // -------------------------------------------------------------------------------------
        SpawnGuaridaLootBoxes(root.transform);

        // -------------------------------------------------------------------------------------
        // 9. ZONA DE PUZZLE DE SEGURIDAD Y GUARDIA DEL BÚNKER (HACE EL NIVEL 3 INTERESANTE)
        // - 2 Nodos de Pista y Suministros (x = 4.2f y x = 11.2f)
        // - 1 Centinela de la Guarida antes de la Compuerta (x = 7.2f)
        // - 1 Compuerta Blindada con Barrera Láser y Consola de Hackeo Noir (x = 14.2f)
        // -------------------------------------------------------------------------------------
        SpawnGuaridaSecurityPuzzleZone(root.transform);
        SpawnGuaridaCorridorGuard(root.transform);

        // -------------------------------------------------------------------------------------
        // 10. JEFE FINAL: EL CALACAS AL FINAL DEL SANCTUM (x = 22.0f, y = -0.75f)
        // -------------------------------------------------------------------------------------
        SpawnCalacasBoss(root.transform);

        return root;
    }

    // =========================================================================================
    // CAJAS DE SUMINISTROS Y CRAFTEO (INTERACTABLE LOOT ORDENADO)
    // =========================================================================================

    private static void SpawnGuaridaLootBoxes(Transform parent)
    {
        GameObject cratesGroup = new GameObject("Cajas_Suministros_Crafteo");
        cratesGroup.transform.SetParent(parent, false);

        Sprite crateBlue = LoadSpriteResource("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Ammo-crate_Blue.png", 32f);
        Sprite crateGreen = LoadSpriteResource("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Ammo-crate_Green.png", 32f);
        Sprite crateRed = LoadSpriteResource("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Ammo-crate_Red.png", 32f);

        // 1. Caja de Primeros Auxilios y Materiales de Inicio (x = 0.5f, y = -1.85f)
        CreateSupplyCrate(
            "Caja_Suministros_Entrada",
            cratesGroup.transform,
            new Vector3(0.5f, -1.85f, 0f),
            "Caja de Botellas y Cinta",
            crateBlue,
            new List<LootTableEntry>
            {
                CreateItemEntry("Botella Vacia", ItemCategory.MaterialDeCrafteo, 2, "Botellas de vidrio para fabricar Molotovs o Vendajes."),
                CreateItemEntry("Cinta Canela", ItemCategory.MaterialDeCrafteo, 2, "Cinta adhesiva de alta resistencia para crafteo.")
            }
        );

        // 2. Caja de Munición y Combustible a Mitad de Pasillo (x = 10.0f, y = -1.85f)
        CreateSupplyCrate(
            "Caja_Municion_MitadPasillo",
            cratesGroup.transform,
            new Vector3(10.0f, -1.85f, 0f),
            "Caja de Municion y Gasolina",
            crateGreen,
            new List<LootTableEntry>
            {
                CreateAmmoEntry("Municion .38 Especial (Detective)", 4, "Municion calibre .38 para el revolver del Detective Vargas."),
                CreateItemEntry("Gasolina", ItemCategory.MaterialDeCrafteo, 2, "Combustible inflamable para preparar Molotovs con Intelecto.")
            }
        );

        // 3. Alijo de Guerra Final antes de la Cámara de El Calacas (x = 19.5f, y = -1.85f)
        CreateSupplyCrate(
            "Caja_Alijo_Final_Calacas",
            cratesGroup.transform,
            new Vector3(19.5f, -1.85f, 0f),
            "Alijo Tactico de El Calacas",
            crateRed,
            new List<LootTableEntry>
            {
                CreateItemEntry("Torta Callejera", ItemCategory.ConsumibleCuracion, 2, "Racion sustanciosa que restaura +60 HP a Enrique."),
                CreateAmmoEntry("Municion .38 Especial (Detective)", 6, "Municion pesada para el revolver del Detective Vargas."),
                CreateItemEntry("Tubo de Plomo", ItemCategory.Arma, 1, "Arma contundente para el combate cuerpo a cuerpo."),
                CreateItemEntry("Gasolina", ItemCategory.MaterialDeCrafteo, 2, "Combustible inflamable para romper el manto de sombras de El Calacas.")
            }
        );
    }

    private static GameObject CreateSupplyCrate(
        string objectName,
        Transform parent,
        Vector3 position,
        string displayName,
        Sprite crateSprite,
        List<LootTableEntry> entries)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = crateSprite != null ? crateSprite : GetProceduralStoneSprite(new Color(0.25f, 0.45f, 0.65f, 1f));
        sr.color = Color.white;
        sr.sortingOrder = 4;

        float spriteWidth = (sr.sprite != null && sr.sprite.bounds.size.x > 0.01f) ? sr.sprite.bounds.size.x : 1.0f;
        float worldScale = Mathf.Clamp(0.78f / spriteWidth, 1.0f, 10.0f);
        go.transform.localScale = new Vector3(worldScale, worldScale, 1f);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.40f / worldScale, 1.40f / worldScale);

        // Hijo con BoxCollider2D físico pegado al borde inferior para no estorbar el paso
        GameObject solidCore = new GameObject("SolidCrateBody");
        solidCore.transform.SetParent(go.transform, false);
        BoxCollider2D solidCol = solidCore.AddComponent<BoxCollider2D>();
        solidCol.isTrigger = false;
        solidCol.offset = new Vector2(0f, -0.14f / worldScale);
        solidCol.size = new Vector2(0.55f / worldScale, 0.25f / worldScale);

        InteractableLoot loot = go.AddComponent<InteractableLoot>();
        loot.containerDisplayName = displayName;
        loot.isLooted = false;
        loot.disappearWhenLooted = true;
        loot.spriteRenderer = sr;
        loot.unlootedSprite = sr.sprite;
        loot.lootedColor = new Color(0.40f, 0.40f, 0.45f, 0.90f);
        loot.lootTable = ResolveCrateLootAssets(entries);

        GameObject promptGO = CreateWorldPromptLabel("PromptUI_E", go.transform, new Vector3(0f, 0.65f / worldScale, 0f), $"[E] REGISTRAR {displayName.ToUpper()}");
        promptGO.transform.localScale = new Vector3(0.35f / worldScale, 0.35f / worldScale, 1f);
        loot.promptUI = promptGO;
        loot.promptLabel = promptGO.GetComponent<TextMeshPro>();

        return go;
    }

    private static LootTableEntry CreateItemEntry(string name, ItemCategory category, int amount, string desc)
    {
        LootTableEntry entry = new LootTableEntry();
        entry.entryDisplayName = name;
        entry.fallbackCategory = category;
        entry.rewardKind = LootRewardKind.InventoryItem;
        entry.amount = amount;
        entry.customDescription = desc;
        entry.dropWeight = 50f;
        return entry;
    }

    private static LootTableEntry CreateAmmoEntry(string name, int amount, string desc)
    {
        LootTableEntry entry = new LootTableEntry();
        entry.entryDisplayName = name;
        entry.rewardKind = LootRewardKind.DetectiveAmmo;
        entry.amount = amount;
        entry.customDescription = desc;
        entry.dropWeight = 50f;
        return entry;
    }

    private static List<LootTableEntry> ResolveCrateLootAssets(List<LootTableEntry> entries)
    {
        ItemData refresco = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Item_Refresco.asset");
        ItemData torta = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Item_Torta.asset");
        ItemData botella = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Item_BotellaVacia.asset")
                           ?? RuntimeAssetCatalog.GetItem("Assets/Data/Items/Botella_Vacia.asset");
        ItemData chile = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Item_ChilePolvo.asset")
                         ?? RuntimeAssetCatalog.GetItem("Assets/Data/Items/Chile_Polvo.asset");
        ItemData cinta = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Cinta_Canela.asset");
        ItemData tubo = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Arma_TuboPlomo.asset");

        foreach (var e in entries)
        {
            if (e.rewardKind == LootRewardKind.InventoryItem)
            {
                if (e.entryDisplayName.Contains("Refresco") && refresco != null) e.itemAsset = refresco;
                else if (e.entryDisplayName.Contains("Torta") && torta != null) e.itemAsset = torta;
                else if (e.entryDisplayName.Contains("Botella") && botella != null) e.itemAsset = botella;
                else if ((e.entryDisplayName.Contains("Gasolina") || e.entryDisplayName.Contains("Chile")) && chile != null) e.itemAsset = chile;
                else if (e.entryDisplayName.Contains("Cinta") && cinta != null) e.itemAsset = cinta;
                else if (e.entryDisplayName.Contains("Tubo") && tubo != null) e.itemAsset = tubo;
            }
        }
        return entries;
    }

    // =========================================================================================
    // ZONA DE PUZZLE DE SEGURIDAD NOIR Y CENTINELA DEL BÚNKER
    // =========================================================================================

    private static void SpawnGuaridaSecurityPuzzleZone(Transform parent)
    {
        GameObject puzzleGroup = new GameObject("Zona_Puzzle_Seguridad_Guarida");
        puzzleGroup.transform.SetParent(parent, false);

        // ÚNICA TERMINAL MAESTRA + COMPUERTA BLINDADA LÁSER (x = 15.2f, y = -0.75f)
        GameObject gateRoot = new GameObject("Compuerta_Seguridad_Calacas");
        gateRoot.transform.SetParent(puzzleGroup.transform, false);
        gateRoot.transform.position = new Vector3(15.2f, -0.75f, 0f);

        // BoxCollider2D SÓLIDO que bloquea físicamente todo el ancho vertical del corredor hasta resolver el puzzle
        GameObject solidBlockerGO = new GameObject("Collider_Bloqueo_Compuerta");
        solidBlockerGO.transform.SetParent(gateRoot.transform, false);
        solidBlockerGO.transform.localPosition = Vector3.zero;
        BoxCollider2D solidGateCol = solidBlockerGO.AddComponent<BoxCollider2D>();
        solidGateCol.isTrigger = false;
        solidGateCol.size = new Vector2(0.85f, 4.0f);

        // Pilares de acero de la compuerta (arriba y abajo tocando ambos muros)
        Sprite blastDoorSprite = GetProceduralBlastDoorSprite();

        GameObject topPylon = new GameObject("Pilar_Compuerta_Superior");
        topPylon.transform.SetParent(gateRoot.transform, false);
        topPylon.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        topPylon.transform.localScale = new Vector3(0.85f, 0.50f, 1f);
        SpriteRenderer topSR = topPylon.AddComponent<SpriteRenderer>();
        topSR.sprite = blastDoorSprite;
        topSR.color = new Color(0.75f, 0.80f, 0.90f, 1f);
        topSR.sortingOrder = 5;

        GameObject botPylon = new GameObject("Pilar_Compuerta_Inferior");
        botPylon.transform.SetParent(gateRoot.transform, false);
        botPylon.transform.localPosition = new Vector3(0f, -1.55f, 0f);
        botPylon.transform.localScale = new Vector3(0.85f, 0.45f, 1f);
        SpriteRenderer botSR = botPylon.AddComponent<SpriteRenderer>();
        botSR.sprite = blastDoorSprite;
        botSR.color = new Color(0.75f, 0.80f, 0.90f, 1f);
        botSR.sortingOrder = 6;

        // 3 Haces láser rojos verticales cubriendo todo el alto del pasillo
        Sprite beamSprite = GetProceduralLaserBeamSprite();
        SpriteRenderer[] beamRenderers = new SpriteRenderer[3];
        float[] beamOffsetsX = new float[] { -0.18f, 0f, 0.18f };
        for (int i = 0; i < 3; i++)
        {
            GameObject beamGO = new GameObject($"Barrera_Laser_{i + 1}");
            beamGO.transform.SetParent(gateRoot.transform, false);
            beamGO.transform.localPosition = new Vector3(beamOffsetsX[i], -0.10f, 0f);
            beamGO.transform.localScale = new Vector3(1.25f, 1.55f, 1f);

            SpriteRenderer bsr = beamGO.AddComponent<SpriteRenderer>();
            bsr.sprite = beamSprite;
            bsr.color = new Color(1.0f, 0.22f, 0.22f, 0.92f);
            bsr.sortingOrder = 5;
            beamRenderers[i] = bsr;
        }

        // Consola Maestra de Hackeo despejada entre ColumnaGotica_12.5 y la compuerta (x = 13.95f)
        GameObject consoleGO = new GameObject("Consola_Master_Puzzle");
        consoleGO.transform.SetParent(gateRoot.transform, false);
        consoleGO.transform.localPosition = new Vector3(-1.25f, 1.25f, 0f);
        consoleGO.transform.localScale = Vector3.one;

        SpriteRenderer consoleSR = consoleGO.AddComponent<SpriteRenderer>();
        consoleSR.sprite = GetProceduralTerminalConsoleSprite();
        consoleSR.sortingOrder = 4;

        // Colisionador sólido en la base de la consola
        BoxCollider2D consoleSolid = consoleGO.AddComponent<BoxCollider2D>();
        consoleSolid.isTrigger = false;
        consoleSolid.offset = new Vector2(0f, -0.18f);
        consoleSolid.size = new Vector2(0.65f, 0.25f);

        // Luz indicadora de estado (Roja cuando está bloqueada, Verde al resolverse)
        GameObject statusLightGO = new GameObject("ConsoleStatusGlow");
        statusLightGO.transform.SetParent(consoleGO.transform, false);
        statusLightGO.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        statusLightGO.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        SpriteRenderer statusLightSR = statusLightGO.AddComponent<SpriteRenderer>();
        statusLightSR.sprite = GetProceduralGlowSprite();
        statusLightSR.color = new Color(1.0f, 0.20f, 0.20f, 0.75f);
        statusLightSR.sortingOrder = 5;

        // Trigger amplio en todo el ancho vertical del corredor frente a la consola y la compuerta
        GameObject interactZone = new GameObject("Trigger_Interactuar_Puzzle");
        interactZone.transform.SetParent(consoleGO.transform, false);
        interactZone.transform.localPosition = new Vector3(0.25f, -1.25f, 0f);

        BoxCollider2D triggerCol = interactZone.AddComponent<BoxCollider2D>();
        triggerCol.isTrigger = true;
        triggerCol.offset = Vector2.zero;
        triggerCol.size = new Vector2(2.6f, 3.2f);

        GameObject promptGO = CreateWorldPromptLabel(
            "Prompt_HackearCompuerta",
            consoleGO.transform,
            new Vector3(0f, 0.85f, 0f),
            ""
        );

        GuaridaSecurityGateInteractable gateInteractable = interactZone.AddComponent<GuaridaSecurityGateInteractable>();
        gateInteractable.solidGateBlocker = solidGateCol;
        gateInteractable.laserBeamRenderers = beamRenderers;
        gateInteractable.consoleStatusLight = statusLightSR;
        gateInteractable.promptObject = promptGO;
    }

    private static void SpawnGuaridaCorridorGuard(Transform parent)
    {
        // Guardia Centinela a mitad del corredor (x = 7.2f, y = -0.75f) que suelta materiales de crafteo al derrotarlo
        Sprite pIdle1 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Idle/idle1.png", 32f);
        Sprite pIdle2 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Idle/idle2.png", 32f);
        Sprite pIdle3 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Idle/idle3.png", 32f);
        Sprite pIdle4 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Idle/idle4.png", 32f);

        Sprite pWalk1 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Walk/walk1.png", 32f);
        Sprite pWalk2 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Walk/walk2.png", 32f);
        Sprite pWalk3 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Walk/walk3.png", 32f);
        Sprite pWalk4 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Walk/walk4.png", 32f);

        Sprite pPunch1 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Punch/punch1.png", 32f);
        Sprite pPunch2 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Punch/punch2.png", 32f);
        Sprite pPunch3 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Punch/punch3.png", 32f);

        Sprite pHurt1 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Hurt/hurt1.png", 32f);
        Sprite pHurt2 = LoadSpriteResource("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Hurt/hurt2.png", 32f);

        Sprite[] guardIdle = new Sprite[] { pIdle1, pIdle2, pIdle3, pIdle4 };
        Sprite[] guardWalk = new Sprite[] { pWalk1, pWalk2, pWalk3, pWalk4 };
        Sprite[] guardAttack = new Sprite[] { pPunch1, pPunch2, pPunch3 };
        Sprite[] guardHurt = new Sprite[] { pHurt1, pHurt2 };

        EnemyData guardData = ScriptableObject.CreateInstance<EnemyData>();
        guardData.enemyName = "Centinela del Búnker";
        guardData.enemyType = EnemyType.SicarioVerdugoNivel2;
        guardData.maxHP = 65;
        guardData.attack = 11;
        guardData.defense = 3;
        guardData.motricidad = 8;
        guardData.EXP_Reward = 80;
        guardData.enemySprite = pIdle1;
        guardData.idleFrames = guardIdle;
        guardData.walkFrames = guardWalk;
        guardData.attackFrames = guardAttack;
        guardData.hurtFrames = guardHurt;
        guardData.primaryAttackName = "Porrazo Eléctrico";
        guardData.secondaryAttackName = "Patada de Contención";

        GameObject guardGO = new GameObject("Centinela_Guarida_Seguridad");
        guardGO.transform.SetParent(parent, false);
        guardGO.transform.position = new Vector3(7.2f, -0.75f, 0f);
        guardGO.transform.localScale = new Vector3(1.25f, 1.25f, 1f);

        Rigidbody2D rb = guardGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        BoxCollider2D col = guardGO.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.90f, 1.20f);

        SpriteRenderer sr = guardGO.AddComponent<SpriteRenderer>();
        sr.sprite = pIdle1 != null ? pIdle1 : GetProceduralStoneSprite(new Color(0.8f, 0.3f, 0.3f, 1f));
        sr.flipX = true;
        sr.sortingOrder = 5;

        SpriteFrameAnimator anim = guardGO.AddComponent<SpriteFrameAnimator>();
        anim.frames = guardIdle;
        anim.framesPerSecond = 6f;

        EnemyFollowAI followAI = guardGO.AddComponent<EnemyFollowAI>();
        followAI.spriteRenderer = sr;
        followAI.detectionRadius = 3.2f;
        followAI.stoppingDistance = 0.85f;
        followAI.walkSpeed = 1.3f;
        followAI.chaseSpeed = 2.1f;
        followAI.wanderRadius = 0.9f;

        EnemyEncounterTrigger encounter = guardGO.AddComponent<EnemyEncounterTrigger>();
        encounter.forceDoubleEncounterGroup = false;
        encounter.enemies = new EnemyData[] { guardData };
        encounter.isBossEncounter = false;
        encounter.nextSceneName = "";
        encounter.introDialogueLines = new string[]
        {
            "CENTINELA DEL BÚNKER: \"¡Alto ahí! Nadie cruza hacia la compuerta blindada del jefe sin autorización.\"",
            "DETECTIVE VARGAS: \"Quitaremos a este guardia del camino y usaremos la consola de seguridad para abrir la compuerta, Bosques.\""
        };
    }

    // =========================================================================================
    // JEFE FINAL: EL CALACAS CON SKIN DE GOTHICVANIA PLAYER (AL FINAL DEL PASILLO, x = 22.0f)
    // =========================================================================================

    private static Sprite[] LoadGothicvaniaPlayerSequence(string folderName, int frameCount)
    {
        Sprite[] frames = new Sprite[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            string path = $"Assets/NewStuff/gothicvania church files/Assets/SPRITES/player/{folderName}/sprites/f-{(i + 1):00}.png";
            frames[i] = LoadSpriteResource(path, 32f);
        }
        return frames;
    }

    private static bool HasValidSprites(Sprite[] frames)
    {
        return frames != null && frames.Length > 0 && frames[0] != null;
    }

    public static void ApplyGothicvaniaSpritesToCalacasData(EnemyData data, bool saveAssetInEditor = false)
    {
        if (data == null) return;

        Sprite[] idle = LoadGothicvaniaPlayerSequence("Idle", 4);
        Sprite[] walk = LoadGothicvaniaPlayerSequence("Walk", 6);
        Sprite[] punch = LoadGothicvaniaPlayerSequence("Punch", 6);
        Sprite[] kick = LoadGothicvaniaPlayerSequence("Kick", 5);
        Sprite[] flyingKick = LoadGothicvaniaPlayerSequence("Flying Kick", 2);
        Sprite[] crouchKick = LoadGothicvaniaPlayerSequence("crouch-kick", 5);
        Sprite[] hurt = LoadGothicvaniaPlayerSequence("Hurt", 2);

        data.animatorController = null;
        data.idleAltFrames = System.Array.Empty<Sprite>();

        if (HasValidSprites(idle))
        {
            data.enemySprite = idle[0];
            data.idleFrames = idle;
        }
        if (HasValidSprites(walk))
        {
            data.walkFrames = walk;
            data.runFrames = walk;
        }
        else if (HasValidSprites(data.walkFrames))
        {
            data.runFrames = data.walkFrames;
        }
        if (HasValidSprites(punch)) data.attackFrames = punch;
        if (HasValidSprites(kick)) data.attack2Frames = kick;
        if (HasValidSprites(flyingKick)) data.attack3Frames = flyingKick;
        if (HasValidSprites(crouchKick))
        {
            data.specialFrames = crouchKick;
        }
        else if (HasValidSprites(data.attackFrames))
        {
            data.specialFrames = data.attackFrames;
        }
        if (HasValidSprites(hurt))
        {
            data.hurtFrames = hurt;
            data.deadFrames = hurt;
        }

#if UNITY_EDITOR
        if (saveAssetInEditor && !Application.isPlaying && AssetDatabase.Contains(data))
        {
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
#endif
    }

    private static void EnsureCalacasBossSpritesUnified(GameObject root)
    {
        bool saveInEditMode = !Application.isPlaying;
        EnemyData updatedData = null;

        if (root != null)
        {
            Transform bossTr = root.transform.Find("Jefe_Final_ElCalacas");
            if (bossTr != null)
            {
                EnemyEncounterTrigger encounter = bossTr.GetComponent<EnemyEncounterTrigger>();
                if (encounter != null && encounter.enemies != null)
                {
                    foreach (EnemyData e in encounter.enemies)
                    {
                        if (e != null && e.enemyType == EnemyType.JefeElCalacas)
                        {
                            ApplyGothicvaniaSpritesToCalacasData(e, saveAssetInEditor: saveInEditMode);
                            updatedData = e;
                        }
                    }
                }
            }
        }

        if (updatedData == null)
        {
            EnemyData diskAsset = RuntimeAssetCatalog.GetEnemy("Assets/Data/Enemies/Jefe_ElCalacas.asset");
            if (diskAsset != null)
            {
                ApplyGothicvaniaSpritesToCalacasData(diskAsset, saveAssetInEditor: saveInEditMode);
            }
        }
    }

    private static void SpawnCalacasBoss(Transform parent)
    {
        EnemyData calacasData = GetOrCreateCalacasEnemyData();
        Sprite[] calacasIdle = calacasData != null && calacasData.idleFrames != null
            ? calacasData.idleFrames
            : LoadGothicvaniaPlayerSequence("Idle", 4);
        Sprite gIdle1 = calacasIdle.Length > 0 ? calacasIdle[0] : null;

        // Posicionar a El Calacas al final del pasillo espacioso (x = 22.0, y = -0.75f)
        GameObject calacasGO = new GameObject("Jefe_Final_ElCalacas");
        calacasGO.transform.SetParent(parent, false);
        calacasGO.transform.position = new Vector3(22.0f, -0.75f, 0f);
        calacasGO.transform.localScale = new Vector3(1.35f, 1.35f, 1f);

        Rigidbody2D rb = calacasGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        BoxCollider2D col = calacasGO.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.9f, 1.4f);

        SpriteRenderer sr = calacasGO.AddComponent<SpriteRenderer>();
        sr.sprite = gIdle1 != null ? gIdle1 : GetProceduralStoneSprite(new Color(0.6f, 0.2f, 0.7f, 1f));
        sr.sortingOrder = 5;

        // Animador de Idle de Gothicvania
        SpriteFrameAnimator anim = calacasGO.AddComponent<SpriteFrameAnimator>();
        anim.frames = calacasIdle;
        anim.framesPerSecond = 6f;

        // IA y Controlador Especial de Jefe Final
        EnemyFollowAI followAI = calacasGO.AddComponent<EnemyFollowAI>();
        followAI.spriteRenderer = sr;
        followAI.detectionRadius = 5.2f;
        followAI.stoppingDistance = 1.0f;
        followAI.walkSpeed = 1.6f;
        followAI.chaseSpeed = 2.8f;
        followAI.wanderRadius = 1.0f;

        CalacasBossController bossController = calacasGO.AddComponent<CalacasBossController>();
        bossController.bossDisplayName = "EL CALACAS";
        bossController.nextLevelSceneName = "00_MenuIntro";

        EnemyEncounterTrigger encounter = calacasGO.AddComponent<EnemyEncounterTrigger>();
        encounter.forceDoubleEncounterGroup = false;
        encounter.enemies = new EnemyData[] { calacasData };
        encounter.isBossEncounter = true;
        encounter.nextSceneName = "00_MenuIntro";
        encounter.level2BossController = bossController;
        encounter.victoryNarrativeLines = bossController.finalVictoryNarrativeLines;
        encounter.introDialogueLines = new string[]
        {
            "EL CALACAS: \"Por fin llegan hasta mi despacho subterráneo... Tumbaste a mis guardias, repartidor. Pero de aquí no saldrán con vida.\"",
            "INVESTIGADOR PRIVADO: \"Baja las armas, Calacas. Tu imperio clandestino termina hoy. ¡Entrega la documentación de la red!\"",
            "EL CALACAS: \"¿Creen que esa caja es un simple envío? ¡Es la verdad que quemará toda Santa Felipina! ¡Vengan si se atreven!\"",
            "VOZ [CORPÓREO]: \"¡Su postura es ágil! Esquivará nuestros golpes directos si no lo acorralamos.\"",
            "VOZ [INTELECTO]: \"¡Usa las Bombas de Chile o el Revólver de Vargas para romper su manto de sombras y dejarlo vulnerable!\""
        };

        // Decoración de antorchas a los lados de El Calacas en el sanctum
        GameObject torchL = new GameObject("AntorchaSanctum_L");
        torchL.transform.SetParent(parent, false);
        torchL.transform.position = new Vector3(20.6f, -0.10f, 0f);
        torchL.transform.localScale = Vector3.one;
        SpriteRenderer tLsr = torchL.AddComponent<SpriteRenderer>();
        tLsr.sprite = LoadSpriteResource("Assets/NewStuff/torch_1.png", 16f);
        tLsr.sortingOrder = 4;
        SpriteFrameAnimator tLanim = torchL.AddComponent<SpriteFrameAnimator>();
        tLanim.frames = new Sprite[]
        {
            LoadSpriteResource("Assets/NewStuff/torch_1.png", 16f),
            LoadSpriteResource("Assets/NewStuff/torch_2.png", 16f),
            LoadSpriteResource("Assets/NewStuff/torch_3.png", 16f),
            LoadSpriteResource("Assets/NewStuff/torch_4.png", 16f)
        };
        tLanim.framesPerSecond = 8f;

        GameObject torchR = new GameObject("AntorchaSanctum_R");
        torchR.transform.SetParent(parent, false);
        torchR.transform.position = new Vector3(23.4f, -0.10f, 0f);
        torchR.transform.localScale = Vector3.one;
        SpriteRenderer tRsr = torchR.AddComponent<SpriteRenderer>();
        tRsr.sprite = LoadSpriteResource("Assets/NewStuff/torch_1.png", 16f);
        tRsr.sortingOrder = 4;
        SpriteFrameAnimator tRanim = torchR.AddComponent<SpriteFrameAnimator>();
        tRanim.frames = tLanim.frames;
        tRanim.framesPerSecond = 8f;
    }

    private static EnemyData GetOrCreateCalacasEnemyData()
    {
#if UNITY_EDITOR
        string assetPath = "Assets/Data/Enemies/Jefe_ElCalacas.asset";
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
        bool isNew = (data == null);
        if (isNew)
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            if (!AssetDatabase.IsValidFolder("Assets/Data/Enemies"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateFolder("Assets/Data", "Enemies");
            }
        }

        ConfigureCalacasBaseData(data);
        ApplyGothicvaniaSpritesToCalacasData(data, saveAssetInEditor: false);

        if (isNew)
        {
            AssetDatabase.CreateAsset(data, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
        return data;
#else
        EnemyData runtime = RuntimeAssetCatalog.GetEnemy("Assets/Data/Enemies/Jefe_ElCalacas.asset");
        if (runtime == null)
        {
            runtime = ScriptableObject.CreateInstance<EnemyData>();
            ConfigureCalacasBaseData(runtime);
        }
        ApplyGothicvaniaSpritesToCalacasData(runtime, saveAssetInEditor: false);
        return runtime;
#endif
    }

    private static void ConfigureCalacasBaseData(EnemyData data)
    {
        data.enemyName = "El Calacas";
        data.enemyType = EnemyType.JefeElCalacas;
        data.maxHP = 220;
        data.attack = 18;
        data.defense = 6;
        data.motricidad = 14;
        data.EXP_Reward = 300;
        data.primaryAttackName = "Cuchillada de Sombras";
        data.secondaryAttackName = "Patada Giratoria Mortal";
    }

    // =========================================================================================
    // UTILIDADES DE CARGA Y GENERACIÓN PROCEDURAL DE SPRITES LIMPIOS
    // =========================================================================================

    public static Sprite LoadSpriteResource(string relativePath, float ppu = 32f)
    {
        Sprite catalogSprite = RuntimeAssetCatalog.GetSprite(relativePath);
        if (catalogSprite != null) return catalogSprite;

#if !UNITY_WEBGL
        string fullPath = Path.Combine(Application.dataPath, relativePath.StartsWith("Assets/") ? relativePath.Substring(7) : relativePath);
        if (File.Exists(fullPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                if (tex.LoadImage(bytes))
                {
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
                }
            }
            catch { }
        }
#endif
        return null;
    }

    private static Sprite s_cachedStoneWall;
    private static Sprite GetProceduralStoneWallSprite()
    {
        if (s_cachedStoneWall != null) return s_cachedStoneWall;

        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] cols = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            int row = y / 8;
            int shift = (row % 2 == 1) ? 16 : 0;
            for (int x = 0; x < 32; x++)
            {
                int col = (x + shift) % 32;
                bool isMortar = (y % 8 == 0) || (col == 0);
                if (isMortar)
                {
                    cols[y * 32 + x] = new Color(0.08f, 0.07f, 0.10f, 1f);
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.08f - 0.04f;
                    cols[y * 32 + x] = new Color(0.14f + noise, 0.13f + noise, 0.18f + noise, 1f);
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        s_cachedStoneWall = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        s_cachedStoneWall.name = "ProceduralStoneWall";
        return s_cachedStoneWall;
    }

    private static Sprite s_cachedFloorSlab;
    private static Sprite GetProceduralFloorSlabSprite()
    {
        if (s_cachedFloorSlab != null) return s_cachedFloorSlab;

        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] cols = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                bool isTopBevel = (y >= 30);
                bool isSideMortar = (x == 0 || x == 31);
                float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.06f - 0.03f;

                if (isTopBevel)
                {
                    cols[y * 32 + x] = new Color(0.38f + noise, 0.36f + noise, 0.42f + noise, 1f);
                }
                else if (isSideMortar)
                {
                    cols[y * 32 + x] = new Color(0.12f, 0.11f, 0.15f, 1f);
                }
                else
                {
                    cols[y * 32 + x] = new Color(0.26f + noise, 0.25f + noise, 0.30f + noise, 1f);
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        s_cachedFloorSlab = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        s_cachedFloorSlab.name = "ProceduralFloorSlab";
        return s_cachedFloorSlab;
    }

    private static Sprite s_cachedCeilingSlab;
    private static Sprite GetProceduralCeilingSlabSprite()
    {
        if (s_cachedCeilingSlab != null) return s_cachedCeilingSlab;

        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] cols = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                bool isBottomBevel = (y <= 2);
                bool isSideMortar = (x == 0 || x == 31);
                float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.05f - 0.025f;

                if (isBottomBevel)
                {
                    cols[y * 32 + x] = new Color(0.10f, 0.09f, 0.13f, 1f);
                }
                else if (isSideMortar)
                {
                    cols[y * 32 + x] = new Color(0.08f, 0.07f, 0.11f, 1f);
                }
                else
                {
                    cols[y * 32 + x] = new Color(0.20f + noise, 0.19f + noise, 0.24f + noise, 1f);
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        s_cachedCeilingSlab = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        s_cachedCeilingSlab.name = "ProceduralCeilingSlab";
        return s_cachedCeilingSlab;
    }

    private static Sprite s_cachedBlastDoor;
    private static Sprite GetProceduralBlastDoorSprite()
    {
        if (s_cachedBlastDoor != null) return s_cachedBlastDoor;

        Texture2D tex = new Texture2D(32, 64, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] cols = new Color[32 * 64];
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                bool isBorder = (x <= 1 || x >= 30 || y <= 1 || y >= 62);
                bool isSeam = (x == 15 || x == 16);
                bool isHinge = (x <= 3 || x >= 28) && ((y >= 12 && y <= 16) || (y >= 48 && y <= 52));
                bool isRivet = (x == 4 || x == 27) && (y % 10 == 0);

                if (isBorder || isSeam)
                {
                    cols[y * 32 + x] = new Color(0.08f, 0.09f, 0.12f, 1f);
                }
                else if (isHinge || isRivet)
                {
                    cols[y * 32 + x] = new Color(0.55f, 0.52f, 0.45f, 1f);
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.05f;
                    cols[y * 32 + x] = new Color(0.22f + noise, 0.23f + noise, 0.27f + noise, 1f);
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        s_cachedBlastDoor = Sprite.Create(tex, new Rect(0, 0, 32, 64), new Vector2(0.5f, 0.5f), 32f);
        s_cachedBlastDoor.name = "ProceduralBlastDoor";
        return s_cachedBlastDoor;
    }

    private static Sprite s_cachedStoneSprite;
    private static Sprite GetProceduralStoneSprite(Color color)
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] cols = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.25f, y * 0.25f) * 0.15f - 0.075f;
                bool isBorder = (x == 0 || x == 31 || y == 0 || y == 31);
                Color c = isBorder ? color * 0.65f : color + new Color(noise, noise, noise, 0f);
                cols[y * 32 + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    private static Sprite GetProceduralColumnSprite()
    {
        Texture2D tex = new Texture2D(24, 64, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color baseCol = new Color(0.24f, 0.22f, 0.28f, 1f);
        Color[] cols = new Color[24 * 64];
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 24; x++)
            {
                bool isCapital = (y < 6 || y > 58);
                bool isEdge = (x == 0 || x == 23);
                Color c = isCapital ? baseCol * 1.2f : baseCol;
                if (isEdge) c *= 0.7f;
                cols[y * 24 + x] = c;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 24, 64), new Vector2(0.5f, 0.5f), 32f);
    }

    private static Sprite s_cachedTerminalConsole;
    private static Sprite GetProceduralTerminalConsoleSprite()
    {
        if (s_cachedTerminalConsole != null) return s_cachedTerminalConsole;

        Texture2D tex = new Texture2D(32, 40, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] cols = new Color[32 * 40];
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                // Pedestal más estrecho abajo (y < 14) y gabinete de consola arriba (y >= 14)
                if (y < 14 && (x < 5 || x > 26))
                {
                    cols[y * 32 + x] = Color.clear;
                    continue;
                }
                if (y >= 14 && (x < 2 || x > 29))
                {
                    cols[y * 32 + x] = Color.clear;
                    continue;
                }

                bool isBorder = (y == 0 || y == 39 || x == 2 || x == 29 || (y < 14 && (x == 5 || x == 26)) || y == 14);
                bool isScreen = (y >= 22 && y <= 34 && x >= 6 && x <= 25);
                bool isKeypad = (y >= 16 && y <= 20 && x >= 7 && x <= 24 && (x % 3 != 0));

                if (isBorder)
                {
                    cols[y * 32 + x] = new Color(0.06f, 0.08f, 0.12f, 1f);
                }
                else if (isScreen)
                {
                    bool isScanline = (y % 2 == 0);
                    cols[y * 32 + x] = isScanline
                        ? new Color(0.10f, 0.55f, 0.72f, 1f)
                        : new Color(0.18f, 0.82f, 0.96f, 1f);
                }
                else if (isKeypad)
                {
                    cols[y * 32 + x] = new Color(0.95f, 0.78f, 0.22f, 1f);
                }
                else
                {
                    cols[y * 32 + x] = new Color(0.20f, 0.23f, 0.29f, 1f);
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        s_cachedTerminalConsole = Sprite.Create(tex, new Rect(0, 0, 32, 40), new Vector2(0.5f, 0.5f), 32f);
        s_cachedTerminalConsole.name = "ProceduralTerminalConsole";
        return s_cachedTerminalConsole;
    }

    private static Sprite s_cachedLaserBeam;
    private static Sprite GetProceduralLaserBeamSprite()
    {
        if (s_cachedLaserBeam != null) return s_cachedLaserBeam;

        Texture2D tex = new Texture2D(8, 64, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] cols = new Color[8 * 64];
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                if (x == 3 || x == 4)
                    cols[y * 8 + x] = new Color(1f, 0.90f, 0.90f, 0.98f);
                else if (x == 2 || x == 5)
                    cols[y * 8 + x] = new Color(1f, 0.25f, 0.25f, 0.75f);
                else
                    cols[y * 8 + x] = Color.clear;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        s_cachedLaserBeam = Sprite.Create(tex, new Rect(0, 0, 8, 64), new Vector2(0.5f, 0.5f), 32f);
        s_cachedLaserBeam.name = "ProceduralLaserBeam";
        return s_cachedLaserBeam;
    }

    private static Sprite GetProceduralGlowSprite()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] cols = new Color[32 * 32];
        Vector2 center = new Vector2(15.5f, 15.5f);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / 16f;
                float alpha = Mathf.Clamp01(1f - dist);
                cols[y * 32 + x] = new Color(1f, 1f, 1f, alpha * alpha);
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    public static GameObject CreateWorldPromptLabel(string name, Transform parent, Vector3 localPos, string text)
    {
        GameObject promptGO = new GameObject(name);
        promptGO.transform.SetParent(parent, false);
        promptGO.transform.localPosition = localPos;
        promptGO.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
        promptGO.SetActive(false);
        return promptGO;
    }
}
