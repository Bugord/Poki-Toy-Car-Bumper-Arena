namespace ToyCarArena.Audio
{
    /// <summary>
    /// Plays the game's sounds and music. One for the whole app: it lives in the app scope, so the music goes on across
    /// the reloads of the Game scene (the Game scene on its own brings its own). All sounds are 2D: the camera looks
    /// down on the whole arena, so where a sound comes from adds nothing.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// The sounds to play, with their levels.
        /// </summary>
        AudioLibrary Library { get; }

        /// <summary>
        /// Holds every sound and the music where they are, and lets them go on when cleared: what an ad needs. The kit's
        /// Poki platform already pauses the audio during its ads; this is the same switch for anything else.
        /// </summary>
        bool IsPaused { get; set; }

        /// <summary>
        /// Plays a sound once, at <paramref name="volume"/> times its own level.
        /// </summary>
        void Play(Sound sound, float volume = 1f);

        /// <summary>
        /// Plays a jingle once, the music ducking under it while it lasts.
        /// </summary>
        void PlayJingle(Sound sound);

        /// <summary>
        /// Starts the arena's music; it goes on if it is already playing.
        /// </summary>
        void PlayMusic();

        void StopMusic();

        /// <summary>
        /// The click of a button on a screen.
        /// </summary>
        void PlayUiClick();
    }
}
