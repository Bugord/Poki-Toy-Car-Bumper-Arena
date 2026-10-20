using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ToyCarArena.Audio
{
    /// <summary>
    /// The game's <see cref="IAudioService"/>: a few audio sources on one object of its own for the sound effects, taken
    /// in turn and the oldest one cut when all are busy, and one more for the music. The object outlives scene loads and
    /// carries the only audio listener, so nothing goes quiet while the Game scene is swapped under the curtain, and the
    /// music does not start over each round. Timing is unscaled: a hit-stop slows the game, not the sound.
    /// </summary>
    public sealed class AudioService : IAudioService, ITickable, IDisposable
    {
        // How fast the music's level follows the duck under a jingle, in shares of its full level per second.
        private const float DuckSpeed = 4f;

        private readonly AudioLibrary library;
        private readonly GameObject root;
        private readonly AudioSource[] voices;
        private readonly float[] voiceStartTimes;
        private readonly AudioSource music;
        private readonly Dictionary<Sound, float> startTimes = new();

        private bool isMusicOn;
        private float duckEndTime;

        public AudioService(AudioLibrary library)
        {
            this.library = library;

            this.root = new GameObject("Audio");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<AudioListener>();

            this.voices = new AudioSource[library.Voices];
            this.voiceStartTimes = new float[voices.Length];
            for (var i = 0; i < voices.Length; i++) {
                voices[i] = CreateSource(false);
            }

            this.music = CreateSource(true);
        }

        public AudioLibrary Library => library;

        public bool IsPaused
        {
            get => AudioListener.pause;
            set => AudioListener.pause = value;
        }

        public void Play(Sound sound, float volume = 1f)
        {
            PlayTake(sound, volume);
        }

        public void PlayJingle(Sound sound)
        {
            var length = PlayTake(sound, 1f);
            if (length > 0f) {
                duckEndTime = Mathf.Max(duckEndTime, Time.unscaledTime + length);
            }
        }

        public void PlayMusic()
        {
            var clip = library.ArenaMusic;
            if (clip == null || (isMusicOn && music.clip == clip)) {
                return;
            }

            isMusicOn = true;
            music.clip = clip;
            music.volume = MusicLevel();
            music.Play();
        }

        public void StopMusic()
        {
            isMusicOn = false;
            music.Stop();
        }

        public void PlayUiClick()
        {
            Play(library.UiClick);
        }

        public void Tick()
        {
            if (isMusicOn) {
                music.volume = Mathf.MoveTowards(music.volume, MusicLevel(),
                    DuckSpeed * library.MusicVolume * Time.unscaledDeltaTime);
            }
        }

        public void Dispose()
        {
            // Null when the app is quitting and the object went first. Switched off at once, since Destroy waits for the
            // end of the frame: the Game scene played on its own reloads itself, and the next scene's service would
            // otherwise hear a second audio listener for that frame.
            if (root != null) {
                root.SetActive(false);
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Starts a take of the sound on a free voice, or on the one that has played longest; how long it lasts, in
        /// seconds, or 0 when nothing started.
        /// </summary>
        private float PlayTake(Sound sound, float volume)
        {
            var clip = sound?.PickClip();
            if (clip == null || volume <= 0f) {
                return 0f;
            }

            var now = Time.unscaledTime;
            if (startTimes.TryGetValue(sound, out var lastTime) && now - lastTime < library.RepeatInterval) {
                return 0f;
            }

            startTimes[sound] = now;

            var voice = PickVoice();
            var source = voices[voice];
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(sound.Volume * volume * library.EffectsVolume);
            source.pitch = 1f + Random.Range(-sound.PitchJitter, sound.PitchJitter);
            source.Play();
            voiceStartTimes[voice] = now;
            return clip.length / source.pitch;
        }

        private int PickVoice()
        {
            var oldest = 0;
            for (var i = 0; i < voices.Length; i++) {
                if (!voices[i].isPlaying) {
                    return i;
                }

                if (voiceStartTimes[i] < voiceStartTimes[oldest]) {
                    oldest = i;
                }
            }

            return oldest;
        }

        private float MusicLevel()
        {
            var duck = Time.unscaledTime < duckEndTime ? library.MusicDuck : 1f;
            return library.MusicVolume * duck;
        }

        private AudioSource CreateSource(bool isLooping)
        {
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = isLooping;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
