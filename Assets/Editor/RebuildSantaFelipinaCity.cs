#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using UnityEditor.SceneManagement;

namespace SantaFelipina.EditorTools
{
    public static class ClearSantaFelipinaCity
    {
        [MenuItem("RPG Santa Felipina/Vaciar Ciudad y Limpiar Sprites")]
        public static void ClearCity()
        {
            Undo.SetCurrentGroupName("Vaciar Ciudad y Limpiar Sprites");
            int undoGroup = Undo.GetCurrentGroup();

            // 1. Eliminar contenedor de decoraciones y props generados
            GameObject decoraciones = GameObject.Find("Decoraciones_SantaFelipina");
            if (decoraciones != null)
            {
                Undo.DestroyObjectImmediate(decoraciones);
                Debug.Log("<color=yellow>[Santa Felipina]</color> Se ha eliminado el contenedor 'Decoraciones_SantaFelipina' y todos sus props.");
            }

            // 2. Limpiar todas las baldosas de los Tilemaps de la ciudad
            string[] tilemapNames = new string[] { "suelo", "edificios", "decoracionN", "decoracionO", "Suelo", "Edificios" };
            foreach (var tName in tilemapNames)
            {
                GameObject go = GameObject.Find(tName);
                if (go != null)
                {
                    Tilemap tm = go.GetComponent<Tilemap>();
                    if (tm != null)
                    {
                        Undo.RecordObject(tm, "Limpiar Tilemap " + tName);
                        tm.ClearAllTiles();
                        tm.RefreshAllTiles();
                        Debug.Log($"<color=yellow>[Santa Felipina]</color> Tilemap '{tName}' vaciado por completo.");
                    }
                }
            }

            // También buscar cualquier otro Tilemap en la escena para asegurar que quede completamente vacío
            Tilemap[] allTilemaps = Object.FindObjectsByType<Tilemap>();
            foreach (var tm in allTilemaps)
            {
                // Limpiar cualquier tilemap que no sea de la UI o cuartos secretos
                if (tm.name.ToLower().Contains("suelo") || 
                    tm.name.ToLower().Contains("edificio") || 
                    tm.name.ToLower().Contains("decoracion"))
                {
                    Undo.RecordObject(tm, "Limpiar " + tm.name);
                    tm.ClearAllTiles();
                    tm.RefreshAllTiles();
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=green>[Santa Felipina]</color> Ciudad vaciada exitosamente. Los Grids y Tilemaps están limpios y listos para dibujar a mano con la Tile Palette.");
        }
    }
}
#endif
