using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Barra de vida visual interactiva para Enrique Bosques.
/// Incluye animación fluida tanto al recibir daño (rastro rojo con drenado suave y pulso de impacto)
/// como al recuperar vida (previsualización luminosa de curación y llenado progresivo),
/// además de saneamiento automático del atlas de fuentes TextMeshPro para evitar bloques de textura.
/// </summary>
public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("Referencias a Datos")]
    [Tooltip("Referencia opcional a PlayerStats. Si está vacía, la buscará automáticamente.")]
    public PlayerStats playerStats;

    [Header("Elementos Visuales de la Barra")]
    [Tooltip("Contenedor o imagen de fondo de la barra.")]
    public Image barBackgroundImage;

    [Tooltip("Borde exterior de la barra.")]
    public Image barBorderImage;

    [Tooltip("Barra secundaria (detrás del fill principal). Muestra en rojo el daño recibido o en verde claro la vida por recuperar.")]
    public RectTransform damageFillRect;
    public Image damageFillImage;

    [Tooltip("Barra de vida principal.")]
    public RectTransform mainFillRect;
    public Image mainFillImage;

    [Tooltip("Slider opcional si se utiliza el componente Slider de Unity.")]
    public Slider healthSlider;

    [Header("Textos de Información")]
    [Tooltip("Texto con la cantidad numérica de vida (ej. ENRIQUE (NV. 1)  HP: 75 / 100).")]
    public TextMeshProUGUI hpText;

    [Tooltip("Texto dedicado al porcentaje de vida (ej. 75%).")]
    public TextMeshProUGUI percentageText;

    [Tooltip("Texto opcional con el nombre del personaje (en el HUD de exploración).")]
    public TextMeshProUGUI nameText;

    [Header("Configuración de Colores")]
    [Tooltip("Color verde de la barra de salud principal (> 50%).")]
    public Color healthyColor = new Color(0.22f, 0.85f, 0.38f, 1f);

    [Tooltip("Color de la barra con salud media (25% a 50%).")]
    public Color warningColor = new Color(0.92f, 0.65f, 0.15f, 1f);

    [Tooltip("Color de la barra con salud crítica (<= 25%).")]
    public Color criticalColor = new Color(0.92f, 0.18f, 0.18f, 1f);

    [Tooltip("Color de la barra de daño que baja tras recibir un golpe.")]
    public Color damageLagColor = new Color(0.88f, 0.14f, 0.14f, 0.82f);

    [Tooltip("Color de previsualización cuando la vida sube (curación).")]
    public Color healPreviewColor = new Color(0.58f, 1f, 0.76f, 0.92f);

    [Tooltip("Color base para el texto de porcentaje.")]
    public Color percentageColor = new Color(0.22f, 0.85f, 0.38f, 1f);

    // Compatibilidad retroactiva
    public Color redPercentageColor { get => percentageColor; set => percentageColor = value; }

    [Header("Comportamiento")]
    [Tooltip("Sincroniza automáticamente con PlayerStats en cada frame si está activado.")]
    public bool autoSyncWithPlayerStats = true;

    [Tooltip("Si es un HUD de exploración, se oculta cuando el panel de combate esté activo.")]
    public bool hideWhenCombatActive = false;

    public bool IsInitialized => lastRecordedHP >= 0;

    private int lastRecordedHP = -1;
    private int lastRecordedMaxHP = -1;
    private bool lastDefendingState = false;
    private int lastAttackBuff = 0;
    private Coroutine drainCoroutine;
    private float currentVisualFill = 1f;
    private float damageVisualFill = 1f;

    private static TMP_FontAsset s_repairedRuntimeFont;
    private const string PREWARM_CHARS = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~¡¿ÁÉÍÓÚÑÜáéíóúñü";

    private void Awake()
    {
        if (playerStats == null)
        {
            playerStats = FindAnyObjectByType<PlayerStats>();
        }

        if (damageFillImage != null)
        {
            damageFillImage.color = damageLagColor;
        }

        AutoDiscoverTextReferences();
        EnsureAllFontsClean();
    }

    private void Start()
    {
        AutoDiscoverTextReferences();
        EnsureAllFontsClean();

        if (playerStats != null && lastRecordedHP < 0)
        {
            SetHealthInstant(playerStats.currentHP, playerStats.maxHP);
        }
    }

    private void Update()
    {
        if (hideWhenCombatActive)
        {
            BattleManager bm = FindAnyObjectByType<BattleManager>();
            if (bm != null && bm.battlePanel != null)
            {
                bool isCombatActive = bm.battlePanel.activeInHierarchy;
                if (gameObject.activeSelf == isCombatActive)
                {
                    gameObject.SetActive(!isCombatActive);
                }
            }
        }

        if (!autoSyncWithPlayerStats || playerStats == null) return;

        if (playerStats.currentHP != lastRecordedHP || playerStats.maxHP != lastRecordedMaxHP)
        {
            UpdateHealth(playerStats.currentHP, playerStats.maxHP, lastDefendingState, lastAttackBuff);
        }
    }

    public void AutoDiscoverTextReferences()
    {
        if (hpText == null)
        {
            hpText = FindChildText(transform, "PlayerHPText");
        }

        if (percentageText == null)
        {
            percentageText = FindChildText(transform, "PlayerHPPctText");
            if (percentageText == null)
                percentageText = FindChildText(transform, "PctText");
        }

        if (nameText == null)
        {
            nameText = FindChildText(transform, "NameText");
        }
    }

    private static TextMeshProUGUI FindChildText(Transform root, string targetName)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                TextMeshProUGUI tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null) return tmp;
            }
            TextMeshProUGUI nested = FindChildText(child, targetName);
            if (nested != null) return nested;
        }
        return null;
    }

    /// <summary>
    /// Repara y valida las fuentes de todos los textos de la barra de vida para evitar que se
    /// rendericen como bloques sólidos con la textura cruda del atlas.
    /// </summary>
    public void EnsureAllFontsClean()
    {
        EnsureCleanPixelFont(hpText);
        EnsureCleanPixelFont(percentageText);
        EnsureCleanPixelFont(nameText);

        TextMeshProUGUI[] allChildTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (allChildTexts != null)
        {
            for (int i = 0; i < allChildTexts.Length; i++)
            {
                EnsureCleanPixelFont(allChildTexts[i]);
            }
        }
    }

    public static void EnsureCleanPixelFont(TMP_Text textComp)
    {
        if (textComp == null) return;

        // Desactivar Bold por completo para evitar que _WeightBold dilate el SDF de la fuente pixel en un bloque sólido
        textComp.fontStyle = FontStyles.Normal;
        textComp.fontWeight = FontWeight.Regular;

        TMP_FontAsset font = textComp.font;
        if (font == null)
        {
            font = TMP_Settings.defaultFontAsset;
        }

        if (font != null)
        {
            TMP_FontAsset validFont = ValidateOrRepairFontAsset(font);
            if (validFont != null)
            {
                textComp.font = validFont;
                if (validFont.material != null)
                {
                    textComp.fontSharedMaterial = validFont.material;
                }
            }
        }

        textComp.SetAllDirty();
    }

    private static TMP_FontAsset ValidateOrRepairFontAsset(TMP_FontAsset font)
    {
        if (font == null) return TMP_Settings.defaultFontAsset;

        // Asegurar que los pesos de negrita no deformen los glifos en bloques
        font.boldStyle = 0f;
        font.boldSpacing = 0f;
        if (font.material != null)
        {
            if (font.material.HasProperty(ShaderUtilities.ID_WeightBold))
                font.material.SetFloat(ShaderUtilities.ID_WeightBold, 0f);
            if (font.material.HasProperty(ShaderUtilities.ID_WeightNormal))
                font.material.SetFloat(ShaderUtilities.ID_WeightNormal, 0f);
        }

        bool atlasInvalid = font.atlasTexture == null || font.atlasTexture.width <= 4 || font.atlasTexture.height <= 4;
        bool charsEmpty = font.characterTable == null || font.characterTable.Count == 0;

        if (!atlasInvalid && !charsEmpty)
        {
            font.atlasTexture.filterMode = FilterMode.Point;
            if (font.material != null)
            {
                font.material.mainTexture = font.atlasTexture;
                if (font.material.HasProperty(ShaderUtilities.ID_TextureWidth))
                    font.material.SetFloat(ShaderUtilities.ID_TextureWidth, font.atlasTexture.width);
                if (font.material.HasProperty(ShaderUtilities.ID_TextureHeight))
                    font.material.SetFloat(ShaderUtilities.ID_TextureHeight, font.atlasTexture.height);
            }
            return font;
        }

        // Si ya construimos un reemplazo limpio en memoria durante esta sesión, reutilizarlo
        if (s_repairedRuntimeFont != null && s_repairedRuntimeFont.atlasTexture != null && s_repairedRuntimeFont.atlasTexture.width > 4)
        {
            return s_repairedRuntimeFont;
        }

        Font sourceTTF = font.sourceFontFile;
#if UNITY_EDITOR
        if (sourceTTF == null)
        {
            sourceTTF = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Pixelzone_Font_1_10/Pixelzone.ttf");
        }
#endif

        // Intentar reconstruir el propio asset si tiene su fuente TTF asignada
        if (sourceTTF != null)
        {
            try
            {
                TMP_FontAsset runtimeFont = TMP_FontAsset.CreateFontAsset(
                    sourceTTF,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true
                );

                if (runtimeFont != null && runtimeFont.atlasTexture != null)
                {
                    var fi = runtimeFont.faceInfo;
                    fi.scale = 1.6f;
                    runtimeFont.faceInfo = fi;
                    runtimeFont.normalSpacingOffset = 1f;
                    runtimeFont.boldStyle = 0f;
                    runtimeFont.boldSpacing = 0f;
                    runtimeFont.atlasTexture.filterMode = FilterMode.Point;

                    if (font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Count > 0)
                    {
                        runtimeFont.fallbackFontAssetTable = font.fallbackFontAssetTable;
                    }

                    runtimeFont.TryAddCharacters(PREWARM_CHARS, out string _);

                    if (runtimeFont.material != null)
                    {
                        runtimeFont.material.mainTexture = runtimeFont.atlasTexture;
                        if (runtimeFont.material.HasProperty(ShaderUtilities.ID_WeightBold))
                            runtimeFont.material.SetFloat(ShaderUtilities.ID_WeightBold, 0f);
                        if (runtimeFont.material.HasProperty(ShaderUtilities.ID_WeightNormal))
                            runtimeFont.material.SetFloat(ShaderUtilities.ID_WeightNormal, 0f);
                        if (runtimeFont.material.HasProperty(ShaderUtilities.ID_TextureWidth))
                            runtimeFont.material.SetFloat(ShaderUtilities.ID_TextureWidth, runtimeFont.atlasTexture.width);
                        if (runtimeFont.material.HasProperty(ShaderUtilities.ID_TextureHeight))
                            runtimeFont.material.SetFloat(ShaderUtilities.ID_TextureHeight, runtimeFont.atlasTexture.height);
                    }

                    if (runtimeFont.atlasTexture.width > 4 && runtimeFont.characterTable != null && runtimeFont.characterTable.Count > 0)
                    {
                        s_repairedRuntimeFont = runtimeFont;
                        return s_repairedRuntimeFont;
                    }
                }
            }
            catch (System.Exception)
            {
                // Fallback seguro abajo
            }
        }

        // Fallback seguro a la fuente estática predeterminada (LiberationSans SDF) si el TTF no está disponible
        if (font.fallbackFontAssetTable != null)
        {
            for (int i = 0; i < font.fallbackFontAssetTable.Count; i++)
            {
                TMP_FontAsset fb = font.fallbackFontAssetTable[i];
                if (fb != null && fb.atlasTexture != null && fb.atlasTexture.width > 4)
                {
                    return fb;
                }
            }
        }

        return TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset : font;
    }

    /// <summary>
    /// Actualiza la barra de vida de forma visual, ejecutando una animación dinámica tanto cuando
    /// la vida baja (daño con rastro rojo) como cuando la vida sube (curación con destello verde).
    /// </summary>
    public void UpdateHealth(int currentHP, int maxHP, bool isDefending = false, int attackBuff = 0)
    {
        maxHP = Mathf.Max(1, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        AutoDiscoverTextReferences();
        EnsureAllFontsClean();

        lastDefendingState = isDefending;
        lastAttackBuff = attackBuff;

        if (lastRecordedHP < 0)
        {
            SetHealthInstant(currentHP, maxHP, isDefending, attackBuff);
            return;
        }

        int oldHP = lastRecordedHP;
        lastRecordedHP = currentHP;
        lastRecordedMaxHP = maxHP;

        if (currentHP == oldHP)
        {
            // Solo cambiaron estados (ej. Guardia o Buff de Ataque)
            UpdateTexts(currentHP, maxHP, isDefending, attackBuff);
            UpdateFillColor(currentVisualFill);
            return;
        }

        if (drainCoroutine != null)
        {
            StopCoroutine(drainCoroutine);
        }

        if (currentHP < oldHP)
        {
            drainCoroutine = StartCoroutine(AnimateDamageDrain(oldHP, currentHP, maxHP, isDefending, attackBuff));
        }
        else
        {
            drainCoroutine = StartCoroutine(AnimateHeal(oldHP, currentHP, maxHP, isDefending, attackBuff));
        }
    }

    /// <summary>
    /// Configura la barra inmediatamente sin transición animada (ej. al inicio de la escena).
    /// </summary>
    public void SetHealthInstant(int currentHP, int maxHP, bool isDefending = false, int attackBuff = 0)
    {
        if (drainCoroutine != null)
        {
            StopCoroutine(drainCoroutine);
            drainCoroutine = null;
        }

        maxHP = Mathf.Max(1, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        lastRecordedHP = currentHP;
        lastRecordedMaxHP = maxHP;
        lastDefendingState = isDefending;
        lastAttackBuff = attackBuff;

        AutoDiscoverTextReferences();
        EnsureAllFontsClean();

        float fill = (float)currentHP / maxHP;
        currentVisualFill = fill;
        damageVisualFill = fill;

        if (damageFillImage != null)
        {
            damageFillImage.color = damageLagColor;
        }

        ResetTextTransforms();
        ApplyFillToRects(fill, fill);
        UpdateTexts(currentHP, maxHP, isDefending, attackBuff);
        UpdateFillColor(fill);

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHP;
            healthSlider.value = currentHP;
        }
    }

    private IEnumerator AnimateDamageDrain(int fromHP, int toHP, int maxHP, bool isDefending, int attackBuff)
    {
        float startFill = currentVisualFill;
        float targetFill = (float)toHP / maxHP;

        // Asegurar que el rastro de daño sea superior o igual al punto de partida
        if (damageVisualFill < startFill)
        {
            damageVisualFill = startFill;
        }

        if (damageFillImage != null)
        {
            damageFillImage.color = damageLagColor;
        }

        // 1. Impacto inicial: la barra principal cae rápidamente mientras el texto tiembla y cuenta hacia abajo
        float dropDuration = 0.22f;
        float elapsed = 0f;

        while (elapsed < dropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dropDuration);
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);

            currentVisualFill = Mathf.Lerp(startFill, targetFill, easeOut);
            int displayHP = Mathf.RoundToInt(Mathf.Lerp(fromHP, toHP, easeOut));

            ApplyFillToRects(currentVisualFill, damageVisualFill);
            UpdateTexts(displayHP, maxHP, isDefending, attackBuff);

            // Destello rojizo/blanco en los primeros instantes del golpe
            if (mainFillImage != null)
            {
                Color baseCol = EvaluateHealthColor(currentVisualFill);
                float flash = Mathf.Sin(t * Mathf.PI);
                mainFillImage.color = Color.Lerp(baseCol, new Color(1f, 0.45f, 0.45f, 1f), flash * 0.65f);
            }

            if (percentageText != null)
            {
                percentageText.color = Color.Lerp(percentageColor, criticalColor, Mathf.Sin(t * Mathf.PI));
                float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.16f;
                percentageText.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (hpText != null)
            {
                float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
                hpText.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }

            yield return null;
        }

        currentVisualFill = targetFill;
        ResetTextTransforms();
        ApplyFillToRects(currentVisualFill, damageVisualFill);
        UpdateTexts(toHP, maxHP, isDefending, attackBuff);
        UpdateFillColor(currentVisualFill);

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHP;
            healthSlider.value = toHP;
        }

        // 2. Breve pausa mostrando el segmento rojo de vida perdida
        float holdDuration = 0.24f;
        elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 3. La barra roja de daño drena suavemente hasta alcanzar la barra principal
        float drainDuration = 0.48f;
        elapsed = 0f;
        float startDamageFill = damageVisualFill;

        while (elapsed < drainDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / drainDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            damageVisualFill = Mathf.Lerp(startDamageFill, targetFill, smoothT);

            ApplyFillToRects(currentVisualFill, damageVisualFill);
            yield return null;
        }

        damageVisualFill = targetFill;
        ApplyFillToRects(currentVisualFill, damageVisualFill);
        drainCoroutine = null;
    }

    private IEnumerator AnimateHeal(int fromHP, int toHP, int maxHP, bool isDefending, int attackBuff)
    {
        float startFill = currentVisualFill;
        float targetFill = (float)toHP / maxHP;

        // 1. La barra trasera se pinta de verde brillante y salta inmediatamente al valor objetivo como vista previa de curación
        if (damageFillImage != null)
        {
            damageFillImage.color = healPreviewColor;
        }
        damageVisualFill = targetFill;
        ApplyFillToRects(startFill, damageVisualFill);

        // Breve pausa para que se aprecie el tramo a recuperar
        float previewHold = 0.12f;
        float elapsed = 0f;
        while (elapsed < previewHold)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / previewHold);
            float scale = Mathf.Lerp(1f, 1.15f, t);
            if (percentageText != null) percentageText.rectTransform.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        // 2. La barra principal sube suavemente llenando el tramo previsualizado mientras el contador de HP y % sube
        float fillDuration = 0.50f;
        elapsed = 0f;

        while (elapsed < fillDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fillDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            currentVisualFill = Mathf.Lerp(startFill, targetFill, smoothT);
            int displayHP = Mathf.RoundToInt(Mathf.Lerp(fromHP, toHP, smoothT));

            ApplyFillToRects(currentVisualFill, damageVisualFill);
            UpdateTexts(displayHP, maxHP, isDefending, attackBuff);

            if (mainFillImage != null)
            {
                Color baseCol = EvaluateHealthColor(currentVisualFill);
                float glow = Mathf.Sin(t * Mathf.PI);
                mainFillImage.color = Color.Lerp(baseCol, healPreviewColor, glow * 0.55f);
            }

            if (percentageText != null)
            {
                percentageText.color = Color.Lerp(percentageColor, healPreviewColor, Mathf.Sin(t * Mathf.PI));
                float scale = Mathf.Lerp(1.15f, 1f, smoothT);
                percentageText.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            if (hpText != null)
            {
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
                hpText.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHP;
                healthSlider.value = displayHP;
            }

            yield return null;
        }

        currentVisualFill = targetFill;
        damageVisualFill = targetFill;

        if (damageFillImage != null)
        {
            damageFillImage.color = damageLagColor;
        }

        ResetTextTransforms();
        ApplyFillToRects(currentVisualFill, damageVisualFill);
        UpdateTexts(toHP, maxHP, isDefending, attackBuff);
        UpdateFillColor(currentVisualFill);

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHP;
            healthSlider.value = toHP;
        }

        drainCoroutine = null;
    }

    private void ResetTextTransforms()
    {
        if (percentageText != null) percentageText.rectTransform.localScale = Vector3.one;
        if (hpText != null) hpText.rectTransform.localScale = Vector3.one;
    }

    private void ApplyFillToRects(float mainFill, float damageFill)
    {
        mainFill = Mathf.Clamp01(mainFill);
        damageFill = Mathf.Clamp01(damageFill);

        if (mainFillRect != null)
        {
            mainFillRect.anchorMin = new Vector2(0f, 0f);
            mainFillRect.anchorMax = new Vector2(mainFill, 1f);
            mainFillRect.offsetMin = Vector2.zero;
            mainFillRect.offsetMax = Vector2.zero;
        }

        if (damageFillRect != null)
        {
            damageFillRect.anchorMin = new Vector2(0f, 0f);
            damageFillRect.anchorMax = new Vector2(damageFill, 1f);
            damageFillRect.offsetMin = Vector2.zero;
            damageFillRect.offsetMax = Vector2.zero;
        }
    }

    private void UpdateTexts(int currentHP, int maxHP, bool isDefending, int attackBuff)
    {
        float pct = ((float)currentHP / Mathf.Max(1, maxHP)) * 100f;
        string defTag = isDefending ? "  <color=#FFD84D>[GUARDIA]</color>" : "";
        string animTag = (attackBuff > 0) ? $"  <color=#66CCFF>[+{attackBuff} ATQ]</color>" : "";

        int lvl = (playerStats != null) ? playerStats.level : 1;

        // Sin etiquetas <b> para evitar que el SDF de la fuente pixel engrose los glifos como bloques
        if (hpText != null)
        {
            hpText.fontStyle = FontStyles.Normal;
            hpText.text = $"ENRIQUE <color=#9FA6B2>(NV. {lvl})</color>   HP: <color=#FFFFFF>{currentHP}</color> / {maxHP}";
        }

        if (percentageText != null)
        {
            percentageText.fontStyle = FontStyles.Normal;
            percentageText.text = $"{pct:0}%{defTag}{animTag}";
            percentageText.color = EvaluateHealthColor(pct / 100f);
        }

        if (nameText != null)
        {
            nameText.fontStyle = FontStyles.Normal;
        }
    }

    private Color EvaluateHealthColor(float fill)
    {
        if (fill <= 0.25f)
            return criticalColor;
        if (fill <= 0.50f)
            return warningColor;
        return healthyColor;
    }

    private void UpdateFillColor(float fill)
    {
        Color targetCol = EvaluateHealthColor(fill);
        if (mainFillImage != null)
        {
            mainFillImage.color = targetCol;
        }
        if (percentageText != null)
        {
            percentageText.color = targetCol;
        }
    }
}
