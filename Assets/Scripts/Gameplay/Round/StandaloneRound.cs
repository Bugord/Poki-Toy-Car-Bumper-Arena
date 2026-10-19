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
        private readonly CancellationTokenSource lifetime = new();

        public StandaloneRound(ArenaRound round, GameTuning tuning)
        {
            this.round = round;
            this.tuning = tuning;
        }

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

        private void OnFinished(RoundResult result)
        {
            Debug.Log($"Round over: {result.Outcome}, #{result.Place} of {result.CarCount} after {result.Seconds:0.0} s.");
            RestartLater(lifetime.Token).Forget();
        }

        private async UniTaskVoid RestartLater(CancellationToken token)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(tuning.Round.ResultDelay), cancellationToken: token);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
