#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerCardoSetup
{
    private const float SHEET_PPU = 210f;
    private const float IDLE_ORIGINAL_PPU = 48f;

    [MenuItem("RPG Santa Felipina/Configurar Protagonista Cardo (Idle y Movimiento)")]
    public static void SetupPlayerCardoIdleAndMovement()
    {
        PlayerPersonajeSetup.SetupPlayerPersonajeFull();
        PlayerPersonajeSetup.SetupPlayerHealthBarsVisual();
    }

    [MenuItem("RPG Santa Felipina/Configurar Barra de Vida Visual (Rojo y Porcentaje)")]
    public static void SetupHealthBarOnly()
    {
        PlayerPersonajeSetup.SetupPlayerHealthBarsVisual();
    }

    [MenuItem("RPG Santa Felipina/Configurar Musica Completa")]
    public static void SetupMusicSystemMenu()
    {
        MusicSetupUtility.SetupMusicSystemFull();
    }

    private static Sprite[] LoadOriginalIdleSprites(string idleFolder, int count)
    {
        Sprite[] result = new Sprite[count];
        for (int i = 1; i <= count; i++)
        {
            string framePath = $"{idleFolder}/{i}.png";
            UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(framePath);
            foreach (var asset in allAssets)
            {
                if (asset is Sprite s)
                {
                    result[i - 1] = s;
                    break;
                }
            }
        }
        return result;
    }

    private static GameObject FindPlayerInLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            foreach (GameObject root in s.GetRootGameObjects())
            {
                if (root.name == "Enrique_Player") return root;
                Transform t = root.transform.Find("Enrique_Player");
                if (t != null) return t.gameObject;
            }
        }
        return GameObject.Find("Enrique_Player");
    }

    private static void SliceCardoFrameUniformly(string assetPath, string spriteName)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = IDLE_ORIGINAL_PPU;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        SerializedObject so = new SerializedObject(importer);
        SerializedProperty spritesProp = so.FindProperty("m_SpriteSheet.m_Sprites");
        SerializedProperty nameTableProp = so.FindProperty("m_InternalIDToNameTable");

        if (spritesProp != null)
        {
            spritesProp.arraySize = 1;
            SerializedProperty elem = spritesProp.GetArrayElementAtIndex(0);

            SerializedProperty nameProp = elem.FindPropertyRelative("m_Name");
            if (nameProp != null) nameProp.stringValue = spriteName;

            SerializedProperty rectProp = elem.FindPropertyRelative("m_Rect");
            if (rectProp != null) rectProp.rectValue = new Rect(32, 0, 64, 76);

            SerializedProperty alignProp = elem.FindPropertyRelative("m_Alignment");
            if (alignProp != null) alignProp.intValue = 9; // Custom Pivot

            SerializedProperty pivotProp = elem.FindPropertyRelative("m_Pivot");
            if (pivotProp != null) pivotProp.vector2Value = new Vector2(0.5f, 0.0f);

            SerializedProperty spriteIdProp = elem.FindPropertyRelative("m_SpriteID");
            if (spriteIdProp != null && string.IsNullOrEmpty(spriteIdProp.stringValue))
            {
                spriteIdProp.stringValue = GUID.Generate().ToString();
            }

            SerializedProperty internalIdProp = elem.FindPropertyRelative("m_InternalID");
            if (internalIdProp != null && internalIdProp.longValue == 0)
            {
                internalIdProp.longValue = 4723107735738678600L;
            }
        }

        if (nameTableProp != null && spritesProp != null)
        {
            nameTableProp.arraySize = 1;
            SerializedProperty spriteElem = spritesProp.GetArrayElementAtIndex(0);
            long internalId = spriteElem.FindPropertyRelative("m_InternalID").longValue;

            SerializedProperty tableElem = nameTableProp.GetArrayElementAtIndex(0);
            SerializedProperty firstProp = tableElem.FindPropertyRelative("first");
            if (firstProp != null)
            {
                SerializedProperty idProp = firstProp.FindPropertyRelative("213");
                if (idProp != null) idProp.longValue = internalId;
            }
            SerializedProperty secondProp = tableElem.FindPropertyRelative("second");
            if (secondProp != null) secondProp.stringValue = spriteName;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        importer.SaveAndReimport();
    }

    private static void SliceSpritesheet(string assetPath, string spritePrefix, Rect[] rects, float ppu)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[PlayerCardoSetup] No se encontro TextureImporter para {assetPath}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = ppu;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        SerializedObject so = new SerializedObject(importer);
        SerializedProperty spritesProp = so.FindProperty("m_SpriteSheet.m_Sprites");
        SerializedProperty nameTableProp = so.FindProperty("m_InternalIDToNameTable");

        if (spritesProp != null)
        {
            spritesProp.arraySize = rects.Length;
            for (int i = 0; i < rects.Length; i++)
            {
                SerializedProperty elem = spritesProp.GetArrayElementAtIndex(i);
                string spriteName = $"{spritePrefix}_{i}";

                SerializedProperty nameProp = elem.FindPropertyRelative("m_Name");
                if (nameProp != null) nameProp.stringValue = spriteName;

                SerializedProperty rectProp = elem.FindPropertyRelative("m_Rect");
                if (rectProp != null) rectProp.rectValue = rects[i];

                SerializedProperty alignProp = elem.FindPropertyRelative("m_Alignment");
                if (alignProp != null) alignProp.intValue = 9; // Custom Pivot

                SerializedProperty pivotProp = elem.FindPropertyRelative("m_Pivot");
                if (pivotProp != null) pivotProp.vector2Value = new Vector2(0.5f, 0.0f);

                SerializedProperty spriteIdProp = elem.FindPropertyRelative("m_SpriteID");
                if (spriteIdProp != null && string.IsNullOrEmpty(spriteIdProp.stringValue))
                {
                    spriteIdProp.stringValue = GUID.Generate().ToString();
                }

                SerializedProperty internalIdProp = elem.FindPropertyRelative("m_InternalID");
                if (internalIdProp != null && internalIdProp.longValue == 0)
                {
                    internalIdProp.longValue = (long)(UnityEngine.Random.Range(100000000, int.MaxValue)) * 1000000L + i;
                }
            }
        }

        if (nameTableProp != null && spritesProp != null)
        {
            nameTableProp.arraySize = rects.Length;
            for (int i = 0; i < rects.Length; i++)
            {
                SerializedProperty spriteElem = spritesProp.GetArrayElementAtIndex(i);
                long internalId = spriteElem.FindPropertyRelative("m_InternalID").longValue;
                string spriteName = $"{spritePrefix}_{i}";

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

    private static Sprite[] LoadOrderedSprites(string assetPath, string prefix, int count)
    {
        Sprite[] result = new Sprite[count];
        UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        foreach (var asset in allAssets)
        {
            if (asset is Sprite s && s.name.StartsWith(prefix))
            {
                string suffix = s.name.Substring(prefix.Length).TrimStart('_');
                if (int.TryParse(suffix, out int idx) && idx >= 0 && idx < count)
                {
                    result[idx] = s;
                }
            }
        }

        return result;
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
}
#endif
