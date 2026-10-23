using UnityEngine;
using UnityEngine.UI;

// NEW: put this on the BRUSH button (e.g. Btn_Brush in the Toolbar).
//   • Clicking it chooses the brush tool (= brush haptic) AND its own paint colour (black by default).
//   • Clicking a colour button or the eraser goes back to the spray can.
//   • The button is bigger, fully visible and outlined while the brush is in use.
// No OnClick setup needed.
[RequireComponent(typeof(Button))]
public class ToolSelectButton : MonoBehaviour
{
    [Tooltip("Empty = found automatically.")]
    public SprayPainter painter;

    [Tooltip("Which tool this button chooses.")]
    public SprayPainter.PaintTool tool = SprayPainter.PaintTool.Brush;

    [Tooltip("Also choose this paint colour when the button is clicked.")]
    public bool setColor = true;
    [Tooltip("Paint colour of this tool, e.g. #000000 = black.")]
    public string colorHex = "#000000";

    [Header("How the SELECTED button looks")]
    public float selectedScale = 1.2f;
    [Range(0f, 1f)] public float otherAlpha = 0.55f;
    public bool selectedOutline = true;
    public Color outlineColor = Color.white;
    public float outlineSize = 6f;

    Graphic graphic;
    Outline outline;
    Vector3 baseScale;

    void Awake()
    {
        if (painter == null) painter = FindFirstObjectByType<SprayPainter>();
        graphic = GetComponent<Graphic>();
        baseScale = transform.localScale;
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

    public void Select()
    {
        if (painter == null) return;
        if (setColor) painter.SetColorHex(colorHex);   // also turns the eraser off
        painter.tool = tool;
    }

    void LateUpdate()
    {
        bool selected = painter != null && painter.tool == tool && !painter.isEraser;
        transform.localScale = baseScale * (selected ? selectedScale : 1f);
        if (graphic != null)
        {
            Color c = graphic.color;
            c.a = selected ? 1f : otherAlpha;
            graphic.color = c;
        }
        if (outline != null) outline.enabled = selected;
    }
}