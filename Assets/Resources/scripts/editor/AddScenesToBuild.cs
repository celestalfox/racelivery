using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AddScenesToBuild
{
    // Order matters: the first scene is the one the game starts in.
    static readonly string[] SceneNames =
    {
        "Main_menu", "Vehicle Menu", "SHOP", "Settings_menu",
        "f1", "moantain",
        "INGAME_UI", "Pause_menu", "DEATH"
    };

    [MenuItem("Tools/Add Menu + Stage Scenes To Build")]
    static void Run()
    {
        var allScenes = AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.Contains("_Recovery"))
            .ToList();

        var list = new List<EditorBuildSettingsScene>();
        foreach (string name in SceneNames)
        {
            string path = allScenes.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
            if (path == null)
            {
                Debug.LogWarning($"[AddScenesToBuild] Scene '{name}' not found, skipped.");
                continue;
            }
            list.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[AddScenesToBuild] Build list set: {string.Join(", ", list.Select(s => Path.GetFileNameWithoutExtension(s.path)))}");
    }
}
