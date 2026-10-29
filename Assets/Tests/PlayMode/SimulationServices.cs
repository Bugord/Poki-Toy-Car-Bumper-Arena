using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Round;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Keeps each measured visit round at the same difficulty, independent of previous simulated results.
    /// </summary>
    public sealed class SimulationProgress : ISessionProgress
    {
        public int Round { get; set; }
        public int LosingStreak { get; set; }
    }

    /// <summary>
    /// Lets feedback read the real sound levels without playing audio or consuming random pitch choices.
    /// </summary>
    public sealed class SilentAudioService : IAudioService
    {
        public AudioLibrary Library { get; }
        public bool IsPaused { get; set; }

        public SilentAudioService(AudioLibrary library)
        {
            this.Library = library;
        }

        public void Play(Sound sound, float volume = 1f)
        {
        }

        public void PlayJingle(Sound sound)
        {
        }

        public void PlayMusic()
        {
        }

        public void StopMusic()
        {
        }

        public void PlayUiClick()
        {
        }
    }
}
