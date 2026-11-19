using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CombatMenuScreenshotCapture
{
    private const string ScenePath = "Assets/Scenes/02_Nivel2_Guarida.unity";

    [MenuItem("RPG Santa Felipina/Capturar Fotografías de Menús de Combate")]
    public static void CaptureCombatTurnsScreenshots()
    {
        string outDir = @"C:\Users\leyot\.gemini\antigravity\brain\05263756-0373-4fa3-89ea-f3f363b4336e";
        if (!Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        BattleManager bm = Object.FindAnyObjectByType<BattleManager>();
        if (bm == null)
        {
            Debug.LogError("[ScreenshotCapture] No se encontró BattleManager en la escena.");
            return;
        }

        Canvas canvas = bm.battlePanel.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ScreenshotCapture] No se encontró Canvas para el BattlePanel.");
            return;
        }

        // Configurar cámara de captura
        GameObject camGO = new GameObject("CaptureCamera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        cam.transform.position = new Vector3(0, 0, -10f);

        RenderMode originalRenderMode = canvas.renderMode;
        Camera originalCam = canvas.worldCamera;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 10f;

        // Activar panel de combate y ocultar HUD de exploracion
        bm.battlePanel.SetActive(true);
        if (bm.actionGifPanel != null) bm.actionGifPanel.SetActive(false);
        if (bm.combatDialoguePanel != null) bm.combatDialoguePanel.SetActive(false);
        Transform explorationHUD = canvas.transform.Find("ExplorationPlayerHUD");
        if (explorationHUD != null) explorationHUD.gameObject.SetActive(false);

        // Cargar enemigos de prueba
        EnemyData enemy1 = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Sicario_Cuchillero_N2.asset");
        EnemyData enemyBoss = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Jefe_CapatazBlindado_N2.asset");

        bm.activeEnemies.Clear();
        if (enemy1 != null)
        {
            bm.activeEnemies.Add(new CombatEnemyInstance
            {
                data = enemy1,
                currentHP = 38,
                isConfused = false,
                attackDebuff = 0
            });
        }
        if (enemyBoss != null)
        {
            bm.activeEnemies.Add(new CombatEnemyInstance
            {
                data = enemyBoss,
                currentHP = 160,
                isConfused = false,
                attackDebuff = 0
            });
        }

        bm.selectedTargetIndex = 0;
        bm.SetupEnemyVisuals();

        // 1. TURNO DE ENRIQUE
        bm.playerStats = Object.FindAnyObjectByType<PlayerStats>();
        if (bm.playerStats != null)
        {
            bm.playerStats.currentHP = 84;
            bm.playerStats.maxHP = 100;
        }
        bm.detectiveStats = DetectiveStats.EnsureInScene();
        if (bm.detectiveStats != null)
        {
            bm.detectiveStats.HP_Actual = 90;
            bm.detectiveStats.HP_Max = 95;
            bm.detectiveStats.municionActual = 5;
            bm.detectiveStats.municionMaxima = 6;
            bm.detectiveStats.hasDetectiveInParty = true;
        }

        bm.state = BattleState.PLAYERTURN;
        bm.SetActionButtonsInteractable(true);
        bm.UpdateHealthUI();
        bm.SetBattleLog("TU TURNO — ENRIQUE (Objetivo: SICARIO CUCHILLERO) | Cola: Enrique(12) > Detective(14) > Sicario(10) > Capataz(8)");

        Canvas.ForceUpdateCanvases();
        RenderTexture rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D screenTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        screenTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        screenTex.Apply();

        string path1 = Path.Combine(outDir, "Turno1_Enrique.png");
        File.WriteAllBytes(path1, screenTex.EncodeToPNG());
        Debug.Log("[ScreenshotCapture] Guardada fotografía 1: " + path1);

        // 2. TURNO DEL DETECTIVE VARGAS
        Sprite detPort = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Portraits/Detective.png");
        if (detPort != null) bm.detectiveCombatPortrait = detPort;
        bm.state = BattleState.DETECTIVETURN;
        bm.SetActionButtonsInteractable(true);
        bm.UpdateHealthUI();
        bm.SetBattleLog("TU TURNO — DETECTIVE VARGAS (Objetivo: SICARIO CUCHILLERO) | Elige una acción táctica (Disparo .38, Punto Débil, Esquivar, Recargar).");

        Canvas.ForceUpdateCanvases();
        cam.Render();

        screenTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        screenTex.Apply();

        string path2 = Path.Combine(outDir, "Turno2_Detective.png");
        File.WriteAllBytes(path2, screenTex.EncodeToPNG());
        Debug.Log("[ScreenshotCapture] Guardada fotografía 2: " + path2);

        // 3. TURNO ENEMIGO / DETECTIVE CAÍDO (Demostración de que la vida se apaga pero Enrique continúa)
        bm.detectiveStats.HP_Actual = 0;
        bm.state = BattleState.PLAYERTURN;
        bm.SetActionButtonsInteractable(true);
        bm.UpdateHealthUI();
        bm.SetBattleLog("¡EL DETECTIVE VARGAS HA CAÍDO INCONSCIENTE! Solo se apaga su vida. Enrique continúa el combate en solitario hasta vencer o caer.");

        Canvas.ForceUpdateCanvases();
        cam.Render();

        screenTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        screenTex.Apply();

        string path3 = Path.Combine(outDir, "Turno3_DetectiveCaido.png");
        File.WriteAllBytes(path3, screenTex.EncodeToPNG());
        Debug.Log("[ScreenshotCapture] Guardada fotografía 3: " + path3);

        // 4. SUB-MENÚ DE INTELECTO (Curación de Aliados: Enrique vs Detective Vargas)
        InventoryManager inv = InventoryManager.Instance ?? Object.FindAnyObjectByType<InventoryManager>();
        if (inv == null)
        {
            GameObject invGO = new GameObject("InventoryManager");
            inv = invGO.AddComponent<InventoryManager>();
        }
        ItemData torta = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Item_Torta.asset");
        CraftingRecipeData recetaVendaje = AssetDatabase.LoadAssetAtPath<CraftingRecipeData>("Assets/Data/Items/Receta_VendajeRudo.asset");
        if (inv != null)
        {
            inv.items.Clear();
            if (torta != null) inv.AddItem(torta, 2);
            if (recetaVendaje != null && inv.knownRecipes != null && !inv.knownRecipes.Contains(recetaVendaje))
            {
                inv.knownRecipes.Add(recetaVendaje);
            }
        }
        bm.state = BattleState.PLAYERTURN;
        bm.detectiveStats.HP_Actual = 45;
        bm.detectiveStats.HP_Max = 95;
        bm.detectiveStats.hasDetectiveInParty = true;
        bm.UpdateHealthUI();

        GameObject intelectoPanelGO = (bm.intelectoAndItemsPanel != null) ? bm.intelectoAndItemsPanel : bm.intelectoCraftingPanel;
        if (intelectoPanelGO != null)
        {
            intelectoPanelGO.SetActive(true);
            CombatIntelectoUI intelectoUI = intelectoPanelGO.GetComponent<CombatIntelectoUI>();
            if (intelectoUI != null)
            {
                intelectoUI.ShowAndRefresh(bm);
            }
        }

        Canvas.ForceUpdateCanvases();
        cam.Render();

        screenTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        screenTex.Apply();

        string path4 = Path.Combine(outDir, "Menu_Intelecto_CuracionAliados.png");
        File.WriteAllBytes(path4, screenTex.EncodeToPNG());
        Debug.Log("[ScreenshotCapture] Guardada fotografía 4: " + path4);

        if (intelectoPanelGO != null) intelectoPanelGO.SetActive(false);

        // 5. PANTALLA DE RESULTADOS Y VICTORIA DE GRUPO
        bm.battlePanel.SetActive(false);
        PlayerLevelUpAndStatsUI statsUI = PlayerLevelUpAndStatsUI.EnsureInScene();
        if (statsUI != null)
        {
            Canvas statsCanvas = statsUI.GetComponentInParent<Canvas>();
            RenderMode origStatsMode = statsCanvas.renderMode;
            Camera origStatsCam = statsCanvas.worldCamera;
            statsCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            statsCanvas.worldCamera = cam;
            statsCanvas.planeDistance = 10f;

            bm.detectiveStats.HP_Actual = 90;
            bm.detectiveStats.municionActual = 5;
            bm.playerStats.currentHP = 84;
            bm.playerStats.availableSkillPoints = 1;

            statsUI.OpenForCombatLevelUp(bm.playerStats, 1, 60, null);

            Canvas.ForceUpdateCanvases();
            cam.Render();

            screenTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            screenTex.Apply();

            string path5 = Path.Combine(outDir, "Resultados_Victoria_Grupo.png");
            File.WriteAllBytes(path5, screenTex.EncodeToPNG());
            Debug.Log("[ScreenshotCapture] Guardada fotografía 5: " + path5);

            statsCanvas.renderMode = origStatsMode;
            statsCanvas.worldCamera = origStatsCam;
            if (statsUI.modalPanelRoot != null) statsUI.modalPanelRoot.SetActive(false);
        }

        // 6. MENÚ SUBSECUENTE DE SUBIDA DE NIVEL Y ESTADÍSTICAS DEL DETECTIVE
        DetectiveLevelUpAndStatsUI detUI = DetectiveLevelUpAndStatsUI.EnsureInScene();
        if (detUI != null)
        {
            if (detUI.modalPanelRoot != null)
            {
                Object.DestroyImmediate(detUI.modalPanelRoot);
                detUI.modalPanelRoot = null;
            }
            detUI.InitializeIfNeeded();

            Canvas detCanvas = detUI.GetComponentInParent<Canvas>();
            RenderMode origDetMode = detCanvas.renderMode;
            Camera origDetCam = detCanvas.worldCamera;
            detCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            detCanvas.worldCamera = cam;
            detCanvas.planeDistance = 10f;

            bm.detectiveStats.Nivel = 2;
            bm.detectiveStats.HP_Max = 100;
            bm.detectiveStats.HP_Actual = 100;
            bm.detectiveStats.EXP_Actual = 25;
            bm.detectiveStats.EXP_Requerida = 155;
            bm.detectiveStats.Ataque_Arma = 20;
            bm.detectiveStats.Ataque_CuerpoACuerpo = 14;
            bm.detectiveStats.Defensa = 7;
            bm.detectiveStats.Velocidad = 15;
            bm.detectiveStats.Probabilidad_Esquiva = 38f;
            bm.detectiveStats.municionActual = 6;
            bm.detectiveStats.municionMaxima = 6;

            List<string> mockLevelUpLogs = new List<string>
            {
                "+5 HP_Max (Total 100)",
                "+2 Ataque_Arma (Total 20)",
                "+1 Defensa (Total 7)",
                "+1 Velocidad (Total 15)"
            };

            detUI.OpenForCombatReport(bm.detectiveStats, 1, 60, mockLevelUpLogs, null);

            Canvas.ForceUpdateCanvases();
            cam.Render();

            screenTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            screenTex.Apply();

            string path6 = Path.Combine(outDir, "Menu_Subsecuente_Detective_Nivel.png");
            File.WriteAllBytes(path6, screenTex.EncodeToPNG());
            Debug.Log("[ScreenshotCapture] Guardada fotografía 6: " + path6);

            string path6Actualizado = Path.Combine(outDir, "Menu_Subsecuente_Detective_Nivel_Actualizado.png");
            File.WriteAllBytes(path6Actualizado, screenTex.EncodeToPNG());
            Debug.Log("[ScreenshotCapture] Guardada fotografía 6 (actualizada): " + path6Actualizado);

            // Generar también detalle de zoom en las estadísticas para evidenciar la tipografía pixel
            Texture2D detailTex = new Texture2D(700, 500, TextureFormat.RGB24, false);
            // Coordenadas en Texture2D (origen inferior izquierdo): x=960, y=320, width=700, height=500
            Color[] detailPixels = screenTex.GetPixels(960, 320, 700, 500);
            detailTex.SetPixels(detailPixels);
            detailTex.Apply();
            string pathDetail = Path.Combine(outDir, "Detalle_Estadisticas_Pixel_Detective.png");
            File.WriteAllBytes(pathDetail, detailTex.EncodeToPNG());
            Object.DestroyImmediate(detailTex);
            Debug.Log("[ScreenshotCapture] Guardada imagen de detalle tipográfico: " + pathDetail);

            detCanvas.renderMode = origDetMode;
            detCanvas.worldCamera = origDetCam;
            if (detUI.modalPanelRoot != null) detUI.modalPanelRoot.SetActive(false);
        }

        // Restaurar
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(screenTex);
        Object.DestroyImmediate(camGO);

        canvas.renderMode = originalRenderMode;
        canvas.worldCamera = originalCam;
        bm.battlePanel.SetActive(false);

        Debug.Log("[ScreenshotCapture] ¡Todas las fotografías fueron generadas con éxito en: " + outDir);
    }
}
