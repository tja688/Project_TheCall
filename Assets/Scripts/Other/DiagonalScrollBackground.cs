using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scrolls a tiled RawImage on a constant screen-pixel diagonal.
/// The texture must use Repeat wrap so the motion loops without a seam.
/// </summary>
public sealed class DiagonalScrollBackground : MonoBehaviour
{
    [SerializeField] RawImage target;
    [SerializeField] float texelScreenPixels = 6f;
    [SerializeField] float pixelsPerSecond = 36f;

    void Awake()
    {
        if (target == null)
            target = GetComponent<RawImage>();
    }

    void Update()
    {
        if (target == null || target.texture == null)
            return;

        var rect = target.rectTransform.rect;
        if (rect.width < 1f || rect.height < 1f)
            return;

        var uv = target.uvRect;
        float tileWidth = target.texture.width * texelScreenPixels;
        float tileHeight = target.texture.height * texelScreenPixels;
        uv.width = rect.width / tileWidth;
        uv.height = rect.height / tileHeight;

        float step = pixelsPerSecond * Time.deltaTime;
        uv.x = Mathf.Repeat(uv.x + step * uv.width / rect.width, 1f);
        uv.y = Mathf.Repeat(uv.y + step * uv.height / rect.height, 1f);
        target.uvRect = uv;
    }
}
