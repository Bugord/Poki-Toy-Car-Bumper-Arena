#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.Text;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Observes the player's last three seconds without changing the simulation. Sampling runs at the end of the
    /// existing movement FixedUpdate, before PhysX; collision callbacks arrive afterwards. Strings are made only
    /// when the round ends or a test asks for a summary. The entire observer is absent from release players.
    /// </summary>
    public sealed class PlayerFallTelemetry
    {
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly Car player;
        private readonly SamplePoint[] samples;
        private readonly ContactPoint[] contacts = new ContactPoint[192];
        private int next;
        private int count;
        private int contactNext;
        private int contactCount;
        private int contactSequence;
        private float assist;
        private ContactPoint lastContact;
        private SamplePoint lastGrounded;
        private bool hasGrounded;

        public string FallSummary { get; private set; }
        public int EnterContacts { get; private set; }
        public int CreditWrites { get; private set; }
        public float PeakSpeed { get; private set; }

        public PlayerFallTelemetry(ArenaRound round, IArenaEdge edge, Car player)
        {
            this.round = round;
            this.edge = edge;
            this.player = player;
            this.samples = new SamplePoint[Mathf.CeilToInt(3f / Time.fixedDeltaTime) + 2];
            this.lastContact.Other = RoundResult.Nobody;
            this.lastContact.Time = float.NegativeInfinity;
        }

        public void Assist(float share)
        {
            this.assist = share;
        }

        /// <summary>
        /// Enter uses the same normal and pre-solver StepVelocity as Car; Stay uses the current velocity and centre
        /// direction, exactly as credit does there. Signed projections are kept before the gameplay clamps them.
        /// </summary>
        public void Contact(Car other, float otherApproach, float ownApproach, bool credited, bool entered)
        {
            if (FallSummary != null) {
                return;
            }

            lastContact = new ContactPoint {
                Sequence = ++contactSequence,
                Time = Time.time,
                Other = round.IndexOf(other),
                OtherApproach = otherApproach,
                OwnApproach = ownApproach,
                Credited = credited,
                Entered = entered,
            };
            contacts[contactNext] = lastContact;
            contactNext = (contactNext + 1) % contacts.Length;
            contactCount = Mathf.Min(contactCount + 1, contacts.Length);
            if (entered) {
                EnterContacts++;
            }

            if (credited) {
                CreditWrites++;
            }
        }

        public void Sample()
        {
            if (!round.IsRunning || player.IsOut || FallSummary != null) {
                return;
            }

            var sample = Read();
            samples[next] = sample;
            next = (next + 1) % samples.Length;
            count = Mathf.Min(count + 1, samples.Length);
            PeakSpeed = Mathf.Max(PeakSpeed, sample.Velocity.magnitude);
            if (sample.Grounded) {
                lastGrounded = sample;
                hasGrounded = true;
            }
        }

        public void Complete(FallCause cause, Car knocker)
        {
            // ArenaRound calls this after its real classifiers, before TakeOut clears the drive command.
            FallSummary = Summary($"eliminated: cause={cause}, KO by={round.IndexOf(knocker)}");
        }

        public string Summary(string outcome)
        {
            var now = Read();
            var text = new StringBuilder();
            text.AppendLine($"{outcome}; contacts enter={EnterContacts}, credit writes={CreditWrites}, " +
                $"peak speed={F(PeakSpeed)} m/s. Clock: absolute scaled Time.time; round t={F(round.Elapsed)}.");
            text.AppendLine($"At observation: boost age={F(now.Time - now.BoostTime)}, " +
                $"ring age={F(now.Time - now.RingTime)}, hit age={F(now.Time - now.HitTime)}, hit by={now.HitBy}.");
            text.AppendLine($"Gameplay LastGroundedTime={F(player.Movement.LastGroundedTime)}; " +
                $"fall-cause boost/ring/hit ages={F(player.Movement.LastGroundedTime - now.BoostTime)}/" +
                $"{F(player.Movement.LastGroundedTime - now.RingTime)}/{F(player.Movement.LastGroundedTime - now.HitTime)}. " +
                "KO credit still uses elimination time. Hold limit=outward speed cap; removed=radial speed removed " +
                "this step; suppressed=stagger or recent pressure (LastHitTime, including Stay).");
            text.AppendLine("Samples: end of movement FixedUpdate, BEFORE PhysX; terminal row: ArenaRound.Tick, " +
                "AFTER PhysX, using its current (possibly interpolated) transform for fall classification. " +
                "positions below are Rigidbody positions; G=grounded, S=staggered, B=boosting; " +
                "gas/heading are the command after assist, S makes effective gas zero; boost overrides it. " +
                "assist is proximity × difficulty, even if heading is inward/tangent.");
            if (hasGrounded) {
                text.AppendLine($"Time since last grounded={F(now.Time - lastGrounded.Time)}; " +
                    $"boost age then={F(lastGrounded.Time - lastGrounded.BoostTime)}; " +
                    $"ring age then={F(lastGrounded.Time - lastGrounded.RingTime)}.");
            }

            text.AppendLine();
            text.AppendLine("| moment | t | xyz | safe/solid radius | safe/solid edge distance | velocity xyz; radial | " +
                "gas; heading xyz; assist | hold limit/removed/suppressed | GSB | boost/ring/hit time; by | last contact seq @t: other; other/own approach; wrote hit |");
            text.AppendLine("|---|---:|---|---|---|---|---|---|---|---|---|");
            if (hasGrounded) {
                Row(text, "last grounded", lastGrounded);
            }

            var written = 0;
            var lastWritten = float.NegativeInfinity;
            var previous = default(SamplePoint);
            for (var i = 0; i < count; i++) {
                var sample = samples[(next - count + i + samples.Length) % samples.Length];
                if (sample.Time < now.Time - 3f) {
                    continue;
                }

                var changed = i == 0 || sample.Grounded != previous.Grounded || sample.Staggered != previous.Staggered
                    || sample.Boosting != previous.Boosting || sample.SafeRadius != previous.SafeRadius
                    || sample.SolidRadius != previous.SolidRadius || sample.BoostTime != previous.BoostTime
                    || sample.RingTime != previous.RingTime || (sample.Assist > 0f) != (previous.Assist > 0f)
                    || (sample.Assist >= 0.999f) != (previous.Assist >= 0.999f)
                    || (sample.HoldRemoved > 0f) != (previous.HoldRemoved > 0f)
                    || sample.HoldSuppressed != previous.HoldSuppressed
                    || (sample.Edge < 0f) != (previous.Edge < 0f);
                if ((changed || sample.Time - lastWritten >= 0.4f || i == count - 1) && written < 32) {
                    Row(text, changed ? "transition" : "interval", sample);
                    lastWritten = sample.Time;
                    written++;
                }

                previous = sample;
            }

            Row(text, "terminal", now);
            text.AppendLine();
            text.AppendLine($"Classifier transform xyz={V(player.Position)}. Contact extract (up to 12; " +
                "Enter/pre-solver and Stay/post-solver use different axes as described above):");
            var contactRows = 0;
            var lastContactRow = float.NegativeInfinity;
            var previousContact = default(ContactPoint);
            for (var i = 0; i < contactCount; i++) {
                var contact = contacts[(contactNext - contactCount + i + contacts.Length) % contacts.Length];
                if (contact.Time < now.Time - 3f) {
                    continue;
                }

                if (contactRows < 12 && (contact.Entered || contact.Credited != previousContact.Credited
                    || contact.Other != previousContact.Other || contact.Time - lastContactRow >= 0.5f
                    || i == contactCount - 1)) {
                    text.AppendLine($"- #{contact.Sequence} {F(contact.Time)} {(contact.Entered ? "Enter" : "Stay")}: " +
                        $"car {contact.Other}, otherApproach={F(contact.OtherApproach)}, ownApproach={F(contact.OwnApproach)}, " +
                        $"LastHitBy written={contact.Credited}.");
                    lastContactRow = contact.Time;
                    contactRows++;
                }

                previousContact = contact;
            }

            if (written == 32 || contactRows == 12) {
                text.AppendLine("Extract capped; terminal and last-grounded rows are always included.");
            }

            return text.ToString();
        }

        private SamplePoint Read()
        {
            var movement = player.Movement;
            var position = movement.DiagnosticPosition;
            var distance = new Vector2(position.x, position.z).magnitude;
            return new SamplePoint {
                Time = Time.time,
                Position = position,
                Velocity = movement.Velocity,
                Radial = distance > 0.001f ? (position.x * movement.Velocity.x + position.z * movement.Velocity.z) / distance : 0f,
                SafeRadius = edge.SafeExtent,
                SolidRadius = edge.SolidExtent,
                Edge = edge.DistanceToEdge(position),
                SolidEdge = edge.DistanceToSolidEdge(position),
                Throttle = movement.DiagnosticThrottle,
                Direction = movement.DiagnosticDirection,
                Assist = assist,
                HoldLimit = movement.DiagnosticHoldLimit,
                HoldRemoved = movement.DiagnosticHoldRemoved,
                HoldSuppressed = movement.DiagnosticHoldSuppressed,
                Grounded = movement.IsGrounded,
                Staggered = movement.IsStaggered,
                Boosting = movement.IsBoosting,
                BoostTime = movement.LastBoostTime,
                RingTime = player.LastRingFallTime,
                HitTime = player.LastHitTime,
                HitBy = round.IndexOf(player.LastHitBy),
                Contact = lastContact,
            };
        }

        private static void Row(StringBuilder text, string moment, SamplePoint sample)
        {
            var contact = sample.Contact;
            text.AppendLine($"| {moment} | {F(sample.Time)} | {V(sample.Position)} | {F(sample.SafeRadius)}/{F(sample.SolidRadius)} " +
                $"| {F(sample.Edge)}/{F(sample.SolidEdge)} | {V(sample.Velocity)}; {F(sample.Radial)} " +
                $"| {F(sample.Throttle)}; {V(sample.Direction)}; {F(sample.Assist)} " +
                $"| {F(sample.HoldLimit)}/{F(sample.HoldRemoved)}/{sample.HoldSuppressed} " +
                $"| {(sample.Grounded ? 1 : 0)}{(sample.Staggered ? 1 : 0)}{(sample.Boosting ? 1 : 0)} " +
                $"| {F(sample.BoostTime)}/{F(sample.RingTime)}/{F(sample.HitTime)}; {sample.HitBy} " +
                $"| {contact.Sequence} @{F(contact.Time)}: {contact.Other}; " +
                $"{F(contact.OtherApproach)}/{F(contact.OwnApproach)}; {contact.Credited} |");
        }

        private static string F(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        private static string V(Vector3 value) => $"({F(value.x)},{F(value.y)},{F(value.z)})";

        private struct SamplePoint
        {
            public float Time, SafeRadius, SolidRadius, Edge, SolidEdge, Radial, Throttle, Assist, BoostTime, RingTime, HitTime;
            public float HoldLimit, HoldRemoved;
            public int HitBy;
            public Vector3 Position, Velocity, Direction;
            public bool Grounded, Staggered, Boosting;
            public bool HoldSuppressed;
            public ContactPoint Contact;
        }

        private struct ContactPoint
        {
            public int Sequence, Other;
            public float Time, OtherApproach, OwnApproach;
            public bool Credited, Entered;
        }
    }
}
#endif
