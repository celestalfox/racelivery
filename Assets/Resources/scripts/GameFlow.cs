using System.Collections.Generic;
using CarContollingScripts;
using TMPro;
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
    public static readonly string[] StageNames = { "F1", "moantain" };   // shown in the Vehicle Menu
    const string StageLabelObject = "Text (TMP)";                        // the text that shows the stage name
    public static bool UseTimer = true;                                  // attach the countdown to the TIME text
    public static readonly float[] StageTimeSeconds = { 120f, 120f };    // time limit per stage (same order as Stages)
    public const int CarCount = 2;            // how many cars LEFT/RIGHT cycles through

    // Show the new INGAME_UI (pause button) on top of stages and hide the old in-stage pause UI
    public static bool UseNewInGameUI = true;

    public static int SelectedCar { get; private set; }
    public static int SelectedStage { get; private set; }

    static string lastStage = Stages[0];
    static bool timeUpScreen;   // true when DEATH is opened because time ran out

    /// <summary>Score of the run that just ended (set when the death screen opens).</summary>
    public static int LastScore { get; private set; }
    public static int BestScore => PlayerPrefs.GetInt("bestScore_" + lastStage, 0);

    // ------------------------------------------------------------------ boot

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SelectedCar = PlayerPrefs.GetInt("selectedCar", 0);
        SelectedStage = Mathf.Clamp(PlayerPrefs.GetInt("selectedStage", 0), 0, Stages.Length - 1);
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
            timeUpScreen = false;
        }
        else
        {
            PrepareAdditive(scene);
        }

        DisableTextRaycasts(scene);
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
                Wire(scene, "back|BACK", () => Go(MainMenu));
                Wire(scene, "next/play|NEXT", () => Go(Stages[SelectedStage]));
                Wire(scene, "select car/track UP", () => ChangeStage(-1));
                Wire(scene, "select car/track down", () => ChangeStage(+1));
                RefreshVehicleMenu();
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
                AttachScoreDisplay(scene);
                if (UseTimer) AttachTimer(scene);
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
                ShowScoreOnDeath(scene);
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

    /// <summary>objectNames can list alternatives separated by '|': the first one found is used.</summary>
    static void Wire(Scene scene, string objectNames, UnityAction action)
    {
        Transform t = null;
        foreach (string n in objectNames.Split('|'))
        {
            t = FindIn(scene, n);
            if (t != null) break;
        }
        if (t == null)
        {
            Debug.LogWarning($"[GameFlow] '{objectNames}' not found in scene '{scene.name}'");
            return;
        }

        // Make sure the object can actually receive clicks.
        Graphic g = t.GetComponent<Graphic>();
        if (g == null)
        {
            g = t.gameObject.AddComponent<Image>();
            ((Image)g).color = Color.clear;
        }
        g.raycastTarget = true;

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
    public static void ShowDeath() => ShowEndScreen(false);

    /// <summary>Called by the timer when it reaches zero: same screen, but with the final score.</summary>
    public static void ShowTimeUp() => ShowEndScreen(true);

    static void ShowEndScreen(bool timeUp)
    {
        if (SceneManager.GetSceneByName(Death).isLoaded) return;   // already showing
        timeUpScreen = timeUp;

        CarController car = Object.FindFirstObjectByType<CarController>();
        LastScore = car != null ? Mathf.FloorToInt(car.score) : 0;
        if (LastScore > BestScore) PlayerPrefs.SetInt("bestScore_" + lastStage, LastScore);

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
        PlayerPrefs.SetInt("selectedStage", SelectedStage);
        Debug.Log($"[GameFlow] Selected stage: {Stages[SelectedStage]}");
        RefreshVehicleMenu();
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

    /// <summary>Find the "SCORE :" label in INGAME_UI and give the number next to it a ScoreDisplay.</summary>
    static void AttachScoreDisplay(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text.Replace(":", "").Trim().ToUpperInvariant() != "SCORE") continue;

                Transform parent = label.transform.parent;
                for (int i = 0; parent != null && i < parent.childCount; i++)
                {
                    Transform sibling = parent.GetChild(i);
                    if (sibling == label.transform) continue;
                    TMP_Text value = sibling.GetComponent<TMP_Text>();
                    if (value == null) continue;

                    if (value.GetComponent<ScoreDisplay>() == null)
                        value.gameObject.AddComponent<ScoreDisplay>().uiText = value;
                    return;
                }
            }
        }
        Debug.LogWarning("[GameFlow] Could not find the 'SCORE :' label in INGAME_UI");
    }

    /// <summary>
    /// The DEATH scene has no score text: copy the "YOU LOST" text and put it underneath.
    /// When time ran out, the heading becomes "FINAL SCORE" and the number is shown big.
    /// </summary>
    static void ShowScoreOnDeath(Scene scene)
    {
        Transform lose = FindIn(scene, "LOSE");
        if (lose == null) { Debug.LogWarning("[GameFlow] 'LOSE' text not found in DEATH"); return; }

        TMP_Text loseText = lose.GetComponent<TMP_Text>();
        if (timeUpScreen && loseText != null) loseText.text = "FINAL SCORE";

        GameObject clone = Object.Instantiate(lose.gameObject, lose.parent);
        clone.name = "SCORE_TEXT";

        RectTransform src = (RectTransform)lose;
        RectTransform rt = (RectTransform)clone.transform;
        rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, timeUpScreen ? -170f : -250f);
        rt.sizeDelta = new Vector2(src.sizeDelta.x, 200f);

        TMP_Text txt = clone.GetComponent<TMP_Text>();
        txt.raycastTarget = false;
        int best = Mathf.Max(BestScore, LastScore);

        if (timeUpScreen)
        {
            txt.fontSize = 110f;
            txt.text = $"{LastScore}\n<size=50>BEST  {best}</size>";
        }
        else
        {
            txt.fontSize = 60f;
            txt.text = $"SCORE  {LastScore}\nBEST  {best}";
        }
    }

    /// <summary>Find the "TIME :" label in INGAME_UI and put a countdown on the number next to it.</summary>
    static void AttachTimer(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<TimeSet>(true) != null) return;   // you placed one yourself

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text.Replace(":", "").Trim().ToUpperInvariant() != "TIME") continue;

                Transform parent = label.transform.parent;
                for (int i = 0; parent != null && i < parent.childCount; i++)
                {
                    Transform sibling = parent.GetChild(i);
                    if (sibling == label.transform || sibling.GetComponent<TMP_Text>() == null) continue;

                    TimeSet timer = sibling.gameObject.AddComponent<TimeSet>();
                    int idx = System.Array.IndexOf(Stages, lastStage);
                    if (idx >= 0 && idx < StageTimeSeconds.Length) timer.duration = StageTimeSeconds[idx];
                    return;
                }
            }
        }
        Debug.LogWarning("[GameFlow] Could not find the 'TIME :' label in INGAME_UI");
    }

    /// <summary>Update the "1 / 2" counter and the stage name in the Vehicle Menu.</summary>
    static void RefreshVehicleMenu()
    {
        Scene s = SceneManager.GetSceneByName(VehicleMenu);
        if (!s.isLoaded) return;
        Transform t = FindIn(s, "Text (TMP) (1)");
        if (t != null)
        {
            TMP_Text counter = t.GetComponent<TMP_Text>();
            if (counter != null) counter.SetText("{0} / {1}"+ SelectedStage + 1, Stages.Length);
        }

        Transform label = FindIn(s, StageLabelObject);
        if (label != null)
        {
            TMP_Text name = label.GetComponent<TMP_Text>();
            if (name != null) name.text = StageNames[SelectedStage];
        }
    }

    /// <summary>
    /// Text sitting on top of a button (BACK, NEXT...) swallows the click. Text never needs
    /// to be clickable, so turn raycasts off on all of it. Wire() re-enables it on real targets.
    /// </summary>
    static void DisableTextRaycasts(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var txt in root.GetComponentsInChildren<TMP_Text>(true))
                txt.raycastTarget = false;
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
