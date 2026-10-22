using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Drop-in menu flow. No scene editing required: when any scene loads, this finds the
/// buttons by their GameObject names and wires them up. Edit the switch in WireScene()
/// to change what a button does.
///
/// Call GameFlow.ShowDeath() from your game code when the player loses.
/// </summary>
public static class GameFlow
{
    // ---- Scene names: must match the .unity file names and be in the Build Profiles list ----
    public const string MainMenu = "Main_menu";
    public const string VehicleMenu = "Vehicle Menu";
    public const string Shop = "SHOP";
    public const string Settings = "Settings_menu";
    public const string Pause = "Pause_menu";
    public const string Death = "DEATH";
    public const string InGameUI = "INGAME_UI";

    public static readonly string[] Stages = { "f1", "moantain" };
    public const int CarCount = 2;            // how many cars LEFT/RIGHT cycles through

    // Show the new INGAME_UI (pause button) on top of stages and hide the old in-stage pause UI
    public static bool UseNewInGameUI = true;

    public static int SelectedCar { get; private set; }
    public static int SelectedStage { get; private set; }

    static string lastStage = Stages[0];

    // ------------------------------------------------------------------ boot

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SelectedCar = PlayerPrefs.GetInt("selectedCar", 0);
        ApplyVolume();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single); // first scene
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool additive = mode == LoadSceneMode.Additive;

        if (!additive)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
        else
        {
            PrepareAdditive(scene);
        }

        WireScene(scene, additive);

        if (!additive && System.Array.IndexOf(Stages, scene.name) >= 0)
        {
            lastStage = scene.name;
            if (UseNewInGameUI)
            {
                HideOldPauseUI(scene);
                LoadAdditive(InGameUI);
            }
        }

        EnsureEventSystem();
    }

    // ------------------------------------------------------------------ button wiring

    static void WireScene(Scene scene, bool additive)
    {
        switch (scene.name)
        {
            case MainMenu:
                Wire(scene, "Button", () => Go(VehicleMenu));       // PLAY
                Wire(scene, "SHOP", () => Go(Shop));
                Wire(scene, "Settings", OpenSettings);
                Wire(scene, "SOUND", ToggleMute);
                Wire(scene, "ADS", () => Debug.Log("[GameFlow] ADS pressed: hook your ad SDK here"));
                break;

            case VehicleMenu:
                Wire(scene, "BACK", () => Go(MainMenu));
                Wire(scene, "NEXT", () => Go(Stages[SelectedStage]));
                Wire(scene, "LEFT", () => ChangeCar(-1));
                Wire(scene, "RIGHT", () => ChangeCar(+1));
                Wire(scene, "UP", () => ChangeStage(-1));
                Wire(scene, "DOWN", () => ChangeStage(+1));
                break;

            case Shop:
                Wire(scene, "Menu", () => Go(MainMenu));
                break;

            case Settings:
                Wire(scene, "Menu", () =>
                {
                    if (additive) Unload(Settings); else Go(MainMenu);
                });
                WireSliders(scene);
                break;

            case InGameUI:
                Wire(scene, "Button", OpenPause);                    // pause icon
                break;

            case Pause:
                Wire(scene, "RESUME", Resume);
                Wire(scene, "BACK", () => Go(MainMenu));
                Wire(scene, "Settings", OpenSettings);
                break;

            case Death:
                Wire(scene, "RESUME", () => Go(MainMenu));           // left-arrow button
                Wire(scene, "RESTART", Retry);
                Wire(scene, "RETRY", Retry);
                Wire(scene, "AD_Button", () => Debug.Log("[GameFlow] Revive-by-ad pressed: hook your ad SDK here"));
                break;
        }
    }

    static void WireSliders(Scene scene)
    {
        var sliders = new List<Slider>();
        foreach (var root in scene.GetRootGameObjects())
            sliders.AddRange(root.GetComponentsInChildren<Slider>(true));

        if (sliders.Count > 0) // first slider = master volume
        {
            sliders[0].SetValueWithoutNotify(PlayerPrefs.GetFloat("master", 1f));
            sliders[0].onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetFloat("master", v);
                PlayerPrefs.SetInt("muted", 0);
                ApplyVolume();
            });
        }
        if (sliders.Count > 1) // second slider = saved as "sfx", use it where you play effects
        {
            sliders[1].SetValueWithoutNotify(PlayerPrefs.GetFloat("sfx", 1f));
            sliders[1].onValueChanged.AddListener(v => PlayerPrefs.SetFloat("sfx", v));
        }
    }

    static void Wire(Scene scene, string objectName, UnityAction action)
    {
        Transform t = FindIn(scene, objectName);
        if (t == null)
        {
            Debug.LogWarning($"[GameFlow] '{objectName}' not found in scene '{scene.name}'");
            return;
        }

        // Some objects (e.g. BACK / NEXT) may not be real buttons yet: make them clickable.
        if (t.GetComponent<Graphic>() == null)
        {
            var img = t.gameObject.AddComponent<Image>();
            img.color = Color.clear;
        }
        Button b = t.GetComponent<Button>();
        if (b == null) b = t.gameObject.AddComponent<Button>();

        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(action);
    }

    static Transform FindIn(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == objectName) return t;
        return null;
    }

    // ------------------------------------------------------------------ actions

    public static void OpenPause()
    {
        LoadAdditive(Pause);
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    public static void Resume()
    {
        Unload(Settings);
        Unload(Pause);
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    /// <summary>Call this from game code when the player loses.</summary>
    public static void ShowDeath()
    {
        LoadAdditive(Death);
        Time.timeScale = 0f;
    }

    public static void Retry() => Go(lastStage);

    static void OpenSettings()
    {
        // Over the pause menu: stack on top. From the main menu: normal scene change.
        if (SceneManager.GetSceneByName(Pause).isLoaded) LoadAdditive(Settings);
        else Go(Settings);
    }

    static void ChangeCar(int d)
    {
        SelectedCar = (SelectedCar + d + CarCount) % CarCount;
        PlayerPrefs.SetInt("selectedCar", SelectedCar);
        Debug.Log($"[GameFlow] Selected car: {SelectedCar}");
    }

    static void ChangeStage(int d)
    {
        SelectedStage = (SelectedStage + d + Stages.Length) % Stages.Length;
        Debug.Log($"[GameFlow] Selected stage: {Stages[SelectedStage]}");
        SceneManager.LoadScene(SelectedStage);
    }

    static void ToggleMute()
    {
        bool muted = PlayerPrefs.GetInt("muted", 0) == 1;
        PlayerPrefs.SetInt("muted", muted ? 0 : 1);
        ApplyVolume();
    }

    static void ApplyVolume()
    {
        AudioListener.volume = PlayerPrefs.GetInt("muted", 0) == 1 ? 0f : PlayerPrefs.GetFloat("master", 1f);
    }

    // ------------------------------------------------------------------ scene helpers

    static void Go(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[GameFlow] Scene '{sceneName}' is not in the Build Profiles scene list. Run Tools > Add Menu + Stage Scenes To Build.");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }

    static void LoadAdditive(string sceneName)
    {
        if (SceneManager.GetSceneByName(sceneName).isLoaded) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[GameFlow] Scene '{sceneName}' is not in the Build Profiles scene list.");
            return;
        }
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    }

    static void Unload(string sceneName)
    {
        Scene s = SceneManager.GetSceneByName(sceneName);
        if (s.isLoaded) SceneManager.UnloadSceneAsync(s);
    }

    /// <summary>An overlay scene brings its own camera/listener/EventSystem: remove the duplicates.</summary>
    static void PrepareAdditive(Scene scene)
    {
        int order = scene.name == Settings ? 200 : scene.name == InGameUI ? 10 : 100;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var cam in root.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
            foreach (var al in root.GetComponentsInChildren<AudioListener>(true)) al.enabled = false;
            foreach (var cv in root.GetComponentsInChildren<Canvas>(true))
                if (cv.isRootCanvas) cv.sortingOrder = order;
        }

        if (Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length > 1)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var es in root.GetComponentsInChildren<EventSystem>(true))
                    es.gameObject.SetActive(false);
        }
    }

    static void HideOldPauseUI(Scene scene)
    {
        Transform t = FindIn(scene, "pausebutton");
        if (t != null) t.gameObject.SetActive(false);
        t = FindIn(scene, "pausepanel");
        if (t != null) t.gameObject.SetActive(false);
    }

    /// <summary>DEATH, SHOP and Vehicle Menu have no EventSystem, so their buttons would not respond.</summary>
    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem (auto)");
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }
}
