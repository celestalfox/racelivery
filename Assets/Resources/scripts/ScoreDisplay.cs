using UnityEngine;
using TMPro;
using CarContollingScripts;

/// <summary>
/// Shows the car's score, zero-padded to the length of the text already in the UI
/// (a text that starts as "00000000000" shows 245 as "00000000245").
/// Finds the player's CarController by itself, so nothing needs to be assigned
/// (GameFlow attaches this to the score number in INGAME_UI automatically).
/// </summary>
public class ScoreDisplay : MonoBehaviour
{
    public TMP_Text uiText;          // optional: found on this object if left empty

    [Tooltip("Number of digits. 0 = copy the length of the text that is in the UI at start.")]
    public int digits = 0;

    CarController car;
    int lastShown = -1;
    float nextSearch;
    string format;

    void Awake()
    {
        if (uiText == null) uiText = GetComponent<TMP_Text>();

        if (digits <= 0)
        {
            string t = uiText != null ? uiText.text.Trim() : "";
            digits = (t.Length >= 2 && IsAllDigits(t)) ? t.Length : 8;   // 8 if the start text isn't zeros
        }
        format = "D" + digits;

        if (uiText != null) uiText.text = 0.ToString(format);
    }

    void Update()
    {
        if (uiText == null) return;

        // (Re)find the car if we don't have one yet, e.g. the UI loaded before the car.
        if (car == null)
        {
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 0.5f;
            car = FindFirstObjectByType<CarController>();
            if (car == null) return;
            lastShown = -1;
        }

        int s = Mathf.FloorToInt(car.score);
        if (s == lastShown) return;   // only touch the UI when the number changes

        lastShown = s;
        uiText.text = s.ToString(format);
    }

    static bool IsAllDigits(string t)
    {
        for (int i = 0; i < t.Length; i++)
            if (t[i] < '0' || t[i] > '9') return false;
        return true;
    }
}
