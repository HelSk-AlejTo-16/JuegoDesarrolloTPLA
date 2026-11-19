using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Controla la llegada de Enrique al Capítulo 2 (El Callejón de las Sombras):
/// - Fundido desde negro con título noir del capítulo.
/// - Construcción armónica del mapa de callejones utilizando estrictamente la misma gramática arquitectónica de Tilemaps
///   que `01_Nivel1_SantaFelipina.unity`.
/// - Compañero Investigador Privado (CompanionFollowAI + DetectiveStats) que se une a Enrique y participa en combate.
/// - Encuentros dobles del Nivel 2 (2 arquetipos nuevos: Sicario Cuchillero y Verdugo de Hacha).
/// - Jefe Final del Nivel 2 (El Capataz Blindado con Level2BossController y transición a Nivel_3).
/// - Contenedores de basura interactuables (InteractableLoot: Botes de Basura y Bolsas de Basura con LootTable).
/// </summary>
public class Capitulo2IntroNarrative : MonoBehaviour
{
    [Header("Referencias de UI")]
    public CanvasGroup fadeCanvasGroup;
    public GameObject bannerPanel;
    public TextMeshProUGUI chapterTitleText;
    public TextMeshProUGUI chapterSubtitleText;

    [Header("Compañero Investigador Privado")]
    public CompanionFollowAI investigatorCompanion;

    [Header("Diálogo Inicial (Encuentro con el Investigador Privado)")]
    [TextArea(2, 4)]
    public string[] arrivalDialogueLines = new string[]
    {
        "NARRADOR: \"La avenida termina cerrada por una reja perimetral y pilas de cajas. Entre los edificios de Santa Felipina solo queda abierto un estrecho callejón peatonal.\"",
        "VOZ [MUNDO INTERNO]: \"El humo de una colilla recién apagada flota junto al farol de la esquina. Alguien nos esperaba en la entrada del callejón, Enrique.\"",
        "INVESTIGADOR PRIVADO: \"Baja los puños, repartidor. Vi cómo tumbaste a los matones de la calle... Me llamo Héctor Vargas, investigador privado.\"",
        "ENRIQUE BOSQUES: \"¿Un detective en este barrio? Si vienes por el paquete, llegas tarde. Ni siquiera sé qué demonios llevo dentro.\"",
        "INVESTIGADOR PRIVADO: \"No busco tu caja, Bosques. Busco al jefe que ordenó emboscarte: 'El Calacas'. Llevo meses siguiéndole el rastro por toda la ciudad.\"",
        "VOZ [INTELECTO]: \"Un investigador privado... Sus expedientes, su revólver .38 y su conocimiento táctico del terreno serán vitales en estos callejones.\"",
        "INVESTIGADOR PRIVADO: \"Este pasaje entre las azoteas y los edificios es el único acceso hacia su guarida. Cerraron el paso vehicular; aquí solo se pasa a pie y sus sicarios patrullan en parejas.\"",
        "VOZ [CORPOREO]: \"Perfecto. Entre tu revólver y nuestros puños limpiaremos el callejón.\"",
        "VOZ [MOTRICIDAD]: \"Revisa también los botes y bolsas de basura con [E]: los contrabandistas suelen esconder munición, vendas y materiales útiles.\"",
        "INVESTIGADOR PRIVADO: \"Adelante hay parejas de cuchilleros y verdugos vigilando el paso, y 'El Capataz Punk' custodia el portón del complejo. Me uno a tu equipo, Enrique.\"",
        "NARRADOR: \"[ EL DETECTIVE HÉCTOR VARGAS SE HA UNIDO AL EQUIPO DE COMBATE ]\""
    };

    [Tooltip("Gestor de diálogo de exploración en la escena.")]
    public ExplorationDialogueManager explorationDialogueManager;

    private void Awake()
    {
        EnsureAlleyMapAndNoirDialogueReady();
    }

    private void Start()
    {
        PlayerHealthBarUI.EnsureCleanPixelFont(chapterTitleText);
        PlayerHealthBarUI.EnsureCleanPixelFont(chapterSubtitleText);
        StartCoroutine(IntroSequenceRoutine());
    }

    /// <summary>
    /// Garantiza que el mapa armónico de callejones, el BattleManager de combate, los enemigos dobles de Nivel 2,
    /// el Jefe Final con Level2BossController, los contenedores InteractableLoot y el Detective estén listos.
    /// </summary>
    public void EnsureAlleyMapAndNoirDialogueReady()
    {
        // 1. Reemplazar versiones anteriores si existen
        GameObject oldWarehouse = GameObject.Find("Almacen_Clandestino_Root");
        if (oldWarehouse != null)
        {
            Destroy(oldWarehouse);
        }

        // Desvincular al Investigador Privado antes de validar/reemplazar alleyRoot para que NUNCA sea destruido por accidente
        if (investigatorCompanion == null)
        {
            investigatorCompanion = FindAnyObjectByType<CompanionFollowAI>();
        }
        if (investigatorCompanion != null && investigatorCompanion.transform.parent != null)
        {
            investigatorCompanion.transform.SetParent(null, true);
        }

        GameObject alleyRoot = GameObject.Find("Callejon_Ciudad_Root");
        if (alleyRoot != null &&
            alleyRoot.transform.Find("Callejon_Armonico_V8") == null &&
            alleyRoot.transform.Find("Callejon_Armonico_V7") == null)
        {
            Destroy(alleyRoot);
            alleyRoot = null;
        }

        if (alleyRoot == null)
        {
            alleyRoot = BuildUrbanAlleyEnvironment();
        }

        // 2. Posicionar a Enrique en la entrada peatonal oeste
        PlayerController2D player = FindAnyObjectByType<PlayerController2D>();
        if (player != null)
        {
            if (Vector2.Distance(player.transform.position, new Vector2(-6f, -3.5f)) < 2.0f ||
                Vector2.Distance(player.transform.position, new Vector2(-13.6f, -0.4f)) < 2.5f)
            {
                player.transform.position = new Vector3(-9.6f, -0.4f, 0f);
            }
        }

        // Ajustar cámara con los mismos parámetros limpios de la Escena 1
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
            mainCam.orthographicSize = 5.5f;

            CameraFollow2D camFollow = mainCam.GetComponent<CameraFollow2D>();
            if (camFollow != null)
            {
                camFollow.useBounds = true;
                camFollow.minBounds = new Vector2(-7.5f, -18.5f);
                camFollow.maxBounds = new Vector2(16.5f, 3.5f);
                if (player != null)
                {
                    camFollow.target = player.transform;
                    mainCam.transform.position = new Vector3(
                        Mathf.Clamp(player.transform.position.x, camFollow.minBounds.x, camFollow.maxBounds.x),
                        Mathf.Clamp(player.transform.position.y, camFollow.minBounds.y, camFollow.maxBounds.y),
                        -10f
                    );
                }
            }
        }

        // 3. Asegurar que el Investigador Privado exista con su sprite personalizado, CompanionFollowAI y DetectiveStats activo en el equipo
        if (investigatorCompanion == null)
        {
            investigatorCompanion = FindAnyObjectByType<CompanionFollowAI>();
        }
        if (investigatorCompanion == null)
        {
            investigatorCompanion = CreateInvestigatorCompanion(null);
        }
        else
        {
            if (investigatorCompanion.transform.parent != null)
            {
                investigatorCompanion.transform.SetParent(null, true);
            }
            EnsureGenericInvestigatorAppearance(investigatorCompanion);
        }

        if (investigatorCompanion != null)
        {
            if (player != null && Vector2.Distance(investigatorCompanion.transform.position, player.transform.position) > 3.2f)
            {
                investigatorCompanion.transform.position = player.transform.position + new Vector3(1.8f, 0f, 0f);
            }
            investigatorCompanion.ActivateCompanionFollow();
            DetectiveStats.IsDetectiveInPartyGlobal = true;
        }

        // Ocultar cualquier texto flotante o etiqueta sobre los objetos y personajes del mapa
        PlayerController2D.HideAllWorldSpaceObjectLabels();

        // 4. Asegurar que ExplorationDialogueManager tenga el diseño noir completo idéntico al Nivel 1
        if (explorationDialogueManager == null)
        {
            explorationDialogueManager = FindAnyObjectByType<ExplorationDialogueManager>();
        }
        if (explorationDialogueManager != null)
        {
            if (explorationDialogueManager.playerController == null && player != null)
            {
                explorationDialogueManager.playerController = player;
            }
            UpgradeDialoguePanelToNoirStyle(explorationDialogueManager);
        }

        // 5. Asegurar InventoryManager y BattleManager para los combates del Nivel 2
        EnsureInventoryManagerContinuity();
        BattleManager bm = BattleManager.EnsureInScene();

        // Conectar BattleManager a todos los triggers de combate de la escena
        if (bm != null)
        {
            EnemyEncounterTrigger[] triggers = FindObjectsByType<EnemyEncounterTrigger>(FindObjectsInactive.Include);
            foreach (EnemyEncounterTrigger tr in triggers)
            {
                if (tr != null && tr.battleManager == null)
                {
                    tr.battleManager = bm;
                }
            }
        }
    }

    private static void EnsureInventoryManagerContinuity()
    {
        InventoryManager inv = FindAnyObjectByType<InventoryManager>();
        if (inv == null)
        {
            GameObject invGO = new GameObject("InventoryManager");
            inv = invGO.AddComponent<InventoryManager>();
        }

        if (inv.knownRecipes == null) inv.knownRecipes = new List<CraftingRecipeData>();
        if (inv.knownRecipes.Count == 0)
        {
            CraftingRecipeData r1 = RuntimeAssetCatalog.GetRecipe("Assets/Data/Items/Receta_BombaChile.asset");
            CraftingRecipeData r2 = RuntimeAssetCatalog.GetRecipe("Assets/Data/Items/Receta_VendajeRudo.asset");
            if (r1 != null) inv.knownRecipes.Add(r1);
            if (r2 != null) inv.knownRecipes.Add(r2);
        }

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

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true;
        }

        if (bannerPanel != null) bannerPanel.SetActive(true);
        if (chapterTitleText != null)
        {
            PlayerHealthBarUI.EnsureCleanPixelFont(chapterTitleText);
            chapterTitleText.text = "CAPÍTULO 2: EL CALLEJÓN DE LAS SOMBRAS";
        }
        if (chapterSubtitleText != null)
        {
            PlayerHealthBarUI.EnsureCleanPixelFont(chapterSubtitleText);
            chapterSubtitleText.text = "Paso Peatonal entre los Edificios — En Busca de El Calacas";
        }

        yield return new WaitForSeconds(2.1f);

        if (bannerPanel != null) bannerPanel.SetActive(false);

        float elapsed = 0f;
        float duration = 1.15f;
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

        yield return new WaitForSeconds(0.25f);

        if (explorationDialogueManager == null)
        {
            explorationDialogueManager = FindAnyObjectByType<ExplorationDialogueManager>();
        }

        if (explorationDialogueManager != null && arrivalDialogueLines != null && arrivalDialogueLines.Length > 0)
        {
            explorationDialogueManager.StartDialogue(arrivalDialogueLines, () =>
            {
                if (investigatorCompanion != null)
                {
                    investigatorCompanion.ActivateCompanionFollow();
                }
                if (player != null) player.SetCanMove(true);
            });
        }
        else
        {
            if (investigatorCompanion != null)
            {
                investigatorCompanion.ActivateCompanionFollow();
            }
            if (player != null) player.SetCanMove(true);
        }
    }

    // =========================================================================================
    // CONSTRUCCIÓN ARMÓNICA Y ORDENADA DEL CALLEJÓN + ENEMIGOS NIVEL 2 + JEFE + LOOT DE BASURA
    // =========================================================================================

    private const float CellWorldSize = 0.8f;

    private static Sprite s_cachedWhiteSprite;

    public static Sprite GetWhiteSquareSprite()
    {
        if (s_cachedWhiteSprite != null) return s_cachedWhiteSprite;

        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] cols = new Color[16 * 16];
        for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
        tex.SetPixels(cols);
        tex.Apply();

        s_cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        s_cachedWhiteSprite.name = "ProceduralWhiteSquare";
        return s_cachedWhiteSprite;
    }

    private static Sprite LoadProjectSprite(string assetPath)
    {
        return RuntimeAssetCatalog.GetSprite(assetPath);
    }

    private static Sprite[] LoadAllSpritesAtPath(string assetPath)
    {
        return RuntimeAssetCatalog.GetSprites(assetPath);
    }

    private static TileBase LoadTileAssetAtPath(string fullAssetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<TileBase>(fullAssetPath);
#else
        return null;
#endif
    }

    private static TileBase BY(int id)
    {
        return LoadTileAssetAtPath($"Assets/TileMap/Background_Bleak-Yellow_TileSet_{id}.asset");
    }

    private static TileBase TP(int id)
    {
        return LoadTileAssetAtPath($"Assets/TileMap/tilemap_packed_{id}.asset");
    }

    private static TileBase GD(int id)
    {
        return LoadTileAssetAtPath($"Assets/Sprites/GandalfHardcore City Tiles/Decoration 32x32_{id}.asset");
    }

    private static TileBase TX(int id)
    {
        return LoadTileAssetAtPath($"Assets/Sprites/TX Props_{id}.asset");
    }

    public static GameObject BuildUrbanAlleyEnvironment()
    {
        GameObject root = new GameObject("Callejon_Ciudad_Root");

        // Etiqueta de versión armónica V8 (incluye Detective en combate, Enemigos Dobles N2 con sprites normales, Jefe N2 Enemy-Punk animado y Loot de Basura)
        GameObject versionTag = new GameObject("Callejon_Armonico_V8");
        versionTag.transform.SetParent(root.transform, false);

        // 1. ESTRUCTURA DE GRIDS Y TILEMAPS IDÉNTICA A 01_Nivel1_SantaFelipina.unity
        Tilemap suelo = CreateGridWithTilemap("Suelo", "suelo", root.transform, new Vector3(-0.015f, -0.04f, 0f), 1);
        Tilemap edificios = CreateGridWithTilemap("Edificios", "edificios", root.transform, new Vector3(-0.015f, -0.044f, 0f), 2);
        Tilemap decoracionO = CreateGridWithTilemap("DecoracionObjetos", "decoracionO", root.transform, Vector3.zero, 3);
        Tilemap decoracionN = CreateGridWithTilemap("DecoracionNormal", "decoracionN", root.transform, new Vector3(0f, -0.04f, 0f), 3);

        GameObject collidersGroup = new GameObject("Colisiones_Arquitectura");
        collidersGroup.transform.SetParent(root.transform, false);

        PopulateHarmoniousAlleyMap(suelo, edificios, decoracionO, decoracionN, collidersGroup.transform);

        // 2. INTERACTUABLE DE LA GUARIDA DE EL CALACAS
        GameObject bossGateGO = new GameObject("Porton_Guarida_ElCalacas");
        bossGateGO.transform.SetParent(root.transform, false);
        bossGateGO.transform.position = new Vector3(19.6f, -15.4f, 0f);

        BoxCollider2D gateCol = bossGateGO.AddComponent<BoxCollider2D>();
        gateCol.isTrigger = true;
        gateCol.size = new Vector2(2.4f, 2.2f);

        MapInteractable gateInteract = bossGateGO.AddComponent<MapInteractable>();
        gateInteract.type = InteractableType.DialogueOnly;
        gateInteract.dialogueLines = new string[]
        {
            "INVESTIGADOR PRIVADO: \"Esta es la entrada blindada del complejo donde se esconde El Calacas, Enrique.\"",
            "VOZ [INTELECTO]: \"El Capataz Punk tiene las llaves de acceso al Nivel 3. Derrotémoslo para cruzar el portón.\""
        };

        GameObject gatePrompt = CreateWorldPromptLabel("PromptUI_E", bossGateGO.transform, new Vector3(0f, 1.35f, 0f), "[E] ENTRADA GUARIDA DE EL CALACAS");
        gateInteract.promptUI = gatePrompt;

        // 3. PUNTOS DE APARICIÓN Y GENERACIÓN DE ENCUENTROS DOBLES (NIVEL 2) + JEFE FINAL
        GameObject spawnMarkers = new GameObject("Puntos_Enemigos_Callejon");
        spawnMarkers.transform.SetParent(root.transform, false);
        CreateMarker("Spawn_Enemigo_Callejon_Norte", spawnMarkers.transform, new Vector3(-0.8f, -4.5f, 0f));
        CreateMarker("Spawn_Enemigo_Pasaje_Vertical", spawnMarkers.transform, new Vector3(-1.2f, -11.5f, 0f));
        CreateMarker("Spawn_Enemigo_Patio_Sur", spawnMarkers.transform, new Vector3(7.5f, -17.8f, 0f));
        CreateMarker("Spawn_JefeFinal_Callejon", spawnMarkers.transform, new Vector3(17.5f, -17.2f, 0f));

        SpawnLevel2EnemiesAndBoss(root.transform);

        // 4. SISTEMA DE LOOT EN EL MAPA: CONTENEDORES DE BASURA INTERACTUABLES (BOTES Y BOLSAS)
        SpawnLevel2TrashLootContainers(root.transform);

        return root;
    }

    // =========================================================================================
    // CREACIÓN DE LOS 2 NUEVOS ARQUETIPOS DE ENEMIGOS DEL NIVEL 2 Y EL JEFE FINAL
    // =========================================================================================

    public static EnemyData GetOrCreateSicarioCuchilleroData()
    {
#if UNITY_EDITOR
        string assetPath = "Assets/Data/Enemies/Sicario_Cuchillero_N2.asset";
        EnemyData existing = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
        if (existing != null)
        {
            ConfigureSicarioCuchillero(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        EnemyData created = ScriptableObject.CreateInstance<EnemyData>();
        ConfigureSicarioCuchillero(created);
        if (!AssetDatabase.IsValidFolder("Assets/Data/Enemies"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            AssetDatabase.CreateFolder("Assets/Data", "Enemies");
        }
        AssetDatabase.CreateAsset(created, assetPath);
        EditorUtility.SetDirty(created);
        return created;
#else
        EnemyData runtimeData = ScriptableObject.CreateInstance<EnemyData>();
        ConfigureSicarioCuchillero(runtimeData);
        return runtimeData;
#endif
    }

    private static void ConfigureSicarioCuchillero(EnemyData data)
    {
        data.name = "Sicario_Cuchillero_N2";
        data.enemyName = "Sicario Cuchillero";
        data.enemyType = EnemyType.SicarioCuchilleroNivel2;
        data.maxHP = 72;
        data.attack = 14;
        data.defense = 4;
        data.motricidad = 13;
        data.EXP_Reward = 75;
        data.primaryAttackName = "Tajo Rápido de Navaja";
        data.secondaryAttackName = "Doble Puñalada Callejera";
        data.placeholderColor = new Color(0.85f, 0.45f, 0.15f, 1f);

        data.animatorController = RuntimeAssetCatalog.GetAnimatorController("Assets/Animations/Homeless_2/Homeless2_Controller.controller");

        Sprite[] idle = LoadAllSpritesAtPath("Assets/Sprites/Homeless_2/Idle.png");
        Sprite[] idleAlt = LoadAllSpritesAtPath("Assets/Sprites/Homeless_2/Idle_2.png");
        Sprite[] walk = LoadAllSpritesAtPath("Assets/Sprites/Homeless_2/Walk.png");
        Sprite[] run = LoadAllSpritesAtPath("Assets/Sprites/Homeless_2/Run.png");
        Sprite[] atk1 = LoadAllSpritesAtPath("Assets/Sprites/Homeless_2/Attack_1.png");
        Sprite[] atk2 = LoadAllSpritesAtPath("Assets/Sprites/Homeless_2/Attack_2.png");
        Sprite[] atk3 = LoadAllSpritesAtPath("Assets/Sprites/Homeless_2/Attack_3.png");
        Sprite[] hurt = LoadAllSpritesAtPath("Assets/Sprites/Homeless_2/Hurt.png");
        Sprite[] dead = LoadAllSpritesAtPath("Assets/Sprites/Homeless_2/Dead.png");

        if (idle != null && idle.Length > 0)
        {
            data.enemySprite = idle[0];
            data.idleFrames = idle;
        }
        if (idleAlt != null && idleAlt.Length > 0) data.idleAltFrames = idleAlt;
        if (walk != null && walk.Length > 0) data.walkFrames = walk;
        if (run != null && run.Length > 0) data.runFrames = run;
        if (atk1 != null && atk1.Length > 0) data.attackFrames = atk1;
        if (atk2 != null && atk2.Length > 0) data.attack2Frames = atk2;
        if (atk3 != null && atk3.Length > 0) data.attack3Frames = atk3;
        if (hurt != null && hurt.Length > 0) data.hurtFrames = hurt;
        if (dead != null && dead.Length > 0) data.deadFrames = dead;

        if (data.enemySprite == null)
        {
            data.enemySprite = LoadProjectSprite("Assets/Sprites/Homeless_2/Idle.png") ?? GetWhiteSquareSprite();
        }

        data.halfHealthDialogueLines = new string[]
        {
            "SICARIO CUCHILLERO: \"¡Maldito repartidor! ¡De este callejón no salen vivos ni tú ni ese sabueso!\""
        };
    }

    public static EnemyData GetOrCreateSicarioVerdugoData()
    {
#if UNITY_EDITOR
        string assetPath = "Assets/Data/Enemies/Sicario_Verdugo_N2.asset";
        EnemyData existing = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
        if (existing != null)
        {
            ConfigureSicarioVerdugo(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        EnemyData created = ScriptableObject.CreateInstance<EnemyData>();
        ConfigureSicarioVerdugo(created);
        if (!AssetDatabase.IsValidFolder("Assets/Data/Enemies"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            AssetDatabase.CreateFolder("Assets/Data", "Enemies");
        }
        AssetDatabase.CreateAsset(created, assetPath);
        EditorUtility.SetDirty(created);
        return created;
#else
        EnemyData runtimeData = ScriptableObject.CreateInstance<EnemyData>();
        ConfigureSicarioVerdugo(runtimeData);
        return runtimeData;
#endif
    }

    private static void ConfigureSicarioVerdugo(EnemyData data)
    {
        data.name = "Sicario_Verdugo_N2";
        data.enemyName = "Verdugo de Hacha";
        data.enemyType = EnemyType.SicarioVerdugoNivel2;
        data.maxHP = 95;
        data.attack = 17;
        data.defense = 6;
        data.motricidad = 10;
        data.EXP_Reward = 90;
        data.primaryAttackName = "Hachazo Demoledor";
        data.secondaryAttackName = "Barrido Brutal de Hacha";
        data.placeholderColor = new Color(0.55f, 0.1f, 0.1f, 1f);

        data.animatorController = RuntimeAssetCatalog.GetAnimatorController("Assets/Animations/Homeless_3/Homeless3_Controller.controller");

        Sprite[] idle = LoadAllSpritesAtPath("Assets/Sprites/Homeless_3/Idle.png");
        Sprite[] idleAlt = LoadAllSpritesAtPath("Assets/Sprites/Homeless_3/Idle_2.png");
        Sprite[] walk = LoadAllSpritesAtPath("Assets/Sprites/Homeless_3/Walk.png");
        Sprite[] run = LoadAllSpritesAtPath("Assets/Sprites/Homeless_3/Run.png");
        Sprite[] atk1 = LoadAllSpritesAtPath("Assets/Sprites/Homeless_3/Attack_1.png");
        Sprite[] atk2 = LoadAllSpritesAtPath("Assets/Sprites/Homeless_3/Attack_2.png");
        Sprite[] hurt = LoadAllSpritesAtPath("Assets/Sprites/Homeless_3/Hurt.png");
        Sprite[] dead = LoadAllSpritesAtPath("Assets/Sprites/Homeless_3/Dead.png");

        if (idle != null && idle.Length > 0)
        {
            data.enemySprite = idle[0];
            data.idleFrames = idle;
        }
        if (idleAlt != null && idleAlt.Length > 0) data.idleAltFrames = idleAlt;
        if (walk != null && walk.Length > 0) data.walkFrames = walk;
        if (run != null && run.Length > 0) data.runFrames = run;
        if (atk1 != null && atk1.Length > 0) data.attackFrames = atk1;
        if (atk2 != null && atk2.Length > 0) data.attack2Frames = atk2;
        if (hurt != null && hurt.Length > 0) data.hurtFrames = hurt;
        if (dead != null && dead.Length > 0) data.deadFrames = dead;

        if (data.enemySprite == null)
        {
            data.enemySprite = LoadProjectSprite("Assets/Sprites/Homeless_3/Idle.png") ?? GetWhiteSquareSprite();
        }

        data.halfHealthDialogueLines = new string[]
        {
            "VERDUGO DE HACHA: \"¡El Capataz nos ordenó partirles los huesos antes de que lleguen al almacén!\""
        };
    }

    public static EnemyData GetOrCreateCapatazBossData()
    {
#if UNITY_EDITOR
        string assetPath = "Assets/Data/Enemies/Jefe_CapatazBlindado_N2.asset";
        EnemyData existing = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
        if (existing != null)
        {
            ConfigureCapatazBoss(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        EnemyData created = ScriptableObject.CreateInstance<EnemyData>();
        ConfigureCapatazBoss(created);
        if (!AssetDatabase.IsValidFolder("Assets/Data/Enemies"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            AssetDatabase.CreateFolder("Assets/Data", "Enemies");
        }
        AssetDatabase.CreateAsset(created, assetPath);
        EditorUtility.SetDirty(created);
        return created;
#else
        EnemyData runtimeData = ScriptableObject.CreateInstance<EnemyData>();
        ConfigureCapatazBoss(runtimeData);
        return runtimeData;
#endif
    }

    private static Sprite[] LoadEnemyPunkFrames(string subfolder, string prefix, int count)
    {
        List<Sprite> list = new List<Sprite>();
        for (int i = 1; i <= count; i++)
        {
            string path = $"Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/{subfolder}/{prefix}{i}.png";
            Sprite sp = LoadProjectSprite(path);
            if (sp != null) list.Add(sp);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    private static void ConfigureCapatazBoss(EnemyData data)
    {
        data.name = "Jefe_CapatazBlindado_N2";
        data.enemyName = "El Capataz Punk";
        data.enemyType = EnemyType.JefeNivel2Capataz;
        data.maxHP = 185;
        data.attack = 22;
        data.defense = 8;
        data.motricidad = 12;
        data.EXP_Reward = 260;
        data.primaryAttackName = "Gancho Callejero Punk";
        data.secondaryAttackName = "Ráfaga de Nudillos";
        data.placeholderColor = new Color(0.95f, 0.35f, 0.20f, 1f);

        data.animatorController = RuntimeAssetCatalog.GetAnimatorController("Assets/Animations/Enemy_Punk/EnemyPunk_Controller.controller");

        Sprite[] idle = LoadEnemyPunkFrames("Idle", "idle", 4);
        Sprite[] walk = LoadEnemyPunkFrames("Walk", "walk", 4);
        Sprite[] punch = LoadEnemyPunkFrames("Punch", "punch", 3);
        Sprite[] hurt = LoadEnemyPunkFrames("Hurt", "hurt", 4);

        if (idle != null && idle.Length > 0)
        {
            data.enemySprite = idle[0];
            data.idleFrames = idle;
            data.idleAltFrames = idle;
        }
        if (walk != null && walk.Length > 0)
        {
            data.walkFrames = walk;
            data.runFrames = walk;
        }
        if (punch != null && punch.Length > 0)
        {
            data.attackFrames = punch;
            data.attack2Frames = punch;
        }
        if (hurt != null && hurt.Length > 0)
        {
            data.hurtFrames = hurt;
            data.deadFrames = new Sprite[] { hurt[2], hurt[3] };
        }

        if (data.enemySprite == null)
        {
            data.enemySprite = LoadProjectSprite("Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk/Idle/idle1.png") ?? GetWhiteSquareSprite();
        }

        data.halfHealthDialogueLines = new string[]
        {
            "EL CAPATAZ PUNK: \"¡No me van a tumbar unos novatos! ¡Les romperé la cara con mis propios nudillos!\"",
            "INVESTIGADOR PRIVADO: \"¡Cuidado, Enrique! Cuando enfurece aumenta la velocidad de sus golpes. ¡Aprovecha cuando baje la guardia tras su combo!\""
        };
    }

    private static void SpawnLevel2EnemiesAndBoss(Transform rootParent)
    {
        GameObject enemiesRoot = new GameObject("Enemigos_Nivel2_Callejon");
        enemiesRoot.transform.SetParent(rootParent, false);

        EnemyData cuchillero = GetOrCreateSicarioCuchilleroData();
        EnemyData verdugo = GetOrCreateSicarioVerdugoData();
        EnemyData capatazBoss = GetOrCreateCapatazBossData();

        // 1. Encuentro Doble Norte (1 Sicario Cuchillero + 1 Verdugo de Hacha)
        CreateDoubleEnemyEncounterOnMap(
            "Patrulla_Doble_Norte",
            enemiesRoot.transform,
            new Vector3(-0.8f, -4.5f, 0f),
            cuchillero,
            verdugo,
            "PATRULLA DEL CALLEJÓN (x2)"
        );

        // 2. Encuentro Doble Pasaje Vertical (2 Sicarios Cuchilleros veloces)
        CreateDoubleEnemyEncounterOnMap(
            "Patrulla_Doble_PasajeVertical",
            enemiesRoot.transform,
            new Vector3(-1.2f, -11.5f, 0f),
            cuchillero,
            cuchillero,
            "EMBOSCADA DE CUCHILLEROS (x2)"
        );

        // 3. Encuentro Doble Corredor Sur (1 Verdugo de Hacha + 1 Sicario Cuchillero)
        CreateDoubleEnemyEncounterOnMap(
            "Patrulla_Doble_PatioSur",
            enemiesRoot.transform,
            new Vector3(7.5f, -17.8f, 0f),
            verdugo,
            cuchillero,
            "GUARDIA EXTERIOR DE LA GUARIDA (x2)"
        );

        // 4. Jefe Final del Nivel 2: El Capataz Blindado (con Level2BossController y transición a Nivel_3)
        CreateLevel2BossEncounterOnMap(
            "Jefe_Nivel2_CapatazBlindado",
            enemiesRoot.transform,
            new Vector3(17.5f, -17.2f, 0f),
            capatazBoss
        );
    }

    private static GameObject CreateDoubleEnemyEncounterOnMap(
        string objectName,
        Transform parent,
        Vector3 position,
        EnemyData primaryEnemy,
        EnemyData secondaryEnemy,
        string badgeLabel)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(1.35f, 1.35f, 1f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D triggerCol = go.AddComponent<BoxCollider2D>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector2(1.2f, 1.4f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = (primaryEnemy != null && primaryEnemy.enemySprite != null) ? primaryEnemy.enemySprite : GetWhiteSquareSprite();
        sr.color = Color.white;
        sr.sortingOrder = 5;

        // Segundo sprite visual acompañante en el mapa para reflejar claramente que es una patrulla doble
        GameObject partnerVisGO = new GameObject("CompanionEnemyVisual");
        partnerVisGO.transform.SetParent(go.transform, false);
        partnerVisGO.transform.localPosition = new Vector3(0.55f, 0.12f, 0f);
        partnerVisGO.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
        SpriteRenderer partnerSR = partnerVisGO.AddComponent<SpriteRenderer>();
        partnerSR.sprite = (secondaryEnemy != null && secondaryEnemy.enemySprite != null) ? secondaryEnemy.enemySprite : sr.sprite;
        partnerSR.color = new Color(0.92f, 0.92f, 0.92f, 0.95f);
        partnerSR.sortingOrder = 4;

        EnemyFollowAI followAI = go.AddComponent<EnemyFollowAI>();
        followAI.spriteRenderer = sr;
        followAI.detectionRadius = 5.4f;
        followAI.stoppingDistance = 0.85f;
        followAI.walkSpeed = 1.9f;
        followAI.chaseSpeed = 3.2f;
        followAI.wanderRadius = 1.35f;

        EnemyEncounterTrigger encounter = go.AddComponent<EnemyEncounterTrigger>();
        encounter.forceDoubleEncounterGroup = true;
        encounter.enemies = new EnemyData[] { primaryEnemy, secondaryEnemy };
        encounter.isBossEncounter = false;

        return go;
    }

    private static GameObject CreateLevel2BossEncounterOnMap(
        string objectName,
        Transform parent,
        Vector3 position,
        EnemyData bossData)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(1.6f, 1.6f, 1f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D triggerCol = go.AddComponent<BoxCollider2D>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector2(1.1f, 1.5f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = (bossData != null && bossData.enemySprite != null) ? bossData.enemySprite : GetWhiteSquareSprite();
        sr.color = Color.white;
        sr.sortingOrder = 5;

        Animator anim = go.AddComponent<Animator>();
        if (bossData != null && bossData.animatorController != null)
        {
            anim.runtimeAnimatorController = bossData.animatorController;
        }

        EnemyFollowAI followAI = go.AddComponent<EnemyFollowAI>();
        followAI.spriteRenderer = sr;
        followAI.animator = anim;
        followAI.detectionRadius = 5.2f;
        followAI.stoppingDistance = 0.95f;
        followAI.walkSpeed = 1.6f;
        followAI.chaseSpeed = 2.8f;
        followAI.wanderRadius = 1.1f;

        Level2BossController bossController = go.AddComponent<Level2BossController>();
        bossController.bossDisplayName = "EL CAPATAZ PUNK";
        bossController.nextSceneIdentifier = "Nivel_3";

        EnemyEncounterTrigger encounter = go.AddComponent<EnemyEncounterTrigger>();
        encounter.forceDoubleEncounterGroup = false;
        encounter.enemies = new EnemyData[] { bossData };
        encounter.isBossEncounter = true;
        encounter.nextSceneName = "Nivel_3";
        encounter.level2BossController = bossController;
        encounter.victoryNarrativeLines = bossController.bossDefeatedNarrativeLines;

        return go;
    }

    // =========================================================================================
    // SISTEMA DE LOOT EN EL MAPA: BOTES DE BASURA Y BOLSAS DE BASURA (InteractableLoot)
    // =========================================================================================

    private static void SpawnLevel2TrashLootContainers(Transform rootParent)
    {
        GameObject lootRoot = new GameObject("Contenedores_Basura_Loot");
        lootRoot.transform.SetParent(rootParent, false);

        Sprite trashCanFull = LoadProjectSprite("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Trash-can_1.png");
        Sprite trashCanEmpty = LoadProjectSprite("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Trash-can_2.png");
        Sprite trashBagFull = LoadProjectSprite("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Trash-bag_1.png");
        Sprite trashBagEmpty = LoadProjectSprite("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Trash-bag_2.png");

        // Zona 1: Plazuela Superior
        CreateTrashLootContainer(
            "BoteBasura_PlazuelaOeste",
            lootRoot.transform,
            new Vector3(-10.8f, 2.5f, 0f),
            TrashContainerType.BoteDeBasura,
            "Bote de Basura Oxidado",
            trashCanFull,
            trashCanEmpty
        );

        CreateTrashLootContainer(
            "BolsaBasura_EsquinaNorte",
            lootRoot.transform,
            new Vector3(-0.4f, 2.5f, 0f),
            TrashContainerType.BolsaDeBasura,
            "Bolsa de Basura Negra",
            trashBagFull,
            trashBagEmpty
        );

        // Zona 2: Pasaje Estrecho Vertical
        CreateTrashLootContainer(
            "BolsaBasura_PasajeAlto",
            lootRoot.transform,
            new Vector3(-2.5f, -6.2f, 0f),
            TrashContainerType.BolsaDeBasura,
            "Bolsa de Basura del Pasaje",
            trashBagFull,
            trashBagEmpty
        );

        CreateTrashLootContainer(
            "BoteBasura_ContenedorVerde",
            lootRoot.transform,
            new Vector3(-0.3f, -14.2f, 0f),
            TrashContainerType.BoteDeBasura,
            "Bote de Basura del Callejón",
            trashCanFull,
            trashCanEmpty
        );

        // Zona 3: Corredor Sur hacia la Guarida
        CreateTrashLootContainer(
            "BoteBasura_CorredorSur_Oeste",
            lootRoot.transform,
            new Vector3(3.6f, -16.6f, 0f),
            TrashContainerType.BoteDeBasura,
            "Bote de Basura Callejero",
            trashCanFull,
            trashCanEmpty
        );

        CreateTrashLootContainer(
            "BolsaBasura_CarpasSur",
            lootRoot.transform,
            new Vector3(11.2f, -18.6f, 0f),
            TrashContainerType.BolsaDeBasura,
            "Bolsa de Basura Abandonada",
            trashBagFull,
            trashBagEmpty
        );

        CreateTrashLootContainer(
            "BoteBasura_AntesalaJefe",
            lootRoot.transform,
            new Vector3(15.2f, -16.6f, 0f),
            TrashContainerType.BoteDeBasura,
            "Bote de Basura de la Guarida",
            trashCanFull,
            trashCanEmpty
        );
    }

    private static GameObject CreateTrashLootContainer(
        string objectName,
        Transform parent,
        Vector3 position,
        TrashContainerType containerType,
        string displayName,
        Sprite unlootedSprite,
        Sprite lootedSprite)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(3.2f, 3.2f, 1f);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.55f, 0.55f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = unlootedSprite != null ? unlootedSprite : GetWhiteSquareSprite();
        sr.color = Color.white;
        sr.sortingOrder = 4;

        InteractableLoot loot = go.AddComponent<InteractableLoot>();
        loot.containerType = containerType;
        loot.containerDisplayName = displayName;
        loot.isLooted = false;
        loot.showExplorationDialogueOnLoot = true;
        loot.spriteRenderer = sr;
        loot.unlootedSprite = unlootedSprite;
        loot.lootedSprite = lootedSprite;
        loot.lootTable = BuildProjectLootTable();

        GameObject promptGO = CreateWorldPromptLabel("PromptUI_E", go.transform, new Vector3(0f, 0.30f, 0f), $"[E] REGISTRAR {displayName.ToUpper()}");
        promptGO.transform.localScale = new Vector3(0.30f, 0.30f, 1f);
        loot.promptUI = promptGO;
        loot.promptLabel = promptGO.GetComponent<TextMeshPro>();

        return go;
    }

    private static List<LootTableEntry> BuildProjectLootTable()
    {
        List<LootTableEntry> table = InteractableLoot.BuildDefaultAlleyLootTable();
        ItemData refresco = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Item_Refresco.asset");
        ItemData torta = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Item_Torta.asset");
        ItemData botella = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Item_BotellaVacia.asset")
                           ?? RuntimeAssetCatalog.GetItem("Assets/Data/Items/Botella_Vacia.asset");
        ItemData chile = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Item_ChilePolvo.asset")
                         ?? RuntimeAssetCatalog.GetItem("Assets/Data/Items/Chile_Polvo.asset");
        ItemData cinta = RuntimeAssetCatalog.GetItem("Assets/Data/Items/Cinta_Canela.asset");

        for (int i = 0; i < table.Count; i++)
        {
            LootTableEntry entry = table[i];
            if (entry.rewardKind == LootRewardKind.InventoryItem)
            {
                if (entry.fallbackItemName.Contains("Refresco") && refresco != null) entry.itemData = refresco;
                else if (entry.fallbackItemName.Contains("Torta") && torta != null) entry.itemData = torta;
                else if (entry.fallbackItemName.Contains("Botella") && botella != null) entry.itemData = botella;
                else if (entry.fallbackItemName.Contains("Chile") && chile != null) entry.itemData = chile;
                else if (entry.fallbackItemName.Contains("Cinta") && cinta != null) entry.itemData = cinta;
            }
        }
        return table;
    }

    private static Tilemap CreateGridWithTilemap(string gridName, string layerName, Transform parent, Vector3 layerLocalPos, int sortingOrder)
    {
        GameObject gridGO = new GameObject(gridName);
        gridGO.transform.SetParent(parent, false);
        gridGO.transform.localPosition = Vector3.zero;
        gridGO.transform.localScale = new Vector3(5f, 5f, 1f);

        Grid grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(0.16f, 0.16f, 0f);
        grid.cellGap = Vector3.zero;
        grid.cellLayout = GridLayout.CellLayout.Rectangle;

        GameObject layerGO = new GameObject(layerName);
        layerGO.transform.SetParent(gridGO.transform, false);
        layerGO.transform.localPosition = layerLocalPos;
        layerGO.transform.localScale = Vector3.one;

        Tilemap tm = layerGO.AddComponent<Tilemap>();
        TilemapRenderer tr = layerGO.AddComponent<TilemapRenderer>();
        tr.sortingOrder = sortingOrder;
        return tm;
    }

    private static void PopulateHarmoniousAlleyMap(Tilemap suelo, Tilemap edificios, Tilemap decoracionO, Tilemap decoracionN, Transform colliders)
    {
        // =========================================================================
        // 1. ZONA 1: ENTRADA DESDE LA CALLE Y PLAZUELA SUPERIOR (x = -16..3, y = -4..4)
        // =========================================================================
        StampStreetEntranceWest(suelo, -16, -11);
        FillRect(suelo, -10, 3, -4, 4, BY(23));
        StampFramedDrainagePatch(edificios, decoracionO, -8, -3, -3, 2);

        StampBrickBuildingA(edificios, decoracionO, colliders, -16, -8, 5, true);
        StampConcreteBuildingB(edificios, colliders, -7, -1, 5);
        StampDarkTowerBuildingC(edificios, decoracionO, colliders, 0, 7, 5);

        StampRooftopDarkTar(edificios, colliders, 4, 10, -4, 4, false);
        StampCrateStack2x2(edificios, colliders, 2, 2, false);
        StampCrateStack2x2(edificios, colliders, 2, 0, false);
        StampCrateStack2x2(edificios, colliders, 2, -2, false);

        StampRooftopDarkTar(edificios, colliders, -16, -11, -11, -5, true);
        StampRooftopLightConcrete(edificios, colliders, -10, -5, -11, -5);

        StampStreetLamp1x3(decoracionO, -8, 4);
        decoracionO.SetTile(new Vector3Int(-14, 4, 0), GD(0));
        decoracionO.SetTile(new Vector3Int(-13, 4, 0), GD(3));
        decoracionO.SetTile(new Vector3Int(-10, 4, 0), GD(4));
        StampBench2x1(decoracionO, colliders, -5, 4);
        decoracionO.SetTile(new Vector3Int(0, 4, 0), GD(5));
        decoracionO.SetTile(new Vector3Int(1, 4, 0), GD(14));

        // =========================================================================
        // 2. ZONA 2: PASAJE ESTRECHO VERTICAL HACIA EL SUR (x = -4..1, y = -19..-5)
        // =========================================================================
        FillRect(edificios, -4, 1, -12, -5, BY(23));
        StampVerticalAlleyChannel(edificios, -4, -19, -13);

        StampRooftopBrownTile(edificios, colliders, -12, -5, -19, -12);
        StampRooftopBeige(edificios, colliders, -16, -13, -19, -12);

        StampRooftopDarkTar(edificios, colliders, 4, 10, -11, -5, false);
        StampCrateStack2x2(edificios, colliders, 2, -7, true);
        StampCrateStack2x2(edificios, colliders, 2, -9, true);
        StampCrateStack2x2(edificios, colliders, 2, -11, true);

        decoracionO.SetTile(new Vector3Int(-4, -7, 0), GD(3));
        decoracionO.SetTile(new Vector3Int(-4, -8, 0), GD(5));
        StampElectricPole1x3(decoracionO, 1, -12);
        StampStreetLamp1x3(decoracionO, -4, -16);
        StampDumpster2x2(decoracionO, colliders, 0, -17);
        if (TP(246) != null)
        {
            decoracionO.SetTile(new Vector3Int(-3, -13, 0), TP(246));
            decoracionO.SetTile(new Vector3Int(0, -13, 0), TP(246));
        }

        // =========================================================================
        // 3. ZONA 3: CALLEJÓN PROFUNDO DEL SUR Y GUARIDA DE EL CALACAS (x = -4..27, y = -25..-20)
        // =========================================================================
        FillRect(edificios, -4, 27, -25, -20, BY(23));
        StampFramedDrainagePatch(edificios, decoracionO, 6, 11, -24, -21);

        StampShopBrickBuildingD(edificios, decoracionO, decoracionN, colliders, 2, 12, -19);
        StampConcreteBuildingB(edificios, colliders, 13, 19, -19);
        StampIndustrialWarehouseE(edificios, decoracionO, colliders, 20, 26, -19);

        StampRooftopLightConcrete(edificios, colliders, 11, 18, -11, -4);
        StampRooftopDarkTar(edificios, colliders, 19, 26, -12, -4, true);

        StampRooftopDarkTar(edificios, colliders, 28, 33, -25, -13, false);
        StampCrateStack2x2(edificios, colliders, 26, -21, false);
        StampCrateStack2x2(edificios, colliders, 26, -23, false);
        StampCrateStack2x2(edificios, colliders, 26, -25, false);

        StampRooftopDarkTar(edificios, colliders, -12, -3, -32, -26, true);
        StampRooftopLightConcrete(edificios, colliders, -2, 8, -32, -26);
        StampRooftopBeige(edificios, colliders, 9, 17, -32, -26);
        StampRooftopBrownTile(edificios, colliders, 18, 27, -32, -26);

        StampStreetLamp1x3(decoracionO, 2, -20);
        decoracionO.SetTile(new Vector3Int(4, -20, 0), GD(4));
        decoracionO.SetTile(new Vector3Int(5, -20, 0), GD(5));
        decoracionO.SetTile(new Vector3Int(6, -20, 0), GD(14));
        decoracionO.SetTile(new Vector3Int(14, -20, 0), TX(8));
        decoracionO.SetTile(new Vector3Int(15, -20, 0), TX(9));
        if (TP(676) != null)
        {
            decoracionO.SetTile(new Vector3Int(18, -20, 0), TP(676));
            decoracionO.SetTile(new Vector3Int(19, -20, 0), TP(676));
        }

        decoracionO.SetTile(new Vector3Int(-1, -25, 0), GD(0));
        decoracionO.SetTile(new Vector3Int(0, -25, 0), GD(3));
        decoracionO.SetTile(new Vector3Int(1, -25, 0), GD(5));
        StampBench2x1(decoracionO, colliders, 3, -25);
        StampCanopy3x2(decoracionO, colliders, 12, -25, false);
        StampCanopy3x2(decoracionO, colliders, 15, -25, true);

        StampSmallBrickOutbuilding3x4(decoracionO, colliders, 20, -24);

        AddGridBoxCollider("Limite_Oeste_Entrada", colliders, -17, -17, -5, 5);
    }

    // =========================================================================================
    // ESTAMPAS ARQUITECTÓNICAS EXACTAS DE LA ESCENA 1
    // =========================================================================================

    private static void FillRect(Tilemap tm, int xMin, int xMax, int yMin, int yMax, TileBase tile)
    {
        if (tile == null) return;
        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                tm.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    private static void AddGridBoxCollider(string name, Transform parent, int xMin, int xMax, int yMin, int yMax)
    {
        if (parent == null) return;
        float worldXMin = xMin * CellWorldSize;
        float worldXMax = (xMax + 1) * CellWorldSize;
        float worldYMin = yMin * CellWorldSize;
        float worldYMax = (yMax + 1) * CellWorldSize;

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((worldXMin + worldXMax) * 0.5f, (worldYMin + worldYMax) * 0.5f, 0f);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = false;
        col.size = new Vector2(worldXMax - worldXMin, worldYMax - worldYMin);
    }

    private static void StampStreetEntranceWest(Tilemap suelo, int xMin, int xMax)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isFirst = (x == xMin);
            bool isCrosswalk = (x >= xMax - 3 && x <= xMax - 1);
            bool isRightEdge = (x == xMax);

            suelo.SetTile(new Vector3Int(x, 4, 0), isFirst ? BY(6) : BY(48));
            suelo.SetTile(new Vector3Int(x, 3, 0), isFirst ? BY(28) : (isRightEdge ? BY(190) : BY(188)));
            suelo.SetTile(new Vector3Int(x, 2, 0), isFirst ? BY(157) : (isRightEdge ? BY(162) : BY(161)));

            if (isFirst)
            {
                suelo.SetTile(new Vector3Int(x, 1, 0), BY(181));
                suelo.SetTile(new Vector3Int(x, 0, 0), BY(181));
                suelo.SetTile(new Vector3Int(x, -1, 0), BY(181));
            }
            else if (isRightEdge)
            {
                suelo.SetTile(new Vector3Int(x, 1, 0), BY(32));
                suelo.SetTile(new Vector3Int(x, 0, 0), BY(32));
                suelo.SetTile(new Vector3Int(x, -1, 0), BY(32));
            }
            else if (isCrosswalk)
            {
                suelo.SetTile(new Vector3Int(x, 1, 0), BY(184));
                suelo.SetTile(new Vector3Int(x, 0, 0), BY(210));
                suelo.SetTile(new Vector3Int(x, -1, 0), BY(229));
            }
            else
            {
                suelo.SetTile(new Vector3Int(x, 1, 0), BY(210));
                suelo.SetTile(new Vector3Int(x, 0, 0), BY(159));
                suelo.SetTile(new Vector3Int(x, -1, 0), BY(210));
            }

            suelo.SetTile(new Vector3Int(x, -2, 0), isRightEdge ? BY(231) : BY(227));
            suelo.SetTile(new Vector3Int(x, -3, 0), isFirst ? BY(6) : (isRightEdge ? BY(190) : BY(188)));
            suelo.SetTile(new Vector3Int(x, -4, 0), isFirst ? BY(28) : (isRightEdge ? BY(10) : BY(9)));
        }
    }

    private static void StampFramedDrainagePatch(Tilemap edificios, Tilemap decoracionO, int xMin, int xMax, int yMin, int yMax)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            edificios.SetTile(new Vector3Int(x, yMax, 0), isLeft ? BY(8) : (isRight ? BY(10) : BY(9)));
            edificios.SetTile(new Vector3Int(x, yMin, 0), isLeft ? BY(47) : (isRight ? BY(49) : BY(48)));
        }

        for (int y = yMin + 1; y <= yMax - 1; y++)
        {
            edificios.SetTile(new Vector3Int(xMin, y, 0), BY(30));
            edificios.SetTile(new Vector3Int(xMax, y, 0), BY(32));

            for (int x = xMin + 1; x <= xMax - 1; x++)
            {
                bool isFirstInner = (x == xMin + 1);
                bool isLastInner = (x == xMax - 1);

                if (y == yMax - 1)
                {
                    edificios.SetTile(new Vector3Int(x, y, 0), isFirstInner ? BY(35) : (isLastInner ? BY(16) : BY(15)));
                }
                else if (y == yMin + 1)
                {
                    edificios.SetTile(new Vector3Int(x, y, 0), isFirstInner ? BY(53) : (isLastInner ? BY(55) : BY(54)));
                    decoracionO.SetTile(new Vector3Int(x, y, 0), isFirstInner ? BY(53) : (isLastInner ? BY(55) : BY(54)));
                }
                else
                {
                    TileBase inner = isFirstInner ? BY(35) : (isLastInner ? BY(37) : BY(5));
                    edificios.SetTile(new Vector3Int(x, y, 0), inner);
                    decoracionO.SetTile(new Vector3Int(x, y, 0), inner);
                }
            }
        }
    }

    private static void StampVerticalAlleyChannel(Tilemap edificios, int xLeft, int yBottom, int yTop)
    {
        edificios.SetTile(new Vector3Int(xLeft, yTop, 0), BY(23));
        edificios.SetTile(new Vector3Int(xLeft + 1, yTop, 0), BY(9));
        edificios.SetTile(new Vector3Int(xLeft + 2, yTop, 0), BY(9));
        edificios.SetTile(new Vector3Int(xLeft + 3, yTop, 0), BY(9));
        edificios.SetTile(new Vector3Int(xLeft + 4, yTop, 0), BY(9));
        edificios.SetTile(new Vector3Int(xLeft + 5, yTop, 0), BY(23));

        int yStep = yTop - 1;
        edificios.SetTile(new Vector3Int(xLeft, yStep, 0), BY(30));
        edificios.SetTile(new Vector3Int(xLeft + 1, yStep, 0), BY(157));
        edificios.SetTile(new Vector3Int(xLeft + 2, yStep, 0), BY(161));
        edificios.SetTile(new Vector3Int(xLeft + 3, yStep, 0), BY(161));
        edificios.SetTile(new Vector3Int(xLeft + 4, yStep, 0), BY(162));
        edificios.SetTile(new Vector3Int(xLeft + 5, yStep, 0), BY(32));

        for (int y = yTop - 2; y >= yBottom; y--)
        {
            int offset = (yTop - 2) - y;
            TileBase centerTile = (offset % 3 == 0) ? BY(205) : ((offset % 3 == 1) ? BY(226) : BY(248));

            edificios.SetTile(new Vector3Int(xLeft, y, 0), BY(30));
            edificios.SetTile(new Vector3Int(xLeft + 1, y, 0), BY(181));
            edificios.SetTile(new Vector3Int(xLeft + 2, y, 0), centerTile);
            edificios.SetTile(new Vector3Int(xLeft + 3, y, 0), centerTile);
            edificios.SetTile(new Vector3Int(xLeft + 4, y, 0), BY(186));
            edificios.SetTile(new Vector3Int(xLeft + 5, y, 0), BY(32));
        }
    }

    private static void StampBrickBuildingA(Tilemap edificios, Tilemap decoracionO, Transform colliders, int xMin, int xMax, int yBase, bool withAwning)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);

            edificios.SetTile(new Vector3Int(x, yBase, 0), isLeft ? TP(371) : (isRight ? TP(373) : TP(298)));
            edificios.SetTile(new Vector3Int(x, yBase + 1, 0), isLeft ? TP(186) : (isRight ? TP(188) : TP(187)));
            edificios.SetTile(new Vector3Int(x, yBase + 2, 0), isLeft ? TP(186) : (isRight ? TP(188) : TP(187)));
            edificios.SetTile(new Vector3Int(x, yBase + 3, 0), isLeft ? TP(260) : (isRight ? TP(262) : TP(261)));
            edificios.SetTile(new Vector3Int(x, yBase + 4, 0), isLeft ? TP(186) : (isRight ? TP(188) : TP(187)));
            edificios.SetTile(new Vector3Int(x, yBase + 5, 0), isLeft ? TP(186) : (isRight ? TP(188) : TP(187)));
            edificios.SetTile(new Vector3Int(x, yBase + 6, 0), isLeft ? TP(260) : (isRight ? TP(262) : TP(261)));
        }

        int[] winCols = new int[] { xMin + 2, xMax - 2 };
        foreach (int wx in winCols)
        {
            decoracionO.SetTile(new Vector3Int(wx, yBase + 5, 0), TP(694));
            decoracionO.SetTile(new Vector3Int(wx, yBase + 4, 0), TP(731));
        }
        decoracionO.SetTile(new Vector3Int(xMax - 2, yBase + 3, 0), TP(548));

        if (withAwning)
        {
            decoracionO.SetTile(new Vector3Int(xMin + 3, yBase + 1, 0), TP(911));
            decoracionO.SetTile(new Vector3Int(xMin + 4, yBase + 1, 0), TP(912));
            decoracionO.SetTile(new Vector3Int(xMin + 3, yBase, 0), TP(948));
            decoracionO.SetTile(new Vector3Int(xMin + 4, yBase, 0), TP(949));
        }

        AddGridBoxCollider($"Col_EdificioLadrillo_{xMin}_{yBase}", colliders, xMin, xMax, yBase, yBase + 6);
    }

    private static void StampConcreteBuildingB(Tilemap edificios, Transform colliders, int xMin, int xMax, int yBase)
    {
        int center = (xMin + xMax) / 2;
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            int bayOffset = x - (center - 1);

            edificios.SetTile(new Vector3Int(x, yBase, 0), isLeft ? TP(452) : (isRight ? TP(453) : TP(306)));

            if (bayOffset >= 0 && bayOffset <= 2)
            {
                edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(309 + bayOffset));
                edificios.SetTile(new Vector3Int(x, yBase + 2, 0), TP(272 + bayOffset));
                edificios.SetTile(new Vector3Int(x, yBase + 3, 0), TP(235 + bayOffset));
                edificios.SetTile(new Vector3Int(x, yBase + 4, 0), TP(235 + bayOffset));
            }
            else
            {
                edificios.SetTile(new Vector3Int(x, yBase + 1, 0), isLeft ? TP(415) : (isRight ? TP(416) : TP(195)));
                edificios.SetTile(new Vector3Int(x, yBase + 2, 0), isLeft ? TP(415) : (isRight ? TP(416) : TP(195)));
                edificios.SetTile(new Vector3Int(x, yBase + 3, 0), isLeft ? TP(417) : (isRight ? TP(418) : TP(232)));
                edificios.SetTile(new Vector3Int(x, yBase + 4, 0), isLeft ? TP(415) : (isRight ? TP(416) : TP(195)));
            }

            edificios.SetTile(new Vector3Int(x, yBase + 5, 0), isLeft ? TP(415) : (isRight ? TP(416) : TP(195)));
        }

        AddGridBoxCollider($"Col_EdificioConcreto_{xMin}_{yBase}", colliders, xMin, xMax, yBase, yBase + 5);
    }

    private static void StampDarkTowerBuildingC(Tilemap edificios, Tilemap decoracionO, Transform colliders, int xMin, int xMax, int yBase)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            int bayIdx = x - (xMin + 1);

            edificios.SetTile(new Vector3Int(x, yBase, 0), isLeft ? TP(448) : (isRight ? TP(449) : TP(302)));
            edificios.SetTile(new Vector3Int(x, yBase + 1, 0), isLeft ? TP(411) : (isRight ? TP(412) : TP(191)));
            edificios.SetTile(new Vector3Int(x, yBase + 2, 0), isLeft ? TP(411) : (isRight ? TP(412) : TP(191)));
            edificios.SetTile(new Vector3Int(x, yBase + 3, 0), isLeft ? TP(450) : (isRight ? TP(451) : TP(265)));

            if (!isLeft && !isRight && bayIdx >= 0 && bayIdx <= 2)
            {
                edificios.SetTile(new Vector3Int(x, yBase + 4, 0), TP(346 + bayIdx));
                edificios.SetTile(new Vector3Int(x, yBase + 5, 0), TP(420 + bayIdx));
            }
            else
            {
                edificios.SetTile(new Vector3Int(x, yBase + 4, 0), isLeft ? TP(411) : (isRight ? TP(412) : TP(191)));
                edificios.SetTile(new Vector3Int(x, yBase + 5, 0), isLeft ? TP(411) : (isRight ? TP(412) : TP(191)));
            }
        }

        decoracionO.SetTile(new Vector3Int(xMin + 1, yBase, 0), TP(406));
        decoracionO.SetTile(new Vector3Int(xMin + 1, yBase - 1, 0), TP(480));
        decoracionO.SetTile(new Vector3Int(xMin + 4, yBase, 0), TP(982));
        decoracionO.SetTile(new Vector3Int(xMin + 4, yBase - 1, 0), TP(636));
        decoracionO.SetTile(new Vector3Int(xMin + 4, yBase - 2, 0), TP(673));
        decoracionO.SetTile(new Vector3Int(xMax - 1, yBase, 0), TP(406));
        decoracionO.SetTile(new Vector3Int(xMax - 1, yBase - 1, 0), TP(480));

        AddGridBoxCollider($"Col_TorreGris_{xMin}_{yBase}", colliders, xMin, xMax, yBase, yBase + 5);
    }

    private static void StampShopBrickBuildingD(Tilemap edificios, Tilemap decoracionO, Tilemap decoracionN, Transform colliders, int xMin, int xMax, int yBase)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            int localX = x - xMin;

            if (isLeft) edificios.SetTile(new Vector3Int(x, yBase, 0), TP(444));
            else if (isRight) edificios.SetTile(new Vector3Int(x, yBase, 0), TP(445));
            else if (localX == 6) edificios.SetTile(new Vector3Int(x, yBase, 0), TP(955));
            else edificios.SetTile(new Vector3Int(x, yBase, 0), TP(298));

            if (isLeft) edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(407));
            else if (isRight) edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(408));
            else if (localX == 5) edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(917));
            else if (localX == 6) edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(918));
            else edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(187));

            edificios.SetTile(new Vector3Int(x, yBase + 2, 0), isLeft ? TP(407) : (isRight ? TP(408) : TP(187)));
            edificios.SetTile(new Vector3Int(x, yBase + 3, 0), isLeft ? TP(334) : (isRight ? TP(336) : TP(335)));

            for (int dy = 4; dy <= 7; dy++)
            {
                edificios.SetTile(new Vector3Int(x, yBase + dy, 0), isLeft ? TP(407) : (isRight ? TP(408) : TP(187)));
            }

            TileBase tTop = isLeft ? TP(356) : ((localX == 1) ? TP(357) : (isRight ? TP(359) : TP(358)));
            TileBase tMid = isLeft ? TP(393) : ((localX == 1) ? TP(394) : (isRight ? TP(396) : TP(395)));
            TileBase tBot = isLeft ? TP(430) : ((localX == 1) ? TP(431) : (isRight ? TP(433) : TP(432)));
            decoracionO.SetTile(new Vector3Int(x, yBase + 3, 0), tTop);
            decoracionO.SetTile(new Vector3Int(x, yBase + 2, 0), tMid);
            decoracionO.SetTile(new Vector3Int(x, yBase + 1, 0), tBot);
        }

        int[] winOffsets = new int[] { 2, 5, 8 };
        foreach (int wo in winOffsets)
        {
            decoracionO.SetTile(new Vector3Int(xMin + wo, yBase + 5, 0), TP(694));
            decoracionO.SetTile(new Vector3Int(xMin + wo, yBase + 4, 0), TP(731));
        }
        decoracionO.SetTile(new Vector3Int(xMin + 9, yBase + 5, 0), TP(548));

        AddGridBoxCollider($"Col_EdificioTienda_{xMin}_{yBase}", colliders, xMin, xMax, yBase, yBase + 7);
    }

    private static void StampIndustrialWarehouseE(Tilemap edificios, Tilemap decoracionO, Transform colliders, int xMin, int xMax, int yBase)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            int localX = x - xMin;

            edificios.SetTile(new Vector3Int(x, yBase, 0), isLeft ? TP(999) : (isRight ? TP(1001) : TP(1000)));
            for (int dy = 1; dy <= 5; dy++)
            {
                edificios.SetTile(new Vector3Int(x, yBase + dy, 0), isLeft ? TP(962) : (isRight ? TP(964) : TP(963)));
            }
            edificios.SetTile(new Vector3Int(x, yBase + 6, 0), isLeft ? TP(925) : (isRight ? TP(927) : TP(926)));

            TileBase AwningTop = isLeft ? TP(644) : (isRight ? TP(648) : TP(645));
            TileBase AwningBot = isLeft ? TP(681) : (isRight ? TP(685) : ((localX == xMax - xMin - 1) ? TP(757) : TP(682)));
            decoracionO.SetTile(new Vector3Int(x, yBase + 1, 0), AwningTop);
            decoracionO.SetTile(new Vector3Int(x, yBase, 0), AwningBot);
        }

        if (xMax - xMin >= 6)
        {
            decoracionO.SetTile(new Vector3Int(xMin + 1, yBase + 5, 0), TP(492));
            decoracionO.SetTile(new Vector3Int(xMin + 2, yBase + 5, 0), TP(485));
            decoracionO.SetTile(new Vector3Int(xMin + 3, yBase + 5, 0), TP(486));
            decoracionO.SetTile(new Vector3Int(xMin + 4, yBase + 5, 0), TP(487));
            decoracionO.SetTile(new Vector3Int(xMin + 5, yBase + 5, 0), TP(491));

            decoracionO.SetTile(new Vector3Int(xMin + 2, yBase + 6, 0), TP(572));
            decoracionO.SetTile(new Vector3Int(xMin + 3, yBase + 6, 0), TP(573));
            decoracionO.SetTile(new Vector3Int(xMin + 5, yBase + 6, 0), TP(570));
        }

        StampStreetLamp1x3(decoracionO, xMin, yBase + 2);
        StampElectricPole1x3(decoracionO, xMax, yBase + 2);
        decoracionO.SetTile(new Vector3Int(xMin + 2, yBase + 2, 0), TP(680));
        decoracionO.SetTile(new Vector3Int(xMax - 1, yBase + 2, 0), TP(680));

        AddGridBoxCollider($"Col_AlmacenCalacas_{xMin}_{yBase}", colliders, xMin, xMax, yBase, yBase + 6);
    }

    private static void StampRooftopDarkTar(Tilemap edificios, Transform colliders, int xMin, int xMax, int yMin, int yMax, bool useAltFill)
    {
        TileBase fill = useAltFill ? TP(7) : TP(6);
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            edificios.SetTile(new Vector3Int(x, yMax, 0), isLeft ? TP(0) : (isRight ? TP(1) : TP(2)));
            for (int y = yMin + 1; y <= yMax - 1; y++)
            {
                edificios.SetTile(new Vector3Int(x, y, 0), isLeft ? TP(3) : (isRight ? TP(40) : fill));
            }
            edificios.SetTile(new Vector3Int(x, yMin, 0), isLeft ? TP(37) : (isRight ? TP(38) : TP(39)));
        }
        AddGridBoxCollider($"Col_AzoteaTar_{xMin}_{yMin}", colliders, xMin, xMax, yMin, yMax);
    }

    private static void StampRooftopLightConcrete(Tilemap edificios, Transform colliders, int xMin, int xMax, int yMin, int yMax)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            edificios.SetTile(new Vector3Int(x, yMax, 0), isLeft ? TP(8) : (isRight ? TP(9) : TP(10)));
            for (int y = yMin + 1; y <= yMax - 1; y++)
            {
                edificios.SetTile(new Vector3Int(x, y, 0), isLeft ? TP(11) : (isRight ? TP(48) : TP(14)));
            }
            edificios.SetTile(new Vector3Int(x, yMin, 0), isRight ? TP(46) : TP(47));
        }
        AddGridBoxCollider($"Col_AzoteaConcreto_{xMin}_{yMin}", colliders, xMin, xMax, yMin, yMax);
    }

    private static void StampRooftopBrownTile(Tilemap edificios, Transform colliders, int xMin, int xMax, int yMin, int yMax)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            edificios.SetTile(new Vector3Int(x, yMax, 0), isLeft ? TP(24) : (isRight ? TP(25) : TP(26)));
            for (int y = yMin + 1; y <= yMax - 1; y++)
            {
                edificios.SetTile(new Vector3Int(x, y, 0), isLeft ? TP(27) : (isRight ? TP(64) : TP(30)));
            }
            edificios.SetTile(new Vector3Int(x, yMin, 0), isLeft ? TP(61) : (isRight ? TP(62) : TP(63)));
        }
        AddGridBoxCollider($"Col_AzoteaTeja_{xMin}_{yMin}", colliders, xMin, xMax, yMin, yMax);
    }

    private static void StampRooftopBeige(Tilemap edificios, Transform colliders, int xMin, int xMax, int yMin, int yMax)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            edificios.SetTile(new Vector3Int(x, yMax, 0), isLeft ? TP(16) : (isRight ? TP(17) : TP(18)));
            for (int y = yMin; y <= yMax - 1; y++)
            {
                edificios.SetTile(new Vector3Int(x, y, 0), isLeft ? TP(19) : (isRight ? TP(56) : TP(22)));
            }
        }
        AddGridBoxCollider($"Col_AzoteaBeige_{xMin}_{yMin}", colliders, xMin, xMax, yMin, yMax);
    }

    private static void StampCrateStack2x2(Tilemap edificios, Transform colliders, int x, int yBottom, bool variantB)
    {
        edificios.SetTile(new Vector3Int(x, yBottom + 1, 0), variantB ? TP(471) : TP(469));
        edificios.SetTile(new Vector3Int(x + 1, yBottom + 1, 0), variantB ? TP(472) : TP(470));
        edificios.SetTile(new Vector3Int(x, yBottom, 0), variantB ? TP(508) : TP(506));
        edificios.SetTile(new Vector3Int(x + 1, yBottom, 0), variantB ? TP(509) : TP(507));
        AddGridBoxCollider($"Col_Cajas_{x}_{yBottom}", colliders, x, x + 1, yBottom, yBottom + 1);
    }

    private static void StampDumpster2x2(Tilemap decoracionO, Transform colliders, int x, int yBottom)
    {
        decoracionO.SetTile(new Vector3Int(x, yBottom + 1, 0), TP(845));
        decoracionO.SetTile(new Vector3Int(x + 1, yBottom + 1, 0), TP(846));
        decoracionO.SetTile(new Vector3Int(x, yBottom, 0), TP(882));
        decoracionO.SetTile(new Vector3Int(x + 1, yBottom, 0), TP(883));
        AddGridBoxCollider($"Col_Contenedor_{x}_{yBottom}", colliders, x, x + 1, yBottom, yBottom + 1);
    }

    private static void StampBench2x1(Tilemap decoracionO, Transform colliders, int x, int y)
    {
        decoracionO.SetTile(new Vector3Int(x, y, 0), TP(609));
        decoracionO.SetTile(new Vector3Int(x + 1, y, 0), TP(610));
        AddGridBoxCollider($"Col_Banca_{x}_{y}", colliders, x, x + 1, y, y);
    }

    private static void StampCanopy3x2(Tilemap decoracionO, Transform colliders, int x, int yBottom, bool blueVariant)
    {
        decoracionO.SetTile(new Vector3Int(x, yBottom + 1, 0), blueVariant ? TP(771) : TP(623));
        decoracionO.SetTile(new Vector3Int(x + 1, yBottom + 1, 0), blueVariant ? TP(772) : TP(624));
        decoracionO.SetTile(new Vector3Int(x + 2, yBottom + 1, 0), blueVariant ? TP(773) : TP(625));
        decoracionO.SetTile(new Vector3Int(x, yBottom, 0), blueVariant ? TP(808) : TP(660));
        decoracionO.SetTile(new Vector3Int(x + 1, yBottom, 0), blueVariant ? TP(809) : TP(661));
        decoracionO.SetTile(new Vector3Int(x + 2, yBottom, 0), blueVariant ? TP(810) : TP(662));
        AddGridBoxCollider($"Col_Carpa_{x}_{yBottom}", colliders, x, x + 2, yBottom, yBottom + 1);
    }

    private static void StampSmallBrickOutbuilding3x4(Tilemap decoracionO, Transform colliders, int x, int yBottom)
    {
        decoracionO.SetTile(new Vector3Int(x, yBottom + 3, 0), TP(706));
        decoracionO.SetTile(new Vector3Int(x + 1, yBottom + 3, 0), TP(707));
        decoracionO.SetTile(new Vector3Int(x + 2, yBottom + 3, 0), TP(708));

        decoracionO.SetTile(new Vector3Int(x, yBottom + 2, 0), TP(743));
        decoracionO.SetTile(new Vector3Int(x + 1, yBottom + 2, 0), TP(744));
        decoracionO.SetTile(new Vector3Int(x + 2, yBottom + 2, 0), TP(745));

        decoracionO.SetTile(new Vector3Int(x, yBottom + 1, 0), TP(780));
        decoracionO.SetTile(new Vector3Int(x + 1, yBottom + 1, 0), TP(744));
        decoracionO.SetTile(new Vector3Int(x + 2, yBottom + 1, 0), TP(782));

        decoracionO.SetTile(new Vector3Int(x, yBottom, 0), TP(817));
        decoracionO.SetTile(new Vector3Int(x + 1, yBottom, 0), TP(818));
        decoracionO.SetTile(new Vector3Int(x + 2, yBottom, 0), TP(819));

        AddGridBoxCollider($"Col_CasetaLadrillo_{x}_{yBottom}", colliders, x, x + 2, yBottom, yBottom + 3);
    }

    private static void StampStreetLamp1x3(Tilemap decoracionO, int x, int yBase)
    {
        decoracionO.SetTile(new Vector3Int(x, yBase + 2, 0), TP(537));
        decoracionO.SetTile(new Vector3Int(x, yBase + 1, 0), TP(574));
        decoracionO.SetTile(new Vector3Int(x, yBase, 0), TP(611));
    }

    private static void StampElectricPole1x3(Tilemap decoracionO, int x, int yBase)
    {
        decoracionO.SetTile(new Vector3Int(x, yBase + 2, 0), TP(534));
        decoracionO.SetTile(new Vector3Int(x, yBase + 1, 0), TP(571));
        decoracionO.SetTile(new Vector3Int(x, yBase, 0), TP(608));
    }

    // =========================================================================================
    // COMPAÑERO INVESTIGADOR PRIVADO (CON SPRITE GENÉRICO + DETECTIVESTATS) Y DIÁLOGO NOIR
    // =========================================================================================

    public static CompanionFollowAI CreateInvestigatorCompanion(Transform parent)
    {
        GameObject npcGO = new GameObject("Investigador_Privado");
        if (parent != null) npcGO.transform.SetParent(parent, false);
        npcGO.transform.position = new Vector3(-4.2f, -0.4f, 0f);
        npcGO.transform.localScale = Vector3.one;

        Rigidbody2D rb = npcGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D col = npcGO.AddComponent<BoxCollider2D>();
        col.isTrigger = false;
        col.offset = new Vector2(0f, 0.55f);
        col.size = new Vector2(0.55f, 1.10f);

        GameObject visGO = new GameObject("Visual");
        visGO.transform.SetParent(npcGO.transform, false);
        visGO.transform.localPosition = Vector3.zero;
        visGO.transform.localScale = Vector3.one;

        SpriteRenderer sr = visGO.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;

        CompanionFollowAI companion = npcGO.AddComponent<CompanionFollowAI>();
        companion.companionName = "INVESTIGADOR VARGAS";
        companion.spriteRenderer = sr;
        companion.followDistance = 1.45f;

        DetectiveStats detStats = npcGO.AddComponent<DetectiveStats>();
        detStats.companionName = "INVESTIGADOR VARGAS";
        detStats.hasDetectiveInParty = true;
        companion.detectiveStats = detStats;

        EnsureGenericInvestigatorAppearance(companion);

        GameObject promptGO = CreateWorldPromptLabel("PromptUI_E", npcGO.transform, new Vector3(0f, 2.02f, 0f), "");
        companion.promptUI = promptGO;
        companion.promptText = promptGO.GetComponent<TextMeshPro>();

        return companion;
    }

    public static void EnsureGenericInvestigatorAppearance(CompanionFollowAI companion)
    {
        if (companion == null) return;

        if (companion.spriteRenderer == null)
        {
            companion.spriteRenderer = companion.GetComponentInChildren<SpriteRenderer>();
        }

        Sprite detectiveCustomSprite = LoadProjectSprite("Assets/Sprites/Detective/detective_stand_down.png");
        Sprite detectiveSideSprite = LoadProjectSprite("Assets/Sprites/Detective/detective_stand_side.png");
        Sprite genericIdle1 = (detectiveCustomSprite != null) ? detectiveCustomSprite : LoadProjectSprite("Assets/Sprites/Player - Cardo/Idle/1.png");
        Sprite genericFallback = LoadProjectSprite("Assets/Sprites/PlaceholderSquare.png") ?? GetWhiteSquareSprite();
        Sprite baseGeneric = (genericIdle1 != null) ? genericIdle1 : genericFallback;
        Sprite sideIdle = (detectiveSideSprite != null) ? detectiveSideSprite : baseGeneric;

        Sprite[] genericFrames;
        if (detectiveCustomSprite != null)
        {
            Sprite w1 = LoadProjectSprite("Assets/Sprites/Detective/detective_walk_1.png");
            Sprite w2 = LoadProjectSprite("Assets/Sprites/Detective/detective_walk_2.png");
            Sprite w3 = LoadProjectSprite("Assets/Sprites/Detective/detective_walk_3.png");
            Sprite w4 = LoadProjectSprite("Assets/Sprites/Detective/detective_walk_4.png");

            if (w1 != null && w2 != null && w3 != null && w4 != null)
            {
                genericFrames = new Sprite[] { w1, w2, w3, w4 };
            }
            else
            {
                genericFrames = new Sprite[] { sideIdle };
            }
        }
        else
        {
            genericFrames = new Sprite[8];
            for (int i = 0; i < 8; i++)
            {
                Sprite f = LoadProjectSprite($"Assets/Sprites/Player - Cardo/Idle/{i + 1}.png");
                genericFrames[i] = (f != null) ? f : baseGeneric;
            }
        }

        companion.transform.localScale = Vector3.one;
        if (companion.spriteRenderer != null)
        {
            companion.spriteRenderer.sprite = companion.isFollowingPlayer ? sideIdle : baseGeneric;
            companion.spriteRenderer.color = Color.white;
            companion.spriteRenderer.flipX = false;
            companion.spriteRenderer.sortingOrder = 5;

            if (detectiveCustomSprite != null)
            {
                companion.spriteRenderer.transform.localScale = Vector3.one;
                companion.spriteRenderer.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            }
            else if (genericIdle1 == null)
            {
                companion.spriteRenderer.transform.localScale = new Vector3(0.75f, 1.45f, 1f);
                companion.spriteRenderer.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            }
            else
            {
                companion.spriteRenderer.transform.localScale = new Vector3(1.05f, 1.05f, 1f);
                companion.spriteRenderer.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            }
        }

        companion.idleDownSprite = baseGeneric;
        companion.idleUpSprite = sideIdle;
        companion.idleSideSprite = sideIdle;
        companion.walkDownFrames = genericFrames;
        companion.walkUpFrames = genericFrames;
        companion.walkSideFrames = genericFrames;

        DetectiveStats stats = companion.GetComponent<DetectiveStats>();
        if (stats == null)
        {
            stats = companion.gameObject.AddComponent<DetectiveStats>();
        }
        Sprite detPortraitSprite = LoadProjectSprite("Assets/Portraits/Detective.png");
        stats.combatPortraitSprite = (detPortraitSprite != null) ? detPortraitSprite : baseGeneric;
        stats.combatIdleSprite = baseGeneric;
        companion.detectiveStats = stats;
    }

    public static void UpgradeDialoguePanelToNoirStyle(ExplorationDialogueManager expManager)
    {
        if (expManager == null || expManager.explorationDialoguePanel == null) return;

        GameObject panel = expManager.explorationDialoguePanel;

        RectTransform panelRT = panel.GetComponent<RectTransform>();
        if (panelRT != null)
        {
            panelRT.anchorMin = new Vector2(0.03f, 0.03f);
            panelRT.anchorMax = new Vector2(0.97f, 0.34f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
        }
        Image panelBorderImg = panel.GetComponent<Image>();
        if (panelBorderImg != null)
        {
            panelBorderImg.color = new Color(0.26f, 0.26f, 0.30f, 1f);
        }

        Transform innerBox = panel.transform.Find("ExplorationDialogueBox");
        if (innerBox == null)
        {
            GameObject boxGO = new GameObject("ExplorationDialogueBox", typeof(RectTransform), typeof(Image));
            boxGO.transform.SetParent(panel.transform, false);
            boxGO.transform.SetAsFirstSibling();
            RectTransform boxRT = boxGO.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.003f, 0.015f);
            boxRT.anchorMax = new Vector2(0.997f, 0.985f);
            boxRT.offsetMin = Vector2.zero;
            boxRT.offsetMax = Vector2.zero;
            boxGO.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.06f, 1f);
        }

        if (expManager.enriquePortrait == null)
            expManager.enriquePortrait = LoadProjectSprite("Assets/Portraits/Enrique.png");
        if (expManager.investigadorPortrait == null || expManager.investigadorPortrait.name.Contains("Cardo") || expManager.investigadorPortrait.name == "1")
            expManager.investigadorPortrait = LoadProjectSprite("Assets/Portraits/Detective.png") ?? LoadProjectSprite("Assets/Sprites/Detective/detective_stand_down.png");
        if (expManager.corporeoPortrait == null)
            expManager.corporeoPortrait = LoadProjectSprite("Assets/Portraits/Musculatura.png");
        if (expManager.intelectoPortrait == null)
            expManager.intelectoPortrait = LoadProjectSprite("Assets/Portraits/Intelecto.png");
        if (expManager.motricidadPortrait == null)
            expManager.motricidadPortrait = LoadProjectSprite("Assets/Portraits/Motricidad.png");
        if (expManager.mundoInternoPortrait == null)
            expManager.mundoInternoPortrait = LoadProjectSprite("Assets/Portraits/MundoInterno.png");

        if (expManager.portraitContainer == null)
        {
            Transform existingPortrait = panel.transform.Find("ExplorationPortraitContainer") ?? panel.transform.Find("PortraitContainer");
            if (existingPortrait != null)
            {
                expManager.portraitContainer = existingPortrait.gameObject;
                Transform imgTr = existingPortrait.Find("ExplorationPortraitImage") ?? existingPortrait.Find("PortraitImage");
                if (imgTr != null) expManager.portraitImage = imgTr.GetComponent<Image>();
                expManager.portraitLabelText = existingPortrait.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            else
            {
                GameObject pCont = new GameObject("PortraitContainer", typeof(RectTransform), typeof(Image));
                pCont.transform.SetParent(panel.transform, false);
                pCont.GetComponent<Image>().color = new Color(0.26f, 0.26f, 0.30f, 1f);

                GameObject pImgGO = new GameObject("PortraitImage", typeof(RectTransform), typeof(Image));
                pImgGO.transform.SetParent(pCont.transform, false);
                RectTransform piRT = pImgGO.GetComponent<RectTransform>();
                piRT.anchorMin = new Vector2(0.025f, 0.015f);
                piRT.anchorMax = new Vector2(0.975f, 0.985f);
                piRT.offsetMin = Vector2.zero;
                piRT.offsetMax = Vector2.zero;
                Image pImg = pImgGO.GetComponent<Image>();
                pImg.color = new Color(0.38f, 0.39f, 0.42f, 1f);

                GameObject pLblGO = new GameObject("PortraitLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                pLblGO.transform.SetParent(pImgGO.transform, false);
                RectTransform plRT = pLblGO.GetComponent<RectTransform>();
                plRT.anchorMin = new Vector2(0.08f, 0.08f);
                plRT.anchorMax = new Vector2(0.92f, 0.92f);
                plRT.offsetMin = Vector2.zero;
                plRT.offsetMax = Vector2.zero;
                TextMeshProUGUI pLbl = pLblGO.GetComponent<TextMeshProUGUI>();
                pLbl.fontSize = 35;
                pLbl.alignment = TextAlignmentOptions.Center;
                pLbl.color = new Color(0.90f, 0.88f, 0.84f, 1f);
                PlayerHealthBarUI.EnsureCleanPixelFont(pLbl);

                expManager.portraitContainer = pCont;
                expManager.portraitImage = pImg;
                expManager.portraitLabelText = pLbl;
            }
        }

        if (expManager.portraitContainer != null)
        {
            RectTransform pRT = expManager.portraitContainer.GetComponent<RectTransform>();
            if (pRT != null)
            {
                pRT.anchorMin = new Vector2(0.01f, 0.04f);
                pRT.anchorMax = new Vector2(0.195f, 1.55f);
                pRT.offsetMin = Vector2.zero;
                pRT.offsetMax = Vector2.zero;
            }
        }

        if (expManager.speakerNameText == null)
        {
            Transform existingSpeaker = panel.transform.Find("ExplorationSpeakerNameText") ?? panel.transform.Find("SpeakerNameText");
            if (existingSpeaker != null)
            {
                expManager.speakerNameText = existingSpeaker.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                GameObject spkGO = new GameObject("SpeakerNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
                spkGO.transform.SetParent(panel.transform, false);
                expManager.speakerNameText = spkGO.GetComponent<TextMeshProUGUI>();
            }
        }

        if (expManager.speakerNameText != null)
        {
            RectTransform sRT = expManager.speakerNameText.rectTransform;
            sRT.anchorMin = new Vector2(0.205f, 0.72f);
            sRT.anchorMax = new Vector2(0.81f, 0.92f);
            sRT.offsetMin = Vector2.zero;
            sRT.offsetMax = Vector2.zero;
            expManager.speakerNameText.fontSize = 40;
            expManager.speakerNameText.alignment = TextAlignmentOptions.BottomLeft;
            expManager.speakerNameText.color = new Color(0.92f, 0.65f, 0.15f, 1f);
            PlayerHealthBarUI.EnsureCleanPixelFont(expManager.speakerNameText);
        }

        if (expManager.speakerAndText != null)
        {
            RectTransform bodyRT = expManager.speakerAndText.rectTransform;
            bodyRT.anchorMin = new Vector2(0.205f, 0.22f);
            bodyRT.anchorMax = new Vector2(0.81f, 0.70f);
            bodyRT.offsetMin = Vector2.zero;
            bodyRT.offsetMax = Vector2.zero;
            expManager.speakerAndText.fontSize = 40;
            expManager.speakerAndText.alignment = TextAlignmentOptions.TopLeft;
            expManager.speakerAndText.color = new Color(0.90f, 0.88f, 0.84f, 1f);
            PlayerHealthBarUI.EnsureCleanPixelFont(expManager.speakerAndText);
        }

        if (expManager.continueIndicatorText != null)
        {
            RectTransform contRT = expManager.continueIndicatorText.rectTransform;
            contRT.anchorMin = new Vector2(0.205f, 0.05f);
            contRT.anchorMax = new Vector2(0.81f, 0.20f);
            contRT.offsetMin = Vector2.zero;
            contRT.offsetMax = Vector2.zero;
            expManager.continueIndicatorText.fontSize = 30;
            expManager.continueIndicatorText.alignment = TextAlignmentOptions.BottomLeft;
            expManager.continueIndicatorText.color = new Color(0.50f, 0.50f, 0.54f, 1f);
            PlayerHealthBarUI.EnsureCleanPixelFont(expManager.continueIndicatorText);
        }

        if (expManager.nextLineButton == null)
        {
            Transform existingBtn = panel.transform.Find("ExplorationNextButton");
            if (existingBtn != null)
            {
                expManager.nextLineButton = existingBtn.GetComponent<Button>();
            }
            else
            {
                GameObject btnBorder = new GameObject("ExplorationNextButton", typeof(RectTransform), typeof(Image));
                btnBorder.transform.SetParent(panel.transform, false);
                RectTransform bRT = btnBorder.GetComponent<RectTransform>();
                bRT.anchorMin = new Vector2(0.83f, 0.18f);
                bRT.anchorMax = new Vector2(0.985f, 0.82f);
                bRT.offsetMin = Vector2.zero;
                bRT.offsetMax = Vector2.zero;
                btnBorder.GetComponent<Image>().color = new Color(0.92f, 0.65f, 0.15f, 1f);

                GameObject btnFill = new GameObject("ButtonFill", typeof(RectTransform), typeof(Image));
                btnFill.transform.SetParent(btnBorder.transform, false);
                RectTransform bfRT = btnFill.GetComponent<RectTransform>();
                bfRT.anchorMin = new Vector2(0.02f, 0.06f);
                bfRT.anchorMax = new Vector2(0.98f, 0.94f);
                bfRT.offsetMin = Vector2.zero;
                bfRT.offsetMax = Vector2.zero;
                Image fillImg = btnFill.GetComponent<Image>();
                fillImg.color = new Color(0.09f, 0.09f, 0.12f, 1f);

                Button btn = btnBorder.AddComponent<Button>();
                btn.targetGraphic = fillImg;
                btn.onClick.AddListener(expManager.AdvanceDialogue);

                GameObject lblGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblGO.transform.SetParent(btnFill.transform, false);
                RectTransform lRT = lblGO.GetComponent<RectTransform>();
                lRT.anchorMin = new Vector2(0.06f, 0.08f);
                lRT.anchorMax = new Vector2(0.94f, 0.92f);
                lRT.offsetMin = Vector2.zero;
                lRT.offsetMax = Vector2.zero;
                TextMeshProUGUI lblTMP = lblGO.GetComponent<TextMeshProUGUI>();
                lblTMP.text = "CONTINUAR\n[ESPACIO / E]";
                lblTMP.fontSize = 35;
                lblTMP.alignment = TextAlignmentOptions.Center;
                lblTMP.color = new Color(0.92f, 0.65f, 0.15f, 1f);
                PlayerHealthBarUI.EnsureCleanPixelFont(lblTMP);

                expManager.nextLineButton = btn;
            }
        }
    }

    private static GameObject CreateWorldPromptLabel(string name, Transform parent, Vector3 localPos, string text)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.SetActive(false);
        return go;
    }

    private static void CreateMarker(string name, Transform parent, Vector3 pos)
    {
        GameObject m = new GameObject(name);
        m.transform.SetParent(parent, false);
        m.transform.localPosition = pos;
    }
}
