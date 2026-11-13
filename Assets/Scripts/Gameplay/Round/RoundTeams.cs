using ToyCarArena.Gameplay.Cars;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The teams of a round played in teams (<see cref="ModeRules.Teams"/>), the one place that says which car is on
    /// which team, what colour a team wears and where its cars start. Pure, so the lobby, which runs before the scene is
    /// built, reads the same teams as the round.
    /// <para>
    /// A car's team is its index modulo the number of teams: the player (0) is always on <see cref="Blue"/>, the bots
    /// alternate, so a round of six is three against three. Every car of a team wears the team's paint for the round
    /// (<see cref="PaintOf"/>, over the visit's own colours, which stay as they are), and the teams start on opposite
    /// halves of the spawn ring (<see cref="SpawnSlotOf"/>), facing each other.
    /// </para>
    /// </summary>
    public static class RoundTeams
    {
        /// <summary>The player's team.</summary>
        public const int Blue = 0;

        /// <summary>The other team.</summary>
        public const int Red = 1;

        /// <summary>No team: a round without teams, or a car not in the round.</summary>
        public const int None = -1;

        // CarPaint's order: green, red, blue...
        private const int BluePaint = 2;
        private const int RedPaint = 1;

        /// <summary>The team of car <paramref name="index"/> in a round of <paramref name="teams"/> teams.</summary>
        public static int TeamOf(int index, int teams) => teams > 0 && index >= 0 ? index % teams : None;

        /// <summary>The paint every car of <paramref name="team"/> wears for the round (<see cref="CarPaint"/>).</summary>
        public static int PaintOf(int team) => team == Red ? RedPaint : BluePaint;

        /// <summary>The team's id for analytics and the string table: no numbers, never changed.</summary>
        public static string IdOf(int team) => team == Red ? "red" : "blue";

        /// <summary>
        /// The slot of the spawn ring (0 nearest the camera, going round) car <paramref name="index"/> of
        /// <paramref name="count"/> starts on: a team's cars side by side, the player's on the near half, the other team
        /// across the floor, so the round opens face to face. Without teams a car starts on its own index's slot.
        /// </summary>
        public static int SpawnSlotOf(int index, int count, int teams)
        {
            if (teams != 2 || count < 2 || count % 2 != 0) {
                return index;
            }

            var rank = index / 2;
            // The near team round slot 0 (0, 1, count - 1, 2...), each car of the far team straight across from the
            // near car of its rank: the player faces the first red car over the middle.
            var offset = rank % 2 == 1 ? (rank + 1) / 2 : -(rank / 2);
            var slot = offset + (index % 2 == Red ? count / 2 : 0);
            return ((slot % count) + count) % count;
        }
    }
}
