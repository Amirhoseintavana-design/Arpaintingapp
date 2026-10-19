using UnityEngine;

// GENERATED from HapticNozzle.swift (PART 1–3, 5 + debug).
// All haptic + sound handles in the Unity Inspector. Hover a field to see what it does.
// The values are sent to the Swift plugin when the app starts (Awake),
// so you can tune here and just Build — no Swift editing needed.
// The Inspector value WINS over the default written here.
// Right-click the component title → Reset = back to these defaults.
public class HapticSettings : MonoBehaviour
{
    [Tooltip("Untick = ignore this component; the plugin uses the values written in HapticNozzle.swift.")]
    public bool applyOnStart = true;

    [Header("1A. STRENGTH HANDLES")]
    [Tooltip("Scales ALL patterns at once (quick 'everything a bit softer / stronger').")]
    public float masterHapticGain = 1.0f;
    [Tooltip("push.ahap — the 'push' when the finger touches the screen.")]
    public float pushGain = 1.0f;
    [Tooltip("holding_nozzle.ahap — the CEILING of the nozzle = the speed-warning level. Lowering this also weakens the warning! To soften only the normal hold, lower kAirBaseIntensity (1B) instead.")]
    public float holdingNozzleGain = 1.0f;
    [Tooltip("releasing_nozzle.ahap — when the finger is lifted.")]
    public float releaseGain = 1.5f;
    [Tooltip("double_haptic.ahap — every ball hit on a screen edge.")]
    public float collisionGain = 1.5f;

    [Header("1B. HOLDING NOZZLE — NORMAL SPEED")]
    [Tooltip("NORMAL HOLD STRENGTH. Higher = stronger hold, lower = softer hold. The warning is NOT affected.")]
    public float airBaseIntensity = 0.25f;
    [Tooltip("TURBULENCE: how much the 'air' wiggles up/down (±) around the base. Higher = livelier, more uneven air. Lower = steadier.")]
    public float airTurbulenceDepth = 0.15f;
    [Tooltip("MIDDLE-SPEED RISE: how much the nozzle grows with speed BEFORE the warning. 0.15 → 0.25 still, up to ≈ 0.40 just under the limit. 0 = no rise.")]
    public float airSpeedGain = 0.15f;
    [Tooltip("Shape of that rise. 1.0 = even, 2.0 = mostly near the limit, 0.7 = already clearly at low speed.")]
    public float airSpeedCurve = 1.0f;
    [Tooltip("How gently the speed rise follows your movement (s). Higher = slower swell.")]
    public float airSpeedSmoothing = 0.35f;
    [Tooltip("SAFETY GAP: the normal level can never go above (kWarningIntensity − this), so normal (incl. speed rise + close boost) is never mistaken for the warning. 0.30 → normal is capped at 0.60.")]
    public float warningGapMargin = 0.30f;
    [Tooltip("TEXTURE: how much the sharpness wiggles (±). Higher = more 'hissy' air.")]
    public float airSharpnessDepth = 0.06f;
    [Tooltip("SLOW 'pressure breathing': each wave lasts a random time in this range (s). Longer = slower, calmer waves.")]
    public float slowNoiseIntervalMin = 0.50f;
    [Tooltip("SLOW 'pressure breathing': each wave lasts a random time in this range (s). Longer = slower, calmer waves.")]
    public float slowNoiseIntervalMax = 1.20f;
    [Tooltip("FAST 'flutter': each wiggle lasts a random time in this range (s). Shorter = faster, buzzier flutter.")]
    public float fastNoiseIntervalMin = 0.10f;
    [Tooltip("FAST 'flutter': each wiggle lasts a random time in this range (s). Shorter = faster, buzzier flutter.")]
    public float fastNoiseIntervalMax = 0.22f;
    [Tooltip("Mix of the two layers: 0 = only slow waves, 1 = only flutter.")]
    public float fastNoiseWeight = 0.30f;

    [Header("1C. HOLDING NOZZLE — TOO FAST (speed warning)")]
    [Tooltip("Strength just over the speed limit. Higher = stronger warning.")]
    public float warningIntensity = 0.90f;
    [Tooltip("Strength when far over the limit (1.0 = motor maximum).")]
    public float warningMaxIntensity = 1.00f;
    [Tooltip("How far over the limit (speed units) until kWarningMaxIntensity is reached. Higher = grows more slowly, lower = reaches max sooner.")]
    public float warningOverspeedRange = 0.5f;
    [Tooltip("Turbulence left during the warning (1 = all, 0 = steady 'solid' warning).")]
    public float warningTurbulenceScale = 0.5f;
    [Tooltip("Extra crispness during the warning. Higher = more alarming.")]
    public float warningSharpnessBoost = 0.15f;
    [Tooltip("How fast the warning swells IN (s). Lower = snappier.")]
    public float warningRiseTime = 0.30f;
    [Tooltip("How fast it goes back to normal (s). Higher = gentler return.")]
    public float warningFallTime = 0.50f;

    [Header("1D. HOLDING NOZZLE — PRESS / RELEASE ENVELOPE")]
    [Tooltip("Pause after the push before the nozzle starts (s), so the push is felt alone. .08 (nozzle starts sooner after the push)")]
    public float holdAttackDelay = 0.04f;
    [Tooltip("How fast the nozzle FADES IN after the push (s). .12 (fades in ~2.5× faster)")]
    public float holdAttackTime = 0.05f;
    [Tooltip("How fast the nozzle FADES OUT after release (s).")]
    public float releaseTime = 0.04f;
    [Tooltip("Final smoothing of every level change (s). Keep small (0.03–0.1), otherwise everything feels laggy.")]
    public float outputSmoothingTime = 0.05f;
    [Tooltip("NEW: smoothing when the level goes UP (s). Very small = the nozzle jumps up right away when painting becomes possible, or when you come closer. Going DOWN still uses kOutputSmoothingTime (soft, no clicks).")]
    public float outputRiseTime = 0.012f;

    [Header("1E. BALL COLLISION HAPTIC")]
    [Tooltip("Hits slower than this (m/s) are ignored. Lower = more / lighter hits count.")]
    public float collisionSpeedThreshold = 0.05f;
    [Tooltip("Minimum time between two hit haptics (s). Stops 'buzzing' on an edge.")]
    public float collisionMinInterval = 0.06f;
    [Tooltip("After a hit, the SAME wall fires again only when the ball has moved this far away (points). Higher = fewer double hits from small re-bounces.")]
    public float collisionRearmDistance = 30.0f;
    [Tooltip("No ball hits for this long after pressing (s), so the push is felt cleanly.")]
    public float collisionMuteAfterPress = 0.30f;
    [Tooltip("false = EVERY hit feels the same (the .ahap × kCollisionGain) — your choice. true = harder hits feel stronger, using the 4 values below.")]
    public bool collisionUseImpactStrength = false;
    [Tooltip("Impact speed (m/s) that gives full strength.")]
    public float collisionSpeedForMax = 1.2f;
    [Tooltip("Strength of the lightest hit that still counts (0–1).")]
    public float collisionMinIntensity = 0.3f;
    [Tooltip("Curve: < 1 = medium hits feel stronger, > 1 = weaker.")]
    public float collisionGamma = 0.7f;
    [Tooltip("Hard hits also get crisper (sharpness added at full strength).")]
    public float collisionSharpnessBoost = 0.15f;

    [Header("1E. DUCKING (the nozzle dips at each hit, so the hit stands out)")]
    [Tooltip("true = dip the nozzle at every hit. false = no dip (to compare).")]
    public bool collisionDuckNozzle = true;
    [Tooltip("How deep the dip is (0–1). 0.8 = nozzle drops to 20 %. 1.0 = fully off.")]
    public float collisionDuckDepth = 0.8f;
    [Tooltip("How long the dip lasts (s). Should cover the hit tap(s).")]
    public float collisionDuckTime = 0.08f;
    [Tooltip("How fast the nozzle comes back after the dip (s). Small = quick.")]
    public float collisionDuckRelease = 0.04f;

    [Header("1F. PROXIMITY (distance to the surface under the cursor)")]
    [Tooltip("Proximity on/off. false = always the normal nozzle (useful to compare in a study).")]
    public bool proximityEnabled = true;
    [Tooltip("At this distance (m) or closer: maximum closeness cue.")]
    public float proxNear = 0.30f;
    [Tooltip("From this distance (m) up to Max Distance: normal nozzle, no boost.")]
    public float proxRangeEnd = 0.60f;
    [Tooltip("Extra strength when very close (added to kAirBaseIntensity: 0.25 → 0.40).")]
    public float proxMaxBoost = 0.15f;
    [Tooltip("Extra crispness when very close.")]
    public float proxMaxSharpness = 0.10f;
    [Tooltip("Strength when you CAN'T paint, as a SHARE of the normal level. 0.5 × 0.25 ≈ 0.125 = small but clearly felt ('it's on, but not reaching'). Lower = weaker (below ~0.4 it may not be felt at all). 0 = no nozzle at all.")]
    public float proxFloorFraction = 0.5f;
    [Tooltip("How fast the nozzle drops to the floor after you leave the paint range (s). (Coming back into range is always instant.)")]
    public float proxOutFadeTime = 0.15f;
    [Tooltip("Less sharp when out of range (rounder, 'distant' feel).")]
    public float proxFarSoftening = 0.20f;
    [Tooltip("Turbulence left when out of range (fraction) — calmer air.")]
    public float proxFarTurbulence = 0.30f;
    [Tooltip("Smoothing of the measured distance (s) — removes AR jitter in the boost.")]
    public float proxDistanceSmoothing = 0.15f;
    [Tooltip("How gently the closeness boost follows distance changes (s). .25 (closeness boost reacts faster)")]
    public float proxFactorSmoothing = 0.12f;

    [Header("2. SPEED DETECTION (when the warning starts / ends)")]
    [Tooltip("Warning turns ON above this speed. Lower = warns earlier / more often.")]
    public float speedWarningEnter = 0.5f;
    [Tooltip("Warning turns OFF below this speed. Keep LOWER than Enter (prevents flicker).")]
    public float speedWarningExit = 0.4f;
    [Tooltip("Once on, the warning stays on at least this long (s).")]
    public float warningMinHoldTime = 0.4f;
    [Tooltip("Leak of the speed estimate per 1/60 s (stops it drifting up when still). Lower = speed drops faster after you stop moving.")]
    public float velocityDecay = 0.90f;
    [Tooltip("How fast the speed value follows UP (s) / DOWN (s).")]
    public float speedRiseTime = 0.08f;
    public float speedFallTime = 0.25f;

    [Header("3. BALL PHYSICS (real-world scale)")]
    [Tooltip("Ball diameter in points (50 pt ≈ 8 mm). Bigger = hits walls sooner.")]
    public float ballSize = 50.0f;
    [Tooltip("Reaction to TILT. 1.0 = real gravity. Higher = rolls faster.")]
    public float gravityScale = 1.3f;
    [Tooltip("Reaction to MOVING / SHAKING the phone. 1.0 = real. Higher = more sensitive.")]
    public float inertiaScale = 2.0f;
    [Tooltip("How quickly the ball slows down. Lower = rolls longer.")]
    public float rollingResistance = 0.01f;
    [Tooltip("Bounce at an edge (0 = dead stop, 1 = perfect bounce). Steel ≈ 0.3–0.5.")]
    public float ballRestitution = 0.4f;
    [Tooltip("Speed ALONG the wall lost at each hit (0–1).")]
    public float wallFriction = 0.1f;
    [Tooltip("Smoothing of the motion sensor (s). Higher = lazier, laggy ball.")]
    public float accelSmoothingTime = 0.015f;

    [Header("5. SOUND")]
    [Tooltip("All sounds on/off.")]
    public bool soundEnabled = true;
    [Tooltip("Air sound volume when the nozzle is at 'Air Sound Full Level' or above (0–1).")]
    public float airSoundVolume = 0.6f;
    [Tooltip("Nozzle level that gives the full air volume. Normal hold (≈0.25) → about half; the speed warning (0.9) → full. Lower = louder at normal hold.")]
    public float airSoundFullLevel = 0.5f;
    [Tooltip("Curve of volume vs. nozzle level. 1 = straight, < 1 = louder at low levels, > 1 = turbulence changes are heard more strongly.")]
    public float airSoundCurve = 1.0f;
    [Tooltip("Smoothing of the air volume (s) — avoids crackles, keeps the turbulence audible.")]
    public float airSoundSmoothing = 0.03f;
    [Tooltip("Ball hit sound volume (0–1).")]
    public float collisionSoundVolume = 0.9f;
    [Tooltip("After a hit sound starts, new hit sounds are blocked for its length + this (s).")]
    public float collisionSoundExtraLock = 0.0f;

    [Header("DEBUG")]
    [Tooltip("prints every hit in the Xcode console")]
    public bool logCollisions = true;

    void Awake()
    {
        if (applyOnStart) Apply();
    }

    /// Sends every value to the Swift plugin.
    [ContextMenu("Apply now")]
    public void Apply()
    {
        HapticsBridge.SetParam("kMasterHapticGain", masterHapticGain);
        HapticsBridge.SetParam("kPushGain", pushGain);
        HapticsBridge.SetParam("kHoldingNozzleGain", holdingNozzleGain);
        HapticsBridge.SetParam("kReleaseGain", releaseGain);
        HapticsBridge.SetParam("kCollisionGain", collisionGain);
        HapticsBridge.SetParam("kAirBaseIntensity", airBaseIntensity);
        HapticsBridge.SetParam("kAirTurbulenceDepth", airTurbulenceDepth);
        HapticsBridge.SetParam("kAirSpeedGain", airSpeedGain);
        HapticsBridge.SetParam("kAirSpeedCurve", airSpeedCurve);
        HapticsBridge.SetParam("kAirSpeedSmoothing", airSpeedSmoothing);
        HapticsBridge.SetParam("kWarningGapMargin", warningGapMargin);
        HapticsBridge.SetParam("kAirSharpnessDepth", airSharpnessDepth);
        HapticsBridge.SetParam("kSlowNoiseIntervalMin", slowNoiseIntervalMin);
        HapticsBridge.SetParam("kSlowNoiseIntervalMax", slowNoiseIntervalMax);
        HapticsBridge.SetParam("kFastNoiseIntervalMin", fastNoiseIntervalMin);
        HapticsBridge.SetParam("kFastNoiseIntervalMax", fastNoiseIntervalMax);
        HapticsBridge.SetParam("kFastNoiseWeight", fastNoiseWeight);
        HapticsBridge.SetParam("kWarningIntensity", warningIntensity);
        HapticsBridge.SetParam("kWarningMaxIntensity", warningMaxIntensity);
        HapticsBridge.SetParam("kWarningOverspeedRange", warningOverspeedRange);
        HapticsBridge.SetParam("kWarningTurbulenceScale", warningTurbulenceScale);
        HapticsBridge.SetParam("kWarningSharpnessBoost", warningSharpnessBoost);
        HapticsBridge.SetParam("kWarningRiseTime", warningRiseTime);
        HapticsBridge.SetParam("kWarningFallTime", warningFallTime);
        HapticsBridge.SetParam("kHoldAttackDelay", holdAttackDelay);
        HapticsBridge.SetParam("kHoldAttackTime", holdAttackTime);
        HapticsBridge.SetParam("kReleaseTime", releaseTime);
        HapticsBridge.SetParam("kOutputSmoothingTime", outputSmoothingTime);
        HapticsBridge.SetParam("kOutputRiseTime", outputRiseTime);
        HapticsBridge.SetParam("kCollisionSpeedThreshold", collisionSpeedThreshold);
        HapticsBridge.SetParam("kCollisionMinInterval", collisionMinInterval);
        HapticsBridge.SetParam("kCollisionRearmDistance", collisionRearmDistance);
        HapticsBridge.SetParam("kCollisionMuteAfterPress", collisionMuteAfterPress);
        HapticsBridge.SetParam("kCollisionUseImpactStrength", collisionUseImpactStrength ? 1f : 0f);
        HapticsBridge.SetParam("kCollisionSpeedForMax", collisionSpeedForMax);
        HapticsBridge.SetParam("kCollisionMinIntensity", collisionMinIntensity);
        HapticsBridge.SetParam("kCollisionGamma", collisionGamma);
        HapticsBridge.SetParam("kCollisionSharpnessBoost", collisionSharpnessBoost);
        HapticsBridge.SetParam("kCollisionDuckNozzle", collisionDuckNozzle ? 1f : 0f);
        HapticsBridge.SetParam("kCollisionDuckDepth", collisionDuckDepth);
        HapticsBridge.SetParam("kCollisionDuckTime", collisionDuckTime);
        HapticsBridge.SetParam("kCollisionDuckRelease", collisionDuckRelease);
        HapticsBridge.SetParam("kProximityEnabled", proximityEnabled ? 1f : 0f);
        HapticsBridge.SetParam("kProxNear", proxNear);
        HapticsBridge.SetParam("kProxRangeEnd", proxRangeEnd);
        HapticsBridge.SetParam("kProxMaxBoost", proxMaxBoost);
        HapticsBridge.SetParam("kProxMaxSharpness", proxMaxSharpness);
        HapticsBridge.SetParam("kProxFloorFraction", proxFloorFraction);
        HapticsBridge.SetParam("kProxOutFadeTime", proxOutFadeTime);
        HapticsBridge.SetParam("kProxFarSoftening", proxFarSoftening);
        HapticsBridge.SetParam("kProxFarTurbulence", proxFarTurbulence);
        HapticsBridge.SetParam("kProxDistanceSmoothing", proxDistanceSmoothing);
        HapticsBridge.SetParam("kProxFactorSmoothing", proxFactorSmoothing);
        HapticsBridge.SetParam("kSpeedWarningEnter", speedWarningEnter);
        HapticsBridge.SetParam("kSpeedWarningExit", speedWarningExit);
        HapticsBridge.SetParam("kWarningMinHoldTime", warningMinHoldTime);
        HapticsBridge.SetParam("kVelocityDecay", velocityDecay);
        HapticsBridge.SetParam("kSpeedRiseTime", speedRiseTime);
        HapticsBridge.SetParam("kSpeedFallTime", speedFallTime);
        HapticsBridge.SetParam("kBallSize", ballSize);
        HapticsBridge.SetParam("kGravityScale", gravityScale);
        HapticsBridge.SetParam("kInertiaScale", inertiaScale);
        HapticsBridge.SetParam("kRollingResistance", rollingResistance);
        HapticsBridge.SetParam("kBallRestitution", ballRestitution);
        HapticsBridge.SetParam("kWallFriction", wallFriction);
        HapticsBridge.SetParam("kAccelSmoothingTime", accelSmoothingTime);
        HapticsBridge.SetParam("kSoundEnabled", soundEnabled ? 1f : 0f);
        HapticsBridge.SetParam("kAirSoundVolume", airSoundVolume);
        HapticsBridge.SetParam("kAirSoundFullLevel", airSoundFullLevel);
        HapticsBridge.SetParam("kAirSoundCurve", airSoundCurve);
        HapticsBridge.SetParam("kAirSoundSmoothing", airSoundSmoothing);
        HapticsBridge.SetParam("kCollisionSoundVolume", collisionSoundVolume);
        HapticsBridge.SetParam("kCollisionSoundExtraLock", collisionSoundExtraLock);
        HapticsBridge.SetParam("kLogCollisions", logCollisions ? 1f : 0f);
        Debug.Log("[HapticSettings] Sent 74 values to the haptics plugin");
    }
}