using UnityEngine;
using UnityEngine.UI;

// Put this on EACH colour button and on the eraser button.
//   • Clicking the button selects its colour (or the eraser) — no OnClick setup needed.
//   • The SELECTED button is shown bigger, fully visible and with an outline;
//     the other buttons are a bit transparent. Updates by itself every frame,
//     so it is always correct, whatever changed the colour.
[RequireComponent(typeof(Button))]
public class PaintToolButton : MonoBehaviour
{
    [Tooltip("Empty = found automatically.")]
    public SprayPainter painter;

    [Tooltip("Paint colour of this button, e.g. #FF0000. Ignored when Is Eraser is ticked.")]
    public string colorHex = "#FF0000";
    [Tooltip("Tick for the eraser button.")]
    public bool isEraser = false;
    [Tooltip("NEW: clicking this button switches back to the SPRAY CAN (leaves the brush).")]
    public bool switchToSpray = true;

    [Header("How the SELECTED button looks")]
    [Tooltip("Size of the selected button (1 = normal, 1.2 = 20 % bigger).")]
    public float selectedScale = 1.2f;
    [Tooltip("Transparency of the NOT selected buttons (1 = fully visible, 0.5 = half).")]
    [Range(0f, 1f)] public float otherAlpha = 0.55f;
    [Tooltip("Draw an outline around the selected button.")]
    public bool selectedOutline = true;
    public Color outlineColor = Color.white;
    public float outlineSize = 6f;

    Graphic graphic;
    Outline outline;
    Vector3 baseScale;
    Color myColor;
    bool hasColor;

    void Awake()
    {
        if (painter == null) painter = FindFirstObjectByType<SprayPainter>();
        graphic = GetComponent<Graphic>();          // the button's Image (your spray can)
        baseScale = transform.localScale;
        hasColor = ColorUtility.TryParseHtmlString(colorHex, out myColor);
        if (!isEraser && !hasColor)
            Debug.LogWarning($"[PaintToolButton] '{name}': '{colorHex}' is not a valid colour (use e.g. #FF0000)");

        GetComponent<Button>().onClick.AddListener(Select);

        if (selectedOutline)
        {
            outline = gameObject.GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(outlineSize, -outlineSize);
            outline.enabled = false;
        }
    }

    /// Clicking the button.
    public void Select()
    {
        if (painter == null) return;
        if (switchToSpray) painter.tool = SprayPainter.PaintTool.Spray;   // NEW
        if (isEraser) painter.SetEraser();
        else painter.SetColorHex(colorHex);
    }

    bool IsSelected()
    {
        if (painter == null) return false;
        if (isEraser) return painter.isEraser;
        return !painter.isEraser && painter.tool == SprayPainter.PaintTool.Spray   // NEW: not while brushing
               && hasColor && Same(painter.paintColor, myColor);
    }

    void LateUpdate()
    {
        bool selected = IsSelected();

        transform.localScale = baseScale * (selected ? selectedScale : 1f);

        if (graphic != null)
        {
            Color c = graphic.color;
            c.a = selected ? 1f : otherAlpha;
            graphic.color = c;
        }
        if (outline != null) outline.enabled = selected;
    }

    static bool Same(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
}