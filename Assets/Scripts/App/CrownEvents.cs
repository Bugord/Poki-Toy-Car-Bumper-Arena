using Bugord.App.Platform;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;

namespace ToyCarArena.App
{
    /// <summary>
    /// The analytics of a CROWN round (<see cref="CrownMode"/>), one event at its end beside the mode's own
    /// (<see cref="RoundEvents.MeasureSpecialEnd"/>):
    /// <c>special / crown-held-&lt;s&gt;-takes-&lt;n&gt; / crown</c>. <c>s</c> is how long the player kept the crown, by the
    /// lower end of its bucket: <c>none</c> (never), <c>1</c> (1 to 9 s), <c>10</c> (10 to 19 s), <c>20plus</c>; <c>n</c>
    /// is how many times the player took it, 0..9 or <c>10plus</c>. It says whether the player gets the crown at all and
    /// keeps it, or takes it often and loses it straight away.
    /// </summary>
    /// <remarks>
    /// A bucket is named by its lower end rather than as a range: Poki drops an event with more than
    /// <see cref="GameEvent.MaxNumbers"/> numbers, and <c>held-1-9-takes-3</c> would have three.
    /// </remarks>
    public static class CrownEvents
    {
        private const string Category = "special";

        // Takes past this many share one bucket.
        private const int LastTakesBucket = 10;

        public static void MeasureCrownEnd(this IGamePlatform platform, CrownMode crown, Car player)
        {
            if (crown == null || !crown.IsActive || player == null) {
                return;
            }

            platform.Measure(Category, $"crown-held-{HeldBucketOf(crown.HeldOf(player))}-takes-"
                + TakesBucketOf(crown.TakesOf(player)), "crown");
        }

        /// <summary>none, 1, 10 or 20plus: the player's whole seconds with the crown, by the bucket's lower end.</summary>
        public static string HeldBucketOf(float seconds)
        {
            var whole = (int)seconds;
            return whole <= 0 ? "none" : whole < 10 ? "1" : whole < 20 ? "10" : "20plus";
        }

        /// <summary>0..9, or 10plus: the times the player took the crown.</summary>
        public static string TakesBucketOf(int takes) =>
            takes >= LastTakesBucket ? $"{LastTakesBucket}plus" : System.Math.Max(0, takes).ToString();
    }
}
