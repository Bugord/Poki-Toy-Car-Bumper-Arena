using UnityEngine;

namespace ToyCarArena.App
{
    /// <summary>
    /// Whether NEXT MATCH shows a commercial break: not before the visit has had
    /// <see cref="Gameplay.GameTuning.AdSettings.FreePlaySeconds"/> of rounds, so a new player's first rounds go straight
    /// on, as SMUP's restarts do. Poki's engagement is the share of players who play past three minutes. Once due, every
    /// NEXT MATCH asks for one; Poki itself spaces them out.
    /// </summary>
    public sealed class AdBreaks
    {
        private float played;

        /// <summary>Seconds of rounds counted so far, from GO to the round's end.</summary>
        public float Played => played;

        public bool IsDue(float freePlaySeconds) => played >= freePlaySeconds;

        public void AddPlay(float seconds) => played += Mathf.Max(0f, seconds);

        /// <summary>The dev console sets it to try NEXT MATCH with or without a break.</summary>
        internal void SetPlayed(float seconds) => played = Mathf.Max(0f, seconds);
    }
}
