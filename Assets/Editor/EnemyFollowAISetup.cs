#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Configura el componente EnemyFollowAI y Rigidbody2D en todos los enemigos del mapa de Santa Felipina,
/// permitiéndoles patrullar de forma autónoma y perseguir a Enrique hasta una distancia cercana.
/// </summary>
public static class EnemyFollowAISetup
{
    [MenuItem("RPG Santa Felipina/Configurar Movimiento y Persecucion de Enemigos")]
    public static void SetupAllMapEnemiesWithFollowAI()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("[EnemyFollowAISetup] Saliendo de Play Mode antes de configurar.");
            return;
        }

        string[] enemyNames = new string[]
        {
            "Lacayo_Tutorial",
            "Lacayo_Debil_1",
            "Lacayo_Debil_2",
            "Lacayo_Debil_3",
            "Lacayo_Debil_4",
            "Lacayo_Debil_5",
            "Lacayo_Segundo"
        };

        int count = 0;
        foreach (string name in enemyNames)
        {
            GameObject enemyGO = GameObject.Find(name);
            if (enemyGO == null)
            {
                Debug.LogWarning($"[EnemyFollowAISetup] No se encontró '{name}' en la escena activa.");
                continue;
            }

            // 1. Asegurar Rigidbody2D físico para no atravesar paredes
            Rigidbody2D rb = enemyGO.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = enemyGO.AddComponent<Rigidbody2D>();
            }
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            EditorUtility.SetDirty(rb);

            // 2. Asegurar BoxCollider2D trigger para el inicio de combate
            BoxCollider2D col = enemyGO.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.size = new Vector2(1.4f, 1.5f);
                EditorUtility.SetDirty(col);
            }

            SpriteRenderer sr = enemyGO.GetComponent<SpriteRenderer>();
            Animator anim = enemyGO.GetComponent<Animator>();

            // 3. Añadir o configurar EnemyFollowAI
            EnemyFollowAI ai = enemyGO.GetComponent<EnemyFollowAI>();
            if (ai == null)
            {
                ai = enemyGO.AddComponent<EnemyFollowAI>();
            }

            ai.animator = anim;
            ai.spriteRenderer = sr;

            // Parámetros diferenciados según el tipo de enemigo
            bool isBoss = (name == "Lacayo_Segundo");
            ai.detectionRadius = isBoss ? 6.8f : 5.8f;
            ai.stoppingDistance = isBoss ? 1.35f : 1.20f; // Distancia muy cercana
            ai.leashRadius = isBoss ? 15.0f : 12.0f;
            ai.wanderRadius = isBoss ? 1.5f : 2.0f;
            ai.walkSpeed = 2.0f;
            ai.chaseSpeed = isBoss ? 3.3f : 3.5f;
            ai.enableWander = true;

            EditorUtility.SetDirty(ai);
            EditorUtility.SetDirty(enemyGO);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[EnemyFollowAISetup] ¡{count} enemigos del mapa configurados exitosamente con EnemyFollowAI y Rigidbody2D!");

        SetupCameraFollow();
    }

    [MenuItem("RPG Santa Felipina/Configurar Camara de Seguimiento 2D")]
    public static void SetupCameraFollow()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        GameObject camGO = GameObject.Find("Main Camera");
        if (camGO == null && Camera.main != null) camGO = Camera.main.gameObject;

        if (camGO != null)
        {
            CameraFollow2D follow = camGO.GetComponent<CameraFollow2D>();
            if (follow == null) follow = camGO.AddComponent<CameraFollow2D>();

            GameObject playerGO = GameObject.Find("Enrique_Player");
            if (playerGO != null) follow.target = playerGO.transform;

            follow.smoothTime = 0.18f;
            follow.offset = new Vector3(0f, 0.6f, -10f);
            follow.useBounds = false;

            EditorUtility.SetDirty(follow);
            EditorUtility.SetDirty(camGO);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[EnemyFollowAISetup] ¡Cámara de seguimiento 2D (CameraFollow2D) configurada exitosamente en Main Camera!");
        }
        else
        {
            Debug.LogWarning("[EnemyFollowAISetup] No se encontró 'Main Camera'.");
        }
    }
}
#endif
