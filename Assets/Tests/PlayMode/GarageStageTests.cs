using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The menu and the garage hand the turning car to each other: going back from the garage, which hides the stage
    /// as it closes, the menu's car turns on from where it was instead of snapping back to its first angle.
    /// </summary>
    public sealed class GarageStageTests
    {
        [UnityTest]
        public IEnumerator TheCarKeepsItsTurnFromTheGarageBackToTheMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var host = new GameObject("Garage stage");
            var stage = new GarageStage(AssetDatabase.LoadAssetAtPath<Car>("Assets/Prefabs/Cars/Car.prefab"),
                AssetDatabase.LoadAssetAtPath<MechanicArt>("Assets/Data/Arenas/data_mechanic_art.asset"), host.transform);
            try {
                var car = catalog.Cars[0];
                stage.ShowCar(car, 0, "", true, Color.yellow);
                var start = stage.Yaw;

                // The menu turns the car a while, and the garage opens over it with the same car on the same stage.
                yield return Turn(stage, 0.6f);
                stage.ShowCar(car, 0, "", true);
                yield return Turn(stage, 0.4f);
                Assert.That(Mathf.DeltaAngle(start, stage.Yaw), Is.Not.EqualTo(0f).Within(10f),
                    "The car should have turned on the menu and in the garage.");

                // The garage closes: the stage stops, and the menu shows the car again a little later.
                // Ticked and hidden in one frame, so the angle drawn last is the one the stage keeps.
                stage.Tick();
                stage.Hide();
                var hidden = stage.Yaw;
                yield return new WaitForSecondsRealtime(0.3f);
                stage.ShowCar(car, 0, "", true, Color.yellow);
                Assert.That(Mathf.DeltaAngle(hidden, stage.Yaw), Is.EqualTo(0f).Within(1f),
                    "The menu should turn the car on from where the garage left it.");

                // Another car (bought or equipped in the garage) rebuilds, and still keeps the turn.
                stage.Tick();
                stage.Hide();
                hidden = stage.Yaw;
                yield return new WaitForSecondsRealtime(0.2f);
                stage.ShowCar(catalog.Cars[1], 3, "crown", true, Color.yellow);
                Assert.That(Mathf.DeltaAngle(hidden, stage.Yaw), Is.EqualTo(0f).Within(1f),
                    "Another car should take over the turn, not start again.");
            }
            finally {
                stage.Dispose();
                Object.Destroy(host);
            }
        }

        private static IEnumerator Turn(GarageStage stage, float seconds)
        {
            var until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) {
                stage.Tick();
                yield return null;
            }
        }
    }
}
