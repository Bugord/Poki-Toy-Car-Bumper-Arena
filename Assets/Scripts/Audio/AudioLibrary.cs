using UnityEngine;

namespace ToyCarArena.Audio
{
    /// <summary>
    /// Every sound and the music of the game, with their levels, in one asset (<c>Data/Audio/data_audio_library</c>), so
    /// the mix is tuned in one place and the code names sounds rather than clips. No audio mixer: a handful of 2D
    /// sounds over one music track needs only levels, and the mixer would add to the build.
    /// </summary>
    [CreateAssetMenu(fileName = "data_audio_library", menuName = "Bonk Arena/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Header("Mix")]
        [Tooltip("Level of every sound effect")]
        [Range(0f, 1f)]
        [SerializeField]
        private float effectsVolume = 1f;

        [Tooltip("Level of the music")]
        [Range(0f, 1f)]
        [SerializeField]
        private float musicVolume = 0.45f;

        [Tooltip("Share of its level the music keeps while a win or lose jingle plays over it")]
        [Range(0f, 1f)]
        [SerializeField]
        private float musicDuck = 0.25f;

        [Tooltip("How many sound effects can play at once; past that the oldest one makes way")]
        [Min(1)]
        [SerializeField]
        private int voices = 12;

        [Tooltip("The same sound does not start again sooner than this, in seconds, so a pile of cars hitting on one " +
            "frame is one hit to the ear and does not take every voice")]
        [Min(0f)]
        [SerializeField]
        private float repeatInterval = 0.05f;

        [Header("Music")]
        [Tooltip("The loop under the rounds; it goes on across rounds and the result screen")]
        [SerializeField]
        private AudioClip arenaMusic;

        [Header("Cars")]
        [Tooltip("A car bumping into another")]
        [SerializeField]
        private Sound hitLight = new();

        [Tooltip("A hard or boosted hit")]
        [SerializeField]
        private Sound hitHeavy = new();

        [Tooltip("A car firing its boost")]
        [SerializeField]
        private Sound boost = new();

        [Tooltip("A bot falling off the arena")]
        [SerializeField]
        private Sound knockout = new();

        [Tooltip("The player's car falling off the arena")]
        [SerializeField]
        private Sound knockoutPlayer = new();

        [Tooltip("A bot falling off soon after the player hit it: the player's knock-out")]
        [SerializeField]
        private Sound knockoutCredit = new();

        [Header("Arena")]
        [Tooltip("A ring of the edge starting to shake")]
        [SerializeField]
        private Sound crumbleWarning = new();

        [Tooltip("A ring of the edge falling away")]
        [SerializeField]
        private Sound crumbleFall = new();

        [Header("Round")]
        [Tooltip("Each of the last seconds of a round")]
        [SerializeField]
        private Sound countdownTick = new();

        [Tooltip("The jingle of a round the player won or survived to the end")]
        [SerializeField]
        private Sound win = new();

        [Tooltip("The jingle of a round the player fell out of")]
        [SerializeField]
        private Sound lose = new();

        [Header("Screens")]
        [Tooltip("A button pressed on a screen")]
        [SerializeField]
        private Sound uiClick = new();

        public float EffectsVolume => effectsVolume;
        public float MusicVolume => musicVolume;
        public float MusicDuck => musicDuck;
        public int Voices => voices;
        public float RepeatInterval => repeatInterval;
        public AudioClip ArenaMusic => arenaMusic;
        public Sound HitLight => hitLight;
        public Sound HitHeavy => hitHeavy;
        public Sound Boost => boost;
        public Sound Knockout => knockout;
        public Sound KnockoutPlayer => knockoutPlayer;
        public Sound KnockoutCredit => knockoutCredit;
        public Sound CrumbleWarning => crumbleWarning;
        public Sound CrumbleFall => crumbleFall;
        public Sound CountdownTick => countdownTick;
        public Sound Win => win;
        public Sound Lose => lose;
        public Sound UiClick => uiClick;
    }
}
