using UnityEngine;

public static class FrameRateSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Application.targetFrameRate = 60; // change to 90 or 120 if your device supports it
    }
}