using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;

// Paints round dots on any surface with a collider (LiDAR mesh or test cube).
// Hold a finger anywhere on screen (or the left mouse button in the Editor) to paint.
// Touches on UI buttons are ignored, so no full-screen touch area is needed.
public class SprayPainter : MonoBehaviour
{
    [Header("References")]
    public Camera arCamera;          // leave empty = uses Main Camera
    public Material paintMaterial;   // URP/Unlit, Surface Type: Transparent

    [Header("Brush")]
    public Color paintColor = Color.red;
    public float brushSize = 0.03f;      // dot diameter in metres (always the same)
    public float eraserSize = 0.08f;     // eraser diameter in metres
    public float maxDistance = 1.0f;     // painting range in metres (CHANGED: was 1.5) — the haptic follows this automatically
    public float dotSpacing = 0.008f;    // gap between dots in a stroke
    public float maxStrokeGap = 0.3f;    // bigger jump than this = new stroke (no line through the air)
    public float surfaceOffset = 0.005f; // how far paint floats in front of the surface
    public bool isEraser = false;

    [Header("Haptics")]
    public float proximityLookDistance = 3f;   // how far we look for a surface for the haptic distance

    [Header("Data logging")]
    public DataLogger logger;                  // NEW: drag the DataLogger here (empty = no movement logging)

    [Header("Aim point on screen (0-1). Match your cursor.")]
    // x 0.5 = centre, y 0.7 = upper third. Cursor Pos Y = (y - 0.5) * 1920 = 384
    public Vector2 aimViewport = new Vector2(0.5f, 0.7f);

    readonly List<GameObject> dots = new List<GameObject>();

    // NEW: what each dot is (same order as 'dots'), for final_paint.csv
    public struct DotRecord
    {
        public Vector3 position, normal;
        public Color color;
        public float size;
        public int strokeId;
        public float paintedAt;   // Time.realtimeSinceStartup when painted
    }
    readonly List<DotRecord> dotRecords = new List<DotRecord>();
    readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    Transform dotParent;
    Texture2D dotTexture;
    Vector3? lastPoint;
    bool wasPressing;   // was the finger down in the last frame?
    int strokeId;       // NEW: +1 for every new stroke → newer strokes are drawn ON TOP
    float aimDistance = -1f;

    void Start()
    {
        if (arCamera == null) arCamera = Camera.main;
        dotParent = new GameObject("PaintDots").transform;
        dotTexture = MakeRoundTexture(64);
        HapticsBridge.Start();
    }

    void Update()
    {
        // NEW: one aim ray per frame, used for BOTH the haptic distance and painting
        Ray ray = arCamera.ViewportPointToRay(aimViewport);
        bool hasSurface = Physics.Raycast(ray, out RaycastHit hit, proximityLookDistance);
        bool canPaint = hasSurface && hit.distance <= maxDistance;   // same check as painting below
        aimDistance = hasSurface ? hit.distance : -1f;
        SendHapticDistance(hasSurface, canPaint, hit);

        bool pressing = IsPressing();

        if (pressing && !wasPressing) HapticsBridge.Press();     // finger just went down
        if (!pressing && wasPressing) HapticsBridge.Release();   // finger just came up
        wasPressing = pressing;

        if (logger != null) logger.SetState(pressing, canPaint, aimDistance);   // NEW: for movement.csv

        if (!pressing) { lastPoint = null; return; }

        if (!hasSurface || hit.distance > maxDistance)
        {
            status = hasSurface
                ? $"Surface found but too far: {hit.distance:F2} m (max {maxDistance} m)"
                : "Touch OK, but the aim ray hits NO surface";
            lastPoint = null;
            return;
        }
        status = $"Painting on '{hit.collider.name}' at {hit.distance:F2} m";

        if (isEraser) { Erase(hit.point); return; }

        Vector3 p = hit.point + hit.normal * surfaceOffset; // lift slightly off the surface

        if (lastPoint == null)
        {
            strokeId++;                 // NEW: a new stroke starts → it goes on top of all older ones
            PlaceDot(p, hit.normal);
        }
        else
        {
            float dist = Vector3.Distance(lastPoint.Value, p);
            if (dist < dotSpacing) return;

            if (dist > maxStrokeGap)
            {
                PlaceDot(p, hit.normal); // big jump (e.g. to another surface): don't draw a line through the air
            }
            else
            {
                int steps = Mathf.CeilToInt(dist / dotSpacing);
                for (int i = 1; i <= steps; i++)
                    PlaceDot(Vector3.Lerp(lastPoint.Value, p, (float)i / steps), hit.normal);
            }
        }
        lastPoint = p;
    }

    // NEW: when the session ends the painter is switched off → close the nozzle if a finger is still down.
    void OnDisable()
    {
        if (wasPressing) { HapticsBridge.Release(); wasPressing = false; }
        lastPoint = null;
    }

    // Tell the haptics plugin (1) CAN I PAINT here, and (2) how far the surface is.
    // CAN PAINT uses the SAME check as painting below, so haptic and paint always agree.
    // No surface = "far" (proximityLookDistance). AR tracking lost = -1 (plugin keeps the last value).
    void SendHapticDistance(bool hasSurface, bool canPaint, RaycastHit hit)
    {
        HapticsBridge.SetCanPaint(canPaint);

        if (ARSession.state != ARSessionState.SessionTracking)
            HapticsBridge.SetDistance(-1f);
        else
            HapticsBridge.SetDistance(hasSurface ? hit.distance : proximityLookDistance);
    }

    bool IsPressing()
    {
        bool pressed =
            (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed);
        if (!pressed)
        {
            status = Touchscreen.current == null && Mouse.current == null
                ? "No touchscreen found (check Active Input Handling)"
                : "Not touching";
            return false;
        }

        // Finger is on a UI button -> don't paint
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            status = "Touch is on UI -> not painting";
            return false;
        }
        return true;
    }

    // ---------- Debug text on screen (turn off with Show Debug) ----------

    [Header("Debug")]
    public bool showDebug = true;
    string status = "Starting...";
    int meshCount;
    float nextMeshCheck;

    void OnGUI()
    {
        if (!showDebug) return;

        if (Time.time > nextMeshCheck)
        {
            meshCount = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Length;
            nextMeshCheck = Time.time + 1f;
        }

        var style = new GUIStyle(GUI.skin.label) { fontSize = 40 };
        style.normal.textColor = Color.yellow;
        // CHANGED: moved down to 45 % of the screen, because the haptics debugger is now at the top
        GUI.Label(new Rect(40, Screen.height * 0.45f, Screen.width - 80, 400),
            $"{status}\nScanned mesh pieces: {meshCount}\nDots: {dots.Count}", style);
    }

    void PlaceDot(Vector3 pos, Vector3 normal)
    {
        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(dot.GetComponent<Collider>()); // dots must not block the aim ray
        dot.name = "Dot";
        dot.transform.SetParent(dotParent);
        dot.transform.position = pos;
        dot.transform.rotation = Quaternion.LookRotation(-normal); // face out of the surface
        dot.transform.localScale = Vector3.one * brushSize;
        var rend = dot.GetComponent<Renderer>();
        rend.sharedMaterial = GetMaterial(paintColor);
        rend.sortingOrder = Mathf.Min(strokeId, 32767);   // NEW: newer stroke = higher number = drawn on top
        dots.Add(dot);
        dotRecords.Add(new DotRecord {                     // NEW: remember it for final_paint.csv
            position = pos, normal = normal, color = paintColor, size = brushSize,
            strokeId = strokeId, paintedAt = Time.realtimeSinceStartup });
    }

    /// NEW: copy of every dot that is still there right now (the final painting).
    public List<DotRecord> GetPaintSnapshot() => new List<DotRecord>(dotRecords);

    void Erase(Vector3 point)
    {
        float r = eraserSize * 0.5f;
        for (int i = dots.Count - 1; i >= 0; i--)
        {
            if ((dots[i].transform.position - point).sqrMagnitude < r * r)
            {
                Destroy(dots[i]);
                dots.RemoveAt(i);
                dotRecords.RemoveAt(i);   // NEW: keep the records in sync
            }
        }
    }

    Material GetMaterial(Color c)
    {
        if (!materials.TryGetValue(c, out Material m))
        {
            m = new Material(paintMaterial);
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", dotTexture);
            // NEW: draw dots in the transparent pass without depth writing, so the
            // drawing ORDER decides what is on top (newest stroke wins, see sortingOrder).
            // Real walls still hide the paint (the occlusion mesh is drawn before).
            m.renderQueue = 3000;
            m.SetFloat("_ZWrite", 0f);
            materials[c] = m;
        }
        return m;
    }

    Texture2D MakeRoundTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01((1f - d) * 8f); // solid circle with a soft edge
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return tex;
    }

    // ---------- Called from UI Buttons (OnClick) ----------

    // Type a hex colour in the button's OnClick box, e.g. #FF0000
    public void SetColorHex(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color c)) paintColor = c;
        isEraser = false;
    }

    public void SetEraser() { isEraser = true; }

    // Connect a UI Slider here (On Value Changed -> Dynamic float). Value in metres.
    public void SetBrushSize(float size)
    {
        brushSize = size;
        eraserSize = size * 2.5f; // eraser grows with the brush
    }

    public void ClearAll()
    {
        foreach (var d in dots) Destroy(d);
        dots.Clear();
        dotRecords.Clear();
        strokeId = 0;      // next participant starts from stroke 1
    }
}