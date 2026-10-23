// SprayPainter.cs
// Paints onto REAL surfaces scanned by LiDAR, like a brush/marker.
// • Aims through your cursor image (or a fixed screen position if there is none).
// • While the screen is touched and the surface is within range, it draws a
//   continuous line of paint dots (all the same size) on the real surface.
// • With the Eraser selected, touching removes paint under the cursor.

using System.Collections.Generic;   // List and Dictionary
using UnityEngine;

public class SprayPainter : MonoBehaviour
{
    // ── References (drag these in) ──────────────────────────────────────────
    [Header("References (drag these in)")]
    [Tooltip("The AR camera: XR Origin > Camera Offset > Main Camera")]
    public Camera arCamera;

    [Tooltip("A transparent URP/Unlit material (white). Used for all paint")]
    public Material paintMaterial;

    [Tooltip("Your cursor image (a UI Image). Optional")]
    public RectTransform cursor;

    // ── Cursor ───────────────────────────────────────────────────────────────
    [Header("Cursor")]
    [Tooltip("ON = aim wherever you place your cursor image. OFF = use Cursor Screen Position below")]
    public bool aimAtCursorImage = true;

    [Tooltip("Only used without a cursor image (or with the option above OFF). X: 0 left … 1 right, Y: 0 bottom … 1 top")]
    public Vector2 cursorScreenPosition = new Vector2(0.5f, 0.67f);

    // ── Paint ────────────────────────────────────────────────────────────────
    [Header("Paint")]
    [Tooltip("Colour used at the start (colour buttons change it)")]
    public Color paintColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("Brush width in metres — the same at every distance")]
    public float brushSize = 0.03f;

    [Tooltip("0 = hard edge, 1 = very soft edge")]
    [Range(0f, 1f)] public float brushSoftness = 0.15f;

    [Tooltip("Paint only reaches surfaces closer than this (metres)")]
    public float paintRange = 1.5f;

    [Tooltip("Gap between dots in a line, as a part of the brush width (smaller = smoother line)")]
    public float dotSpacing = 0.3f;

    [Tooltip("If the aim point jumps farther than this in one frame, the line is not connected (metres)")]
    public float maxJump = 0.25f;

    [Tooltip("Safety limit: above this many dots, the oldest are removed")]
    public int maxDots = 6000;

    // ── Eraser ───────────────────────────────────────────────────────────────
    [Header("Eraser")]
    [Tooltip("Eraser width in metres")]
    public float eraserSize = 0.08f;

    // ── Values other scripts can read (haptics later) ───────────────────────
    [HideInInspector] public bool isSpraying;           // finger on screen (set by SprayButton)
    public bool IsEraser { get; private set; }          // true = eraser selected
    public bool HasTarget { get; private set; }         // cursor points at a scanned surface
    public bool InRange { get; private set; }           // …and it is close enough
    public float TargetDistance { get; private set; }   // metres to the surface (-1 = none)
    public bool IsPainting => isSpraying && InRange;    // painting/erasing right now

    // ── Internal ─────────────────────────────────────────────────────────────
    Texture2D dotTexture;                                                  // the round dot image
    readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>(); // one material per colour
    Transform paintRoot;                                                   // parent of all dots
    readonly List<Transform> dots = new List<Transform>();                 // all dots, oldest first
    bool hasLastPoint;                                                     // is a line in progress?
    Vector3 lastPoint;                                                     // where the line got to


    // Start() runs ONCE, when the app starts.
    void Start()
    {
        if (arCamera == null) arCamera = Camera.main;
        dotTexture = MakeDot(64, brushSoftness);
        paintRoot = new GameObject("Paint").transform;

        if (!aimAtCursorImage) PlaceCursor();   // don't move YOUR cursor if you placed it yourself
    }


    // Moves the cursor image to Cursor Screen Position (only when aimAtCursorImage is OFF)
    void PlaceCursor()
    {
        if (cursor == null) return;
        cursor.anchorMin = cursorScreenPosition;
        cursor.anchorMax = cursorScreenPosition;
        cursor.anchoredPosition = Vector2.zero;
    }


    // Where on the screen we aim (0–1). Follows your cursor image if you placed one.
    Vector2 AimPoint()
    {
        if (aimAtCursorImage && cursor != null)
        {
            // Your cursor's position in screen pixels…
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, cursor.position);
            // …turned into 0–1 (divide by the screen size)
            return new Vector2(screen.x / Screen.width, screen.y / Screen.height);
        }
        return cursorScreenPosition;   // no image → use the number
    }


    // Update() runs EVERY FRAME (about 60 times per second).
    void Update()
    {
        // 1. AIM: a ray from the camera through the cursor's point on the screen
        Vector2 aim = AimPoint();
        Ray ray = arCamera.ViewportPointToRay(new Vector3(aim.x, aim.y, 0f));
        HasTarget = Physics.Raycast(ray, out RaycastHit hit, 20f);
        TargetDistance = HasTarget ? hit.distance : -1f;
        InRange = HasTarget && hit.distance <= paintRange;

        // 2. Nothing to do if the finger is up or the surface is too far
        if (!isSpraying || !InRange)
        {
            hasLastPoint = false;   // the next touch starts a new line
            return;
        }

        // 3. First point of a line, or the aim jumped far → just one dot here
        if (!hasLastPoint || Vector3.Distance(lastPoint, hit.point) > maxJump)
        {
            ApplyAt(hit.point, hit.normal);
            lastPoint = hit.point;
            hasLastPoint = true;
            return;
        }

        // 4. Continue the line: fill the gap from the last point to here with evenly spaced dots
        float step = (IsEraser ? eraserSize : brushSize) * dotSpacing;
        float length = Vector3.Distance(lastPoint, hit.point);
        if (length < step) return;                                // hasn't moved enough yet

        int count = Mathf.FloorToInt(length / step);
        Vector3 direction = (hit.point - lastPoint) / length;
        for (int i = 1; i <= count; i++)
        {
            ApplyAt(lastPoint + direction * step * i, hit.normal);
        }
        lastPoint += direction * step * count;
    }


    // Paint or erase at one point, depending on the selected tool
    void ApplyAt(Vector3 point, Vector3 normal)
    {
        if (IsEraser) EraseAt(point);
        else PaintAt(point, normal);
    }


    // Puts one paint dot flat on the surface
    void PaintAt(Vector3 point, Vector3 normal)
    {
        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(dot.GetComponent<Collider>());   // the aim must not hit the paint itself
        dot.name = "PaintDot";

        Transform t = dot.transform;
        t.SetParent(paintRoot, true);
        t.position = point + normal * 0.002f;             // 2 mm above the surface
        t.rotation = Quaternion.LookRotation(-normal)     // lie flat on the surface
                   * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        t.localScale = Vector3.one * brushSize;            // same size at every distance

        dot.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(paintColor);

        dots.Add(t);
        if (dots.Count > maxDots)                          // too many → remove the oldest
        {
            Destroy(dots[0].gameObject);
            dots.RemoveAt(0);
        }
    }


    // Removes every dot that touches the eraser circle
    void EraseAt(Vector3 point)
    {
        float radius = (eraserSize + brushSize) * 0.5f;
        float radiusSquared = radius * radius;
        for (int i = dots.Count - 1; i >= 0; i--)          // backwards, because we remove while looping
        {
            if ((dots[i].position - point).sqrMagnitude <= radiusSquared)
            {
                Destroy(dots[i].gameObject);
                dots.RemoveAt(i);
            }
        }
    }


    // One material per colour (made the first time a colour is used)
    Material MaterialFor(Color c)
    {
        if (!materials.TryGetValue(c, out Material m))
        {
            m = new Material(paintMaterial);
            m.mainTexture = dotTexture;
            m.color = c;
            materials[c] = m;
        }
        return m;
    }


    // ── Called by the buttons ────────────────────────────────────────────────
    public void SelectColor(Color c)
    {
        paintColor = c;
        IsEraser = false;
    }

    public void SelectEraser()
    {
        IsEraser = true;
    }

    public void ClearPaint()
    {
        foreach (Transform t in dots) Destroy(t.gameObject);
        dots.Clear();
    }


    // Makes a round white dot. softness 0 = hard edge, 1 = fades from the centre.
    static Texture2D MakeDot(int size, float softness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;   // 0 centre … 1 edge
                float a = softness <= 0.01f
                        ? (d <= 1f ? 1f : 0f)
                        : Mathf.Clamp01((1f - d) / softness);
                a = a * a * (3f - 2f * a);                                           // smooth
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }
}