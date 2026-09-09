using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ChickenGameSceneBuilder
{
    [MenuItem("Chicken Game/Build MVP Scene")]
    public static void BuildScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject bootstrap = new GameObject("Chicken Game");
        bootstrap.AddComponent<ChickenGameBootstrap>();

        string folder = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ChickenGame.unity");
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/ChickenGame.unity", true)
        };
        AssetDatabase.SaveAssets();
        Debug.Log("Chicken Game MVP scene created.");
    }
}
