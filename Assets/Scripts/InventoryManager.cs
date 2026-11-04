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

    // Snapshot para conservar inventario y arma equipada entre escenas (Nivel 1 -> Nivel 2)
    private struct InventorySnapshot
    {
        public bool initialized;
        public List<InventorySlot> savedItems;
        public List<ItemData> savedUnlockedWeapons;
        public ItemData savedEquippedWeapon;
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

        if (snapshot.initialized)
        {
            RestoreSnapshot();
        }
        else
        {
            items.Clear();
            unlockedWeapons.Clear();
            equippedWeapon = null;
            SyncWeaponsFromItemsList();
            EnsureDefaultStarterWeapon();
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
        if (suppressSaveOnDestroy) return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu")))
        {
            return;
        }

        if (_instance == this)
        {
            SaveSnapshot();
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

        snapshot = new InventorySnapshot
        {
            initialized = true,
            savedItems = clonedItems,
            savedUnlockedWeapons = clonedWeapons,
            savedEquippedWeapon = equippedWeapon
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

        SyncWeaponsFromItemsList();
        EnsureDefaultStarterWeapon();
    }

    public void EnsurePreparedWeaponsLoaded()
    {
        if (preparedWeaponCatalog == null)
            preparedWeaponCatalog = new List<ItemData>();

#if UNITY_EDITOR
        string[] weaponPaths = new string[]
        {
            "Assets/Data/Items/Arma_CadenaBicicleta.asset",
            "Assets/Data/Items/Arma_TuboPlomo.asset",
            "Assets/Data/Items/Arma_BatMadera.asset"
        };

        foreach (string path in weaponPaths)
        {
            ItemData w = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (w != null && !preparedWeaponCatalog.Contains(w))
            {
                preparedWeaponCatalog.Add(w);
            }
        }
#endif

        // Si en runtime no estaban serializadas en el catálogo, crear definiciones en memoria de respaldo
        if (Application.isPlaying && (preparedWeaponCatalog == null || preparedWeaponCatalog.Count == 0))
        {
            preparedWeaponCatalog = CreateRuntimeFallbackWeapons();
        }
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

        InventorySlot existingSlot = items.Find(s => s != null && s.item == item);
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
            if (slot == null || slot.item != item)
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
        if (item == null) return 0;

        RebuildDictionaryFromList();
        return itemLookup.TryGetValue(item, out int count) ? count : 0;
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
                slot.item.category == ItemCategory.ObjetoOfensivoDirecto)
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
        if (recipe.materialA != null && recipe.materialA == recipe.materialB)
        {
            return HasItem(recipe.materialA, 2);
        }

        bool hasA = (recipe.materialA == null) || HasItem(recipe.materialA, 1);
        bool hasB = (recipe.materialB == null) || HasItem(recipe.materialB, 1);

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
            RemoveItem(recipe.materialB, 1);

        return true;
    }
}
