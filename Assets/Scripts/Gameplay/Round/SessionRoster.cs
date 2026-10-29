using System;
using System.Collections.Generic;
using ToyCarArena.Gameplay.Cars;
using Random = UnityEngine.Random;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Seats visit players in coloured slots. Replacements change handles and cosmetic cars, never slot identity in
    /// a round. Names are dealt without reuse until the deck runs out.
    /// </summary>
    public sealed class SessionRoster : ISessionRoster, IDisposable
    {
        public const int MaxBots = 6;

        private readonly CarCatalog catalog;
        private readonly IPlayerCar playerCar;
        private readonly int[] colors = new int[MaxBots];
        private int playerColor;
        private readonly string[] deck = (string[])BotNames.All.Clone();
        private readonly SessionPlayer[] players = new SessionPlayer[MaxBots];
        private readonly bool[] joined = new bool[MaxBots];
        private readonly string[] left = new string[MaxBots];
        private int nextName = BotNames.All.Length;

        public SessionRoster(CarCatalog catalog, IPlayerCar playerCar)
        {
            this.catalog = catalog;
            this.playerCar = playerCar;
            this.playerColor = playerCar.SelectedColor;
            for (var slot = 0; slot < MaxBots; slot++) {
                colors[slot] = slot < playerColor ? slot : slot + 1;
            }
            playerCar.ColorChanged += OnColorChanged;
        }

        public void Dispose() => playerCar.ColorChanged -= OnColorChanged;

        // Replace immutable occupants, so snapshots from the played round retain both name and colour.
        private void OnColorChanged(int color)
        {
            for (var slot = 0; slot < colors.Length; slot++) {
                if (colors[slot] != color) {
                    continue;
                }
                colors[slot] = playerColor;
                var player = players[slot];
                if (player != null) {
                    players[slot] = new SessionPlayer(player.Name, player.Car, playerColor);
                }
                break;
            }
            this.playerColor = color;
        }

        public string NameOf(int carIndex) => carIndex == 0 ? "YOU" : PlayerOf(carIndex).Name;
        public CarCatalog.Entry CarOf(int carIndex) => PlayerOf(carIndex).Car;
        public int ColorOf(int carIndex) => carIndex == 0 ? playerColor : colors[Slot(carIndex)];
        public bool Joined(int carIndex) => carIndex > 0 && joined[Slot(carIndex)];
        public string LeftNameOf(int carIndex) => left[Slot(carIndex)];

        /// <summary>Copies seats, so the old arena and its HUD cannot see the next round's replacements.</summary>
        public IReadOnlyList<SessionPlayer> ForRound(int botCount)
        {
            CheckCount(botCount);
            var snapshot = new SessionPlayer[botCount];
            for (var index = 1; index <= botCount; index++) {
                snapshot[index - 1] = PlayerOf(index);
            }
            return Array.AsReadOnly(snapshot);
        }

        public void NextRound(int playedBots, int nextBots, int nemesis, GameTuning.PlayerSettings settings)
        {
            CheckCount(playedBots);
            CheckCount(nextBots);
            ForRound(playedBots);
            Array.Clear(joined, 0, joined.Length);
            Array.Clear(left, 0, left.Length);

            // A seat removed by difficulty leaves this lobby too, even when its occupant stays reserved for revenge.
            for (var index = nextBots + 1; index <= playedBots; index++) {
                left[index - 1] = players[index - 1].Name;
            }

            var leaving = new List<int>();
            for (var index = 1; index <= playedBots; index++) {
                if (index != nemesis && Random.value < settings.LeaveChance) {
                    leaving.Add(index);
                }
            }
            // A cap must not favour the first colours. Every eligible player has the same chance to leave.
            for (var i = leaving.Count - 1; i > 0; i--) {
                var other = Random.Range(0, i + 1);
                (leaving[i], leaving[other]) = (leaving[other], leaving[i]);
            }
            for (var i = 0; i < Math.Min(settings.MaxLeaves, leaving.Count); i++) {
                var slot = leaving[i] - 1;
                left[slot] = players[slot].Name;
                Seat(slot);
            }

            for (var index = playedBots + 1; index <= nextBots; index++) {
                // An absent revenge target returns as the same player when difficulty brings its slot back.
                if (index != nemesis || players[index - 1] == null) {
                    Seat(index - 1);
                }
            }
        }

        private SessionPlayer PlayerOf(int index)
        {
            var slot = Slot(index);
            if (players[slot] == null) {
                Seat(slot);
            }
            return players[slot];
        }

        private void Seat(int slot)
        {
            if (catalog.Cars.Count == 0) {
                throw new InvalidOperationException("SessionRoster needs a non-empty CarCatalog.");
            }
            if (nextName == deck.Length) {
                Refill();
            }
            players[slot] = new SessionPlayer(deck[nextName++], catalog.Cars[Random.Range(0, catalog.Cars.Count)], colors[slot]);
            joined[slot] = true;
        }

        private void Refill()
        {
            Array.Copy(BotNames.All, deck, deck.Length);
            for (var i = deck.Length - 1; i > 0; i--) {
                var other = Random.Range(0, i + 1);
                (deck[i], deck[other]) = (deck[other], deck[i]);
            }
            // Move seated names to the front and skip them for the entire new deck, including an absent nemesis.
            nextName = 0;
            for (var i = 0; i < deck.Length; i++) {
                foreach (var player in players) {
                    if (player != null && player.Name == deck[i]) {
                        (deck[nextName], deck[i]) = (deck[i], deck[nextName]);
                        nextName++;
                        break;
                    }
                }
            }
        }

        private static int Slot(int index)
        {
            if (index < 1 || index > MaxBots) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return index - 1;
        }

        private static void CheckCount(int count)
        {
            if (count < 0 || count > MaxBots) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }
    }

    /// <summary>An immutable occupant; old round snapshots keep it after the seat is filled again.</summary>
    public sealed class SessionPlayer
    {
        public string Name { get; }
        public CarCatalog.Entry Car { get; }
        public int ColorId { get; }

        public SessionPlayer(string name, CarCatalog.Entry car, int colorId)
        {
            this.Name = name;
            this.Car = car;
            this.ColorId = colorId;
        }
    }
}
