// What GameAudio needs from the music player — the parts of HTMLAudioElement
// audio.ts uses. The Unity host implements it with an AudioSource playing the
// music clip (streamed, looping, volume 1) and, on that AudioSource's
// GameObject, an OnAudioFilterRead that calls GameAudio.renderMusic(data, ch):
// that applies the music gain node and the master gain sample by sample,
// exactly as the MediaElementSource -> musicGain -> master chain does on the web.

using System;

namespace Sisifos.Audio
{
    public interface IMusicPlayer
    {
        /// <summary>el.loop</summary>
        bool loop { get; set; }

        /// <summary>el.preload ("auto"); a hint the host may ignore.</summary>
        string preload { get; set; }

        /// <summary>el.paused</summary>
        bool paused { get; }

        /// <summary>el.play(). Returns false where the web promise would reject (refused); GameAudio ignores it.</summary>
        bool play();

        /// <summary>el.pause()</summary>
        void pause();

        /// <summary>el.addEventListener("error", ...): call when the track cannot be loaded or decoded.</summary>
        void addErrorListener(Action onError);
    }
}
