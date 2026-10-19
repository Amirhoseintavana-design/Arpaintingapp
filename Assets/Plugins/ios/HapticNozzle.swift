// HapticNozzle.swift — Unity plugin version of HapticNozzleController (rev 19)
// ============================================================
// Everything from the Xcode demo, without its screen / buttons / sliders:
//   • PRESS            push.ahap
//   • HOLDING NOZZLE   holding_nozzle.ahap + turbulence + middle-speed rise
//   • SPEED WARNING    the nozzle ramps up when the phone moves too fast
//   • RELEASE          releasing_nozzle.ahap + fast fade-out
//   • METAL BALL       real-scale physics, double_haptic.ahap,
//                      same strength every hit (REV19), nozzle ducking (REV13)
//   • NO collision sound (removed on purpose)
//   • DEBUG values for the on-screen debugger (HapticNozzle_GetDebug)
//   • SOUND            air hiss (volume follows the nozzle + turbulence) and ball hit sound (NEW)
//   • PROXIMITY        closer to the surface = a bit stronger/crisper,
//                      not close enough to paint = weak but still felt (REV12 design, now active)
//
// Unity calls these functions (bottom of this file):
//   Start · Press · Release · SetDistance · GetDebug
// Files: Assets/StreamingAssets/Haptics/  (push, holding_nozzle,
//   releasing_nozzle, double_haptic .ahap)
// Assumes the app is locked to PORTRAIT.
// ============================================================

import UIKit
import CoreHaptics
import CoreMotion
import AVFoundation   // NEW: air + ball sounds


// ████████████████████████████████████████████████████████████
// PART 1 — HAPTIC TUNING
// ████████████████████████████████████████████████████████████
// HOW TO TUNE: change ONE value, save, Build in Unity (Replace), run, feel.
// NEW: you can also tune all of these in Unity (HapticSettings component on any
// GameObject). Unity sends its values at app start and they REPLACE the ones here.
// The values written here are only used when there is no HapticSettings in the scene.
// Watch "nozzle sent" + the bar in the on-screen debugger to SEE the change.
//
// Words used below:
//   • Intensity = strength 0…1 (1.0 = the motor's maximum).
//   • Sharpness = how crisp / "buzzy" it feels (higher = crisper, lower = rounder).
//   • A TIME in seconds = how long a change takes ("time constant"):
//     ~63 % done after that time, ~95 % after 3× that time.
//     Smaller = faster / snappier. Larger = slower / smoother.


// ── 1A. STRENGTH HANDLES ─────────────────────────────────────
// They MULTIPLY every HapticIntensity value written inside the .ahap file:
//     played = min( .ahap value × kMasterHapticGain × pattern gain , 1.0 )
//     e.g. .ahap 0.5 × gain 1.5 = 0.75      .ahap 0.8 × gain 1.5 = 1.2 → capped 1.0
// The SHAPE of the pattern (timing, taps, sharpness) is never changed.
// Keep (.ahap loudest value × gain) ≈ 1.0 — above that, events get capped and
// strong / very strong feel the same. The Xcode console prints the result on load.

/// Scales ALL patterns at once (quick "everything a bit softer / stronger").
var kMasterHapticGain: Float = 1.0
/// push.ahap — the "push" when the finger touches the screen.
var kPushGain: Float = 1.0
/// holding_nozzle.ahap — the CEILING of the nozzle = the speed-warning level.
/// Lowering this also weakens the warning! To soften only the normal hold,
/// lower kAirBaseIntensity (1B) instead.
var kHoldingNozzleGain: Float = 1.0
/// releasing_nozzle.ahap — when the finger is lifted.
var kReleaseGain: Float = 1.5
/// double_haptic.ahap — every ball hit on a screen edge.
var kCollisionGain: Float = 1.5


// ── 1B. HOLDING NOZZLE — NORMAL SPEED ────────────────────────
// All levels here are FRACTIONS of the nozzle ceiling (1A).
// What you feel ≈ ceiling × (kAirBaseIntensity + speed rise + close boost) × far fade
//                 ± turbulence

/// NORMAL HOLD STRENGTH. Higher = stronger hold, lower = softer hold.
/// The warning is NOT affected.                         (was 0.30, CHANGED)
var kAirBaseIntensity: Double = 0.25
/// TURBULENCE: how much the "air" wiggles up/down (±) around the base.
/// Higher = livelier, more uneven air. Lower = steadier. (was 0.10, CHANGED)
var kAirTurbulenceDepth: Double = 0.15
/// MIDDLE-SPEED RISE: how much the nozzle grows with speed BEFORE the warning.
/// 0.15 → 0.25 still, up to ≈ 0.40 just under the limit. 0 = no rise.
var kAirSpeedGain: Double = 0.15
/// Shape of that rise. 1.0 = even, 2.0 = mostly near the limit,
/// 0.7 = already clearly at low speed.
var kAirSpeedCurve: Double = 1.0
/// How gently the speed rise follows your movement (s). Higher = slower swell.
var kAirSpeedSmoothing: Double = 0.35
/// SAFETY GAP: the normal level can never go above (kWarningIntensity − this),
/// so normal (incl. speed rise + close boost) is never mistaken for the warning.
/// 0.30 → normal is capped at 0.60.
var kWarningGapMargin: Double = 0.30
/// TEXTURE: how much the sharpness wiggles (±). Higher = more "hissy" air.
var kAirSharpnessDepth: Double = 0.06
/// SLOW "pressure breathing": each wave lasts a random time in this range (s).
/// Longer = slower, calmer waves.
var kSlowNoiseIntervalMin: Double = 0.50
var kSlowNoiseIntervalMax: Double = 1.20
var kSlowNoiseInterval: ClosedRange<Double> {
    min(kSlowNoiseIntervalMin, kSlowNoiseIntervalMax)...max(kSlowNoiseIntervalMin, kSlowNoiseIntervalMax)
}
/// FAST "flutter": each wiggle lasts a random time in this range (s).
/// Shorter = faster, buzzier flutter.
var kFastNoiseIntervalMin: Double = 0.10
var kFastNoiseIntervalMax: Double = 0.22
var kFastNoiseInterval: ClosedRange<Double> {
    min(kFastNoiseIntervalMin, kFastNoiseIntervalMax)...max(kFastNoiseIntervalMin, kFastNoiseIntervalMax)
}
/// Mix of the two layers: 0 = only slow waves, 1 = only flutter.
var kFastNoiseWeight: Double = 0.30


// ── 1C. HOLDING NOZZLE — TOO FAST (speed warning) ────────────
// The SAME nozzle gets strong and crisp. WHEN it starts: PART 2.

/// Strength just over the speed limit. Higher = stronger warning.
var kWarningIntensity: Double = 0.90
/// Strength when far over the limit (1.0 = motor maximum).
var kWarningMaxIntensity: Double = 1.00
/// How far over the limit (speed units) until kWarningMaxIntensity is reached.
/// Higher = grows more slowly, lower = reaches max sooner.
var kWarningOverspeedRange: Double = 0.5
/// Turbulence left during the warning (1 = all, 0 = steady "solid" warning).
var kWarningTurbulenceScale: Double = 0.5
/// Extra crispness during the warning. Higher = more alarming.
var kWarningSharpnessBoost: Double = 0.15
/// How fast the warning swells IN (s). Lower = snappier.
var kWarningRiseTime: Double = 0.30
/// How fast it goes back to normal (s). Higher = gentler return.
var kWarningFallTime: Double = 0.50


// ── 1D. HOLDING NOZZLE — PRESS / RELEASE ENVELOPE ────────────

/// Pause after the push before the nozzle starts (s), so the push is felt alone.
var kHoldAttackDelay: TimeInterval = 0.04   // CHANGED: was 0.08 (nozzle starts sooner after the push)
/// How fast the nozzle FADES IN after the push (s).
var kHoldAttackTime: Double = 0.05          // CHANGED: was 0.12 (fades in ~2.5× faster)
/// How fast the nozzle FADES OUT after release (s).
var kReleaseTime: Double = 0.04
/// Final smoothing of every level change (s). Keep small (0.03–0.1),
/// otherwise everything feels laggy.
var kOutputSmoothingTime: Double = 0.05
/// NEW: smoothing when the level goes UP (s). Very small = the nozzle jumps up
/// right away when painting becomes possible, or when you come closer.
/// Going DOWN still uses kOutputSmoothingTime (soft, no clicks).
var kOutputRiseTime: Double = 0.012


// ── 1E. BALL COLLISION HAPTIC ────────────────────────────────

/// Hits slower than this (m/s) are ignored. Lower = more / lighter hits count.
/// (was 0.08, CHANGED)
var kCollisionSpeedThreshold: Double = 0.05
/// Minimum time between two hit haptics (s). Stops "buzzing" on an edge.
var kCollisionMinInterval: TimeInterval = 0.06
/// After a hit, the SAME wall fires again only when the ball has moved this far
/// away (points). Higher = fewer double hits from small re-bounces.
var kCollisionRearmDistance: Double = 30
/// No ball hits for this long after pressing (s), so the push is felt cleanly.
var kCollisionMuteAfterPress: TimeInterval = 0.30
/// false = EVERY hit feels the same (the .ahap × kCollisionGain) — your choice.
/// true  = harder hits feel stronger, using the 4 values below.
var kCollisionUseImpactStrength: Bool = false
// ↓ only used when kCollisionUseImpactStrength = true ↓
/// Impact speed (m/s) that gives full strength.
var kCollisionSpeedForMax: Double = 1.2
/// Strength of the lightest hit that still counts (0–1).
var kCollisionMinIntensity: Double = 0.3
/// Curve: < 1 = medium hits feel stronger, > 1 = weaker.
var kCollisionGamma: Double = 0.7
/// Hard hits also get crisper (sharpness added at full strength).
var kCollisionSharpnessBoost: Double = 0.15

// ── DUCKING: the nozzle dips at each hit, so the hit stands out ──
// (A continuous vibration "masks" a short tap. Dipping it for a split second
//  makes the tap feel much stronger; the dip itself is hidden by the hit.)

/// true = dip the nozzle at every hit. false = no dip (to compare).
var kCollisionDuckNozzle: Bool = true
/// How deep the dip is (0–1). 0.8 = nozzle drops to 20 %. 1.0 = fully off.
var kCollisionDuckDepth: Double = 0.8
/// How long the dip lasts (s). Should cover the hit tap(s).
var kCollisionDuckTime: TimeInterval = 0.08
/// How fast the nozzle comes back after the dip (s). Small = quick.
var kCollisionDuckRelease: Double = 0.04


// ── 1F. PROXIMITY (distance to the surface under the cursor) ─
// Unity sends TWO things every frame:
//   • CAN PAINT (yes/no) — the SAME check the painter uses (surface hit AND
//     closer than "Max Distance" on the Painter). This decides ON / FLOOR.
//   • DISTANCE (m)      — only used for the "closer = stronger" boost.
//
//   distance:  0.30 m       0.60 m        Max Distance (1.0 m)
//   ───────────┼─────────────┼─────────────┼──────────────▶
//   strongest  │ stronger as │   normal    │  CAN'T PAINT:
//   (+boost,   │ you come    │   nozzle    │  drops fast to the floor
//    crisper)  │ closer      │             │  (very small, or 0)
//
// Painting ⇒ the full nozzle comes back INSTANTLY (no delay), so
// "painting but no haptic" can't happen. Leaving the range fades out fast.
// The speed warning is never changed by proximity.

/// Proximity on/off. false = always the normal nozzle (useful to compare in a study).
var kProximityEnabled: Bool = true
/// At this distance (m) or closer: maximum closeness cue.
var kProxNear: Double = 0.30
/// From this distance (m) up to Max Distance: normal nozzle, no boost.
var kProxRangeEnd: Double = 0.60
/// Extra strength when very close (added to kAirBaseIntensity: 0.25 → 0.40).
var kProxMaxBoost: Double = 0.15
/// Extra crispness when very close.
var kProxMaxSharpness: Double = 0.10
/// Strength when you CAN'T paint, as a SHARE of the normal level.
/// 0.5 × 0.25 ≈ 0.125 = small but clearly felt ("it's on, but not reaching").
/// Lower = weaker (below ~0.4 it may not be felt at all). 0 = no nozzle at all.
/// (was 0.25, CHANGED)
var kProxFloorFraction: Double = 0.5
/// How fast the nozzle drops to the floor after you leave the paint range (s).
/// (Coming back into range is always instant.)
var kProxOutFadeTime: Double = 0.15
/// Less sharp when out of range (rounder, "distant" feel).
var kProxFarSoftening: Double = 0.20
/// Turbulence left when out of range (fraction) — calmer air.
var kProxFarTurbulence: Double = 0.30
/// Smoothing of the measured distance (s) — removes AR jitter in the boost.
var kProxDistanceSmoothing: Double = 0.15
/// How gently the closeness boost follows distance changes (s).
var kProxFactorSmoothing: Double = 0.12   // CHANGED: was 0.25 (closeness boost reacts faster)


// ████████████████████████████████████████████████████████████
// PART 2 — SPEED DETECTION (when the warning starts / ends)
// ████████████████████████████████████████████████████████████
// "Speed" = how vigorously the phone moves (motion-energy number from the
// motion sensor), NOT meters per second. Shown as "speed" in the debugger.

/// Warning turns ON above this speed. Lower = warns earlier / more often.
var kSpeedWarningEnter: Double = 0.5
/// Warning turns OFF below this speed. Keep LOWER than Enter (prevents flicker).
var kSpeedWarningExit: Double = 0.4
/// Once on, the warning stays on at least this long (s).
var kWarningMinHoldTime: TimeInterval = 0.4
/// Leak of the speed estimate per 1/60 s (stops it drifting up when still).
/// Lower = speed drops faster after you stop moving.
var kVelocityDecay: Double = 0.90
/// How fast the speed value follows UP (s) / DOWN (s).
var kSpeedRiseTime: Double = 0.08
var kSpeedFallTime: Double = 0.25


// ████████████████████████████████████████████████████████████
// PART 3 — BALL PHYSICS (real-world scale)
// ████████████████████████████████████████████████████████████
// The ball feels what the motion sensor feels: gravity (tilt) + inertia
// (moving / shaking the phone) — like a real steel ball in a box.

/// Screen points per real meter (iPhone ≈ 6000). Don't change.
var kPointsPerMeter: Double = 6000
/// Ball diameter in points (50 pt ≈ 8 mm). Bigger = hits walls sooner.
var kBallSize: Double = 50
/// Reaction to TILT. 1.0 = real gravity. Higher = rolls faster.  (was 1.0, CHANGED)
var kGravityScale: Double = 1.3
/// Reaction to MOVING / SHAKING the phone. 1.0 = real. Higher = more sensitive.
/// (was 1.0, CHANGED)
var kInertiaScale: Double = 2.0
/// A rolling ball gets 5/7 of the push (physics). Don't change.
var kRollingFactor: Double = 5.0 / 7.0
/// How quickly the ball slows down. Lower = rolls longer.        (was 0.02, CHANGED)
var kRollingResistance: Double = 0.01
/// Bounce at an edge (0 = dead stop, 1 = perfect bounce). Steel ≈ 0.3–0.5.
var kBallRestitution: Double = 0.4
/// Speed ALONG the wall lost at each hit (0–1).
var kWallFriction: Double = 0.1
/// Physics steps per frame (more = more accurate fast hits). Keep 4.
var kPhysicsSubsteps: Int = 4
/// Smoothing of the motion sensor (s). Higher = lazier, laggy ball.
var kAccelSmoothingTime: Double = 0.015


// ████████████████████████████████████████████████████████████
// PART 5 — SOUND (NEW)
// ████████████████████████████████████████████████████████████
// Two sounds, perfectly in sync with the haptics:
//   • AIR  – a looping hiss while the nozzle is held. Its volume follows the
//            nozzle level EVERY FRAME (incl. turbulence, speed rise, proximity,
//            warning) → the sound "breathes" with the air.
//   • BALL – a short metallic hit at every ball collision.
// Files (optional) in Assets/StreamingAssets/Haptics/:
//     air_nozzle.wav  and  ball_collision.wav   (.m4a / .caf also work)
// If a file is missing, the sound is CREATED in code (hiss / "tink").
// Sound plays in BOTH conditions (A and H) — condition A only switches the
// MOTOR off. Untick Sound Enabled to have no sound at all.

/// All sounds on/off.
var kSoundEnabled: Bool = true
/// Air sound volume when the nozzle is at "Air Sound Full Level" or above (0–1).
var kAirSoundVolume: Double = 0.6
/// Nozzle level that gives the full air volume. Normal hold (≈0.25) → about half;
/// the speed warning (0.9) → full. Lower = louder at normal hold.
var kAirSoundFullLevel: Double = 0.5
/// Curve of volume vs. nozzle level. 1 = straight, < 1 = louder at low levels,
/// > 1 = turbulence changes are heard more strongly.
var kAirSoundCurve: Double = 1.0
/// Smoothing of the air volume (s) — avoids crackles, keeps the turbulence audible.
var kAirSoundSmoothing: Double = 0.03
/// Ball hit sound volume (0–1).
var kCollisionSoundVolume: Double = 0.9
/// After a hit sound starts, new hit sounds are blocked for its length + this (s).
var kCollisionSoundExtraLock: Double = 0.0

let kAirSoundFile = "air_nozzle"
let kCollisionSoundFile = "ball_collision"


// ████████████████████████████████████████████████████████████
// PART 4 — DEBUG / FILES
// ████████████████████████████████████████████████████████████
var kLogCollisions: Bool = true            // prints every hit in the Xcode console
/// NEW: master switch for the study condition. false = condition A: the motor is
/// NEVER driven (no push, nozzle, release, ball hits). Everything is still computed
/// (speed, warning, distance, ball), so the data can be compared with condition H.
/// Set by SessionManager at the start of each session.
var kHapticsEnabled: Bool = true
/// Where Unity puts Assets/StreamingAssets/Haptics inside the iPhone app.
let kHapticsFolder = "/Data/Raw/Haptics/"


// MARK: - Helpers (same as the demo)

enum Wall: CaseIterable {
    case left, right, top, bottom
}

extension Comparable {
    func clamped(to range: ClosedRange<Self>) -> Self {
        min(max(self, range.lowerBound), range.upperBound)
    }
}

/// Frame-rate independent exponential smoothing; tau = time constant (s).
func smooth(_ current: Double, toward target: Double, dt: Double, tau: Double) -> Double {
    guard tau > 0 else { return target }
    let a = 1.0 - exp(-dt / tau)
    return current + (target - current) * a
}

/// Smooth random noise in -1…1 (never periodic, no jumps).
struct SmoothNoise {
    private var from: Double = 0
    private var to: Double = Double.random(in: -1...1)
    private var elapsed: Double = 0
    private var segment: Double
    var intervalRange: ClosedRange<Double>

    init(intervalRange: ClosedRange<Double>) {
        self.intervalRange = intervalRange
        self.segment = Double.random(in: intervalRange)
    }

    mutating func next(dt: Double) -> Double {
        elapsed += dt
        while elapsed >= segment {
            elapsed -= segment
            from = to
            to = Double.random(in: -1...1)
            segment = Double.random(in: intervalRange)
        }
        let u = elapsed / segment
        let s = u * u * (3 - 2 * u)
        return from + (to - from) * s
    }
}

/// ±40 % around a typical duration (REV15).
func range(around t: Double) -> ClosedRange<Double> {
    let t = max(t, 0.02)
    return (t * 0.6)...(t * 1.4)
}

/// Smooth 0…1 S-curve: 0 below edge0, 1 above edge1.
func smoothstep(_ edge0: Double, _ edge1: Double, _ x: Double) -> Double {
    let t = ((x - edge0) / (edge1 - edge0)).clamped(to: 0...1)
    return t * t * (3 - 2 * t)
}

/// Everything the nozzle needs from the proximity, per frame.
struct ProximityOutput {
    var boost: Double = 0            // + intensity when close
    var sharpness: Double = 0        // + / − sharpness
    var fade: Double = 1             // × intensity when far (floor … 1)
    var turbulenceScale: Double = 1  // × turbulence when far
}

/// Proximity: CAN PAINT decides on / floor (instant on, fast fade out);
/// distance gives the "closer = stronger" boost while painting is possible.
struct ProximityModulator {
    private(set) var smoothedDistance: Double? = nil
    private var closeness: Double = 0   // 0 = at kProxRangeEnd or farther, 1 = at kProxNear
    private var farness: Double = 0     // 0 = can paint, 1 = can't paint (floor)

    /// distance in meters (nil = AR tracking lost → keep last value), canPaint from Unity.
    mutating func update(distance: Double?, canPaint: Bool, dt: Double) -> ProximityOutput {
        if let d = distance, d.isFinite, d >= 0 {
            if let current = smoothedDistance {
                smoothedDistance = smooth(current, toward: d, dt: dt, tau: kProxDistanceSmoothing)
            } else {
                smoothedDistance = d
            }
        }

        // ON / FLOOR: instant back to full when painting is possible, quick fade when not.
        if canPaint {
            farness = 0
        } else {
            farness = smooth(farness, toward: 1, dt: dt, tau: kProxOutFadeTime)
        }

        // Closeness boost (only while painting is possible).
        let d = smoothedDistance ?? kProxRangeEnd
        let closeTarget = canPaint ? 1 - smoothstep(kProxNear, kProxRangeEnd, d) : 0
        closeness = smooth(closeness, toward: closeTarget, dt: dt, tau: kProxFactorSmoothing)

        var out = ProximityOutput()
        out.boost           = kProxMaxBoost * closeness
        out.sharpness       = kProxMaxSharpness * closeness - kProxFarSoftening * farness
        out.fade            = 1 - (1 - kProxFloorFraction) * farness
        out.turbulenceScale = 1 - (1 - kProxFarTurbulence) * farness
        return out
    }
}


// MARK: - The haptics system (one shared object for the whole app)

final class HapticNozzle: NSObject {

    static let shared = HapticNozzle()
    private override init() { super.init() }

    // ── CoreHaptics ──
    private var engine: CHHapticEngine?
    private var engineNeedsStart = false
    private var nozzlePlayer: CHHapticAdvancedPatternPlayer?
    private var liveOneShots: [AnyObject] = []
    private var patternCache: [String: CHHapticPattern] = [:]
    private var ahapJSONCache: [String: [String: Any]] = [:]

    // ── Audio (NEW) ──
    private var audioEngine: AVAudioEngine?
    private let airNode = AVAudioPlayerNode()
    private let hitNode = AVAudioPlayerNode()
    private var hitBuffer: AVAudioPCMBuffer?
    private var airBuffer: AVAudioPCMBuffer?
    private var hitDuration: Double = 0
    private var hitBusyUntil: CFTimeInterval = 0
    private var airVolume: Double = 0
    private var nextAudioRestart: CFTimeInterval = 0

    // ── Motion + frame loop ──
    private let motionManager = CMMotionManager()
    private var displayLink: CADisplayLink?
    private var lastFrameTime: CFTimeInterval = 0

    // ── Ball (meters, screen coords: x → right, y → down) ──
    private var screenW: Double = 0   // points
    private var screenH: Double = 0   // points
    private var ballX: Double = 0, ballY: Double = 0
    private var ballVX: Double = 0, ballVY: Double = 0
    private var accX: Double = 0, accY: Double = 0, accZ: Double = 0
    private var ballActive = false
    private var lastCollisionTime: CFTimeInterval = 0
    private var armedWalls: Set<Wall> = Set(Wall.allCases)

    // ── State ──
    private var started = false
    private var isHeld = false
    private var pressTime: CFTimeInterval = 0
    private var integratedSpeed: Double = 0
    private var smoothedSpeed: Double = 0
    private var overSpeed = false
    private var overSpeedSince: CFTimeInterval = 0
    private var speedRise: Double = 0
    private var envelope: Double = 0
    private var warningMix: Double = 0
    private var nozzleOut: Double = 0
    private var duckUntil: CFTimeInterval = 0
    private var duckMul: Double = 1
    private var lastNozzleSharpness: Double = 0

    // ── Proximity ──
    private var proximity = ProximityModulator()
    private var arDistance: Double? = nil      // set by Unity every frame (nil = tracking lost)
    private var canPaint = false               // set by Unity every frame (same check as the painter)
    private var lastProx = ProximityOutput()

    // ── Debug (read by Unity through HapticNozzle_GetDebug) ──
    private var lastSentIntensity: Double = 0
    private var collisionCount = 0
    private var lastHitWall = -1          // 0 left, 1 right, 2 top, 3 bottom
    private var errorCount = 0
    private var fileFound: [String: Bool] = [:]

    private var warnStartSpeed: Double { kSpeedWarningEnter }
    private var warnStopSpeed: Double { warnStartSpeed * (kSpeedWarningExit / kSpeedWarningEnter) }
    private var flutterTime: Double { (kFastNoiseInterval.lowerBound + kFastNoiseInterval.upperBound) / 2 }
    private var waveTime: Double { (kSlowNoiseInterval.lowerBound + kSlowNoiseInterval.upperBound) / 2 }
    private var slowNoise = SmoothNoise(intervalRange: kSlowNoiseInterval)
    private var fastNoise = SmoothNoise(intervalRange: kFastNoiseInterval)


    // MARK: - Called from Unity

    func start() {
        guard !started else { return }   // already running
        started = true
        print("[Haptic] \(paramsReceived) settings received from Unity" + (paramsReceived == 0 ? " (using the values in HapticNozzle.swift)" : ""))

        // CHANGED: first session → create the engine; next sessions → restart it.
        if engine == nil {
            prepareEngine()
        } else {
            engineNeedsStart = true
            ensureEngineRunning()
        }
        // Load everything now, so the first press / hit has no delay.
        _ = pattern(named: "push")
        _ = pattern(named: "releasing_nozzle")
        _ = pattern(named: "holding_nozzle")
        _ = ahapJSON(named: "double_haptic")

        setupAudio()   // NEW: air + ball sounds (first time) / restart (next sessions)

        // Ball box = the real screen (portrait).
        let b = UIScreen.main.bounds
        screenW = Double(min(b.width, b.height))
        screenH = Double(max(b.width, b.height))

        startMotionAndFrameLoop()
        resetBall()          // REV11: bottom centre
        ballActive = true    // REV16: ball rolls from app start
    }

    /// = buttonTouchDown() in the demo
    func press() {
        isHeld = true
        pressTime = CACurrentMediaTime()
        integratedSpeed = 0
        smoothedSpeed = 0
        overSpeed = false
        warningMix = 0
        slowNoise = SmoothNoise(intervalRange: range(around: waveTime))
        fastNoise = SmoothNoise(intervalRange: range(around: flutterTime))
        speedRise = 0

        ensureEngineRunning()
        playOneShot(named: "push")    // the transient "push"
        ensureNozzlePlayer()          // starts silent; envelope fades it in
    }

    /// = buttonTouchUp() in the demo
    func release() {
        isHeld = false
        overSpeed = false
        ensureEngineRunning()
        playOneShot(named: "releasing_nozzle")
        // The nozzle fades out in updateNozzleOutput, then stops.
    }

    /// Distance (m) from the phone to the surface under the cursor.
    /// Negative = AR tracking lost → keep the last good value.
    func setDistance(_ meters: Float) {
        arDistance = meters < 0 ? nil : Double(meters)
    }

    /// true = the painter can paint right now (surface hit within Max Distance).
    func setCanPaint(_ yes: Bool) {
        canPaint = yes
    }

    /// NEW: session over → switch EVERYTHING off (nozzle, ball, motion sensor, engine).
    func stop() {
        isHeld = false
        ballActive = false
        displayLink?.invalidate()
        displayLink = nil
        motionManager.stopDeviceMotionUpdates()
        stopNozzlePlayer()
        liveOneShots.removeAll()
        engine?.stop(completionHandler: nil)
        engineNeedsStart = true
        airVolume = 0
        airNode.volume = 0
        audioEngine?.pause()            // NEW: sounds off
        started = false                 // CHANGED: the next session can start() again
        print("[Haptic] Stopped (session ended)")
    }

    /// NEW: set one tuning value by its name (sent by HapticSettings.cs in Unity).
    func setParam(_ name: String, _ v: Float) {
        let d = Double(v)
        var resetPatterns = false   // gains are baked into the loaded patterns → reload them
        switch name {
        case "kMasterHapticGain": kMasterHapticGain = v; resetPatterns = true
        case "kPushGain": kPushGain = v; resetPatterns = true
        case "kHoldingNozzleGain": kHoldingNozzleGain = v; resetPatterns = true
        case "kReleaseGain": kReleaseGain = v; resetPatterns = true
        case "kCollisionGain": kCollisionGain = v; resetPatterns = true
        case "kAirBaseIntensity": kAirBaseIntensity = d
        case "kAirTurbulenceDepth": kAirTurbulenceDepth = d
        case "kAirSpeedGain": kAirSpeedGain = d
        case "kAirSpeedCurve": kAirSpeedCurve = d
        case "kAirSpeedSmoothing": kAirSpeedSmoothing = d
        case "kWarningGapMargin": kWarningGapMargin = d
        case "kAirSharpnessDepth": kAirSharpnessDepth = d
        case "kSlowNoiseIntervalMin": kSlowNoiseIntervalMin = d
        case "kSlowNoiseIntervalMax": kSlowNoiseIntervalMax = d
        case "kFastNoiseIntervalMin": kFastNoiseIntervalMin = d
        case "kFastNoiseIntervalMax": kFastNoiseIntervalMax = d
        case "kFastNoiseWeight": kFastNoiseWeight = d
        case "kWarningIntensity": kWarningIntensity = d
        case "kWarningMaxIntensity": kWarningMaxIntensity = d
        case "kWarningOverspeedRange": kWarningOverspeedRange = d
        case "kWarningTurbulenceScale": kWarningTurbulenceScale = d
        case "kWarningSharpnessBoost": kWarningSharpnessBoost = d
        case "kWarningRiseTime": kWarningRiseTime = d
        case "kWarningFallTime": kWarningFallTime = d
        case "kHoldAttackDelay": kHoldAttackDelay = d
        case "kHoldAttackTime": kHoldAttackTime = d
        case "kReleaseTime": kReleaseTime = d
        case "kOutputSmoothingTime": kOutputSmoothingTime = d
        case "kOutputRiseTime": kOutputRiseTime = d
        case "kCollisionSpeedThreshold": kCollisionSpeedThreshold = d
        case "kCollisionMinInterval": kCollisionMinInterval = d
        case "kCollisionRearmDistance": kCollisionRearmDistance = d
        case "kCollisionMuteAfterPress": kCollisionMuteAfterPress = d
        case "kCollisionUseImpactStrength": kCollisionUseImpactStrength = v > 0.5
        case "kCollisionSpeedForMax": kCollisionSpeedForMax = d
        case "kCollisionMinIntensity": kCollisionMinIntensity = d
        case "kCollisionGamma": kCollisionGamma = d
        case "kCollisionSharpnessBoost": kCollisionSharpnessBoost = d
        case "kCollisionDuckNozzle": kCollisionDuckNozzle = v > 0.5
        case "kCollisionDuckDepth": kCollisionDuckDepth = d
        case "kCollisionDuckTime": kCollisionDuckTime = d
        case "kCollisionDuckRelease": kCollisionDuckRelease = d
        case "kProximityEnabled": kProximityEnabled = v > 0.5
        case "kProxNear": kProxNear = d
        case "kProxRangeEnd": kProxRangeEnd = d
        case "kProxMaxBoost": kProxMaxBoost = d
        case "kProxMaxSharpness": kProxMaxSharpness = d
        case "kProxFloorFraction": kProxFloorFraction = d
        case "kProxOutFadeTime": kProxOutFadeTime = d
        case "kProxFarSoftening": kProxFarSoftening = d
        case "kProxFarTurbulence": kProxFarTurbulence = d
        case "kProxDistanceSmoothing": kProxDistanceSmoothing = d
        case "kProxFactorSmoothing": kProxFactorSmoothing = d
        case "kSpeedWarningEnter": kSpeedWarningEnter = d
        case "kSpeedWarningExit": kSpeedWarningExit = d
        case "kWarningMinHoldTime": kWarningMinHoldTime = d
        case "kVelocityDecay": kVelocityDecay = d
        case "kSpeedRiseTime": kSpeedRiseTime = d
        case "kSpeedFallTime": kSpeedFallTime = d
        case "kBallSize": kBallSize = d
        case "kGravityScale": kGravityScale = d
        case "kInertiaScale": kInertiaScale = d
        case "kRollingResistance": kRollingResistance = d
        case "kBallRestitution": kBallRestitution = d
        case "kWallFriction": kWallFriction = d
        case "kAccelSmoothingTime": kAccelSmoothingTime = d
        case "kLogCollisions": kLogCollisions = v > 0.5
        case "kSoundEnabled": kSoundEnabled = v > 0.5
        case "kAirSoundVolume": kAirSoundVolume = d
        case "kAirSoundFullLevel": kAirSoundFullLevel = d
        case "kAirSoundCurve": kAirSoundCurve = d
        case "kAirSoundSmoothing": kAirSoundSmoothing = d
        case "kCollisionSoundVolume": kCollisionSoundVolume = d
        case "kCollisionSoundExtraLock": kCollisionSoundExtraLock = d
        case "kHapticsEnabled":
            kHapticsEnabled = v > 0.5
            if !kHapticsEnabled { stopNozzlePlayer() }   // silence right away
            print("[Haptic] Haptics " + (kHapticsEnabled ? "ON (condition H)" : "OFF (condition A)"))
        default:
            print("⚠️ [Haptic] Unknown setting from Unity: \(name)")
            return
        }
        paramsReceived += 1
        if resetPatterns {
            patternCache.removeAll()          // reloaded with the new gain on next use
            if nozzlePlayer != nil { stopNozzlePlayer() }   // recreated next frame if held
        }
    }
    private var paramsReceived = 0


    // MARK: - Frame loop (60 fps): ball → speed → warning → nozzle

    private func startMotionAndFrameLoop() {
        if motionManager.isDeviceMotionAvailable {
            motionManager.deviceMotionUpdateInterval = 1.0 / 60.0
            motionManager.startDeviceMotionUpdates()   // pull mode, read each frame
        } else {
            print("⚠️ [Haptic] Device motion not available.")
        }

        displayLink?.invalidate()
        let link = CADisplayLink(target: self, selector: #selector(frameTick(_:)))
        link.preferredFrameRateRange = CAFrameRateRange(minimum: 60, maximum: 60, preferred: 60)
        link.add(to: .main, forMode: .common)
        displayLink = link
        lastFrameTime = 0
    }

    @objc private func frameTick(_ link: CADisplayLink) {
        let now = link.timestamp
        let dt = lastFrameTime == 0 ? 1.0 / 60.0 : (now - lastFrameTime).clamped(to: 0.001...0.05)
        lastFrameTime = now

        if let motion = motionManager.deviceMotion {
            updateBall(motion: motion, dt: dt)
            updateSpeed(motion: motion, dt: dt)
        }
        updateWarning(now: now)
        updateNozzleOutput(now: now, dt: dt)
    }


    // MARK: - Speed (leaky integral of user-acceleration magnitude)

    private func updateSpeed(motion: CMDeviceMotion, dt: Double) {
        let a = motion.userAcceleration
        let accelMag = sqrt(a.x * a.x + a.y * a.y + a.z * a.z) * 9.81

        integratedSpeed += accelMag * dt
        integratedSpeed *= pow(kVelocityDecay, dt * 60.0)

        let tau = integratedSpeed > smoothedSpeed ? kSpeedRiseTime : kSpeedFallTime
        smoothedSpeed = smooth(smoothedSpeed, toward: integratedSpeed, dt: dt, tau: tau)
    }

    /// Over the limit? (with hysteresis + minimum hold time)
    private func updateWarning(now: CFTimeInterval) {
        guard isHeld else { overSpeed = false; return }

        if !overSpeed, smoothedSpeed > warnStartSpeed {
            overSpeed = true
            overSpeedSince = now
        } else if overSpeed,
                  smoothedSpeed < warnStopSpeed,
                  now - overSpeedSince > kWarningMinHoldTime {
            overSpeed = false
        }
    }


    // MARK: - Holding nozzle output (the ONE place that sets its intensity)

    private func updateNozzleOutput(now: CFTimeInterval, dt: Double) {
        if isHeld {
            ensureEngineRunning()
            ensureNozzlePlayer()
        }

        // 1. Press / release envelope
        let envTarget: Double = (isHeld && now - pressTime >= kHoldAttackDelay) ? 1 : 0
        envelope = smooth(envelope, toward: envTarget, dt: dt,
                          tau: isHeld ? kHoldAttackTime : kReleaseTime)

        // 2. Warning amount 0 → 1
        warningMix = smooth(warningMix, toward: overSpeed ? 1 : 0, dt: dt,
                            tau: overSpeed ? kWarningRiseTime : kWarningFallTime)

        // 3. Air turbulence: slow + fast smooth noise
        let slow = slowNoise.next(dt: dt)
        let fast = fastNoise.next(dt: dt)
        let air = (1 - kFastNoiseWeight) * slow + kFastNoiseWeight * fast

        // 3b. NEW: proximity (closer = boost, too far = fade to the floor)
        let prox = kProximityEnabled ? proximity.update(distance: arDistance, canPaint: canPaint, dt: dt) : ProximityOutput()
        lastProx = prox

        // 4. NORMAL level: (base + closeness boost + middle-speed rise) × far fade,
        //    always capped below the warning
        let speedNorm = (smoothedSpeed / warnStartSpeed).clamped(to: 0...1)
        let speedCurve = pow(speedNorm, kAirSpeedCurve)
        speedRise = smooth(speedRise, toward: kAirSpeedGain * speedCurve, dt: dt, tau: kAirSpeedSmoothing)
        let normalLevel = min((kAirBaseIntensity + prox.boost + speedRise) * prox.fade,
                              kWarningIntensity - kWarningGapMargin)

        // 5. TOO-FAST level
        let over = ((smoothedSpeed - warnStartSpeed) / kWarningOverspeedRange).clamped(to: 0...1)
        let warningLevel = kWarningIntensity + (kWarningMaxIntensity - kWarningIntensity) * over

        // 6. Blend
        let target = (normalLevel + (warningLevel - normalLevel) * warningMix).clamped(to: 0...1)

        // 7. Smooth the level
        //    CHANGED: going UP is almost instant (kOutputRiseTime), going DOWN stays soft.
        let levelTarget = target * envelope
        let outTau = levelTarget > nozzleOut ? kOutputRiseTime : kOutputSmoothingTime
        nozzleOut = smooth(nozzleOut, toward: levelTarget, dt: dt, tau: outTau)

        // 7b. Turbulence AFTER smoothing (REV14), less during the warning
        let turbScale = 1 - (1 - kWarningTurbulenceScale) * warningMix
        let turbulence = kAirTurbulenceDepth * turbScale * prox.turbulenceScale * prox.fade * air * envelope

        // 8. Sharpness: air texture + closeness texture (fades out during the warning)
        //    + crispness while too fast
        let sharpness = kAirSharpnessDepth * fast * prox.turbulenceScale
                      + prox.sharpness * (1 - warningMix)
                      + kWarningSharpnessBoost * warningMix

        // REV13: ducking — instant dip at a hit, smooth quick recovery
        let duckTarget = (kCollisionDuckNozzle && now < duckUntil) ? (1 - kCollisionDuckDepth) : 1
        if duckTarget < duckMul {
            duckMul = duckTarget
        } else {
            duckMul = smooth(duckMul, toward: duckTarget, dt: dt, tau: kCollisionDuckRelease)
        }
        lastNozzleSharpness = sharpness
        let sent = ((nozzleOut + turbulence) * duckMul).clamped(to: 0...1)
        lastSentIntensity = sent
        send(to: nozzlePlayer, intensity: sent, sharpness: sharpness)

        // NEW: air sound follows the nozzle level (incl. turbulence), without the
        // ducking dip (that is a haptic trick; the hit has its own sound).
        updateAirSound(level: (nozzleOut + turbulence).clamped(to: 0...1), now: now, dt: dt)

        // 9. After release, once faded out, stop the player
        if !isHeld, envelope < 0.005, nozzlePlayer != nil {
            stopNozzlePlayer()
        }
    }

    private func send(to player: CHHapticAdvancedPatternPlayer?, intensity: Double, sharpness: Double) {
        guard kHapticsEnabled, let player = player else { return }   // condition A: nothing to the motor
        try? player.sendParameters([
            CHHapticDynamicParameter(parameterID: .hapticIntensityControl,
                                     value: Float(intensity.clamped(to: 0...1)), relativeTime: 0),
            CHHapticDynamicParameter(parameterID: .hapticSharpnessControl,
                                     value: Float(sharpness.clamped(to: -1...1)), relativeTime: 0)
        ], atTime: CHHapticTimeImmediate)
    }


    // MARK: - Ball

    private func resetBall() {
        ballVX = 0
        ballVY = 0
        let r = (kBallSize / 2) / kPointsPerMeter
        ballX = (screenW / 2) / kPointsPerMeter          // centre
        ballY = screenH / kPointsPerMeter - r            // resting on the bottom edge
        armedWalls = Set(Wall.allCases)
        armedWalls.remove(.bottom)                       // already lying on it
        if let m = motionManager.deviceMotion {
            let a = apparentAcceleration(m)
            accX = a.x; accY = a.y; accZ = a.z
        }
    }

    private func updateBall(motion: CMDeviceMotion, dt: Double) {
        guard ballActive else { return }
        let a = apparentAcceleration(motion)
        accX = smooth(accX, toward: a.x, dt: dt, tau: kAccelSmoothingTime)
        accY = smooth(accY, toward: a.y, dt: dt, tau: kAccelSmoothingTime)
        accZ = smooth(accZ, toward: a.z, dt: dt, tau: kAccelSmoothingTime)
        stepBall(dt: dt)
        rearmWalls()
    }

    /// Gravity (tilt) + inertia, in m/s², screen coords.
    private func apparentAcceleration(_ m: CMDeviceMotion) -> (x: Double, y: Double, z: Double) {
        let g = m.gravity
        let u = m.userAcceleration
        let x = (g.x * kGravityScale + u.x * kInertiaScale) * 9.81
        let y = (g.y * kGravityScale + u.y * kInertiaScale) * 9.81
        let z = (g.z * kGravityScale + u.z * kInertiaScale) * 9.81
        return (x, -y, z)   // CoreMotion y up → screen y down
    }

    private func stepBall(dt: Double) {
        let r = (kBallSize / 2) / kPointsPerMeter
        let w = screenW / kPointsPerMeter
        let h = screenH / kPointsPerMeter
        let steps = max(1, kPhysicsSubsteps)
        let sdt = dt / Double(steps)
        let normal = abs(accZ)

        for _ in 0..<steps {
            // 1. Tilt + inertia (rolling ball gets 5/7)
            ballVX += accX * kRollingFactor * sdt
            ballVY += accY * kRollingFactor * sdt

            // 2. Rolling resistance
            let speed = (ballVX * ballVX + ballVY * ballVY).squareRoot()
            let loss = kRollingResistance * normal * sdt
            if speed <= loss {
                ballVX = 0; ballVY = 0
            } else {
                let k = (speed - loss) / speed
                ballVX *= k; ballVY *= k
            }

            // 3. Move
            ballX += ballVX * sdt
            ballY += ballVY * sdt

            // 4. Walls
            if ballX < r {
                ballX = r
                if ballVX < 0 {
                    wallHit(.left, impact: -ballVX)
                    ballVX = -ballVX * kBallRestitution
                    ballVY *= (1 - kWallFriction)
                }
            } else if ballX > w - r {
                ballX = w - r
                if ballVX > 0 {
                    wallHit(.right, impact: ballVX)
                    ballVX = -ballVX * kBallRestitution
                    ballVY *= (1 - kWallFriction)
                }
            }
            if ballY < r {
                ballY = r
                if ballVY < 0 {
                    wallHit(.top, impact: -ballVY)
                    ballVY = -ballVY * kBallRestitution
                    ballVX *= (1 - kWallFriction)
                }
            } else if ballY > h - r {
                ballY = h - r
                if ballVY > 0 {
                    wallHit(.bottom, impact: ballVY)
                    ballVY = -ballVY * kBallRestitution
                    ballVX *= (1 - kWallFriction)
                }
            }
        }
    }

    /// Distance from the ball's edge to a wall, in points (0 = touching).
    private func gap(to wall: Wall) -> Double {
        let cx = ballX * kPointsPerMeter
        let cy = ballY * kPointsPerMeter
        let r = kBallSize / 2
        switch wall {
        case .left:   return cx - r
        case .right:  return screenW - (cx + r)
        case .top:    return cy - r
        case .bottom: return screenH - (cy + r)
        }
    }

    private func rearmWalls() {
        for wall in Wall.allCases where !armedWalls.contains(wall) {
            if gap(to: wall) > kCollisionRearmDistance { armedWalls.insert(wall) }
        }
    }

    private func wallHit(_ wall: Wall, impact: Double) {
        let now = CACurrentMediaTime()
        guard now - pressTime >= kCollisionMuteAfterPress else { return }
        guard now - lastCollisionTime >= kCollisionMinInterval else { return }
        guard armedWalls.contains(wall) else { return }
        guard impact >= kCollisionSpeedThreshold else { return }
        lastCollisionTime = now
        armedWalls.remove(wall)
        collisionCount += 1
        switch wall {
        case .left: lastHitWall = 0
        case .right: lastHitWall = 1
        case .top: lastHitWall = 2
        case .bottom: lastHitWall = 3
        }

        let n = ((impact - kCollisionSpeedThreshold)
                 / (kCollisionSpeedForMax - kCollisionSpeedThreshold)).clamped(to: 0...1)
        let shaped = pow(n, kCollisionGamma)
        let intensity = kCollisionUseImpactStrength
            ? kCollisionMinIntensity + (1 - kCollisionMinIntensity) * shaped
            : 1.0
        let sharpnessBoost = kCollisionUseImpactStrength ? kCollisionSharpnessBoost * n : 0

        if kLogCollisions {
            let played = min(intensity * Double(gain(for: "double_haptic")), 1.0)
            print(String(format: "[Collision] %@ wall  impact %.2f m/s  → haptic %.2f%@",
                         "\(wall)", impact, played, played >= 1.0 ? " (MAX)" : ""))
        }
        playCollision(impact: Float(intensity), sharpnessBoost: Float(sharpnessBoost))   // haptic (H only)
        playHitSound(impact: Float(intensity))                                            // NEW: sound (A and H)
    }

    /// Collision haptic (no sound in this version).
    private func playCollision(impact: Float, sharpnessBoost: Float) {
        guard kHapticsEnabled else { return }   // condition A: no hit haptic
        ensureEngineRunning()
        guard let e = engine else { return }

        let scale = impact * gain(for: "double_haptic")
        var p: CHHapticPattern? = nil
        if let json = ahapJSON(named: "double_haptic") {
            p = makePattern(from: json, intensityScale: scale, sharpnessBoost: sharpnessBoost)
        }
        if p == nil {
            p = fallbackCollisionPattern(intensity: min(scale, 1), sharpnessBoost: sharpnessBoost)
        }
        guard let pattern = p else { return }

        // REV13: dip the nozzle RIGHT NOW
        if kCollisionDuckNozzle {
            duckUntil = CACurrentMediaTime() + kCollisionDuckTime
            duckMul = 1 - kCollisionDuckDepth
            send(to: nozzlePlayer, intensity: nozzleOut * duckMul, sharpness: lastNozzleSharpness)
        }

        do {
            let player = try e.makePlayer(with: pattern)
            try player.start(atTime: CHHapticTimeImmediate)
            retain(player, for: pattern.duration)
        } catch {
            errorCount += 1
            print("[Haptic] Collision failed: \(error)")
        }
    }

    private func fallbackCollisionPattern(intensity: Float, sharpnessBoost: Float) -> CHHapticPattern? {
        let base: Float = 0.75
        return try? CHHapticPattern(events: [
            CHHapticEvent(eventType: .hapticTransient, parameters: [
                CHHapticEventParameter(parameterID: .hapticIntensity, value: intensity),
                CHHapticEventParameter(parameterID: .hapticSharpness, value: min(base + sharpnessBoost, 1))
            ], relativeTime: 0),
            CHHapticEvent(eventType: .hapticTransient, parameters: [
                CHHapticEventParameter(parameterID: .hapticIntensity, value: intensity * 0.45),
                CHHapticEventParameter(parameterID: .hapticSharpness, value: min(base + sharpnessBoost, 1) - 0.1)
            ], relativeTime: 0.06)
        ], parameters: [])
    }


    // MARK: - Sound (NEW)

    private func setupAudio() {
        if let ae = audioEngine {               // next session: just restart
            try? ae.start()
            startAirLoop()
            return
        }
        do {
            // .playback = plays even with the silent switch on; mixWithOthers = keep Unity's audio
            try AVAudioSession.sharedInstance().setCategory(.playback, mode: .default, options: [.mixWithOthers])
            try AVAudioSession.sharedInstance().setActive(true)
        } catch {
            print("[Audio] Session setup failed: \(error)")
        }

        airBuffer = loadSound(named: kAirSoundFile) ?? makeAirHiss(seconds: 3)
        hitBuffer = loadSound(named: kCollisionSoundFile) ?? makeMetalTink()
        if let hb = hitBuffer { hitDuration = Double(hb.frameLength) / hb.format.sampleRate }

        let ae = AVAudioEngine()
        ae.attach(airNode)
        ae.attach(hitNode)
        if let ab = airBuffer { ae.connect(airNode, to: ae.mainMixerNode, format: ab.format) }
        if let hb = hitBuffer { ae.connect(hitNode, to: ae.mainMixerNode, format: hb.format) }
        ae.prepare()
        do {
            try ae.start()
        } catch {
            print("[Audio] Engine start failed: \(error)")
        }
        audioEngine = ae

        startAirLoop()
        print(String(format: "[Audio] Ready (air loop %@, hit %.2f s)",
                     airBuffer != nil ? "ok" : "MISSING", hitDuration))
    }

    /// The air loop plays all the time at volume 0; its volume is driven every frame.
    /// (Re-scheduled if the audio was interrupted, e.g. by a phone call.)
    private func startAirLoop() {
        guard let ab = airBuffer, let ae = audioEngine, ae.isRunning, !airNode.isPlaying else { return }
        airNode.volume = 0
        airNode.scheduleBuffer(ab, at: nil, options: [.loops], completionHandler: nil)
        airNode.play()
    }

    /// Every frame: air volume from the nozzle level.
    private func updateAirSound(level: Double, now: CFTimeInterval, dt: Double) {
        guard let ae = audioEngine else { return }
        if !ae.isRunning {                      // e.g. after a phone call — try again once per second
            if now >= nextAudioRestart {
                nextAudioRestart = now + 1
                try? ae.start()
                startAirLoop()
            }
            return
        }
        var target = 0.0
        if kSoundEnabled, kAirSoundFullLevel > 0 {
            let x = (level / kAirSoundFullLevel).clamped(to: 0...1)
            target = kAirSoundVolume * pow(x, max(kAirSoundCurve, 0.01))
        }
        airVolume = smooth(airVolume, toward: target, dt: dt, tau: kAirSoundSmoothing)
        airNode.volume = Float(airVolume.clamped(to: 0...1))
    }

    /// Ball hit sound, with the lock: while it plays, new hit sounds are skipped.
    private func playHitSound(impact: Float) {
        guard kSoundEnabled, let ae = audioEngine, let buffer = hitBuffer else { return }
        let now = CACurrentMediaTime()
        guard now >= hitBusyUntil else { return }
        hitBusyUntil = now + hitDuration + kCollisionSoundExtraLock
        if !ae.isRunning { try? ae.start() }
        hitNode.stop()
        hitNode.volume = Float(kCollisionSoundVolume) * impact
        hitNode.scheduleBuffer(buffer, at: nil, options: [], completionHandler: nil)
        hitNode.play()
    }

    /// Loads a sound from StreamingAssets/Haptics (wav, m4a or caf). nil = not found.
    private func loadSound(named name: String) -> AVAudioPCMBuffer? {
        for ext in ["wav", "m4a", "caf"] {
            guard let url = fileURL(named: name, ext: ext) else { continue }
            do {
                let file = try AVAudioFile(forReading: url)
                guard let buf = AVAudioPCMBuffer(pcmFormat: file.processingFormat,
                                                 frameCapacity: AVAudioFrameCount(file.length)) else { return nil }
                try file.read(into: buf)
                print("[Audio] Loaded \(name).\(ext)")
                return buf
            } catch {
                print("[Audio] Could not read \(name).\(ext): \(error)")
            }
        }
        print("[Audio] \(name) not found — using the sound made in code")
        return nil
    }

    /// Made-in-code air hiss: band-limited noise (~1–6 kHz), seamless loop.
    private func makeAirHiss(seconds: Double) -> AVAudioPCMBuffer? {
        let sr = 44100.0
        guard let fmt = AVAudioFormat(standardFormatWithSampleRate: sr, channels: 1) else { return nil }
        let n = Int(sr * seconds), fade = Int(sr * 0.1)
        var raw = [Float](repeating: 0, count: n + fade)
        var lpHi: Float = 0, lpLo: Float = 0
        let aHi = Float(1 - exp(-2 * Double.pi * 6000 / sr))   // removes above ~6 kHz
        let aLo = Float(1 - exp(-2 * Double.pi * 1000 / sr))   // removes below ~1 kHz
        for i in 0..<raw.count {
            let white = Float.random(in: -1...1)
            lpHi += aHi * (white - lpHi)
            lpLo += aLo * (lpHi - lpLo)
            raw[i] = (lpHi - lpLo) * 1.6
        }
        guard let buf = AVAudioPCMBuffer(pcmFormat: fmt, frameCapacity: AVAudioFrameCount(n)),
              let ch = buf.floatChannelData?[0] else { return nil }
        for i in 0..<n {
            var v = raw[i]
            if i < fade {                       // cross-fade the end into the start → no click at the loop
                let t = Float(i) / Float(fade)
                v = raw[i] * t + raw[n + i] * (1 - t)
            }
            ch[i] = v.clamped(to: -1...1)
        }
        buf.frameLength = AVAudioFrameCount(n)
        return buf
    }

    /// Made-in-code metal "tink": a few high tones that die out fast + a tiny click.
    private func makeMetalTink() -> AVAudioPCMBuffer? {
        let sr = 44100.0, seconds = 0.12
        guard let fmt = AVAudioFormat(standardFormatWithSampleRate: sr, channels: 1) else { return nil }
        let n = Int(sr * seconds)
        guard let buf = AVAudioPCMBuffer(pcmFormat: fmt, frameCapacity: AVAudioFrameCount(n)),
              let ch = buf.floatChannelData?[0] else { return nil }
        let tones: [(f: Double, a: Double, tau: Double)] = [(2900, 0.5, 0.030), (4700, 0.3, 0.020), (6800, 0.2, 0.012)]
        for i in 0..<n {
            let t = Double(i) / sr
            var v = 0.0
            for tone in tones { v += tone.a * sin(2 * Double.pi * tone.f * t) * exp(-t / tone.tau) }
            if t < 0.003 { v += Double.random(in: -0.6...0.6) * (1 - t / 0.003) }   // click
            ch[i] = Float(v * 0.9).clamped(to: -1...1)
        }
        buf.frameLength = AVAudioFrameCount(n)
        return buf
    }


    // MARK: - CoreHaptics engine

    private func prepareEngine() {
        guard CHHapticEngine.capabilitiesForHardware().supportsHaptics else {
            print("⚠️ [Haptic] Device does not support haptics.")
            return
        }
        do {
            let e = try CHHapticEngine()
            e.playsHapticsOnly = true
            e.stoppedHandler = { [weak self] reason in
                print("[Haptic] Engine stopped: \(reason.rawValue)")
                DispatchQueue.main.async { self?.invalidatePlayers() }
            }
            e.resetHandler = { [weak self] in
                print("[Haptic] Engine reset — will restart")
                DispatchQueue.main.async { self?.invalidatePlayers() }
            }
            try e.start()
            engine = e
            print("[Haptic] Engine started")
        } catch {
            errorCount += 1
            print("[Haptic] Engine creation failed: \(error)")
        }
    }

    private func invalidatePlayers() {
        nozzlePlayer = nil
        liveOneShots.removeAll()
        engineNeedsStart = true
    }

    private func ensureEngineRunning() {
        guard engineNeedsStart, let e = engine else { return }
        do {
            try e.start()
            engineNeedsStart = false
        } catch {
            errorCount += 1
            print("[Haptic] Engine restart failed: \(error)")
        }
    }


    // MARK: - Nozzle player

    private func ensureNozzlePlayer() {
        guard kHapticsEnabled else { return }   // condition A: no nozzle
        guard nozzlePlayer == nil, let e = engine, !engineNeedsStart else { return }
        do {
            let p = try pattern(named: "holding_nozzle") ?? makeContinuousFallback(sharpness: 0.3)
            let player = try e.makeAdvancedPlayer(with: p)
            player.loopEnabled = true
            let silent = [CHHapticDynamicParameter(parameterID: .hapticIntensityControl,
                                                   value: 0, relativeTime: 0)]
            try? player.sendParameters(silent, atTime: CHHapticTimeImmediate)
            try player.start(atTime: CHHapticTimeImmediate)
            try? player.sendParameters(silent, atTime: CHHapticTimeImmediate)
            nozzlePlayer = player
            nozzleOut = 0
        } catch {
            errorCount += 1
            print("[Haptic] Nozzle player failed: \(error)")
        }
    }

    private func makeContinuousFallback(sharpness: Float) throws -> CHHapticPattern {
        let event = CHHapticEvent(eventType: .hapticContinuous, parameters: [
            CHHapticEventParameter(parameterID: .hapticIntensity, value: 1.0),
            CHHapticEventParameter(parameterID: .hapticSharpness, value: sharpness)
        ], relativeTime: 0, duration: 30)
        return try CHHapticPattern(events: [event], parameters: [])
    }

    private func stopNozzlePlayer() {
        try? nozzlePlayer?.stop(atTime: CHHapticTimeImmediate)
        nozzlePlayer = nil
        nozzleOut = 0
    }


    // MARK: - Loading .ahap / .wav files

    private func gain(for name: String) -> Float {
        let perPattern: Float
        switch name {
        case "push":             perPattern = kPushGain
        case "holding_nozzle":   perPattern = kHoldingNozzleGain
        case "releasing_nozzle": perPattern = kReleaseGain
        case "double_haptic":    perPattern = kCollisionGain
        default:                 perPattern = 1.0
        }
        return kMasterHapticGain * perPattern
    }

    /// Looks in Unity's StreamingAssets/Haptics first, then in the app bundle.
    private func fileURL(named name: String, ext: String) -> URL? {
        let path = Bundle.main.bundlePath + kHapticsFolder + name + "." + ext
        if FileManager.default.fileExists(atPath: path) { return URL(fileURLWithPath: path) }
        return Bundle.main.url(forResource: name, withExtension: ext)
    }

    private func ahapJSON(named name: String) -> [String: Any]? {
        if let cached = ahapJSONCache[name] { return cached }
        guard let url = fileURL(named: name, ext: "ahap") else {
            fileFound[name] = false
            print("⚠️ [Haptic] \(name).ahap not found — using fallback")
            return nil
        }
        guard let data = try? Data(contentsOf: url),
              let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            fileFound[name] = false
            print("[Haptic] Could not read \(name).ahap as JSON.")
            return nil
        }
        fileFound[name] = true
        ahapJSONCache[name] = obj
        return obj
    }

    /// Pattern WITH its strength handle applied (cached).
    /// Returns nil if the file is missing (callers use their own fallback).
    private func pattern(named name: String) -> CHHapticPattern? {
        if let cached = patternCache[name] { return cached }
        guard let json = ahapJSON(named: name) else {
            // push / release: simple tap so there is still feedback
            if name == "push" || name == "releasing_nozzle", let fb = fallbackTransient(intensity: min(gain(for: name), 1)) {
                patternCache[name] = fb
                return fb
            }
            return nil
        }
        let g = gain(for: name)
        guard let p = makePattern(from: json, intensityScale: g, sharpnessBoost: 0) else { return nil }
        let loudest = maxIntensity(in: json)
        print(String(format: "[Haptic] %@.ahap  gain %.2f  loudest %.2f → %.2f",
                     name, g, loudest, min(loudest * g, 1)))
        patternCache[name] = p
        return p
    }

    private func makePattern(from json: [String: Any],
                             intensityScale: Float,
                             sharpnessBoost: Float) -> CHHapticPattern? {
        var dict = json
        guard let items = dict["Pattern"] as? [[String: Any]] else { return nil }

        dict["Pattern"] = items.map { item -> [String: Any] in
            guard var event = item["Event"] as? [String: Any],
                  let params = event["EventParameters"] as? [[String: Any]] else { return item }
            event["EventParameters"] = params.map { param -> [String: Any] in
                var param = param
                guard let id = param["ParameterID"] as? String,
                      let value = (param["ParameterValue"] as? NSNumber)?.floatValue else { return param }
                if id == "HapticIntensity" {
                    param["ParameterValue"] = (value * intensityScale).clamped(to: 0...1)
                } else if id == "HapticSharpness" {
                    param["ParameterValue"] = (value + sharpnessBoost).clamped(to: 0...1)
                }
                return param
            }
            var newItem = item
            newItem["Event"] = event
            return newItem
        }

        var typed: [CHHapticPattern.Key: Any] = [:]
        for (key, value) in dict { typed[CHHapticPattern.Key(rawValue: key)] = value }
        do {
            return try CHHapticPattern(dictionary: typed)
        } catch {
            print("[Haptic] Building scaled pattern failed: \(error)")
            return nil
        }
    }

    private func maxIntensity(in json: [String: Any]) -> Float {
        guard let items = json["Pattern"] as? [[String: Any]] else { return 0 }
        var loudest: Float = 0
        for item in items {
            guard let event = item["Event"] as? [String: Any],
                  let params = event["EventParameters"] as? [[String: Any]] else { continue }
            for param in params where (param["ParameterID"] as? String) == "HapticIntensity" {
                if let v = (param["ParameterValue"] as? NSNumber)?.floatValue { loudest = max(loudest, v) }
            }
        }
        return loudest
    }

    private func fallbackTransient(intensity: Float) -> CHHapticPattern? {
        let event = CHHapticEvent(eventType: .hapticTransient, parameters: [
            CHHapticEventParameter(parameterID: .hapticIntensity, value: intensity),
            CHHapticEventParameter(parameterID: .hapticSharpness, value: 0.5)
        ], relativeTime: 0)
        return try? CHHapticPattern(events: [event], parameters: [])
    }


    // MARK: - Debug values for Unity (HapticNozzle_GetDebug)

    func debugValue(_ id: Int32) -> Float {
        func b(_ v: Bool) -> Float { v ? 1 : 0 }
        func f(_ name: String) -> Float { fileFound[name] == true ? 1 : 0 }
        switch id {
        case 0:  return b(engine != nil && !engineNeedsStart)     // engine running
        case 1:  return b(motionManager.isDeviceMotionActive)     // motion sensor on
        case 2:  return Float(errorCount)
        case 3:  return f("push")
        case 4:  return f("releasing_nozzle")
        case 5:  return f("holding_nozzle")
        case 6:  return f("double_haptic")
        case 7:  return b(isHeld)                                 // finger down
        case 8:  return b(nozzlePlayer != nil)                    // nozzle player exists
        case 9:  return Float(smoothedSpeed)
        case 10: return Float(warnStartSpeed)
        case 11: return b(overSpeed)
        case 12: return Float(warningMix)
        case 13: return Float(envelope)
        case 14: return kHapticsEnabled ? Float(lastSentIntensity) : 0   // value REALLY sent to the motor (0 in condition A)
        case 15: return Float(lastNozzleSharpness)
        case 16: return screenW > 0 ? Float(ballX * kPointsPerMeter / screenW) : 0.5   // 0 left … 1 right
        case 17: return screenH > 0 ? Float(ballY * kPointsPerMeter / screenH) : 1     // 0 top … 1 bottom
        case 18: return Float((ballVX * ballVX + ballVY * ballVY).squareRoot())         // m/s
        case 19: return Float(collisionCount)
        case 20: return Float(lastHitWall)
        case 21: return Float(proximity.smoothedDistance ?? -1)   // m (smoothed)
        case 22: return Float(lastProx.boost)
        case 23: return Float(lastProx.fade)
        case 24: return b(canPaint)
        case 25: return Float(airVolume)                           // NEW: air sound volume
        default: return 0
        }
    }


    // MARK: - One-shots

    private func playOneShot(named name: String) {
        guard kHapticsEnabled else { return }   // condition A: no push / release
        guard let e = engine, let p = pattern(named: name) else { return }
        do {
            let player = try e.makePlayer(with: p)
            try player.start(atTime: CHHapticTimeImmediate)
            retain(player, for: p.duration)
        } catch {
            errorCount += 1
            print("[Haptic] One-shot \(name) failed: \(error)")
        }
    }

    private func retain(_ player: CHHapticPatternPlayer, for duration: TimeInterval) {
        let obj = player as AnyObject
        liveOneShots.append(obj)
        DispatchQueue.main.asyncAfter(deadline: .now() + duration + 0.2) { [weak self] in
            self?.liveOneShots.removeAll { $0 === obj }
        }
    }
}


// MARK: - The 4 functions Unity calls (names must match HapticsBridge.cs)

@_cdecl("HapticNozzle_Start")
public func HapticNozzle_Start() { HapticNozzle.shared.start() }

@_cdecl("HapticNozzle_Press")
public func HapticNozzle_Press() { HapticNozzle.shared.press() }

@_cdecl("HapticNozzle_Release")
public func HapticNozzle_Release() { HapticNozzle.shared.release() }

@_cdecl("HapticNozzle_SetDistance")
public func HapticNozzle_SetDistance(_ meters: Float) { HapticNozzle.shared.setDistance(meters) }

@_cdecl("HapticNozzle_SetParam")
public func HapticNozzle_SetParam(_ name: UnsafePointer<CChar>, _ value: Float) {
    HapticNozzle.shared.setParam(String(cString: name), value)
}

@_cdecl("HapticNozzle_Stop")
public func HapticNozzle_Stop() { HapticNozzle.shared.stop() }

@_cdecl("HapticNozzle_SetCanPaint")
public func HapticNozzle_SetCanPaint(_ yes: Int32) { HapticNozzle.shared.setCanPaint(yes != 0) }

@_cdecl("HapticNozzle_GetDebug")
public func HapticNozzle_GetDebug(_ id: Int32) -> Float { HapticNozzle.shared.debugValue(id) }
