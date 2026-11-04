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
///   que `01_Nivel1_SantaFelipina.unity` (fachadas completas con bordes/cornisas, azoteas cerradas en 9-slice,
///   adoquín limpio BY_23, cajas apiladas 2x2, contenedores 2x2, faroles 1x3, bancas 2x1 y botes/bolsas de basura en Tilemap).
/// - Compañero Investigador Privado (CompanionFollowAI) con sprite genérico que se une a Enrique y lo sigue por el callejón.
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
        "VOZ [INTELECTO]: \"Un investigador privado... Sus expedientes y su conocimiento del terreno nos ahorran caminar a ciegas por estos callejones. Escucha lo que sabe.\"",
        "INVESTIGADOR PRIVADO: \"Este pasaje entre las azoteas y los edificios es el único acceso hacia su guarida. Cerraron el paso vehicular para que nadie entre en auto; aquí solo se pasa a pie.\"",
        "VOZ [CORPOREO]: \"Perfecto. En un callejón estrecho no podrán rodearnos; tendrán que dar la cara de frente.\"",
        "VOZ [MOTRICIDAD]: \"Avanza hacia el sur por el pasaje estrecho y luego dobla al este junto a los edificios altos.\"",
        "INVESTIGADOR PRIVADO: \"Adelante hay varios de sus sicarios vigilando el callejón y otro jefe custodiando la entrada de su complejo. Me uno a ti, Enrique: yo te sigo, vamos a encontrar a El Calacas.\"",
        "NARRADOR: \"[ EL INVESTIGADOR PRIVADO SE HA UNIDO A TI Y TE SEGUIRÁ POR EL CALLEJÓN ]\""
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
    /// Garantiza que el mapa armónico de callejones (construido 100% con los bloques de Tilemap de la Escena 1),
    /// el Investigador Privado con sprite genérico, el InventoryManager y los cuadros de texto noir estén listos.
    /// </summary>
    public void EnsureAlleyMapAndNoirDialogueReady()
    {
        // 1. Reemplazar versiones anteriores si existen
        GameObject oldWarehouse = GameObject.Find("Almacen_Clandestino_Root");
        if (oldWarehouse != null)
        {
            Destroy(oldWarehouse);
        }

        GameObject alleyRoot = GameObject.Find("Callejon_Ciudad_Root");
        if (alleyRoot != null && alleyRoot.transform.Find("Callejon_Armonico_V5") == null)
        {
            Destroy(alleyRoot);
            alleyRoot = null;
        }

        if (alleyRoot == null)
        {
            alleyRoot = BuildUrbanAlleyEnvironment();
        }

        // 2. Posicionar a Enrique en la entrada peatonal oeste (sobre la calle que desemboca en la plazuela del callejón)
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

        // 3. Asegurar que el Investigador Privado exista con su sprite genérico y CompanionFollowAI
        if (investigatorCompanion == null)
        {
            investigatorCompanion = FindAnyObjectByType<CompanionFollowAI>();
        }
        if (investigatorCompanion == null)
        {
            investigatorCompanion = CreateInvestigatorCompanion(alleyRoot != null ? alleyRoot.transform : null);
        }
        else
        {
            EnsureGenericInvestigatorAppearance(investigatorCompanion);
        }

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

        // 5. Asegurar InventoryManager para conservar los objetos, recetas y armas equipadas de la Escena 1
        EnsureInventoryManagerContinuity();
    }

    private static void EnsureInventoryManagerContinuity()
    {
        InventoryManager inv = FindAnyObjectByType<InventoryManager>();
        if (inv == null)
        {
            GameObject invGO = new GameObject("InventoryManager");
            inv = invGO.AddComponent<InventoryManager>();
        }

#if UNITY_EDITOR
        if (inv.knownRecipes == null) inv.knownRecipes = new List<CraftingRecipeData>();
        if (inv.knownRecipes.Count == 0)
        {
            CraftingRecipeData r1 = AssetDatabase.LoadAssetAtPath<CraftingRecipeData>("Assets/Data/Items/Receta_BombaChile.asset");
            CraftingRecipeData r2 = AssetDatabase.LoadAssetAtPath<CraftingRecipeData>("Assets/Data/Items/Receta_VendajeRudo.asset");
            if (r1 != null) inv.knownRecipes.Add(r1);
            if (r2 != null) inv.knownRecipes.Add(r2);
        }

        if (inv.preparedWeaponCatalog == null) inv.preparedWeaponCatalog = new List<ItemData>();
        if (inv.preparedWeaponCatalog.Count == 0)
        {
            ItemData w1 = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_PunosCallejeros.asset");
            ItemData w2 = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_TuboPlomo.asset");
            ItemData w3 = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_BatCallejero.asset");
            if (w1 != null) inv.preparedWeaponCatalog.Add(w1);
            if (w2 != null) inv.preparedWeaponCatalog.Add(w2);
            if (w3 != null) inv.preparedWeaponCatalog.Add(w3);
        }
#endif
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
    // CONSTRUCCIÓN ARMÓNICA Y ORDENADA DEL CALLEJÓN (MISMA ARQUITECTURA DE TILEMAP QUE ESCENA 1)
    // =========================================================================================

    private const float CellWorldSize = 0.8f; // CellSize 0.16 * Escala 5 = 0.80 unidades por celda

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
#if UNITY_EDITOR
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sp != null) return sp;
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (all != null)
        {
            foreach (var o in all)
            {
                if (o is Sprite s) return s;
            }
        }
#endif
        return null;
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

        // Etiqueta de versión armónica V5
        GameObject versionTag = new GameObject("Callejon_Armonico_V5");
        versionTag.transform.SetParent(root.transform, false);

        // 1. ESTRUCTURA DE GRIDS Y TILEMAPS IDÉNTICA A 01_Nivel1_SantaFelipina.unity
        // En Escena 1: CellSize = (0.16, 0.16, 0), LocalScale = (5, 5, 1) -> 1 celda = 0.80m
        // - suelo:       localPos (-0.015, -0.04, 0),  sortingOrder = 1
        // - edificios:   localPos (-0.015, -0.044, 0), sortingOrder = 2
        // - decoracionO: localPos (0, 0, 0),           sortingOrder = 3
        // - decoracionN: localPos (0, -0.04, 0),       sortingOrder = 3
        Tilemap suelo = CreateGridWithTilemap("Suelo", "suelo", root.transform, new Vector3(-0.015f, -0.04f, 0f), 1);
        Tilemap edificios = CreateGridWithTilemap("Edificios", "edificios", root.transform, new Vector3(-0.015f, -0.044f, 0f), 2);
        Tilemap decoracionO = CreateGridWithTilemap("DecoracionObjetos", "decoracionO", root.transform, Vector3.zero, 3);
        Tilemap decoracionN = CreateGridWithTilemap("DecoracionNormal", "decoracionN", root.transform, new Vector3(0f, -0.04f, 0f), 3);

        GameObject collidersGroup = new GameObject("Colisiones_Arquitectura");
        collidersGroup.transform.SetParent(root.transform, false);

        PopulateHarmoniousAlleyMap(suelo, edificios, decoracionO, decoracionN, collidersGroup.transform);

        // 2. INTERACTUABLE DE LA GUARIDA DE EL CALACAS (Integrado en la puerta del edificio industrial al final del callejón)
        // La puerta del almacén industrial (TP_757) está exactamente en la celda (x = 24, y = -19) -> mundo (19.6f, -14.8f)
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
            "INVESTIGADOR PRIVADO: \"Esta es la entrada trasera del complejo donde se esconde El Calacas, Enrique.\"",
            "VOZ [INTELECTO]: \"La cortina metálica y la puerta tienen marcas frescas. Aquí termina el callejón peatonal y comienza el territorio de su guardia personal.\"",
            "VOZ [CORPOREO]: \"En cuanto salgan sus matones y el jefe del callejón, les abriremos esa puerta a golpes.\""
        };

        GameObject gatePrompt = CreateWorldPromptLabel("PromptUI_E", bossGateGO.transform, new Vector3(0f, 1.35f, 0f), "[E] ENTRADA GUARIDA DE EL CALACAS");
        gateInteract.promptUI = gatePrompt;

        // 3. PUNTOS DE APARICIÓN ORDENADOS PARA FUTUROS ENEMIGOS Y JEFE
        GameObject spawnMarkers = new GameObject("Puntos_Enemigos_Callejon");
        spawnMarkers.transform.SetParent(root.transform, false);
        CreateMarker("Spawn_Enemigo_Callejon_Norte", spawnMarkers.transform, new Vector3(-0.8f, -4.5f, 0f));
        CreateMarker("Spawn_Enemigo_Pasaje_Vertical", spawnMarkers.transform, new Vector3(-1.2f, -11.5f, 0f));
        CreateMarker("Spawn_Enemigo_Patio_Sur", spawnMarkers.transform, new Vector3(7.5f, -17.8f, 0f));
        CreateMarker("Spawn_JefeFinal_Callejon", spawnMarkers.transform, new Vector3(17.5f, -17.2f, 0f));

        return root;
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

    /// <summary>
    /// Construye el mapa del Nivel 2 siguiendo exactamente el mismo orden, proporción y combinación de tiles de la Escena 1:
    /// - ZONA 1 (Plazuela de Entrada al Callejón, x = -16..3, y = -4..4):
    ///   Tramo final de la calle pavimentada que desemboca en una plazuela peatonal (BY_23) flanqueada al norte por 3 edificios completos
    ///   (Ladrillo Rojo, Departamentos de Concreto y Torre Gris), al sur por 2 azoteas cerradas y al este por una azotea con pilas de cajas 2x2.
    /// - ZONA 2 (Pasaje Estrecho Vertical hacia el Sur, x = -4..1, y = -19..-5):
    ///   Callejón angosto que baja encajonado entre azoteas completas (Techo de Teja a la izquierda y Azotea de Alquitrán con cajas apiladas a la derecha),
    ///   con el canal pavimentado bordeado (BY_30, BY_181, BY_205/226, BY_186, BY_32), faroles callejeros y el contenedor verde 2x2 (TP_845..883).
    /// - ZONA 3 (Callejón Profundo del Sur hacia la Guarida de El Calacas, x = -4..26, y = -25..-20):
    ///   Al llegar al fondo del pasaje vertical, el camino dobla 90° al este pasando al pie de 3 edificios altos en medio del mapa
    ///   (Edificio de Ladrillo con Letrero, Edificio de Concreto y el Almacén Industrial de El Calacas) y 4 azoteas cerradas al sur.
    /// </summary>
    private static void PopulateHarmoniousAlleyMap(Tilemap suelo, Tilemap edificios, Tilemap decoracionO, Tilemap decoracionN, Transform colliders)
    {
        // =========================================================================
        // 1. ZONA 1: ENTRADA DESDE LA CALLE Y PLAZUELA SUPERIOR (x = -16..3, y = -4..4)
        // =========================================================================

        // A. Tramo final de la calle asfaltada al Oeste (x = -16..-11, y = -4..4) con paso peatonal igual que Escena 1
        StampStreetEntranceWest(suelo, -16, -11);

        // B. Piso peatonal limpio y uniforme (BY_23) en toda la Plazuela Superior (x = -10..3, y = -4..4)
        FillRect(suelo, -10, 3, -4, 4, BY(23));

        // C. Rectángulo de desagüe/desgaste con marco 9-slice en el centro de la plazuela (x = -8..-3, y = -3..2) — idéntico a Escena 1 (x = 22..27, y = -7..2)
        StampFramedDrainagePatch(edificios, decoracionO, -8, -3, -3, 2);

        // D. Edificios del Norte de la Zona 1 (y = 5..12)
        // Edificio N1: Ladrillo Rojo con toldo y ventanas (x = -16..-8, y = 5..11) — réplica de Escena 1 (x = -11..-3, y = 5..11)
        StampBrickBuildingA(edificios, decoracionO, colliders, -16, -8, 5, true);
        // Edificio N2: Departamentos de Concreto con ventanal central 3x4 (x = -7..-1, y = 5..10) — réplica de Escena 1 (x = -2..4, y = 5..10)
        StampConcreteBuildingB(edificios, colliders, -7, -1, 5);
        // Edificio N3: Torre Gris Oscura con tuberías y ventilaciones (x = 0..7, y = 5..10) — réplica de Escena 1 (x = 5..12, y = 5..10)
        StampDarkTowerBuildingC(edificios, decoracionO, colliders, 0, 7, 5);

        // E. Azotea Este que cierra el paso recto y obliga a doblar al Sur (x = 4..10, y = -4..4) — réplica de Escena 1 (x = 44..50, y = -4..5)
        StampRooftopDarkTar(edificios, colliders, 4, 10, -4, 4, false);
        // Pilas de cajas 2x2 alineadas contra el borde izquierdo de la Azotea Este (x = 2..3, y = -2..3) — igual que en Escena 1 (x = 42..43, y = -2..4)
        StampCrateStack2x2(edificios, colliders, 2, 2, false);
        StampCrateStack2x2(edificios, colliders, 2, 0, false);
        StampCrateStack2x2(edificios, colliders, 2, -2, false);

        // F. Azoteas que encajonan el lado Sur-Oeste de la Zona 1 (x = -16..-5, y = -11..-5)
        StampRooftopDarkTar(edificios, colliders, -16, -11, -11, -5, true);
        StampRooftopLightConcrete(edificios, colliders, -10, -5, -11, -5);

        // G. Decoración ordenada de la Zona 1 (Botes, bolsas de basura, banca y farol alineados junto a las paredes)
        // Farol callejero en la esquina del edificio N1 (x = -8, y = 4..6)
        StampStreetLamp1x3(decoracionO, -8, 4);
        // Bolsas y bote de basura al pie del edificio de ladrillo (y = 4), igual que en Escena 1
        decoracionO.SetTile(new Vector3Int(-14, 4, 0), GD(0)); // Bote cerrado
        decoracionO.SetTile(new Vector3Int(-13, 4, 0), GD(3)); // Montón de bolsas
        decoracionO.SetTile(new Vector3Int(-10, 4, 0), GD(4)); // Bolsa gris
        // Banca de madera 2x1 frente al edificio de concreto (x = -5..-4, y = 4)
        StampBench2x1(decoracionO, colliders, -5, 4);
        // Botes y bolsas en la esquina antes de bajar al callejón (x = 0..1, y = 4)
        decoracionO.SetTile(new Vector3Int(0, 4, 0), GD(5));   // Bolsa negra atada
        decoracionO.SetTile(new Vector3Int(1, 4, 0), GD(14));  // Contenedor/bote

        // =========================================================================
        // 2. ZONA 2: PASAJE ESTRECHO VERTICAL HACIA EL SUR (x = -4..1, y = -19..-5)
        // =========================================================================

        // A. Piso peatonal del tramo medio vertical (x = -4..1, y = -12..-5)
        FillRect(edificios, -4, 1, -12, -5, BY(23));

        // B. Canal estrecho pavimentado con guarniciones laterales (x = -4..1, y = -19..-13) — réplica exacta de Escena 1 (x = 23..28, y = -26..-19)
        StampVerticalAlleyChannel(edificios, -4, -19, -13);

        // C. Azoteas y bloques que encajonan el Pasaje Vertical por el Oeste (x = -16..-5, y = -19..-12)
        StampRooftopBrownTile(edificios, colliders, -12, -5, -19, -12);
        StampRooftopBeige(edificios, colliders, -16, -13, -19, -12);

        // D. Azotea y pilas de cajas que encajonan el Pasaje Vertical por el Este (x = 4..10, y = -11..-5)
        StampRooftopDarkTar(edificios, colliders, 4, 10, -11, -5, false);
        // Pilas de cajas 2x2 alineadas contra la pared derecha del pasaje (x = 2..3, y = -11..-6) — réplica de Escena 1 (x = 28..29, y = -12..-6)
        StampCrateStack2x2(edificios, colliders, 2, -7, true);
        StampCrateStack2x2(edificios, colliders, 2, -9, true);
        StampCrateStack2x2(edificios, colliders, 2, -11, true);

        // E. Decoración ordenada del Pasaje Vertical (exactamente como el callejón vertical de Escena 1)
        // Bolsas de basura pegadas al muro izquierdo (x = -4, y = -8..-7)
        decoracionO.SetTile(new Vector3Int(-4, -7, 0), GD(3));
        decoracionO.SetTile(new Vector3Int(-4, -8, 0), GD(5));
        // Poste eléctrico a la derecha (x = 1, y = -12..-10)
        StampElectricPole1x3(decoracionO, 1, -12);
        // Faroles en la pared izquierda del canal estrecho (x = -4, y = -16..-14)
        StampStreetLamp1x3(decoracionO, -4, -16);
        // Contenedor de basura verde 2x2 (TP_845/846/882/883) pegado al muro derecho (x = 0..1, y = -17..-16) — idéntico a Escena 1 (x = 27..28, y = -23..-22)
        StampDumpster2x2(decoracionO, colliders, 0, -17);
        // Bolsas de basura pequeñas (TP_246) junto al paso (x = -3, y = -13 y x = 0, y = -13)
        if (TP(246) != null)
        {
            decoracionO.SetTile(new Vector3Int(-3, -13, 0), TP(246));
            decoracionO.SetTile(new Vector3Int(0, -13, 0), TP(246));
        }

        // =========================================================================
        // 3. ZONA 3: CALLEJÓN PROFUNDO DEL SUR Y GUARIDA DE EL CALACAS (x = -4..27, y = -25..-20)
        // =========================================================================

        // A. Piso peatonal limpio y uniforme (BY_23) a lo largo de todo el corredor inferior (x = -4..27, y = -25..-20)
        FillRect(edificios, -4, 27, -25, -20, BY(23));

        // B. Rectángulo de desagüe/humedad con marco 9-slice en medio del corredor sur (x = 6..11, y = -24..-21)
        StampFramedDrainagePatch(edificios, decoracionO, 6, 11, -24, -21);

        // C. GRANDES EDIFICIOS EN EL CENTRO DEL MAPA (Se alzan en y = -19..-12 justo al norte del corredor sur, dando profundidad real)
        // Edificio C1: Edificio alto de ladrillo con puerta y letrero (x = 2..12, y = -19..-12) — réplica exacta de Escena 1 (x = 13..23, y = 5..12)
        StampShopBrickBuildingD(edificios, decoracionO, decoracionN, colliders, 2, 12, -19);
        // Edificio C2: Departamentos de concreto con ventanal 3x4 (x = 13..19, y = -19..-14) — réplica exacta de Escena 1 (x = -2..4, y = 5..10)
        StampConcreteBuildingB(edificios, colliders, 13, 19, -19);
        // Edificio C3: Complejo Industrial de El Calacas (x = 20..26, y = -19..-13) — réplica exacta de Escena 1 (x = 37..43, y = 6..12)
        StampIndustrialWarehouseE(edificios, decoracionO, colliders, 20, 26, -19);

        // Azoteas superiores detrás de los Edificios C2 y C3 (x = 11..26, y = -13..-4) para que no quede hueco negro entre las zonas
        StampRooftopLightConcrete(edificios, colliders, 11, 18, -11, -4);
        StampRooftopDarkTar(edificios, colliders, 19, 26, -12, -4, true);

        // D. Azotea Este que cierra el fondo del corredor sur (x = 28..33, y = -25..-13) con pilas de cajas 2x2 en x = 26..27
        StampRooftopDarkTar(edificios, colliders, 28, 33, -25, -13, false);
        StampCrateStack2x2(edificios, colliders, 26, -21, false);
        StampCrateStack2x2(edificios, colliders, 26, -23, false);
        StampCrateStack2x2(edificios, colliders, 26, -25, false);

        // E. Fila continua de Azoteas cerradas en 9-slice al Sur del corredor (x = -12..27, y = -32..-26) — igual que el borde sur de Escena 1
        StampRooftopDarkTar(edificios, colliders, -12, -3, -32, -26, true);
        StampRooftopLightConcrete(edificios, colliders, -2, 8, -32, -26);
        StampRooftopBeige(edificios, colliders, 9, 17, -32, -26);
        StampRooftopBrownTile(edificios, colliders, 18, 27, -32, -26);

        // F. Decoración ordenada del Corredor Sur (Carpas, bancas, botes y bolsas alineados como en Escena 1)
        // Al pie de los edificios del norte del corredor (y = -20):
        StampStreetLamp1x3(decoracionO, 2, -20);
        decoracionO.SetTile(new Vector3Int(4, -20, 0), GD(4));
        decoracionO.SetTile(new Vector3Int(5, -20, 0), GD(5));
        decoracionO.SetTile(new Vector3Int(6, -20, 0), GD(14));
        decoracionO.SetTile(new Vector3Int(14, -20, 0), TX(8)); // Caja de madera TX Props
        decoracionO.SetTile(new Vector3Int(15, -20, 0), TX(9)); // Caja de madera TX Props
        if (TP(676) != null)
        {
            decoracionO.SetTile(new Vector3Int(18, -20, 0), TP(676));
            decoracionO.SetTile(new Vector3Int(19, -20, 0), TP(676));
        }

        // Junto al borde sur del corredor (y = -25..-24), replicando la acera sur de Escena 1 (y = -7..-6):
        decoracionO.SetTile(new Vector3Int(-1, -25, 0), GD(0)); // Bote de basura
        decoracionO.SetTile(new Vector3Int(0, -25, 0), GD(3));  // Bolsas de basura
        decoracionO.SetTile(new Vector3Int(1, -25, 0), GD(5));  // Bolsa negra
        StampBench2x1(decoracionO, colliders, 3, -25);          // Banca 2x1
        StampCanopy3x2(decoracionO, colliders, 12, -25, false); // Carpa verde 3x2 (TP_623..625 / TP_660..662)
        StampCanopy3x2(decoracionO, colliders, 15, -25, true);  // Carpa azul 3x2 (TP_771..773 / TP_808..810)

        // Pequeña estructura de ladrillo 3x4 en la antesala de El Calacas (x = 20..22, y = -24..-21) — réplica de Escena 1 (x = 34..36, y = -2..1)
        StampSmallBrickOutbuilding3x4(decoracionO, colliders, 20, -24);

        // Colisionadores de los límites exteriores (Oeste de la calle de entrada)
        AddGridBoxCollider("Limite_Oeste_Entrada", colliders, -17, -17, -5, 5);
    }

    // =========================================================================================
    // ESTAMPAS ARQUITECTÓNICAS EXACTAS DE LA ESCENA 1 (SIN ELEMENTOS ALEATORIOS)
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

    /// <summary>
    /// Estampa el tramo final de la calle asfaltada con banquetas norte/sur y paso de cebra peatonal,
    /// idéntico al extremo derecho de la avenida en 01_Nivel1_SantaFelipina.unity (x = 12..18, y = -4..4).
    /// </summary>
    private static void StampStreetEntranceWest(Tilemap suelo, int xMin, int xMax)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isFirst = (x == xMin);
            bool isCrosswalk = (x >= xMax - 3 && x <= xMax - 1);
            bool isRightEdge = (x == xMax);

            // Banqueta Norte (y = 4 borde sup, y = 3 loseta, y = 2 guarnición)
            suelo.SetTile(new Vector3Int(x, 4, 0), isFirst ? BY(6) : BY(48));
            suelo.SetTile(new Vector3Int(x, 3, 0), isFirst ? BY(28) : (isRightEdge ? BY(190) : BY(188)));
            suelo.SetTile(new Vector3Int(x, 2, 0), isFirst ? BY(157) : (isRightEdge ? BY(162) : BY(161)));

            // Arroyo vehicular (y = -1..1)
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

            // Banqueta Sur (y = -2 guarnición, y = -3 loseta, y = -4 borde inf)
            suelo.SetTile(new Vector3Int(x, -2, 0), isRightEdge ? BY(231) : BY(227));
            suelo.SetTile(new Vector3Int(x, -3, 0), isFirst ? BY(6) : (isRightEdge ? BY(190) : BY(188)));
            suelo.SetTile(new Vector3Int(x, -4, 0), isFirst ? BY(28) : (isRightEdge ? BY(10) : BY(9)));
        }
    }

    /// <summary>
    /// Estampa el rectángulo de desagüe/desgaste con marco 9-slice completo idéntico a Escena 1 (x = 22..27, y = -7..2).
    /// </summary>
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

    /// <summary>
    /// Estampa el canal vertical estrecho de 6 celdas de ancho idéntico al callejón sur de Escena 1 (x = 23..28, y = -26..-19).
    /// </summary>
    private static void StampVerticalAlleyChannel(Tilemap edificios, int xLeft, int yBottom, int yTop)
    {
        // Fila superior de entrada al canal (y = yTop): BY_23, BY_9 x4, BY_23
        edificios.SetTile(new Vector3Int(xLeft, yTop, 0), BY(23));
        edificios.SetTile(new Vector3Int(xLeft + 1, yTop, 0), BY(9));
        edificios.SetTile(new Vector3Int(xLeft + 2, yTop, 0), BY(9));
        edificios.SetTile(new Vector3Int(xLeft + 3, yTop, 0), BY(9));
        edificios.SetTile(new Vector3Int(xLeft + 4, yTop, 0), BY(9));
        edificios.SetTile(new Vector3Int(xLeft + 5, yTop, 0), BY(23));

        // Segunda fila (escalón de guarnición, y = yTop - 1): BY_30, BY_157, BY_161, BY_161, BY_162, BY_32
        int yStep = yTop - 1;
        edificios.SetTile(new Vector3Int(xLeft, yStep, 0), BY(30));
        edificios.SetTile(new Vector3Int(xLeft + 1, yStep, 0), BY(157));
        edificios.SetTile(new Vector3Int(xLeft + 2, yStep, 0), BY(161));
        edificios.SetTile(new Vector3Int(xLeft + 3, yStep, 0), BY(161));
        edificios.SetTile(new Vector3Int(xLeft + 4, yStep, 0), BY(162));
        edificios.SetTile(new Vector3Int(xLeft + 5, yStep, 0), BY(32));

        // Cuerpo del canal (y = yBottom .. yTop - 2)
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

    /// <summary>
    /// Edificio A: Ladrillo rojo de 7 pisos de alto con cornisas, ventanas dobles y toldo opcional (réplica de Escena 1, x = -11..-3, y = 5..11).
    /// </summary>
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

        // Ventanas superiores alineadas (TP_694 / TP_731) y rejilla (TP_548)
        int[] winCols = new int[] { xMin + 2, xMax - 2 };
        foreach (int wx in winCols)
        {
            decoracionO.SetTile(new Vector3Int(wx, yBase + 5, 0), TP(694));
            decoracionO.SetTile(new Vector3Int(wx, yBase + 4, 0), TP(731));
        }
        decoracionO.SetTile(new Vector3Int(xMax - 2, yBase + 3, 0), TP(548));

        // Toldo comercial 2x2 (TP_911, 912 / TP_948, 949)
        if (withAwning)
        {
            decoracionO.SetTile(new Vector3Int(xMin + 3, yBase + 1, 0), TP(911));
            decoracionO.SetTile(new Vector3Int(xMin + 4, yBase + 1, 0), TP(912));
            decoracionO.SetTile(new Vector3Int(xMin + 3, yBase, 0), TP(948));
            decoracionO.SetTile(new Vector3Int(xMin + 4, yBase, 0), TP(949));
        }

        AddGridBoxCollider($"Col_EdificioLadrillo_{xMin}_{yBase}", colliders, xMin, xMax, yBase, yBase + 6);
    }

    /// <summary>
    /// Edificio B: Departamentos de concreto con ventanal central integrado 3x4 (réplica de Escena 1, x = -2..4, y = 5..10).
    /// </summary>
    private static void StampConcreteBuildingB(Tilemap edificios, Transform colliders, int xMin, int xMax, int yBase)
    {
        int center = (xMin + xMax) / 2;
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            int bayOffset = x - (center - 1); // 0, 1, 2 para las 3 columnas centrales

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

    /// <summary>
    /// Edificio C: Torre urbana gris oscura con ventanal superior 3x2, tuberías y ventilaciones (réplica de Escena 1, x = 5..12, y = 5..10).
    /// </summary>
    private static void StampDarkTowerBuildingC(Tilemap edificios, Tilemap decoracionO, Transform colliders, int xMin, int xMax, int yBase)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            int bayIdx = x - (xMin + 1); // 0, 1, 2 en las primeras 3 columnas internas

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

        // Tuberías y ventilaciones en la base (igual que Escena 1 en x = 5..12, y = 3..5)
        decoracionO.SetTile(new Vector3Int(xMin + 1, yBase, 0), TP(406));
        decoracionO.SetTile(new Vector3Int(xMin + 1, yBase - 1, 0), TP(480));
        decoracionO.SetTile(new Vector3Int(xMin + 4, yBase, 0), TP(982));
        decoracionO.SetTile(new Vector3Int(xMin + 4, yBase - 1, 0), TP(636));
        decoracionO.SetTile(new Vector3Int(xMin + 4, yBase - 2, 0), TP(673));
        decoracionO.SetTile(new Vector3Int(xMax - 1, yBase, 0), TP(406));
        decoracionO.SetTile(new Vector3Int(xMax - 1, yBase - 1, 0), TP(480));

        AddGridBoxCollider($"Col_TorreGris_{xMin}_{yBase}", colliders, xMin, xMax, yBase, yBase + 5);
    }

    /// <summary>
    /// Edificio D: Gran edificio de ladrillo de 8 pisos con puerta y marquesina de 3 filas (réplica de Escena 1, x = 13..23, y = 5..12).
    /// </summary>
    private static void StampShopBrickBuildingD(Tilemap edificios, Tilemap decoracionO, Tilemap decoracionN, Transform colliders, int xMin, int xMax, int yBase)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            int localX = x - xMin;

            // Fila base (yBase)
            if (isLeft) edificios.SetTile(new Vector3Int(x, yBase, 0), TP(444));
            else if (isRight) edificios.SetTile(new Vector3Int(x, yBase, 0), TP(445));
            else if (localX == 6) edificios.SetTile(new Vector3Int(x, yBase, 0), TP(955));
            else edificios.SetTile(new Vector3Int(x, yBase, 0), TP(298));

            // Fila 1 (yBase + 1)
            if (isLeft) edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(407));
            else if (isRight) edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(408));
            else if (localX == 5) edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(917));
            else if (localX == 6) edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(918));
            else edificios.SetTile(new Vector3Int(x, yBase + 1, 0), TP(187));

            // Fila 2 (yBase + 2)
            edificios.SetTile(new Vector3Int(x, yBase + 2, 0), isLeft ? TP(407) : (isRight ? TP(408) : TP(187)));

            // Fila 3 (yBase + 3: cornisa intermedia)
            edificios.SetTile(new Vector3Int(x, yBase + 3, 0), isLeft ? TP(334) : (isRight ? TP(336) : TP(335)));

            // Filas 4 a 7 (yBase + 4 .. yBase + 7)
            for (int dy = 4; dy <= 7; dy++)
            {
                edificios.SetTile(new Vector3Int(x, yBase + dy, 0), isLeft ? TP(407) : (isRight ? TP(408) : TP(187)));
            }

            // Marquesina / fachada decorativa en filas yBase+1..yBase+3
            TileBase tTop = isLeft ? TP(356) : ((localX == 1) ? TP(357) : (isRight ? TP(359) : TP(358)));
            TileBase tMid = isLeft ? TP(393) : ((localX == 1) ? TP(394) : (isRight ? TP(396) : TP(395)));
            TileBase tBot = isLeft ? TP(430) : ((localX == 1) ? TP(431) : (isRight ? TP(433) : TP(432)));
            decoracionO.SetTile(new Vector3Int(x, yBase + 3, 0), tTop);
            decoracionO.SetTile(new Vector3Int(x, yBase + 2, 0), tMid);
            decoracionO.SetTile(new Vector3Int(x, yBase + 1, 0), tBot);
        }

        // Ventanas del piso superior (TP_694 / TP_731)
        int[] winOffsets = new int[] { 2, 5, 8 };
        foreach (int wo in winOffsets)
        {
            decoracionO.SetTile(new Vector3Int(xMin + wo, yBase + 5, 0), TP(694));
            decoracionO.SetTile(new Vector3Int(xMin + wo, yBase + 4, 0), TP(731));
        }
        decoracionO.SetTile(new Vector3Int(xMin + 9, yBase + 5, 0), TP(548));

        AddGridBoxCollider($"Col_EdificioTienda_{xMin}_{yBase}", colliders, xMin, xMax, yBase, yBase + 7);
    }

    /// <summary>
    /// Edificio E: Almacén Industrial de El Calacas con cortina metálica, puerta y letrero en el techo
    /// (réplica exacta de Escena 1, x = 37..43, y = 6..12).
    /// </summary>
    private static void StampIndustrialWarehouseE(Tilemap edificios, Tilemap decoracionO, Transform colliders, int xMin, int xMax, int yBase)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool isLeft = (x == xMin);
            bool isRight = (x == xMax);
            int localX = x - xMin;

            // Pared industrial (edificios)
            edificios.SetTile(new Vector3Int(x, yBase, 0), isLeft ? TP(999) : (isRight ? TP(1001) : TP(1000)));
            for (int dy = 1; dy <= 5; dy++)
            {
                edificios.SetTile(new Vector3Int(x, yBase + dy, 0), isLeft ? TP(962) : (isRight ? TP(964) : TP(963)));
            }
            edificios.SetTile(new Vector3Int(x, yBase + 6, 0), isLeft ? TP(925) : (isRight ? TP(927) : TP(926)));

            // Fachada inferior con cortina y puerta (decoracionO, yBase y yBase+1)
            TileBase AwningTop = isLeft ? TP(644) : (isRight ? TP(648) : TP(645));
            TileBase AwningBot = isLeft ? TP(681) : (isRight ? TP(685) : ((localX == xMax - xMin - 1) ? TP(757) : TP(682)));
            decoracionO.SetTile(new Vector3Int(x, yBase + 1, 0), AwningTop);
            decoracionO.SetTile(new Vector3Int(x, yBase, 0), AwningBot);
        }

        // Letrero industrial en la parte alta (yBase + 5 y yBase + 6)
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

        // Farol a la izquierda, poste a la derecha y botes TP_680 sobre la marquesina (idéntico a Escena 1)
        StampStreetLamp1x3(decoracionO, xMin, yBase + 2);
        StampElectricPole1x3(decoracionO, xMax, yBase + 2);
        decoracionO.SetTile(new Vector3Int(xMin + 2, yBase + 2, 0), TP(680));
        decoracionO.SetTile(new Vector3Int(xMax - 1, yBase + 2, 0), TP(680));

        AddGridBoxCollider($"Col_AlmacenCalacas_{xMin}_{yBase}", colliders, xMin, xMax, yBase, yBase + 6);
    }

    // =========================================================================================
    // ESTAMPAS DE AZOTEAS EN 9-SLICE (RECTÁNGULOS COMPLETOS CON SUS 4 ESQUINAS Y BORDES)
    // =========================================================================================

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

    // =========================================================================================
    // ESTAMPAS DE OBJETOS MULTI-CELDA DE LA ESCENA 1 (CAJAS, CONTENEDORES, CARPAS, FAROLES)
    // =========================================================================================

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
    // COMPAÑERO INVESTIGADOR PRIVADO (CON SPRITE GENÉRICO) Y PANEL DE DIÁLOGO NOIR
    // =========================================================================================

    public static CompanionFollowAI CreateInvestigatorCompanion(Transform parent)
    {
        GameObject npcGO = new GameObject("Investigador_Privado");
        if (parent != null) npcGO.transform.SetParent(parent, false);
        // Ubicado en la plazuela superior justo en la entrada hacia el callejón vertical (x = -4.2, y = -0.4)
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

        EnsureGenericInvestigatorAppearance(companion);

        // Etiqueta flotante sobre el Investigador
        GameObject tagGO = new GameObject("Badge_Investigador");
        tagGO.transform.SetParent(npcGO.transform, false);
        tagGO.transform.localPosition = new Vector3(0f, 1.68f, 0f);
        TextMeshPro badgeTMP = tagGO.AddComponent<TextMeshPro>();
        badgeTMP.text = "INVESTIGADOR PRIVADO";
        badgeTMP.fontSize = 2.3f;
        badgeTMP.alignment = TextAlignmentOptions.Center;
        badgeTMP.color = new Color(0.95f, 0.78f, 0.36f, 0.95f);
        badgeTMP.sortingOrder = 6;
        PlayerHealthBarUI.EnsureCleanPixelFont(badgeTMP);

        // Indicador interactivo [E]
        GameObject promptGO = CreateWorldPromptLabel("PromptUI_E", npcGO.transform, new Vector3(0f, 2.02f, 0f), "[E] HABLAR CON INVESTIGADOR");
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

        Sprite genericIdle1 = LoadProjectSprite("Assets/Sprites/Player - Cardo/Idle/1.png");
        Sprite genericFallback = LoadProjectSprite("Assets/Sprites/PlaceholderSquare.png") ?? GetWhiteSquareSprite();
        Sprite baseGeneric = (genericIdle1 != null) ? genericIdle1 : genericFallback;

        Sprite[] genericFrames = new Sprite[8];
        for (int i = 0; i < 8; i++)
        {
            Sprite f = LoadProjectSprite($"Assets/Sprites/Player - Cardo/Idle/{i + 1}.png");
            genericFrames[i] = (f != null) ? f : baseGeneric;
        }

        companion.transform.localScale = Vector3.one;
        if (companion.spriteRenderer != null)
        {
            companion.spriteRenderer.sprite = baseGeneric;
            companion.spriteRenderer.color = new Color(0.78f, 0.86f, 0.96f, 1f);
            companion.spriteRenderer.flipX = true;
            companion.spriteRenderer.sortingOrder = 5;

            if (genericIdle1 == null)
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
        companion.idleUpSprite = baseGeneric;
        companion.idleSideSprite = baseGeneric;
        companion.walkDownFrames = genericFrames;
        companion.walkUpFrames = genericFrames;
        companion.walkSideFrames = genericFrames;
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
        if (expManager.investigadorPortrait == null)
            expManager.investigadorPortrait = LoadProjectSprite("Assets/Sprites/Player - Cardo/Idle/1.png");
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

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 2.5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.78f, 0.32f, 1f);
        tmp.sortingOrder = 7;
        PlayerHealthBarUI.EnsureCleanPixelFont(tmp);
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
