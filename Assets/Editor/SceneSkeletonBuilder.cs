#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Sistema 5: Constructor automático de toda la Hierarchy y conexiones para "DiscoElysium2" (RPG Santa Felipina).
/// Soporta múltiples enemigos en la misma pantalla frente a la cámara, selección de objetivo con clic,
/// cola de turnos por Motricidad, GIFs debajo de los enemigos y diálogos con Portrait superpuestos.
/// </summary>
public static class SceneSkeletonBuilder
{
    [MenuItem("RPG Santa Felipina/Construir Esqueleto Completo en Hierarchy")]
    public static void BuildCompleteSceneSkeleton()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("[RPG Santa Felipina] Saliendo de Play Mode. Vuelve a ejecutar el menú cuando esté en Edit Mode.");
            return;
        }

        // 0. Configurar resolución 16:9 (1920x1080) y vista Game en 16:9
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        SetGameViewTo16By9();

        // 1. Asegurar que existan los activos EnemyData en Assets/Data/Enemies/, los ItemData/Recetas en Assets/Data/Items/ y el Sprite cuadrado 2D
        EnsureEnemyDataAssets(out EnemyData lacayoRapido, out EnemyData lacayoFuerte, out EnemyData jefeCalacas);
        EnsureItemAndRecipeAssets(
            out ItemData itemRefresco,
            out ItemData itemTorta,
            out ItemData itemBotellaVacia,
            out ItemData itemChilePolvo,
            out CraftingRecipeData recetaBombaChile,
            out CraftingRecipeData recetaVendajeRudo
        );
        Sprite whiteSquareSprite = GetOrCreateWhiteSquareSprite();
        TMP_FontAsset pixelFont = CreateOrLoadPixelzoneFont(forceRecreate: false);

        // 2. Configurar Main Camera en Orthographic con fondo gris muy oscuro / negro
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camGO = GameObject.Find("Main Camera");
            if (camGO == null) camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            mainCam = camGO.GetComponent<Camera>();
            if (mainCam == null) mainCam = camGO.AddComponent<Camera>();
            if (camGO.GetComponent<AudioListener>() == null) camGO.AddComponent<AudioListener>();
        }
        mainCam.orthographic = true;
        mainCam.orthographicSize = 5.5f;
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1.0f);
        mainCam.transform.position = new Vector3(0f, 0f, -10f);
        EditorUtility.SetDirty(mainCam);

        // Limpiar objetos previos de una construcción anterior para evitar duplicados
        DestroyExistingIfFound("Enrique_Player");
        DestroyExistingIfFound("Lacayo_Tutorial");
        DestroyExistingIfFound("Lacayo_Segundo");
        DestroyExistingIfFound("Pickup_ChilePolvo");
        DestroyExistingIfFound("Pickup_BotellaVacia");
        DestroyExistingIfFound("Pickup_CintaCanela");
        DestroyExistingIfFound("Interactable_PuertaEquivocada");
        DestroyExistingIfFound("Pickup_Refresco_Curativo");
        DestroyExistingIfFound("Pickup_Botella");
        DestroyExistingIfFound("Pickup_Chile");
        DestroyExistingIfFound("Puerta_CasaEquivocada");
        DestroyExistingIfFound("Canvas");
        DestroyExistingIfFound("MainUI_Canvas");
        DestroyExistingIfFound("BattleManager");
        DestroyExistingIfFound("InventoryManager");
        DestroyExistingIfFound("ExplorationDialogueManager");
        DestroyExistingIfFound("IntroSlideshowManager");
        DestroyExistingIfFound("GameManagers");

        // 3. Asegurar EventSystem
        EventSystem eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            eventSystem = esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }

        // 4. Crear a Enrique_Player en el mapa
        GameObject enriqueGO = new GameObject("Enrique_Player");
        enriqueGO.tag = "Player";
        enriqueGO.transform.position = new Vector3(-3.5f, 0f, 0f);
        enriqueGO.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        Sprite cardoSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Personaje/stand_down.png");
        if (cardoSprite == null)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Personaje/stand_down.png");
            foreach (var a in assets) { if (a is Sprite s) { cardoSprite = s; break; } }
        }

        GameObject visualGO = new GameObject("Visual");
        visualGO.transform.SetParent(enriqueGO.transform, false);
        visualGO.transform.localPosition = Vector3.zero;
        visualGO.transform.localRotation = Quaternion.identity;
        visualGO.transform.localScale = Vector3.one;

        SpriteRenderer enriqueSR = visualGO.AddComponent<SpriteRenderer>();
        enriqueSR.sprite = (cardoSprite != null) ? cardoSprite : whiteSquareSprite;
        enriqueSR.color = Color.white;
        enriqueSR.sortingOrder = 5;

        Rigidbody2D enriqueRB = enriqueGO.AddComponent<Rigidbody2D>();
        enriqueRB.bodyType = RigidbodyType2D.Dynamic;
        enriqueRB.gravityScale = 0f;
        enriqueRB.freezeRotation = true;
        enriqueRB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D enriqueCol = enriqueGO.AddComponent<BoxCollider2D>();
        enriqueCol.isTrigger = false;
        enriqueCol.offset = new Vector2(0f, 0.72f);
        enriqueCol.size = new Vector2(0.7f, 1.44f);

        RuntimeAnimatorController playerCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player/Player_Controller.controller");
        Animator enriqueAnim = visualGO.AddComponent<Animator>();
        if (playerCtrl != null)
        {
            enriqueAnim.runtimeAnimatorController = playerCtrl;
        }

        PlayerStats playerStats = enriqueGO.AddComponent<PlayerStats>();
        playerStats.maxHP = 100;
        playerStats.currentHP = 100;
        playerStats.corporeo = 12;
        playerStats.intelecto = 14;
        playerStats.motricidad = 12;
        playerStats.mundoInterno = 12;

        PlayerController2D playerController = enriqueGO.AddComponent<PlayerController2D>();
        playerController.moveSpeed = 5.5f;
        playerController.canMove = true;
        playerController.visualTransform = visualGO.transform;
        playerController.animator = enriqueAnim;
        playerController.spriteRenderer = enriqueSR;

        // Conectar cámara de seguimiento hacia Enrique
        GameObject mainCameraGO = GameObject.Find("Main Camera");
        if (mainCameraGO != null)
        {
            CameraFollow2D camFollow = mainCameraGO.GetComponent<CameraFollow2D>();
            if (camFollow == null) camFollow = mainCameraGO.AddComponent<CameraFollow2D>();
            camFollow.target = enriqueGO.transform;
            camFollow.smoothTime = 0.18f;
            camFollow.offset = new Vector3(0f, 0.6f, -10f);
            camFollow.useBounds = false;
            EditorUtility.SetDirty(camFollow);
        }

        // 4b. Crear InventoryManager persistente con los objetos y las 2 recetas en knownRecipes
        GameObject inventoryGO = new GameObject("InventoryManager");
        InventoryManager inventoryManager = inventoryGO.AddComponent<InventoryManager>();
        inventoryManager.items.Clear();
        inventoryManager.unlockedWeapons.Clear();
        inventoryManager.equippedWeapon = null;
        inventoryManager.knownRecipes.Clear();
        inventoryManager.knownRecipes.Add(recetaBombaChile);
        inventoryManager.knownRecipes.Add(recetaVendajeRudo);
        EditorUtility.SetDirty(inventoryManager);

        // 5. Crear Canvas principal (1920x1080)
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Colores sobrios donde predomina el negro
        Color pureBlack = new Color(0.02f, 0.02f, 0.025f, 1f);
        Color matteBlack = new Color(0.05f, 0.05f, 0.06f, 1f);
        Color subtleBorder = new Color(0.26f, 0.26f, 0.30f, 1f);
        Color targetGoldBorder = new Color(0.92f, 0.68f, 0.22f, 1f);
        Color boneWhite = new Color(0.90f, 0.88f, 0.84f, 1f);
        Color mutedGrayText = new Color(0.65f, 0.66f, 0.70f, 1f);

        // =====================================================================
        // 6. CONSTRUIR COMBAT PANEL (2 Enemigos frente a cámara, Negro Predominante)
        // =====================================================================
        GameObject battleManagerGO = new GameObject("BattleManager");
        BattleManager battleManager = battleManagerGO.AddComponent<BattleManager>();

        GameObject combatPanel = CreatePanel("CombatPanel", canvasGO.transform, pureBlack);

        // Fondo de combate (escena1Combate)
        string combatBgPath = "Assets/Sprites/escena1Combate.png";
        Sprite combatBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(combatBgPath);
        if (combatBgSprite == null)
        {
            UnityEngine.Object[] subs = AssetDatabase.LoadAllAssetsAtPath(combatBgPath);
            foreach (var o in subs)
            {
                if (o is Sprite s) { combatBgSprite = s; break; }
            }
        }
        if (combatBgSprite != null)
        {
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
            bgImg.sprite = combatBgSprite;
            bgImg.color = Color.white;
            bgImg.preserveAspect = false;
            viewportGO.transform.SetAsFirstSibling();
            battleManager.combatBackgroundImage = bgImg;

            GameObject dividerGO = CreatePanel("CombatViewportDivider", combatPanel.transform, subtleBorder);
            SetAnchors(dividerGO.GetComponent<RectTransform>(), new Vector2(0f, 0.437f), new Vector2(1f, 0.442f));
            dividerGO.transform.SetSiblingIndex(1);
        }

        // --- ZONA SUPERIOR (y: 0.48 a 0.98): LOS 2 SLOTS DE ENEMIGOS FRENTE A LA CÁMARA (Sin rectángulos, con indicador dinámico) ---
        EnemyUISlot slot0 = CreateEnemyUISlot(
            "EnemySlot_0",
            combatPanel.transform,
            new Vector2(0.08f, 0.48f),
            new Vector2(0.48f, 0.98f),
            "> [OBJETIVO] LACAYO RAPIDO Y DEBIL (MOT: 10)",
            "HP: 45 / 45",
            new Color(0.75f, 0.38f, 0.10f, 1f),
            "[CLIC PARA APUNTAR]\nLACAYO RAPIDO Y DEBIL",
            targetGoldBorder,
            matteBlack,
            subtleBorder,
            pixelFont
        );

        EnemyUISlot slot1 = CreateEnemyUISlot(
            "EnemySlot_1",
            combatPanel.transform,
            new Vector2(0.52f, 0.48f),
            new Vector2(0.92f, 0.98f),
            "LACAYO LENTO Y FUERTE (MOT: 6)",
            "HP: 90 / 90",
            new Color(0.55f, 0.10f, 0.10f, 1f),
            "[CLIC PARA APUNTAR]\nLACAYO LENTO Y FUERTE",
            subtleBorder,
            matteBlack,
            subtleBorder,
            pixelFont
        );

        // --- ZONA MEDIA (y: 0.29 a 0.51): REGISTRO DE COMBATE Y ANIMACIONES GIF (DEBAJO DE ENEMIGOS, ARRIBA DE OPCIONES) ---
        GameObject battleLogBorder = CreatePanel("BattleLogBorder", combatPanel.transform, subtleBorder);
        SetAnchors(battleLogBorder.GetComponent<RectTransform>(), new Vector2(0.08f, 0.29f), new Vector2(0.92f, 0.51f));

        GameObject battleLogBox = CreatePanel("BattleLogBox", battleLogBorder.transform, matteBlack);
        SetAnchors(battleLogBox.GetComponent<RectTransform>(), new Vector2(0.003f, 0.012f), new Vector2(0.997f, 0.988f));

        TextMeshProUGUI battleLogText = CreateTMPText(
            "BattleLogText",
            battleLogBox.transform,
            "TU TURNO (Objetivo: Lacayo Rapido) | Cola: Enrique(12) > Lacayo Rapido(10) > Lacayo Lento(6)",
            38,
            TextAlignmentOptions.Center,
            pixelFont
        );
        battleLogText.color = boneWhite;
        SetAnchors(battleLogText.rectTransform, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f));

        // ActionGifPanel ubicado EXACTAMENTE debajo de los enemigos y arriba de las opciones (y: 0.29 a 0.51)
        GameObject actionGifPanel = CreatePanel("ActionGifPanel", combatPanel.transform, pureBlack);
        SetAnchors(actionGifPanel.GetComponent<RectTransform>(), new Vector2(0.08f, 0.29f), new Vector2(0.92f, 0.51f));

        GameObject actionGifBorder = CreatePanel("ActionGifBorder", actionGifPanel.transform, new Color(0.65f, 0.62f, 0.55f, 1f));
        SetAnchors(actionGifBorder.GetComponent<RectTransform>(), new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f));

        GameObject actionGifImageGO = CreatePanel("ActionGifImage", actionGifBorder.transform, new Color(0.22f, 0.08f, 0.08f, 1f));
        Image actionGifImage = actionGifImageGO.GetComponent<Image>();
        SetAnchors(actionGifImageGO.GetComponent<RectTransform>(), new Vector2(0.004f, 0.015f), new Vector2(0.996f, 0.985f));

        TextMeshProUGUI actionGifLabelText = CreateTMPText(
            "ActionGifLabelText",
            actionGifImageGO.transform,
            "[GIF: CORPOREO - Golpe fisico directo]",
            44,
            TextAlignmentOptions.Center,
            pixelFont
        );
        actionGifLabelText.color = boneWhite;
        SetAnchors(actionGifLabelText.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
        actionGifPanel.SetActive(false);

        // --- ZONA INFERIOR (y: 0.03 a 0.26): ESTADO DE ENRIQUE + LAS 4 OPCIONES ---
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

        Button corporeoButton = CreateNoirButton(
            "CorporeoButton",
            combatPanel.transform,
            "CORPOREO\nAtacar (Fisico)",
            matteBlack,
            new Color(0.65f, 0.18f, 0.18f, 1f),
            36,
            pixelFont
        );
        SetAnchors(corporeoButton.GetComponent<RectTransform>(), new Vector2(0.03f, 0.03f), new Vector2(0.255f, 0.19f));

        Button intelectoButton = CreateNoirButton(
            "IntelectoButton",
            combatPanel.transform,
            "INTELECTO\nObjeto / Improvisar",
            matteBlack,
            new Color(0.18f, 0.42f, 0.68f, 1f),
            36,
            pixelFont
        );
        SetAnchors(intelectoButton.GetComponent<RectTransform>(), new Vector2(0.268f, 0.03f), new Vector2(0.493f, 0.19f));

        Button motricidadButton = CreateNoirButton(
            "MotricidadButton",
            combatPanel.transform,
            "MOTRICIDAD\nEsquivar / Defender",
            matteBlack,
            new Color(0.68f, 0.52f, 0.14f, 1f),
            36,
            pixelFont
        );
        SetAnchors(motricidadButton.GetComponent<RectTransform>(), new Vector2(0.507f, 0.03f), new Vector2(0.732f, 0.19f));

        Button mundoInternoButton = CreateNoirButton(
            "MundoInternoButton",
            combatPanel.transform,
            "MUNDO INTERNO\nEfecto de Estado",
            matteBlack,
            new Color(0.48f, 0.20f, 0.65f, 1f),
            36,
            pixelFont
        );
        SetAnchors(mundoInternoButton.GetComponent<RectTransform>(), new Vector2(0.745f, 0.03f), new Vector2(0.97f, 0.19f));

        UnityEventTools.AddPersistentListener(corporeoButton.onClick, battleManager.OnCorporeoButton);
        UnityEventTools.AddPersistentListener(intelectoButton.onClick, battleManager.OnIntelectoButton);
        UnityEventTools.AddPersistentListener(motricidadButton.onClick, battleManager.OnMotricidadButton);
        UnityEventTools.AddPersistentListener(mundoInternoButton.onClick, battleManager.OnMundoInternoButton);

        // --- SUB-PANEL DE INTELECTO Y OBJETOS CURATIVOS (IntelectoAndItemsPanel) ---
        GameObject intelectoAndItemsPanel = CreatePanel("IntelectoAndItemsPanel", combatPanel.transform, new Color(0.20f, 0.48f, 0.82f, 1f));
        SetAnchors(intelectoAndItemsPanel.GetComponent<RectTransform>(), new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.53f));

        GameObject intelectoInnerBox = CreatePanel("IntelectoInnerBox", intelectoAndItemsPanel.transform, new Color(0.03f, 0.03f, 0.04f, 0.99f));
        SetAnchors(intelectoInnerBox.GetComponent<RectTransform>(), new Vector2(0.003f, 0.008f), new Vector2(0.997f, 0.992f));

        TextMeshProUGUI intelectoTitleText = CreateTMPText(
            "IntelectoTitleText",
            intelectoInnerBox.transform,
            "SECCION 1: USAR OBJETOS CURATIVOS  |  SECCION 2: CRAFTEAR CON INTELECTO",
            34,
            TextAlignmentOptions.Center,
            pixelFont
        );
        intelectoTitleText.color = new Color(0.32f, 0.66f, 0.98f, 1f);
        SetAnchors(intelectoTitleText.rectTransform, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.98f));

        TextMeshProUGUI intelectoInventorySummaryText = CreateTMPText(
            "InventorySummaryText",
            intelectoInnerBox.transform,
            "INVENTARIO: Refresco x1, Torta x1, Botella Vacia x1, Gasolina x1",
            32,
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
            32,
            pixelFont
        );
        SetAnchors(cancelIntelectoButton.GetComponent<RectTransform>(), new Vector2(0.22f, 0.025f), new Vector2(0.78f, 0.17f));
        UnityEventTools.AddPersistentListener(cancelIntelectoButton.onClick, battleManager.CloseIntelectoPanel);

        CombatIntelectoUI intelectoUI = intelectoAndItemsPanel.AddComponent<CombatIntelectoUI>();
        intelectoUI.battleManager = battleManager;
        intelectoUI.panelRoot = intelectoAndItemsPanel;
        intelectoUI.titleText = intelectoTitleText;
        intelectoUI.inventorySummaryText = intelectoInventorySummaryText;
        intelectoUI.dynamicButtonsContainer = dynamicButtonsRT;
        intelectoUI.cancelButton = cancelIntelectoButton;
        intelectoUI.customFont = pixelFont;

        intelectoAndItemsPanel.SetActive(false);

        // --- SUB-PANEL DE MUNDO INTERNO (MundoInternoPanel) ---
        Color violetBorder = new Color(0.58f, 0.26f, 0.78f, 1f);
        GameObject mundoInternoPanel = CreatePanel("MundoInternoPanel", combatPanel.transform, violetBorder);
        SetAnchors(mundoInternoPanel.GetComponent<RectTransform>(), new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.53f));

        GameObject mundoInternoInnerBox = CreatePanel("MundoInternoInnerBox", mundoInternoPanel.transform, new Color(0.03f, 0.03f, 0.04f, 0.99f));
        SetAnchors(mundoInternoInnerBox.GetComponent<RectTransform>(), new Vector2(0.003f, 0.008f), new Vector2(0.997f, 0.992f));

        TextMeshProUGUI mundoInternoTitleText = CreateTMPText(
            "MundoInternoTitleText",
            mundoInternoInnerBox.transform,
            "MUNDO INTERNO: PSICOLOGIA Y EMOCIONES DE ENRIQUE",
            34,
            TextAlignmentOptions.Center,
            pixelFont
        );
        mundoInternoTitleText.color = new Color(0.78f, 0.45f, 0.98f, 1f);
        SetAnchors(mundoInternoTitleText.rectTransform, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.98f));

        TextMeshProUGUI mundoInternoDescText = CreateTMPText(
            "MundoInternoDescriptionText",
            mundoInternoInnerBox.transform,
            "Elige como influir en la psique del combate (no gasta turno si cancelas):",
            26,
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
            28,
            pixelFont
        );
        SetAnchors(insultarBtn.GetComponent<RectTransform>(), new Vector2(0.05f, 0.53f), new Vector2(0.95f, 0.70f));

        Button animarseBtn = CreateNoirButton(
            "AnimarseButton",
            mundoInternoInnerBox.transform,
            "[2. ANIMARSE] - Enrique se auto-sugestiona (+5 ATQ para el proximo golpe Corporeo)",
            new Color(0.07f, 0.06f, 0.03f, 1f),
            new Color(0.92f, 0.72f, 0.22f, 1f),
            28,
            pixelFont
        );
        SetAnchors(animarseBtn.GetComponent<RectTransform>(), new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.51f));

        Button delirioBtn = CreateNoirButton(
            "DelirioButton",
            mundoInternoInnerBox.transform,
            "[3. DELIRIO PSIQUICO] - Confunde al rival haciendole perder su proximo turno",
            new Color(0.05f, 0.03f, 0.07f, 1f),
            violetBorder,
            28,
            pixelFont
        );
        SetAnchors(delirioBtn.GetComponent<RectTransform>(), new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.33f));

        Button cancelMundoInternoButton = CreateNoirButton(
            "CancelMundoInternoButton",
            mundoInternoInnerBox.transform,
            "CANCELAR / VOLVER A LAS 4 VOCES (NO GASTA TURNO)",
            matteBlack,
            subtleBorder,
            26,
            pixelFont
        );
        SetAnchors(cancelMundoInternoButton.GetComponent<RectTransform>(), new Vector2(0.22f, 0.02f), new Vector2(0.78f, 0.14f));

        CombatMundoInternoUI mundoInternoUI = mundoInternoPanel.AddComponent<CombatMundoInternoUI>();
        mundoInternoUI.battleManager = battleManager;
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

        mundoInternoPanel.SetActive(false);

        // --- DIÁLOGOS EN COMBATE: Mismo diseño de Conversación Normal (Portrait + Diálogo) superpuesto arriba del menú de acciones ---
        GameObject combatDialoguePanel = CreatePanel("CombatDialoguePanel", combatPanel.transform, subtleBorder);
        SetAnchors(combatDialoguePanel.GetComponent<RectTransform>(), new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.36f));

        GameObject combatDialogueInner = CreatePanel("CombatDialogueBoxBG", combatDialoguePanel.transform, new Color(0.04f, 0.04f, 0.05f, 0.99f));
        SetAnchors(combatDialogueInner.GetComponent<RectTransform>(), new Vector2(0.002f, 0.008f), new Vector2(0.998f, 0.992f));

        GameObject combatPortraitContainer = CreatePanel("CombatPortraitContainer", combatDialogueInner.transform, subtleBorder);
        SetAnchors(combatPortraitContainer.GetComponent<RectTransform>(), new Vector2(0.015f, 0.07f), new Vector2(0.185f, 0.93f));

        GameObject combatPortraitGO = CreatePanel("CombatPortraitImage", combatPortraitContainer.transform, new Color(0.38f, 0.40f, 0.43f, 1f));
        Image combatPortraitImage = combatPortraitGO.GetComponent<Image>();
        SetAnchors(combatPortraitGO.GetComponent<RectTransform>(), new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.97f));

        TextMeshProUGUI combatPortraitLabelText = CreateTMPText(
            "CombatPortraitLabelText",
            combatPortraitGO.transform,
            "RETRATO",
            34,
            TextAlignmentOptions.Center,
            pixelFont
        );
        combatPortraitLabelText.color = Color.white;
        SetAnchors(combatPortraitLabelText.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));

        TextMeshProUGUI combatSpeakerNameText = CreateTMPText(
            "CombatSpeakerNameText",
            combatDialogueInner.transform,
            "VOZ: CORPOREO",
            50,
            TextAlignmentOptions.BottomLeft,
            pixelFont
        );
        combatSpeakerNameText.color = boneWhite;
        SetAnchors(combatSpeakerNameText.rectTransform, new Vector2(0.205f, 0.73f), new Vector2(0.815f, 0.95f));

        TextMeshProUGUI combatDialogueText = CreateTMPText(
            "CombatDialogueText",
            combatDialogueInner.transform,
            "Texto de dialogo en medio del combate...",
            44,
            TextAlignmentOptions.TopLeft,
            pixelFont
        );
        combatDialogueText.color = boneWhite;
        SetAnchors(combatDialogueText.rectTransform, new Vector2(0.205f, 0.06f), new Vector2(0.815f, 0.70f));

        Button combatNextDialogueButton = CreateNoirButton(
            "CombatNextDialogueButton",
            combatDialogueInner.transform,
            "SIGUIENTE >>\n(ESPACIO)",
            matteBlack,
            subtleBorder,
            34,
            pixelFont
        );
        SetAnchors(combatNextDialogueButton.GetComponent<RectTransform>(), new Vector2(0.83f, 0.18f), new Vector2(0.985f, 0.82f));
        UnityEventTools.AddPersistentListener(combatNextDialogueButton.onClick, battleManager.AdvanceCombatDialogue);

        combatDialoguePanel.SetActive(false);

        // Conectar todas las referencias al BattleManager
        battleManager.playerStats = playerStats;
        battleManager.playerController = playerController;
        battleManager.playerMovementScript = playerController;
        battleManager.currentEnemy = lacayoRapido;
        battleManager.battlePanel = combatPanel;
        battleManager.enemySlots = new EnemyUISlot[] { slot0, slot1 };
        battleManager.enemyPlaceholderImage = slot0.enemyPlaceholderImage;
        battleManager.enemyAvatarLabelText = slot0.enemyAvatarLabelText;
        battleManager.enemyNameText = slot0.enemyNameText;
        battleManager.enemyHPText = slot0.enemyHPText;
        battleManager.playerHPText = playerHPText;
        battleManager.battleLogText = battleLogText;
        battleManager.corporeoButton = corporeoButton;
        battleManager.intelectoButton = intelectoButton;
        battleManager.motricidadButton = motricidadButton;
        battleManager.mundoInternoButton = mundoInternoButton;
        battleManager.actionGifPanel = actionGifPanel;
        battleManager.actionGifImage = actionGifImage;
        battleManager.actionGifLabelText = actionGifLabelText;
        battleManager.combatDialoguePanel = combatDialoguePanel;
        battleManager.combatPortraitContainer = combatPortraitContainer;
        battleManager.combatPortraitImage = combatPortraitImage;
        battleManager.combatPortraitLabelText = combatPortraitLabelText;
        battleManager.combatSpeakerNameText = combatSpeakerNameText;
        battleManager.combatDialogueText = combatDialogueText;
        battleManager.combatNextDialogueButton = combatNextDialogueButton;
        LoadPersonalityPortraits(
            out Sprite corporeoPortrait,
            out Sprite intelectoPortrait,
            out Sprite motricidadPortrait,
            out Sprite mundoInternoPortrait
        );
        battleManager.corporeoPortrait = corporeoPortrait;
        battleManager.intelectoPortrait = intelectoPortrait;
        battleManager.motricidadPortrait = motricidadPortrait;
        battleManager.mundoInternoPortrait = mundoInternoPortrait;
        battleManager.intelectoAndItemsPanel = intelectoAndItemsPanel;
        battleManager.intelectoCraftingPanel = intelectoAndItemsPanel;
        battleManager.intelectoCraftingUI = intelectoUI;
        battleManager.mundoInternoPanel = mundoInternoPanel;
        battleManager.mundoInternoUI = mundoInternoUI;

        combatPanel.SetActive(false);
        EditorUtility.SetDirty(battleManager);
        EditorUtility.SetDirty(intelectoUI);
        EditorUtility.SetDirty(mundoInternoUI);

        // =====================================================================
        // 7. CREAR LOS 2 ENCUENTROS SEPARADOS EN EL MAPA (Lacayo_Tutorial y Lacayo_Segundo)
        // =====================================================================
        // Encuentro 1: Lacayo_Tutorial (Arriba a la derecha, pelea 1v1 con diálogo tutorial)
        GameObject lacayoGO = new GameObject("Lacayo_Tutorial");
        lacayoGO.transform.position = new Vector3(2.8f, 1.5f, 0f);
        lacayoGO.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        SpriteRenderer lacayoSR = lacayoGO.AddComponent<SpriteRenderer>();
        lacayoSR.sprite = whiteSquareSprite;
        lacayoSR.color = new Color(1.0f, 0.55f, 0.10f, 1.0f); // Naranja (Rápido)
        lacayoSR.sortingOrder = 2;

        BoxCollider2D lacayoCol = lacayoGO.AddComponent<BoxCollider2D>();
        lacayoCol.isTrigger = true;
        lacayoCol.size = new Vector2(1.4f, 1.4f);

        string[] tutorialIntroLines = new string[]
        {
            "LACAYO DE EL CALACAS: \"Quieto ahi, repartidor! Ese paquete no va a llegar a su destino.\"",
            "VOZ [CORPOREO]: \"Miralo bien, Enrique. Es debil. Usa mi boton (CORPOREO) para romperle la guardia de un golpe directo!\"",
            "VOZ [INTELECTO]: \"Si nos topamos con alguien de defensa alta, usa mi boton (INTELECTO) para improvisar un artefacto con chatarra del mapa.\"",
            "VOZ [MOTRICIDAD]: \"Somos mas rapidos que el (Motricidad 12 vs 10). Mi boton (MOTRICIDAD) nos prepara para esquivar o reducir el dano un 70%!\"",
            "VOZ [MUNDO INTERNO]: \"Y si quieres que pierda su turno en la cola, usa mi boton (MUNDO INTERNO) para confundir su mente.\""
        };

        ItemData cadenaWeapon = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_CadenaBicicleta.asset");
        battleManager.starterRewardWeapon = cadenaWeapon;

        EnemyEncounterTrigger trigger1 = lacayoGO.AddComponent<EnemyEncounterTrigger>();
        trigger1.enemies = new EnemyData[] { lacayoRapido };
        trigger1.enemyData = lacayoRapido;
        trigger1.mapObjectsToDisableOnWin = new GameObject[] { lacayoGO };
        trigger1.battleManager = battleManager;
        trigger1.introDialogueLines = tutorialIntroLines;
        trigger1.rewardItemOnWin = cadenaWeapon;
        trigger1.rewardDialogueLines = new string[]
        {
            "NARRADOR: \"¡El lacayo derrotado cae contra el pavimento y suelta una pesada CADENA DE BICICLETA con eslabones de acero engrasado! Enrique la recoge del asfalto.\"",
            "VOZ [CORPOREO]: \"¡Nuestra primera arma callejera! Con esta Cadena de Bicicleta (+4 ATQ, +2 Motricidad) nuestros golpes seran mas letales y rapidos.\"",
            "VOZ [INTELECTO]: \"¡CADENA DE BICICLETA OBTENIDA Y EQUIPADA! Se ha colocado en tus manos. Puedes abrir el menú [INVENTARIO / TAB] en cualquier momento para ver tu equipamiento, o hacer clic en tu retrato para cambiarla.\""
        };
        EditorUtility.SetDirty(trigger1);

        // Encuentro 2: Lacayo_Segundo (Abajo más a la derecha, pelea separada 2 contra 1 en la misma pantalla con otro diálogo)
        GameObject lacayoSegundoGO = new GameObject("Lacayo_Segundo");
        lacayoSegundoGO.transform.position = new Vector3(5.8f, -1.5f, 0f);
        lacayoSegundoGO.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

        SpriteRenderer lacayoSegundoSR = lacayoSegundoGO.AddComponent<SpriteRenderer>();
        lacayoSegundoSR.sprite = whiteSquareSprite;
        lacayoSegundoSR.color = new Color(0.65f, 0.14f, 0.14f, 1.0f); // Rojo Oscuro (Grupo de 2 Lacayos)
        lacayoSegundoSR.sortingOrder = 2;

        BoxCollider2D lacayoSegundoCol = lacayoSegundoGO.AddComponent<BoxCollider2D>();
        lacayoSegundoCol.isTrigger = true;
        lacayoSegundoCol.size = new Vector2(1.4f, 1.4f);

        string[] secondEncounterLines = new string[]
        {
            "LACAYO FUERTE: \"Mira quien viene ahi... Dejaste tirado a nuestro companero, pero contra nosotros dos no vas a poder.\"",
            "LACAYO RAPIDO: \"Rodealo! Yo lo distraigo rapido y tu lo aplastas!\"",
            "VOZ [MOTRICIDAD]: \"Son dos en la misma pelea. Haz clic sobre cualquiera de los dos arriba para cambiar de objetivo.\""
        };

        EnemyEncounterTrigger trigger2 = lacayoSegundoGO.AddComponent<EnemyEncounterTrigger>();
        trigger2.enemies = new EnemyData[] { lacayoRapido, lacayoFuerte };
        trigger2.enemyData = lacayoFuerte;
        trigger2.mapObjectsToDisableOnWin = new GameObject[] { lacayoSegundoGO };
        trigger2.battleManager = battleManager;
        trigger2.introDialogueLines = secondEncounterLines;
        EditorUtility.SetDirty(trigger2);

        // =====================================================================
        // 7b. CREAR EXPLORATION DIALOGUE PANEL Y OBJETOS INTERACTUABLES EN LA CALLE
        // =====================================================================
        GameObject explorationDialogueGO = new GameObject("ExplorationDialogueManager");
        ExplorationDialogueManager explorationDialogueManager = explorationDialogueGO.AddComponent<ExplorationDialogueManager>();

        GameObject explorationDialoguePanel = CreatePanel("ExplorationDialoguePanel", canvasGO.transform, subtleBorder);
        SetAnchors(explorationDialoguePanel.GetComponent<RectTransform>(), new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.34f));

        GameObject expDialogueBox = CreatePanel("ExplorationDialogueBox", explorationDialoguePanel.transform, matteBlack);
        SetAnchors(expDialogueBox.GetComponent<RectTransform>(), new Vector2(0.003f, 0.015f), new Vector2(0.997f, 0.985f));

        GameObject expPortraitContainer = CreatePanel("ExplorationPortraitContainer", expDialogueBox.transform, subtleBorder);
        SetAnchors(expPortraitContainer.GetComponent<RectTransform>(), new Vector2(0.015f, 0.10f), new Vector2(0.185f, 0.90f));

        GameObject expPortraitImgGO = CreatePanel("ExplorationPortraitImage", expPortraitContainer.transform, new Color(0.38f, 0.39f, 0.42f, 1f));
        Image expPortraitImage = expPortraitImgGO.GetComponent<Image>();
        SetAnchors(expPortraitImgGO.GetComponent<RectTransform>(), new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));

        TextMeshProUGUI expPortraitLabelText = CreateTMPText(
            "ExplorationPortraitLabelText",
            expPortraitImgGO.transform,
            "VOZ\n[INTELECTO]",
            34,
            TextAlignmentOptions.Center,
            pixelFont
        );
        expPortraitLabelText.color = boneWhite;
        SetAnchors(expPortraitLabelText.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));

        TextMeshProUGUI expSpeakerNameText = CreateTMPText(
            "ExplorationSpeakerNameText",
            expDialogueBox.transform,
            "VOZ: INTELECTO",
            40,
            TextAlignmentOptions.BottomLeft,
            pixelFont
        );
        expSpeakerNameText.color = new Color(0.20f, 0.48f, 0.82f, 1f);
        SetAnchors(expSpeakerNameText.rectTransform, new Vector2(0.205f, 0.72f), new Vector2(0.81f, 0.92f));

        TextMeshProUGUI expSpeakerAndText = CreateTMPText(
            "SpeakerAndText",
            expDialogueBox.transform,
            "No es basura, es gasolina inflamable para una Molotov. Guardalo.",
            40,
            TextAlignmentOptions.TopLeft,
            pixelFont
        );
        expSpeakerAndText.color = boneWhite;
        SetAnchors(expSpeakerAndText.rectTransform, new Vector2(0.205f, 0.22f), new Vector2(0.81f, 0.70f));

        TextMeshProUGUI expContinueIndicator = CreateTMPText(
            "ContinueIndicatorText",
            expDialogueBox.transform,
            "Presiona ESPACIO / E / CLIC para continuar",
            30,
            TextAlignmentOptions.BottomLeft,
            pixelFont
        );
        expContinueIndicator.color = mutedGrayText;
        SetAnchors(expContinueIndicator.rectTransform, new Vector2(0.205f, 0.05f), new Vector2(0.81f, 0.20f));

        Button expNextButton = CreateNoirButton(
            "ExplorationNextButton",
            expDialogueBox.transform,
            "CONTINUAR\n[ESPACIO / E]",
            matteBlack,
            new Color(0.55f, 0.55f, 0.60f, 1f),
            34,
            pixelFont
        );
        SetAnchors(expNextButton.GetComponent<RectTransform>(), new Vector2(0.83f, 0.18f), new Vector2(0.985f, 0.82f));
        UnityEventTools.AddPersistentListener(expNextButton.onClick, explorationDialogueManager.AdvanceDialogue);

        explorationDialoguePanel.SetActive(false);

        explorationDialogueManager.explorationDialoguePanel = explorationDialoguePanel;
        explorationDialogueManager.speakerAndText = expSpeakerAndText;
        explorationDialogueManager.continueIndicatorText = expContinueIndicator;
        explorationDialogueManager.nextLineButton = expNextButton;
        explorationDialogueManager.portraitContainer = expPortraitContainer;
        explorationDialogueManager.portraitImage = expPortraitImage;
        explorationDialogueManager.portraitLabelText = expPortraitLabelText;
        explorationDialogueManager.speakerNameText = expSpeakerNameText;
        explorationDialogueManager.corporeoPortrait = corporeoPortrait;
        explorationDialogueManager.intelectoPortrait = intelectoPortrait;
        explorationDialogueManager.motricidadPortrait = motricidadPortrait;
        explorationDialogueManager.mundoInternoPortrait = mundoInternoPortrait;
        explorationDialogueManager.playerController = playerController;
        EditorUtility.SetDirty(explorationDialogueManager);

        // 4. Objetos recolectables e interactuables en el mapa (entre Enrique y el Lacayo)
        CreateMapInteractableInScene(
            "Pickup_Refresco_Curativo",
            new Vector3(-2.1f, 1.3f, 0f),
            whiteSquareSprite,
            new Color(0.35f, 0.95f, 0.48f, 1f), // Cuadro verde claro
            InteractableType.PickupItem,
            itemRefresco,
            1,
            new string[]
            {
                "NARRADOR: \"Encuentras una botella cerrada de Refresco de Cola helada dentro de una hielera abandonada.\"",
                "VOZ [CORPOREO]: \"Azucar liquida y cafeina! Guardalo ya, con un trago de esto nos curamos 35 de vida en medio de los golpes!\""
            },
            null,
            explorationDialogueManager,
            pixelFont
        );

        CreateMapInteractableInScene(
            "Pickup_Botella",
            new Vector3(-0.8f, 0.6f, 0f),
            whiteSquareSprite,
            new Color(0.45f, 0.82f, 0.32f, 1f), // Pequeño cuadro verde
            InteractableType.PickupItem,
            itemBotellaVacia,
            1,
            new string[]
            {
                "VOZ [CORPOREO]: \"Una botella vacia tirada en la banqueta. Basura.\"",
                "VOZ [INTELECTO]: \"No es basura, es un envase de vidrio templado (Botella Vacia). Guardalo para fabricar una Molotov o un Vendaje Rudo en combate.\""
            },
            null,
            explorationDialogueManager,
            pixelFont
        );

        CreateMapInteractableInScene(
            "Pickup_Chile",
            new Vector3(-0.06f, 2.50f, 0f),
            whiteSquareSprite,
            new Color(0.95f, 0.82f, 0.18f, 1f),
            InteractableType.PickupItem,
            itemChilePolvo,
            1,
            new string[]
            {
                "VOZ [CORPOREO]: \"Un bote con gasolina y un trapo tirados en el suelo.\"",
                "VOZ [INTELECTO]: \"Combustible inflamable (Gasolina). Junto con la Botella Vacia podremos fabricar una Molotov para quemar y herir a cualquier lacayo ignorando su defensa.\""
            },
            null,
            explorationDialogueManager,
            pixelFont
        );

        CreateMapInteractableInScene(
            "Pickup_Torta_Callejera",
            new Vector3(4.20f, 1.05f, 0f),
            whiteSquareSprite,
            new Color(0.88f, 0.65f, 0.25f, 1f),
            InteractableType.PickupItem,
            itemTorta,
            1,
            new string[]
            {
                "NARRADOR: \"Una torta callejera envuelta en papel estraza, fresca y con olor a adobo sobre una banca.\"",
                "VOZ [CORPOREO]: \"Comida de verdad! Devorarla en combate recuperara 70 puntos de vida enteros. Guardala bien en la mochila.\""
            },
            null,
            explorationDialogueManager,
            pixelFont
        );

        ItemData itemCinta = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Cinta_Canela.asset");
        CreateMapInteractableInScene(
            "Pickup_Cinta_Canela",
            new Vector3(10.50f, 0.85f, 0f),
            whiteSquareSprite,
            new Color(0.72f, 0.52f, 0.24f, 1f),
            InteractableType.PickupItem,
            itemCinta,
            1,
            new string[]
            {
                "NARRADOR: \"Un rollo de cinta canela industrial tirado junto a unas cajas de carton abandonadas.\"",
                "VOZ [INTELECTO]: \"Cinta adhesiva resistente (Cinta Canela). Nos servira con una Botella Vacia para fabricar un Vendaje Rudo improvisado (+50 HP)!\""
            },
            null,
            explorationDialogueManager,
            pixelFont
        );

        ItemData itemTubo = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_TuboPlomo.asset");
        CreateMapInteractableInScene(
            "Pickup_Tubo_Plomo",
            new Vector3(14.20f, 1.00f, 0f),
            whiteSquareSprite,
            new Color(0.58f, 0.62f, 0.66f, 1f),
            InteractableType.PickupItem,
            itemTubo,
            1,
            new string[]
            {
                "NARRADOR: \"Un tubo de plomo solido y oxidado, arrancado de las tuberias de drenaje de Santa Felipina.\"",
                "VOZ [CORPOREO]: \"Pesa como demonio, pero multiplica la contundencia de nuestro golpe (+6 ATQ). Nadie se levantara de un golpe con esto.\"",
                "VOZ [INTELECTO]: \"Pesado pero efectivo. Recuerda entrar a la pestana de Armas para equiparlo en tus manos.\""
            },
            null,
            explorationDialogueManager,
            pixelFont
        );

        CreateMapInteractableInScene(
            "Pickup_Chile (1)",
            new Vector3(23.84f, 0.81f, 0f),
            whiteSquareSprite,
            new Color(0.95f, 0.82f, 0.18f, 1f),
            InteractableType.PickupItem,
            itemChilePolvo,
            1,
            new string[]
            {
                "VOZ [CORPOREO]: \"Gasolina adicional abandonada en la esquina.\""
            },
            null,
            explorationDialogueManager,
            pixelFont
        );

        CreateMapInteractableInScene(
            "Pickup_Botella (1)",
            new Vector3(24.78f, 0.71f, 0f),
            whiteSquareSprite,
            new Color(0.45f, 0.82f, 0.32f, 1f),
            InteractableType.PickupItem,
            itemBotellaVacia,
            1,
            new string[]
            {
                "VOZ [INTELECTO]: \"Otra botella de vidrio templado. Mas material para craftear.\""
            },
            null,
            explorationDialogueManager,
            pixelFont
        );

        CreateMapInteractableInScene(
            "Puerta_CasaEquivocada",
            new Vector3(-1.6f, -1.7f, 0f),
            whiteSquareSprite,
            new Color(0.48f, 0.28f, 0.14f, 1f), // Cuadro café
            InteractableType.DialogueOnly,
            null,
            0,
            null,
            new string[]
            {
                "NARRADOR: \"Enrique deja el paquete frente a la puerta de madera descascarada y toca el timbre.\"",
                "VOZ [INTELECTO]: \"Espera un segundo... El recibo dice Callejon del Perro #14, y esta es la #41. Te equivocaste de casa otra vez!\"",
                "VOZ [CORPOREO]: \"Patada a la puerta, agarramos la caja y fingimos que aqui no paso nada.\"",
                "VOZ [MOTRICIDAD]: \"Demasiado tarde para correr sin hacer ruido. Ya nos vieron desde la esquina.\"",
                "VOZ [MUNDO INTERNO]: \"Esa caja no contiene un envio comun... El aire huele a polvora y a deuda ajena.\""
            },
            explorationDialogueManager,
            pixelFont
        );

        // Dejar estado inicial del mapa listo para jugar (sin paneles de intro encima)
        combatPanel.SetActive(false);
        actionGifPanel.SetActive(false);
        combatDialoguePanel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        // 8. Construir la escena separada de Menú e Introducción (Assets/Scenes/00_MenuIntro.unity)
        BuildMenuSceneAndReturnToMap(pixelFont);

        Debug.Log("[RPG Santa Felipina] Escenas separadas listas: 00_MenuIntro (Menú + Intro) y 01_Nivel1_SantaFelipina (Mapa + Combate).");
    }

    [MenuItem("RPG Santa Felipina/Separar Menu a MenuScene (Sin Reiniciar Mapa)")]
    public static void SeparateMenuIntoOwnScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        string sampleScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";
        if (File.Exists(sampleScenePath))
        {
            EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);
        }

        // Eliminar los objetos de menú/intro de SampleScene sin tocar el mapa ni enemigos
        DestroyExistingIfFound("StartScreenPanel");
        DestroyExistingIfFound("SlideshowPanel");
        DestroyExistingIfFound("IntroSlideshowManager");

        // Asegurar que Enrique pueda moverse desde el inicio y se dibuje por encima del mapa (Order in Layer = 5)
        PlayerController2D playerController = UnityEngine.Object.FindAnyObjectByType<PlayerController2D>();
        if (playerController != null)
        {
            playerController.canMove = true;
            playerController.enabled = true;
            SpriteRenderer playerSR = (playerController.spriteRenderer != null) ? playerController.spriteRenderer : playerController.GetComponentInChildren<SpriteRenderer>();
            if (playerSR != null)
            {
                playerSR.sortingOrder = 5;
                EditorUtility.SetDirty(playerSR);
            }
            EditorUtility.SetDirty(playerController);
        }

        // Ajustar Order in Layer de los enemigos en el mapa (5)
        EnemyEncounterTrigger[] triggers = UnityEngine.Object.FindObjectsByType<EnemyEncounterTrigger>(FindObjectsInactive.Include);
        foreach (EnemyEncounterTrigger trig in triggers)
        {
            SpriteRenderer sr = trig.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = 5;
                EditorUtility.SetDirty(sr);
            }
        }

        // Ajustar Order in Layer de los objetos interactuables del mapa (4) y su prompt [E] (6)
        MapInteractable[] interactables = UnityEngine.Object.FindObjectsByType<MapInteractable>(FindObjectsInactive.Include);
        foreach (MapInteractable inter in interactables)
        {
            SpriteRenderer sr = inter.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = 4;
                EditorUtility.SetDirty(sr);
            }
            if (inter.promptUI != null)
            {
                TextMeshPro tmp = inter.promptUI.GetComponent<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.sortingOrder = 6;
                    EditorUtility.SetDirty(tmp);
                }
            }
        }

        // Cargar los 4 portraits de personalidades desde Assets/Portraits/ y asignarlos en SampleScene
        LoadPersonalityPortraits(
            out Sprite corporeoPortrait,
            out Sprite intelectoPortrait,
            out Sprite motricidadPortrait,
            out Sprite mundoInternoPortrait
        );

        ExplorationDialogueManager expManager = UnityEngine.Object.FindAnyObjectByType<ExplorationDialogueManager>();
        if (expManager != null)
        {
            expManager.corporeoPortrait = corporeoPortrait;
            expManager.intelectoPortrait = intelectoPortrait;
            expManager.motricidadPortrait = motricidadPortrait;
            expManager.mundoInternoPortrait = mundoInternoPortrait;
            EditorUtility.SetDirty(expManager);
        }

        BattleManager battleMgr = UnityEngine.Object.FindAnyObjectByType<BattleManager>();
        if (battleMgr != null)
        {
            battleMgr.corporeoPortrait = corporeoPortrait;
            battleMgr.intelectoPortrait = intelectoPortrait;
            battleMgr.motricidadPortrait = motricidadPortrait;
            battleMgr.mundoInternoPortrait = mundoInternoPortrait;
            EditorUtility.SetDirty(battleMgr);
        }

        SpawnWeakMapEnemiesWithoutDialogue();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        TMP_FontAsset pixelFont = CreateOrLoadPixelzoneFont(forceRecreate: false);
        BuildMenuSceneAndReturnToMap(pixelFont);

        Debug.Log("[RPG Santa Felipina] Capas de mapa ajustadas, MenuScene separada y los 4 portraits de personalidades asignados.");
    }

    [MenuItem("RPG Santa Felipina/Agregar Enemigos Debiles Sin Dialogo al Mapa")]
    public static void SpawnWeakMapEnemiesWithoutDialogue()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        string sampleScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";
        if (EditorSceneManager.GetActiveScene().path != sampleScenePath && File.Exists(sampleScenePath))
        {
            EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);
        }

        if (!Directory.Exists("Assets/Data/Enemies"))
        {
            Directory.CreateDirectory("Assets/Data/Enemies");
        }

        // Crear o actualizar el ScriptableObject de enemigo débil estándar sin diálogos
        EnemyData lacayoDebilSinDialogo = CreateOrUpdateEnemyAsset(
            "Assets/Data/Enemies/Lacayo_CallejeroDebil.asset",
            "Lacayo Callejero",
            EnemyType.LacayoRapidoDebil,
            maxHP: 40,
            attack: 9,
            defense: 2,
            motricidad: 9,
            color: new Color(0.85f, 0.45f, 0.15f, 1.0f),
            halfHealthDialogue: Array.Empty<string>()
        );
        AssetDatabase.SaveAssets();

        Sprite whiteSquareSprite = GetOrCreateWhiteSquareSprite();
        BattleManager battleManager = UnityEngine.Object.FindAnyObjectByType<BattleManager>();

        // Posiciones distribuidas por los callejones y avenidas transitables del mapa (libres de colisiones de edificios)
        (string name, Vector3 pos, int count)[] spawns = new (string, Vector3, int)[]
        {
            ("Lacayo_Debil_1", new Vector3(9.5f, -2.2f, 0f), 1),
            ("Lacayo_Debil_2", new Vector3(15.5f, -3.5f, 0f), 1),
            ("Lacayo_Debil_3", new Vector3(20.5f, -7.5f, 0f), 1),
            ("Lacayo_Debil_4", new Vector3(19.5f, -14.5f, 0f), 2),
            ("Lacayo_Debil_5", new Vector3(20.5f, -21.0f, 0f), 1)
        };

        foreach (var sp in spawns)
        {
            DestroyExistingIfFound(sp.name);

            GameObject go = new GameObject(sp.name);
            go.transform.position = sp.pos;
            go.transform.localScale = new Vector3(1.15f, 1.15f, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = whiteSquareSprite;
            sr.color = (sp.count > 1)
                ? new Color(0.90f, 0.35f, 0.12f, 1.0f)
                : new Color(0.95f, 0.52f, 0.16f, 1.0f);
            sr.sortingOrder = 5;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.4f, 1.4f);

            EnemyEncounterTrigger trig = go.AddComponent<EnemyEncounterTrigger>();
            EnemyData[] enemyList = new EnemyData[sp.count];
            for (int i = 0; i < sp.count; i++)
            {
                enemyList[i] = lacayoDebilSinDialogo;
            }

            trig.enemies = enemyList;
            trig.enemyData = lacayoDebilSinDialogo;
            trig.mapObjectsToDisableOnWin = new GameObject[] { go };
            trig.battleManager = battleManager;
            trig.introDialogueLines = Array.Empty<string>(); // Sin diálogo inicial
            EditorUtility.SetDirty(trig);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[RPG Santa Felipina] Se agregaron 5 encuentros de enemigos débiles sin diálogo a lo largo del mapa.");
    }

    [MenuItem("RPG Santa Felipina/Crear Pantalla de Game Over en SampleScene")]
    public static void CreateOrUpdateGameOverScreenInScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        string sampleScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";
        if (EditorSceneManager.GetActiveScene().path != sampleScenePath && File.Exists(sampleScenePath))
        {
            EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);
        }

        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        BattleManager battleManager = UnityEngine.Object.FindAnyObjectByType<BattleManager>();
        if (canvas == null || battleManager == null)
        {
            Debug.LogWarning("[RPG Santa Felipina] No se encontró Canvas o BattleManager en SampleScene.");
            return;
        }

        TMP_FontAsset pixelFont = CreateOrLoadPixelzoneFont(forceRecreate: false);

        DestroyExistingIfFound("GameOverPanel");

        Color pureBlack = new Color(0.02f, 0.02f, 0.025f, 1f);
        Color matteBlack = new Color(0.05f, 0.05f, 0.06f, 1f);
        Color crimsonBorder = new Color(0.68f, 0.14f, 0.14f, 1f);
        Color subtleBorder = new Color(0.28f, 0.28f, 0.32f, 1f);
        Color boneWhite = new Color(0.90f, 0.88f, 0.84f, 1f);
        Color mutedGrayText = new Color(0.68f, 0.68f, 0.72f, 1f);

        GameObject gameOverPanel = CreatePanel("GameOverPanel", canvas.transform, pureBlack);

        GameObject innerBorder = CreatePanel("GameOverBorderFrame", gameOverPanel.transform, crimsonBorder);
        SetAnchors(innerBorder.GetComponent<RectTransform>(), new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));

        GameObject innerBox = CreatePanel("GameOverInnerBox", innerBorder.transform, matteBlack);
        SetAnchors(innerBox.GetComponent<RectTransform>(), new Vector2(0.005f, 0.008f), new Vector2(0.995f, 0.992f));

        TextMeshProUGUI gameOverTitle = CreateTMPText(
            "GameOverTitleText",
            innerBox.transform,
            "GAME OVER",
            140,
            TextAlignmentOptions.Center,
            pixelFont
        );
        gameOverTitle.color = new Color(0.85f, 0.18f, 0.18f, 1f);
        SetAnchors(gameOverTitle.rectTransform, new Vector2(0.06f, 0.66f), new Vector2(0.94f, 0.90f));

        TextMeshProUGUI gameOverSubtitle = CreateTMPText(
            "GameOverSubtitleText",
            innerBox.transform,
            "FIN DEL RECORRIDO EN SANTA FELIPINA",
            56,
            TextAlignmentOptions.Center,
            pixelFont
        );
        gameOverSubtitle.color = boneWhite;
        SetAnchors(gameOverSubtitle.rectTransform, new Vector2(0.06f, 0.53f), new Vector2(0.94f, 0.66f));

        TextMeshProUGUI gameOverNarrative = CreateTMPText(
            "GameOverNarrativeText",
            innerBox.transform,
            "Enrique Bosques cayo inconsciente sobre el asfalto caliente.\nEl paquete nunca llego a su destino y las cuatro voces de su mente se apagaron en la oscuridad...",
            40,
            TextAlignmentOptions.Center,
            pixelFont
        );
        gameOverNarrative.color = mutedGrayText;
        SetAnchors(gameOverNarrative.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.52f));

        Button retryButton = CreateNoirButton(
            "RetryBattleButton",
            innerBox.transform,
            "REINTENTAR EN EL MAPA\n(ESPACIO / R)",
            matteBlack,
            crimsonBorder,
            38,
            pixelFont
        );
        SetAnchors(retryButton.GetComponent<RectTransform>(), new Vector2(0.10f, 0.10f), new Vector2(0.47f, 0.27f));

        Button menuButton = CreateNoirButton(
            "ReturnToMenuButton",
            innerBox.transform,
            "VOLVER AL MENU PRINCIPAL\n(M / ESC)",
            matteBlack,
            subtleBorder,
            38,
            pixelFont
        );
        SetAnchors(menuButton.GetComponent<RectTransform>(), new Vector2(0.53f, 0.10f), new Vector2(0.90f, 0.27f));

        battleManager.gameOverPanel = gameOverPanel;
        battleManager.retryBattleButton = retryButton;
        battleManager.returnToMenuButton = menuButton;
        battleManager.menuSceneName = "00_MenuIntro";

        UnityEventTools.AddPersistentListener(retryButton.onClick, battleManager.RetryFromGameOver);
        UnityEventTools.AddPersistentListener(menuButton.onClick, battleManager.ReturnToMainMenu);

        gameOverPanel.SetActive(false);

        EditorUtility.SetDirty(battleManager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[RPG Santa Felipina] Pantalla de Game Over creada y conectada al BattleManager en SampleScene.");
    }

    [MenuItem("RPG Santa Felipina/Configurar Portrait de Enrique y Agrandar Portraits")]
    public static void ConfigureEnriqueAndEnlargeAllPortraits()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        // Asegurar que Enrique.png tenga FilterMode.Point para que el pixel art se vea nítido al agrandarse
        TextureImporter enriqueImporter = AssetImporter.GetAtPath("Assets/Portraits/Enrique.png") as TextureImporter;
        if (enriqueImporter != null && enriqueImporter.filterMode != FilterMode.Point)
        {
            enriqueImporter.filterMode = FilterMode.Point;
            enriqueImporter.SaveAndReimport();
        }

        Sprite enriquePortrait = LoadSpriteFromAssetPath("Assets/Portraits/Enrique.png");
        LoadPersonalityPortraits(
            out Sprite corporeoPortrait,
            out Sprite intelectoPortrait,
            out Sprite motricidadPortrait,
            out Sprite mundoInternoPortrait
        );

        string sampleScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";
        if (EditorSceneManager.GetActiveScene().path != sampleScenePath && File.Exists(sampleScenePath))
        {
            EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);
        }

        ExplorationDialogueManager expManager = UnityEngine.Object.FindAnyObjectByType<ExplorationDialogueManager>();
        if (expManager != null)
        {
            expManager.enriquePortrait = enriquePortrait;
            expManager.corporeoPortrait = corporeoPortrait;
            expManager.intelectoPortrait = intelectoPortrait;
            expManager.motricidadPortrait = motricidadPortrait;
            expManager.mundoInternoPortrait = mundoInternoPortrait;

            if (expManager.portraitContainer != null)
            {
                SetAnchors(expManager.portraitContainer.GetComponent<RectTransform>(), new Vector2(0.01f, 0.04f), new Vector2(0.195f, 1.55f));
            }
            if (expManager.portraitImage != null)
            {
                SetAnchors(expManager.portraitImage.rectTransform, new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.99f));
                expManager.portraitImage.preserveAspect = false;
                if (enriquePortrait != null)
                {
                    expManager.portraitImage.sprite = enriquePortrait;
                    expManager.portraitImage.color = Color.white;
                }
            }
            if (expManager.portraitLabelText != null)
            {
                expManager.portraitLabelText.gameObject.SetActive(false);
            }
            EditorUtility.SetDirty(expManager);
        }

        BattleManager battleManager = UnityEngine.Object.FindAnyObjectByType<BattleManager>();
        if (battleManager != null)
        {
            battleManager.enriquePortrait = enriquePortrait;
            battleManager.corporeoPortrait = corporeoPortrait;
            battleManager.intelectoPortrait = intelectoPortrait;
            battleManager.motricidadPortrait = motricidadPortrait;
            battleManager.mundoInternoPortrait = mundoInternoPortrait;

            // 1. Agrandar el portrait de diálogos de combate
            if (battleManager.combatPortraitContainer != null)
            {
                SetAnchors(battleManager.combatPortraitContainer.GetComponent<RectTransform>(), new Vector2(0.01f, 0.04f), new Vector2(0.195f, 1.55f));
            }
            if (battleManager.combatPortraitImage != null)
            {
                SetAnchors(battleManager.combatPortraitImage.rectTransform, new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.99f));
                battleManager.combatPortraitImage.preserveAspect = false;
                if (enriquePortrait != null)
                {
                    battleManager.combatPortraitImage.sprite = enriquePortrait;
                    battleManager.combatPortraitImage.color = Color.white;
                }
            }
            if (battleManager.combatPortraitLabelText != null)
            {
                battleManager.combatPortraitLabelText.gameObject.SetActive(false);
            }

            // 2. Agregar el recuadro fijo del Portrait de Enrique en la interfaz de Combate (abajo a la izquierda)
            if (battleManager.battlePanel != null)
            {
                Transform combatPanelTr = battleManager.battlePanel.transform;
                Transform existingFrame = combatPanelTr.Find("EnriqueCombatPortraitFrame");
                if (existingFrame != null)
                {
                    UnityEngine.Object.DestroyImmediate(existingFrame.gameObject);
                }

                Color subtleBorder = new Color(0.28f, 0.28f, 0.32f, 1f);
                GameObject frameGO = CreatePanel("EnriqueCombatPortraitFrame", combatPanelTr, subtleBorder);
                SetAnchors(frameGO.GetComponent<RectTransform>(), new Vector2(0.02f, 0.03f), new Vector2(0.175f, 0.44f));

                GameObject imgGO = CreatePanel("EnriqueCombatPortraitImage", frameGO.transform, Color.white);
                SetAnchors(imgGO.GetComponent<RectTransform>(), new Vector2(0.015f, 0.015f), new Vector2(0.985f, 0.985f));
                Image playerPortraitImg = imgGO.GetComponent<Image>();
                playerPortraitImg.sprite = enriquePortrait;
                playerPortraitImg.color = Color.white;
                playerPortraitImg.preserveAspect = false;

                battleManager.playerCombatPortraitImage = playerPortraitImg;

                // Ajustar la barra de vida y los 4 botones a la derecha del portrait de Enrique (x: 0.19 a 0.97)
                Transform statusStrip = combatPanelTr.Find("PlayerStatusStrip");
                if (statusStrip != null)
                {
                    SetAnchors(statusStrip.GetComponent<RectTransform>(), new Vector2(0.19f, 0.198f), new Vector2(0.97f, 0.282f));
                }
                if (battleManager.corporeoButton != null)
                {
                    SetAnchors(battleManager.corporeoButton.GetComponent<RectTransform>(), new Vector2(0.19f, 0.03f), new Vector2(0.375f, 0.19f));
                }
                if (battleManager.intelectoButton != null)
                {
                    SetAnchors(battleManager.intelectoButton.GetComponent<RectTransform>(), new Vector2(0.388f, 0.03f), new Vector2(0.573f, 0.19f));
                }
                if (battleManager.motricidadButton != null)
                {
                    SetAnchors(battleManager.motricidadButton.GetComponent<RectTransform>(), new Vector2(0.587f, 0.03f), new Vector2(0.772f, 0.19f));
                }
                if (battleManager.mundoInternoButton != null)
                {
                    SetAnchors(battleManager.mundoInternoButton.GetComponent<RectTransform>(), new Vector2(0.785f, 0.03f), new Vector2(0.97f, 0.19f));
                }

                // Asegurar que CombatDialoguePanel e IntelectoAndItemsPanel queden al frente cuando se abran
                if (battleManager.intelectoAndItemsPanel != null)
                    battleManager.intelectoAndItemsPanel.transform.SetAsLastSibling();
                if (battleManager.mundoInternoPanel != null)
                    battleManager.mundoInternoPanel.transform.SetAsLastSibling();
                if (battleManager.combatDialoguePanel != null)
                    battleManager.combatDialoguePanel.transform.SetAsLastSibling();
            }

            EditorUtility.SetDirty(battleManager);
        }

        // Incluir intervención de Enrique en el encuentro tutorial y en la puerta para que también se vea su portrait al hablar
        GameObject lacayoTutGO = GameObject.Find("Lacayo_Tutorial");
        if (lacayoTutGO != null)
        {
            EnemyEncounterTrigger trig1 = lacayoTutGO.GetComponent<EnemyEncounterTrigger>();
            if (trig1 != null)
            {
                trig1.introDialogueLines = new string[]
                {
                    "LACAYO DE EL CALACAS: \"Quieto ahi, repartidor! Ese paquete no va a llegar a su destino.\"",
                    "ENRIQUE BOSQUES: \"Oye tranquilo, solo soy el repartidor... aunque las voces en mi cabeza no opinan igual.\"",
                    "VOZ [CORPOREO]: \"Miralo bien, Enrique. Es debil. Usa mi boton (CORPOREO) para romperle la guardia de un golpe directo!\"",
                    "VOZ [INTELECTO]: \"Si nos topamos con alguien de defensa alta, usa mi boton (INTELECTO) para improvisar un artefacto con chatarra del mapa.\"",
                    "VOZ [MOTRICIDAD]: \"Somos mas rapidos que el (Motricidad 12 vs 10). Mi boton (MOTRICIDAD) nos prepara para esquivar o reducir el dano un 70%!\"",
                    "VOZ [MUNDO INTERNO]: \"Y si quieres que pierda su turno en la cola, usa mi boton (MUNDO INTERNO) para confundir su mente.\""
                };
                EditorUtility.SetDirty(trig1);
            }
        }

        GameObject puertaGO = GameObject.Find("Puerta_CasaEquivocada");
        if (puertaGO != null)
        {
            MapInteractable inter = puertaGO.GetComponent<MapInteractable>();
            if (inter != null)
            {
                inter.dialogueLines = new string[]
                {
                    "NARRADOR: \"Enrique deja el paquete frente a la puerta de madera descascarada y toca el timbre.\"",
                    "ENRIQUE BOSQUES: \"Listo, entregado... Espera, por que esta puerta tiene el numero 41?\"",
                    "VOZ [INTELECTO]: \"El recibo dice Callejon del Perro #14, y esta es la #41. Te equivocaste de casa otra vez!\"",
                    "VOZ [CORPOREO]: \"Patada a la puerta, agarramos la caja y fingimos que aqui no paso nada.\"",
                    "VOZ [MOTRICIDAD]: \"Demasiado tarde para correr sin hacer ruido. Ya nos vieron desde la esquina.\"",
                    "VOZ [MUNDO INTERNO]: \"Esa caja no contiene un envio comun... El aire huele a polvora y a deuda ajena.\""
                };
                EditorUtility.SetDirty(inter);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        // Actualizar también MenuScene con el portrait de Enrique y los portraits agrandados
        TMP_FontAsset pixelFont = CreateOrLoadPixelzoneFont(forceRecreate: false);
        BuildMenuSceneAndReturnToMap(pixelFont);

        Debug.Log("[RPG Santa Felipina] Portrait de Enrique agregado en combate y diálogos, y todos los portraits agrandados al 100% de su contenedor vertical.");
    }

    [MenuItem("RPG Santa Felipina/Crear Cuartos Secretos y Objetos de Dialogo")]
    public static void CreateSecretRoomsAndDialogueObjects()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        string sampleScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";
        if (EditorSceneManager.GetActiveScene().path != sampleScenePath && File.Exists(sampleScenePath))
        {
            EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);
        }

        Sprite whiteSquare = GetOrCreateWhiteSquareSprite();
        TMP_FontAsset pixelFont = CreateOrLoadPixelzoneFont(forceRecreate: false);
        ExplorationDialogueManager expManager = UnityEngine.Object.FindAnyObjectByType<ExplorationDialogueManager>();

        EnsureItemAndRecipeAssets(
            out ItemData itemRefresco,
            out ItemData itemTorta,
            out ItemData itemBotellaVacia,
            out ItemData itemChilePolvo,
            out _,
            out _
        );

        // =====================================================================
        // 1. CREAR 3 CUARTOS SECRETOS (Con Techo Ocultador que se transparenta al entrar)
        // =====================================================================
        CreateSecretRoomInScene(
            roomName: "Cuarto_Secreto_1_Oeste",
            position: new Vector3(-11.0f, -1.5f, 0f),
            roomSize: new Vector2(5.5f, 4.5f),
            openEntranceSide: "East", // Entrada por la derecha conectada al inicio de la calle
            whiteSquare: whiteSquare,
            hiddenItem: itemTorta,
            hiddenItemName: "Secreto_Torta_Escondida",
            discoveryLines: new string[]
            {
                "VOZ [MUNDO INTERNO]: \"Detras de esa pared falsa habia un cuarto oculto...\"",
                "ENRIQUE BOSQUES: \"Mira nada mas, alguien dejo provisiones escondidas aqui adentro.\""
            },
            dialogueManager: expManager,
            font: pixelFont
        );

        ItemData itemBat = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Arma_BatMadera.asset");
        CreateSecretRoomInScene(
            roomName: "Cuarto_Secreto_2_PasajeEste",
            position: new Vector3(26.5f, -11.6f, 0f),
            roomSize: new Vector2(5.5f, 3.6f),
            openEntranceSide: "West", // Entrada por la izquierda en el callejón entre los dos edificios del este
            whiteSquare: whiteSquare,
            hiddenItem: itemBat,
            hiddenItemName: "Secreto_Bat_Escondido",
            discoveryLines: new string[]
            {
                "VOZ [INTELECTO]: \"El angulo entre estos dos muros ocultaba una bodega clandestina.\"",
                "VOZ [CORPOREO]: \"¡Mira eso apoyado contra las cajas! Un Bat Callejero Reforzado (+9 ATQ). Un arma brutal.\""
            },
            dialogueManager: expManager,
            font: pixelFont
        );

        CreateSecretRoomInScene(
            roomName: "Cuarto_Secreto_3_SotanoSur",
            position: new Vector3(20.2f, -29.2f, 0f),
            roomSize: new Vector2(5.5f, 4.5f),
            openEntranceSide: "North", // Entrada por arriba al final de la calle sur
            whiteSquare: whiteSquare,
            hiddenItem: itemChilePolvo,
            hiddenItemName: "Secreto_Chile_Escondido",
            discoveryLines: new string[]
            {
                "VOZ [MOTRICIDAD]: \"Pasos silenciosos... Nadie nos vio entrar a este escondite del sur.\""
            },
            dialogueManager: expManager,
            font: pixelFont
        );

        // =====================================================================
        // 2. CREAR GAMEOBJECTS DE DIÁLOGO LISTOS PARA EDITAR O DUPLICAR
        // =====================================================================
        DestroyExistingIfFound("Dialogos_Del_Mapa");
        GameObject dialoguesParent = new GameObject("Dialogos_Del_Mapa");

        // Diálogo 1: Cartel / Objeto examinable con tecla E
        MapInteractable d1 = CreateMapInteractableInScene(
            "Dialogo_1_CartelCallejon",
            new Vector3(0.5f, 1.5f, 0f),
            whiteSquare,
            new Color(0.25f, 0.65f, 0.95f, 1f), // Azul claro (Objeto de Diálogo)
            InteractableType.DialogueOnly,
            null,
            0,
            null,
            new string[]
            {
                "NARRADOR: \"Un cartel pegado en el poste anuncia: 'SE BUSCA REPARTIDOR QUE SE LLEVO EL PAQUETE DE EL CALACAS'.\"",
                "ENRIQUE BOSQUES: \"Genial... hasta mi foto borrosa pusieron en el poste.\"",
                "VOZ [INTELECTO]: \"Al menos escribieron mal tu apellido. Tenemos ventaja tactica.\""
            },
            expManager,
            pixelFont
        );
        d1.transform.SetParent(dialoguesParent.transform, true);

        // Diálogo 2: NPC / Vecino con tecla E
        MapInteractable d2 = CreateMapInteractableInScene(
            "Dialogo_2_NPC_Vecino",
            new Vector3(12.2f, -1.2f, 0f),
            whiteSquare,
            new Color(0.85f, 0.70f, 0.25f, 1f), // Dorado/Amarillo (NPC)
            InteractableType.DialogueOnly,
            null,
            0,
            null,
            new string[]
            {
                "DON AURELIO: \"Oye muchacho, yo que tu no bajaba por la avenida sin antes revisar los callejones laterales.\"",
                "ENRIQUE BOSQUES: \"Gracias por el aviso, jefe. Sabe donde vive el dueno de este paquete?\"",
                "VOZ [MUNDO INTERNO]: \"Sus manos tiemblan. Sabe mucho mas de lo que se atreve a decir en voz alta.\""
            },
            expManager,
            pixelFont
        );
        d2.transform.SetParent(dialoguesParent.transform, true);

        // Diálogo 3: Objeto examinable en la avenida derecha con tecla E
        MapInteractable d3 = CreateMapInteractableInScene(
            "Dialogo_3_CajasSospechosas",
            new Vector3(18.2f, -10.5f, 0f),
            whiteSquare,
            new Color(0.25f, 0.65f, 0.95f, 1f),
            InteractableType.DialogueOnly,
            null,
            0,
            null,
            new string[]
            {
                "NARRADOR: \"Cajas de madera apiladas junto a la pared del callejon.\"",
                "VOZ [CORPOREO]: \"Si las rompemos de una patada seguro hacemos mucho ruido, mejor sigamos.\"",
                "VOZ [MOTRICIDAD]: \"Mira hacia la derecha entre los dos edificios... parece haber una entrada secreta en el muro.\""
            },
            expManager,
            pixelFont
        );
        d3.transform.SetParent(dialoguesParent.transform, true);

        // Diálogo 4: Zona de diálogo automático al pisar (sin presionar E)
        MapInteractable d4 = CreateMapInteractableInScene(
            "Dialogo_4_ZonaAutomatica_AlPisar",
            new Vector3(14.5f, -6.0f, 0f),
            whiteSquare,
            new Color(0.65f, 0.30f, 0.85f, 0.45f), // Morado semitransparente
            InteractableType.DialogueOnly,
            null,
            0,
            null,
            new string[]
            {
                "VOZ [MUNDO INTERNO]: \"El viento cambia de direccion al doblar esta esquina. Mas abajo hay lacayos vigilando la calle.\"",
                "ENRIQUE BOSQUES: \"Mejor preparo mis molotovs y refrescos antes de seguir bajando.\""
            },
            expManager,
            pixelFont
        );
        d4.triggerAutomaticallyOnEnter = true;
        d4.triggerDialogueOnce = true;
        d4.transform.SetParent(dialoguesParent.transform, true);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[RPG Santa Felipina] Se crearon 3 Cuartos Secretos y 4 GameObjects de Diálogo en SampleScene.");
    }

    private static void CreateSecretRoomInScene(
        string roomName,
        Vector3 position,
        Vector2 roomSize,
        string openEntranceSide,
        Sprite whiteSquare,
        ItemData hiddenItem,
        string hiddenItemName,
        string[] discoveryLines,
        ExplorationDialogueManager dialogueManager,
        TMP_FontAsset font)
    {
        DestroyExistingIfFound(roomName);

        GameObject roomRoot = new GameObject(roomName);
        roomRoot.transform.position = position;

        // 1. Contenedor Interior Oculto (solo se vuelve visible al interactuar con la Puerta Secreta con [E])
        GameObject interiorRoot = new GameObject("Interior_Oculto");
        interiorRoot.transform.SetParent(roomRoot.transform, false);
        interiorRoot.transform.localPosition = Vector3.zero;

        // Piso interior del cuarto secreto
        GameObject floorGO = new GameObject("Piso_Secreto");
        floorGO.transform.SetParent(interiorRoot.transform, false);
        floorGO.transform.localPosition = Vector3.zero;
        floorGO.transform.localScale = new Vector3(roomSize.x, roomSize.y, 1f);
        SpriteRenderer floorSR = floorGO.AddComponent<SpriteRenderer>();
        floorSR.sprite = whiteSquare;
        floorSR.color = new Color(0.14f, 0.13f, 0.18f, 1f);
        floorSR.sortingOrder = 1;

        // Paredes con colisión dentro de Interior_Oculto (dejando el lado de la puerta para la entrada)
        float wallThickness = 0.35f;
        if (openEntranceSide != "North")
            CreateSecretRoomWall("Pared_Norte", interiorRoot.transform, new Vector3(0f, roomSize.y * 0.5f, 0f), new Vector2(roomSize.x, wallThickness), whiteSquare);
        if (openEntranceSide != "South")
            CreateSecretRoomWall("Pared_Sur", interiorRoot.transform, new Vector3(0f, -roomSize.y * 0.5f, 0f), new Vector2(roomSize.x, wallThickness), whiteSquare);
        if (openEntranceSide != "East")
            CreateSecretRoomWall("Pared_Este", interiorRoot.transform, new Vector3(roomSize.x * 0.5f, 0f, 0f), new Vector2(wallThickness, roomSize.y), whiteSquare);
        if (openEntranceSide != "West")
            CreateSecretRoomWall("Pared_West", interiorRoot.transform, new Vector3(-roomSize.x * 0.5f, 0f, 0f), new Vector2(wallThickness, roomSize.y), whiteSquare);

        // Contenedor para que el usuario esconda objetos y diálogos aquí adentro
        GameObject hiddenContainer = new GameObject("Objetos_Escondidos_Aqui");
        hiddenContainer.transform.SetParent(interiorRoot.transform, false);
        hiddenContainer.transform.localPosition = Vector3.zero;

        if (hiddenItem != null)
        {
            MapInteractable pickup = CreateMapInteractableInScene(
                hiddenItemName,
                position,
                whiteSquare,
                hiddenItem.placeholderColor,
                InteractableType.PickupItem,
                hiddenItem,
                1,
                new string[]
                {
                    $"ENRIQUE BOSQUES: \"Encontre un {hiddenItem.itemName} escondido en este cuarto secreto!\"",
                    "VOZ [CORPOREO]: \"Guardalo en la mochila antes de que alguien nos vea.\""
                },
                null,
                dialogueManager,
                font
            );
            pickup.transform.SetParent(hiddenContainer.transform, true);
        }

        // 2. Calcular posición y tamaño de la Puerta Secreta Interactuable en el borde de entrada
        Vector3 doorLocalPos = Vector3.zero;
        Vector2 doorVisualSize = new Vector2(0.55f, 1.4f);
        switch (openEntranceSide)
        {
            case "East":
                doorLocalPos = new Vector3(roomSize.x * 0.5f, 0f, 0f);
                doorVisualSize = new Vector2(0.55f, 1.4f);
                break;
            case "West":
                doorLocalPos = new Vector3(-roomSize.x * 0.5f, 0f, 0f);
                doorVisualSize = new Vector2(0.55f, 1.4f);
                break;
            case "North":
                doorLocalPos = new Vector3(0f, roomSize.y * 0.5f, 0f);
                doorVisualSize = new Vector2(1.4f, 0.55f);
                break;
            case "South":
            default:
                doorLocalPos = new Vector3(0f, -roomSize.y * 0.5f, 0f);
                doorVisualSize = new Vector2(1.4f, 0.55f);
                break;
        }

        // 3. Puerta Secreta Interactuable (siempre visible en el mapa; al presionar [E] revela Interior_Oculto)
        GameObject doorGO = new GameObject("Puerta_Secreta_Interactuable");
        doorGO.transform.SetParent(roomRoot.transform, false);
        doorGO.transform.localPosition = doorLocalPos;

        // Marco visual de la puerta (queda visible aún abierta para saber dónde está la entrada)
        GameObject doorFrameGO = new GameObject("Marco_Puerta");
        doorFrameGO.transform.SetParent(doorGO.transform, false);
        doorFrameGO.transform.localPosition = Vector3.zero;
        doorFrameGO.transform.localScale = new Vector3(doorVisualSize.x, doorVisualSize.y, 1f);
        SpriteRenderer frameSR = doorFrameGO.AddComponent<SpriteRenderer>();
        frameSR.sprite = whiteSquare;
        frameSR.color = new Color(0.32f, 0.20f, 0.12f, 1f);
        frameSR.sortingOrder = 3;

        // Hoja cerrada + bloqueo físico (se desactiva al abrir la puerta con [E])
        GameObject doorBlockerGO = new GameObject("Puerta_Cerrada_Bloqueo");
        doorBlockerGO.transform.SetParent(doorGO.transform, false);
        doorBlockerGO.transform.localPosition = Vector3.zero;
        doorBlockerGO.transform.localScale = new Vector3(doorVisualSize.x * 0.85f, doorVisualSize.y * 0.85f, 1f);
        SpriteRenderer blockerSR = doorBlockerGO.AddComponent<SpriteRenderer>();
        blockerSR.sprite = whiteSquare;
        blockerSR.color = new Color(0.58f, 0.36f, 0.18f, 1f);
        blockerSR.sortingOrder = 4;
        BoxCollider2D blockerCol = doorBlockerGO.AddComponent<BoxCollider2D>();
        blockerCol.isTrigger = false;
        blockerCol.size = Vector2.one;

        // Indicador flotante "[E]" sobre la puerta secreta
        GameObject promptCanvasGO = new GameObject("PromptUI_E");
        promptCanvasGO.transform.SetParent(doorGO.transform, false);
        promptCanvasGO.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        promptCanvasGO.transform.localScale = new Vector3(0.015f, 0.015f, 1f);

        Canvas promptCanvas = promptCanvasGO.AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.WorldSpace;
        promptCanvas.sortingOrder = 25;

        RectTransform promptRT = promptCanvasGO.GetComponent<RectTransform>();
        promptRT.sizeDelta = new Vector2(120f, 50f);

        GameObject promptBgGO = new GameObject("Bg");
        promptBgGO.transform.SetParent(promptCanvasGO.transform, false);
        RectTransform bgRT = promptBgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = promptBgGO.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.06f, 0.09f, 0.92f);

        GameObject promptTextGO = new GameObject("Label");
        promptTextGO.transform.SetParent(promptCanvasGO.transform, false);
        RectTransform txtRT = promptTextGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = promptTextGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "[E] Abrir";
        tmp.fontSize = 24;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.85f, 0.35f, 1f);
        if (font != null) tmp.font = font;

        promptCanvasGO.SetActive(false);

        // Trigger de interacción en la puerta + componente SecretRoomZone
        BoxCollider2D doorTrigger = doorGO.AddComponent<BoxCollider2D>();
        doorTrigger.isTrigger = true;
        doorTrigger.size = new Vector2(2.0f, 2.0f);

        SecretRoomZone secretZone = doorGO.AddComponent<SecretRoomZone>();
        secretZone.roomInteriorRoot = interiorRoot;
        secretZone.doorBlockerObject = doorBlockerGO;
        secretZone.promptUI = promptCanvasGO;
        secretZone.outsideAlpha = 0f;
        secretZone.insideAlpha = 0f;
        secretZone.discoveryDialogueLines = discoveryLines;
        EditorUtility.SetDirty(secretZone);
    }

    private static void CreateSecretRoomWall(string name, Transform parent, Vector3 localPos, Vector2 size, Sprite whiteSquare)
    {
        GameObject wallGO = new GameObject(name);
        wallGO.transform.SetParent(parent, false);
        wallGO.transform.localPosition = localPos;
        wallGO.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = wallGO.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSquare;
        sr.color = new Color(0.22f, 0.20f, 0.25f, 1f);
        sr.sortingOrder = 2;

        BoxCollider2D col = wallGO.AddComponent<BoxCollider2D>();
        col.isTrigger = false;
        col.size = Vector2.one;
    }

    private static void LoadPersonalityPortraits(
        out Sprite corporeoPortrait,
        out Sprite intelectoPortrait,
        out Sprite motricidadPortrait,
        out Sprite mundoInternoPortrait)
    {
        corporeoPortrait = LoadSpriteFromAssetPath("Assets/Portraits/Musculatura.png");
        intelectoPortrait = LoadSpriteFromAssetPath("Assets/Portraits/Intelecto.png");
        motricidadPortrait = LoadSpriteFromAssetPath("Assets/Portraits/Motricidad.png");
        mundoInternoPortrait = LoadSpriteFromAssetPath("Assets/Portraits/MundoInterno.png");
    }

    private static Sprite LoadSpriteFromAssetPath(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;

        UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all != null)
        {
            foreach (UnityEngine.Object obj in all)
            {
                if (obj is Sprite sp)
                    return sp;
            }
        }
        return null;
    }

    private static void BuildMenuSceneAndReturnToMap(TMP_FontAsset pixelFont)
    {
        if (!Directory.Exists("Assets/Scenes"))
        {
            Directory.CreateDirectory("Assets/Scenes");
        }

        string menuScenePath = "Assets/Scenes/00_MenuIntro.unity";
        string mapScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";

        var menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Cámara de MenuScene
        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        Camera mainCam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        mainCam.orthographic = true;
        mainCam.orthographicSize = 5.5f;
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = new Color(0.02f, 0.02f, 0.025f, 1.0f);
        mainCam.transform.position = new Vector3(0f, 0f, -10f);

        // 2. EventSystem
        GameObject esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGO.AddComponent<InputSystemUIInputModule>();
#else
        esGO.AddComponent<StandaloneInputModule>();
#endif

        // 3. Canvas de MenuScene (1920x1080)
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        Color pureBlack = new Color(0.02f, 0.02f, 0.025f, 1f);
        Color matteBlack = new Color(0.05f, 0.05f, 0.06f, 1f);
        Color subtleBorder = new Color(0.26f, 0.26f, 0.30f, 1f);
        Color boneWhite = new Color(0.90f, 0.88f, 0.84f, 1f);
        Color mutedGrayText = new Color(0.65f, 0.66f, 0.70f, 1f);

        // 4. SlideshowPanel
        GameObject slideshowPanel = CreatePanel("SlideshowPanel", canvasGO.transform, pureBlack);

        GameObject photoBorder = CreatePanel("PhotoBorderFrame", slideshowPanel.transform, subtleBorder);
        SetAnchors(photoBorder.GetComponent<RectTransform>(), new Vector2(0.10f, 0.39f), new Vector2(0.90f, 0.95f));

        GameObject photoGO = CreatePanel("PhotoPlaceholderImage", photoBorder.transform, new Color(0.55f, 0.34f, 0.14f, 1f));
        Image photoPlaceholderImage = photoGO.GetComponent<Image>();
        SetAnchors(photoGO.GetComponent<RectTransform>(), new Vector2(0.004f, 0.008f), new Vector2(0.996f, 0.992f));

        TextMeshProUGUI photoLabelText = CreateTMPText(
            "PhotoLabelText",
            photoGO.transform,
            "FOTO 1: Calle de Santa Felipina\nEnrique repartiendo en bicicleta",
            60,
            TextAlignmentOptions.Center,
            pixelFont
        );
        photoLabelText.color = boneWhite;
        SetAnchors(photoLabelText.rectTransform, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.90f));

        GameObject narrativeBorder = CreatePanel("NarrativeBoxBorder", slideshowPanel.transform, subtleBorder);
        SetAnchors(narrativeBorder.GetComponent<RectTransform>(), new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.36f));

        GameObject narrativeBox = CreatePanel("NarrativeBoxBG", narrativeBorder.transform, new Color(0.04f, 0.04f, 0.05f, 0.99f));
        SetAnchors(narrativeBox.GetComponent<RectTransform>(), new Vector2(0.002f, 0.008f), new Vector2(0.998f, 0.992f));

        GameObject portraitContainer = CreatePanel("PortraitContainer", narrativeBox.transform, subtleBorder);
        SetAnchors(portraitContainer.GetComponent<RectTransform>(), new Vector2(0.01f, 0.04f), new Vector2(0.195f, 1.55f));

        GameObject portraitGO = CreatePanel("PortraitImage", portraitContainer.transform, new Color(0.38f, 0.40f, 0.43f, 1f));
        Image portraitImage = portraitGO.GetComponent<Image>();
        portraitImage.preserveAspect = false;
        SetAnchors(portraitGO.GetComponent<RectTransform>(), new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.99f));

        TextMeshProUGUI portraitLabelText = CreateTMPText(
            "PortraitLabelText",
            portraitGO.transform,
            "RETRATO",
            34,
            TextAlignmentOptions.Center,
            pixelFont
        );
        portraitLabelText.color = Color.white;
        SetAnchors(portraitLabelText.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));

        TextMeshProUGUI speakerNameText = CreateTMPText(
            "SpeakerNameText",
            narrativeBox.transform,
            "NARRADOR",
            50,
            TextAlignmentOptions.BottomLeft,
            pixelFont
        );
        speakerNameText.color = boneWhite;
        SetAnchors(speakerNameText.rectTransform, new Vector2(0.205f, 0.73f), new Vector2(0.815f, 0.95f));

        TextMeshProUGUI narrativeText = CreateTMPText(
            "NarrativeText",
            narrativeBox.transform,
            "Santa Felipina hierve bajo el sol y el esmog. Entre callejones empedrados y cables colgantes, Enrique Bosques pedalea a toda velocidad.",
            44,
            TextAlignmentOptions.TopLeft,
            pixelFont
        );
        narrativeText.color = boneWhite;
        SetAnchors(narrativeText.rectTransform, new Vector2(0.205f, 0.06f), new Vector2(0.815f, 0.70f));

        Button nextSlideButton = CreateNoirButton(
            "NextSlideButton",
            narrativeBox.transform,
            "SIGUIENTE >>\n(ESPACIO)",
            matteBlack,
            subtleBorder,
            34,
            pixelFont
        );
        SetAnchors(nextSlideButton.GetComponent<RectTransform>(), new Vector2(0.83f, 0.18f), new Vector2(0.985f, 0.82f));

        // 5. StartScreenPanel
        GameObject startScreenPanel = CreatePanel("StartScreenPanel", canvasGO.transform, pureBlack);

        TextMeshProUGUI titleText = CreateTMPText(
            "TitleText",
            startScreenPanel.transform,
            "PROYECTO: VOCES INTERNAS",
            130,
            TextAlignmentOptions.Center,
            pixelFont
        );
        titleText.color = boneWhite;
        SetAnchors(titleText.rectTransform, new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.85f));

        TextMeshProUGUI chapterText = CreateTMPText(
            "ChapterText",
            startScreenPanel.transform,
            "CRONICAS URBANAS",
            70,
            TextAlignmentOptions.Center,
            pixelFont
        );
        chapterText.color = new Color(0.75f, 0.52f, 0.22f, 1f);
        SetAnchors(chapterText.rectTransform, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.62f));

        TextMeshProUGUI subtitleText = CreateTMPText(
            "SubtitleText",
            startScreenPanel.transform,
            "Enrique Bosques y sus 4 Voces Interiores",
            46,
            TextAlignmentOptions.Center,
            pixelFont
        );
        subtitleText.color = mutedGrayText;
        SetAnchors(subtitleText.rectTransform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.47f));

        Button startGameButton = CreateNoirButton(
            "StartGameButton",
            startScreenPanel.transform,
            "INICIAR JUEGO (ESPACIO)",
            matteBlack,
            new Color(0.65f, 0.45f, 0.20f, 1f),
            46,
            pixelFont
        );
        SetAnchors(startGameButton.GetComponent<RectTransform>(), new Vector2(0.28f, 0.16f), new Vector2(0.72f, 0.30f));

        // 6. IntroSlideshowManager configurado para cargar SampleScene al terminar y con los portraits de Enrique y las 4 personalidades asignados
        Sprite enriquePortrait = LoadSpriteFromAssetPath("Assets/Portraits/Enrique.png");
        LoadPersonalityPortraits(
            out Sprite corporeoPortrait,
            out Sprite intelectoPortrait,
            out Sprite motricidadPortrait,
            out Sprite mundoInternoPortrait
        );

        GameObject introManagerGO = new GameObject("IntroSlideshowManager");
        IntroSlideshowManager introManager = introManagerGO.AddComponent<IntroSlideshowManager>();
        introManager.startScreenPanel = startScreenPanel;
        introManager.startGameButton = startGameButton;
        introManager.slideshowPanel = slideshowPanel;
        introManager.photoPlaceholderImage = photoPlaceholderImage;
        introManager.photoLabelText = photoLabelText;
        introManager.narrativeText = narrativeText;
        introManager.nextSlideButton = nextSlideButton;
        introManager.portraitContainer = portraitContainer;
        introManager.portraitImage = portraitImage;
        introManager.portraitLabelText = portraitLabelText;
        introManager.speakerNameText = speakerNameText;
        introManager.enriquePortrait = enriquePortrait;
        introManager.corporeoPortrait = corporeoPortrait;
        introManager.intelectoPortrait = intelectoPortrait;
        introManager.motricidadPortrait = motricidadPortrait;
        introManager.mundoInternoPortrait = mundoInternoPortrait;
        introManager.loadSceneOnFinish = true;
        introManager.nextSceneName = "01_Nivel1_SantaFelipina";

        if (introManager.slides != null)
        {
            foreach (SlideData slide in introManager.slides)
            {
                if (slide == null) continue;
                if (slide.speakerType == DialogueSpeakerType.SkillVoice)
                {
                    slide.customPortraitSprite = introManager.GetSkillVoicePortrait(slide.skillVoice);
                }
                else if (slide.speakerType == DialogueSpeakerType.Character &&
                         !string.IsNullOrEmpty(slide.characterName) &&
                         (slide.characterName.IndexOf("Enrique", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          false))
                {
                    slide.customPortraitSprite = enriquePortrait;
                }
            }
        }

        UnityEventTools.AddPersistentListener(startGameButton.onClick, introManager.StartGame);
        UnityEventTools.AddPersistentListener(nextSlideButton.onClick, introManager.NextSlide);

        slideshowPanel.SetActive(false);
        startScreenPanel.SetActive(true);

        EditorUtility.SetDirty(introManager);
        EditorSceneManager.SaveScene(menuScene, menuScenePath);

        // Registrar las 3 escenas en Build Settings (0: 00_MenuIntro, 1: 01_Nivel1_SantaFelipina, 2: 02_Nivel2_Guarida)
        string cap2ScenePath = "Assets/Scenes/02_Nivel2_Guarida.unity";
        EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(menuScenePath, true),
            new EditorBuildSettingsScene(mapScenePath, true),
            new EditorBuildSettingsScene(cap2ScenePath, true)
        };

        // Volver a abrir SampleScene para que el usuario pueda trabajar directamente en el mapa
        if (File.Exists(mapScenePath))
        {
            EditorSceneManager.OpenScene(mapScenePath, OpenSceneMode.Single);
        }
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
        // Contenedor principal del slot del enemigo (transparente para ver la calle de fondo)
        GameObject slotRoot = new GameObject(slotName, typeof(RectTransform), typeof(Image), typeof(Button));
        slotRoot.transform.SetParent(parent, false);
        SetAnchors(slotRoot.GetComponent<RectTransform>(), anchorMin, anchorMax);

        Image clickCatcher = slotRoot.GetComponent<Image>();
        clickCatcher.color = new Color(0f, 0f, 0f, 0f); // Transparente para capturar clics
        clickCatcher.raycastTarget = true;

        Button selectBtn = slotRoot.GetComponent<Button>();
        selectBtn.targetGraphic = clickCatcher;

        // 1. Anillo/Sombra de contacto en el suelo bajo los pies del enemigo
        GameObject ringGO = CreatePanel("GroundIndicatorRing", slotRoot.transform, new Color(0f, 0f, 0f, 0.32f));
        SetAnchors(ringGO.GetComponent<RectTransform>(), new Vector2(0.15f, 0.00f), new Vector2(0.85f, 0.10f));
        Image ringImg = ringGO.GetComponent<Image>();
        ringImg.raycastTarget = false;

        // 2. Imagen del Sprite del enemigo (pixel-art limpio sobre la calle, sin rectángulos oscuros)
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

        // 3. Nameplate flotante estilizado sobre la cabeza del enemigo
        GameObject nameplateBorder = CreatePanel("EnemyNameplateBorder", slotRoot.transform, subtleBorder);
        SetAnchors(nameplateBorder.GetComponent<RectTransform>(), new Vector2(0.06f, 0.77f), new Vector2(0.94f, 0.89f));
        Image borderImg = nameplateBorder.GetComponent<Image>();
        borderImg.raycastTarget = false;

        GameObject nameplateBg = CreatePanel("EnemyNameplateBg", nameplateBorder.transform, new Color(0.06f, 0.06f, 0.09f, 0.85f));
        SetAnchors(nameplateBg.GetComponent<RectTransform>(), new Vector2(0.008f, 0.03f), new Vector2(0.992f, 0.97f));
        Image bgImg = nameplateBg.GetComponent<Image>();
        bgImg.raycastTarget = false;

        TextMeshProUGUI nameText = CreateTMPText(
            "EnemyNameText",
            nameplateBg.transform,
            initialTitle,
            28,
            TextAlignmentOptions.Center,
            font
        );
        nameText.color = new Color(0.90f, 0.88f, 0.84f, 1f);
        SetAnchors(nameText.rectTransform, new Vector2(0.02f, 0.50f), new Vector2(0.98f, 0.98f));

        TextMeshProUGUI hpText = CreateTMPText(
            "EnemyHPText",
            nameplateBg.transform,
            initialHP,
            26,
            TextAlignmentOptions.Center,
            font
        );
        hpText.color = new Color(0.90f, 0.38f, 0.38f, 1f);
        SetAnchors(hpText.rectTransform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.50f));

        // 4. Indicador Dinámico de Selección (retícula/flecha flotante con animación bobbing)
        GameObject targetIndicatorGO = new GameObject("TargetIndicator", typeof(RectTransform));
        targetIndicatorGO.transform.SetParent(slotRoot.transform, false);
        RectTransform indRT = targetIndicatorGO.GetComponent<RectTransform>();
        SetAnchors(indRT, new Vector2(0.22f, 0.89f), new Vector2(0.78f, 1.00f));

        GameObject badgeGO = CreatePanel("TargetBadge", targetIndicatorGO.transform, new Color(0.92f, 0.68f, 0.22f, 0.95f));
        SetAnchors(badgeGO.GetComponent<RectTransform>(), new Vector2(0.02f, 0.26f), new Vector2(0.98f, 0.98f));
        Image badgeImg = badgeGO.GetComponent<Image>();
        badgeImg.raycastTarget = false;

        TextMeshProUGUI badgeText = CreateTMPText("BadgeText", badgeGO.transform, "[ OBJETIVO ]", 24, TextAlignmentOptions.Center, font);
        badgeText.color = new Color(0.12f, 0.08f, 0.02f, 1f);
        badgeText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI arrowText = CreateTMPText("ArrowText", targetIndicatorGO.transform, "▼", 30, TextAlignmentOptions.Center, font);
        arrowText.color = new Color(0.96f, 0.78f, 0.22f, 1f);
        SetAnchors(arrowText.rectTransform, new Vector2(0.35f, 0.00f), new Vector2(0.65f, 0.32f));

        targetIndicatorGO.SetActive(false);

        return new EnemyUISlot
        {
            slotRoot = slotRoot,
            borderImage = borderImg,
            enemyPlaceholderImage = placeholderImage,
            enemyAvatarLabelText = labelText,
            enemyNameText = nameText,
            enemyHPText = hpText,
            selectTargetButton = selectBtn,
            targetIndicator = targetIndicatorGO,
            targetIndicatorRect = indRT,
            targetIndicatorImage = badgeImg,
            targetIndicatorText = badgeText,
            groundIndicatorRing = ringImg
        };
    }

    private static void RenderTwoEnemiesCombatPreview(
        Canvas canvas,
        GameObject startPanel,
        GameObject slidePanel,
        GameObject combatPanel,
        GameObject actionGifPanel,
        GameObject combatDialoguePanel,
        string outputDir)
    {
        try
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            Camera cam = Camera.main;
            if (cam == null) return;

            RenderMode oldMode = canvas.renderMode;
            Camera oldCam = canvas.worldCamera;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture prevCamRT = cam.targetTexture;
            cam.targetTexture = rt;
            RenderTexture.active = rt;

            startPanel.SetActive(false);
            slidePanel.SetActive(false);
            combatPanel.SetActive(true);
            actionGifPanel.SetActive(false);
            combatDialoguePanel.SetActive(false);

            Canvas.ForceUpdateCanvases();
            cam.Render();
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(outputDir, "preview_combat_two_enemies.png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            cam.targetTexture = prevCamRT;
            RenderTexture.active = prevRT;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);

            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCam;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RPG Santa Felipina] Preview render warning: {e.Message}");
        }
    }

    private static void DestroyExistingIfFound(string objName)
    {
        GameObject existing = GameObject.Find(objName);
        while (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
            existing = GameObject.Find(objName);
        }
    }

    private static Sprite GetOrCreateWhiteSquareSprite()
    {
        if (!Directory.Exists("Assets/Sprites"))
        {
            Directory.CreateDirectory("Assets/Sprites");
        }

        string spritePath = "Assets/Sprites/PlaceholderSquare.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (existing != null)
            return existing;

        Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[64 * 64];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        tex.SetPixels(pixels);
        tex.Apply();

        File.WriteAllBytes(spritePath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
    }

    private static void EnsureEnemyDataAssets(out EnemyData lacayoRapido, out EnemyData lacayoFuerte, out EnemyData jefeCalacas)
    {
        string folderPath = "Assets/Data/Enemies";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        lacayoRapido = CreateOrUpdateEnemyAsset(
            $"{folderPath}/Lacayo_RapidoDebil.asset",
            "Lacayo Rapido y Debil",
            EnemyType.LacayoRapidoDebil,
            maxHP: 45,
            attack: 10,
            defense: 2,
            motricidad: 10,
            color: new Color(0.75f, 0.38f, 0.10f, 1.0f),
            halfHealthDialogue: new string[]
            {
                "LACAYO RAPIDO: \"Agh! Mis costillas... Oye, tranquilo, solo sigo ordenes de El Calacas!\""
            }
        );

        lacayoFuerte = CreateOrUpdateEnemyAsset(
            $"{folderPath}/Lacayo_LentoFuerte.asset",
            "Lacayo Lento y Fuerte",
            EnemyType.LacayoLentoFuerte,
            maxHP: 75,
            attack: 11,
            defense: 4,
            motricidad: 5,
            color: new Color(0.55f, 0.10f, 0.10f, 1.0f),
            halfHealthDialogue: new string[]
            {
                "LACAYO FUERTE: \"Maldito repartidor... Como lograste atravesar mi guardia?!\"",
                "VOZ [CORPOREO]: \"Incluso el mas grande sangra cuando le das donde duele.\""
            }
        );

        jefeCalacas = CreateOrUpdateEnemyAsset(
            $"{folderPath}/Jefe_ElCalacas.asset",
            "El Calacas",
            EnemyType.JefeElCalacas,
            maxHP: 130,
            attack: 14,
            defense: 5,
            motricidad: 9,
            color: new Color(0.48f, 0.16f, 0.65f, 1.0f),
            halfHealthDialogue: new string[]
            {
                "EL CALACAS: \"Nada mal para un simple repartidor de Santa Felipina... Pero apenas estoy calentando.\"",
                "VOZ [MUNDO INTERNO]: \"El aire se vuelve helado. Ten cuidado, Enrique.\""
            }
        );

        AssetDatabase.SaveAssets();
    }

    private static EnemyData CreateOrUpdateEnemyAsset(
        string assetPath,
        string enemyName,
        EnemyType enemyType,
        int maxHP,
        int attack,
        int defense,
        int motricidad,
        Color color,
        string[] halfHealthDialogue)
    {
        EnemyData asset = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
        bool isNew = (asset == null);
        if (isNew)
        {
            asset = ScriptableObject.CreateInstance<EnemyData>();
        }

        asset.enemyName = enemyName;
        asset.enemyType = enemyType;
        asset.maxHP = maxHP;
        asset.attack = attack;
        asset.defense = defense;
        asset.motricidad = motricidad;
        asset.placeholderColor = color;
        asset.halfHealthDialogueLines = halfHealthDialogue;

        if (isNew)
        {
            AssetDatabase.CreateAsset(asset, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(asset);
        }

        return asset;
    }

    [MenuItem("RPG Santa Felipina/Generar Objetos y Recetas de Prueba")]
    public static void GenerateTestItemsAndRecipesMenu()
    {
        EnsureItemAndRecipeAssets(out _, out _, out _, out _, out _, out _);
        Debug.Log("[RPG Santa Felipina] Objetos y recetas de prueba generados en Assets/Data/Items/.");
    }

    private static void EnsureItemAndRecipeAssets(
        out ItemData itemRefresco,
        out ItemData itemTorta,
        out ItemData itemBotellaVacia,
        out ItemData itemChilePolvo,
        out CraftingRecipeData recetaBombaChile,
        out CraftingRecipeData recetaVendajeRudo)
    {
        if (!Directory.Exists("Assets/Data"))
        {
            Directory.CreateDirectory("Assets/Data");
        }

        string folderPath = "Assets/Data/Items";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // 2 Objetos de Curación Directa
        itemRefresco = CreateOrUpdateItemAsset(
            $"{folderPath}/Item_Refresco.asset",
            itemID: "item_refresco",
            itemName: "Refresco Frio",
            description: "Refresco de cola bien helado en envase de vidrio. Pura azucar y cafeina para seguir pedaleando.",
            combatUseMessage: "Enrique se toma un refresco bien frio de un trago y recupera energia!",
            placeholderColor: new Color(0.35f, 0.95f, 0.48f, 1.0f),
            category: ItemCategory.ConsumibleCuracion,
            healAmount: 35,
            damageAmount: 0,
            curesStatusEffects: false
        );

        itemTorta = CreateOrUpdateItemAsset(
            $"{folderPath}/Item_Torta.asset",
            itemID: "item_torta",
            itemName: "Torta Callejera",
            description: "Torta de milanesa envuelta en papel estraza. Pesada pero altamente restauradora.",
            combatUseMessage: "Enrique devora una torta callejera en segundos y recupera fuerzas!",
            placeholderColor: new Color(0.88f, 0.65f, 0.25f, 1.0f),
            category: ItemCategory.ConsumibleCuracion,
            healAmount: 60,
            damageAmount: 0,
            curesStatusEffects: true
        );

        // 2 Materiales de Calle
        itemBotellaVacia = CreateOrUpdateItemAsset(
            $"{folderPath}/Item_BotellaVacia.asset",
            itemID: "item_botella_vacia",
            itemName: "Botella Vacia",
            description: "Envase de vidrio grueso encontrado en un callejon de Santa Felipina. Ingrediente base para Intelecto.",
            combatUseMessage: "Enrique arroja la botella vacia!",
            placeholderColor: new Color(0.30f, 0.65f, 0.68f, 1.0f),
            category: ItemCategory.MaterialDeCrafteo,
            healAmount: 0,
            damageAmount: 10,
            curesStatusEffects: false
        );

        itemChilePolvo = CreateOrUpdateItemAsset(
            $"{folderPath}/Item_ChilePolvo.asset",
            itemID: "item_chile_polvo",
            itemName: "Gasolina",
            description: "Botecito con gasolina y un trapo empapado. Ingrediente inflamable para armar una Molotov con Intelecto.",
            combatUseMessage: "Enrique arroja gasolina inflamable!",
            placeholderColor: new Color(0.92f, 0.38f, 0.12f, 1.0f),
            category: ItemCategory.MaterialDeCrafteo,
            healAmount: 0,
            damageAmount: 12,
            curesStatusEffects: false
        );

        // 2 Recetas de Intelecto
        recetaBombaChile = CreateOrUpdateRecipeAsset(
            $"{folderPath}/Receta_BombaChile.asset",
            recipeName: "Molotov Improvisada",
            intelectoComment: "VOZ [INTELECTO]: \"Vidrio fragil mas combustible inflamable. Al estallar en llamas frente a su cara ignorara toda su defensa y lo dejara confundido.\"",
            materialA: itemBotellaVacia,
            materialB: itemChilePolvo,
            damageIgnoringDefense: 30,
            healAmount: 0,
            appliesConfusion: true
        );

        recetaVendajeRudo = CreateOrUpdateRecipeAsset(
            $"{folderPath}/Receta_VendajeRudo.asset",
            recipeName: "Vendaje Rudo",
            intelectoComment: "VOZ [INTELECTO]: \"Compresa fria improvisada con vidrio helado y gas carbonico. Cierra las heridas al instante (+50 HP).\"",
            materialA: itemBotellaVacia,
            materialB: itemRefresco,
            damageIgnoringDefense: 0,
            healAmount: 50,
            appliesConfusion: false
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static ItemData CreateOrUpdateItemAsset(
        string assetPath,
        string itemID,
        string itemName,
        string description,
        string combatUseMessage,
        Color placeholderColor,
        ItemCategory category,
        int healAmount,
        int damageAmount,
        bool curesStatusEffects)
    {
        ItemData asset = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
        bool isNew = (asset == null);
        if (isNew)
        {
            asset = ScriptableObject.CreateInstance<ItemData>();
        }

        asset.itemID = itemID;
        asset.itemName = itemName;
        asset.description = description;
        asset.combatUseMessage = combatUseMessage;
        asset.placeholderColor = placeholderColor;
        asset.category = category;
        asset.healAmount = healAmount;
        asset.damageAmount = damageAmount;
        asset.curesStatusEffects = curesStatusEffects;

        if (isNew)
        {
            AssetDatabase.CreateAsset(asset, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(asset);
        }

        return asset;
    }

    private static CraftingRecipeData CreateOrUpdateRecipeAsset(
        string assetPath,
        string recipeName,
        string intelectoComment,
        ItemData materialA,
        ItemData materialB,
        int damageIgnoringDefense,
        int healAmount,
        bool appliesConfusion)
    {
        CraftingRecipeData asset = AssetDatabase.LoadAssetAtPath<CraftingRecipeData>(assetPath);
        bool isNew = (asset == null);
        if (isNew)
        {
            asset = ScriptableObject.CreateInstance<CraftingRecipeData>();
        }

        asset.recipeName = recipeName;
        asset.intelectoComment = intelectoComment;
        asset.materialA = materialA;
        asset.materialB = materialB;
        asset.damageIgnoringDefense = damageIgnoringDefense;
        asset.healAmount = healAmount;
        asset.appliesConfusion = appliesConfusion;

        if (isNew)
        {
            AssetDatabase.CreateAsset(asset, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(asset);
        }

        return asset;
    }

    private static TMP_FontAsset CreateOrLoadPixelzoneFont(bool forceRecreate)
    {
        string[] customFontGuids = AssetDatabase.FindAssets("t:Font", new[] { "Assets/Fonts" });
        foreach (string guid in customFontGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Font customFont = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (customFont != null)
            {
                string targetAssetPath = $"Assets/Fonts/{customFont.name}_TMP.asset";
                TMP_FontAsset existingAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetAssetPath);
                if (existingAsset != null && !forceRecreate && existingAsset.atlasTexture != null)
                {
                    return existingAsset;
                }

                if (existingAsset != null)
                {
                    AssetDatabase.DeleteAsset(targetAssetPath);
                }

                TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
                    customFont,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic
                );

                if (created != null)
                {
                    FaceInfo fi = created.faceInfo;
                    fi.scale = 1.6f;
                    created.faceInfo = fi;

                    created.normalSpacingOffset = 1.0f;
                    created.boldStyle = 0.0f;
                    created.boldSpacing = 0.0f;
                    SerializedObject createdSO = new SerializedObject(created);
                    SerializedProperty clearProp = createdSO.FindProperty("m_ClearDynamicDataOnBuild");
                    if (clearProp != null)
                    {
                        clearProp.boolValue = false;
                        createdSO.ApplyModifiedPropertiesWithoutUndo();
                    }

                    string[] fallbackGuids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
                    if (fallbackGuids.Length > 0)
                    {
                        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fallbackGuids[0]));
                        if (fallback != null && fallback != created)
                        {
                            created.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
                        }
                    }

                    AssetDatabase.CreateAsset(created, targetAssetPath);
                    if (created.atlasTexture != null)
                    {
                        created.atlasTexture.name = $"{customFont.name} Atlas";
                        created.atlasTexture.filterMode = FilterMode.Point;
                        AssetDatabase.AddObjectToAsset(created.atlasTexture, created);
                    }
                    if (created.material != null)
                    {
                        created.material.name = $"{customFont.name} Material";
                        AssetDatabase.AddObjectToAsset(created.material, created);
                    }
                    EditorUtility.SetDirty(created);
                    AssetDatabase.SaveAssets();
                    return created;
                }
            }
        }

        string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        if (fontGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(fontGuids[0]);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        return null;
    }

    private static void SetGameViewTo16By9()
    {
        try
        {
            Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, "Game", false);
            if (gameView == null) return;

            PropertyInfo selectedSizeIndexProp = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (selectedSizeIndexProp != null)
            {
                selectedSizeIndexProp.SetValue(gameView, 1, null);
                gameView.Repaint();
            }
        }
        catch
        {
        }
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

    private static MapInteractable CreateMapInteractableInScene(
        string objectName,
        Vector3 position,
        Sprite placeholderSprite,
        Color color,
        InteractableType type,
        ItemData itemToPickup,
        int quantity,
        string[] pickupDialogue,
        string[] dialogueLines,
        ExplorationDialogueManager dialogueManager,
        TMP_FontAsset font)
    {
        GameObject go = new GameObject(objectName);
        go.transform.position = position;
        go.transform.localScale = (type == InteractableType.PickupItem)
            ? new Vector3(0.65f, 0.65f, 1f)
            : new Vector3(1.1f, 1.4f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = placeholderSprite;
        sr.color = color;
        sr.sortingOrder = 4;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.8f, 1.8f);

        // Pequeño indicador flotante "[E]" sobre el objeto cuando Enrique se acerca
        GameObject promptGO = new GameObject("PromptUI_E");
        promptGO.transform.SetParent(go.transform, false);
        promptGO.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        promptGO.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

        TextMeshPro worldTmp = promptGO.AddComponent<TextMeshPro>();
        if (font != null) worldTmp.font = font;
        worldTmp.text = "[E]";
        worldTmp.fontSize = 36;
        worldTmp.alignment = TextAlignmentOptions.Center;
        worldTmp.color = new Color(0.95f, 0.85f, 0.35f, 1f);
        worldTmp.sortingOrder = 6;
        promptGO.SetActive(false);

        MapInteractable interactable = go.AddComponent<MapInteractable>();
        interactable.type = type;
        interactable.itemToPickup = itemToPickup;
        interactable.quantity = quantity;
        if (pickupDialogue != null) interactable.pickupDialogue = pickupDialogue;
        if (dialogueLines != null) interactable.dialogueLines = dialogueLines;
        interactable.promptUI = promptGO;
        interactable.explorationDialogueManager = dialogueManager;

        EditorUtility.SetDirty(interactable);
        return interactable;
    }

    // =========================================================================
    // CONFIGURACIÓN DE SPRITES Y ANIMACIONES DE HOMELESS_2 (SECUACES DÉBILES)
    // Y HOMELESS_3 (BOSS DE LA ZONA / ENEMIGO MÁS FUERTE)
    // =========================================================================

    [MenuItem("RPG Santa Felipina/Configurar Sprites y Animaciones de Enemigos (Homeless)")]
    public static void SetupHomelessSpritesAndAnimations()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        string sampleScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";
        if (EditorSceneManager.GetActiveScene().path != sampleScenePath && File.Exists(sampleScenePath))
        {
            EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);
        }

        // 1. Recortar uniformemente todas las hojas de sprites en Homeless_2 y Homeless_3 (celdas uniformes 92x80 centradas en cada cuadro de 128x128)
        (string file, int frames)[] h2Sheets = new (string, int)[]
        {
            ("Idle", 7),
            ("Idle_2", 9),
            ("Walk", 8),
            ("Run", 8),
            ("Jump", 12),
            ("Attack_1", 10),
            ("Attack_2", 4),
            ("Attack_3", 4),
            ("Hurt", 3),
            ("Dead", 4)
        };

        foreach (var s in h2Sheets)
        {
            SliceHomelessSheetUniformly($"Assets/Sprites/Homeless_2/{s.file}.png", s.file, s.frames);
        }

        (string file, int frames)[] h3Sheets = new (string, int)[]
        {
            ("Idle", 6),
            ("Idle_2", 11),
            ("Walk", 8),
            ("Run", 8),
            ("Jump", 16),
            ("Special", 13),
            ("Attack_1", 5),
            ("Attack_2", 3),
            ("Hurt", 3),
            ("Dead", 4)
        };

        foreach (var s in h3Sheets)
        {
            SliceHomelessSheetUniformly($"Assets/Sprites/Homeless_3/{s.file}.png", s.file, s.frames);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 2. Cargar los arreglos de Sprites de Homeless_2 (Secuaces Débiles)
        Sprite[] h2Idle = LoadOrderedSprites("Assets/Sprites/Homeless_2/Idle.png");
        Sprite[] h2Idle2 = LoadOrderedSprites("Assets/Sprites/Homeless_2/Idle_2.png");
        Sprite[] h2Walk = LoadOrderedSprites("Assets/Sprites/Homeless_2/Walk.png");
        Sprite[] h2Run = LoadOrderedSprites("Assets/Sprites/Homeless_2/Run.png");
        Sprite[] h2Jump = LoadOrderedSprites("Assets/Sprites/Homeless_2/Jump.png");
        Sprite[] h2Atk1 = LoadOrderedSprites("Assets/Sprites/Homeless_2/Attack_1.png");
        Sprite[] h2Atk2 = LoadOrderedSprites("Assets/Sprites/Homeless_2/Attack_2.png");
        Sprite[] h2Atk3 = LoadOrderedSprites("Assets/Sprites/Homeless_2/Attack_3.png");
        Sprite[] h2Hurt = LoadOrderedSprites("Assets/Sprites/Homeless_2/Hurt.png");
        Sprite[] h2Dead = LoadOrderedSprites("Assets/Sprites/Homeless_2/Dead.png");

        // 3. Cargar los arreglos de Sprites de Homeless_3 (Boss de la Zona / Más Fuerte)
        Sprite[] h3Idle = LoadOrderedSprites("Assets/Sprites/Homeless_3/Idle.png");
        Sprite[] h3Idle2 = LoadOrderedSprites("Assets/Sprites/Homeless_3/Idle_2.png");
        Sprite[] h3Walk = LoadOrderedSprites("Assets/Sprites/Homeless_3/Walk.png");
        Sprite[] h3Run = LoadOrderedSprites("Assets/Sprites/Homeless_3/Run.png");
        Sprite[] h3Jump = LoadOrderedSprites("Assets/Sprites/Homeless_3/Jump.png");
        Sprite[] h3Special = LoadOrderedSprites("Assets/Sprites/Homeless_3/Special.png");
        Sprite[] h3Atk1 = LoadOrderedSprites("Assets/Sprites/Homeless_3/Attack_1.png");
        Sprite[] h3Atk2 = LoadOrderedSprites("Assets/Sprites/Homeless_3/Attack_2.png");
        Sprite[] h3Hurt = LoadOrderedSprites("Assets/Sprites/Homeless_3/Hurt.png");
        Sprite[] h3Dead = LoadOrderedSprites("Assets/Sprites/Homeless_3/Dead.png");

        // 4. Crear todos los AnimationClips (.anim) y AnimatorControllers (.controller) en Assets/Animations
        if (!Directory.Exists("Assets/Animations")) Directory.CreateDirectory("Assets/Animations");
        if (!Directory.Exists("Assets/Animations/Homeless_2")) Directory.CreateDirectory("Assets/Animations/Homeless_2");
        if (!Directory.Exists("Assets/Animations/Homeless_3")) Directory.CreateDirectory("Assets/Animations/Homeless_3");

        AnimatorController h2Controller = BuildHomelessAnimatorController(
            folder: "Assets/Animations/Homeless_2",
            prefix: "Homeless2",
            idle: h2Idle,
            idle2: h2Idle2,
            walk: h2Walk,
            run: h2Run,
            jump: h2Jump,
            attack1: h2Atk1,
            attack2: h2Atk2,
            attack3: h2Atk3,
            special: null,
            hurt: h2Hurt,
            dead: h2Dead
        );

        AnimatorController h3Controller = BuildHomelessAnimatorController(
            folder: "Assets/Animations/Homeless_3",
            prefix: "Homeless3",
            idle: h3Idle,
            idle2: h3Idle2,
            walk: h3Walk,
            run: h3Run,
            jump: h3Jump,
            attack1: h3Atk1,
            attack2: h3Atk2,
            attack3: null,
            special: h3Special,
            hurt: h3Hurt,
            dead: h3Dead
        );

        // 5. Asignar Sprites y Animaciones a los EnemyData (ScriptableObjects)
        EnemyData lacayoCallejero = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Lacayo_CallejeroDebil.asset");
        if (lacayoCallejero != null)
        {
            AssignEnemyAnimationData(
                lacayoCallejero,
                h2Idle, h2Idle2, h2Walk, h2Run,
                h2Atk1, h2Atk2, h2Atk3, null,
                h2Hurt, h2Dead, h2Controller
            );
        }

        EnemyData lacayoRapido = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Lacayo_RapidoDebil.asset");
        if (lacayoRapido != null)
        {
            AssignEnemyAnimationData(
                lacayoRapido,
                h2Idle, h2Idle2, h2Walk, h2Run,
                h2Atk1, h2Atk2, h2Atk3, null,
                h2Hurt, h2Dead, h2Controller
            );
        }

        EnemyData lacayoFuerteBoss = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Lacayo_LentoFuerte.asset");
        if (lacayoFuerteBoss != null)
        {
            lacayoFuerteBoss.enemyName = "Jefe Callejero (Boss)";
            lacayoFuerteBoss.maxHP = 115;
            lacayoFuerteBoss.attack = 18;
            lacayoFuerteBoss.defense = 8;
            lacayoFuerteBoss.motricidad = 7;
            lacayoFuerteBoss.halfHealthDialogueLines = new string[]
            {
                "JEFE CALLEJERO: \"Hasta aqui llegaste, repartidor! (Se estrella la botella en la frente y empuña el pico roto)\"",
                "VOZ [CORPOREO]: \"Se acaba de romper la botella en la cabeza sin parpadear! Cuidado con los vidrios!\"",
                "VOZ [INTELECTO]: \"Su defensa es alta (8). Usa artefactos de Intelecto o cubre el golpe con Motricidad!\""
            };
            AssignEnemyAnimationData(
                lacayoFuerteBoss,
                h3Idle, h3Idle2, h3Walk, h3Run,
                h3Atk1, h3Atk2, null, h3Special,
                h3Hurt, h3Dead, h3Controller
            );
        }

        EnemyData jefeCalacas = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Jefe_ElCalacas.asset");
        if (jefeCalacas != null)
        {
            AssignEnemyAnimationData(
                jefeCalacas,
                h3Idle, h3Idle2, h3Walk, h3Run,
                h3Atk1, h3Atk2, null, h3Special,
                h3Hurt, h3Dead, h3Controller
            );
        }

        AssetDatabase.SaveAssets();

        // 6. Aplicar el primer Homeless (Homeless_2) a todos los secuaces débiles del mapa en SampleScene
        string[] weakMinionNames = new string[]
        {
            "Lacayo_Tutorial",
            "Lacayo_Debil_1",
            "Lacayo_Debil_2",
            "Lacayo_Debil_3",
            "Lacayo_Debil_4",
            "Lacayo_Debil_5"
        };

        Sprite h2DefaultSprite = (h2Idle != null && h2Idle.Length > 0) ? h2Idle[0] : null;
        foreach (string minionName in weakMinionNames)
        {
            GameObject go = GameObject.Find(minionName);
            if (go != null)
            {
                ConfigureMapEnemyVisualAndAnimator(go, h2DefaultSprite, h2Controller, new Vector3(1.35f, 1.35f, 1f));
                if (minionName == "Lacayo_Debil_5")
                {
                    // Separarlo ligeramente hacia el norte para dejarle el final de la calle al Boss de la Zona
                    go.transform.position = new Vector3(20.5f, -18.0f, 0f);
                }
            }
        }

        // 7. Aplicar el segundo Homeless (Homeless_3) al enemigo más fuerte que sirve como Boss de la Zona (Lacayo_Segundo)
        Sprite h3DefaultSprite = (h3Idle != null && h3Idle.Length > 0) ? h3Idle[0] : null;
        GameObject bossGO = GameObject.Find("Lacayo_Segundo");
        if (bossGO != null)
        {
            bossGO.transform.position = new Vector3(20.44f, -22.5f, 0f);
            ConfigureMapEnemyVisualAndAnimator(bossGO, h3DefaultSprite, h3Controller, new Vector3(1.6f, 1.6f, 1f));

            EnemyEncounterTrigger bossTrig = bossGO.GetComponent<EnemyEncounterTrigger>();
            if (bossTrig != null && lacayoFuerteBoss != null)
            {
                if (lacayoRapido != null)
                {
                    bossTrig.enemies = new EnemyData[] { lacayoFuerteBoss, lacayoRapido };
                }
                else
                {
                    bossTrig.enemies = new EnemyData[] { lacayoFuerteBoss };
                }
                bossTrig.enemyData = lacayoFuerteBoss;
                bossTrig.introDialogueLines = new string[]
                {
                    "JEFE CALLEJERO: \"Asi que tu eres el repartidor que anda tumbando a mis muchachos por el barrio...\"",
                    "ENRIQUE BOSQUES: \"Solo quiero entregar este paquete y largarme de aqui.\"",
                    "VOZ [MUNDO INTERNO]: \"Huele a alcohol barato y a violencia vieja. Es el lider de esta calle.\"",
                    "VOZ [MOTRICIDAD]: \"Viene acompanado de un secuaz rapido. Haz clic sobre cualquiera de los dos arriba para elegir tu objetivo!\""
                };
                bossTrig.isBossEncounter = true;
                bossTrig.nextSceneName = "02_Nivel2_Guarida";
                bossTrig.victoryNarrativeLines = new string[]
                {
                    "El Jefe Callejero cae noqueado contra el pavimento, soltando las llaves del almacén clandestino...",
                    "ENRIQUE BOSQUES: \"(Limpiando la sangre de sus nudillos) Se acabó. El rastro conduce detrás de este portón.\"",
                    "VOZ [INTELECTO]: \"Las llaves del jefe abren la reja de acero... Escucho zumbidos de generadores en el interior.\"",
                    "VOZ [MUNDO INTERNO]: \"El aire adentro es frío y denso. Prepárate, Enrique...\"",
                    "[ CAPÍTULO 2: LA GUARIDA DE LA MAFIA ]"
                };
                EditorUtility.SetDirty(bossTrig);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[RPG Santa Felipina] Sprites y animaciones de Homeless_2 (Secuaces Débiles) y Homeless_3 (Boss de Zona) creados y asignados en mapa y combate.");
    }

    [MenuItem("RPG Santa Felipina/Actualizar Fondo de Combate y Slots de Enemigos")]
    public static void UpdateCombatBackgroundAndEnemySlots()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        string sampleScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";
        if (EditorSceneManager.GetActiveScene().path != sampleScenePath && File.Exists(sampleScenePath))
        {
            EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);
        }

        // 1. Configurar la textura de fondo escena1Combate.png
        string bgPath = "Assets/Sprites/escena1Combate.png";
        TextureImporter importer = AssetImporter.GetAtPath(bgPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Sprite combatBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);
        if (combatBgSprite == null)
        {
            UnityEngine.Object[] subs = AssetDatabase.LoadAllAssetsAtPath(bgPath);
            foreach (var o in subs)
            {
                if (o is Sprite s) { combatBgSprite = s; break; }
            }
        }

        BattleManager battleManager = UnityEngine.Object.FindAnyObjectByType<BattleManager>();
        if (battleManager == null || battleManager.battlePanel == null)
        {
            Debug.LogError("[RPG Santa Felipina] No se encontró BattleManager o battlePanel en SampleScene.");
            return;
        }

        GameObject combatPanel = battleManager.battlePanel;
        TMP_FontAsset pixelFont = CreateOrLoadPixelzoneFont(forceRecreate: false);

        Color targetGoldBorder = new Color(0.92f, 0.68f, 0.22f, 1f);
        Color subtleBorder = new Color(0.26f, 0.26f, 0.30f, 1f);
        Color matteBlack = new Color(0.05f, 0.05f, 0.06f, 1f);

        // 2. Crear o actualizar CombatBackgroundViewport y CombatBackground
        // Confinado a la parte superior (y: 0.44 a 1.0) donde están los enemigos,
        // dejando la parte inferior (portrait de Enrique y opciones del menú) con su fondo original oscuro y limpio.
        Transform existingViewport = combatPanel.transform.Find("CombatBackgroundViewport");
        GameObject viewportGO;
        if (existingViewport != null)
        {
            viewportGO = existingViewport.gameObject;
        }
        else
        {
            viewportGO = new GameObject("CombatBackgroundViewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(combatPanel.transform, false);
        }
        SetAnchors(viewportGO.GetComponent<RectTransform>(), new Vector2(0f, 0.44f), new Vector2(1f, 1f));

        Transform existingBg = viewportGO.transform.Find("CombatBackground");
        if (existingBg == null) existingBg = combatPanel.transform.Find("CombatBackground");

        GameObject combatBgGO;
        if (existingBg != null)
        {
            combatBgGO = existingBg.gameObject;
            combatBgGO.transform.SetParent(viewportGO.transform, false);
        }
        else
        {
            combatBgGO = CreatePanel("CombatBackground", viewportGO.transform, Color.white);
        }

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
        battleManager.combatBackgroundImage = bgImg;

        // Línea divisoria elegante (divider) entre el visor de combate y la consola del menú
        Transform existingDivider = combatPanel.transform.Find("CombatViewportDivider");
        GameObject dividerGO;
        if (existingDivider != null)
        {
            dividerGO = existingDivider.gameObject;
        }
        else
        {
            dividerGO = CreatePanel("CombatViewportDivider", combatPanel.transform, subtleBorder);
        }
        SetAnchors(dividerGO.GetComponent<RectTransform>(), new Vector2(0f, 0.437f), new Vector2(1f, 0.442f));
        dividerGO.transform.SetSiblingIndex(1);

        // 3. Eliminar los rectángulos oscuros anteriores de los slots de enemigos
        Transform oldSlot0 = combatPanel.transform.Find("EnemySlot_0");
        if (oldSlot0 != null) UnityEngine.Object.DestroyImmediate(oldSlot0.gameObject);

        Transform oldSlot1 = combatPanel.transform.Find("EnemySlot_1");
        if (oldSlot1 != null) UnityEngine.Object.DestroyImmediate(oldSlot1.gameObject);

        // 4. Ajustar la barra de registro de combate y GIFs para que no tapen a Enrique ni a los pies de los enemigos
        Transform logBorderTr = combatPanel.transform.Find("BattleLogBorder");
        if (logBorderTr != null)
        {
            SetAnchors(logBorderTr.GetComponent<RectTransform>(), new Vector2(0.19f, 0.28f), new Vector2(0.97f, 0.43f));
        }
        Transform actionGifTr = combatPanel.transform.Find("ActionGifPanel");
        if (actionGifTr != null)
        {
            SetAnchors(actionGifTr.GetComponent<RectTransform>(), new Vector2(0.19f, 0.28f), new Vector2(0.97f, 0.43f));
        }

        // 5. Crear los nuevos slots limpios con sprite libre sobre la calle, sombra de contacto, nameplate flotante e indicador dinámico
        EnemyUISlot slot0 = CreateEnemyUISlot(
            "EnemySlot_0",
            combatPanel.transform,
            new Vector2(0.10f, 0.44f),
            new Vector2(0.50f, 0.94f),
            "> [OBJETIVO] LACAYO RAPIDO Y DEBIL (MOT: 10)",
            "HP: 45 / 45",
            new Color(0.75f, 0.38f, 0.10f, 1f),
            "[CLIC PARA APUNTAR]\nLACAYO RAPIDO Y DEBIL",
            targetGoldBorder,
            matteBlack,
            subtleBorder,
            pixelFont
        );

        EnemyUISlot slot1 = CreateEnemyUISlot(
            "EnemySlot_1",
            combatPanel.transform,
            new Vector2(0.52f, 0.44f),
            new Vector2(0.92f, 0.94f),
            "JEFE CALLEJERO (BOSS) (MOT: 7)",
            "HP: 115 / 115",
            new Color(0.55f, 0.10f, 0.10f, 1f),
            "[CLIC PARA APUNTAR]\nJEFE CALLEJERO (BOSS)",
            subtleBorder,
            matteBlack,
            subtleBorder,
            pixelFont
        );

        // Configurar botones de selección al hacer clic sobre el slot del enemigo
        slot0.selectTargetButton.onClick.RemoveAllListeners();
        slot0.selectTargetButton.onClick.AddListener(() => battleManager.SelectTargetEnemy(0));
        slot1.selectTargetButton.onClick.RemoveAllListeners();
        slot1.selectTargetButton.onClick.AddListener(() => battleManager.SelectTargetEnemy(1));

        battleManager.enemySlots = new EnemyUISlot[] { slot0, slot1 };

        // Mostrar sprites de prueba en el Editor para previsualizar cómo se ven sobre la calle
        Sprite h2Sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Homeless_2/Idle_0");
        if (h2Sprite == null)
        {
            var subs = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Homeless_2/Idle.png");
            foreach (var o in subs) { if (o is Sprite s) { h2Sprite = s; break; } }
        }
        if (h2Sprite != null) slot0.enemyPlaceholderImage.sprite = h2Sprite;

        Sprite h3Sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Homeless_3/Idle_0");
        if (h3Sprite == null)
        {
            var subs = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Homeless_3/Idle.png");
            foreach (var o in subs) { if (o is Sprite s) { h3Sprite = s; break; } }
        }
        if (h3Sprite != null) slot1.enemyPlaceholderImage.sprite = h3Sprite;

        // Activar el indicador dinámico en el objetivo 0 para previsualizarlo
        slot0.targetIndicator.SetActive(true);
        slot1.targetIndicator.SetActive(false);

        // Asegurar orden de sub-paneles superiores
        if (battleManager.actionGifPanel != null) battleManager.actionGifPanel.transform.SetAsLastSibling();
        if (battleManager.intelectoAndItemsPanel != null) battleManager.intelectoAndItemsPanel.transform.SetAsLastSibling();
        if (battleManager.mundoInternoPanel != null) battleManager.mundoInternoPanel.transform.SetAsLastSibling();
        if (battleManager.combatDialoguePanel != null) battleManager.combatDialoguePanel.transform.SetAsLastSibling();

        // Renderizar preview para verificar
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            try
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    combatPanel.SetActive(true);
                    RenderMode oldMode = canvas.renderMode;
                    Camera oldCam = canvas.worldCamera;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 1f;

                    RenderTexture rt = new RenderTexture(1920, 1080, 24);
                    RenderTexture prevRT = RenderTexture.active;
                    RenderTexture prevCamRT = cam.targetTexture;
                    cam.targetTexture = rt;
                    RenderTexture.active = rt;

                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes("Assets/preview_combat.png", tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);

                    cam.targetTexture = prevCamRT;
                    RenderTexture.active = prevRT;
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);

                    canvas.renderMode = oldMode;
                    canvas.worldCamera = oldCam;
                    combatPanel.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RPG Santa Felipina] Preview render warning: {ex.Message}");
            }
        }

        EditorUtility.SetDirty(battleManager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[RPG Santa Felipina] Fondo escena1Combate aplicado, rectángulos eliminados e indicador dinámico configurado.");
    }

    private static void ConfigureMapEnemyVisualAndAnimator(
        GameObject go,
        Sprite defaultSprite,
        RuntimeAnimatorController controller,
        Vector3 scale)
    {
        if (go == null) return;

        go.transform.localScale = scale;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = defaultSprite;
        sr.color = Color.white;
        sr.sortingOrder = 5;
        EditorUtility.SetDirty(sr);

        Animator anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        EditorUtility.SetDirty(anim);

        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            col.size = new Vector2(1.4f, 1.5f);
            col.offset = Vector2.zero;
            EditorUtility.SetDirty(col);
        }

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        EditorUtility.SetDirty(rb);

        EnemyFollowAI ai = go.GetComponent<EnemyFollowAI>();
        if (ai == null) ai = go.AddComponent<EnemyFollowAI>();
        ai.animator = anim;
        ai.spriteRenderer = sr;
        bool isBoss = (go.name == "Lacayo_Segundo");
        ai.detectionRadius = isBoss ? 6.8f : 5.8f;
        ai.stoppingDistance = isBoss ? 1.35f : 1.20f;
        ai.leashRadius = isBoss ? 15.0f : 12.0f;
        ai.wanderRadius = isBoss ? 1.5f : 2.0f;
        ai.walkSpeed = 2.0f;
        ai.chaseSpeed = isBoss ? 3.3f : 3.5f;
        ai.enableWander = true;
        EditorUtility.SetDirty(ai);

        EditorUtility.SetDirty(go);
    }

    private static void AssignEnemyAnimationData(
        EnemyData enemy,
        Sprite[] idle,
        Sprite[] idleAlt,
        Sprite[] walk,
        Sprite[] run,
        Sprite[] atk1,
        Sprite[] atk2,
        Sprite[] atk3,
        Sprite[] special,
        Sprite[] hurt,
        Sprite[] dead,
        RuntimeAnimatorController controller)
    {
        if (enemy == null) return;

        enemy.enemySprite = (idle != null && idle.Length > 0) ? idle[0] : null;
        enemy.animatorController = controller;
        enemy.idleFrames = idle;
        enemy.idleAltFrames = idleAlt;
        enemy.walkFrames = walk;
        enemy.runFrames = run;
        enemy.attackFrames = atk1;
        enemy.attack2Frames = atk2;
        enemy.attack3Frames = atk3;
        enemy.specialFrames = special;
        enemy.hurtFrames = hurt;
        enemy.deadFrames = dead;

        EditorUtility.SetDirty(enemy);
    }

    private static AnimatorController BuildHomelessAnimatorController(
        string folder,
        string prefix,
        Sprite[] idle,
        Sprite[] idle2,
        Sprite[] walk,
        Sprite[] run,
        Sprite[] jump,
        Sprite[] attack1,
        Sprite[] attack2,
        Sprite[] attack3,
        Sprite[] special,
        Sprite[] hurt,
        Sprite[] dead)
    {
        AnimationClip idleClip = CreateSpriteAnimationClip($"{folder}/{prefix}_Idle.anim", idle, 7f, loop: true);
        AnimationClip idle2Clip = CreateSpriteAnimationClip($"{folder}/{prefix}_Idle_2.anim", idle2, 7f, loop: false);
        AnimationClip walkClip = CreateSpriteAnimationClip($"{folder}/{prefix}_Walk.anim", walk, 9f, loop: true);
        AnimationClip runClip = CreateSpriteAnimationClip($"{folder}/{prefix}_Run.anim", run, 11f, loop: true);
        AnimationClip jumpClip = CreateSpriteAnimationClip($"{folder}/{prefix}_Jump.anim", jump, 10f, loop: false);
        AnimationClip atk1Clip = CreateSpriteAnimationClip($"{folder}/{prefix}_Attack_1.anim", attack1, 10f, loop: false);
        AnimationClip atk2Clip = CreateSpriteAnimationClip($"{folder}/{prefix}_Attack_2.anim", attack2, 9f, loop: false);
        AnimationClip atk3Clip = (attack3 != null && attack3.Length > 0)
            ? CreateSpriteAnimationClip($"{folder}/{prefix}_Attack_3.anim", attack3, 9f, loop: false)
            : null;
        AnimationClip specialClip = (special != null && special.Length > 0)
            ? CreateSpriteAnimationClip($"{folder}/{prefix}_Special.anim", special, 9f, loop: false)
            : null;
        AnimationClip hurtClip = CreateSpriteAnimationClip($"{folder}/{prefix}_Hurt.anim", hurt, 8f, loop: false);
        AnimationClip deadClip = CreateSpriteAnimationClip($"{folder}/{prefix}_Dead.anim", dead, 7f, loop: false);

        string controllerPath = $"{folder}/{prefix}_Controller.controller";
        if (File.Exists(controllerPath))
        {
            AssetDatabase.DeleteAsset(controllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack3", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Special", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idleState = sm.AddState("Idle", new Vector3(260, 60, 0));
        idleState.motion = idleClip;
        sm.defaultState = idleState;

        AnimatorState idle2State = sm.AddState("Idle_2", new Vector3(540, 60, 0));
        idle2State.motion = idle2Clip;

        // Alternar automáticamente entre Idle (2 repeticiones) e Idle_2 (agacharse / beber botella) en el mapa
        AnimatorStateTransition toIdle2 = idleState.AddTransition(idle2State);
        toIdle2.hasExitTime = true;
        toIdle2.exitTime = 2.0f;
        toIdle2.duration = 0f;

        AnimatorStateTransition backToIdle = idle2State.AddTransition(idleState);
        backToIdle.hasExitTime = true;
        backToIdle.exitTime = 1.0f;
        backToIdle.duration = 0f;

        AnimatorState walkState = sm.AddState("Walk", new Vector3(260, 160, 0));
        walkState.motion = walkClip;
        AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0f;
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
        AnimatorStateTransition fromWalk = walkState.AddTransition(idleState);
        fromWalk.hasExitTime = false;
        fromWalk.duration = 0f;
        fromWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");

        AnimatorState runState = sm.AddState("Run", new Vector3(540, 160, 0));
        runState.motion = runClip;
        AnimatorStateTransition toRun = idleState.AddTransition(runState);
        toRun.hasExitTime = false;
        toRun.duration = 0f;
        toRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");
        AnimatorStateTransition fromRun = runState.AddTransition(idleState);
        fromRun.hasExitTime = false;
        fromRun.duration = 0f;
        fromRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

        if (jumpClip != null)
        {
            AnimatorState jumpState = sm.AddState("Jump", new Vector3(540, 240, 0));
            jumpState.motion = jumpClip;
        }

        if (atk1Clip != null)
        {
            AnimatorState atk1State = sm.AddState("Attack_1", new Vector3(260, -40, 0));
            atk1State.motion = atk1Clip;
            AnimatorStateTransition t = sm.AddAnyStateTransition(atk1State);
            t.hasExitTime = false;
            t.duration = 0f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack1");
            AnimatorStateTransition ret = atk1State.AddTransition(idleState);
            ret.hasExitTime = true;
            ret.exitTime = 1f;
            ret.duration = 0f;
        }

        if (atk2Clip != null)
        {
            AnimatorState atk2State = sm.AddState("Attack_2", new Vector3(500, -40, 0));
            atk2State.motion = atk2Clip;
            AnimatorStateTransition t = sm.AddAnyStateTransition(atk2State);
            t.hasExitTime = false;
            t.duration = 0f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack2");
            AnimatorStateTransition ret = atk2State.AddTransition(idleState);
            ret.hasExitTime = true;
            ret.exitTime = 1f;
            ret.duration = 0f;
        }

        if (atk3Clip != null)
        {
            AnimatorState atk3State = sm.AddState("Attack_3", new Vector3(740, -40, 0));
            atk3State.motion = atk3Clip;
            AnimatorStateTransition t = sm.AddAnyStateTransition(atk3State);
            t.hasExitTime = false;
            t.duration = 0f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack3");
            AnimatorStateTransition ret = atk3State.AddTransition(idleState);
            ret.hasExitTime = true;
            ret.exitTime = 1f;
            ret.duration = 0f;
        }

        if (specialClip != null)
        {
            AnimatorState specialState = sm.AddState("Special", new Vector3(740, 60, 0));
            specialState.motion = specialClip;
            AnimatorStateTransition t = sm.AddAnyStateTransition(specialState);
            t.hasExitTime = false;
            t.duration = 0f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Special");
            AnimatorStateTransition ret = specialState.AddTransition(idleState);
            ret.hasExitTime = true;
            ret.exitTime = 1f;
            ret.duration = 0f;
        }

        if (hurtClip != null)
        {
            AnimatorState hurtState = sm.AddState("Hurt", new Vector3(260, -130, 0));
            hurtState.motion = hurtClip;
            AnimatorStateTransition t = sm.AddAnyStateTransition(hurtState);
            t.hasExitTime = false;
            t.duration = 0f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
            AnimatorStateTransition ret = hurtState.AddTransition(idleState);
            ret.hasExitTime = true;
            ret.exitTime = 1f;
            ret.duration = 0f;
        }

        if (deadClip != null)
        {
            AnimatorState deadState = sm.AddState("Dead", new Vector3(500, -130, 0));
            deadState.motion = deadClip;
            AnimatorStateTransition t = sm.AddAnyStateTransition(deadState);
            t.hasExitTime = false;
            t.duration = 0f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Dead");
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip CreateSpriteAnimationClip(
        string clipPath,
        Sprite[] sprites,
        float fps,
        bool loop)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        bool isNew = (clip == null);
        if (isNew)
        {
            clip = new AnimationClip();
        }
        else
        {
            clip.ClearCurves();
        }

        clip.frameRate = fps;

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[sprites.Length];
        float frameTime = 1f / fps;
        for (int i = 0; i < sprites.Length; i++)
        {
            keyFrames[i] = new ObjectReferenceKeyframe
            {
                time = i * frameTime,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyFrames);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNew)
        {
            AssetDatabase.CreateAsset(clip, clipPath);
        }
        else
        {
            EditorUtility.SetDirty(clip);
        }

        return clip;
    }

    private static void SliceHomelessSheetUniformly(string assetPath, string baseName, int frameCount)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = 48f;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        SerializedObject so = new SerializedObject(importer);
        SerializedProperty spritesProp = so.FindProperty("m_SpriteSheet.m_Sprites");
        SerializedProperty nameTableProp = so.FindProperty("m_InternalIDToNameTable");

        if (spritesProp != null)
        {
            spritesProp.arraySize = frameCount;
            for (int i = 0; i < frameCount; i++)
            {
                SerializedProperty elem = spritesProp.GetArrayElementAtIndex(i);
                string spriteName = $"{baseName}_{i}";

                SerializedProperty nameProp = elem.FindPropertyRelative("m_Name");
                if (nameProp != null) nameProp.stringValue = spriteName;

                SerializedProperty rectProp = elem.FindPropertyRelative("m_Rect");
                if (rectProp != null) rectProp.rectValue = new Rect(i * 128 + 16, 0, 92, 80);

                SerializedProperty alignProp = elem.FindPropertyRelative("m_Alignment");
                if (alignProp != null) alignProp.intValue = 9; // Custom Pivot

                SerializedProperty pivotProp = elem.FindPropertyRelative("m_Pivot");
                if (pivotProp != null) pivotProp.vector2Value = new Vector2(0.5f, 0.42f);

                SerializedProperty spriteIdProp = elem.FindPropertyRelative("m_SpriteID");
                if (spriteIdProp != null && string.IsNullOrEmpty(spriteIdProp.stringValue))
                {
                    spriteIdProp.stringValue = GUID.Generate().ToString();
                }

                SerializedProperty internalIdProp = elem.FindPropertyRelative("m_InternalID");
                if (internalIdProp != null && internalIdProp.longValue == 0)
                {
                    internalIdProp.longValue = 4723107735738678500L + i;
                }
            }
        }

        if (nameTableProp != null && spritesProp != null)
        {
            nameTableProp.arraySize = frameCount;
            for (int i = 0; i < frameCount; i++)
            {
                SerializedProperty spriteElem = spritesProp.GetArrayElementAtIndex(i);
                long internalId = spriteElem.FindPropertyRelative("m_InternalID").longValue;
                string spriteName = spriteElem.FindPropertyRelative("m_Name").stringValue;

                SerializedProperty tableElem = nameTableProp.GetArrayElementAtIndex(i);
                SerializedProperty firstProp = tableElem.FindPropertyRelative("first");
                if (firstProp != null)
                {
                    SerializedProperty idProp = firstProp.FindPropertyRelative("213");
                    if (idProp != null) idProp.longValue = internalId;
                }
                SerializedProperty secondProp = tableElem.FindPropertyRelative("second");
                if (secondProp != null) secondProp.stringValue = spriteName;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadOrderedSprites(string assetPath)
    {
        UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (all == null || all.Length == 0)
            return Array.Empty<Sprite>();

        List<Sprite> sprites = new List<Sprite>();
        foreach (UnityEngine.Object obj in all)
        {
            if (obj is Sprite s)
            {
                sprites.Add(s);
            }
        }

        sprites.Sort((a, b) =>
        {
            int idxA = ExtractTrailingIndex(a.name);
            int idxB = ExtractTrailingIndex(b.name);
            return idxA.CompareTo(idxB);
        });

        return sprites.ToArray();
    }

    [MenuItem("RPG Santa Felipina/Configurar Sub-Panel y Habilidades de Mundo Interno")]
    public static void SetupMundoInternoSubPanelAndSkills()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        string sampleScenePath = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";
        if (EditorSceneManager.GetActiveScene().path != sampleScenePath && File.Exists(sampleScenePath))
        {
            EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);
        }

        BattleManager battleManager = UnityEngine.Object.FindAnyObjectByType<BattleManager>();
        if (battleManager == null || battleManager.battlePanel == null)
        {
            Debug.LogError("[MundoInternoSetup] No se encontro BattleManager o battlePanel en la escena.");
            return;
        }

        GameObject combatPanel = battleManager.battlePanel;
        TMP_FontAsset pixelFont = CreateOrLoadPixelzoneFont(forceRecreate: false);

        Color boneWhite = new Color(0.92f, 0.90f, 0.86f, 1f);
        Color subtleBorder = new Color(0.26f, 0.26f, 0.30f, 1f);
        Color matteBlack = new Color(0.05f, 0.05f, 0.06f, 1f);
        Color violetBorder = new Color(0.58f, 0.26f, 0.78f, 1f);

        Transform oldPanel = combatPanel.transform.Find("MundoInternoPanel");
        if (oldPanel != null)
        {
            UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);
        }

        GameObject mundoInternoPanel = CreatePanel("MundoInternoPanel", combatPanel.transform, violetBorder);
        SetAnchors(mundoInternoPanel.GetComponent<RectTransform>(), new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.53f));

        GameObject mundoInternoInnerBox = CreatePanel("MundoInternoInnerBox", mundoInternoPanel.transform, new Color(0.03f, 0.03f, 0.04f, 0.99f));
        SetAnchors(mundoInternoInnerBox.GetComponent<RectTransform>(), new Vector2(0.003f, 0.008f), new Vector2(0.997f, 0.992f));

        TextMeshProUGUI mundoInternoTitleText = CreateTMPText(
            "MundoInternoTitleText",
            mundoInternoInnerBox.transform,
            "MUNDO INTERNO: PSICOLOGIA Y EMOCIONES DE ENRIQUE",
            34,
            TextAlignmentOptions.Center,
            pixelFont
        );
        mundoInternoTitleText.color = new Color(0.78f, 0.45f, 0.98f, 1f);
        SetAnchors(mundoInternoTitleText.rectTransform, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.98f));

        TextMeshProUGUI mundoInternoDescText = CreateTMPText(
            "MundoInternoDescriptionText",
            mundoInternoInnerBox.transform,
            "Elige como influir en la psique del combate (no gasta turno si cancelas):",
            26,
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
            28,
            pixelFont
        );
        SetAnchors(insultarBtn.GetComponent<RectTransform>(), new Vector2(0.05f, 0.53f), new Vector2(0.95f, 0.70f));

        Button animarseBtn = CreateNoirButton(
            "AnimarseButton",
            mundoInternoInnerBox.transform,
            "[2. ANIMARSE] - Enrique se auto-sugestiona (+5 ATQ para el proximo golpe Corporeo)",
            new Color(0.07f, 0.06f, 0.03f, 1f),
            new Color(0.92f, 0.72f, 0.22f, 1f),
            28,
            pixelFont
        );
        SetAnchors(animarseBtn.GetComponent<RectTransform>(), new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.51f));

        Button delirioBtn = CreateNoirButton(
            "DelirioButton",
            mundoInternoInnerBox.transform,
            "[3. DELIRIO PSIQUICO] - Confunde al rival haciendole perder su proximo turno",
            new Color(0.05f, 0.03f, 0.07f, 1f),
            violetBorder,
            28,
            pixelFont
        );
        SetAnchors(delirioBtn.GetComponent<RectTransform>(), new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.33f));

        Button cancelMundoInternoButton = CreateNoirButton(
            "CancelMundoInternoButton",
            mundoInternoInnerBox.transform,
            "CANCELAR / VOLVER A LAS 4 VOCES (NO GASTA TURNO)",
            matteBlack,
            subtleBorder,
            26,
            pixelFont
        );
        SetAnchors(cancelMundoInternoButton.GetComponent<RectTransform>(), new Vector2(0.22f, 0.02f), new Vector2(0.78f, 0.14f));

        CombatMundoInternoUI mundoInternoUI = mundoInternoPanel.AddComponent<CombatMundoInternoUI>();
        mundoInternoUI.battleManager = battleManager;
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

        mundoInternoPanel.SetActive(false);

        battleManager.mundoInternoPanel = mundoInternoPanel;
        battleManager.mundoInternoUI = mundoInternoUI;

        // Asegurar que quede al frente cuando se abra
        mundoInternoPanel.transform.SetAsLastSibling();
        if (battleManager.combatDialoguePanel != null)
            battleManager.combatDialoguePanel.transform.SetAsLastSibling();

        EditorUtility.SetDirty(battleManager);
        EditorUtility.SetDirty(mundoInternoUI);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[MundoInternoSetup] ¡Sub-panel de Mundo Interno con Insultar, Animarse y Delirio configurado exitosamente!");
    }

    private static int ExtractTrailingIndex(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        int lastUnderscore = name.LastIndexOf('_');
        if (lastUnderscore >= 0 && lastUnderscore + 1 < name.Length)
        {
            if (int.TryParse(name.Substring(lastUnderscore + 1), out int val))
                return val;
        }
        return 0;
    }
}
#endif
