using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Countdown timer for a TextMeshPro text. Shows "MM : SS : CC" (minutes : seconds : hundredths),
/// ticks down, and when it reaches zero opens the end screen with the FINAL SCORE.
///
/// GameFlow adds this automatically to the number next to "TIME :" in INGAME_UI, using the
/// per-stage limits in GameFlow.StageTimeSeconds. You can also add it by hand to any text
/// (then set Duration here).
///
/// Uses scaled time, so it freezes while the game is paused.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TimeSet : MonoBehaviour
{
    [Tooltip("Starting time in seconds.")]
    public float duration = 120f;

    public bool startOnStart = true;

    [Tooltip("Show MM : SS : CC. Off = MM : SS.")]
    public bool showHundredths = true;

    [Header("Warning")]
    public float warnAtSeconds = 10f;
    public Color warnColor = new Color(1f, 0.25f, 0.25f);

    [Header("When it hits zero")]
    [Tooltip("Open the end screen with the final score.")]
    public bool endsGame = true;
    public UnityEvent onTimeUp;

    TMP_Text text;
    Color baseColor;
    float remaining;
    bool running;
    bool finished;
    char[] buf;
    int lastShown = -1;

    public float Remaining => remaining;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        baseColor = text.color;
    }

    // Start (not Awake) so that a duration set right after AddComponent() is respected.
    void Start()
    {
        buf = showHundredths ? "00 : 00 : 00".ToCharArray() : "00 : 00".ToCharArray();
        remaining = duration;
        running = startOnStart;
        Display(true);
    }

    void Update()
    {
        if (!running || finished) return;

        remaining -= Time.deltaTime;

        if (remaining <= 0f)
        {
            remaining = 0f;
            Display(true);
            Finish();
            return;
        }

        Display(false);
    }

    void Finish()
    {
        finished = true;
        running = false;
        onTimeUp?.Invoke();
        if (endsGame) GameFlow.ShowTimeUp();
    }

    // ------------------------------------------------------------ public controls

    public void StartTimer() { running = true; }
    public void PauseTimer() { running = false; }

    public void Restart(float newDuration = -1f)
    {
        if (newDuration > 0f) duration = newDuration;
        remaining = duration;
        finished = false;
        running = true;
        Display(true);
    }

    /// <summary>Bonus time pickups: AddTime(5f). Negative values take time away.</summary>
    public void AddTime(float seconds)
    {
        if (finished) return;
        remaining = Mathf.Max(0.01f, remaining + seconds);
    }

    // ------------------------------------------------------------ display (no allocations)

    void Display(bool force)
    {
        if (buf == null) return;

        int total = Mathf.CeilToInt(remaining * 100f);               // hundredths of a second
        if (!showHundredths) total = Mathf.CeilToInt(remaining) * 100;

        if (!force && total == lastShown) return;
        lastShown = total;

        int minutes = Mathf.Min(99, total / 6000);
        int seconds = (total / 100) % 60;
        int hundredths = total % 100;

        Put(0, minutes);
        Put(5, seconds);
        if (showHundredths) Put(10, hundredths);

        text.SetCharArray(buf, 0, buf.Length);

        text.color = (remaining <= warnAtSeconds) ? warnColor : baseColor;
    }

    void Put(int index, int value)
    {
        buf[index] = (char)('0' + value / 10);
        buf[index + 1] = (char)('0' + value % 10);
    }
}
