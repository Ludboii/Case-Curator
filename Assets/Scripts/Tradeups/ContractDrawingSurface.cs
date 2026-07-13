using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class ContractDrawingSurface : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    [Header("Drawing")]
    [SerializeField, Min(1)] private int brushRadius = 4;
    [SerializeField] private Color brushColor = Color.blue;
    [SerializeField] private Color clearColor = new Color(0f, 0f, 0f, 0f);

    private RawImage rawImage;
    private RectTransform rectTransform;
    private Texture2D drawingTexture;
    private Vector2Int previousPixel;
    private bool isDrawing;
    private bool drawingEnabled = true;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
        rectTransform = transform as RectTransform;

        rawImage.raycastTarget = true;
        CreateTexture();
    }

    private void OnEnable()
    {
        if (drawingTexture == null)
            CreateTexture();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled || rectTransform == null)
            return;

        int width = Mathf.Max(1, Mathf.RoundToInt(rectTransform.rect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(rectTransform.rect.height));

        if (drawingTexture == null ||
            drawingTexture.width != width ||
            drawingTexture.height != height)
        {
            CreateTexture();
        }
    }

    public void SetDrawingEnabled(bool enabled)
    {
        drawingEnabled = enabled;
        isDrawing = false;
    }

    public void ClearDrawing()
    {
        if (drawingTexture == null)
            CreateTexture();

        Color[] pixels = new Color[
            drawingTexture.width * drawingTexture.height];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clearColor;

        drawingTexture.SetPixels(pixels);
        drawingTexture.Apply(false, false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!drawingEnabled || !TryGetPixel(eventData, out Vector2Int pixel))
            return;

        isDrawing = true;
        previousPixel = pixel;
        DrawBrush(pixel);
        drawingTexture.Apply(false, false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!drawingEnabled || !isDrawing)
            return;

        if (!TryGetPixel(eventData, out Vector2Int pixel))
            return;

        DrawLine(previousPixel, pixel);
        previousPixel = pixel;
        drawingTexture.Apply(false, false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDrawing = false;
    }

    private void CreateTexture()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (rawImage == null)
            rawImage = GetComponent<RawImage>();

        int width = Mathf.Max(
            1,
            Mathf.RoundToInt(rectTransform.rect.width));

        int height = Mathf.Max(
            1,
            Mathf.RoundToInt(rectTransform.rect.height));

        if (drawingTexture != null)
            Destroy(drawingTexture);

        drawingTexture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false);

        drawingTexture.name = "TradeupContractDrawing";
        drawingTexture.filterMode = FilterMode.Bilinear;
        drawingTexture.wrapMode = TextureWrapMode.Clamp;

        rawImage.texture = drawingTexture;
        rawImage.color = Color.white;

        ClearDrawing();
    }

    private bool TryGetPixel(
        PointerEventData eventData,
        out Vector2Int pixel)
    {
        pixel = default;

        if (rectTransform == null || drawingTexture == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = rectTransform.rect;

        float normalizedX = Mathf.InverseLerp(
            rect.xMin,
            rect.xMax,
            localPoint.x);

        float normalizedY = Mathf.InverseLerp(
            rect.yMin,
            rect.yMax,
            localPoint.y);

        if (normalizedX < 0f || normalizedX > 1f ||
            normalizedY < 0f || normalizedY > 1f)
        {
            return false;
        }

        pixel = new Vector2Int(
            Mathf.Clamp(
                Mathf.RoundToInt(normalizedX * (drawingTexture.width - 1)),
                0,
                drawingTexture.width - 1),
            Mathf.Clamp(
                Mathf.RoundToInt(normalizedY * (drawingTexture.height - 1)),
                0,
                drawingTexture.height - 1));

        return true;
    }

    private void DrawLine(Vector2Int from, Vector2Int to)
    {
        int deltaX = Mathf.Abs(to.x - from.x);
        int deltaY = Mathf.Abs(to.y - from.y);
        int steps = Mathf.Max(deltaX, deltaY);

        if (steps <= 0)
        {
            DrawBrush(to);
            return;
        }

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;

            DrawBrush(
                new Vector2Int(
                    Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)),
                    Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t))));
        }
    }

    private void DrawBrush(Vector2Int center)
    {
        int radiusSquared = brushRadius * brushRadius;

        for (int y = -brushRadius; y <= brushRadius; y++)
        {
            for (int x = -brushRadius; x <= brushRadius; x++)
            {
                if (x * x + y * y > radiusSquared)
                    continue;

                int pixelX = center.x + x;
                int pixelY = center.y + y;

                if (pixelX < 0 || pixelX >= drawingTexture.width ||
                    pixelY < 0 || pixelY >= drawingTexture.height)
                {
                    continue;
                }

                drawingTexture.SetPixel(pixelX, pixelY, brushColor);
            }
        }
    }
}
