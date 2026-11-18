using UnityEditor;
using UnityEngine;

/// <summary>
/// Evita errores al iniciar Unity causados por la ventana modal de bienvenida de com.unity.learn.iet-framework
/// (NullReferenceException en TutorialModalWindow.UpdateContent y OnGUIDepth changed).
/// </summary>
[InitializeOnLoad]
public static class DisableBrokenWelcomeModal
{
    private const string WelcomeDialogSeenKey = "TutorialFramework.WelcomeDialogSeen";

    static DisableBrokenWelcomeModal()
    {
        SessionState.SetBool(WelcomeDialogSeenKey, true);
        EditorApplication.delayCall += CloseOrphanTutorialModalWindows;
    }

    private static void CloseOrphanTutorialModalWindows()
    {
        var allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (var win in allWindows)
        {
            if (win != null && win.GetType().FullName == "Unity.Tutorials.Editor.TutorialModalWindow")
            {
                win.Close();
            }
        }
    }
}
