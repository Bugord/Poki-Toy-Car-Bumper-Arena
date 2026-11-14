namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// Whether the player has ever used an item, on this device and for good. Until they have, every round with boxes
    /// teaches them: a box waits in front of their car at the start (<see cref="ItemLesson"/>) and the HUD keeps its
    /// TAP TO USE hint up whenever an item is ready. On the Poki test of v0.17.9 the players used 59% of the items they
    /// got in the first round with boxes against 79% from the fourth on.
    /// <para>
    /// From Boot it is the app's HUD controls, which keep it in the storage; the Game scene on its own and the
    /// simulations keep it in memory (<see cref="ItemLessonMemory"/>).
    /// </para>
    /// </summary>
    public interface IItemLesson
    {
        /// <summary>Whether the player has used an item at least once.</summary>
        bool HasUsedItem { get; }

        /// <summary>The player used an item: the lesson is over, for good.</summary>
        void MarkItemUsed();
    }

    /// <summary>The lesson kept in memory for this run only: the Game scene on its own, the simulations and tests.</summary>
    public sealed class ItemLessonMemory : IItemLesson
    {
        public ItemLessonMemory(bool hasUsedItem = false)
        {
            this.HasUsedItem = hasUsedItem;
        }

        public bool HasUsedItem { get; set; }

        public void MarkItemUsed() => HasUsedItem = true;
    }
}
