using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Constructor de escena para el Capítulo 3: "La Guarida de El Calacas" (Nivel 3).
/// Limpia cualquier residuo de esbirros o decoraciones desordenadas, y construye un pasillo
/// arquitectónico subterráneo con columnas rítmicas, antorchas, piso y techo bien alineados,
/// y exclusivamente al Jefe Final "El Calacas" al final del túnel.
/// </summary>
public static class Capitulo3SceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Nivel_3.unity";

    [MenuItem("RPG Santa Felipina/Construir Nivel 3 - Guarida de El Calacas")]
    public static void BuildNivel3Scene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // 1. Destruir mapa antiguo si existe para reconstruir limpiamente desde cero
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r != null && (r.name.Contains("Guarida") || r.name.Contains("Punk") || r.name.Contains("Calacas") ||
                              r.name.Contains("Barril") || r.name.Contains("Crate") || r.name.Contains("Props")))
            {
                Object.DestroyImmediate(r);
            }
        }
        foreach (var staleInv in Object.FindObjectsByType<PlayerInventoryAndWeaponsUI>(FindObjectsInactive.Include))
        {
            if (staleInv != null) Object.DestroyImmediate(staleInv.gameObject);
        }
        foreach (var staleStats in Object.FindObjectsByType<PlayerLevelUpAndStatsUI>(FindObjectsInactive.Include))
        {
            if (staleStats != null) Object.DestroyImmediate(staleStats.gameObject);
        }

        // 2. Construir el pasillo arquitectónico limpio con el jefe final al final
        GameObject newRoot = Capitulo3GuaridaNarrative.BuildGuaridaEnvironment();
        if (newRoot != null)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(newRoot, scene);
        }

        // 3. Asegurar o configurar Capitulo3GuaridaNarrative
        Capitulo3GuaridaNarrative narrative = Object.FindAnyObjectByType<Capitulo3GuaridaNarrative>();
        if (narrative == null)
        {
            GameObject nGO = new GameObject("Capitulo3GuaridaNarrative");
            narrative = nGO.AddComponent<Capitulo3GuaridaNarrative>();
        }
        narrative.arrivalDialogueLines = Capitulo3GuaridaNarrative.GetDefaultTutorialDialogueLines();
        EditorUtility.SetDirty(narrative);

        // 4. Asegurar ExplorationDialogueManager, PauseMenuManager y Canvases estandarizados
        ExplorationDialogueManager dialogueMgr = ExplorationDialogueManager.EnsureInScene();
        narrative.explorationDialogueManager = dialogueMgr;

        // Nota: PlayerInventoryAndWeaponsUI y PlayerLevelUpAndStatsUI se generan en runtime vía EnsureInScene()
        // igual que en Nivel 1 y Nivel 2, evitando serializar botones sin listeners persistentes en la escena.
        PauseMenuManager.EnsureInScene();

        // 5. Vincular referencias de UI y banner (título + subtítulo)
        GameObject banner = GameObject.Find("Nivel3Banner");
        if (banner != null)
        {
            narrative.bannerPanel = banner;
            TextMeshProUGUI[] bannerTexts = banner.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (bannerTexts.Length < 2)
            {
                if (bannerTexts.Length == 1 && bannerTexts[0] != null)
                {
                    RectTransform titleRT = bannerTexts[0].rectTransform;
                    titleRT.anchorMin = new Vector2(0f, 0.42f);
                    titleRT.anchorMax = new Vector2(1f, 1f);
                    titleRT.offsetMin = new Vector2(16f, 2f);
                    titleRT.offsetMax = new Vector2(-16f, -8f);
                }
                GameObject subGO = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                subGO.transform.SetParent(banner.transform, false);
                RectTransform subRT = subGO.GetComponent<RectTransform>();
                subRT.anchorMin = new Vector2(0f, 0f);
                subRT.anchorMax = new Vector2(1f, 0.42f);
                subRT.offsetMin = new Vector2(16f, 8f);
                subRT.offsetMax = new Vector2(-16f, -2f);
                TextMeshProUGUI subTMP = subGO.GetComponent<TextMeshProUGUI>();
                subTMP.fontSize = 20f;
                subTMP.alignment = TextAlignmentOptions.Center;
                subTMP.color = new Color(0.78f, 0.80f, 0.86f, 1f);
                bannerTexts = banner.GetComponentsInChildren<TextMeshProUGUI>(true);
            }
            if (bannerTexts.Length > 0)
            {
                narrative.chapterTitleText = bannerTexts[0];
                bannerTexts[0].text = "CAPÍTULO 3: LA GUARIDA DE EL CALACAS";
                PlayerHealthBarUI.EnsureCleanPixelFont(bannerTexts[0]);
            }
            if (bannerTexts.Length > 1)
            {
                narrative.chapterSubtitleText = bannerTexts[1];
                bannerTexts[1].text = "El Búnker Subterráneo — El Enfrentamiento Final";
                PlayerHealthBarUI.EnsureCleanPixelFont(bannerTexts[1]);
            }
        }

        GameObject headerCanvas = GameObject.Find("Nivel3HeaderCanvas");
        if (headerCanvas != null)
        {
            CanvasGroup cg = headerCanvas.GetComponent<CanvasGroup>();
            if (cg == null) cg = headerCanvas.AddComponent<CanvasGroup>();
            narrative.fadeCanvasGroup = cg;
        }

        // 6. Configurar jugador y detective al inicio del pasillo espacioso
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        if (player != null)
        {
            player.transform.position = new Vector3(-2.0f, -0.75f, 0f);
            player.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            dialogueMgr.playerController = player;

            BoxCollider2D pCol = player.GetComponent<BoxCollider2D>();
            if (pCol != null)
            {
                pCol.offset = new Vector2(0f, -0.35f);
                pCol.size = new Vector2(0.50f, 0.40f);
                pCol.isTrigger = false;
            }
        }

        CompanionFollowAI companion = Object.FindAnyObjectByType<CompanionFollowAI>();
        if (companion != null)
        {
            companion.transform.position = new Vector3(-3.2f, -0.75f, 0f);
            companion.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            companion.autoTriggerJoinDialogueOnApproach = false;
            companion.recruitmentDialogueLines = System.Array.Empty<string>();
            companion.followUpDialogueLines = System.Array.Empty<string>();
            narrative.investigatorCompanion = companion;
            Capitulo2IntroNarrative.EnsureGenericInvestigatorAppearance(companion);
            Transform badge = companion.transform.Find("Badge_Investigador");
            if (badge != null) Object.DestroyImmediate(badge.gameObject);

            BoxCollider2D cCol = companion.GetComponent<BoxCollider2D>();
            if (cCol != null)
            {
                cCol.offset = new Vector2(0f, -0.35f);
                cCol.size = new Vector2(0.50f, 0.40f);
                cCol.isTrigger = false;
            }
        }

        // 7. Reconstruir CombatCanvas/Canvas, CombatPanel, HUD y GameOverPanel 100% IDÉNTICOS al Nivel 1 y Nivel 2
        TMP_FontAsset pixelFont = PlayerHealthBarUI.GetCleanPixelFont();
        PlayerStats pStats = (player != null) ? player.GetComponent<PlayerStats>() : Object.FindAnyObjectByType<PlayerStats>();
        DetectiveStats detStats = DetectiveStats.EnsureInScene();
        if (detStats != null)
        {
            detStats.hasDetectiveInParty = true;
        }

        BattleManager bm = Capitulo2SceneBuilder.BuildCompleteLevel2CombatUI(pStats, player, detStats, pixelFont);
        if (bm != null)
        {
            bm.LoadSfxClipsIfMissing();
            bm.LoadPortraitsIfMissing();
            bm.EnforceUnifiedCombatUILayout();

            if (bm.gameOverPanel != null)
            {
                Transform subTr = bm.gameOverPanel.transform.Find("GameOverBorderFrame/GameOverInnerBox/GameOverSubtitleText");
                if (subTr != null && subTr.TryGetComponent<TextMeshProUGUI>(out var subTmp))
                {
                    subTmp.text = "FIN DEL RECORRIDO EN LA GUARIDA DE EL CALACAS";
                }
                Transform narTr = bm.gameOverPanel.transform.Find("GameOverBorderFrame/GameOverInnerBox/GameOverNarrativeText");
                if (narTr != null && narTr.TryGetComponent<TextMeshProUGUI>(out var narTmp))
                {
                    narTmp.text = "Enrique y el Detective Vargas cayeron en el búnker subterráneo.\nEl imperio clandestino de El Calacas se cerró para siempre bajo tierra.";
                }
            }

            EnemyEncounterTrigger[] triggers = Object.FindObjectsByType<EnemyEncounterTrigger>(FindObjectsInactive.Include);
            foreach (EnemyEncounterTrigger tr in triggers)
            {
                if (tr != null)
                {
                    tr.battleManager = bm;
                    EditorUtility.SetDirty(tr);
                }
            }
            EditorUtility.SetDirty(bm);
        }

        // Limpiar tarjeta flotante antigua del Detective y botón flotante de inventario
        GameObject detAllySlot = GameObject.Find("Detective_AllyCombatSlot");
        if (detAllySlot != null)
        {
            Object.DestroyImmediate(detAllySlot);
        }
        GameObject combatInvBtn = GameObject.Find("Combat_InventoryAndWeaponsButton");
        if (combatInvBtn != null)
        {
            combatInvBtn.SetActive(false);
        }

        PlayerController2D.HideAllWorldSpaceObjectLabels();

        // 8. Configurar cámara de seguimiento centrada en el pasillo amplio
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);
            mainCam.orthographicSize = 3.2f;

            CameraFollow2D camFollow = mainCam.GetComponent<CameraFollow2D>();
            if (camFollow == null) camFollow = mainCam.gameObject.AddComponent<CameraFollow2D>();

            camFollow.useBounds = true;
            camFollow.minBounds = new Vector2(0.0f, -0.75f);
            camFollow.maxBounds = new Vector2(20.0f, -0.75f);
            camFollow.smoothTime = 0.15f;
            if (player != null)
            {
                camFollow.target = player.transform;
                mainCam.transform.position = new Vector3(
                    Mathf.Clamp(player.transform.position.x, camFollow.minBounds.x, camFollow.maxBounds.x),
                    -0.75f,
                    -10f
                );
            }
        }

        // 9. Asegurar Menú de Pausa estandarizado en la jerarquía guardada
        MainMenuSceneBuilder.SetupPauseMenuInCurrentScene();

        GameObject detSlotCleanup = GameObject.Find("Detective_AllyCombatSlot");
        if (detSlotCleanup != null)
        {
            Object.DestroyImmediate(detSlotCleanup);
        }

        // 10. Guardar la escena en disco de forma explícita
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        if (!saved) saved = EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log($"[RPG Santa Felipina] ¡Nivel 3: Pasillo y Jefe Final El Calacas construidos y guardados con éxito! (Guardado={saved})");
    }
}
