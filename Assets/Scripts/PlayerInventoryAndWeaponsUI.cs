using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum InventorySubMenu
{
    Objetos,
    Armas
}

/// <summary>
/// Menú de Inventario y Armas de Enrique Bosques organizado en SubMenús (Pestañas):
/// 1. SUBMENÚ OBJETOS: Consumibles de curación, objetos ofensivos y recetas de Intelecto con espacio amplio.
/// 2. SUBMENÚ ARMAS: Retrato de Enrique con el arma equipada, estadísticas y catálogo de armas callejeras.
/// </summary>
public class PlayerInventoryAndWeaponsUI : MonoBehaviour
{
    private static PlayerInventoryAndWeaponsUI _instance;
    public static PlayerInventoryAndWeaponsUI Instance => _instance;

    [Header("Referencias de Datos")]
    public PlayerStats playerStats;
    public InventoryManager inventoryManager;
    public BattleManager battleManager;
    public PlayerController2D playerController;
    public TMP_FontAsset customFont;

    [Header("Retratos y Visuales")]
    public Sprite enriquePortrait;
    public Sprite corporeoPortrait;

    [Header("Colores de Estilo Noir")]
    public Color consumableGreenColor = new Color(0.22f, 0.85f, 0.42f, 1f);
    public Color materialCyanColor = new Color(0.22f, 0.68f, 0.94f, 1f);
    public Color offensiveOrangeColor = new Color(0.94f, 0.44f, 0.18f, 1f);
    public Color weaponGoldColor = new Color(0.98f, 0.78f, 0.24f, 1f);
    public Color corporeoRedColor = new Color(0.86f, 0.24f, 0.24f, 1f);

    [Header("Botones HUD (Texto: INVENTARIO)")]
    [Tooltip("Botón HUD primario ubicado directamente debajo de la barra de vida en la esquina superior izquierda.")]
    public GameObject hudTopLeftButtonRoot;
    public Button hudTopLeftOpenButton;
    public TextMeshProUGUI hudTopLeftButtonLabel;
    public Image hudTopLeftButtonBorder;

    [Tooltip("Botón HUD secundario ubicado en la esquina superior derecha.")]
    public GameObject hudButtonRoot;
    public Button hudOpenInventoryButton;
    public TextMeshProUGUI hudButtonLabel;
    public Image hudButtonBorder;

    [Header("Modal Principal y SubMenús")]
    public GameObject modalPanelRoot;
    public TextMeshProUGUI headerTitleText;
    public TextMeshProUGUI subHeaderStatsText;
    public TextMeshProUGUI feedbackBannerText;

    [Header("Pestañas de Navegación de SubMenús")]
    public InventorySubMenu currentSubMenu = InventorySubMenu.Objetos;
    public Button tabObjetosButton;
    public Image tabObjetosBorderImage;
    public Image tabObjetosBgImage;
    public TextMeshProUGUI tabObjetosLabel;

    public Button tabArmasButton;
    public Image tabArmasBorderImage;
    public Image tabArmasBgImage;
    public TextMeshProUGUI tabArmasLabel;

    [Header("Panel 1: SubMenú de Objetos")]
    public GameObject itemsPanelRoot;
    public RectTransform itemsListContainer;
    public TextMeshProUGUI itemsSectionCounterText;
    public TextMeshProUGUI recipesStatusText;
    public Button quickCraftHealRecipeButton;
    public Image quickCraftHealBorderImage;
    public TextMeshProUGUI quickCraftHealButtonLabel;

    [Header("Panel 2: SubMenú de Armas y Retrato de Enrique")]
    public GameObject weaponsPanelRoot;
    public GameObject enriquePortraitCardRoot;
    public Image enriquePortraitBorderImage;
    public Image enriquePortraitImage;
    public Image enriquePortraitWeaponIcon;
    public TextMeshProUGUI enriquePortraitWeaponBadgeText;
    public Button enriquePortraitClickButton;

    public Image equippedWeaponColorIcon;
    public TextMeshProUGUI equippedWeaponNameText;
    public TextMeshProUGUI equippedWeaponStatsText;
    public TextMeshProUGUI equippedWeaponDescText;
    public Button unequipWeaponButton;
    public Image unequipWeaponBorderImage;
    public TextMeshProUGUI unequipWeaponButtonLabel;
    public Button changeWeaponCycleButton;
    public RectTransform weaponsListContainer;

    [Header("Botones Inferiores de Navegación")]
    public Button switchToStatsButton;
    public Button closeMenuButton;

    private readonly List<GameObject> spawnedItemRows = new List<GameObject>();
    private readonly List<GameObject> spawnedWeaponRows = new List<GameObject>();

    private bool wasPlayerMovementAllowedBeforeOpen = true;
    private int openedFrame = -1;

    public bool IsOpen => modalPanelRoot != null && modalPanelRoot.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeOnSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckAndEnsureInActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckAndEnsureInActiveScene();
    }

    public static void CheckAndEnsureInActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid()) return;
        if (activeScene.name.Contains("00_MenuIntro") || activeScene.name.Contains("Intro") || activeScene.name.Contains("Menu"))
            return;
        if (FindAnyObjectByType<PlayerStats>() == null)
            return;

        EnsureInScene();
    }

    public static PlayerInventoryAndWeaponsUI EnsureInScene()
    {
        if (_instance != null)
        {
            _instance.InitializeIfNeeded();
            return _instance;
        }

        PlayerInventoryAndWeaponsUI existing = FindAnyObjectByType<PlayerInventoryAndWeaponsUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            existing.InitializeIfNeeded();
            return existing;
        }

        GameObject go = new GameObject("PlayerInventoryAndWeaponsUI_Canvas");
        PlayerInventoryAndWeaponsUI ui = go.AddComponent<PlayerInventoryAndWeaponsUI>();
        _instance = ui;
        ui.InitializeIfNeeded();
        return ui;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        InitializeIfNeeded();
    }

    private void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnStatsChanged -= RefreshUI;
        }
        if (inventoryManager != null && inventoryManager.onInventoryChanged != null)
        {
            inventoryManager.onInventoryChanged.RemoveListener(RefreshUI);
        }
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void InitializeIfNeeded()
    {
        if (playerStats == null)
            playerStats = FindAnyObjectByType<PlayerStats>();
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance;
        if (battleManager == null)
            battleManager = FindAnyObjectByType<BattleManager>(FindObjectsInactive.Include);
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController2D>();

        FetchPortraitsAndFontIfMissing();

        if (modalPanelRoot == null)
        {
            BuildCompleteUI();
        }
        NormalizeRecipesFooterLayout();
        SetupButtonListeners();

        if (playerStats != null)
        {
            playerStats.OnStatsChanged -= RefreshUI;
            playerStats.OnStatsChanged += RefreshUI;
        }

        if (inventoryManager != null && inventoryManager.onInventoryChanged != null)
        {
            inventoryManager.onInventoryChanged.RemoveListener(RefreshUI);
            inventoryManager.onInventoryChanged.AddListener(RefreshUI);
        }

        RefreshUI();
    }

    /// <summary>
    /// Re-vincula los listeners de los botones estáticos tras construir o cargar el Canvas desde escena.
    /// </summary>
    private void SetupButtonListeners()
    {
        RebindButton(tabObjetosButton, () => SwitchSubMenu(InventorySubMenu.Objetos));
        RebindButton(tabArmasButton, () => SwitchSubMenu(InventorySubMenu.Armas));
        RebindButton(enriquePortraitClickButton, CycleOrEquipWeaponFromPortrait);
        RebindButton(unequipWeaponButton, () => OnEquipWeaponClicked(null));
        RebindButton(changeWeaponCycleButton, CycleOrEquipWeaponFromPortrait);
        RebindButton(switchToStatsButton, SwitchToStatsMenu);
        RebindButton(closeMenuButton, CloseInventoryMenu);
    }

    private static void RebindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void FetchPortraitsAndFontIfMissing()
    {
        if (battleManager != null)
        {
            if (enriquePortrait == null) enriquePortrait = battleManager.enriquePortrait;
            if (corporeoPortrait == null) corporeoPortrait = battleManager.corporeoPortrait;
        }

        if (enriquePortrait == null)
        {
            ExplorationDialogueManager exp = FindAnyObjectByType<ExplorationDialogueManager>(FindObjectsInactive.Include);
            if (exp != null && exp.enriquePortrait != null)
            {
                enriquePortrait = exp.enriquePortrait;
            }
        }

        if (enriquePortrait == null)
        {
            enriquePortrait = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Enrique.png");
        }
        if (corporeoPortrait == null)
        {
            corporeoPortrait = RuntimeAssetCatalog.GetSprite("Assets/Portraits/Musculatura.png");
        }

        if (customFont == null)
        {
            customFont = PlayerHealthBarUI.GetCleanPixelFont();
        }
    }

    private void Update()
    {
        UpdateHudButtonVisibility();

        if (Time.frameCount == openedFrame)
            return;

        if (IsOpen)
        {
            if (WasToggleInventoryKeyPressed() || WasEscapeKeyPressed())
            {
                CloseInventoryMenu();
                return;
            }

            // Atajos de teclado para alternar entre submenús (1/Q: Objetos, 2/E: Armas, Tab: Alternar)
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame || Keyboard.current.qKey.wasPressedThisFrame)
                {
                    SwitchSubMenu(InventorySubMenu.Objetos);
                }
                else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)
                {
                    SwitchSubMenu(InventorySubMenu.Armas);
                }
                else if (Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    SwitchSubMenu(currentSubMenu == InventorySubMenu.Objetos ? InventorySubMenu.Armas : InventorySubMenu.Objetos);
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Q))
            {
                SwitchSubMenu(InventorySubMenu.Objetos);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.E))
            {
                SwitchSubMenu(InventorySubMenu.Armas);
            }
            else if (Input.GetKeyDown(KeyCode.Tab))
            {
                SwitchSubMenu(currentSubMenu == InventorySubMenu.Objetos ? InventorySubMenu.Armas : InventorySubMenu.Objetos);
            }
#endif
            return;
        }

        if (WasToggleInventoryKeyPressed())
        {
            bool inDialogue = (ExplorationDialogueManager.Instance != null && ExplorationDialogueManager.Instance.IsDialogueActive);
            bool inCombat = (battleManager != null && battleManager.state != BattleState.INACTIVE);
            bool statsMenuOpen = (PlayerLevelUpAndStatsUI.Instance != null && PlayerLevelUpAndStatsUI.Instance.IsOpen);

            if (!inDialogue && (!inCombat || battleManager.state == BattleState.PLAYERTURN || battleManager.state == BattleState.DETECTIVETURN))
            {
                if (statsMenuOpen)
                {
                    if (playerStats != null && playerStats.availableSkillPoints > 0)
                        return;
                    PlayerLevelUpAndStatsUI.Instance.OnCloseOrContinueClicked();
                }
                OpenInventoryMenu();
            }
        }
    }

    private void UpdateHudButtonVisibility()
    {
        // Los botones de HUD en pantalla han sido retirados a petición del usuario.
        // El acceso al inventario se realiza con la tecla [I] o desde el menú de combate.
        if (hudTopLeftButtonRoot != null && hudTopLeftButtonRoot.activeSelf)
        {
            hudTopLeftButtonRoot.SetActive(false);
        }

        if (hudButtonRoot != null && hudButtonRoot.activeSelf)
        {
            hudButtonRoot.SetActive(false);
        }
    }

    // =========================================================================
    // NAVEGACIÓN ENTRE SUBMENÚS (OBJETOS / ARMAS)
    // =========================================================================

    public void SwitchSubMenu(InventorySubMenu targetMenu)
    {
        currentSubMenu = targetMenu;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayBotonEnCombateSfx();
        }

        UpdateSubMenuVisuals();
        RefreshUI();
    }

    private void UpdateSubMenuVisuals()
    {
        bool isObjetos = (currentSubMenu == InventorySubMenu.Objetos);

        if (itemsPanelRoot != null) itemsPanelRoot.SetActive(isObjetos);
        if (weaponsPanelRoot != null) weaponsPanelRoot.SetActive(!isObjetos);

        // Actualizar aspecto de las pestañas
        if (tabObjetosBorderImage != null)
            tabObjetosBorderImage.color = isObjetos ? consumableGreenColor : new Color(0.28f, 0.30f, 0.35f, 1f);
        if (tabObjetosBgImage != null)
            tabObjetosBgImage.color = isObjetos ? new Color(0.08f, 0.16f, 0.11f, 1f) : new Color(0.05f, 0.06f, 0.08f, 0.95f);
        if (tabObjetosLabel != null)
            tabObjetosLabel.color = isObjetos ? consumableGreenColor : new Color(0.65f, 0.68f, 0.74f, 1f);

        if (tabArmasBorderImage != null)
            tabArmasBorderImage.color = !isObjetos ? weaponGoldColor : new Color(0.28f, 0.30f, 0.35f, 1f);
        if (tabArmasBgImage != null)
            tabArmasBgImage.color = !isObjetos ? new Color(0.18f, 0.14f, 0.06f, 1f) : new Color(0.05f, 0.06f, 0.08f, 0.95f);
        if (tabArmasLabel != null)
            tabArmasLabel.color = !isObjetos ? weaponGoldColor : new Color(0.65f, 0.68f, 0.74f, 1f);
    }

    // =========================================================================
    // APERTURA Y CIERRE DEL MENÚ DE INVENTARIO Y ARMAS
    // =========================================================================

    public void ToggleInventoryMenu()
    {
        if (IsOpen)
            CloseInventoryMenu();
        else
            OpenInventoryMenu();
    }

    public void OpenInventoryMenu()
    {
        InitializeIfNeeded();
        if (IsOpen) return;

        openedFrame = Time.frameCount;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
        }

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController2D>();

        bool inCombat = (battleManager != null && battleManager.state != BattleState.INACTIVE);
        if (!inCombat && playerController != null)
        {
            wasPlayerMovementAllowedBeforeOpen = playerController.canMove;
            playerController.SetCanMove(false);
        }

        modalPanelRoot.SetActive(true);
        UpdateSubMenuVisuals();
        RefreshUI();
    }

    public void CloseInventoryMenu()
    {
        if (!IsOpen) return;

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayBotonConfirmacionDialogoSfx();
        }

        modalPanelRoot.SetActive(false);

        bool inCombat = (battleManager != null && battleManager.state != BattleState.INACTIVE);
        if (!inCombat && playerController != null && wasPlayerMovementAllowedBeforeOpen)
        {
            playerController.SetCanMove(true);
        }

        if (inCombat && battleManager != null)
        {
            battleManager.UpdateHealthUI();
        }
    }

    public void SwitchToStatsMenu()
    {
        CloseInventoryMenu();
        PlayerLevelUpAndStatsUI statsUI = PlayerLevelUpAndStatsUI.EnsureInScene();
        if (statsUI != null)
        {
            statsUI.OpenStatsMenu();
        }
    }

    // =========================================================================
    // ACCIONES DE USO DE OBJETOS Y EQUIPAMIENTO DE ARMAS
    // =========================================================================

    private void OnUseItemClicked(ItemData item)
    {
        if (item == null) return;
        if (inventoryManager == null) inventoryManager = InventoryManager.Instance;
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (inventoryManager == null || !inventoryManager.HasItem(item, 1)) return;

        bool inCombat = (battleManager != null && battleManager.state != BattleState.INACTIVE);

        if (inCombat)
        {
            if (battleManager.state == BattleState.PLAYERTURN || battleManager.state == BattleState.DETECTIVETURN)
            {
                bool targetDet = (battleManager.state == BattleState.DETECTIVETURN);
                modalPanelRoot.SetActive(false);
                battleManager.UseConsumableInCombat(item, targetDet);
            }
            return;
        }

        bool isHealingItem = (item.category == ItemCategory.ConsumibleCuracion || item.healAmount > 0);
        if (isHealingItem && playerStats != null)
        {
            if (playerStats.currentHP >= playerStats.maxHP)
            {
                if (MusicManager.Instance != null)
                    MusicManager.Instance.PlayBotonEnCombateSfx();

                if (feedbackBannerText != null)
                {
                    feedbackBannerText.text = $"Tu salud ya está al máximo ({playerStats.currentHP}/{playerStats.maxHP} HP). Guarda \"{item.itemName}\" para cuando estés herido.";
                    feedbackBannerText.color = weaponGoldColor;
                }
                return;
            }

            int healBonus = playerStats.GetIntelectoHealBonus();
            int totalHeal = item.healAmount + healBonus;

            inventoryManager.RemoveItem(item, 1);
            playerStats.Heal(totalHeal);

            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayCurarseSfx();
            }

            string bonusNote = healBonus > 0 ? $" (+{healBonus} por Intelecto)" : "";
            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = $"¡USASTE {item.itemName.ToUpper()}! Recuperaste +{totalHeal} HP{bonusNote}. Salud actual: {playerStats.currentHP}/{playerStats.maxHP} HP.";
                feedbackBannerText.color = consumableGreenColor;
            }

            RefreshUI();
        }
        else
        {
            if (MusicManager.Instance != null)
                MusicManager.Instance.PlayBotonEnCombateSfx();

            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = $"\"{item.itemName}\" es un objeto ofensivo/material que se utiliza durante los combates.";
                feedbackBannerText.color = materialCyanColor;
            }
        }
    }

    private void OnQuickCraftHealRecipeClicked(CraftingRecipeData healRecipe)
    {
        if (healRecipe == null) return;
        if (inventoryManager == null) inventoryManager = InventoryManager.Instance;
        if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        if (inventoryManager == null || !inventoryManager.CanCraft(healRecipe)) return;

        bool inCombat = (battleManager != null && battleManager.state != BattleState.INACTIVE);
        if (inCombat)
        {
            if (battleManager.state == BattleState.PLAYERTURN || battleManager.state == BattleState.DETECTIVETURN)
            {
                bool targetDet = (battleManager.state == BattleState.DETECTIVETURN);
                modalPanelRoot.SetActive(false);
                battleManager.CraftAndUseRecipeInCombat(healRecipe, targetDet);
            }
            return;
        }

        if (playerStats != null && playerStats.currentHP >= playerStats.maxHP)
        {
            if (MusicManager.Instance != null)
                MusicManager.Instance.PlayBotonEnCombateSfx();

            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = $"Tu salud ya está al máximo ({playerStats.currentHP}/{playerStats.maxHP} HP). No necesitas gastar materiales en {healRecipe.recipeName} ahora.";
                feedbackBannerText.color = weaponGoldColor;
            }
            return;
        }

        if (inventoryManager.ConsumeRecipeMaterials(healRecipe))
        {
            int healBonus = (playerStats != null) ? playerStats.GetIntelectoHealBonus() : 0;
            int totalHeal = healRecipe.healAmount + healBonus;

            if (playerStats != null)
            {
                playerStats.Heal(totalHeal);
            }

            string extraMsg = "";
            if (healRecipe.detectiveAmmoRestored > 0 && DetectiveStats.Instance != null)
            {
                int added = DetectiveStats.Instance.ReloadAmmo(healRecipe.detectiveAmmoRestored);
                extraMsg = $" (+{added} Balas .38 para el Detective)";
            }

            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayCurarseSfx();
            }

            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = $"¡INTELECTO FABRICÓ Y USÓ {healRecipe.recipeName.ToUpper()}! Recuperaste +{totalHeal} HP{extraMsg}.";
                feedbackBannerText.color = consumableGreenColor;
            }

            RefreshUI();
        }
    }

    public void CycleOrEquipWeaponFromPortrait()
    {
        if (inventoryManager == null) inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null) return;

        inventoryManager.EnsurePreparedWeaponsLoaded();

        List<ItemData> available = new List<ItemData>();
        available.Add(null); // Puños desnudos

        if (inventoryManager.unlockedWeapons != null)
        {
            foreach (ItemData w in inventoryManager.unlockedWeapons)
            {
                if (w != null && !available.Contains(w)) available.Add(w);
            }
        }

        if (available.Count <= 1)
        {
            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = "¡NO TIENES OTRAS ARMAS EN TU EQUIPAMIENTO! Explora Santa Felipina para encontrar armas.";
                feedbackBannerText.color = new Color(0.95f, 0.75f, 0.35f, 1f);
            }
            return;
        }

        int currentIndex = available.IndexOf(inventoryManager.equippedWeapon);
        int nextIndex = (currentIndex + 1) % available.Count;
        ItemData nextWeapon = available[nextIndex];

        OnEquipWeaponClicked(nextWeapon);
    }

    public void OnEquipWeaponClicked(ItemData weapon)
    {
        if (inventoryManager == null) inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null) return;

        inventoryManager.EquipWeapon(weapon);

        if (playerStats != null)
        {
            playerStats.NotifyEquipmentChanged();
        }

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayBotonEnCombateSfx();
        }

        if (feedbackBannerText != null)
        {
            if (weapon != null)
            {
                string motPart = weapon.weaponMotricidadBonus != 0
                    ? $", {(weapon.weaponMotricidadBonus > 0 ? "+" : "")}{weapon.weaponMotricidadBonus} Motricidad"
                    : "";
                feedbackBannerText.text = $"¡ARMA EQUIPADA EN EL RETRATO DE ENRIQUE: {weapon.itemName.ToUpper()} (+{weapon.weaponAttackBonus} Daño Físico{motPart})!";
                feedbackBannerText.color = weaponGoldColor;
            }
            else
            {
                feedbackBannerText.text = "Has desequipado el arma. Enrique ahora pelea a Puños Desnudos en combate.";
                feedbackBannerText.color = new Color(0.80f, 0.82f, 0.86f, 1f);
            }
        }

        RefreshUI();

        if (battleManager != null && battleManager.battlePanel != null && battleManager.battlePanel.activeInHierarchy)
        {
            battleManager.UpdateHealthUI();
        }
    }

    // =========================================================================
    // REFRESCO DE INTERFAZ (HUD, OBJETOS, RETRATO Y ARMAS)
    // =========================================================================

    public void RefreshUI()
    {
        if (playerStats == null)
            playerStats = FindAnyObjectByType<PlayerStats>();
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance;

        FetchPortraitsAndFontIfMissing();

        // 1. Botones HUD de Exploración: Solamente la palabra "INVENTARIO"
        if (hudTopLeftButtonLabel != null)
        {
            hudTopLeftButtonLabel.text = "INVENTARIO";
        }
        if (hudButtonLabel != null)
        {
            hudButtonLabel.text = "INVENTARIO";
        }

        if (!IsOpen) return;

        // 2. Cabecera del Modal y Estadísticas Generales
        if (subHeaderStatsText != null && playerStats != null)
        {
            int baseCorp = playerStats.corporeo;
            int weaponAtk = playerStats.GetEquippedWeaponAttackBonus();
            int totalAtk = playerStats.GetTotalPhysicalAttack();
            int healBonus = playerStats.GetIntelectoHealBonus();

            string atkBreakdown = weaponAtk > 0
                ? $"<color=#FF6666>{totalAtk} ATQ</color> ({baseCorp} Corpóreo <color=#FFD84D>+{weaponAtk} Arma</color>)"
                : $"<color=#FF6666>{totalAtk} ATQ</color> (Sin bono de arma)";

            subHeaderStatsText.text =
                $"SALUD: <color=#52E06B>{playerStats.currentHP} / {playerStats.maxHP} HP</color>   |   " +
                $"DAÑO FÍSICO: {atkBreakdown}   |   " +
                $"BONO INTELECTO: <color=#4DB8FF>+{healBonus} Curación/Daño</color>";
        }

        if (currentSubMenu == InventorySubMenu.Objetos)
        {
            RefreshItemsColumn();
        }
        else
        {
            RefreshWeaponsColumn();
        }
    }

    private void RefreshItemsColumn()
    {
        ClearSpawnedList(spawnedItemRows);

        List<InventorySlot> slots = (inventoryManager != null)
            ? inventoryManager.GetAllNonWeaponItems()
            : new List<InventorySlot>();

        if (itemsSectionCounterText != null)
        {
            int totalUnits = (inventoryManager != null) ? inventoryManager.GetTotalItemUnitsCount() : 0;
            itemsSectionCounterText.text = $"MOCHILA DE ENRIQUE ({slots.Count} tipos de objetos / {totalUnits} unidades totales)";
        }

        int healBonus = (playerStats != null) ? playerStats.GetIntelectoHealBonus() : 0;
        int dmgBonus = (playerStats != null) ? playerStats.GetIntelectoDamageBonus() : 0;
        bool inCombat = (battleManager != null && battleManager.state != BattleState.INACTIVE);

        if (slots.Count == 0)
        {
            GameObject emptyCard = CreatePanel("EmptyInventoryRow", itemsListContainer, new Vector2(0.02f, 0.35f), new Vector2(0.98f, 0.65f), new Color(0.22f, 0.24f, 0.28f, 1f));
            GameObject inner = CreatePanel("Inner", emptyCard.transform, new Vector2(0.005f, 0.04f), new Vector2(0.995f, 0.96f), new Color(0.06f, 0.07f, 0.09f, 1f));
            TextMeshProUGUI emptyTxt = CreateText("EmptyText", inner.transform,
                "TU INVENTARIO DE OBJETOS ESTÁ VACÍO\n<size=18><color=#9AA0AC>Explora las calles de Santa Felipina y presiona [E] cerca de objetos brillantes para recoger Refrescos, Tortas, Gasolina, Botellas y Cinta Canela.</color></size>",
                22, TextAlignmentOptions.Center, new Color(0.75f, 0.78f, 0.82f, 1f));
            SetAnchors(emptyTxt.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            spawnedItemRows.Add(emptyCard);
        }
        else
        {
            int maxRows = Mathf.Max(4, slots.Count);
            float rowHeight = 1.0f / maxRows;
            float pad = 0.015f;

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot == null || slot.item == null) continue;

                float topY = 1.0f - (i * rowHeight) - pad;
                float bottomY = 1.0f - ((i + 1) * rowHeight) + pad;

                GameObject rowGO = BuildItemRow(slot, i, new Vector2(0.015f, Mathf.Clamp01(bottomY)), new Vector2(0.985f, Mathf.Clamp01(topY)), healBonus, dmgBonus, inCombat);
                spawnedItemRows.Add(rowGO);
            }
        }

        UpdateRecipesFooter(healBonus, dmgBonus);
    }

    private GameObject BuildItemRow(InventorySlot slot, int index, Vector2 anchorMin, Vector2 anchorMax, int healBonus, int dmgBonus, bool inCombat)
    {
        ItemData item = slot.item;
        bool isHealing = (item.category == ItemCategory.ConsumibleCuracion || item.healAmount > 0);
        bool isOffensive = (item.category == ItemCategory.ObjetoOfensivoDirecto && !isHealing);

        Color categoryColor = isHealing
            ? consumableGreenColor
            : (isOffensive ? offensiveOrangeColor : materialCyanColor);

        string categoryBadge = isHealing
            ? "CONSUMIBLE CURATIVO"
            : (isOffensive ? "OBJETO OFENSIVO" : "MATERIAL DE CRAFTEO");

        string effectSummary;
        if (isHealing)
        {
            int totalHeal = item.healAmount + healBonus;
            string bonusTag = healBonus > 0 ? $" ({item.healAmount} + {healBonus} Intelecto)" : "";
            effectSummary = $"<color=#52E06B>Efecto: Recupera +{totalHeal} HP{bonusTag}</color>";
        }
        else if (isOffensive)
        {
            int totalDmg = item.damageAmount + dmgBonus;
            effectSummary = $"<color=#FF884D>Efecto: Inflige {totalDmg} de daño directo en combate</color>";
        }
        else
        {
            effectSummary = "<color=#66CCFF>Efecto: Se combina con [INTELECTO] para fabricar Molotov o Vendaje</color>";
        }

        GameObject rowBorder = CreatePanel($"ItemRow_{index}", itemsListContainer, anchorMin, anchorMax, categoryColor);
        GameObject rowInner = CreatePanel("Inner", rowBorder.transform, new Vector2(0.003f, 0.03f), new Vector2(0.997f, 0.97f), new Color(0.07f, 0.08f, 0.10f, 1f));

        // 1. Icono del objeto a la izquierda
        GameObject iconFrame = CreatePanel("IconFrame", rowInner.transform, new Vector2(0.012f, 0.10f), new Vector2(0.085f, 0.90f), new Color(0.18f, 0.20f, 0.24f, 1f));
        GameObject iconColorBox = CreatePanel("IconColor", iconFrame.transform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), item.placeholderColor);
        Image iconImg = iconColorBox.GetComponent<Image>();
        if (item.icon != null)
        {
            iconImg.sprite = item.icon;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
        }

        TextMeshProUGUI qtyOverlay = CreateText("QtyText", iconColorBox.transform, $"x{slot.quantity}", 19, TextAlignmentOptions.BottomRight, Color.white);
        qtyOverlay.fontStyle = FontStyles.Bold;
        SetAnchors(qtyOverlay.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));

        // 2. Nombre del objeto y Categoría
        TextMeshProUGUI titleTxt = CreateText("TitleText", rowInner.transform,
            $"<b>{item.itemName.ToUpper()}</b>  <size=16><color=#B0B6C2>[{categoryBadge}]</color></size>",
            22, TextAlignmentOptions.Left, Color.white);
        SetAnchors(titleTxt.rectTransform, new Vector2(0.10f, 0.54f), new Vector2(0.74f, 0.94f));

        // 3. Descripción y Resumen de Efecto (espacioso y sin traslape)
        TextMeshProUGUI descTxt = CreateText("DescText", rowInner.transform,
            $"{item.description}\n{effectSummary}",
            16, TextAlignmentOptions.Left, new Color(0.84f, 0.86f, 0.90f, 1f));
        SetAnchors(descTxt.rectTransform, new Vector2(0.10f, 0.06f), new Vector2(0.74f, 0.54f));

        // 4. Botón de Acción a la derecha
        bool canUseNow = isHealing || (isOffensive && inCombat);
        Color btnBorderCol = canUseNow ? categoryColor : new Color(0.26f, 0.28f, 0.32f, 1f);

        GameObject useBtnBorder = CreatePanel("UseItemButton", rowInner.transform, new Vector2(0.76f, 0.14f), new Vector2(0.988f, 0.86f), btnBorderCol);
        GameObject useBtnInner = CreatePanel("Inner", useBtnBorder.transform, new Vector2(0.015f, 0.06f), new Vector2(0.985f, 0.94f), new Color(0.09f, 0.11f, 0.14f, 1f));

        Button useBtn = useBtnBorder.AddComponent<Button>();
        useBtn.targetGraphic = useBtnInner.GetComponent<Image>();
        useBtn.interactable = canUseNow;

        ItemData capturedItem = item;
        useBtn.onClick.AddListener(() => OnUseItemClicked(capturedItem));

        string btnLabelStr;
        if (isHealing)
        {
            int totalHeal = item.healAmount + healBonus;
            btnLabelStr = $"USAR OBJETO\n<size=15>(+{totalHeal} HP)</size>";
        }
        else if (isOffensive)
        {
            btnLabelStr = inCombat ? "LANZAR AHORA" : "SOLO EN\nCOMBATE";
        }
        else
        {
            btnLabelStr = "MATERIAL\nINTELECTO";
        }

        TextMeshProUGUI btnTxt = CreateText("UseBtnLabel", useBtnInner.transform, btnLabelStr, 17, TextAlignmentOptions.Center,
            canUseNow ? categoryColor : new Color(0.55f, 0.58f, 0.62f, 1f));
        btnTxt.fontStyle = FontStyles.Bold;
        SetAnchors(btnTxt.rectTransform, Vector2.zero, Vector2.one);

        return rowBorder;
    }

    private void UpdateRecipesFooter(int healBonus, int dmgBonus)
    {
        if (recipesStatusText == null) return;

        CraftingRecipeData healRecipe = null;
        List<string> recipeLines = new List<string>();

        if (inventoryManager != null)
        {
            inventoryManager.EnsureKnownRecipesLoaded();
        }
        if (inventoryManager != null && inventoryManager.knownRecipes != null)
        {
            HashSet<string> seenRecipeKeys = new HashSet<string>();
            foreach (CraftingRecipeData r in inventoryManager.knownRecipes)
            {
                if (r == null || string.IsNullOrEmpty(r.recipeName)) continue;
                string recipeKey = InventoryManager.GetCanonicalItemKey(r.recipeName);
                if (!seenRecipeKeys.Add(recipeKey)) continue;
                bool ready = inventoryManager.CanCraft(r);
                string matA = r.materialA != null ? r.materialA.itemName : "?";
                string matB = r.materialB != null ? r.materialB.itemName : "?";
                string statusTag = ready ? "<color=#52E06B>[LISTA]</color>" : "<color=#888E99>[FALTAN MATERIALES]</color>";

                if (r.healAmount > 0 && (healRecipe == null || (!inventoryManager.CanCraft(healRecipe) && ready)))
                {
                    healRecipe = r;
                }

                string effect = r.GetCompactEffectTag(healBonus, dmgBonus).Trim();

                recipeLines.Add($"• <b>{r.recipeName}</b> ({matA} + {matB} -> {effect}) {statusTag}");
            }
        }

        recipesStatusText.text = recipeLines.Count > 0
            ? "<b>RECETAS DE INTELECTO:</b>\n" + string.Join("\n", recipeLines)
            : "<b>RECETAS DE INTELECTO:</b> Combina Botella Vacía + Gasolina (Molotov) o Cinta Canela + Botella (Vendaje Rudo).";

        if (quickCraftHealRecipeButton != null && quickCraftHealButtonLabel != null)
        {
            bool canCraftHeal = (healRecipe != null && inventoryManager != null && inventoryManager.CanCraft(healRecipe));
            quickCraftHealRecipeButton.interactable = canCraftHeal;
            quickCraftHealRecipeButton.onClick.RemoveAllListeners();

            if (healRecipe != null)
            {
                CraftingRecipeData captured = healRecipe;
                quickCraftHealRecipeButton.onClick.AddListener(() => OnQuickCraftHealRecipeClicked(captured));
                int totalHeal = healRecipe.healAmount + healBonus;
                quickCraftHealButtonLabel.text = canCraftHeal
                    ? $"FABRICAR Y USAR\n{healRecipe.recipeName.ToUpper()} (+{totalHeal} HP)"
                    : $"{healRecipe.recipeName.ToUpper()}\n(Faltan materiales)";
            }
            else
            {
                quickCraftHealButtonLabel.text = "VENDAJE RUDO\n(Sin materiales)";
            }

            quickCraftHealButtonLabel.color = canCraftHeal ? consumableGreenColor : new Color(0.50f, 0.52f, 0.56f, 1f);
            if (quickCraftHealBorderImage != null)
            {
                quickCraftHealBorderImage.color = canCraftHeal ? consumableGreenColor : new Color(0.25f, 0.27f, 0.30f, 1f);
            }
        }
    }

    private void RefreshWeaponsColumn()
    {
        ClearSpawnedList(spawnedWeaponRows);

        if (inventoryManager != null)
        {
            inventoryManager.EnsurePreparedWeaponsLoaded();
        }

        ItemData equipped = (inventoryManager != null) ? inventoryManager.equippedWeapon : null;
        int baseCorp = (playerStats != null) ? playerStats.corporeo : 12;
        int baseMot = (playerStats != null) ? playerStats.motricidad : 12;

        // 1. Actualizar el Retrato de Enrique en la tarjeta de equipamiento
        if (enriquePortraitImage != null)
        {
            if (enriquePortrait != null)
            {
                enriquePortraitImage.sprite = enriquePortrait;
                enriquePortraitImage.color = Color.white;
                enriquePortraitImage.preserveAspect = false;
            }
            else
            {
                enriquePortraitImage.sprite = null;
                enriquePortraitImage.color = new Color(0.35f, 0.38f, 0.44f, 1f);
            }
        }

        if (enriquePortraitBorderImage != null)
        {
            enriquePortraitBorderImage.color = (equipped != null) ? weaponGoldColor : new Color(0.35f, 0.40f, 0.48f, 1f);
        }

        // Overlay sobre el retrato de Enrique (Icono de arma y Badge)
        if (enriquePortraitWeaponIcon != null)
        {
            if (equipped != null && equipped.icon != null)
            {
                enriquePortraitWeaponIcon.gameObject.SetActive(true);
                enriquePortraitWeaponIcon.sprite = equipped.icon;
                enriquePortraitWeaponIcon.color = Color.white;
                enriquePortraitWeaponIcon.preserveAspect = true;
            }
            else
            {
                enriquePortraitWeaponIcon.gameObject.SetActive(false);
            }
        }

        if (enriquePortraitWeaponBadgeText != null)
        {
            if (equipped != null)
            {
                enriquePortraitWeaponBadgeText.text = $"<b>{equipped.itemName.ToUpper()}</b>\n<color=#66FF88>+{equipped.weaponAttackBonus} ATQ</color> <color=#FFD84D>[CLIC: CAMBIAR]</color>";
                enriquePortraitWeaponBadgeText.color = Color.white;
            }
            else
            {
                enriquePortraitWeaponBadgeText.text = "<b>PUÑOS DESNUDOS</b>\n<color=#B0B6C2>12 ATQ</color> <color=#66CCFF>[CLIC: EQUIPAR]</color>";
                enriquePortraitWeaponBadgeText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            }
        }

        // 2. Textos informativos de equipamiento junto al retrato con amplio espacio
        if (equipped != null)
        {
            if (equippedWeaponNameText != null)
            {
                equippedWeaponNameText.text = $"ARMA EQUIPADA: <color=#FFD84D>{equipped.itemName.ToUpper()}</color>";
            }
            if (equippedWeaponStatsText != null)
            {
                string motBonusStr = equipped.weaponMotricidadBonus != 0
                    ? $"   |   Velocidad Motricidad: <b>{baseMot + equipped.weaponMotricidadBonus}</b> ({(equipped.weaponMotricidadBonus > 0 ? "+" : "")}{equipped.weaponMotricidadBonus})"
                    : "   |   Velocidad Motricidad: <b>" + baseMot + "</b> (+0)";
                equippedWeaponStatsText.text =
                    $"Bono de Daño Físico: <color=#66FF88>+{equipped.weaponAttackBonus} ATQ</color>   |   " +
                    $"Daño Corpóreo Total: <color=#FF6666><b>{baseCorp + equipped.weaponAttackBonus} pts</b></color>{motBonusStr}";
            }
            if (equippedWeaponDescText != null)
            {
                equippedWeaponDescText.text = equipped.description;
            }
            if (unequipWeaponButton != null)
            {
                unequipWeaponButton.interactable = true;
            }
            if (unequipWeaponBorderImage != null)
            {
                unequipWeaponBorderImage.color = new Color(0.88f, 0.36f, 0.32f, 1f);
            }
            if (unequipWeaponButtonLabel != null)
            {
                unequipWeaponButtonLabel.text = "DESEQUIPAR ARMA";
                unequipWeaponButtonLabel.color = new Color(1f, 0.55f, 0.50f, 1f);
            }
        }
        else
        {
            if (equippedWeaponNameText != null)
            {
                equippedWeaponNameText.text = "ARMA EQUIPADA: <color=#D0D4DC>PUÑOS DESNUDOS (SIN ARMA)</color>";
            }
            if (equippedWeaponStatsText != null)
            {
                equippedWeaponStatsText.text = $"Bono de Daño: +0 ATQ   |   Daño Corpóreo Base: <color=#FF6666><b>{baseCorp} pts</b></color>   |   Velocidad Motricidad: <b>{baseMot}</b>";
            }
            if (equippedWeaponDescText != null)
            {
                equippedWeaponDescText.text = "Enrique pelea a puño limpio en las calles de Santa Felipina. Haz clic en el Retrato de Enrique o elige un arma en el catálogo inferior para equiparla.";
            }
            if (unequipWeaponButton != null)
            {
                unequipWeaponButton.interactable = false;
            }
            if (unequipWeaponBorderImage != null)
            {
                unequipWeaponBorderImage.color = new Color(0.24f, 0.25f, 0.28f, 1f);
            }
            if (unequipWeaponButtonLabel != null)
            {
                unequipWeaponButtonLabel.text = "USANDO PUÑOS";
                unequipWeaponButtonLabel.color = new Color(0.48f, 0.50f, 0.54f, 1f);
            }
        }

        // 3. Construir catálogo de armas en el contenedor inferior
        List<ItemData> weaponEntries = new List<ItemData>();
        weaponEntries.Add(null); // Opción Puños Desnudos

        if (inventoryManager != null && inventoryManager.unlockedWeapons != null)
        {
            foreach (ItemData w in inventoryManager.unlockedWeapons)
            {
                if (w != null && !weaponEntries.Contains(w))
                    weaponEntries.Add(w);
            }
        }

        bool hasNoWeaponsFound = (inventoryManager == null || inventoryManager.unlockedWeapons == null || inventoryManager.unlockedWeapons.Count == 0);

        if (hasNoWeaponsFound)
        {
            // Fila 0: Puños Desnudos en la parte superior
            GameObject fistsRow = BuildWeaponRow(null, 0, new Vector2(0.015f, 0.58f), new Vector2(0.985f, 0.98f), equipped);
            spawnedWeaponRows.Add(fistsRow);

            // Tarjeta informativa inferior indicando que explore para encontrar armas
            GameObject infoBorder = CreatePanel("NoWeaponsInfoCard", weaponsListContainer, new Vector2(0.015f, 0.05f), new Vector2(0.985f, 0.52f), new Color(0.35f, 0.38f, 0.44f, 1f));
            GameObject infoInner = CreatePanel("Inner", infoBorder.transform, new Vector2(0.003f, 0.03f), new Vector2(0.997f, 0.97f), new Color(0.08f, 0.09f, 0.12f, 1f));
            TextMeshProUGUI infoTxt = CreateText("InfoText", infoInner.transform,
                "<b>EXPLORA LAS CALLES DE SANTA FELIPINA PARA ENCONTRAR ARMAS</b>\n" +
                "<size=17><color=#D4DAE4>No tienes armas recogidas aún. Recorre las banquetas, postes, callejones y cuartos secretos para encontrar:</color></size>\n" +
                "<size=16><color=#66FF88>• Cadena de Bicicleta</color> (+4 ATQ, +2 Motricidad)   |   <color=#66CCFF>• Tubo de Plomo</color> (+6 ATQ)   |   <color=#FFD84D>• Bat Reforzado</color> (+9 ATQ)\n" +
                "<color=#9AA0AC>Al acercarte y presionar [E], el arma se agregará automáticamente aquí y podrás equiparla en el retrato de Enrique.</color></size>",
                20, TextAlignmentOptions.Center, new Color(0.88f, 0.90f, 0.94f, 1f));
            SetAnchors(infoTxt.rectTransform, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f));
            spawnedWeaponRows.Add(infoBorder);
        }
        else
        {
            int totalRows = Mathf.Max(4, weaponEntries.Count);
            float rowHeight = 1.0f / totalRows;
            float pad = 0.015f;

            for (int i = 0; i < weaponEntries.Count; i++)
            {
                float topY = 1.0f - (i * rowHeight) - pad;
                float bottomY = 1.0f - ((i + 1) * rowHeight) + pad;

                GameObject wRow = BuildWeaponRow(weaponEntries[i], i, new Vector2(0.015f, Mathf.Clamp01(bottomY)), new Vector2(0.985f, Mathf.Clamp01(topY)), equipped);
                spawnedWeaponRows.Add(wRow);
            }
        }
    }

    private GameObject BuildWeaponRow(ItemData weapon, int index, Vector2 anchorMin, Vector2 anchorMax, ItemData currentlyEquipped)
    {
        bool isThisEquipped = (weapon == currentlyEquipped);

        string wName = (weapon != null) ? weapon.itemName.ToUpper() : "PUÑOS DESNUDOS (ESTÁNDAR)";
        string wDesc = (weapon != null) ? weapon.description : "Pelea callejera sin armas adicionales. Sin penalización ni bonificación.";
        Color swatchCol = (weapon != null) ? weapon.placeholderColor : new Color(0.50f, 0.52f, 0.56f, 1f);

        string bonusText;
        if (weapon == null)
        {
            bonusText = "<color=#B0B6C2>Bono Daño: +0 ATQ  |  Bono Motricidad: +0</color>";
        }
        else
        {
            string motPart = weapon.weaponMotricidadBonus != 0
                ? $"  |  Bono Motricidad: <color={(weapon.weaponMotricidadBonus > 0 ? "#66FF88" : "#FF7777")}>{(weapon.weaponMotricidadBonus > 0 ? "+" : "")}{weapon.weaponMotricidadBonus}</color>"
                : "  |  Bono Motricidad: +0";
            bonusText = $"<color=#FFD84D>Bono Daño Físico Corpóreo: +{weapon.weaponAttackBonus} ATQ</color>{motPart}";
        }

        Color borderCol = isThisEquipped ? weaponGoldColor : new Color(0.32f, 0.35f, 0.42f, 1f);

        GameObject rowBorder = CreatePanel($"WeaponRow_{index}", weaponsListContainer, anchorMin, anchorMax, borderCol);
        GameObject rowInner = CreatePanel("Inner", rowBorder.transform, new Vector2(0.003f, 0.04f), new Vector2(0.997f, 0.96f),
            isThisEquipped ? new Color(0.12f, 0.11f, 0.07f, 1f) : new Color(0.07f, 0.08f, 0.10f, 1f));

        // 1. Icono del arma a la izquierda
        GameObject iconFrame = CreatePanel("WeaponIconFrame", rowInner.transform, new Vector2(0.012f, 0.12f), new Vector2(0.085f, 0.88f), new Color(0.20f, 0.22f, 0.26f, 1f));
        GameObject iconBox = CreatePanel("WeaponIcon", iconFrame.transform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), swatchCol);
        if (weapon != null && weapon.icon != null)
        {
            Image img = iconBox.GetComponent<Image>();
            img.sprite = weapon.icon;
            img.color = Color.white;
            img.preserveAspect = true;
        }

        // 2. Título y Bonos
        string equippedTag = isThisEquipped ? "  <color=#52E06B>[EQUIPADA EN EL RETRATO]</color>" : "";
        TextMeshProUGUI titleTxt = CreateText("WeaponTitle", rowInner.transform, $"<b>{wName}</b>{equippedTag}", 21, TextAlignmentOptions.Left,
            isThisEquipped ? weaponGoldColor : Color.white);
        SetAnchors(titleTxt.rectTransform, new Vector2(0.10f, 0.54f), new Vector2(0.76f, 0.94f));

        // 3. Descripción y bonificaciones (amplio y sin traslape)
        TextMeshProUGUI descTxt = CreateText("WeaponDesc", rowInner.transform, $"{bonusText}\n<color=#B8BEC8>{wDesc}</color>", 16, TextAlignmentOptions.Left, Color.white);
        SetAnchors(descTxt.rectTransform, new Vector2(0.10f, 0.06f), new Vector2(0.76f, 0.54f));

        // 4. Botón Equipar a la derecha
        Color btnBorderColor = isThisEquipped ? new Color(0.24f, 0.82f, 0.42f, 1f) : weaponGoldColor;
        GameObject equipBtnBorder = CreatePanel("EquipButton", rowInner.transform, new Vector2(0.78f, 0.16f), new Vector2(0.988f, 0.84f), btnBorderColor);
        GameObject equipBtnInner = CreatePanel("Inner", equipBtnBorder.transform, new Vector2(0.015f, 0.06f), new Vector2(0.985f, 0.94f), new Color(0.09f, 0.10f, 0.13f, 1f));

        Button equipBtn = equipBtnBorder.AddComponent<Button>();
        equipBtn.targetGraphic = equipBtnInner.GetComponent<Image>();
        equipBtn.interactable = !isThisEquipped;

        ItemData capturedWeapon = weapon;
        equipBtn.onClick.AddListener(() => OnEquipWeaponClicked(capturedWeapon));

        string btnLabel = isThisEquipped ? "✓ EN USO" : "EQUIPAR ARMA";
        TextMeshProUGUI btnTxt = CreateText("EquipLabel", equipBtnInner.transform, btnLabel, 17, TextAlignmentOptions.Center,
            isThisEquipped ? new Color(0.35f, 0.92f, 0.52f, 1f) : weaponGoldColor);
        btnTxt.fontStyle = FontStyles.Bold;
        SetAnchors(btnTxt.rectTransform, Vector2.zero, Vector2.one);

        return rowBorder;
    }

    private void ClearSpawnedList(List<GameObject> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(list[i]);
                else
                    DestroyImmediate(list[i]);
            }
        }
        list.Clear();
    }

    // =========================================================================
    // CONSTRUCCIÓN PROGRAMÁTICA DEL CANVAS Y SUBMENÚS (OBJETOS / ARMAS)
    // =========================================================================

    private void BuildCompleteUI()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 505;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // Los botones HUD flotantes en pantalla se han retirado a petición del usuario
        // para mantener las escenas limpias (el acceso al inventario se realiza con la tecla [I] o desde el menú de combate).
        if (hudTopLeftButtonRoot != null)
        {
            Destroy(hudTopLeftButtonRoot);
            hudTopLeftButtonRoot = null;
        }
        if (hudButtonRoot != null)
        {
            Destroy(hudButtonRoot);
            hudButtonRoot = null;
        }

        // 3. Panel Modal Principal
        modalPanelRoot = CreatePanel("InventoryAndWeaponsModal", transform, Vector2.zero, Vector2.one, new Color(0.01f, 0.01f, 0.02f, 0.94f));

        GameObject outerFrame = CreatePanel("OuterFrame", modalPanelRoot.transform, new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.975f), new Color(0.45f, 0.48f, 0.55f, 1f));
        GameObject mainBox = CreatePanel("MainBlackBox", outerFrame.transform, new Vector2(0.003f, 0.005f), new Vector2(0.997f, 0.995f), new Color(0.05f, 0.05f, 0.07f, 1f));

        // Cabecera Principal y Estadísticas
        headerTitleText = CreateText("HeaderTitleText", mainBox.transform, "INVENTARIO DE ENRIQUE BOSQUES", 34, TextAlignmentOptions.Center, weaponGoldColor);
        headerTitleText.fontStyle = FontStyles.Bold;
        SetAnchors(headerTitleText.rectTransform, new Vector2(0.03f, 0.925f), new Vector2(0.97f, 0.985f));

        subHeaderStatsText = CreateText("SubHeaderStatsText", mainBox.transform, "SALUD: 100 / 100 HP  |  DAÑO FÍSICO: 12 ATQ", 21, TextAlignmentOptions.Center, Color.white);
        SetAnchors(subHeaderStatsText.rectTransform, new Vector2(0.03f, 0.870f), new Vector2(0.97f, 0.920f));

        // =====================================================================
        // BARRA DE PESTAÑAS DE SUBMENÚS (OBJETOS / ARMAS)
        // =====================================================================
        GameObject subMenuTabsRoot = CreatePanel("SubMenuTabsBar", mainBox.transform, new Vector2(0.06f, 0.795f), new Vector2(0.94f, 0.855f), new Color(0f, 0f, 0f, 0f));

        // Pestaña 1: OBJETOS
        GameObject tabObjBorder = CreatePanel("Tab_Objetos", subMenuTabsRoot.transform, new Vector2(0f, 0f), new Vector2(0.488f, 1f), consumableGreenColor);
        tabObjetosBorderImage = tabObjBorder.GetComponent<Image>();
        GameObject tabObjInner = CreatePanel("Inner", tabObjBorder.transform, new Vector2(0.005f, 0.06f), new Vector2(0.995f, 0.94f), new Color(0.08f, 0.16f, 0.11f, 1f));
        tabObjetosBgImage = tabObjInner.GetComponent<Image>();
        tabObjetosButton = tabObjBorder.AddComponent<Button>();
        tabObjetosButton.targetGraphic = tabObjetosBgImage;

        tabObjetosLabel = CreateText("Label", tabObjInner.transform, "1. OBJETOS Y CONSUMIBLES [1 / Q]", 22, TextAlignmentOptions.Center, consumableGreenColor);
        tabObjetosLabel.fontStyle = FontStyles.Bold;
        SetAnchors(tabObjetosLabel.rectTransform, Vector2.zero, Vector2.one);

        // Pestaña 2: ARMAS
        GameObject tabArmBorder = CreatePanel("Tab_Armas", subMenuTabsRoot.transform, new Vector2(0.512f, 0f), new Vector2(1f, 1f), new Color(0.28f, 0.30f, 0.35f, 1f));
        tabArmasBorderImage = tabArmBorder.GetComponent<Image>();
        GameObject tabArmInner = CreatePanel("Inner", tabArmBorder.transform, new Vector2(0.005f, 0.06f), new Vector2(0.995f, 0.94f), new Color(0.05f, 0.06f, 0.08f, 0.95f));
        tabArmasBgImage = tabArmInner.GetComponent<Image>();
        tabArmasButton = tabArmBorder.AddComponent<Button>();
        tabArmasButton.targetGraphic = tabArmasBgImage;

        tabArmasLabel = CreateText("Label", tabArmInner.transform, "2. ARMAS Y EQUIPAMIENTO (RETRATO) [2 / E]", 22, TextAlignmentOptions.Center, new Color(0.65f, 0.68f, 0.74f, 1f));
        tabArmasLabel.fontStyle = FontStyles.Bold;
        SetAnchors(tabArmasLabel.rectTransform, Vector2.zero, Vector2.one);

        // =====================================================================
        // SUBMENÚ 1: PANEL COMPLETO DE OBJETOS
        // =====================================================================
        itemsPanelRoot = CreatePanel("SubMenu_Items_Panel", mainBox.transform, new Vector2(0.025f, 0.11f), new Vector2(0.975f, 0.780f), consumableGreenColor);
        GameObject itemsInner = CreatePanel("Inner", itemsPanelRoot.transform, new Vector2(0.003f, 0.005f), new Vector2(0.997f, 0.995f), new Color(0.06f, 0.07f, 0.09f, 1f));

        GameObject itemsHeaderBg = CreatePanel("ItemsHeaderBg", itemsInner.transform, new Vector2(0f, 0.925f), new Vector2(1f, 1f), new Color(0.09f, 0.15f, 0.11f, 1f));
        itemsSectionCounterText = CreateText("ItemsHeaderTitle", itemsHeaderBg.transform, "MOCHILA DE ENRIQUE", 22, TextAlignmentOptions.Center, consumableGreenColor);
        itemsSectionCounterText.fontStyle = FontStyles.Bold;
        SetAnchors(itemsSectionCounterText.rectTransform, new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.95f));

        GameObject itemsContainerGO = CreatePanel("ItemsListContainer", itemsInner.transform, new Vector2(0.015f, 0.290f), new Vector2(0.985f, 0.915f), new Color(0f, 0f, 0f, 0f));
        itemsListContainer = itemsContainerGO.GetComponent<RectTransform>();

        // Pie de Objetos: Recetas de Intelecto y Botón Rápido
        GameObject recipesBox = CreatePanel("RecipesFooterBox", itemsInner.transform, new Vector2(0.015f, 0.015f), new Vector2(0.985f, 0.280f), new Color(0.12f, 0.14f, 0.19f, 1f));
        recipesStatusText = CreateText("RecipesStatusText", recipesBox.transform, "RECETAS DE INTELECTO:", 12, TextAlignmentOptions.TopLeft, new Color(0.88f, 0.90f, 0.94f, 1f));
        recipesStatusText.enableAutoSizing = true;
        recipesStatusText.fontSizeMin = 8f;
        recipesStatusText.fontSizeMax = 12.5f;
        recipesStatusText.lineSpacing = 0f;
        recipesStatusText.margin = Vector4.zero;
        recipesStatusText.overflowMode = TextOverflowModes.Truncate;
        SetAnchors(recipesStatusText.rectTransform, new Vector2(0.012f, 0.04f), new Vector2(0.73f, 0.95f));

        GameObject quickHealBorder = CreatePanel("QuickCraftHealBtn", recipesBox.transform, new Vector2(0.74f, 0.12f), new Vector2(0.985f, 0.88f), consumableGreenColor);
        quickCraftHealBorderImage = quickHealBorder.GetComponent<Image>();
        GameObject quickHealInner = CreatePanel("Inner", quickHealBorder.transform, new Vector2(0.015f, 0.06f), new Vector2(0.985f, 0.94f), new Color(0.07f, 0.09f, 0.11f, 1f));
        quickCraftHealRecipeButton = quickHealBorder.AddComponent<Button>();
        quickCraftHealRecipeButton.targetGraphic = quickHealInner.GetComponent<Image>();

        quickCraftHealButtonLabel = CreateText("QuickHealLabel", quickHealInner.transform, "VENDAJE RUDO", 17, TextAlignmentOptions.Center, consumableGreenColor);
        quickCraftHealButtonLabel.fontStyle = FontStyles.Bold;
        SetAnchors(quickCraftHealButtonLabel.rectTransform, Vector2.zero, Vector2.one);

        // =====================================================================
        // SUBMENÚ 2: PANEL COMPLETO DE ARMAS Y RETRATO DE ENRIQUE
        // =====================================================================
        weaponsPanelRoot = CreatePanel("SubMenu_Weapons_Panel", mainBox.transform, new Vector2(0.025f, 0.11f), new Vector2(0.975f, 0.780f), weaponGoldColor);
        GameObject weaponsInner = CreatePanel("Inner", weaponsPanelRoot.transform, new Vector2(0.003f, 0.005f), new Vector2(0.997f, 0.995f), new Color(0.06f, 0.07f, 0.09f, 1f));

        GameObject weaponsHeaderBg = CreatePanel("WeaponsHeaderBg", weaponsInner.transform, new Vector2(0f, 0.925f), new Vector2(1f, 1f), new Color(0.18f, 0.15f, 0.07f, 1f));
        TextMeshProUGUI weaponsHeaderTitle = CreateText("WeaponsHeaderTitle", weaponsHeaderBg.transform, "ARMAS Y EQUIPAMIENTO DE ENRIQUE BOSQUES", 22, TextAlignmentOptions.Center, weaponGoldColor);
        weaponsHeaderTitle.fontStyle = FontStyles.Bold;
        SetAnchors(weaponsHeaderTitle.rectTransform, new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.95f));

        // Showcase Superior: Retrato de Enrique & Arma en Uso (Espacioso y Elegante)
        GameObject equippedBoxBorder = CreatePanel("EquippedWeaponBox", weaponsInner.transform, new Vector2(0.015f, 0.52f), new Vector2(0.985f, 0.915f), weaponGoldColor);
        GameObject equippedBoxInner = CreatePanel("Inner", equippedBoxBorder.transform, new Vector2(0.003f, 0.02f), new Vector2(0.997f, 0.98f), new Color(0.09f, 0.09f, 0.12f, 1f));

        // A) Retrato de Enrique interactivo
        enriquePortraitCardRoot = CreatePanel("EnriquePortraitCard", equippedBoxInner.transform, new Vector2(0.015f, 0.05f), new Vector2(0.24f, 0.95f), weaponGoldColor);
        enriquePortraitBorderImage = enriquePortraitCardRoot.GetComponent<Image>();

        GameObject enriqueInnerBg = CreatePanel("PortraitInner", enriquePortraitCardRoot.transform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), new Color(0.05f, 0.05f, 0.07f, 1f));
        GameObject portraitImgGO = CreatePanel("PortraitImage", enriqueInnerBg.transform, Vector2.zero, Vector2.one, Color.white);
        enriquePortraitImage = portraitImgGO.GetComponent<Image>();
        if (enriquePortrait != null)
        {
            enriquePortraitImage.sprite = enriquePortrait;
        }

        // Overlay icono del arma en la esquina superior derecha del retrato
        GameObject weaponOverlayIconGO = CreatePanel("WeaponOverlayIcon", enriqueInnerBg.transform, new Vector2(0.68f, 0.68f), new Vector2(0.98f, 0.98f), Color.white);
        enriquePortraitWeaponIcon = weaponOverlayIconGO.GetComponent<Image>();

        // Banner inferior en el retrato
        GameObject weaponBadgeBanner = CreatePanel("WeaponBadgeBanner", enriqueInnerBg.transform, new Vector2(0f, 0f), new Vector2(1f, 0.30f), new Color(0.05f, 0.06f, 0.08f, 0.92f));
        enriquePortraitWeaponBadgeText = CreateText("PortraitBadgeText", weaponBadgeBanner.transform, "PUÑOS DESNUDOS\n[CLIC: ARMAS]", 14, TextAlignmentOptions.Center, Color.white);
        SetAnchors(enriquePortraitWeaponBadgeText.rectTransform, Vector2.zero, Vector2.one);

        enriquePortraitClickButton = enriquePortraitCardRoot.AddComponent<Button>();
        enriquePortraitClickButton.targetGraphic = enriquePortraitBorderImage;

        // B) Información detallada del arma equipada al centro del showcase (con amplio espacio para no traslapar)
        equippedWeaponNameText = CreateText("EquippedWeaponName", equippedBoxInner.transform, "ARMA EQUIPADA: PUÑOS DESNUDOS", 23, TextAlignmentOptions.Left, Color.white);
        equippedWeaponNameText.fontStyle = FontStyles.Bold;
        SetAnchors(equippedWeaponNameText.rectTransform, new Vector2(0.26f, 0.68f), new Vector2(0.76f, 0.96f));

        equippedWeaponStatsText = CreateText("EquippedWeaponStats", equippedBoxInner.transform, "Bono de Daño: +0 ATQ", 18, TextAlignmentOptions.Left, new Color(0.90f, 0.92f, 0.95f, 1f));
        SetAnchors(equippedWeaponStatsText.rectTransform, new Vector2(0.26f, 0.38f), new Vector2(0.76f, 0.68f));

        equippedWeaponDescText = CreateText("EquippedWeaponDesc", equippedBoxInner.transform, "Haz clic en el Retrato de Enrique para cambiar armas o selecciona una abajo.", 16, TextAlignmentOptions.Left, new Color(0.80f, 0.83f, 0.88f, 1f));
        SetAnchors(equippedWeaponDescText.rectTransform, new Vector2(0.26f, 0.04f), new Vector2(0.76f, 0.38f));

        // C) Botones de Acción a la derecha del showcase
        GameObject unequipBorder = CreatePanel("UnequipButton", equippedBoxInner.transform, new Vector2(0.78f, 0.53f), new Vector2(0.985f, 0.92f), new Color(0.88f, 0.36f, 0.32f, 1f));
        unequipWeaponBorderImage = unequipBorder.GetComponent<Image>();
        GameObject unequipInner = CreatePanel("Inner", unequipBorder.transform, new Vector2(0.015f, 0.08f), new Vector2(0.985f, 0.92f), new Color(0.09f, 0.08f, 0.10f, 1f));
        unequipWeaponButton = unequipBorder.AddComponent<Button>();
        unequipWeaponButton.targetGraphic = unequipInner.GetComponent<Image>();

        unequipWeaponButtonLabel = CreateText("UnequipLabel", unequipInner.transform, "DESEQUIPAR ARMA", 17, TextAlignmentOptions.Center, Color.white);
        unequipWeaponButtonLabel.fontStyle = FontStyles.Bold;
        SetAnchors(unequipWeaponButtonLabel.rectTransform, Vector2.zero, Vector2.one);

        GameObject cycleBorder = CreatePanel("CycleWeaponButton", equippedBoxInner.transform, new Vector2(0.78f, 0.08f), new Vector2(0.985f, 0.47f), weaponGoldColor);
        GameObject cycleInner = CreatePanel("Inner", cycleBorder.transform, new Vector2(0.015f, 0.08f), new Vector2(0.985f, 0.92f), new Color(0.09f, 0.08f, 0.10f, 1f));
        changeWeaponCycleButton = cycleBorder.AddComponent<Button>();
        changeWeaponCycleButton.targetGraphic = cycleInner.GetComponent<Image>();

        TextMeshProUGUI cycleLabel = CreateText("CycleLabel", cycleInner.transform, "CAMBIAR ARMA [CLIC]", 17, TextAlignmentOptions.Center, weaponGoldColor);
        cycleLabel.fontStyle = FontStyles.Bold;
        SetAnchors(cycleLabel.rectTransform, Vector2.zero, Vector2.one);

        // Catálogo Inferior de Armas Disponibles
        GameObject weaponsCatalogBorder = CreatePanel("WeaponsCatalogBorder", weaponsInner.transform, new Vector2(0.015f, 0.015f), new Vector2(0.985f, 0.50f), new Color(0.24f, 0.26f, 0.30f, 1f));
        GameObject weaponsCatalogInner = CreatePanel("Inner", weaponsCatalogBorder.transform, new Vector2(0.003f, 0.01f), new Vector2(0.997f, 0.99f), new Color(0.06f, 0.07f, 0.09f, 1f));

        GameObject weaponsContainerGO = CreatePanel("WeaponsListContainer", weaponsCatalogInner.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
        weaponsListContainer = weaponsContainerGO.GetComponent<RectTransform>();

        // =====================================================================
        // BOTONES INFERIORES DE NAVEGACIÓN (VER ESTADÍSTICAS / CERRAR)
        // =====================================================================
        GameObject statsBtnBorder = CreatePanel("SwitchToStatsButton", mainBox.transform, new Vector2(0.14f, 0.02f), new Vector2(0.48f, 0.09f), new Color(0.38f, 0.58f, 0.88f, 1f));
        GameObject statsBtnInner = CreatePanel("Inner", statsBtnBorder.transform, new Vector2(0.006f, 0.08f), new Vector2(0.994f, 0.92f), new Color(0.07f, 0.09f, 0.13f, 1f));
        switchToStatsButton = statsBtnBorder.AddComponent<Button>();
        switchToStatsButton.targetGraphic = statsBtnInner.GetComponent<Image>();

        TextMeshProUGUI statsBtnTxt = CreateText("StatsBtnText", statsBtnInner.transform, "VER HABILIDADES Y NIVEL", 22, TextAlignmentOptions.Center, new Color(0.75f, 0.88f, 1f, 1f));
        statsBtnTxt.fontStyle = FontStyles.Bold;
        SetAnchors(statsBtnTxt.rectTransform, Vector2.zero, Vector2.one);

        GameObject closeBtnBorder = CreatePanel("CloseInventoryButton", mainBox.transform, new Vector2(0.52f, 0.02f), new Vector2(0.86f, 0.09f), new Color(0.55f, 0.58f, 0.65f, 1f));
        GameObject closeBtnInner = CreatePanel("Inner", closeBtnBorder.transform, new Vector2(0.006f, 0.08f), new Vector2(0.994f, 0.92f), new Color(0.07f, 0.08f, 0.10f, 1f));
        closeMenuButton = closeBtnBorder.AddComponent<Button>();
        closeMenuButton.targetGraphic = closeBtnInner.GetComponent<Image>();

        TextMeshProUGUI closeBtnTxt = CreateText("CloseBtnText", closeBtnInner.transform, "CERRAR INVENTARIO [I / ESC]", 22, TextAlignmentOptions.Center, Color.white);
        closeBtnTxt.fontStyle = FontStyles.Bold;
        SetAnchors(closeBtnTxt.rectTransform, Vector2.zero, Vector2.one);

        modalPanelRoot.SetActive(false);
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        SetAnchors(rt, anchorMin, anchorMax);
        Image img = go.GetComponent<Image>();
        img.color = color;
        return go;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string content, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (customFont == null)
        {
            customFont = PlayerHealthBarUI.GetCleanPixelFont();
        }
        tmp.font = customFont;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Normal;
        tmp.fontWeight = FontWeight.Regular;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        PlayerHealthBarUI.EnsureCleanPixelFont(tmp);
        return tmp;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private bool WasToggleInventoryKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.bKey.wasPressedThisFrame)
                return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.B))
            return true;
#endif
        return false;
    }

    private void NormalizeRecipesFooterLayout()
    {
        if (itemsListContainer != null)
        {
            SetAnchors(itemsListContainer, new Vector2(0.015f, 0.290f), new Vector2(0.985f, 0.915f));
        }

        if (recipesStatusText != null)
        {
            RectTransform recipesBoxRT = recipesStatusText.transform.parent as RectTransform;
            if (recipesBoxRT != null)
            {
                SetAnchors(recipesBoxRT, new Vector2(0.015f, 0.015f), new Vector2(0.985f, 0.280f));
            }
            recipesStatusText.alignment = TextAlignmentOptions.TopLeft;
            recipesStatusText.fontSize = 12f;
            recipesStatusText.enableAutoSizing = true;
            recipesStatusText.fontSizeMin = 8f;
            recipesStatusText.fontSizeMax = 12.5f;
            recipesStatusText.lineSpacing = 0f;
            recipesStatusText.margin = Vector4.zero;
            recipesStatusText.overflowMode = TextOverflowModes.Truncate;
            SetAnchors(recipesStatusText.rectTransform, new Vector2(0.012f, 0.04f), new Vector2(0.73f, 0.95f));
        }
    }

    private bool WasEscapeKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;
#endif
        return false;
    }
}
