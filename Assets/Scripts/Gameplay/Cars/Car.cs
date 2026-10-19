using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// A car in the round, whoever drives it: the round places it and takes it out when it falls, and the drivers
    /// steer it through <see cref="Movement"/>. Out of the round, it keeps falling in view until it is well below the
    /// arena, and then it is put away.
    /// </summary>
    [RequireComponent(typeof(CarMovement))]
    public sealed class Car : MonoBehaviour
    {
        private GameTuning.RoundSettings settings;

        public CarMovement Movement { get; private set; }

        /// <summary>
        /// Whether the car has fallen off and left the round.
        /// </summary>
        public bool IsOut { get; private set; }

        public Vector3 Position => transform.position;

        [Inject]
        private void Construct(GameTuning tuning)
        {
            this.settings = tuning.Round;
        }

        private void Awake()
        {
            Movement = GetComponent<CarMovement>();
        }

        private void Update()
        {
            if (IsOut && transform.position.y < settings.RemoveHeight) {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Leaves the round: nobody drives the car any more, and it falls on.
        /// </summary>
        public void TakeOut()
        {
            IsOut = true;
            Movement.Drive(Vector3.zero, 0f);
        }
    }
}
