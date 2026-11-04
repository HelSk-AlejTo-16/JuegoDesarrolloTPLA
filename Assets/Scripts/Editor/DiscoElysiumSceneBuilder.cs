#if UNITY_EDITOR
using UnityEditor;

public static class DiscoElysiumSceneBuilder
{
    [MenuItem("Tools/DiscoElysium2/Build Complete Scene Skeleton")]
    public static void BuildAll()
    {
        SceneSkeletonBuilder.BuildCompleteSceneSkeleton();
    }
}
#endif
