#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Utilidad para organizar, renombrar y conectar la secuencia completa de escenas:
/// 0: Assets/Scenes/00_MenuIntro.unity
/// 1: Assets/Scenes/01_Nivel1_SantaFelipina.unity
/// 2: Assets/Scenes/02_Nivel2_Guarida.unity
/// </summary>
public static class SceneOrganizer
{
    public const string Scene0_Menu_Old = "Assets/Scenes/MenuScene.unity";
    public const string Scene0_Menu_New = "Assets/Scenes/00_MenuIntro.unity";

    public const string Scene1_Map_Old = "Assets/Scenes/SampleScene.unity";
    public const string Scene1_Map_New = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";

    public const string Scene2_Guarida_Old = "Assets/Scenes/Capitulo2_Guarida.unity";
    public const string Scene2_Guarida_New = "Assets/Scenes/02_Nivel2_Guarida.unity";

    [MenuItem("RPG Santa Felipina/Organizar y Conectar Escenas (00, 01, 02)")]
    public static void OrganizeAndConnectAllScenes()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("[SceneOrganizer] Saliendo de Play Mode antes de organizar.");
            return;
        }

        // 1. Abrir escena temporal vacía para liberar cualquier archivo bloqueado
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Renombrar las escenas si aún conservan sus nombres antiguos
        RenameSceneAsset(Scene0_Menu_Old, "00_MenuIntro");
        RenameSceneAsset(Scene1_Map_Old, "01_Nivel1_SantaFelipina");
        RenameSceneAsset(Scene2_Guarida_Old, "02_Nivel2_Guarida");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Configurar EditorBuildSettings.scenes con el orden canónico
        EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(Scene0_Menu_New, true),
            new EditorBuildSettingsScene(Scene1_Map_New, true),
            new EditorBuildSettingsScene(Scene2_Guarida_New, true)
        };
        Debug.Log("[SceneOrganizer] Build Settings configurado: \n  [0] 00_MenuIntro\n  [1] 01_Nivel1_SantaFelipina\n  [2] 02_Nivel2_Guarida");

        // 4. Configurar Escena 0: 00_MenuIntro
        if (File.Exists(Scene0_Menu_New))
        {
            var menuScene = EditorSceneManager.OpenScene(Scene0_Menu_New, OpenSceneMode.Single);
            IntroSlideshowManager introManager = Object.FindAnyObjectByType<IntroSlideshowManager>();
            if (introManager != null)
            {
                introManager.loadSceneOnFinish = true;
                introManager.nextSceneName = "01_Nivel1_SantaFelipina";
                EditorUtility.SetDirty(introManager);
                Debug.Log("[SceneOrganizer] 'IntroSlideshowManager' configurado para cargar '01_Nivel1_SantaFelipina'.");
            }
            else
            {
                Debug.LogWarning("[SceneOrganizer] No se encontró 'IntroSlideshowManager' en 00_MenuIntro.");
            }

            // Aplicar textos neutros en StartScreenPanel
            ApplyNeutralTitlesInStartScreen();

            EditorSceneManager.MarkSceneDirty(menuScene);
            EditorSceneManager.SaveScene(menuScene);
        }
        else
        {
            Debug.LogError($"[SceneOrganizer] No se encontró el archivo {Scene0_Menu_New}");
        }

        // 5. Configurar Escena 1: 01_Nivel1_SantaFelipina
        if (File.Exists(Scene1_Map_New))
        {
            var mapScene = EditorSceneManager.OpenScene(Scene1_Map_New, OpenSceneMode.Single);

            // BattleManager -> volver al menú principal
            BattleManager bm = Object.FindAnyObjectByType<BattleManager>();
            if (bm != null)
            {
                bm.menuSceneName = "00_MenuIntro";
                if (bm.bossVictoryTransitionUI == null)
                {
                    bm.bossVictoryTransitionUI = BossVictoryTransitionUI.EnsureInScene();
                }
                EditorUtility.SetDirty(bm);
                Debug.Log("[SceneOrganizer] 'BattleManager' configurado con menuSceneName = '00_MenuIntro'.");
            }

            // Boss Lacayo_Segundo -> transición al capítulo 2
            GameObject bossGO = GameObject.Find("Lacayo_Segundo");
            if (bossGO != null)
            {
                EnemyEncounterTrigger bossTrigger = bossGO.GetComponent<EnemyEncounterTrigger>();
                if (bossTrigger != null)
                {
                    bossTrigger.isBossEncounter = true;
                    bossTrigger.nextSceneName = "02_Nivel2_Guarida";
                    EditorUtility.SetDirty(bossTrigger);
                    Debug.Log("[SceneOrganizer] Boss 'Lacayo_Segundo' configurado para cargar '02_Nivel2_Guarida'.");
                }
            }
            else
            {
                Debug.LogWarning("[SceneOrganizer] No se encontró 'Lacayo_Segundo' en 01_Nivel1_SantaFelipina.");
            }

            EditorSceneManager.MarkSceneDirty(mapScene);
            EditorSceneManager.SaveScene(mapScene);
        }
        else
        {
            Debug.LogError($"[SceneOrganizer] No se encontró el archivo {Scene1_Map_New}");
        }

        // 6. Configurar Escena 2: 02_Nivel2_Guarida
        if (File.Exists(Scene2_Guarida_New))
        {
            var guaridaScene = EditorSceneManager.OpenScene(Scene2_Guarida_New, OpenSceneMode.Single);
            // Asegurar que guarde sin errores
            EditorSceneManager.MarkSceneDirty(guaridaScene);
            EditorSceneManager.SaveScene(guaridaScene);
            Debug.Log("[SceneOrganizer] Escena '02_Nivel2_Guarida' validada y guardada.");
        }
        else
        {
            Debug.LogError($"[SceneOrganizer] No se encontró el archivo {Scene2_Guarida_New}");
        }

        // 7. Abrir 00_MenuIntro como escena inicial activa
        if (File.Exists(Scene0_Menu_New))
        {
            EditorSceneManager.OpenScene(Scene0_Menu_New, OpenSceneMode.Single);
            Debug.Log("[SceneOrganizer] Escena inicial activa establecida en: 00_MenuIntro.unity");
        }

        Debug.Log("<color=green><b>[SceneOrganizer] ¡Organización de escenas completada exitosamente!</b></color>");
    }

    [MenuItem("RPG Santa Felipina/Actualizar Textos de Portada a Neutro")]
    public static void ApplyNeutralTitlesInStartScreen()
    {
        GameObject startPanel = GameObject.Find("StartScreenPanel");
        if (startPanel == null)
        {
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allGOs)
            {
                if (go.name == "StartScreenPanel" && go.scene.isLoaded)
                {
                    startPanel = go;
                    break;
                }
            }
        }

        if (startPanel != null)
        {
            Transform titleT = startPanel.transform.Find("TitleText");
            if (titleT != null)
            {
                var tmp = titleT.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = "PROYECTO: VOCES INTERNAS";
                    tmp.ForceMeshUpdate();
                    EditorUtility.SetDirty(tmp);
                    Debug.Log("[SceneOrganizer] TitleText establecido a 'PROYECTO: VOCES INTERNAS'.");
                }
            }

            Transform chapterT = startPanel.transform.Find("ChapterText");
            if (chapterT != null)
            {
                var tmp = chapterT.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = "CRONICAS URBANAS";
                    tmp.ForceMeshUpdate();
                    EditorUtility.SetDirty(tmp);
                    Debug.Log("[SceneOrganizer] ChapterText establecido a 'CRONICAS URBANAS'.");
                }
            }

            Transform subtitleT = startPanel.transform.Find("SubtitleText");
            if (subtitleT != null)
            {
                var tmp = subtitleT.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = "Enrique Bosques y sus 4 Voces Interiores";
                    tmp.ForceMeshUpdate();
                    EditorUtility.SetDirty(tmp);
                    Debug.Log("[SceneOrganizer] SubtitleText establecido a 'Enrique Bosques y sus 4 Voces Interiores'.");
                }
            }

            EditorUtility.SetDirty(startPanel);
        }
    }

    private static void RenameSceneAsset(string oldPath, string newNameWithoutExtension)
    {
        if (File.Exists(oldPath))
        {
            string error = AssetDatabase.RenameAsset(oldPath, newNameWithoutExtension);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[SceneOrganizer] Error al renombrar {oldPath} a {newNameWithoutExtension}: {error}");
            }
            else
            {
                Debug.Log($"[SceneOrganizer] Renombrado exitoso: {oldPath} -> {newNameWithoutExtension}.unity");
            }
        }
    }
}
#endif
