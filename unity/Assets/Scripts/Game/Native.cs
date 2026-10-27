// Ported from src/game/native.ts.
/*
 * The bits that differ when the game runs as a store app rather than in a browser:
 * vibration goes through the native haptics engine, the music stops when the app is
 * sent to the background, and Android's back button closes a menu before it leaves.
 *
 * PORT: Capacitor is gone. The Unity host plugs in what the platform can do:
 *   Native.native        — whether this is a store app (true by default: a Unity build is);
 *   Native.impact        — play one haptic impact ("light" | "medium" | "heavy") after a delay
 *                          in ms (the web scheduled them with setTimeout); used when native;
 *   Native.browserVibrate — navigator.vibrate(pattern), used when not native;
 *   Native.minimizeApp   — Android's "leave the app" when the back button was not used;
 *   and it calls Native.appPaused(), appResumed() and backButton() from its lifecycle hooks.
 * Every delegate is a no-op until the host sets it.
 */
using System;
using System.Collections.Generic;

namespace Sisifos.Game
{
    public sealed class AppHandlers
    {
        public Action onPause;
        public Action onResume;
        /// <summary>true if it was used (a menu closed), false to leave the app</summary>
        public Func<bool> onBack;
    }

    public static class Native
    {
        public static bool native = true;
        public static Action<string, double> impact = null;
        public static Action<double[]> browserVibrate = null;
        /// <summary>navigator has vibrate (only asked when not native)</summary>
        public static bool browserCanVibrate = false;
        public static Action minimizeApp = null;

        public static bool isNative()
        {
            try
            {
                return native;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void vibrate(double pattern) { vibrate(new[] { pattern }); }

        /// <summary>
        /// Play a vibration pattern in the navigator.vibrate shape: a duration, or
        /// alternating on/off durations. Natively each "on" becomes an impact, its
        /// strength chosen by how long the browser version would have buzzed.
        /// </summary>
        public static void vibrate(double[] pattern)
        {
            if (!isNative())
            {
                try
                {
                    browserVibrate?.Invoke(pattern);
                }
                catch (Exception)
                {
                    /* not allowed here */
                }
                return;
            }
            var steps = pattern;
            double at = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                double ms = steps[i];
                if (i % 2 == 0 && ms > 0)
                {
                    string style = ms >= 60 ? "heavy" : ms >= 20 ? "medium" : "light";
                    try { impact?.Invoke(style, at); } catch (Exception) { }
                }
                at += ms;
            }
        }

        /// <summary>vibration is available: a phone browser that has it, or any native app</summary>
        public static bool canVibrate()
        {
            return isNative() || browserCanVibrate;
        }

        static readonly List<AppHandlers> watchers = new List<AppHandlers>();

        /// <summary>
        /// App lifecycle: `onPause` when the app goes to the background (music off),
        /// `onResume` when it comes back, and `onBack` for Android's back button —
        /// return true if it was used (a menu closed), false to leave the app.
        /// Returns the function that stops watching.
        /// </summary>
        public static Action watchApp(AppHandlers handlers)
        {
            if (!isNative()) return () => { };
            watchers.Add(handlers);
            return () => watchers.Remove(handlers);
        }

        /// <summary>host: the app went to the background</summary>
        public static void appPaused()
        {
            foreach (var h in watchers.ToArray()) h.onPause?.Invoke();
        }

        /// <summary>host: the app came back</summary>
        public static void appResumed()
        {
            foreach (var h in watchers.ToArray()) h.onResume?.Invoke();
        }

        /// <summary>host: Android's back button</summary>
        public static void backButton()
        {
            foreach (var h in watchers.ToArray())
                if (h.onBack == null || !h.onBack()) minimizeApp?.Invoke();
        }
    }
}
