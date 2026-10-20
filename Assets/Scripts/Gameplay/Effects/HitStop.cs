using UnityEngine;

namespace ToyCarArena.Gameplay.Effects
{
    /// <summary>
    /// The split second the game nearly stops on a big hit, so the hit lands. It slows <see cref="Time.timeScale"/> and
    /// puts it back once the stop is over, timed on the unscaled clock; the owner calls <see cref="Tick"/> every frame
    /// and <see cref="Release"/> when it goes away, so a scene unloaded mid-stop never leaves the game slowed.
    /// <para>
    /// Stops do not stack: one that comes during another, or too soon after it, is dropped, so a pile-up of hits is one
    /// stop and not a stutter. Nor does a stop fight anything else over the time scale: it starts only if the game runs
    /// faster than the stop, and on the way out it puts the old scale back only if the scale is still its own, so a
    /// pause that set it meanwhile wins.
    /// </para>
    /// </summary>
    public sealed class HitStop
    {
        private bool isStopped;
        private float endTime;
        private float readyTime;
        private float stopScale;
        private float restoreScale;

        public bool IsStopped => isStopped;

        /// <summary>
        /// Slows the game to <paramref name="timeScale"/> for <paramref name="duration"/> real seconds, unless a stop is
        /// on or ended less than <paramref name="gap"/> seconds ago.
        /// </summary>
        public void Stop(float duration, float timeScale, float gap)
        {
            var now = Time.unscaledTime;
            if (isStopped || duration <= 0f || now < readyTime || Time.timeScale <= timeScale) {
                return;
            }

            isStopped = true;
            restoreScale = Time.timeScale;
            stopScale = timeScale;
            endTime = now + duration;
            readyTime = endTime + gap;
            Time.timeScale = timeScale;
        }

        public void Tick()
        {
            if (isStopped && Time.unscaledTime >= endTime) {
                Release();
            }
        }

        /// <summary>
        /// Ends the stop now, if one is on.
        /// </summary>
        public void Release()
        {
            if (!isStopped) {
                return;
            }

            isStopped = false;
            if (Mathf.Approximately(Time.timeScale, stopScale)) {
                Time.timeScale = restoreScale;
            }
        }
    }
}
