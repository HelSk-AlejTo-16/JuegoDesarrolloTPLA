using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Sistema de Loot en el Mapa para Contenedores de Basura (Botes de Basura y Bolsas de Basura) en el Nivel 2.
/// Al interactuar el jugador con ellos (colisión + tecla E):
/// 1. Verifica si el contenedor ya fue saqueado (isLooted == false).
/// 2. Selecciona un ítem de una tabla de probabilidad (LootTable: consumibles, materiales de creación para Enrique,
///    munición para el Detective o curación).
/// 3. Añade el ítem al inventario general (InventoryManager), muestra una notificación en la UI,
///    cambia el estado a isLooted = true y actualiza el sprite/estado visual del bote o bolsa para indicar que está vacío.
/// </summary>
public enum TrashContainerType
{
    BoteDeBasura = 0,
    BolsaDeBasura = 1
}

public enum TrashContainerKind
{
    BoteDeBasura = 0,
    BolsaDeBasura = 1
}

public enum LootRewardKind
{
    InventoryItem = 0,
    DetectiveAmmo = 1,
    DirectHeal = 2
}

[Serializable]
public class LootTableEntry
{
    [Tooltip("Nombre descriptivo del ítem en la tabla de probabilidad.")]
    public string entryDisplayName = "Botella Vacía";
    public string fallbackItemName { get => entryDisplayName; set => entryDisplayName = value; }

    [Tooltip("Tipo de recompensa (Objeto de inventario, Munición para el Detective o Curación).")]
    public LootRewardKind rewardKind = LootRewardKind.InventoryItem;

    [Tooltip("Referencia al ItemData (si no está asignado, se resuelve o crea dinámicamente por nombre/categoría).")]
    public ItemData itemAsset;
    public ItemData itemData { get => itemAsset; set => itemAsset = value; }

    [Tooltip("Categoría del objeto si se genera dinámicamente.")]
    public ItemCategory fallbackCategory = ItemCategory.Material;

    [Tooltip("Cantidad de unidades, balas o puntos de salud que otorga.")]
    [Min(1)]
    public int amount = 1;

    [Tooltip("Valor de curación o daño si se crea un ItemData consumible en runtime.")]
    public int effectValue = 25;

    [Tooltip("Peso relativo de probabilidad en la LootTable (mayor peso = más frecuente).")]
    [Range(1f, 100f)]
    public float dropWeight = 25f;

    [TextArea(1, 3)]
    public string customDescription = "Recurso recuperado entre los contenedores del callejón.";
}

[RequireComponent(typeof(Collider2D))]
public class InteractableLoot : MonoBehaviour
{
    [Header("1. Configuración del Contenedor de Basura")]
    public TrashContainerKind containerKind = TrashContainerKind.BoteDeBasura;
    public TrashContainerType containerType
    {
        get => (TrashContainerType)containerKind;
        set => containerKind = (TrashContainerKind)value;
    }
    public string containerDisplayName = "Bote de Basura";

    [Tooltip("Indica si este contenedor ya fue saqueado por el jugador.")]
    public bool isLooted = false;

    [Tooltip("Si está activo (o si no hay sprite de contenedor vacío), el objeto desaparece del mapa al recogerse.")]
    public bool disappearWhenLooted = false;

    [Header("2. Sprites y Estado Visual (Lleno vs Vacío/Saqueado)")]
    public SpriteRenderer containerSpriteRenderer;
    public SpriteRenderer spriteRenderer
    {
        get => containerSpriteRenderer;
        set => containerSpriteRenderer = value;
    }
    public Sprite unlootedSprite;
    public Sprite lootedSprite;
    public Color unlootedColor = Color.white;
    public Color lootedColor = new Color(0.52f, 0.52f, 0.56f, 0.92f);

    [Header("3. Indicador de Interacción en el Mapa")]
    public GameObject promptUI;
    public TextMeshPro promptText;
    public TextMeshPro promptLabel
    {
        get => promptText;
        set => promptText = value;
    }

    [Header("4. Tabla de Probabilidad (LootTable)")]
    public List<LootTableEntry> lootTable = new List<LootTableEntry>();

    [Header("5. Mostrar Diálogo Breve Además de la Notificación UI")]
    public bool showExplorationDialogueOnLoot = true;

    private bool isPlayerInRange = false;

    // UI estática compartida para notificaciones flotantes de saqueo
    private static GameObject s_lootNotificationCanvas;
    private static TextMeshProUGUI s_lootNotificationText;
    private static CanvasGroup s_lootNotificationCanvasGroup;
    private static LootToastRunner s_toastRunner;
    private static Coroutine s_activeToastCoroutine;

    private sealed class LootToastRunner : MonoBehaviour { }

    private void Awake()
    {
        showExplorationDialogueOnLoot = true;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (containerSpriteRenderer == null)
        {
            containerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (lootTable == null || lootTable.Count == 0)
        {
            PopulateDefaultLevel2LootTable();
        }

        RefreshVisualState();
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    /// <summary>
    /// Inicializa una LootTable balanceada con consumibles, materiales de creación para Enrique y munición para el Detective.
    /// </summary>
    public void PopulateDefaultLevel2LootTable()
    {
        lootTable = BuildDefaultAlleyLootTable();
    }

    /// <summary>
    /// Genera la tabla de loot estándar para botes y bolsas de basura del Nivel 2.
    /// </summary>
    public static List<LootTableEntry> BuildDefaultAlleyLootTable()
    {
        return new List<LootTableEntry>
        {
            new LootTableEntry
            {
                entryDisplayName = "Botella Vacia",
                rewardKind = LootRewardKind.InventoryItem,
                fallbackCategory = ItemCategory.MaterialDeCrafteo,
                amount = 1,
                effectValue = 0,
                dropWeight = 24f,
                customDescription = "Envase de vidrio grueso encontrado en el callejon. Ingrediente base para fabricar una Molotov Improvisada o Vendaje Rudo con Intelecto."
            },
            new LootTableEntry
            {
                entryDisplayName = "Gasolina",
                rewardKind = LootRewardKind.InventoryItem,
                fallbackCategory = ItemCategory.MaterialDeCrafteo,
                amount = 1,
                effectValue = 0,
                dropWeight = 24f,
                customDescription = "Botecito con gasolina y un trapo empapado. Combinado con una Botella Vacia permite armar una Molotov Improvisada con Intelecto."
            },
            new LootTableEntry
            {
                entryDisplayName = "Cinta Canela",
                rewardKind = LootRewardKind.InventoryItem,
                fallbackCategory = ItemCategory.MaterialDeCrafteo,
                amount = 1,
                effectValue = 0,
                dropWeight = 20f,
                customDescription = "Rollo de cinta adhesiva industrial resistente para fabricar Vendaje Rudo (+45 HP) o Parche Blindado con Chatarra Afilada."
            },
            new LootTableEntry
            {
                entryDisplayName = "Polvora Callejera",
                rewardKind = LootRewardKind.InventoryItem,
                fallbackCategory = ItemCategory.MaterialDeCrafteo,
                amount = 1,
                effectValue = 0,
                dropWeight = 22f,
                customDescription = "Pólvora negra extraída de cartuchos viejos. Combínala con Chatarra Afilada (Granada de Metralla) o con Gasolina (Bomba Cegadora de Humo)."
            },
            new LootTableEntry
            {
                entryDisplayName = "Chatarra Afilada",
                rewardKind = LootRewardKind.InventoryItem,
                fallbackCategory = ItemCategory.MaterialDeCrafteo,
                amount = 1,
                effectValue = 0,
                dropWeight = 22f,
                customDescription = "Clavos oxidados y láminas cortantes. Combínala con Pólvora Callejera (Granada de Metralla) o con Cinta Canela (Parche Blindado y Munición)."
            },
            new LootTableEntry
            {
                entryDisplayName = "Alcohol Industrial",
                rewardKind = LootRewardKind.InventoryItem,
                fallbackCategory = ItemCategory.MaterialDeCrafteo,
                amount = 1,
                effectValue = 0,
                dropWeight = 18f,
                customDescription = "Botella de alcohol etílico de 96°. Combínalo con un Refresco Frío para destilar un Tónico de Adrenalina (+55 HP, +8 ATQ y Guardia)."
            },
            new LootTableEntry
            {
                entryDisplayName = "Refresco Frio",
                rewardKind = LootRewardKind.InventoryItem,
                fallbackCategory = ItemCategory.ConsumibleCuracion,
                amount = 1,
                effectValue = 35,
                dropWeight = 18f,
                customDescription = "Refresco de cola bien helado en envase de vidrio. Restaura +35 HP en combate o exploracion."
            },
            new LootTableEntry
            {
                entryDisplayName = "Torta Callejera",
                rewardKind = LootRewardKind.InventoryItem,
                fallbackCategory = ItemCategory.ConsumibleCuracion,
                amount = 1,
                effectValue = 60,
                dropWeight = 12f,
                customDescription = "Torta de milanesa envuelta en papel estraza. Restaura +60 HP."
            },
            new LootTableEntry
            {
                entryDisplayName = "Municion .38 Especial (Detective)",
                rewardKind = LootRewardKind.DetectiveAmmo,
                fallbackCategory = ItemCategory.ConsumibleCuracion,
                amount = 3,
                effectValue = 3,
                dropWeight = 20f,
                customDescription = "Cartuchos calibre .38 Especial para el revolver del Detective Vargas (+3 Balas)."
            }
        };
    }

    private void Update()
    {
        if (!isPlayerInRange || isLooted)
        {
            return;
        }

        if (ExplorationDialogueManager.Instance != null && ExplorationDialogueManager.Instance.IsDialogueActive)
        {
            return;
        }

        BattleManager bm = FindAnyObjectByType<BattleManager>();
        if (bm != null && bm.state != BattleState.INACTIVE)
        {
            return;
        }

        if (WasInteractKeyPressedThisFrame())
        {
            TryLootContainer();
        }
    }

    private static bool WasInteractKeyPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E))
        {
            return true;
        }
#endif
        return false;
    }

    /// <summary>
    /// Ejecuta el saqueo del contenedor:
    /// 1. Verifica isLooted == false.
    /// 2. Selecciona un ítem de la LootTable por probabilidad ponderada.
    /// 3. Añade el ítem al inventario general (o munición al Detective), muestra notificación en UI,
    ///    marca isLooted = true y actualiza el sprite/estado visual para indicar que está vacío.
    /// </summary>
    public void TryLootContainer()
    {
        ExplorationDialogueManager dialogueMgr = ExplorationDialogueManager.Instance ?? FindAnyObjectByType<ExplorationDialogueManager>();

        // 1. Verificar si ya fue saqueado
        if (isLooted)
        {
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
            }

            if (dialogueMgr != null)
            {
                dialogueMgr.StartDialogue(new string[]
                {
                    $"NARRADOR: \"Revisas nuevamente {containerDisplayName.ToLower()}, pero ya no queda nada útil entre los desperdicios.\"",
                    "VOZ [INTELECTO]: \"Ya registramos este punto. Sigamos avanzando por el callejón.\""
                });
            }
            else
            {
                ShowLootNotificationToast($"[{containerDisplayName.ToUpper()}]: Este contenedor ya está vacío.");
            }
            return;
        }

        // 2. Seleccionar un ítem de la LootTable
        LootTableEntry selectedEntry = RollItemFromLootTable();
        if (selectedEntry == null)
        {
            return;
        }

        // 3. Añadir al inventario general / munición del Detective
        string notificationMessage = GrantLootRewardToPlayer(selectedEntry);

        // Cambiar estado a isLooted = true antes de mostrar la UI y ocultar si aplica
        isLooted = true;
        if (!ShouldDisappearWhenLooted())
        {
            RefreshVisualState();
        }
        else if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
            MusicManager.Instance.PlayRecogerObjetoSfx();
        }

        if (showExplorationDialogueOnLoot && dialogueMgr != null)
        {
            string itemDisplay = !string.IsNullOrWhiteSpace(selectedEntry.entryDisplayName)
                ? selectedEntry.entryDisplayName
                : (selectedEntry.itemData != null ? selectedEntry.itemData.itemName : selectedEntry.fallbackItemName);
            string desc = !string.IsNullOrWhiteSpace(selectedEntry.customDescription)
                ? selectedEntry.customDescription
                : (selectedEntry.itemData != null ? selectedEntry.itemData.description : "Nos servirá para sobrevivir a los combates de más adelante.");

            string voiceLine = (selectedEntry.rewardKind == LootRewardKind.DetectiveAmmo)
                ? $"INVESTIGADOR VARGAS: \"Buen hallazgo, Bosques. Esos cartuchos .38 Especial me vendrán de maravilla (+{Mathf.Max(1, selectedEntry.amount)} Balas).\""
                : (selectedEntry.fallbackCategory == ItemCategory.MaterialDeCrafteo
                    ? $"VOZ [INTELECTO]: \"{desc}\""
                    : $"VOZ [CORPOREO]: \"{desc}\"");

            dialogueMgr.StartDialogue(new string[]
            {
                $"NARRADOR: \"Rebuscas entre {containerDisplayName.ToLower()} del callejón y encuentras algo útil.\"",
                $"ENRIQUE BOSQUES: \"Mira esto... {itemDisplay} (x{Mathf.Max(1, selectedEntry.amount)}). Al bolsillo.\"",
                voiceLine,
                $"[ OBJETO OBTENIDO: {itemDisplay.ToUpper()} x{Mathf.Max(1, selectedEntry.amount)} ]"
            });
        }
        else
        {
            ShowLootNotificationToast(notificationMessage);
        }

        if (ShouldDisappearWhenLooted())
        {
            RefreshVisualState();
        }
    }

    private bool ShouldDisappearWhenLooted()
    {
        return disappearWhenLooted || lootedSprite == null;
    }

    private LootTableEntry RollItemFromLootTable()
    {
        if (lootTable == null || lootTable.Count == 0)
        {
            PopulateDefaultLevel2LootTable();
        }

        float totalWeight = 0f;
        for (int i = 0; i < lootTable.Count; i++)
        {
            if (lootTable[i] != null && lootTable[i].dropWeight > 0f)
            {
                totalWeight += lootTable[i].dropWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            return lootTable[0];
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < lootTable.Count; i++)
        {
            LootTableEntry entry = lootTable[i];
            if (entry == null || entry.dropWeight <= 0f) continue;

            cumulative += entry.dropWeight;
            if (roll <= cumulative)
            {
                return entry;
            }
        }

        return lootTable[lootTable.Count - 1];
    }

    private string GrantLootRewardToPlayer(LootTableEntry entry)
    {
        int qty = Mathf.Max(1, entry.amount);

        if (entry.rewardKind == LootRewardKind.DetectiveAmmo)
        {
            DetectiveStats det = DetectiveStats.EnsureInScene();
            int reloaded = 0;
            if (det != null)
            {
                reloaded = det.ReloadAmmo(qty);
                det.SaveProgressionSnapshot();
            }

            // Además, si el tambor ya estaba lleno, entregar también un recurso útil al inventario
            if (reloaded == 0 && InventoryManager.Instance != null)
            {
                ItemData fallbackMat = ResolveOrCreateItemData("Botella Vacía", ItemCategory.Material, 0, "Botella de vidrio recuperada del contenedor.");
                InventoryManager.Instance.AddItem(fallbackMat, 1);
                return $"¡SAQUEADO [{containerDisplayName.ToUpper()}]! Tambor .38 lleno + Obtuviste: Botella Vacía x1";
            }

            return $"¡SAQUEADO [{containerDisplayName.ToUpper()}]! +{qty} Munición .38 Especial para el Detective Vargas (Balas: {det.municionActual}/{det.municionMaxima})";
        }

        if (entry.rewardKind == LootRewardKind.DirectHeal)
        {
            PlayerStats pStats = FindAnyObjectByType<PlayerStats>();
            int healVal = Mathf.Max(10, entry.effectValue);
            if (pStats != null)
            {
                pStats.Heal(healVal);
            }
            return $"¡SAQUEADO [{containerDisplayName.ToUpper()}]! {entry.entryDisplayName} (+{healVal} HP restaurados)";
        }

        // Recompensa estándar: Añadir al InventoryManager general
        EnsureInventoryManagerExists();
        ItemData itemToGive = entry.itemAsset;
        if (itemToGive == null)
        {
            itemToGive = ResolveOrCreateItemData(entry.entryDisplayName, entry.fallbackCategory, entry.effectValue, entry.customDescription);
        }

        if (InventoryManager.Instance != null && itemToGive != null)
        {
            InventoryManager.Instance.AddItem(itemToGive, qty);
            if (PlayerInventoryAndWeaponsUI.Instance != null)
            {
                PlayerInventoryAndWeaponsUI.Instance.RefreshUI();
            }
        }

        string categoryLabel = (entry.fallbackCategory == ItemCategory.Material) ? "Material de Creación" : "Consumible";
        return $"¡OBJETO OBTENIDO [{containerDisplayName.ToUpper()}]! +{qty} {entry.entryDisplayName} ({categoryLabel}) añadido al inventario.";
    }

    private static void EnsureInventoryManagerExists()
    {
        if (InventoryManager.Instance != null) return;
        GameObject go = new GameObject("InventoryManager");
        go.AddComponent<InventoryManager>();
    }

    private static ItemData ResolveOrCreateItemData(string itemName, ItemCategory category, int effectVal, string description)
    {
        // Buscar primero si ya existe en el inventario o en recetas conocidas
        if (InventoryManager.Instance != null)
        {
            foreach (var stack in InventoryManager.Instance.items)
            {
                if (stack != null && stack.item != null &&
                    string.Equals(stack.item.itemName, itemName, StringComparison.OrdinalIgnoreCase))
                {
                    return stack.item;
                }
            }

            if (InventoryManager.Instance.knownRecipes != null)
            {
                foreach (var recipe in InventoryManager.Instance.knownRecipes)
                {
                    if (recipe == null) continue;
                    if (recipe.ingredientA != null && HasMatchingKeyword(recipe.ingredientA.itemName, itemName))
                        return recipe.ingredientA;
                    if (recipe.ingredientB != null && HasMatchingKeyword(recipe.ingredientB.itemName, itemName))
                        return recipe.ingredientB;
                    if (recipe.resultItem != null && HasMatchingKeyword(recipe.resultItem.itemName, itemName))
                        return recipe.resultItem;
                }
            }
        }

        string[] candidatePaths = new string[]
        {
            "Assets/Data/Items/Item_BotellaVacia.asset",
            "Assets/Data/Items/Item_ChilePolvo.asset",
            "Assets/Data/Items/Cinta_Canela.asset",
            "Assets/Data/Items/Item_Refresco.asset",
            "Assets/Data/Items/Item_Torta.asset",
            "Assets/Data/Items/Botella_Vacia.asset",
            "Assets/Data/Items/Chile_Polvo.asset"
        };

        foreach (string path in candidatePaths)
        {
            ItemData loaded = RuntimeAssetCatalog.GetItem(path);
            if (loaded != null && HasMatchingKeyword(loaded.itemName, itemName))
            {
                return loaded;
            }
        }

        // Crear instancia en memoria si no se encontró asset previo
        string canonicalKey = InventoryManager.GetCanonicalItemKey(itemName);
        ItemData runtimeItem = ScriptableObject.CreateInstance<ItemData>();
        runtimeItem.name = "RuntimeLoot_" + itemName.Replace(" ", "");
        if (canonicalKey == "MAT_BOTELLA_VACIA")
        {
            runtimeItem.itemID = "item_botella_vacia";
            runtimeItem.itemName = "Botella Vacia";
            runtimeItem.category = ItemCategory.MaterialDeCrafteo;
            runtimeItem.placeholderColor = new Color(0.30f, 0.65f, 0.68f, 1f);
        }
        else if (canonicalKey == "MAT_GASOLINA")
        {
            runtimeItem.itemID = "item_chile_polvo";
            runtimeItem.itemName = "Gasolina";
            runtimeItem.category = ItemCategory.MaterialDeCrafteo;
            runtimeItem.placeholderColor = new Color(0.92f, 0.38f, 0.12f, 1f);
        }
        else if (canonicalKey == "MAT_CINTA_CANELA")
        {
            runtimeItem.itemID = "cinta_canela";
            runtimeItem.itemName = "Cinta Canela";
            runtimeItem.category = ItemCategory.MaterialDeCrafteo;
            runtimeItem.placeholderColor = new Color(0.72f, 0.52f, 0.24f, 1f);
        }
        else if (canonicalKey == "CONS_REFRESCO")
        {
            runtimeItem.itemID = "item_refresco";
            runtimeItem.itemName = "Refresco Frio";
            runtimeItem.category = ItemCategory.ConsumibleCuracion;
            runtimeItem.healAmount = 35;
            runtimeItem.placeholderColor = new Color(0.35f, 0.95f, 0.48f, 1f);
        }
        else if (canonicalKey == "CONS_TORTA")
        {
            runtimeItem.itemID = "item_torta";
            runtimeItem.itemName = "Torta Callejera";
            runtimeItem.category = ItemCategory.ConsumibleCuracion;
            runtimeItem.healAmount = 60;
            runtimeItem.placeholderColor = new Color(0.88f, 0.65f, 0.25f, 1f);
        }
        else
        {
            runtimeItem.itemName = itemName;
            runtimeItem.category = category;
            runtimeItem.healAmount = (category == ItemCategory.Consumible) ? Mathf.Max(20, effectVal) : 0;
        }
        runtimeItem.description = description;
        runtimeItem.damageAmount = 0;
        runtimeItem.confusesTarget = false;
        return runtimeItem;
    }

    private static bool HasMatchingKeyword(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        string keyA = InventoryManager.GetCanonicalItemKey(a);
        string keyB = InventoryManager.GetCanonicalItemKey(b);
        return !string.IsNullOrEmpty(keyA) && keyA == keyB;
    }

    /// <summary>
    /// Actualiza el sprite, color y texto flotante del contenedor según su estado (isLooted).
    /// </summary>
    public void RefreshVisualState()
    {
        if (promptText != null)
        {
            promptText.text = string.Empty;
        }

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        if (isLooted && ShouldDisappearWhenLooted())
        {
            isPlayerInRange = false;
            gameObject.SetActive(false);
            return;
        }

        if (containerSpriteRenderer != null)
        {
            if (isLooted)
            {
                if (lootedSprite != null)
                {
                    containerSpriteRenderer.sprite = lootedSprite;
                }
                containerSpriteRenderer.color = lootedColor;
            }
            else
            {
                if (unlootedSprite != null)
                {
                    containerSpriteRenderer.sprite = unlootedSprite;
                }
                containerSpriteRenderer.color = unlootedColor;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    /// <summary>
    /// Muestra una notificación visual emergente en la parte superior de la pantalla indicando el objeto saqueado.
    /// </summary>
    public void ShowLootNotificationToast(string message)
    {
        EnsureToastUIExists();
        if (s_lootNotificationText != null)
        {
            PlayerHealthBarUI.EnsureCleanPixelFont(s_lootNotificationText);
            s_lootNotificationText.text = message;
        }

        if (s_toastRunner == null) return;

        if (s_activeToastCoroutine != null)
        {
            s_toastRunner.StopCoroutine(s_activeToastCoroutine);
        }
        s_activeToastCoroutine = s_toastRunner.StartCoroutine(AnimateToastRoutine());
    }

    private static void EnsureToastUIExists()
    {
        if (s_lootNotificationCanvas != null && s_lootNotificationText != null && s_toastRunner != null)
        {
            return;
        }

        if (s_lootNotificationCanvas != null)
        {
            Destroy(s_lootNotificationCanvas);
        }

        s_lootNotificationCanvas = new GameObject("LootNotificationToastCanvas");
        s_toastRunner = s_lootNotificationCanvas.AddComponent<LootToastRunner>();

        Canvas canvas = s_lootNotificationCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;

        CanvasScaler scaler = s_lootNotificationCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject bannerBorder = new GameObject("LootBannerBorder", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        bannerBorder.transform.SetParent(s_lootNotificationCanvas.transform, false);
        RectTransform borderRT = bannerBorder.GetComponent<RectTransform>();
        borderRT.anchorMin = new Vector2(0.22f, 0.88f);
        borderRT.anchorMax = new Vector2(0.78f, 0.955f);
        borderRT.offsetMin = Vector2.zero;
        borderRT.offsetMax = Vector2.zero;

        Image borderImg = bannerBorder.GetComponent<Image>();
        borderImg.color = new Color(0.95f, 0.78f, 0.28f, 0.96f);
        borderImg.raycastTarget = false;

        s_lootNotificationCanvasGroup = bannerBorder.GetComponent<CanvasGroup>();
        s_lootNotificationCanvasGroup.alpha = 0f;
        s_lootNotificationCanvasGroup.blocksRaycasts = false;
        s_lootNotificationCanvasGroup.interactable = false;

        GameObject innerGO = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        innerGO.transform.SetParent(bannerBorder.transform, false);
        RectTransform innerRT = innerGO.GetComponent<RectTransform>();
        innerRT.anchorMin = new Vector2(0.004f, 0.06f);
        innerRT.anchorMax = new Vector2(0.996f, 0.94f);
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;
        Image innerImg = innerGO.GetComponent<Image>();
        innerImg.color = new Color(0.05f, 0.06f, 0.09f, 0.96f);
        innerImg.raycastTarget = false;

        GameObject textGO = new GameObject("ToastText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(innerGO.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.02f, 0.05f);
        textRT.anchorMax = new Vector2(0.98f, 0.95f);
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        s_lootNotificationText = textGO.GetComponent<TextMeshProUGUI>();
        s_lootNotificationText.fontSize = 23f;
        s_lootNotificationText.alignment = TextAlignmentOptions.Center;
        s_lootNotificationText.color = new Color(0.96f, 0.92f, 0.78f, 1f);
        s_lootNotificationText.raycastTarget = false;
        PlayerHealthBarUI.EnsureCleanPixelFont(s_lootNotificationText);
    }

    private static IEnumerator AnimateToastRoutine()
    {
        if (s_lootNotificationCanvasGroup == null) yield break;

        float fadeIn = 0.2f;
        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.deltaTime;
            if (s_lootNotificationCanvasGroup != null)
            {
                s_lootNotificationCanvasGroup.alpha = Mathf.Clamp01(t / fadeIn);
            }
            yield return null;
        }

        if (s_lootNotificationCanvasGroup != null)
        {
            s_lootNotificationCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(2.6f);

        float fadeOut = 0.45f;
        t = 0f;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            if (s_lootNotificationCanvasGroup != null)
            {
                s_lootNotificationCanvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOut);
            }
            yield return null;
        }

        if (s_lootNotificationCanvasGroup != null)
        {
            s_lootNotificationCanvasGroup.alpha = 0f;
        }
    }
}
