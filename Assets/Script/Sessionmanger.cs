using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

// Study session flow:
//   1. App opens → BLACK session page: Participant ID, Session time (minutes) and
//      the CONDITION:  A = haptics completely OFF,  H = haptics ON.
//   2. "Start" → the page disappears, painting + haptics + data logging begin.
//      Data folder = Sessions/<Participant ID>.
//   3. When the time is over → everything is saved, haptics stop, the painting is
//      cleared, and the SESSION PAGE comes back (ready for the next participant).
//      A green line on the page says which session was saved.
//
// Put this on any GameObject (e.g. a new empty "Session" object).
// It builds its own screen in code — no Canvas setup needed.
public class SessionManager : MonoBehaviour
{
    [Header("References (empty = found automatically)")]
    public SprayPainter painter;
    public DataLogger logger;

    [Header("Defaults shown in the boxes")]
    public string defaultParticipantId = "";
    public float defaultMinutes = 10f;
    [Tooltip("Pre-selected condition: \"A\", \"H\", or empty = the experimenter must choose.")]
    public string defaultCondition = "";

    [Header("End of session")]
    [Tooltip("Remove the painting when a session ends, so the next participant starts with clean walls.")]
    public bool clearPaintingAtEnd = true;
    [Tooltip("Empty the Participant ID box for the next participant (the minutes stay).")]
    public bool clearIdAtEnd = true;
    [Tooltip("Un-select the condition for the next session, so it is always chosen on purpose.")]
    public bool clearConditionAtEnd = true;
    [Tooltip("Show a small countdown in the corner during the session.")]
    public bool showCountdown = false;

    // ── state ──
    bool running;
    float endTime;
    string condition = "";          // "A" or "H"


    // ── UI ──
    Canvas canvas;
    GameObject startPanel;
    InputField idInput, minutesInput;
    Text errorText, savedText, countdownText;
    Image btnAImg, btnHImg;
    Text btnALabel, btnHLabel;
    Font font;

    // ───────────────────────── setup ─────────────────────────

    void Awake()
    {
        if (painter == null) painter = FindFirstObjectByType<SprayPainter>();
        if (logger == null) logger = FindFirstObjectByType<DataLogger>();

        // No painting / haptics until the session starts.
        if (painter != null) painter.enabled = false;

        font = LoadFont();
        BuildUI();
        startPanel.SetActive(true);
    }

    void Update()
    {
        if (!running) return;

        float left = endTime - Time.realtimeSinceStartup;
        if (showCountdown && countdownText != null)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(left));
            countdownText.text = $"{s / 60}:{s % 60:00}";
        }
        if (left <= 0f) EndSession();
    }

    // ───────────────────────── start / end ─────────────────────────

    void OnStartPressed()
    {
        string id = Sanitize(idInput.text);
        if (id.Length == 0) { errorText.text = "Please enter a Participant ID."; return; }

        string m = minutesInput.text.Trim().Replace(',', '.');
        if (!float.TryParse(m, NumberStyles.Float, CultureInfo.InvariantCulture, out float minutes) || minutes <= 0f)
        {
            errorText.text = "Please enter the session time in minutes (e.g. 10).";
            return;
        }

        if (condition != "A" && condition != "H")
        {
            errorText.text = "Please choose a condition: A (no haptics) or H (haptics).";
            return;
        }

        errorText.text = "";
        savedText.text = "";
        startPanel.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(showCountdown);

        if (logger != null) logger.BeginSession(id, minutes, condition);
        // Condition A = motor completely off, H = on. Set BEFORE the haptics start.
        HapticsBridge.SetParam("kHapticsEnabled", condition == "H" ? 1f : 0f);
        HapticsBridge.Start();                         // haptics system on (again, for the 2nd, 3rd … session)
        if (painter != null) painter.enabled = true;   // painting starts now

        endTime = Time.realtimeSinceStartup + minutes * 60f;
        running = true;
        Debug.Log($"[Session] Started: {id}, condition {condition}, {minutes} min");
    }

    void EndSession()
    {
        running = false;

        if (painter != null) painter.enabled = false;  // also releases the nozzle (OnDisable)
        HapticsBridge.Stop();                          // ball + motion + engine off
        if (logger != null) logger.EndSession();       // saves everything (BEFORE clearing!)
        if (clearPaintingAtEnd && painter != null) painter.ClearAll();

        // back to the session page
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        string saved = logger != null && logger.SessionFolder != null
            ? System.IO.Path.GetFileName(logger.SessionFolder) : "";
        savedText.text = saved.Length > 0 ? "Session " + saved + " finished and saved." : "Session finished.";
        if (clearIdAtEnd) idInput.text = "";
        if (clearConditionAtEnd) SelectCondition("");
        startPanel.SetActive(true);
        Debug.Log("[Session] Ended → back to the session page");
    }

    // Highlights the chosen condition button (white = selected, dark = not selected).
    void SelectCondition(string c)
    {
        condition = c;
        SetChoice(btnAImg, btnALabel, c == "A");
        SetChoice(btnHImg, btnHLabel, c == "H");
    }

    static void SetChoice(Image img, Text label, bool selected)
    {
        if (img == null) return;
        img.color = selected ? Color.white : new Color(1, 1, 1, 0.12f);
        label.color = selected ? Color.black : Color.white;
    }

    // Only letters, digits, - and _ (safe as a folder name).
    static string Sanitize(string s) => Regex.Replace((s ?? "").Trim(), @"[^A-Za-z0-9_\-]", "");

    // ───────────────────────── UI built in code ─────────────────────────

    void BuildUI()
    {
        var cgo = new GameObject("SessionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // above everything else
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // ── start screen ──
        startPanel = MakePanel("StartPanel");
        MakeText(startPanel.transform, "Spray Paint Study", 72, Color.white, new Vector2(0, 640), new Vector2(1000, 120), FontStyle.Bold);

        MakeText(startPanel.transform, "Participant ID", 44, new Color(1, 1, 1, 0.7f), new Vector2(0, 440), new Vector2(800, 70));
        idInput = MakeInput(startPanel.transform, "e.g. P01", InputField.ContentType.Alphanumeric, new Vector2(0, 350));
        idInput.text = defaultParticipantId;

        MakeText(startPanel.transform, "Session time (minutes)", 44, new Color(1, 1, 1, 0.7f), new Vector2(0, 200), new Vector2(800, 70));
        minutesInput = MakeInput(startPanel.transform, "e.g. 10", InputField.ContentType.DecimalNumber, new Vector2(0, 110));
        minutesInput.text = defaultMinutes.ToString(CultureInfo.InvariantCulture);

        // NEW: condition A / H
        MakeText(startPanel.transform, "Condition", 44, new Color(1, 1, 1, 0.7f), new Vector2(0, -40), new Vector2(800, 70));
        MakeChoiceButton(startPanel.transform, "A\n<size=30>no haptics</size>", new Vector2(-205, -150),
                         () => SelectCondition("A"), out btnAImg, out btnALabel);
        MakeChoiceButton(startPanel.transform, "H\n<size=30>haptics</size>", new Vector2(205, -150),
                         () => SelectCondition("H"), out btnHImg, out btnHLabel);
        SelectCondition(defaultCondition == "A" || defaultCondition == "H" ? defaultCondition : "");

        MakeButton(startPanel.transform, "Start", new Vector2(0, -360), OnStartPressed);
        errorText = MakeText(startPanel.transform, "", 38, new Color(1f, 0.45f, 0.45f), new Vector2(0, -500), new Vector2(950, 120));
        savedText = MakeText(startPanel.transform, "", 38, new Color(0.45f, 1f, 0.55f), new Vector2(0, -610), new Vector2(950, 120));

        // ── optional countdown (top-right) ──
        countdownText = MakeText(canvas.transform, "", 40, new Color(1, 1, 1, 0.8f), Vector2.zero, new Vector2(260, 70));
        var crt = countdownText.rectTransform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-30, -120);
        countdownText.alignment = TextAnchor.UpperRight;
        countdownText.gameObject.SetActive(false);
    }

    GameObject MakePanel(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;   // blocks touches → no painting behind it
        return go;
    }

    Text MakeText(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 boxSize,
                  FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = boxSize;
        rt.anchoredPosition = pos;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    InputField MakeInput(Transform parent, string placeholder, InputField.ContentType type, Vector2 pos)
    {
        var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800, 120);
        rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.12f);

        Text text = MakeText(go.transform, "", 54, Color.white, Vector2.zero, new Vector2(760, 110));
        text.supportRichText = false;                 // required by InputField
        Text ph = MakeText(go.transform, placeholder, 54, new Color(1, 1, 1, 0.3f), Vector2.zero, new Vector2(760, 110));
        ph.fontStyle = FontStyle.Italic;

        var input = go.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = ph;
        input.contentType = type;
        input.characterLimit = 32;
        return input;
    }

    void MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(500, 140);
        rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = Color.white;
        go.GetComponent<Button>().onClick.AddListener(onClick);
        MakeText(go.transform, label, 60, Color.black, Vector2.zero, new Vector2(500, 140), FontStyle.Bold);
    }

    void MakeChoiceButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick,
                          out Image img, out Text text)
    {
        var go = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(380, 150);
        rt.anchoredPosition = pos;
        img = go.GetComponent<Image>();
        go.GetComponent<Button>().onClick.AddListener(onClick);
        text = MakeText(go.transform, label, 56, Color.white, Vector2.zero, new Vector2(380, 150), FontStyle.Bold);
        text.supportRichText = true;
    }

    static Font LoadFont()
    {
        // Unity 2022.2+ / Unity 6 built-in font; older versions use Arial.ttf.
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        return f;
    }
}