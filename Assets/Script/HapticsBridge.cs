using System.Runtime.InteropServices;
using UnityEngine;

// The "phone line" between Unity (C#) and the Swift plugin.
public static class HapticsBridge
{
#if UNITY_IOS && !UNITY_EDITOR
    // These must match the @_cdecl names in HapticNozzle.swift exactly.
    [DllImport("__Internal")] static extern void HapticNozzle_Start();
    [DllImport("__Internal")] static extern void HapticNozzle_Press();
    [DllImport("__Internal")] static extern void HapticNozzle_Release();
    [DllImport("__Internal")] static extern void HapticNozzle_SetDistance(float meters);
    [DllImport("__Internal")] static extern void HapticNozzle_SetCanPaint(int yes);
    [DllImport("__Internal")] static extern float HapticNozzle_GetDebug(int id);
    [DllImport("__Internal")] static extern void HapticNozzle_SetParam(string name, float value);
    [DllImport("__Internal")] static extern void HapticNozzle_Stop();

    public static void Start()                  => HapticNozzle_Start();
    public static void Press()                  => HapticNozzle_Press();
    public static void Release()                => HapticNozzle_Release();
    public static void SetDistance(float m)     => HapticNozzle_SetDistance(m);
    public static void SetCanPaint(bool yes)    => HapticNozzle_SetCanPaint(yes ? 1 : 0);
    public static float GetDebug(int id)        => HapticNozzle_GetDebug(id);
    public static void SetParam(string n, float v) => HapticNozzle_SetParam(n, v);   // one tuning value
    public static void Stop()                   => HapticNozzle_Stop();   // NEW: everything off (session end)
    public static bool IsAvailable              => true;
#else
    // In the Unity Editor there is no Swift, so we only print a message.
    public static void Start()                  => Debug.Log("[Haptics] Start");
    public static void Press()                  => Debug.Log("[Haptics] Press");
    public static void Release()                => Debug.Log("[Haptics] Release");
    public static void SetDistance(float m)     { }
    public static void SetCanPaint(bool yes)    { }
    public static float GetDebug(int id)        => 0f;
    public static void SetParam(string n, float v) { }
    public static void Stop()                   => Debug.Log("[Haptics] Stop");
    public static bool IsAvailable              => false;
#endif
}