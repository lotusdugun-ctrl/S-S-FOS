/**
 * The bits that differ when the game runs as a store app (Capacitor) rather
 * than in a browser: vibration goes through the native haptics engine (iOS has
 * no navigator.vibrate at all), the music stops when the app is sent to the
 * background, and Android's back button closes a menu before it leaves.
 *
 * Everything here is a no-op in a browser, and the plugins are only imported
 * when actually running native, so the website does not carry them.
 */
import { Capacitor } from "@capacitor/core";

export const isNative = (): boolean => {
  try {
    return Capacitor.isNativePlatform();
  } catch {
    return false;
  }
};

type HapticsMod = typeof import("@capacitor/haptics");
let haptics: Promise<HapticsMod> | null = null;
const loadHaptics = () => (haptics ??= import("@capacitor/haptics"));

/**
 * Play a vibration pattern in the navigator.vibrate shape: a duration, or
 * alternating on/off durations. Natively each "on" becomes an impact, its
 * strength chosen by how long the browser version would have buzzed.
 */
export function vibrate(pattern: number | number[]): void {
  if (!isNative()) {
    try {
      navigator.vibrate?.(pattern);
    } catch {
      /* not allowed here */
    }
    return;
  }
  const steps = typeof pattern === "number" ? [pattern] : pattern;
  void loadHaptics().then(({ Haptics, ImpactStyle }) => {
    let at = 0;
    steps.forEach((ms, i) => {
      if (i % 2 === 0 && ms > 0) {
        const style =
          ms >= 60 ? ImpactStyle.Heavy : ms >= 20 ? ImpactStyle.Medium : ImpactStyle.Light;
        window.setTimeout(() => void Haptics.impact({ style }).catch(() => {}), at);
      }
      at += ms;
    });
  });
}

/** vibration is available: a phone browser that has it, or any native app */
export const canVibrate = (): boolean =>
  isNative() || (typeof navigator !== "undefined" && "vibrate" in navigator);

/**
 * App lifecycle: `onPause` when the app goes to the background (music off),
 * `onResume` when it comes back, and `onBack` for Android's back button —
 * return true if it was used (a menu closed), false to leave the app.
 */
export function watchApp(handlers: {
  onPause: () => void;
  onResume: () => void;
  onBack: () => boolean;
}): () => void {
  if (!isNative()) return () => {};
  let off: Array<() => void> = [];
  let cancelled = false;
  void import("@capacitor/app").then(async ({ App }) => {
    const subs = await Promise.all([
      App.addListener("appStateChange", ({ isActive }) =>
        isActive ? handlers.onResume() : handlers.onPause(),
      ),
      App.addListener("backButton", () => {
        if (!handlers.onBack()) void App.minimizeApp();
      }),
    ]);
    off = subs.map((s) => () => void s.remove());
    if (cancelled) off.forEach((f) => f());
  });
  return () => {
    cancelled = true;
    off.forEach((f) => f());
  };
}
