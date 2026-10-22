
using UnityEngine;
using UnityEngine.UI;

public class ScrollingBackground : MonoBehaviour
{
    [SerializeField] private RawImage background;
    [SerializeField] private float scrollSpeed = 0.1f;

    private void Awake()
    {
        if (background == null)
            background = GetComponent<RawImage>();
    }

    private void Update()
    {
        if (background == null) return;

        Rect uv = background.uvRect;
        uv.y += scrollSpeed * Time.deltaTime;
        background.uvRect = uv;
    }
}
