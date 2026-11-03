using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ToyCarArena.App
{
    /// <summary>
    /// Holds the game still while an ad plays. The platform already pauses the sound, but the result and the lobby lie
    /// over a live arena (the bots play on after the player is out, the podium turns), and Poki wants the game paused
    /// under its ads.
    /// </summary>
    public static class AdPause
    {
        public static async UniTask<T> Run<T>(Func<UniTask<T>> ad)
        {
            var timeScale = Time.timeScale;
            Time.timeScale = 0f;
            try {
                return await ad();
            }
            finally {
                Time.timeScale = timeScale;
            }
        }

        public static UniTask Run(Func<UniTask> ad) => Run(async () => {
            await ad();
            return true;
        });
    }
}
