#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class PlayerPersonajeSetup
{
    public const float SPRITE_PPU = 64f;
    public static readonly Vector2 SPRITE_PIVOT = new Vector2(0.5f, 4f / 128f); // 0.03125f: suelas tocando el suelo en Y=4 (128x128)

    private const string SPRITES_FOLDER = "Assets/Sprites/Personaje";
    private const string ANIM_FOLDER = "Assets/Animations/Player";
    private const string CONTROLLER_PATH = "Assets/Animations/Player/Player_Controller.controller";

    [MenuItem("Tools/Configurar Personaje Principal")]
    public static void SetupPlayerPersonajeFull()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("[PlayerPersonajeSetup] Saliendo de Play Mode antes de configurar.");
            return;
        }

        Debug.Log("[PlayerPersonajeSetup] === Iniciando configuración de Sprites y Animaciones de Personaje ===");

        // 1. Reimportar los 27 sprites de Personaje con PPU=64, FilterMode=Point, Uncompressed y Pivot en los pies
        ReimportAllPersonajeSprites();

        // 2. Cargar sprites
        Sprite standDown = LoadSprite($"{SPRITES_FOLDER}/stand_down.png");
        Sprite standSide = LoadSprite($"{SPRITES_FOLDER}/stand_side.png");
        Sprite standUp = LoadSprite($"{SPRITES_FOLDER}/stand_up.png");

        Sprite[] walkDownFrames = LoadFrames($"{SPRITES_FOLDER}/walk_down", 8);
        Sprite[] walkSideFrames = LoadFrames($"{SPRITES_FOLDER}/walk_side", 8);
        Sprite[] walkUpFrames = LoadFrames($"{SPRITES_FOLDER}/walk_up", 8);

        if (standDown == null || standSide == null || standUp == null ||
            walkDownFrames.Any(s => s == null) ||
            walkSideFrames.Any(s => s == null) ||
            walkUpFrames.Any(s => s == null))
        {
            Debug.LogError("[PlayerPersonajeSetup] Error: No se pudieron cargar todos los sprites requeridos de Assets/Sprites/Personaje.");
            return;
        }

        // 3. Crear carpeta de animaciones si no existe
        if (!Directory.Exists(ANIM_FOLDER))
        {
            Directory.CreateDirectory(ANIM_FOLDER);
            AssetDatabase.Refresh();
        }

        // 4. Crear AnimationClips independientes
        AnimationClip clipIdleDown = CreateSpriteAnimationClip($"{ANIM_FOLDER}/Player_Idle_Down.anim", new[] { standDown }, 8f, true);
        AnimationClip clipIdleSide = CreateSpriteAnimationClip($"{ANIM_FOLDER}/Player_Idle_Side.anim", new[] { standSide }, 8f, true);
        AnimationClip clipIdleUp = CreateSpriteAnimationClip($"{ANIM_FOLDER}/Player_Idle_Up.anim", new[] { standUp }, 8f, true);

        AnimationClip clipWalkDown = CreateSpriteAnimationClip($"{ANIM_FOLDER}/Player_Walk_Down.anim", walkDownFrames, 10f, true);
        AnimationClip clipWalkSide = CreateSpriteAnimationClip($"{ANIM_FOLDER}/Player_Walk_Side.anim", walkSideFrames, 10f, true);
        AnimationClip clipWalkUp = CreateSpriteAnimationClip($"{ANIM_FOLDER}/Player_Walk_Up.anim", walkUpFrames, 10f, true);

        // Actualizar también los clips clásicos (Player_Idle, Player_Walk, Player_Run) para compatibilidad
        CreateSpriteAnimationClip($"{ANIM_FOLDER}/Player_Idle.anim", new[] { standDown }, 8f, true);
        CreateSpriteAnimationClip($"{ANIM_FOLDER}/Player_Walk.anim", walkDownFrames, 10f, true);
        CreateSpriteAnimationClip($"{ANIM_FOLDER}/Player_Run.anim", walkSideFrames, 14f, true);

        // 5. Crear/Actualizar AnimatorController con Blend Trees 2D Directional
        if (File.Exists(CONTROLLER_PATH))
        {
            AssetDatabase.DeleteAsset(CONTROLLER_PATH);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("LookX", AnimatorControllerParameterType.Float);
        controller.AddParameter("LookY", AnimatorControllerParameterType.Float);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        // Estado Idle con Blend Tree 2D Simple Directional
        BlendTree idleTree;
        AnimatorState idleState = controller.CreateBlendTreeInController("Idle", out idleTree);
        idleState.name = "Idle";
        idleTree.name = "Idle Blend Tree";
        idleTree.blendType = BlendTreeType.SimpleDirectional2D;
        idleTree.blendParameter = "LookX";
        idleTree.blendParameterY = "LookY";
        idleTree.AddChild(clipIdleDown, new Vector2(0f, -1f));
        idleTree.AddChild(clipIdleUp, new Vector2(0f, 1f));
        idleTree.AddChild(clipIdleSide, new Vector2(1f, 0f));
        idleTree.AddChild(clipIdleSide, new Vector2(-1f, 0f));
        sm.defaultState = idleState;

        // Estado Walk con Blend Tree 2D Simple Directional
        BlendTree walkTree;
        AnimatorState walkState = controller.CreateBlendTreeInController("Walk", out walkTree);
        walkState.name = "Walk";
        walkTree.name = "Walk Blend Tree";
        walkTree.blendType = BlendTreeType.SimpleDirectional2D;
        walkTree.blendParameter = "MoveX";
        walkTree.blendParameterY = "MoveY";
        walkTree.AddChild(clipWalkDown, new Vector2(0f, -1f));
        walkTree.AddChild(clipWalkUp, new Vector2(0f, 1f));
        walkTree.AddChild(clipWalkSide, new Vector2(1f, 0f));
        walkTree.AddChild(clipWalkSide, new Vector2(-1f, 0f));

        // Estado Run con Blend Tree 2D Simple Directional (1.45x velocidad)
        BlendTree runTree;
        AnimatorState runState = controller.CreateBlendTreeInController("Run", out runTree);
        runState.name = "Run";
        runState.speed = 1.45f;
        runTree.name = "Run Blend Tree";
        runTree.blendType = BlendTreeType.SimpleDirectional2D;
        runTree.blendParameter = "MoveX";
        runTree.blendParameterY = "MoveY";
        runTree.AddChild(clipWalkDown, new Vector2(0f, -1f));
        runTree.AddChild(clipWalkUp, new Vector2(0f, 1f));
        runTree.AddChild(clipWalkSide, new Vector2(1f, 0f));
        runTree.AddChild(clipWalkSide, new Vector2(-1f, 0f));

        // Transiciones Idle <-> Walk
        AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0f;
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        toWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

        AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0f;
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        // Transiciones Walk <-> Run
        AnimatorStateTransition walkToRun = walkState.AddTransition(runState);
        walkToRun.hasExitTime = false;
        walkToRun.duration = 0f;
        walkToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");

        AnimatorStateTransition runToWalk = runState.AddTransition(walkState);
        runToWalk.hasExitTime = false;
        runToWalk.duration = 0f;
        runToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");
        runToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");

        // Transiciones Idle <-> Run
        AnimatorStateTransition toRunFromIdle = idleState.AddTransition(runState);
        toRunFromIdle.hasExitTime = false;
        toRunFromIdle.duration = 0f;
        toRunFromIdle.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        toRunFromIdle.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");

        AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0f;
        runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        AssetDatabase.SaveAssets();

        // 6. Aplicar la configuración al protagonista en las escenas del proyecto
        string activeScenePath = SceneManager.GetActiveScene().path;

        ApplyToSceneFile("Assets/Scenes/01_Nivel1_SantaFelipina.unity", controller, standDown);
        ApplyToSceneFile("Assets/Scenes/02_Nivel2_Guarida.unity", controller, standDown);

        // Restaurar escena activa si estaba abierta
        if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
        {
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerPersonajeSetup] ¡Personaje principal configurado exitosamente con sprites de Personaje y animaciones 4 direcciones!");
    }

    private static void ReimportAllPersonajeSprites()
    {
        string[] pngFiles = Directory.GetFiles(SPRITES_FOLDER, "*.png", SearchOption.TopDirectoryOnly);
        foreach (string file in pngFiles)
        {
            string unityPath = file.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SPRITE_PPU;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;

            TextureImporterSettings tis = new TextureImporterSettings();
            importer.ReadTextureSettings(tis);
            tis.spriteAlignment = (int)SpriteAlignment.Custom;
            tis.spritePivot = SPRITE_PIVOT;
            tis.filterMode = FilterMode.Point;
            importer.SetTextureSettings(tis);

            importer.spritePivot = SPRITE_PIVOT;

            // Limpiar tabla previa de spriteSheet si existía
            SerializedObject so = new SerializedObject(importer);
            SerializedProperty spritesProp = so.FindProperty("m_SpriteSheet.m_Sprites");
            if (spritesProp != null && spritesProp.isArray) spritesProp.ClearArray();
            SerializedProperty nameTableProp = so.FindProperty("m_InternalIDToNameTable");
            if (nameTableProp != null && nameTableProp.isArray) nameTableProp.ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();

            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;

        UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var asset in allAssets)
        {
            if (asset is Sprite spr) return spr;
        }

        return null;
    }

    private static Sprite[] LoadFrames(string prefix, int count)
    {
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            string path = $"{prefix}_{i:D2}.png";
            frames[i] = LoadSprite(path);
        }
        return frames;
    }

    private static AnimationClip CreateSpriteAnimationClip(string clipPath, Sprite[] sprites, float fps, bool loop)
    {
        if (sprites == null || sprites.Length == 0) return null;

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        bool isNew = (clip == null);
        if (isNew) clip = new AnimationClip();
        else clip.ClearCurves();

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

        if (isNew) AssetDatabase.CreateAsset(clip, clipPath);
        else EditorUtility.SetDirty(clip);

        return clip;
    }

    private static void ApplyToSceneFile(string scenePath, RuntimeAnimatorController controller, Sprite defaultSprite)
    {
        if (!File.Exists(scenePath)) return;

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) return;

        GameObject playerGO = GameObject.Find("Enrique_Player");
        if (playerGO == null)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Enrique_Player") { playerGO = root; break; }
                Transform t = root.transform.Find("Enrique_Player");
                if (t != null) { playerGO = t.gameObject; break; }
            }
        }

        if (playerGO != null)
        {
            Transform visualTransform = playerGO.transform.Find("Visual");
            GameObject visualGO;
            if (visualTransform == null)
            {
                visualGO = new GameObject("Visual");
                visualGO.transform.SetParent(playerGO.transform, false);
            }
            else
            {
                visualGO = visualTransform.gameObject;
            }

            visualGO.transform.localPosition = Vector3.zero;
            visualGO.transform.localRotation = Quaternion.identity;
            visualGO.transform.localScale = Vector3.one;

            SpriteRenderer sr = visualGO.GetComponent<SpriteRenderer>();
            if (sr == null) sr = visualGO.AddComponent<SpriteRenderer>();

            // Eliminar SpriteRenderer en root si existía
            SpriteRenderer rootSR = playerGO.GetComponent<SpriteRenderer>();
            if (rootSR != null) UnityEngine.Object.DestroyImmediate(rootSR);

            sr.sprite = defaultSprite;
            sr.flipX = false;
            sr.color = Color.white;
            sr.sortingOrder = 5;
            EditorUtility.SetDirty(sr);

            Animator anim = visualGO.GetComponent<Animator>();
            if (anim == null) anim = visualGO.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(anim);

            // Eliminar Animator en root si existía
            Animator rootAnim = playerGO.GetComponent<Animator>();
            if (rootAnim != null) UnityEngine.Object.DestroyImmediate(rootAnim);

            BoxCollider2D col = playerGO.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.offset = new Vector2(0f, 0.74f);
                col.size = new Vector2(0.7f, 1.48f);
                EditorUtility.SetDirty(col);
            }

            PlayerController2D pc = playerGO.GetComponent<PlayerController2D>();
            if (pc != null)
            {
                pc.visualTransform = visualGO.transform;
                pc.animator = anim;
                pc.spriteRenderer = sr;
                pc.moveSpeed = 5.5f;
                pc.runSpeed = 8.0f;
                pc.canMove = true;
                pc.ResetVisualTransform();
                EditorUtility.SetDirty(pc);
            }

            EditorUtility.SetDirty(visualGO);
            EditorUtility.SetDirty(playerGO);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PlayerPersonajeSetup] Escena '{scenePath}' actualizada exitosamente.");
        }
    }

    [MenuItem("Tools/Configurar Barra de Vida Visual")]
    public static void SetupPlayerHealthBarsVisual()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("[PlayerPersonajeSetup] Saliendo de Play Mode antes de configurar barra de vida.");
            return;
        }

        TMP_FontAsset pixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Pixelzone_TMP.asset");
        if (pixelFont != null)
        {
            SerializedObject fontSO = new SerializedObject(pixelFont);
            SerializedProperty clearProp = fontSO.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearProp != null)
            {
                clearProp.boolValue = false;
                fontSO.ApplyModifiedPropertiesWithoutUndo();
            }
            pixelFont.boldStyle = 0f;
            pixelFont.boldSpacing = 0f;
            if (pixelFont.material != null && pixelFont.material.HasProperty(ShaderUtilities.ID_WeightBold))
            {
                pixelFont.material.SetFloat(ShaderUtilities.ID_WeightBold, 0f);
            }
            pixelFont.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~¡¿ÁÉÍÓÚÑÜáéíóúñü", out string _);
            if (pixelFont.atlasTexture != null)
            {
                pixelFont.atlasTexture.filterMode = FilterMode.Point;
            }
            EditorUtility.SetDirty(pixelFont);
        }
        string activeScenePath = SceneManager.GetActiveScene().path;

        SetupHealthBarInScene("Assets/Scenes/01_Nivel1_SantaFelipina.unity", pixelFont);
        SetupHealthBarInScene("Assets/Scenes/02_Nivel2_Guarida.unity", pixelFont);
        SetupHealthBarInScene("Assets/Scenes/Nivel_3.unity", pixelFont);

        // Reconstruir Menú Principal (Escena 00)
        MainMenuSceneBuilder.RebuildMainMenuScene();

        // Configurar Menú de Pausa en Escenas de Juego (Escena 01 y 02)
        MainMenuSceneBuilder.SetupPauseMenuInGameplayScenes();

        if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
        {
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerPersonajeSetup] ¡Barra de vida visual y porcentaje en rojo configurados con éxito!");
    }

    private static void SetupHealthBarInScene(string scenePath, TMP_FontAsset pixelFont)
    {
        if (!File.Exists(scenePath)) return;

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) return;

        BattleManager bm = UnityEngine.Object.FindAnyObjectByType<BattleManager>();
        PlayerStats playerStats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (bm == null) bm = root.GetComponentInChildren<BattleManager>(true);
            if (playerStats == null) playerStats = root.GetComponentInChildren<PlayerStats>(true);
        }

        GameObject canvasGO = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Canvas" || root.name == "MainUI_Canvas" || root.name == "ExplorationDialogueCanvas")
            {
                canvasGO = root;
                break;
            }
        }

        Transform stripT = null;
        if (canvasGO != null)
        {
            stripT = FindChildRecursive(canvasGO.transform, "PlayerStatusStrip");
        }

        // 1. Configurar barra visual en CombatPanel -> PlayerStatusStrip
        GameObject playerStatusStrip = stripT != null ? stripT.gameObject : null;
        if (playerStatusStrip != null)
        {
            // Limpiar hijos existentes de PlayerStatusStrip (como el antiguo PlayerHPText plano)
            for (int i = playerStatusStrip.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(playerStatusStrip.transform.GetChild(i).gameObject);
            }

            RectTransform stripRT = playerStatusStrip.GetComponent<RectTransform>();
            if (stripRT != null)
            {
                // Ajustar altura del strip para albergar texto y barra con separación visual amplia
                stripRT.anchorMin = new Vector2(0.19f, 0.198f);
                stripRT.anchorMax = new Vector2(0.97f, 0.282f);
                stripRT.offsetMin = Vector2.zero;
                stripRT.offsetMax = Vector2.zero;
            }

            Image stripImg = playerStatusStrip.GetComponent<Image>();
            if (stripImg != null)
            {
                stripImg.color = new Color(0.06f, 0.06f, 0.06f, 0.95f);
                stripImg.raycastTarget = false;
            }

            // A) Fila superior: Texto de HP y Porcentaje en Rojo
            GameObject headerGO = CreateUIObject("HeaderRow", playerStatusStrip.transform);
            RectTransform headerRT = headerGO.GetComponent<RectTransform>();
            SetAnchors(headerRT, new Vector2(0.015f, 0.48f), new Vector2(0.985f, 0.96f));

            TextMeshProUGUI playerHPText = CreateTMPText(
                "PlayerHPText",
                headerGO.transform,
                "ENRIQUE HP: 100 / 100",
                22,
                TextAlignmentOptions.MidlineLeft,
                new Color(0.92f, 0.90f, 0.86f, 1f),
                pixelFont
            );
            SetAnchors(playerHPText.rectTransform, new Vector2(0f, 0f), new Vector2(0.70f, 1f));

            TextMeshProUGUI playerHPPctText = CreateTMPText(
                "PlayerHPPctText",
                headerGO.transform,
                "100%",
                24,
                TextAlignmentOptions.MidlineRight,
                new Color(0.22f, 0.85f, 0.38f, 1f), // Verde
                pixelFont
            );
            SetAnchors(playerHPPctText.rectTransform, new Vector2(0.70f, 0f), new Vector2(1f, 1f));

            // B) Fila inferior: Contenedor de la Barra de Vida Visual con margen superior de respiro
            GameObject barBorderGO = CreateUIObject("HealthBarBorder", playerStatusStrip.transform);
            RectTransform barBorderRT = barBorderGO.GetComponent<RectTransform>();
            SetAnchors(barBorderRT, new Vector2(0.015f, 0.08f), new Vector2(0.985f, 0.38f));
            Image barBorderImg = barBorderGO.AddComponent<Image>();
            barBorderImg.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            barBorderImg.raycastTarget = false;

            GameObject barBgGO = CreateUIObject("HealthBarBackground", barBorderGO.transform);
            RectTransform barBgRT = barBgGO.GetComponent<RectTransform>();
            SetAnchors(barBgRT, new Vector2(0.002f, 0.08f), new Vector2(0.998f, 0.92f));
            Image barBgImg = barBgGO.AddComponent<Image>();
            barBgImg.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            barBgImg.raycastTarget = false;

            // Barra de daño tenue
            GameObject damageFillGO = CreateUIObject("DamageFill", barBgGO.transform);
            RectTransform damageFillRT = damageFillGO.GetComponent<RectTransform>();
            SetAnchors(damageFillRT, new Vector2(0f, 0f), new Vector2(1f, 1f));
            Image damageFillImg = damageFillGO.AddComponent<Image>();
            damageFillImg.color = new Color(0.88f, 0.14f, 0.14f, 0.75f);
            damageFillImg.raycastTarget = false;

            // Barra de vida principal en combate: verde
            GameObject mainFillGO = CreateUIObject("MainFill", barBgGO.transform);
            RectTransform mainFillRT = mainFillGO.GetComponent<RectTransform>();
            SetAnchors(mainFillRT, new Vector2(0f, 0f), new Vector2(1f, 1f));
            Image mainFillImg = mainFillGO.AddComponent<Image>();
            mainFillImg.color = new Color(0.22f, 0.85f, 0.38f, 1f); // Verde
            mainFillImg.raycastTarget = false;

            // Configurar el componente PlayerHealthBarUI
            PlayerHealthBarUI healthUI = playerStatusStrip.GetComponent<PlayerHealthBarUI>();
            if (healthUI == null) healthUI = playerStatusStrip.AddComponent<PlayerHealthBarUI>();

            healthUI.playerStats = playerStats;
            healthUI.healthyColor = new Color(0.22f, 0.85f, 0.38f, 1f);
            healthUI.percentageColor = new Color(0.22f, 0.85f, 0.38f, 1f);
            healthUI.barBorderImage = barBorderImg;
            healthUI.barBackgroundImage = barBgImg;
            healthUI.damageFillRect = damageFillRT;
            healthUI.damageFillImage = damageFillImg;
            healthUI.mainFillRect = mainFillRT;
            healthUI.mainFillImage = mainFillImg;
            healthUI.hpText = playerHPText;
            healthUI.percentageText = playerHPPctText;
            healthUI.autoSyncWithPlayerStats = true;
            healthUI.hideWhenCombatActive = false;

            if (playerStats != null)
            {
                healthUI.SetHealthInstant(playerStats.currentHP, playerStats.maxHP);
            }

            if (bm != null)
            {
                bm.playerHPText = playerHPText;
                bm.playerHealthBarUI = healthUI;
                EditorUtility.SetDirty(bm);
            }

            EditorUtility.SetDirty(playerStatusStrip);
        }

        // 2. Configurar barra visual en Overworld Exploration (ExplorationPlayerHUD)
        if (canvasGO != null)
        {
            Transform existingHUD = canvasGO.transform.Find("ExplorationPlayerHUD");
            if (existingHUD != null)
            {
                UnityEngine.Object.DestroyImmediate(existingHUD.gameObject);
            }

            GameObject hudGO = CreateUIObject("ExplorationPlayerHUD", canvasGO.transform);
            RectTransform hudRT = hudGO.GetComponent<RectTransform>();
            SetAnchors(hudRT, new Vector2(0.02f, 0.88f), new Vector2(0.24f, 0.975f));

            Image hudBorder = hudGO.AddComponent<Image>();
            hudBorder.color = new Color(0.35f, 0.35f, 0.35f, 0.90f);
            hudBorder.raycastTarget = false;

            GameObject hudBg = CreateUIObject("Background", hudGO.transform);
            RectTransform hudBgRT = hudBg.GetComponent<RectTransform>();
            SetAnchors(hudBgRT, new Vector2(0.01f, 0.04f), new Vector2(0.99f, 0.96f));
            Image hudBgImg = hudBg.AddComponent<Image>();
            hudBgImg.color = new Color(0.07f, 0.07f, 0.07f, 0.92f);
            hudBgImg.raycastTarget = false;

            TextMeshProUGUI nameText = CreateTMPText(
                "NameText",
                hudBg.transform,
                "ENRIQUE BOSQUES",
                20,
                TextAlignmentOptions.MidlineLeft,
                new Color(0.92f, 0.90f, 0.86f, 1f),
                pixelFont
            );
            SetAnchors(nameText.rectTransform, new Vector2(0.04f, 0.52f), new Vector2(0.65f, 0.94f));

            TextMeshProUGUI pctText = CreateTMPText(
                "PctText",
                hudBg.transform,
                "100%",
                22,
                TextAlignmentOptions.MidlineRight,
                new Color(0.22f, 0.85f, 0.38f, 1f), // Verde
                pixelFont
            );
            SetAnchors(pctText.rectTransform, new Vector2(0.65f, 0.52f), new Vector2(0.96f, 0.94f));

            GameObject barContainer = CreateUIObject("BarContainer", hudBg.transform);
            RectTransform barContainerRT = barContainer.GetComponent<RectTransform>();
            SetAnchors(barContainerRT, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.38f));
            Image barContainerImg = barContainer.AddComponent<Image>();
            barContainerImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            barContainerImg.raycastTarget = false;

            GameObject hudDamageFill = CreateUIObject("DamageFill", barContainer.transform);
            RectTransform hudDamageRT = hudDamageFill.GetComponent<RectTransform>();
            SetAnchors(hudDamageRT, new Vector2(0f, 0f), new Vector2(1f, 1f));
            Image hudDamageImg = hudDamageFill.AddComponent<Image>();
            hudDamageImg.color = new Color(0.88f, 0.14f, 0.14f, 0.75f);
            hudDamageImg.raycastTarget = false;

            GameObject hudMainFill = CreateUIObject("MainFill", barContainer.transform);
            RectTransform hudMainRT = hudMainFill.GetComponent<RectTransform>();
            SetAnchors(hudMainRT, new Vector2(0f, 0f), new Vector2(1f, 1f));
            Image hudMainImg = hudMainFill.AddComponent<Image>();
            hudMainImg.color = new Color(0.22f, 0.85f, 0.38f, 1f); // Verde
            hudMainImg.raycastTarget = false;

            PlayerHealthBarUI hudHealthUI = hudGO.AddComponent<PlayerHealthBarUI>();
            hudHealthUI.playerStats = playerStats;
            hudHealthUI.healthyColor = new Color(0.22f, 0.85f, 0.38f, 1f);
            hudHealthUI.percentageColor = new Color(0.22f, 0.85f, 0.38f, 1f);
            hudHealthUI.damageFillRect = hudDamageRT;
            hudHealthUI.damageFillImage = hudDamageImg;
            hudHealthUI.mainFillRect = hudMainRT;
            hudHealthUI.mainFillImage = hudMainImg;
            hudHealthUI.percentageText = pctText;
            hudHealthUI.autoSyncWithPlayerStats = true;
            hudHealthUI.hideWhenCombatActive = true;

            if (playerStats != null)
            {
                hudHealthUI.SetHealthInstant(playerStats.currentHP, playerStats.maxHP);
            }

            EditorUtility.SetDirty(hudGO);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[PlayerPersonajeSetup] Barra visual de salud configurada en '{scenePath}'.");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color color, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        if (rt == null) return;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;
        if (parent.name == childName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), childName);
            if (result != null) return result;
        }
        return null;
    }
}
#endif