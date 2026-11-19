using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla el sub-menú intelectoAndItemsPanel en combate, dividiendo claramente:
/// - SECCIÓN 1: USAR OBJETOS (Curación y Consumibles)
/// - SECCIÓN 2: CRAFTEAR CON INTELECTO (Combinar Materiales)
/// - Botón "Cancelar / Volver"
/// </summary>
public class CombatIntelectoUI : MonoBehaviour
{
    [Header("Referencias del Sub-Panel de Intelecto y Objetos")]
    public BattleManager battleManager;
    public GameObject panelRoot;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI inventorySummaryText;
    public RectTransform dynamicButtonsContainer;
    public Button cancelButton;
    public TMP_FontAsset customFont;

    [Header("Colores de Estilo Noir (Negro Predominante)")]
    public Color buttonInnerBlack = new Color(0.05f, 0.05f, 0.06f, 1f);
    public Color consumableBorderColor = new Color(0.22f, 0.82f, 0.38f, 1f);
    public Color craftableBorderColor = new Color(0.15f, 0.68f, 0.92f, 1f);
    public Color missingMaterialsBorderColor = new Color(0.28f, 0.28f, 0.32f, 1f);
    public Color enabledTextColor = new Color(0.92f, 0.90f, 0.86f, 1f);
    public Color disabledTextColor = new Color(0.50f, 0.50f, 0.54f, 1f);

    private readonly List<GameObject> spawnedActionButtons = new List<GameObject>();

    private void Awake()
    {
        StripLayoutGroupFromContainer();
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    private void StripLayoutGroupFromContainer()
    {
        if (dynamicButtonsContainer == null) return;
        LayoutGroup lg = dynamicButtonsContainer.GetComponent<LayoutGroup>();
        if (lg != null)
        {
            if (Application.isPlaying)
                Destroy(lg);
            else
                DestroyImmediate(lg);
        }
    }

    public void ShowAndRefresh(BattleManager manager)
    {
        if (manager != null)
        {
            battleManager = manager;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }

        RefreshDynamicContent();
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void OnCancelClicked()
    {
        if (battleManager != null)
        {
            battleManager.CloseIntelectoPanel();
        }
        else
        {
            Hide();
        }
    }

    /// <summary>
    /// Reconstruye dinámicamente las 2 secciones:
    /// SECCIÓN 1: USAR OBJETOS (Curación y Consumibles)
    /// SECCIÓN 2: CRAFTEAR CON INTELECTO (Combinar Materiales)
    /// </summary>
    public void RefreshDynamicContent()
    {
        ClearSpawnedButtons();

        InventoryManager inv = InventoryManager.Instance;

        // Resumen superior de todos los objetos y materiales actuales en el inventario
        if (inventorySummaryText != null)
        {
            if (inv == null || inv.items == null || inv.items.Count == 0)
            {
                inventorySummaryText.text = "INVENTARIO: Vacio (Recoge objetos y materiales en la calle con [E])";
            }
            else
            {
                List<string> itemEntries = new List<string>();
                foreach (InventorySlot slot in inv.items)
                {
                    if (slot != null && slot.item != null && slot.quantity > 0)
                    {
                        itemEntries.Add($"{slot.item.itemName} x{slot.quantity}");
                    }
                }

                inventorySummaryText.text = itemEntries.Count > 0
                    ? "INVENTARIO: " + string.Join(", ", itemEntries)
                    : "INVENTARIO: Vacio (Recoge objetos y materiales en la calle con [E])";
            }
        }

        if (dynamicButtonsContainer == null)
            return;

        StripLayoutGroupFromContainer();

        List<ButtonRowDescriptor> rows = new List<ButtonRowDescriptor>();

        // =====================================================================
        // SECCIÓN 1: USAR OBJETOS (Curación y Consumibles — Diseño Unificado en todos los niveles)
        // =====================================================================
        PlayerStats pStats = (battleManager != null && battleManager.playerStats != null)
            ? battleManager.playerStats
            : FindAnyObjectByType<PlayerStats>();
        int healBonus = (pStats != null) ? pStats.GetIntelectoHealBonus() : 0;
        int dmgBonus = (pStats != null) ? pStats.GetIntelectoDamageBonus() : 0;

        bool hasDetectiveInParty = (battleManager != null && battleManager.detectiveStats != null && battleManager.detectiveStats.hasDetectiveInParty);
        DetectiveStats dStats = hasDetectiveInParty ? battleManager.detectiveStats : null;
        bool isDetectiveTurn = (battleManager != null && battleManager.state == BattleState.DETECTIVETURN);

        if (inv != null)
        {
            List<InventorySlot> consumables = inv.GetConsumableItems();
            foreach (InventorySlot slot in consumables)
            {
                if (slot == null || slot.item == null || slot.quantity <= 0)
                    continue;

                ItemData capturedItem = slot.item;
                bool isHealing = (capturedItem.category == ItemCategory.ConsumibleCuracion || capturedItem.healAmount > 0);

                // Determinar automáticamente si el objeto curativo aplica a Vargas (en su turno, si cayó inconsciente, o si Enrique tiene 100% HP y Vargas está herido)
                bool healDetective = false;
                if (isHealing && hasDetectiveInParty && dStats != null)
                {
                    bool detUnconscious = dStats.HP_Actual <= 0;
                    bool enriqueFullAndDetHurt = (pStats != null && pStats.currentHP >= pStats.maxHP && dStats.HP_Actual < dStats.HP_Max);
                    healDetective = isDetectiveTurn || detUnconscious || enriqueFullAndDetHurt;
                }

                string btnLabel;
                if (isHealing)
                {
                    int totalHeal = capturedItem.healAmount + healBonus;
                    btnLabel = $"Usar: {capturedItem.itemName} (x{slot.quantity}) - Cura {totalHeal} HP";
                }
                else
                {
                    int totalDmg = capturedItem.damageAmount + dmgBonus;
                    btnLabel = $"Usar: {capturedItem.itemName} (x{slot.quantity}) - Dano {totalDmg}";
                }

                bool capturedTargetDet = healDetective;
                rows.Add(new ButtonRowDescriptor
                {
                    label = btnLabel,
                    interactable = true,
                    borderColor = consumableBorderColor,
                    onClick = () =>
                    {
                        if (battleManager != null)
                        {
                            battleManager.UseConsumableInCombat(capturedItem, targetDetective: capturedTargetDet);
                        }
                    }
                });
            }
        }

        // =====================================================================
        // SECCIÓN 2: CRAFTEAR CON INTELECTO (Combinar Materiales — Diseño Unificado en todos los niveles)
        // =====================================================================
        if (inv != null)
        {
            inv.EnsureKnownRecipesLoaded();
        }
        if (inv != null && inv.knownRecipes != null)
        {
            List<CraftingRecipeData> sortedRecipes = new List<CraftingRecipeData>(inv.knownRecipes);
            sortedRecipes.Sort((a, b) =>
            {
                bool ca = a != null && inv.CanCraft(a);
                bool cb = b != null && inv.CanCraft(b);
                return cb.CompareTo(ca);
            });

            foreach (CraftingRecipeData recipe in sortedRecipes)
            {
                if (recipe == null)
                    continue;

                CraftingRecipeData capturedRecipe = recipe;
                bool canCraft = inv.CanCraft(capturedRecipe);

                string matAName = (capturedRecipe.materialA != null) ? capturedRecipe.materialA.itemName : "Ninguno";
                string matBName = (capturedRecipe.materialB != null) ? capturedRecipe.materialB.itemName : "Ninguno";

                bool healDetective = false;
                if (capturedRecipe.healAmount > 0 && hasDetectiveInParty && dStats != null)
                {
                    bool detUnconscious = dStats.HP_Actual <= 0;
                    bool enriqueFullAndDetHurt = (pStats != null && pStats.currentHP >= pStats.maxHP && dStats.HP_Actual < dStats.HP_Max);
                    healDetective = isDetectiveTurn || detUnconscious || enriqueFullAndDetHurt;
                }

                string effectTag = capturedRecipe.GetCompactEffectTag(healBonus, dmgBonus);

                string btnText = $"Craftear: {capturedRecipe.recipeName}{effectTag} ({matAName} + {matBName})";
                bool capturedTargetDet = healDetective;

                rows.Add(new ButtonRowDescriptor
                {
                    label = btnText,
                    interactable = canCraft,
                    borderColor = canCraft ? craftableBorderColor : missingMaterialsBorderColor,
                    onClick = () =>
                    {
                        if (battleManager != null)
                        {
                            battleManager.CraftAndUseRecipeInCombat(capturedRecipe, targetDetective: capturedTargetDet);
                        }
                    }
                });
            }
        }

        if (rows.Count == 0)
        {
            rows.Add(new ButtonRowDescriptor
            {
                label = "Sin objetos curativos ni recetas disponibles",
                interactable = false,
                borderColor = missingMaterialsBorderColor,
                onClick = null
            });
        }

        int totalItems = rows.Count;
        if (totalItems <= 4)
        {
            int maxVisibleSlots = Mathf.Max(3, totalItems);
            float slotHeight = 1.0f / maxVisibleSlots;
            float verticalPadding = 0.02f;

            for (int i = 0; i < totalItems; i++)
            {
                float topY = 1.0f - (i * slotHeight) - verticalPadding;
                float bottomY = 1.0f - ((i + 1) * slotHeight) + verticalPadding;

                GameObject btnGO = CreateDynamicActionButton(
                    $"IntelectoOption_{i}",
                    dynamicButtonsContainer,
                    new Vector2(0.01f, Mathf.Clamp01(bottomY)),
                    new Vector2(0.99f, Mathf.Clamp01(topY)),
                    rows[i],
                    24f
                );
                spawnedActionButtons.Add(btnGO);
            }
        }
        else
        {
            int cols = 2;
            int numGridRows = Mathf.CeilToInt(totalItems / (float)cols);
            float slotHeight = 1.0f / Mathf.Max(3, numGridRows);
            float verticalPadding = 0.02f;

            for (int i = 0; i < totalItems; i++)
            {
                int r = i / cols;
                int c = i % cols;
                float topY = 1.0f - (r * slotHeight) - verticalPadding;
                float bottomY = 1.0f - ((r + 1) * slotHeight) + verticalPadding;
                float minX = (c == 0) ? 0.01f : 0.505f;
                float maxX = (c == 0) ? 0.495f : 0.99f;

                GameObject btnGO = CreateDynamicActionButton(
                    $"IntelectoOption_{i}",
                    dynamicButtonsContainer,
                    new Vector2(minX, Mathf.Clamp01(bottomY)),
                    new Vector2(maxX, Mathf.Clamp01(topY)),
                    rows[i],
                    19f
                );
                spawnedActionButtons.Add(btnGO);
            }
        }
    }

    private void ClearSpawnedButtons()
    {
        for (int i = spawnedActionButtons.Count - 1; i >= 0; i--)
        {
            if (spawnedActionButtons[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(spawnedActionButtons[i]);
                else
                    DestroyImmediate(spawnedActionButtons[i]);
            }
        }
        spawnedActionButtons.Clear();

        if (dynamicButtonsContainer != null)
        {
            for (int i = dynamicButtonsContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = dynamicButtonsContainer.GetChild(i);
                if (child != null && child.name.StartsWith("IntelectoOption_"))
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    private GameObject CreateDynamicActionButton(
        string objName,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        ButtonRowDescriptor desc,
        float fontSize = 24f)
    {
        GameObject borderGO = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button));
        borderGO.transform.SetParent(parent, false);

        RectTransform borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = anchorMin;
        borderRT.anchorMax = anchorMax;
        borderRT.offsetMin = Vector2.zero;
        borderRT.offsetMax = Vector2.zero;

        Image borderImg = borderGO.GetComponent<Image>();
        borderImg.color = desc.borderColor;

        GameObject innerGO = new GameObject("InnerBlack", typeof(RectTransform), typeof(Image));
        innerGO.transform.SetParent(borderGO.transform, false);
        RectTransform innerRT = innerGO.GetComponent<RectTransform>();
        innerRT.anchorMin = new Vector2(0.004f, 0.06f);
        innerRT.anchorMax = new Vector2(0.996f, 0.94f);
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;

        Image innerImg = innerGO.GetComponent<Image>();
        innerImg.color = buttonInnerBlack;

        Button btn = borderGO.GetComponent<Button>();
        btn.targetGraphic = innerImg;
        btn.interactable = desc.interactable;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
        btn.colors = cb;

        if (desc.interactable && desc.onClick != null)
        {
            btn.onClick.AddListener(() => desc.onClick.Invoke());
        }

        GameObject textGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(innerGO.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.02f, 0.06f);
        textRT.anchorMax = new Vector2(0.98f, 0.94f);
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        if (customFont == null)
        {
            customFont = PlayerHealthBarUI.GetCleanPixelFont();
        }
        tmp.font = customFont;

        tmp.text = desc.label;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Normal;
        tmp.fontWeight = FontWeight.Regular;
        tmp.enableAutoSizing = false;
        tmp.characterSpacing = 1.2f;
        tmp.wordSpacing = 4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = desc.interactable ? enabledTextColor : disabledTextColor;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        PlayerHealthBarUI.EnsureCleanPixelFont(tmp);

        return borderGO;
    }

    private struct ButtonRowDescriptor
    {
        public string label;
        public bool interactable;
        public Color borderColor;
        public System.Action onClick;
    }
}
