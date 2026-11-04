#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AttackAnimationAndSpritesSetup
{
    private const string ATTACK_TEX_PATH = "Assets/Scripts/AnimacionAtaque.png";
    private const string EXPLOSION_TEX_PATH = "Assets/Scripts/AtaqueExplosion.png";
    private const string SCENE_PATH = "Assets/Scenes/01_Nivel1_SantaFelipina.unity";

    [MenuItem("Tools/Setup Attack Animations and Sprites")]
    public static void ExecuteSetup()
    {
        Debug.Log("[Setup] Iniciando configuración de animaciones de ataque y sprites de objetos...");

        // 1. Slicing de spritesheets
        SliceAttackSpritesheet();
        SliceExplosionSpritesheet();

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 2. Obtener sprites naranja
        Sprite[] orangeAttackSprites = LoadSpritesByPrefix(ATTACK_TEX_PATH, "AnimacionAtaque_Orange_");
        Sprite[] orangeExplosionSprites = LoadSpritesByPrefix(EXPLOSION_TEX_PATH, "AtaqueExplosion_Orange_");

        Debug.Log($"[Setup] Sprites de ataque naranja cargados: {orangeAttackSprites?.Length ?? 0}");
        Debug.Log($"[Setup] Sprites de explosión naranja cargados: {orangeExplosionSprites?.Length ?? 0}");

        // 3. Crear AnimationClips
        CreateAnimationClips(orangeAttackSprites, orangeExplosionSprites);

        // 4. Actualizar ScriptableObjects de Items
        UpdateItemScriptableObjects();

        // 5. Configurar objetos y BattleManager en la escena
        UpdateSceneObjectsAndBattleManager(orangeAttackSprites, orangeExplosionSprites);

        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] ¡Configuración completada exitosamente!");
    }

    private static void SliceAttackSpritesheet()
    {
        TextureImporter ti = AssetImporter.GetAtPath(ATTACK_TEX_PATH) as TextureImporter;
        if (ti == null)
        {
            Debug.LogError($"[Setup] No se encontró el TextureImporter en {ATTACK_TEX_PATH}");
            return;
        }

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.isReadable = true;
        ti.spritePixelsPerUnit = 64;

        // AnimacionAtaque.png es 512x576 (8 columnas x 9 filas de 64x64)
        // Row 0 (Y=512) es la variante NARANJA
        string[] rowColorNames = new string[]
        {
            "Orange", "Purple", "Blue", "Green", "Brown", "Gray", "Pink", "Red", "Shadow"
        };

        List<SpriteMetaData> metas = new List<SpriteMetaData>();
        int cols = 8;
        int rows = 9;
        int cellSize = 64;

        for (int r = 0; r < rows; r++)
        {
            // En Unity Y=0 está abajo. Row 0 (arriba) tiene Y = 576 - 64 = 512.
            int unityY = (rows - 1 - r) * cellSize;
            string colorName = (r < rowColorNames.Length) ? rowColorNames[r] : $"Row{r}";

            for (int c = 0; c < cols; c++)
            {
                int unityX = c * cellSize;
                SpriteMetaData meta = new SpriteMetaData
                {
                    name = (r == 0) ? $"AnimacionAtaque_Orange_{c}" : $"AnimacionAtaque_{colorName}_{c}",
                    rect = new Rect(unityX, unityY, cellSize, cellSize),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center
                };
                metas.Add(meta);
            }
        }

#pragma warning disable CS0618
        ti.spritesheet = metas.ToArray();
#pragma warning restore CS0618

        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
        Debug.Log($"[Setup] AnimacionAtaque rebanada exitosamente en {metas.Count} sprites (Row 0 = Orange).");
    }

    private static void SliceExplosionSpritesheet()
    {
        TextureImporter ti = AssetImporter.GetAtPath(EXPLOSION_TEX_PATH) as TextureImporter;
        if (ti == null)
        {
            Debug.LogError($"[Setup] No se encontró el TextureImporter en {EXPLOSION_TEX_PATH}");
            return;
        }

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.isReadable = true;
        ti.spritePixelsPerUnit = 64;

        // AtaqueExplosion.png es 640x576 (10 columnas x 9 filas de 64x64)
        // Row 0 (Y=512) es la variante NARANJA
        string[] rowColorNames = new string[]
        {
            "Orange", "Purple", "Blue", "Green", "Brown", "Gray", "Pink", "Red", "Shadow"
        };

        List<SpriteMetaData> metas = new List<SpriteMetaData>();
        int cols = 10;
        int rows = 9;
        int cellSize = 64;

        for (int r = 0; r < rows; r++)
        {
            int unityY = (rows - 1 - r) * cellSize;
            string colorName = (r < rowColorNames.Length) ? rowColorNames[r] : $"Row{r}";

            for (int c = 0; c < cols; c++)
            {
                int unityX = c * cellSize;
                SpriteMetaData meta = new SpriteMetaData
                {
                    name = (r == 0) ? $"AtaqueExplosion_Orange_{c}" : $"AtaqueExplosion_{colorName}_{c}",
                    rect = new Rect(unityX, unityY, cellSize, cellSize),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center
                };
                metas.Add(meta);
            }
        }

#pragma warning disable CS0618
        ti.spritesheet = metas.ToArray();
#pragma warning restore CS0618

        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
        Debug.Log($"[Setup] AtaqueExplosion rebanada exitosamente en {metas.Count} sprites (Row 0 = Orange).");
    }

    private static Sprite[] LoadSpritesByPrefix(string texturePath, string prefix)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        List<Sprite> list = new List<Sprite>();
        foreach (Object obj in assets)
        {
            if (obj is Sprite s && s.name.StartsWith(prefix))
            {
                list.Add(s);
            }
        }

        list.Sort((a, b) =>
        {
            int aIndex = ExtractTrailingNumber(a.name);
            int bIndex = ExtractTrailingNumber(b.name);
            return aIndex.CompareTo(bIndex);
        });

        return list.ToArray();
    }

    private static int ExtractTrailingNumber(string name)
    {
        int lastUnder = name.LastIndexOf('_');
        if (lastUnder >= 0 && int.TryParse(name.Substring(lastUnder + 1), out int val))
        {
            return val;
        }
        return 0;
    }

    private static void CreateAnimationClips(Sprite[] attackSprites, Sprite[] explosionSprites)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
        {
            AssetDatabase.CreateFolder("Assets", "Animations");
        }

        if (attackSprites != null && attackSprites.Length > 0)
        {
            string path = "Assets/Animations/AtaqueNormal_Orange.anim";
            AnimationClip clip = new AnimationClip();
            clip.frameRate = 12;

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[attackSprites.Length];
            for (int i = 0; i < attackSprites.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = (float)i / 12f,
                    value = attackSprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            AssetDatabase.CreateAsset(clip, path);
            Debug.Log($"[Setup] Creado AnimationClip: {path}");
        }

        if (explosionSprites != null && explosionSprites.Length > 0)
        {
            string path = "Assets/Animations/Explosion_Orange.anim";
            AnimationClip clip = new AnimationClip();
            clip.frameRate = 12;

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[explosionSprites.Length];
            for (int i = 0; i < explosionSprites.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = (float)i / 12f,
                    value = explosionSprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            AssetDatabase.CreateAsset(clip, path);
            Debug.Log($"[Setup] Creado AnimationClip: {path}");
        }
    }

    private static void UpdateItemScriptableObjects()
    {
        // Botella sprite en Decoration 32x32.png
        Sprite bottleSprite = FindSpriteInPath("Assets/Sprites/GandalfHardcore City Tiles/Decoration 32x32.png", "Decoration 32x32_0");

        string[] bottleItemPaths = new string[]
        {
            "Assets/Data/Items/Botella_Vacia.asset",
            "Assets/Data/Items/Item_BotellaVacia.asset"
        };

        foreach (string p in bottleItemPaths)
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(p);
            if (item != null)
            {
                if (bottleSprite != null)
                {
                    item.icon = bottleSprite;
                    EditorUtility.SetDirty(item);
                    Debug.Log($"[Setup] Asignado icono de botella a {p}");
                }
            }
        }
    }

    private static void UpdateSceneObjectsAndBattleManager(Sprite[] attackSprites, Sprite[] explosionSprites)
    {
        Scene currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != SCENE_PATH)
        {
            currentScene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        }

        // Cargar sprites para los objetos
        Sprite bottleSprite = FindSpriteInPath("Assets/Sprites/GandalfHardcore City Tiles/Decoration 32x32.png", "Decoration 32x32_0");
        Sprite sodaSprite = LoadSingleSpriteAtPath("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Canned-soup.png");
        if (sodaSprite == null) sodaSprite = FindSpriteInPath("Assets/Sprites/GandalfHardcore City Tiles/Decoration 32x32.png", "Decoration 32x32_3");
        Sprite foodSprite = LoadSingleSpriteAtPath("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Canned-food.png");
        Sprite chileSprite = LoadSingleSpriteAtPath("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Bullet-box_1_Red.png");
        Sprite bandageSprite = LoadSingleSpriteAtPath("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Pickable/Bandage.png");
        Sprite pipeSprite = LoadSingleSpriteAtPath("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Exhaust-pipe.png");
        Sprite cardboardSprite = LoadSingleSpriteAtPath("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Cardboard_1.png");
        Sprite signSprite = LoadSingleSpriteAtPath("Assets/Sprites/PostApocalypse_AssetPack_v1.1.2/Objects/Stop-sign_Down_1.png");

        // 10 objetos en la escena
        AssignSpriteToSceneObject("Pickup_Botella", bottleSprite);
        AssignSpriteToSceneObject("Pickup_Botella (1)", bottleSprite);
        AssignSpriteToSceneObject("Pickup_Refresco_Curativo", sodaSprite);
        AssignSpriteToSceneObject("Pickup_Torta_Callejera", foodSprite);
        AssignSpriteToSceneObject("Pickup_Chile", chileSprite);
        AssignSpriteToSceneObject("Pickup_Chile (1)", chileSprite);
        AssignSpriteToSceneObject("Pickup_Cinta_Canela", bandageSprite);
        AssignSpriteToSceneObject("Pickup_Tubo_Plomo", pipeSprite);
        AssignSpriteToSceneObject("Dialogo_3_CajasSospechosas", cardboardSprite);
        AssignSpriteToSceneObject("Dialogo_1_CartelCallejon", signSprite);

        // Configurar BattleManager
        BattleManager bm = Object.FindAnyObjectByType<BattleManager>();
        if (bm != null)
        {
            if (bm.corporeoGif == null) bm.corporeoGif = new ActionGifData();
            bm.corporeoGif.gifFrames = attackSprites;

            if (bm.enemyAttackGif == null) bm.enemyAttackGif = new ActionGifData();
            bm.enemyAttackGif.gifFrames = attackSprites;

            if (bm.explosionGif == null) bm.explosionGif = new ActionGifData();
            bm.explosionGif.gifFrames = explosionSprites;

            EditorUtility.SetDirty(bm);
            Debug.Log("[Setup] Asignados frames de ataque y explosión naranja al BattleManager de la escena.");
        }
        else
        {
            Debug.LogWarning("[Setup] No se encontró BattleManager en la escena activa.");
        }

        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        Debug.Log($"[Setup] Escena {SCENE_PATH} guardada exitosamente.");
    }

    private static void AssignSpriteToSceneObject(string gameObjectName, Sprite sprite)
    {
        if (sprite == null)
        {
            Debug.LogWarning($"[Setup] El sprite proporcionado para {gameObjectName} es nulo.");
            return;
        }

        GameObject go = GameObject.Find(gameObjectName);
        if (go == null)
        {
            Debug.LogWarning($"[Setup] No se encontró el GameObject '{gameObjectName}' en la escena.");
            return;
        }

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = sprite;
            sr.color = Color.white;
            EditorUtility.SetDirty(sr);
            Debug.Log($"[Setup] Asignado sprite '{sprite.name}' y color blanco a '{gameObjectName}'.");
        }
        else
        {
            Debug.LogWarning($"[Setup] '{gameObjectName}' no tiene componente SpriteRenderer.");
        }
    }

    private static Sprite FindSpriteInPath(string path, string spriteName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object o in assets)
        {
            if (o is Sprite s && s.name == spriteName)
            {
                return s;
            }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite LoadSingleSpriteAtPath(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object o in assets)
        {
            if (o is Sprite s)
            {
                return s;
            }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
