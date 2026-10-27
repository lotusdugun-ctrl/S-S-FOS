// What the store apps got from Capacitor plugins, on Unity: haptic impacts (@capacitor/haptics)
// and the audio output (WebAudio + the <audio> element for the music).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Sisifos.Audio;
using Sisifos.Game;
using UnityEngine;

namespace Sisifos.App
{
    /// <summary>
    /// Native.impact: one impact ("light" | "medium" | "heavy") after a delay in ms. Android plays
    /// the platform's predefined click effects (tick / click / heavy click, API 29+; a short one-shot
    /// before that); iOS a UIImpactFeedbackGenerator of the same style (Plugins/iOS/SisifosNative.mm).
    /// </summary>
    public static class Haptics
    {
        struct Pending
        {
            public double at;
            public string style;
        }

        static readonly List<Pending> pending = new List<Pending>();

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SisifosHaptics_Impact(int style);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk;
        // never true: the reference makes the Android build add the VIBRATE permission
        static bool addVibratePermission;
#endif

        public static void Init()
        {
            Native.impact = (style, delay) =>
            {
                lock (pending) pending.Add(new Pending { at = Js.now() + delay, style = style });
            };
#if UNITY_ANDROID && !UNITY_EDITOR
            if (addVibratePermission) Handheld.Vibrate();
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = up.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch (Exception e) { Debug.LogException(e); vibrator = null; }
#endif
        }

        /// <summary>play the impacts that are due; the host calls it every frame</summary>
        public static void Tick()
        {
            if (pending.Count == 0) return;
            double now = Js.now();
            lock (pending)
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    if (pending[i].at > now) continue;
                    Play(pending[i].style);
                    pending.RemoveAt(i--);
                }
            }
        }

        static void Play(string style)
        {
            int s = style == "light" ? 0 : style == "heavy" ? 2 : 1;
#if UNITY_IOS && !UNITY_EDITOR
            SisifosHaptics_Impact(s);
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (vibrator == null) return;
            try
            {
                if (sdk >= 29)
                {
                    // VibrationEffect.EFFECT_TICK = 2, EFFECT_CLICK = 0, EFFECT_HEAVY_CLICK = 5
                    int id = s == 0 ? 2 : s == 1 ? 0 : 5;
                    using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = ve.CallStatic<AndroidJavaObject>("createPredefined", id))
                        vibrator.Call("vibrate", effect);
                }
                else if (sdk >= 26)
                {
                    long ms = s == 0 ? 10 : s == 1 ? 20 : 40;
                    int amp = s == 0 ? 80 : s == 1 ? 160 : 255;
                    using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = ve.CallStatic<AndroidJavaObject>("createOneShot", ms, amp))
                        vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", (long)(s == 0 ? 10 : s == 1 ? 20 : 40));
            }
            catch (Exception e) { Debug.LogException(e); }
#endif
        }
    }

    /// <summary>
    /// The music element (IMusicPlayer) on a looping, streamed AudioSource playing
    /// Resources/Audio/music.mp3. GameAudio applies its gain chain in renderMusic, from the
    /// OnAudioFilterRead of the same GameObject (MusicFilter).
    /// </summary>
    public sealed class MusicPlayer : IMusicPlayer
    {
        readonly AudioSource src;
        Action onError;
        bool started, failed;

        public MusicPlayer(AudioSource src) { this.src = src; }

        public bool loop { get { return src.loop; } set { src.loop = value; } }
        public string preload { get; set; }
        public bool paused { get { return !src.isPlaying; } }

        public bool play()
        {
            if (failed || src.clip == null) { Fail(); return false; }
            if (!started) { src.Play(); started = true; }
            else if (!src.isPlaying) src.UnPause();
            return true;
        }

        public void pause()
        {
            if (src.isPlaying) src.Pause();
        }

        public void addErrorListener(Action onError)
        {
            this.onError += onError;
            if (src.clip == null) Fail();
        }

        void Fail()
        {
            if (failed) return;
            failed = true;
            var e = onError;
            onError = null;
            e?.Invoke();
        }

        /// <summary>the host checks the stream every frame (a decode error shows up as Failed)</summary>
        public void Tick()
        {
            if (!failed && src.clip != null && src.clip.loadState == AudioDataLoadState.Failed) Fail();
        }
    }

    /// <summary>OnAudioFilterRead of the music source: GameAudio's musicGain × master.</summary>
    public sealed class MusicFilter : MonoBehaviour
    {
        public GameAudio audio;

        void OnAudioFilterRead(float[] data, int channels)
        {
            var a = audio;
            if (a == null) return;
            a.renderMusic(data, channels);
        }
    }

    /// <summary>OnAudioFilterRead of a clip-less source: the synthesized effects.</summary>
    public sealed class SynthFilter : MonoBehaviour
    {
        public GameAudio audio;

        void OnAudioFilterRead(float[] data, int channels)
        {
            var a = audio;
            if (a == null) { Array.Clear(data, 0, data.Length); return; }
            a.render(data, channels);
        }
    }

    /// <summary>
    /// The audio host (GameAudio.cs header): a clip-less AudioSource whose filter renders the
    /// effects, and the music AudioSource whose filter applies the music gain. Attach() gives
    /// GameAudio its music player and the output rate before start().
    /// </summary>
    public sealed class AudioHost : MonoBehaviour
    {
        AudioSource synth, music;
        SynthFilter synthFilter;
        MusicFilter musicFilter;
        MusicPlayer player;

        public static AudioHost Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            return go.AddComponent<AudioHost>();
        }

        void Awake()
        {
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            var sgo = new GameObject("Synth");
            sgo.transform.SetParent(transform, false);
            synth = sgo.AddComponent<AudioSource>();
            synth.playOnAwake = false;
            synth.loop = true;
            synth.spatialBlend = 0;
            synth.volume = 1;
            synthFilter = sgo.AddComponent<SynthFilter>();

            var mgo = new GameObject("Music");
            mgo.transform.SetParent(transform, false);
            music = mgo.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0;
            music.volume = 1;
            music.clip = Resources.Load<AudioClip>("Audio/music");
            musicFilter = mgo.AddComponent<MusicFilter>();
            player = new MusicPlayer(music);
        }

        /// <summary>hand the engine's GameAudio its output (call before audio.start())</summary>
        public void Attach(GameAudio audio)
        {
            audio.musicPlayer = player;
            audio.sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            synthFilter.audio = audio;
            musicFilter.audio = audio;
            // the effects run from the start; GameAudio renders silence until it is started
            synth.Play();
        }

        void Update()
        {
            player?.Tick();
        }
    }
}
