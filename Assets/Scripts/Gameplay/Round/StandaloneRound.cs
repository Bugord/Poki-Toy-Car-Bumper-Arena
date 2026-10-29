using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Plays rounds in the Game scene opened on its own, without Boot and its app states: begins the round at once and,
    /// a little after it finishes, loads the scene again for the next one. Only registered when the scope has no parent.
    /// </summary>
    public sealed class StandaloneRound : IStartable, IDisposable
    {
        private readonly ArenaRound round;
        private readonly GameTuning tuning;
        private readonly SessionProgress progress;
        private readonly CancellationTokenSource lifetime = new();

        public StandaloneRound(ArenaRound round, GameTuning tuning, SessionProgress progress)
        {
            this.round = round;
            this.tuning = tuning;
            this.progress = progress;
        }

        /// <summary>
        /// The session's progress while the scene plays on its own. The scene reloads itself every round, its scope with
        /// it, and there is no app scope above it to keep the progress, so it is kept here, for the whole play session:
        /// the bots then ramp up and ease off as they do from Boot. Static, so it is set anew when play starts, in case
        /// the editor keeps static state between play sessions (domain reload off).
        /// </summary>
        public static SessionProgress Session { get; private set; } = new();

        // Lazy: Unity's random API must run on the main thread, not in a static initializer during scene loading.
        private static SessionRoster roster;
        public static SessionRoster Roster => roster ??= new SessionRoster();

        public void Start()
        {
            round.Finished += OnFinished;
            round.Begin();
        }

        public void Dispose()
        {
            round.Finished -= OnFinished;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            Session = new SessionProgress();
            roster = null;
        }

        private void OnFinished(RoundResult result)
        {
            Debug.Log($"Round {progress.Round} over: {result.Outcome}, #{result.Place} of {result.CarCount} after " +
                $"{result.Seconds:0.0} s, {result.Knockouts} knocked out, knocked out by {result.KnockedOutBy}.");
            progress.Record(result);
            RestartLater(result.Outcome, lifetime.Token).Forget();
        }

        private async UniTaskVoid RestartLater(RoundOutcome outcome, CancellationToken token)
        {
            var delay = outcome == RoundOutcome.Out ? tuning.Round.OutResultDelay : tuning.Round.ResultDelay;
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
