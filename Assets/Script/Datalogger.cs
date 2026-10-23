using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

// Saves research data as CSV files, one folder per participant session.
// Logging starts when the session starts (SessionManager → BeginSession) and
// stops when the session time is over (EndSession).
//
//   Documents/Sessions/<P_ID>/          (e.g. Sessions/P07 — a second session with
//                                         the same ID becomes P07_2, P07_3, …)
//      session_info.csv  – participant ID, planned duration, device, start/end time
//      movement.csv      – where the user walks / looks (10× per second)
//      haptics.csv       – finger, can paint, distance, nozzle intensity, speed, warning, tool, brush (10× per second)
//                          (condition A: nozzle_intensity is 0 — nothing is sent to the motor —
//                           but speed / warning / distance are still measured)
//      final_paint.csv   – the FINAL painting: every dot still on the walls
//      pointcloud.csv    – the scanned room as a DENSE point cloud (LiDAR mesh surface sampled)
//
// movement.csv and haptics.csv are written WHILE the session runs and flushed to
// disk every second. final_paint.csv (every 10 s) and pointcloud.csv (every 60 s)
// are re-saved as snapshots → after a crash you still have the last snapshot.
// When the app goes to the background, is closed, or the session ends, everything
// is saved at once.
//
// On the iPhone: Files app → On My iPhone → (app name) → Sessions
// (needs EnableFileSharing.cs in Assets/Editor).
public class DataLogger : MonoBehaviour
{
    [Header("References (empty = found automatically)")]
    public Camera arCamera;
    public ARMeshManager meshManager;
    public SprayPainter painter;

    [Header("Settings")]
    public bool logging = true;                     // untick = nothing is saved
    [Range(1, 60)] public float movementHz = 10f;   // movement.csv samples per second
    [Range(1, 60)] public float hapticsHz = 10f;    // haptics.csv samples per second
    public float flushInterval = 1f;                // write to disk every … s (crash safety)
    public float paintSaveInterval = 10f;           // save the final painting every … s (0 = only at the end)
    public float pointCloudSaveInterval = 60f;      // save the room scan every … s (0 = only at the end)

    [Tooltip("Optional: add the condition to the folder name (Sessions/P01_A). " +
             "Off = folder is only the participant ID (Sessions/P01).")]
    public bool addConditionToFolderName = false;

    [Header("Point cloud density")]
    [Tooltip("Distance between points on the scanned surfaces (m). 0.01 = 1 cm = very dense (big file). " +
             "0.02 = 2 cm. 0 = only the mesh corners (sparse, like before).")]
    public float pointSpacing = 0.015f;
    [Tooltip("Also include the original mesh corner points.")]
    public bool includeMeshVertices = true;

    public string SessionFolder { get; private set; }
    public string ParticipantId { get; private set; }
    public string Condition { get; private set; }
    public bool IsRunning => moveW != null;

    static readonly CultureInfo IC = CultureInfo.InvariantCulture;   // always "1.5", never "1,5"

    StreamWriter moveW, hapW;
    float startTime, plannedMinutes, nextMove, nextHap, nextFlush, nextCloud, nextPaint;
    bool fingerDown, canPaint;
    float aimDistance = -1f;
    Vector3? lastGround;
    float walked;                                // metres walked on the ground (x/z)
    readonly object cloudLock = new object();
    readonly object paintLock = new object();
    volatile bool cloudSaving, paintSaving;
    readonly StringBuilder sb = new StringBuilder(256);

    void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
        if (meshManager == null) meshManager = FindFirstObjectByType<ARMeshManager>();
        if (painter == null) painter = FindFirstObjectByType<SprayPainter>();
    }

    // ───────────────────────── session start / end ─────────────────────────

    /// Called by SessionManager when the participant presses Start.
    public void BeginSession(string participantId, float minutes, string condition = "")
    {
        if (!logging || IsRunning) return;
        ParticipantId = participantId;
        Condition = condition ?? "";
        plannedMinutes = minutes;

        // Folder = participant ID (+ condition); never overwrite an earlier session.
        string root = Path.Combine(Application.persistentDataPath, "Sessions");
        string baseName = participantId +
            (addConditionToFolderName && Condition.Length > 0 ? "_" + Condition : "");
        string folder = Path.Combine(root, baseName);
        for (int n = 2; Directory.Exists(folder); n++)
            folder = Path.Combine(root, baseName + "_" + n);
        SessionFolder = folder;
        Directory.CreateDirectory(SessionFolder);

        moveW = Open("movement.csv",
            "t_s,time,cam_x,cam_y,cam_z,rot_x,rot_y,rot_z,rot_w,yaw_deg,pitch_deg,roll_deg," +
            "ground_x,ground_z,walked_m,tracking");
        hapW = Open("haptics.csv",
            "t_s,time,finger_down,can_paint,aim_distance_m,nozzle_intensity,phone_speed,too_fast," +
            "tool,brush_speed_mps,brush_base_intensity,brush_ticks_total");

        startTime = Time.realtimeSinceStartup;
        nextMove = nextHap = nextFlush = startTime;
        nextCloud = startTime + pointCloudSaveInterval;
        nextPaint = startTime + paintSaveInterval;
        walked = 0; lastGround = null;

        WriteSessionInfo(ended: false);
        Debug.Log("[DataLogger] Session " + participantId + " → " + SessionFolder);
    }

    /// Called by SessionManager when the session time is over.
    public void EndSession()
    {
        if (!IsRunning) return;
        SaveAll();
        WriteSessionInfo(ended: true);
        Close();
        Debug.Log("[DataLogger] Session ended and saved");
    }

    StreamWriter Open(string file, string header)
    {
        var w = new StreamWriter(Path.Combine(SessionFolder, file), false, new UTF8Encoding(false));
        w.WriteLine(header);
        w.Flush();
        return w;
    }

    void WriteSessionInfo(bool ended)
    {
        using (var w = new StreamWriter(Path.Combine(SessionFolder, "session_info.csv"), false, new UTF8Encoding(false)))
        {
            w.WriteLine("key,value");
            w.WriteLine("participant_id," + Csv(ParticipantId));
            w.WriteLine("condition," + Csv(Condition));
            w.WriteLine("haptics_on," + (Condition == "A" ? "0" : "1"));
            w.WriteLine("planned_minutes," + F(plannedMinutes));
            w.WriteLine("session_start," + DateTime.Now.AddSeconds(-(Time.realtimeSinceStartup - startTime)).ToString("yyyy-MM-dd HH:mm:ss", IC));
            w.WriteLine("session_end," + (ended ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", IC) : "(still running or app closed early)"));
            w.WriteLine("actual_seconds," + (ended ? F(Time.realtimeSinceStartup - startTime) : ""));
            w.WriteLine("device_model," + Csv(SystemInfo.deviceModel));
            w.WriteLine("operating_system," + Csv(SystemInfo.operatingSystem));
            w.WriteLine("app_version," + Csv(Application.version));
            w.WriteLine("movement_hz," + F(movementHz));
            w.WriteLine("haptics_hz," + F(hapticsHz));
            w.WriteLine("point_spacing_m," + F(pointSpacing));
            w.WriteLine("units,metres / degrees / seconds since session start (t_s)");
        }
    }

    // ───────────────────────── called by SprayPainter ─────────────────────────

    /// Every frame: finger, can-paint and aim distance (for haptics.csv).
    public void SetState(bool finger, bool paint, float distance)
    {
        fingerDown = finger;
        canPaint = paint;
        aimDistance = distance;
    }

    // ───────────────────────── every frame ─────────────────────────

    void Update()
    {
        if (!IsRunning) return;
        float now = Time.realtimeSinceStartup;

        if (now >= nextMove) { nextMove = now + 1f / Mathf.Max(1f, movementHz); LogMovement(); }
        if (now >= nextHap)  { nextHap  = now + 1f / Mathf.Max(1f, hapticsHz);  LogHaptics(); }
        if (now >= nextFlush) { nextFlush = now + flushInterval; Flush(); }
        if (paintSaveInterval > 0 && now >= nextPaint)
        {
            nextPaint = now + paintSaveInterval;
            SaveFinalPaint(false);   // in the background, no hitch
        }
        if (pointCloudSaveInterval > 0 && now >= nextCloud)
        {
            nextCloud = now + pointCloudSaveInterval;
            SavePointCloud(false);   // in the background, no hitch
        }
    }

    void LogMovement()
    {
        if (moveW == null || arCamera == null) return;
        Transform t = arCamera.transform;
        Vector3 p = t.position;
        Quaternion q = t.rotation;
        Vector3 e = q.eulerAngles;

        // distance walked on the ground (ignores height and tiny tracking jitter)
        var ground = new Vector3(p.x, 0, p.z);
        if (lastGround.HasValue)
        {
            float step = Vector3.Distance(ground, lastGround.Value);
            if (step > 0.01f) { walked += step; lastGround = ground; }
        }
        else lastGround = ground;

        bool tracking = ARSession.state == ARSessionState.SessionTracking;

        sb.Clear();
        Begin(sb).Append(',')
            .Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',')
            .Append(F(q.x)).Append(',').Append(F(q.y)).Append(',').Append(F(q.z)).Append(',').Append(F(q.w)).Append(',')
            .Append(F(e.y)).Append(',').Append(F(Signed(e.x))).Append(',').Append(F(Signed(e.z))).Append(',')
            .Append(F(p.x)).Append(',').Append(F(p.z)).Append(',').Append(F(walked)).Append(',')
            .Append(tracking ? 1 : 0);
        moveW.WriteLine(sb.ToString());
    }

    void LogHaptics()
    {
        if (hapW == null) return;
        sb.Clear();
        Begin(sb).Append(',')
            .Append(fingerDown ? 1 : 0).Append(',')
            .Append(canPaint ? 1 : 0).Append(',')
            .Append(F(aimDistance)).Append(',')
            .Append(F(HapticsBridge.GetDebug(14))).Append(',')     // nozzle intensity sent to the motor
            .Append(F(HapticsBridge.GetDebug(9))).Append(',')      // phone speed
            .Append(HapticsBridge.GetDebug(11) > 0.5f ? 1 : 0).Append(',')   // too fast (warning on)
            .Append(HapticsBridge.GetDebug(29) > 0.5f ? "brush" : "spray").Append(',')   // NEW: tool
            .Append(F(HapticsBridge.GetDebug(30))).Append(',')     // NEW: brush hand speed on the wall
            .Append(F(HapticsBridge.GetDebug(31))).Append(',')     // NEW: brush base intensity sent
            .Append(F(HapticsBridge.GetDebug(32)));                // NEW: bristle ticks played so far
        hapW.WriteLine(sb.ToString());
    }

    // ───────────────────────── final painting ─────────────────────────

    /// Saves every dot that is still painted (erased dots are not included).
    public void SaveFinalPaint(bool sync)
    {
        if (painter == null || SessionFolder == null) return;
        if (!sync && paintSaving) return;

        List<SprayPainter.DotRecord> dots = painter.GetPaintSnapshot();   // copy on the main thread
        string path = Path.Combine(SessionFolder, "final_paint.csv");
        float t0 = startTime;
        float tSave = Time.realtimeSinceStartup - startTime;

        if (sync)
        {
            WritePaint(path, dots, t0, tSave);
        }
        else
        {
            paintSaving = true;
            Task.Run(() =>
            {
                try { WritePaint(path, dots, t0, tSave); }
                catch (Exception ex) { Debug.LogWarning("[DataLogger] Final paint save failed: " + ex.Message); }
                finally { paintSaving = false; }
            });
        }
    }

    void WritePaint(string path, List<SprayPainter.DotRecord> dots, float t0, float tSave)
    {
        lock (paintLock)
        {
            string tmp = path + ".tmp";
            using (var w = new StreamWriter(tmp, false, new UTF8Encoding(false)))
            {
                w.WriteLine("stroke_id,x,y,z,nx,ny,nz,color,size_m,painted_at_t_s,saved_at_t_s");
                string ts = F(tSave);
                var line = new StringBuilder(128);
                foreach (var d in dots)
                {
                    line.Clear();
                    line.Append(d.strokeId).Append(',')
                        .Append(F(d.position.x)).Append(',').Append(F(d.position.y)).Append(',').Append(F(d.position.z)).Append(',')
                        .Append(F(d.normal.x)).Append(',').Append(F(d.normal.y)).Append(',').Append(F(d.normal.z)).Append(',')
                        .Append('#').Append(Hex(d.color)).Append(',')
                        .Append(F(d.size)).Append(',')
                        .Append(F(d.paintedAt - t0)).Append(',')
                        .Append(ts);
                    w.WriteLine(line.ToString());
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }

    // ───────────────────────── dense point cloud (scanned room) ─────────────────────────
    // The LiDAR mesh only has a corner point every few cm (sparse). To make the
    // cloud DENSE, we also place points ON the mesh triangles, one per
    // pointSpacing × pointSpacing area — like sprinkling sand evenly over every surface.

    struct MeshCopy
    {
        public int id;
        public Vector3[] v, n;
        public int[] tri;
        public Matrix4x4 toWorld;
    }

    /// sync = true when the app is closing / the session ends (must finish right now).
    public void SavePointCloud(bool sync)
    {
        if (meshManager == null || SessionFolder == null) return;
        if (!sync && cloudSaving) return;   // previous background save still running

        // Unity meshes can only be read on the main thread → copy the raw data first.
        var copies = new List<MeshCopy>();
        int meshId = 0;
        foreach (MeshFilter mf in meshManager.meshes)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            Mesh m = mf.sharedMesh;
            copies.Add(new MeshCopy
            {
                id = meshId++,
                v = m.vertices,
                n = m.normals,
                tri = m.triangles,
                toWorld = mf.transform.localToWorldMatrix
            });
        }

        string path = Path.Combine(SessionFolder, "pointcloud.csv");
        float tSave = Time.realtimeSinceStartup - startTime;
        float spacing = pointSpacing;
        bool withVerts = includeMeshVertices;

        if (sync)
        {
            WriteCloud(path, copies, spacing, withVerts, tSave);
        }
        else
        {
            cloudSaving = true;
            Task.Run(() =>
            {
                try { WriteCloud(path, copies, spacing, withVerts, tSave); }
                catch (Exception ex) { Debug.LogWarning("[DataLogger] Point cloud save failed: " + ex.Message); }
                finally { cloudSaving = false; }
            });
        }
    }

    // Runs on a background thread: only plain maths here (no Unity objects).
    void WriteCloud(string path, List<MeshCopy> meshes, float spacing, bool withVerts, float tSave)
    {
        lock (cloudLock)
        {
            var rng = new System.Random(12345);   // fixed seed → same room gives the same sampling pattern
            float cellArea = spacing > 0 ? spacing * spacing : 0f;
            long count = 0;

            string tmp = path + ".tmp";   // write to a temp file first → never a half-written CSV
            using (var w = new StreamWriter(tmp, false, new UTF8Encoding(false)))
            {
                w.WriteLine("mesh_id,source,x,y,z,nx,ny,nz,saved_at_t_s");
                string ts = F(tSave);
                var line = new StringBuilder(112);

                void Write(int id, string src, Vector3 p, Vector3 nrm)
                {
                    line.Clear();
                    line.Append(id).Append(',').Append(src).Append(',')
                        .Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',')
                        .Append(F(nrm.x)).Append(',').Append(F(nrm.y)).Append(',').Append(F(nrm.z)).Append(',')
                        .Append(ts);
                    w.WriteLine(line.ToString());
                    count++;
                }

                foreach (var m in meshes)
                {
                    bool hasN = m.n != null && m.n.Length == m.v.Length;

                    // 1) the original mesh corner points
                    if (withVerts)
                        for (int i = 0; i < m.v.Length; i++)
                            Write(m.id, "vertex", m.toWorld.MultiplyPoint3x4(m.v[i]),
                                  hasN ? m.toWorld.MultiplyVector(m.n[i]).normalized : Vector3.zero);

                    // 2) extra points spread evenly over every triangle
                    if (cellArea <= 0f) continue;
                    for (int t = 0; t + 2 < m.tri.Length; t += 3)
                    {
                        int i0 = m.tri[t], i1 = m.tri[t + 1], i2 = m.tri[t + 2];
                        Vector3 a = m.toWorld.MultiplyPoint3x4(m.v[i0]);
                        Vector3 b = m.toWorld.MultiplyPoint3x4(m.v[i1]);
                        Vector3 c = m.toWorld.MultiplyPoint3x4(m.v[i2]);
                        Vector3 cross = Vector3.Cross(b - a, c - a);
                        float area = cross.magnitude * 0.5f;
                        if (area <= 0f) continue;

                        // how many points this triangle gets (fractional part decided randomly)
                        float expected = area / cellArea;
                        int k = (int)expected;
                        if (rng.NextDouble() < expected - k) k++;

                        Vector3 na = hasN ? m.toWorld.MultiplyVector(m.n[i0]) : cross;
                        Vector3 nb = hasN ? m.toWorld.MultiplyVector(m.n[i1]) : cross;
                        Vector3 nc = hasN ? m.toWorld.MultiplyVector(m.n[i2]) : cross;

                        for (int s = 0; s < k; s++)
                        {
                            // random point inside the triangle (uniform)
                            float r1 = (float)rng.NextDouble(), r2 = (float)rng.NextDouble();
                            if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                            float r0 = 1f - r1 - r2;
                            Vector3 p = a * r0 + b * r1 + c * r2;
                            Vector3 nrm = (na * r0 + nb * r1 + nc * r2).normalized;
                            Write(m.id, "surface", p, nrm);
                        }
                    }
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            Debug.Log("[DataLogger] Point cloud saved: " + count + " points");
        }
    }

    // ───────────────────────── saving when the app closes ─────────────────────────

    void Flush()
    {
        moveW?.Flush();
        hapW?.Flush();
    }

    void SaveAll()
    {
        if (SessionFolder == null) return;
        Flush();
        SaveFinalPaint(true);
        SavePointCloud(true);
        Debug.Log("[DataLogger] Saved all data");
    }

    // iOS: going to the home screen / app switcher / phone call / swiping the app away
    // ALWAYS passes through here first → the safest place to save.
    void OnApplicationPause(bool paused)
    {
        if (paused && IsRunning) SaveAll();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) Flush();
    }

    void OnApplicationQuit()
    {
        if (IsRunning) SaveAll();
        Close();
    }

    void OnDestroy() => Close();

    void Close()
    {
        Flush();
        moveW?.Dispose(); moveW = null;
        hapW?.Dispose();  hapW = null;
    }

    // ───────────────────────── helpers ─────────────────────────

    StringBuilder Begin(StringBuilder b)
    {
        float t = Time.realtimeSinceStartup - startTime;
        return b.Append(F(t)).Append(',').Append(DateTime.Now.ToString("HH:mm:ss.fff", IC));
    }

    static string F(float v) => v.ToString("0.#####", IC);
    // colour as #RRGGBB (plain C#, safe to use on the background thread)
    static string Hex(Color c)
    {
        Color32 c32 = c;
        return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
    }
    static float Signed(float deg) => deg > 180f ? deg - 360f : deg;   // 350° → -10°
    static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "'") + "\"";
}