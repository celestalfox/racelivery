using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class fps : MonoBehaviour

{
    
    public TMP_Text uiText;

    private float deltaTime = 0.0f;

    void Update()
    {
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;
        uiText.text = Mathf.Ceil(fps).ToString() + " FPS";

    }
}