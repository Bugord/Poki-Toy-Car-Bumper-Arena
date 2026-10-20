using System;
using UnityEngine;

namespace ToyCarArena.Audio
{
    /// <summary>
    /// One sound of the game as the <see cref="AudioLibrary"/> lists it: a clip, or a few takes of it picked at random,
    /// with its own level and a little random pitch. A sound heard over and over, a hit, gets two takes and a pitch
    /// spread, so a scrum of cars does not rattle like a machine gun.
    /// </summary>
    [Serializable]
    public sealed class Sound
    {
        [Tooltip("The takes of the sound; each play picks one at random")]
        [SerializeField]
        private AudioClip[] clips = Array.Empty<AudioClip>();

        [Tooltip("Level of the sound against the others, before the caller's own volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float volume = 1f;

        [Tooltip("Most the pitch moves up or down at random on each play, as a share of the normal pitch")]
        [Range(0f, 0.5f)]
        [SerializeField]
        private float pitchJitter;

        public float Volume => volume;
        public float PitchJitter => pitchJitter;

        /// <summary>
        /// A take to play, null when the sound has none.
        /// </summary>
        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0) {
                return null;
            }

            return clips[clips.Length == 1 ? 0 : UnityEngine.Random.Range(0, clips.Length)];
        }
    }
}
