using UnityEngine;

// On-screen debugger for the Swift haptics plugin.
// Shows live values from HapticNozzle.swift + a red dot where the invisible ball is.
// Put it on any GameObject (e.g. Painter). Untick "Show" to hide it.
public class HapticsDebugOverlay : MonoBehaviour
{
    public bool show = true;
    public bool showBall = true;
    [Range(20, 60)] public int fontSize = 34;
    public bool atTop = true;   // NEW: true = top of the screen (below the notch), false = middle

    // Same numbers as debugValue() in HapticNozzle.swift
    const int ENGINE = 0, MOTION = 1, ERRORS = 2,
              F_PUSH = 3, F_RELEASE = 4, F_NOZZLE = 5, F_DOUBLE = 6,
              HELD = 7, NOZZLE_PLAYER = 8,
              SPEED = 9, WARN_START = 10, OVERSPEED = 11, WARN_MIX = 12,
              ENVELOPE = 13, SENT = 14, SHARPNESS = 15,
              BALL_X = 16, BALL_Y = 17, BALL_SPEED = 18, HITS = 19, LAST_WALL = 20,
              DISTANCE = 21, PROX_BOOST = 22, PROX_FADE = 23, CAN_PAINT = 24,
              AIR_VOL = 25, HIT_SRC = 26, AIR_SRC = 27, HIT_SOUNDS = 28,
              TOOL = 29, BRUSH_SPEED = 30, BRUSH_SENT = 31, BRUSH_TICKS = 32,
              F_BRUSH_MOVE = 33, F_BRUSH_TICK = 34;

    static readonly string[] WallNames = { "left", "right", "top", "bottom" };

    float hitFlashUntil;
    int lastHits;
    GUIStyle style;

    static float V(int id) => HapticsBridge.GetDebug(id);
    static bool B(int id) => V(id) > 0.5f;
    static string OK(bool v) => v ? "OK" : "NO";
    static string File(int id)
    {
        float v = V(id);
        if (v < -0.5f) return "<color=#aaaaaa>not loaded yet</color>";
        return v > 0.5f ? "ok" : "<color=#ff5050>MISSING</color>";
    }

    void OnGUI()
    {
        if (!show) return;

        if (style == null)
            style = new GUIStyle(GUI.skin.label) { richText = true };
        style.fontSize = fontSize;
        style.normal.textColor = Color.white;

        float w = Screen.width - 80;
        // CHANGED: top of the screen, just below the notch / Dynamic Island (safe area)
        float topInset = Screen.height - (Screen.safeArea.y + Screen.safeArea.height);
        float y = atTop ? topInset + 20 : Screen.height * 0.40f;

        // Dark background so the text is readable over the camera image
        float boxH = fontSize * 14.7f;
        GUI.color = new Color(0, 0, 0, 0.55f);
        GUI.DrawTexture(new Rect(20, y - 10, Screen.width - 40, boxH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (!HapticsBridge.IsAvailable)
        {
            GUI.Label(new Rect(40, y, w, fontSize * 2),
                "HAPTICS: only on iPhone (Editor has no Swift)", style);
            return;
        }

        int hits = Mathf.RoundToInt(V(HITS));
        if (hits != lastHits) { hitFlashUntil = Time.time + 0.25f; lastHits = hits; }
        int wall = Mathf.RoundToInt(V(LAST_WALL));
        string wallName = wall >= 0 && wall < 4 ? WallNames[wall] : "-";
        string hitText = Time.time < hitFlashUntil ? "<color=#ff5050>HIT!</color>" : "";

        bool tooFast = B(OVERSPEED);
        string text =
            $"<b>HAPTICS</b>  engine {OK(B(ENGINE))}  motion {OK(B(MOTION))}  errors {V(ERRORS):0}\n" +
            $"files: push {File(F_PUSH)}  release {File(F_RELEASE)}  nozzle {File(F_NOZZLE)}  double {File(F_DOUBLE)}\n" +
            $"finger {(B(HELD) ? "<color=#7CFC00>DOWN</color>" : "up")}   nozzle player {(B(NOZZLE_PLAYER) ? "ON" : "off")}   env {V(ENVELOPE):0.00}\n" +
            $"speed {V(SPEED):0.00} / warn at {V(WARN_START):0.00}   " +
            $"{(tooFast ? "<color=#ff5050>TOO FAST</color>" : "ok")}  warn mix {V(WARN_MIX):0.00}\n" +
            $"distance {DistanceText()}   boost +{V(PROX_BOOST):0.00}   fade x{V(PROX_FADE):0.00}\n" +
            $"nozzle sent {V(SENT):0.00}   sharpness {V(SHARPNESS):+0.00;-0.00}\n" +
            $"ball {V(BALL_SPEED):0.00} m/s   hits {hits} (last: {wallName}) {hitText}" + "\n" +
            $"sound: ball {Src(HIT_SRC)}   air {Src(AIR_SRC)}   air vol {V(AIR_VOL):0.00}   hit sounds {V(HIT_SOUNDS):0}\n" +
            $"tool {(B(TOOL) ? "<color=#7CFC00>BRUSH</color>" : "spray")}   brush speed {V(BRUSH_SPEED):0.00} m/s   base {V(BRUSH_SENT):0.00}   ticks {V(BRUSH_TICKS):0}\n" +
            $"brush files: moving {File(F_BRUSH_MOVE)}  tick {File(F_BRUSH_TICK)}";
        GUI.Label(new Rect(40, y, w, boxH), text, style);

        // Bar = intensity really sent to the motor (0…1). Red line = warning level 0.9.
        float barY = y + fontSize * 12.8f, barH = fontSize * 0.8f;
        GUI.color = new Color(1, 1, 1, 0.25f);
        GUI.DrawTexture(new Rect(40, barY, w, barH), Texture2D.whiteTexture);
        GUI.color = tooFast ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 0.9f, 1f);
        GUI.DrawTexture(new Rect(40, barY, w * Mathf.Clamp01(V(SENT)), barH), Texture2D.whiteTexture);
        GUI.color = Color.red;
        GUI.DrawTexture(new Rect(40 + w * 0.9f, barY, 3, barH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // The invisible ball as a red dot (its real position on the screen)
        if (showBall)
        {
            float bx = V(BALL_X) * Screen.width;
            float by = V(BALL_Y) * Screen.height;
            float size = Screen.width * (50f / 393f);   // ball is 50 pt on a ~393 pt wide screen
            GUI.color = Time.time < hitFlashUntil ? Color.yellow : new Color(1f, 0.2f, 0.2f, 0.8f);
            GUI.DrawTexture(new Rect(bx - size / 2, by - size / 2, size, size), MakeDot());
            GUI.color = Color.white;
        }
    }

    // 1 = your file, 0 = made in code, -1 = sound not started yet (start a session)
    static string Src(int id)
    {
        float v = V(id);
        if (v < -0.5f) return "<color=#aaaaaa>not started</color>";
        return v > 0.5f ? "<color=#7CFC00>YOUR FILE</color>" : "<color=#ffb050>made in code</color>";
    }

    static string DistanceText()
    {
        float d = V(DISTANCE);
        string dist = d < 0 ? "-" : $"{d:0.00} m";
        if (!B(CAN_PAINT)) return $"{dist} <color=#ffb050>(can't paint)</color>";
        string zone = d <= 0.30f ? "very close" : d <= 0.60f ? "close" : "normal";
        return $"{dist} <color=#7CFC00>(painting: {zone})</color>";
    }

    Texture2D dot;
    Texture2D MakeDot()
    {
        if (dot != null) return dot;
        const int n = 64;
        dot = new Texture2D(n, n, TextureFormat.RGBA32, false);
        float c = (n - 1) / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                dot.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 6f)));
            }
        dot.Apply();
        return dot;
    }
}