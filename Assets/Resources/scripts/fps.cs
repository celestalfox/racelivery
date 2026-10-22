using UnityEngine;
using TMPro;
using CarContollingScripts;

public class ScoreDisplay : MonoBehaviour
{
    public TMP_Text uiText;
    public CarController car;

    int lastShown = -1;

    void Update()
    {
        if (car == null || uiText == null) return;

        int s = Mathf.FloorToInt(car.score);
        if (s == lastShown) return;   // only touch the UI when the number changes

        lastShown = s;
        uiText.SetText("{0}", s);     // no string allocation, unlike ToString()
    }
}