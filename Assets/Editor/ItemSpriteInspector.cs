#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ItemSpriteInspector
{
    [MenuItem("Tools/Inspeccionar Sprites de Items")]
    public static void InspectItemSprites()
    {
        string[] guids = new string[]
        {
            "5cc5bad929b984642b377064d1980aac", // Bat
            "23ea9d59c3dae0547a06f9ce9552632e", // Cadena
            "573c400a6db199c4eb75f113b3ac6c5c", // Tubo
            "457c2ee17531fdd438a4408b2df779ed", // Gasolina
            "1c40b3b7698f5b14a98c9a344d7cc17f", // Cinta
            "ed53148bcda3dee40b69994eef5adbf3", // Refresco
            "ff3db27107d411848bbe700c75b5c879"  // Torta
        };

        foreach (string g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            Debug.Log($"[ItemSpriteInspector] GUID {g} -> Path: {p}");
        }
    }
}
#endif
