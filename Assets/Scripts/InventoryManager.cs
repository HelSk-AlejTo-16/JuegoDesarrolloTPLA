using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[Serializable]
public class InventorySlot
{
    public ItemData item;
    public int quantity = 1;
}

[Serializable]
public class ItemSlot : InventorySlot
{
}

/// <summary>
/// Gestor de Inventario (Consumibles de Curación + Materiales de Crafteo + Armas Equipables) y Recetas para "Intelecto".
/// Accesible desde el mapa y desde el combate mediante InventoryManager.Instance.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    private static InventoryManager _instance;

    public static InventoryManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<InventoryManager>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("1. Objetos Actuales en el Inventario (Visible en Inspector)")]
    public List<InventorySlot> items = new List<InventorySlot>();

    [Header("2. Recetas Conocidas por Intelecto")]
    public List<CraftingRecipeData> knownRecipes = new List<CraftingRecipeData>();

    [Header("3. Sistema de Armas de Enrique")]
    [Tooltip("Arma equipada actualmente por Enrique. Si es null, pelea a Puño Limpio.")]
    public ItemData equippedWeapon;

    [Tooltip("Lista de armas que Enrique posee y puede equipar en la pestaña de Armas.")]
    public List<ItemData> unlockedWeapons = new List<ItemData>();

    [Tooltip("Catálogo de armas preparadas en el proyecto para mostrar en la sección de Armas.")]
    public List<ItemData> preparedWeaponCatalog = new List<ItemData>();

    [Header("4. Eventos de Actualización")]
    public UnityEvent onInventoryChanged;

    private readonly Dictionary<ItemData, int> itemLookup = new Dictionary<ItemData, int>();

    // Snapshot para conservar inventario, recetas y armas equipadas entre escenas (Nivel 1 -> Nivel 2 -> Nivel 3)
    private struct InventorySnapshot
    {
        public bool initialized;
        public List<InventorySlot> savedItems;
        public List<ItemData> savedUnlockedWeapons;
        public ItemData savedEquippedWeapon;
        public List<CraftingRecipeData> savedKnownRecipes;
    }

    private static InventorySnapshot snapshot;
    private static bool suppressSaveOnDestroy = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnSubsystemRegistration()
    {
        snapshot = default;
        suppressSaveOnDestroy = false;
        _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu") || activeScene.name.Contains("01_Nivel1")))
        {
            ResetSavedInventory();
        }
    }

    public static void ResetSavedInventory()
    {
        snapshot = default;
        suppressSaveOnDestroy = true;
    }

    public static void EnableSaveOnDestroy()
    {
        suppressSaveOnDestroy = false;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        Scene activeScene = SceneManager.GetActiveScene();
        bool isIntroOrMenu = activeScene.IsValid() && (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu"));
        bool isLevel1 = activeScene.IsValid() && activeScene.name.Contains("01_Nivel1");

        if (isIntroOrMenu || isLevel1)
        {
            ResetSavedInventory();
        }

        suppressSaveOnDestroy = false;

        EnsurePreparedWeaponsLoaded();
        EnsureKnownRecipesLoaded();

        if (snapshot.initialized)
        {
            RestoreSnapshot();
            EnsureKnownRecipesLoaded();
        }
        else
        {
            items.Clear();
            unlockedWeapons.Clear();
            equippedWeapon = null;
            SyncWeaponsFromItemsList();
            EnsureDefaultStarterWeapon();
            EnsureKnownRecipesLoaded();
            SaveSnapshot();
        }

        RebuildDictionaryFromList();
    }

    private void Start()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && !activeScene.name.Contains("00_MenuIntro") && !activeScene.name.Contains("Intro") && !activeScene.name.Contains("Menu"))
        {
            PlayerInventoryAndWeaponsUI.EnsureInScene();
            PlayerLevelUpAndStatsUI.EnsureInScene();
        }
    }

    private void OnDestroy()
    {
        if (suppressSaveOnDestroy)
        {
            if (_instance == this) _instance = null;
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu")))
        {
            if (_instance == this) _instance = null;
            return;
        }

        if (_instance == this)
        {
            SaveSnapshot();
            _instance = null;
        }
    }

    private void OnValidate()
    {
        EnsurePreparedWeaponsLoaded();
        SyncWeaponsFromItemsList();
        RebuildDictionaryFromList();
    }

    public void SaveSnapshot()
    {
        List<InventorySlot> clonedItems = new List<InventorySlot>();
        if (items != null)
        {
            foreach (InventorySlot s in items)
            {
                if (s != null && s.item != null && s.quantity > 0)
                {
                    clonedItems.Add(new InventorySlot { item = s.item, quantity = s.quantity });
                }
            }
        }

        List<ItemData> clonedWeapons = new List<ItemData>();
        if (unlockedWeapons != null)
        {
            foreach (ItemData w in unlockedWeapons)
            {
                if (w != null && !clonedWeapons.Contains(w))
                {
                    clonedWeapons.Add(w);
                }
            }
        }

        List<CraftingRecipeData> clonedRecipes = new List<CraftingRecipeData>();
        if (knownRecipes != null)
        {
            foreach (CraftingRecipeData r in knownRecipes)
            {
                if (r != null && !clonedRecipes.Contains(r))
                {
                    clonedRecipes.Add(r);
                }
            }
        }

        snapshot = new InventorySnapshot
        {
            initialized = true,
            savedItems = clonedItems,
            savedUnlockedWeapons = clonedWeapons,
            savedEquippedWeapon = equippedWeapon,
            savedKnownRecipes = clonedRecipes
        };
    }

    private void RestoreSnapshot()
    {
        items = new List<InventorySlot>();
        if (snapshot.savedItems != null)
        {
            foreach (InventorySlot s in snapshot.savedItems)
            {
                if (s != null && s.item != null && s.quantity > 0)
                {
                    items.Add(new InventorySlot { item = s.item, quantity = s.quantity });
                }
            }
        }

        unlockedWeapons = new List<ItemData>();
        if (snapshot.savedUnlockedWeapons != null)
        {
            foreach (ItemData w in snapshot.savedUnlockedWeapons)
            {
                if (w != null && !unlockedWeapons.Contains(w))
                {
                    unlockedWeapons.Add(w);
                }
            }
        }

        equippedWeapon = snapshot.savedEquippedWeapon;

        if (snapshot.savedKnownRecipes != null && snapshot.savedKnownRecipes.Count > 0)
        {
            if (knownRecipes == null) knownRecipes = new List<CraftingRecipeData>();
            foreach (CraftingRecipeData r in snapshot.savedKnownRecipes)
            {
                if (r != null && !knownRecipes.Contains(r))
                {
                    knownRecipes.Add(r);
                }
            }
        }

        SyncWeaponsFromItemsList();
        EnsureDefaultStarterWeapon();
        RebuildDictionaryFromList();

        onInventoryChanged?.Invoke();
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.NotifyEquipmentChanged();
        }
    }

    public void EnsurePreparedWeaponsLoaded()
    {
        if (preparedWeaponCatalog == null)
            preparedWeaponCatalog = new List<ItemData>();

        string[] weaponPaths = new string[]
        {
            "Assets/Data/Items/Arma_CadenaBicicleta.asset",
            "Assets/Data/Items/Arma_TuboPlomo.asset",
            "Assets/Data/Items/Arma_BatMadera.asset"
        };

        foreach (string path in weaponPaths)
        {
            ItemData w = RuntimeAssetCatalog.GetItem(path);
            if (w != null && !preparedWeaponCatalog.Contains(w))
            {
                preparedWeaponCatalog.Add(w);
            }
        }

        // Si en runtime no estaban serializadas en el catálogo, crear definiciones en memoria de respaldo
        if (Application.isPlaying && (preparedWeaponCatalog == null || preparedWeaponCatalog.Count == 0))
        {
            preparedWeaponCatalog = CreateRuntimeFallbackWeapons();
        }
    }

    public ItemData GetOrCreateCanonicalItem(string itemName, string description, ItemCategory category, int healAmount, Color placeholderColor)
    {
        string targetKey = GetCanonicalItemKey(itemName);
        if (items != null)
        {
            foreach (InventorySlot s in items)
            {
                if (s != null && s.item != null && GetCanonicalItemKey(s.item.itemName) == targetKey)
                {
                    return s.item;
                }
            }
        }
        if (knownRecipes != null)
        {
            foreach (CraftingRecipeData r in knownRecipes)
            {
                if (r == null) continue;
                if (r.materialA != null && GetCanonicalItemKey(r.materialA.itemName) == targetKey) return r.materialA;
                if (r.materialB != null && GetCanonicalItemKey(r.materialB.itemName) == targetKey) return r.materialB;
            }
        }
        string[] candidatePaths = new string[]
        {
            "Assets/Data/Items/Item_BotellaVacia.asset",
            "Assets/Data/Items/Botella_Vacia.asset",
            "Assets/Data/Items/Item_ChilePolvo.asset",
            "Assets/Data/Items/Chile_Polvo.asset",
            "Assets/Data/Items/Cinta_Canela.asset",
            "Assets/Data/Items/Item_Refresco.asset",
            "Assets/Data/Items/Refresco_Cola.asset",
            "Assets/Data/Items/Item_Torta.asset",
            "Assets/Data/Items/Torta_Jamon.asset"
        };
        foreach (string p in candidatePaths)
        {
            ItemData loaded = RuntimeAssetCatalog.GetItem(p);
            if (loaded != null && GetCanonicalItemKey(loaded.itemName) == targetKey)
            {
                return loaded;
            }
        }
        ItemData created = ScriptableObject.CreateInstance<ItemData>();
        created.itemID = targetKey.ToLowerInvariant();
        created.itemName = itemName;
        created.description = description;
        created.category = category;
        created.healAmount = healAmount;
        created.placeholderColor = placeholderColor;
        return created;
    }

    public void EnsureKnownRecipesLoaded()
    {
        if (knownRecipes == null)
        {
            knownRecipes = new List<CraftingRecipeData>();
        }
        else
        {
            HashSet<string> seenKeys = new HashSet<string>();
            List<CraftingRecipeData> unique = new List<CraftingRecipeData>(knownRecipes.Count);
            foreach (CraftingRecipeData r in knownRecipes)
            {
                if (r == null || string.IsNullOrEmpty(r.recipeName)) continue;
                string k = GetCanonicalItemKey(r.recipeName);
                if (seenKeys.Add(k))
                {
                    unique.Add(r);
                }
            }
            knownRecipes = unique;
        }

        CraftingRecipeData rMolotov = RuntimeAssetCatalog.GetRecipe("Assets/Data/Items/Receta_BombaChile.asset");
        CraftingRecipeData rVendaje = RuntimeAssetCatalog.GetRecipe("Assets/Data/Items/Receta_VendajeRudo.asset");
        if (rMolotov != null && !HasRecipeNamed(rMolotov.recipeName)) knownRecipes.Add(rMolotov);
        if (rVendaje != null && !HasRecipeNamed(rVendaje.recipeName)) knownRecipes.Add(rVendaje);

        ItemData matBotella = GetOrCreateCanonicalItem("Botella Vacia", "Envase de vidrio grueso. Sirve como recipiente para fabricar Molotovs o Vendajes.", ItemCategory.MaterialDeCrafteo, 0, new Color(0.55f, 0.82f, 0.92f, 1f));
        ItemData matGasolina = GetOrCreateCanonicalItem("Gasolina", "Combustible inflamable callejero para fabricar explosivos e incendiarios.", ItemCategory.MaterialDeCrafteo, 0, new Color(0.95f, 0.55f, 0.18f, 1f));
        ItemData matCinta = GetOrCreateCanonicalItem("Cinta Canela", "Rollo de cinta adhesiva industrial para vendajes y parches blindados.", ItemCategory.MaterialDeCrafteo, 0, new Color(0.78f, 0.62f, 0.35f, 1f));
        ItemData matPolvora = GetOrCreateCanonicalItem("Polvora Callejera", "Pólvora negra extraída de cartuchos de los sicarios. Base para granadas y bombas de humo.", ItemCategory.MaterialDeCrafteo, 0, new Color(0.85f, 0.28f, 0.25f, 1f));
        ItemData matChatarra = GetOrCreateCanonicalItem("Chatarra Afilada", "Clavos oxidados, eslabones y láminas cortantes para metralla o refuerzo táctico.", ItemCategory.MaterialDeCrafteo, 0, new Color(0.65f, 0.70f, 0.76f, 1f));
        ItemData matAlcohol = GetOrCreateCanonicalItem("Alcohol Industrial", "Botella de alcohol etílico de 96° para desinfectar heridas o destilar estimulantes.", ItemCategory.MaterialDeCrafteo, 0, new Color(0.35f, 0.88f, 0.75f, 1f));
        ItemData consRefresco = GetOrCreateCanonicalItem("Refresco Frio", "Refresco de cola helado (+35 HP) rico en cafeína y azúcar.", ItemCategory.ConsumibleCuracion, 35, new Color(0.25f, 0.82f, 0.42f, 1f));

        // 1. Bomba Molotov (si no estaba ya cargada)
        if (!HasRecipeNamed("Molotov") && !HasRecipeNamed("Bomba"))
        {
            CraftingRecipeData molotov = ScriptableObject.CreateInstance<CraftingRecipeData>();
            molotov.recipeName = "Bomba Molotov";
            molotov.materialA = matBotella;
            molotov.materialB = matGasolina;
            molotov.damageIgnoringDefense = 30;
            molotov.healAmount = 0;
            molotov.appliesConfusion = true;
            molotov.intelectoComment = "VOZ [INTELECTO]: \"Combustible inflamable en envase de vidrio con mecha. ¡30 de daño de fuego ignorando defensa y confunde al objetivo!\"";
            knownRecipes.Add(molotov);
        }

        // 2. Vendaje Rudo (si no estaba ya cargado)
        if (!HasRecipeNamed("Vendaje"))
        {
            CraftingRecipeData vendaje = ScriptableObject.CreateInstance<CraftingRecipeData>();
            vendaje.recipeName = "Vendaje Rudo";
            vendaje.materialA = matBotella;
            vendaje.materialB = matCinta;
            vendaje.damageIgnoringDefense = 0;
            vendaje.healAmount = 45;
            vendaje.appliesConfusion = false;
            vendaje.intelectoComment = "VOZ [INTELECTO]: \"Compresa improvisada con cinta industrial para cerrar heridas en combate (+45 HP).\"";
            knownRecipes.Add(vendaje);
        }

        // 3. NUEVO CRAFTEO 1: Granada de Metralla (Pólvora Callejera + Chatarra Afilada) -> Daño en Área + Rompe Defensa y Ataque
        if (!HasRecipeNamed("Granada de Metralla"))
        {
            CraftingRecipeData metralla = ScriptableObject.CreateInstance<CraftingRecipeData>();
            metralla.recipeName = "Granada de Metralla";
            metralla.materialA = matPolvora;
            metralla.materialB = matChatarra;
            metralla.damageIgnoringDefense = 28;
            metralla.healAmount = 0;
            metralla.appliesConfusion = false;
            metralla.isAreaOfEffect = true;
            metralla.enemyDefenseDebuff = 5;
            metralla.enemyAttackDebuff = 3;
            metralla.intelectoComment = "VOZ [INTELECTO]: \"¡Granada de Metralla! La onda expansiva golpea a TODOS los enemigos (28 daño) y destroza su blindaje (-5 DEF, -3 ATQ).\"";
            knownRecipes.Add(metralla);
        }

        // 4. NUEVO CRAFTEO 2: Tónico de Adrenalina (Alcohol Industrial + Refresco Frío) -> Alta Curación + Buff de Ataque + Guardia Defensiva
        if (!HasRecipeNamed("Tonico de Adrenalina") && !HasRecipeNamed("Tónico de Adrenalina"))
        {
            CraftingRecipeData tonico = ScriptableObject.CreateInstance<CraftingRecipeData>();
            tonico.recipeName = "Tónico de Adrenalina";
            tonico.materialA = matAlcohol;
            tonico.materialB = consRefresco;
            tonico.damageIgnoringDefense = 0;
            tonico.healAmount = 55;
            tonico.appliesConfusion = false;
            tonico.playerAttackBuff = 8;
            tonico.grantsPlayerGuard = true;
            tonico.intelectoComment = "VOZ [INTELECTO]: \"¡Tónico de Adrenalina! Alcohol y cafeína pura: restaura +55 HP, otorga +8 ATQ al próximo golpe y activa Guardia Defensiva.\"";
            knownRecipes.Add(tonico);
        }

        // 5. NUEVO CRAFTEO 3: Bomba Cegadora de Humo (Pólvora Callejera + Gasolina) -> Daño en Área + Confunde/Aturde a TODOS + Reduce Velocidad
        if (!HasRecipeNamed("Bomba Cegadora"))
        {
            CraftingRecipeData humo = ScriptableObject.CreateInstance<CraftingRecipeData>();
            humo.recipeName = "Bomba Cegadora de Humo";
            humo.materialA = matPolvora;
            humo.materialB = matGasolina;
            humo.damageIgnoringDefense = 18;
            humo.healAmount = 0;
            humo.appliesConfusion = true;
            humo.isAreaOfEffect = true;
            humo.enemyMotricidadDebuff = 4;
            humo.intelectoComment = "VOZ [INTELECTO]: \"¡Bomba Cegadora de Humo! El fogonazo químico daña a TODOS (18 daño), los deja CONFUNDIDOS por 1 turno y reduce su velocidad (-4 VEL).\"";
            knownRecipes.Add(humo);
        }

        // 6. NUEVO CRAFTEO 4: Parche Blindado y Munición (Cinta Canela + Chatarra Afilada) -> Curación + Bono de Ataque + Recarga Balas del Detective
        if (!HasRecipeNamed("Parche Blindado"))
        {
            CraftingRecipeData parche = ScriptableObject.CreateInstance<CraftingRecipeData>();
            parche.recipeName = "Parche Blindado y Munición";
            parche.materialA = matCinta;
            parche.materialB = matChatarra;
            parche.damageIgnoringDefense = 0;
            parche.healAmount = 35;
            parche.appliesConfusion = false;
            parche.playerAttackBuff = 6;
            parche.enemyDefenseDebuff = 4;
            parche.detectiveAmmoRestored = 3;
            parche.intelectoComment = "VOZ [INTELECTO]: \"¡Parche Blindado y Munición! Refuerza el abrigo (+35 HP), envuelve los nudillos con metal (+6 ATQ, -4 DEF enemigo) y recarga +3 Balas .38 para Vargas.\"";
            knownRecipes.Add(parche);
        }
    }

    private bool HasRecipeNamed(string substring)
    {
        if (knownRecipes == null || string.IsNullOrEmpty(substring)) return false;
        string subUpper = substring.ToUpperInvariant();
        foreach (CraftingRecipeData r in knownRecipes)
        {
            if (r != null && !string.IsNullOrEmpty(r.recipeName) && r.recipeName.ToUpperInvariant().Contains(subUpper))
            {
                return true;
            }
        }
        return false;
    }

    private List<ItemData> CreateRuntimeFallbackWeapons()
    {
        List<ItemData> list = new List<ItemData>();

        ItemData cadena = ScriptableObject.CreateInstance<ItemData>();
        cadena.itemID = "arma_cadena_bicicleta";
        cadena.itemName = "Cadena de Bicicleta";
        cadena.description = "Cadena de acero engrasada que sobró del taller. Envuelta en los nudillos otorga golpes rápidos y certeros.";
        cadena.combatUseMessage = "¡Enrique azota con la Cadena de Bicicleta!";
        cadena.placeholderColor = new Color(0.78f, 0.82f, 0.88f, 1f);
        cadena.category = ItemCategory.Arma;
        cadena.weaponAttackBonus = 4;
        cadena.weaponMotricidadBonus = 2;
        list.Add(cadena);

        ItemData tubo = ScriptableObject.CreateInstance<ItemData>();
        tubo.itemID = "arma_tubo_plomo";
        tubo.itemName = "Tubo de Plomo";
        tubo.description = "Tubo pesado arrancado de una tubería oxidada de Santa Felipina. Multiplica la contundencia de Corpóreo.";
        tubo.combatUseMessage = "¡Enrique descarga un golpe demoledor con el Tubo de Plomo!";
        tubo.placeholderColor = new Color(0.58f, 0.62f, 0.66f, 1f);
        tubo.category = ItemCategory.Arma;
        tubo.weaponAttackBonus = 6;
        tubo.weaponMotricidadBonus = 0;
        list.Add(tubo);

        ItemData bat = ScriptableObject.CreateInstance<ItemData>();
        bat.itemID = "arma_bat_madera";
        bat.itemName = "Bat Callejero Reforzado";
        bat.description = "Bat de madera maciza envuelto con alambre y cinta canela. Arma pesada de alto impacto físico.";
        bat.combatUseMessage = "¡Enrique conecta un batazo brutal!";
        bat.placeholderColor = new Color(0.76f, 0.48f, 0.22f, 1f);
        bat.category = ItemCategory.Arma;
        bat.weaponAttackBonus = 9;
        bat.weaponMotricidadBonus = -1;
        list.Add(bat);

        return list;
    }

    private void SyncWeaponsFromItemsList()
    {
        if (unlockedWeapons == null)
            unlockedWeapons = new List<ItemData>();

        if (items == null) return;

        foreach (InventorySlot slot in items)
        {
            if (slot != null && slot.item != null && slot.quantity > 0 && slot.item.category == ItemCategory.Arma)
            {
                if (!unlockedWeapons.Contains(slot.item))
                {
                    unlockedWeapons.Add(slot.item);
                }
            }
        }
    }

    private void EnsureDefaultStarterWeapon()
    {
        if (unlockedWeapons == null)
            unlockedWeapons = new List<ItemData>();

        // Enrique inicia a mano limpia (Puños Desnudos) sin armas iniciales.
        // Las armas (Cadena de Bicicleta, Tubo de Plomo, Bat Reforzado) deben ser encontradas
        // y recogidas explorando el mapa de Santa Felipina.
    }

    private void RebuildDictionaryFromList()
    {
        itemLookup.Clear();
        if (items == null) return;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            InventorySlot slot = items[i];
            if (slot == null || slot.item == null || slot.quantity <= 0)
                continue;

            if (itemLookup.ContainsKey(slot.item))
            {
                itemLookup[slot.item] += slot.quantity;
            }
            else
            {
                itemLookup[slot.item] = slot.quantity;
            }
        }
    }

    /// <summary>
    /// Determina si dos definiciones de ItemData corresponden al mismo objeto lógico en todo el juego
    /// (por ejemplo: Botella Vacia / Botella Vacía, Gasolina / Chile en Polvo, Cinta Canela / Trapo Limpio, Refresco Frio / Refresco de Cola, Torta Callejera / Torta de Jamón).
    /// </summary>
    public static bool AreItemsEquivalent(ItemData a, ItemData b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        if (!string.IsNullOrEmpty(a.itemID) && !string.IsNullOrEmpty(b.itemID) &&
            string.Equals(a.itemID, b.itemID, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string keyA = GetCanonicalItemKey(a.itemName);
        string keyB = GetCanonicalItemKey(b.itemName);
        return !string.IsNullOrEmpty(keyA) && keyA == keyB;
    }

    public static string GetCanonicalItemKey(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;
        string u = rawName.Trim().ToUpperInvariant()
            .Replace("Á", "A")
            .Replace("É", "E")
            .Replace("Í", "I")
            .Replace("Ó", "O")
            .Replace("Ú", "U");

        if (u.Contains("BOTELLA")) return "MAT_BOTELLA_VACIA";
        if (u.Contains("GASOLINA") || u.Contains("CHILE")) return "MAT_GASOLINA";
        if (u.Contains("CINTA") || u.Contains("TRAPO")) return "MAT_CINTA_CANELA";
        if (u.Contains("POLVORA")) return "MAT_POLVORA";
        if (u.Contains("CHATARRA") || u.Contains("CLAVO") || u.Contains("METRALLA")) return "MAT_CHATARRA";
        if (u.Contains("ALCOHOL") || u.Contains("ETILICO") || u.Contains("BOTIQUIN")) return "MAT_ALCOHOL";
        if (u.Contains("REFRESCO")) return "CONS_REFRESCO";
        if (u.Contains("TORTA")) return "CONS_TORTA";
        if (u.Contains("VENDAJE")) return "CONS_VENDAJE_RUDO";
        if (u.Contains("MOLOTOV") || u.Contains("BOMBA DE CHILE")) return "OFENS_MOLOTOV";
        return u;
    }

    /// <summary>
    /// Agrega una cantidad de un objeto (o arma) al inventario de Enrique.
    /// </summary>
    public void AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return;

        if (item.category == ItemCategory.Arma)
        {
            AddWeapon(item, autoEquipIfNone: false);
        }

        InventorySlot existingSlot = items.Find(s => s != null && s.item != null && AreItemsEquivalent(s.item, item));
        if (existingSlot != null)
        {
            existingSlot.quantity += amount;
        }
        else
        {
            items.Add(new InventorySlot
            {
                item = item,
                quantity = amount
            });
        }

        RebuildDictionaryFromList();
        SaveSnapshot();
        Debug.Log($"[InventoryManager] +{amount}x {item.itemName} agregado al inventario (Total: {GetItemQuantity(item)}).");
        onInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Desbloquea un arma en la sección de Armas de Enrique.
    /// </summary>
    public void AddWeapon(ItemData weapon, bool autoEquipIfNone = false)
    {
        if (weapon == null) return;

        if (unlockedWeapons == null)
            unlockedWeapons = new List<ItemData>();

        if (!unlockedWeapons.Contains(weapon))
        {
            unlockedWeapons.Add(weapon);
        }

        if (preparedWeaponCatalog != null && !preparedWeaponCatalog.Contains(weapon))
        {
            preparedWeaponCatalog.Add(weapon);
        }

        if (autoEquipIfNone && equippedWeapon == null)
        {
            equippedWeapon = weapon;
        }

        SaveSnapshot();
        onInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Equipa un arma de la lista (o pasa null para pelear a Puños Desnudos).
    /// </summary>
    public void EquipWeapon(ItemData weapon)
    {
        if (weapon != null && unlockedWeapons != null && !unlockedWeapons.Contains(weapon))
        {
            unlockedWeapons.Add(weapon);
        }

        equippedWeapon = weapon;
        SaveSnapshot();
        onInventoryChanged?.Invoke();

        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null)
        {
            stats.NotifyEquipmentChanged();
        }
    }

    /// <summary>
    /// Desequipa el arma actual, volviendo a Puños Desnudos.
    /// </summary>
    public void UnequipWeapon()
    {
        EquipWeapon(null);
    }

    /// <summary>
    /// Verifica si Enrique ya posee/desbloqueó esta arma.
    /// </summary>
    public bool IsWeaponUnlocked(ItemData weapon)
    {
        if (weapon == null) return true;
        if (unlockedWeapons != null && unlockedWeapons.Contains(weapon))
            return true;

        if (items != null)
        {
            foreach (InventorySlot s in items)
            {
                if (s != null && s.item == weapon && s.quantity > 0)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Remueve una cantidad de un objeto del inventario. Devuelve true si se pudo remover.
    /// </summary>
    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;

        if (!HasItem(item, amount))
            return false;

        int remainingToRemove = amount;
        for (int i = items.Count - 1; i >= 0 && remainingToRemove > 0; i--)
        {
            InventorySlot slot = items[i];
            if (slot == null || slot.item == null || !AreItemsEquivalent(slot.item, item))
                continue;

            if (slot.quantity > remainingToRemove)
            {
                slot.quantity -= remainingToRemove;
                remainingToRemove = 0;
            }
            else
            {
                remainingToRemove -= slot.quantity;
                items.RemoveAt(i);
            }
        }

        RebuildDictionaryFromList();
        SaveSnapshot();
        Debug.Log($"[InventoryManager] -{amount}x {item.itemName} removido del inventario (Restante: {GetItemQuantity(item)}).");
        onInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Verifica si el jugador tiene al menos la cantidad indicada de un objeto.
    /// </summary>
    public bool HasItem(ItemData item, int amount = 1)
    {
        if (item == null) return false;
        if (amount <= 0) return true;

        return GetItemQuantity(item) >= amount;
    }

    /// <summary>
    /// Devuelve cuántas unidades de este objeto tiene Enrique actualmente.
    /// </summary>
    public int GetItemQuantity(ItemData item)
    {
        if (item == null || items == null) return 0;

        int total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            InventorySlot slot = items[i];
            if (slot != null && slot.item != null && slot.quantity > 0 && AreItemsEquivalent(slot.item, item))
            {
                total += slot.quantity;
            }
        }
        return total;
    }

    public int GetItemQuantityByCanonicalKey(string canonicalKey)
    {
        if (string.IsNullOrEmpty(canonicalKey) || items == null) return 0;
        int total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            InventorySlot slot = items[i];
            if (slot != null && slot.item != null && slot.quantity > 0 &&
                GetCanonicalItemKey(slot.item.itemName) == canonicalKey)
            {
                total += slot.quantity;
            }
        }
        return total;
    }

    public bool RemoveItemByCanonicalKey(string canonicalKey, int amount = 1)
    {
        if (string.IsNullOrEmpty(canonicalKey) || amount <= 0 || GetItemQuantityByCanonicalKey(canonicalKey) < amount)
            return false;

        int remaining = amount;
        for (int i = items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            InventorySlot slot = items[i];
            if (slot == null || slot.item == null || GetCanonicalItemKey(slot.item.itemName) != canonicalKey)
                continue;

            if (slot.quantity > remaining)
            {
                slot.quantity -= remaining;
                remaining = 0;
            }
            else
            {
                remaining -= slot.quantity;
                items.RemoveAt(i);
            }
        }

        RebuildDictionaryFromList();
        SaveSnapshot();
        onInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Devuelve todos los slots del inventario cuyos objetos sean ConsumibleCuracion u ObjetoOfensivoDirecto y tengan quantity > 0.
    /// </summary>
    public List<InventorySlot> GetConsumableItems()
    {
        List<InventorySlot> consumables = new List<InventorySlot>();
        if (items == null) return consumables;

        foreach (InventorySlot slot in items)
        {
            if (slot == null || slot.item == null || slot.quantity <= 0)
                continue;

            if (slot.item.category == ItemCategory.ConsumibleCuracion ||
                slot.item.category == ItemCategory.ObjetoOfensivoDirecto ||
                slot.item.healAmount > 0)
            {
                consumables.Add(slot);
            }
        }

        return consumables;
    }

    /// <summary>
    /// Devuelve todos los objetos no-arma (Consumibles, Ofensivos y Materiales) con quantity > 0 para mostrar en el Menú de Inventario.
    /// </summary>
    public List<InventorySlot> GetAllNonWeaponItems()
    {
        List<InventorySlot> result = new List<InventorySlot>();
        if (items == null) return result;

        foreach (InventorySlot slot in items)
        {
            if (slot == null || slot.item == null || slot.quantity <= 0)
                continue;

            if (slot.item.category != ItemCategory.Arma)
            {
                result.Add(slot);
            }
        }

        return result;
    }

    /// <summary>
    /// Cuenta el total de unidades de objetos (no armas) que Enrique lleva en su mochila.
    /// </summary>
    public int GetTotalItemUnitsCount()
    {
        int total = 0;
        if (items == null) return 0;
        foreach (InventorySlot slot in items)
        {
            if (slot != null && slot.item != null && slot.quantity > 0 && slot.item.category != ItemCategory.Arma)
            {
                total += slot.quantity;
            }
        }
        return total;
    }

    /// <summary>
    /// Verifica si el jugador tiene materialA y materialB en su inventario para fabricar la receta.
    /// </summary>
    public bool CanCraft(CraftingRecipeData recipe)
    {
        if (recipe == null)
            return false;

        if (recipe.materialA == null && recipe.materialB == null)
            return false;

        // Si ambos materiales son el mismo objeto, requiere 2 unidades
        if (recipe.materialA != null && recipe.materialB != null && AreItemsEquivalent(recipe.materialA, recipe.materialB))
        {
            return HasItem(recipe.materialA, 2);
        }

        bool hasA = (recipe.materialA == null) || HasItem(recipe.materialA, 1);
        bool hasB = (recipe.materialB == null) || HasItem(recipe.materialB, 1);

        // Para Vendaje Rudo (receta curativa), permitir Botella Vacía + Cinta Canela (o Refresco Frío de respaldo)
        bool isVendaje = recipe.recipeName != null && recipe.recipeName.ToUpperInvariant().Contains("VENDAJE");
        if (!hasB && isVendaje)
        {
            hasB = GetItemQuantityByCanonicalKey("MAT_CINTA_CANELA") >= 1 ||
                   GetItemQuantityByCanonicalKey("CONS_REFRESCO") >= 1;
        }

        return hasA && hasB;
    }

    /// <summary>
    /// Consume 1 unidad de materialA y 1 unidad de materialB de la receta si están disponibles.
    /// </summary>
    public bool ConsumeRecipeMaterials(CraftingRecipeData recipe)
    {
        if (!CanCraft(recipe))
            return false;

        if (recipe.materialA != null)
            RemoveItem(recipe.materialA, 1);

        if (recipe.materialB != null)
        {
            bool isVendaje = recipe.recipeName != null && recipe.recipeName.ToUpperInvariant().Contains("VENDAJE");
            if (!RemoveItem(recipe.materialB, 1) && isVendaje)
            {
                if (!RemoveItemByCanonicalKey("MAT_CINTA_CANELA", 1))
                {
                    RemoveItemByCanonicalKey("CONS_REFRESCO", 1);
                }
            }
        }

        return true;
    }
}
