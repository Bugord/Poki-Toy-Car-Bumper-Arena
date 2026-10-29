#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Input;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Diagnostic measurements, not outcome assertions. Every case uses a fresh real Game scene on Easy or Normal;
    /// only test setup and input differ. The ordinary VContainer tick order and PhysX player loop remain in charge.
    /// </summary>
    public sealed class EdgeFallScenarioTests : SimulationSceneFixture
    {
        private const string ReportPath = "Logs/edge-fall-report.md";
        private const int TotalTrials = 121;
        private readonly StringBuilder details = new();
        private readonly List<Observation> observations = new();
        private readonly SimulationInput input = new();
        private readonly SimulationProgress progress = new() { Round = 1 };
        private readonly Stopwatch stopwatch = new();
        private int hitStopRecoveries;

        private enum Scenario
        {
            DriveCenter, DriveSpawn, Tangent, Diagonal30, Diagonal60, Mouse,
            BoostCenter, BoostHalf, BoostAssist, TapCenter, TapHalf, TapAssist,
            RamStill, RamPassing, Idle, IdleRing, BotPush, BotStayPush, Novice,
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MeasureEasyEdgeFalls()
        {
            details.Clear();
            observations.Clear();
            hitStopRecoveries = 0;
            stopwatch.Restart();
            // Fewer rendered frames, the same fixed timestep and PhysX. Captured time runs at CPU speed.
            Time.captureDeltaTime = 1f / 30f;
            CreateParent(input, progress);
            WriteReport(false);

            foreach (Scenario scenario in Enum.GetValues(typeof(Scenario))) {
                var runs = scenario == Scenario.Novice ? 40 : 3;
                for (var run = 0; run < runs; run++) {
                    var seed = scenario == Scenario.Novice ? run + 1 : 7100 + run;
                    var angle = run % 3 * 137f;
                    yield return Measure(scenario, seed, angle, 1);
                    WriteReport(false);
                    yield return UnloadGame();
                    UnityEngine.Debug.Log($"Edge falls: {scenario}, {run + 1}/{runs}, seed {seed}, " +
                        $"wall {stopwatch.Elapsed.TotalSeconds:0.0}s.");
                }
            }

            foreach (Scenario scenario in Enum.GetValues(typeof(Scenario))) {
                if (scenario != Scenario.Tangent && scenario != Scenario.Diagonal30 && scenario != Scenario.Diagonal60
                    && !IsBoost(scenario)) {
                    continue;
                }

                for (var run = 0; run < 3; run++) {
                    yield return Measure(scenario, 7100 + run, run * 137f, 7);
                    WriteReport(false);
                    yield return UnloadGame();
                    UnityEngine.Debug.Log($"Edge falls: Normal {scenario}, {run + 1}/3, " +
                        $"wall {stopwatch.Elapsed.TotalSeconds:0.0}s.");
                }
            }

            stopwatch.Stop();
            WriteReport(true);
            UnityEngine.Debug.Log($"Edge fall measurements saved to {Path.GetFullPath(ReportPath)} " +
                $"({stopwatch.Elapsed.TotalSeconds:0.0}s).");
        }

        private IEnumerator Measure(Scenario scenario, int seed, float angle, int visitRound)
        {
            progress.Round = visitRound;
            progress.LosingStreak = 0;
            input.Reset();
            Time.timeScale = 1f;
            UnityEngine.Random.InitState(seed);
            yield return LoadGame();
            var scope = GameScope();
            var container = scope.Container;
            var round = container.Resolve<ArenaRound>();
            var tuning = container.Resolve<GameTuning>();
            var difficulty = container.Resolve<RoundDifficulty>();
            var camera = container.Resolve<ArenaCamera>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            botDriver.Seed = seed;
            Assert.That(difficulty.Level, Is.EqualTo(visitRound == 1 ? 0f : 1f), "Unexpected scenario difficulty.");
            Assert.That(difficulty.EdgeAssist, Is.EqualTo(visitRound == 1 ? 1f : 0.3f), "Unexpected edge assist.");
            round.Place();

            var outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var tangent = Vector3.Cross(Vector3.up, outward);
            var radius = tuning.Arena.Radius;
            var natural = scenario == Scenario.Idle || scenario == Scenario.Novice;
            var boost = IsBoost(scenario);
            var tap = scenario == Scenario.TapCenter || scenario == Scenario.TapHalf || scenario == Scenario.TapAssist;
            var ram = scenario == Scenario.RamStill || scenario == Scenario.RamPassing;
            var pushed = scenario == Scenario.BotPush || scenario == Scenario.BotStayPush;
            var targetRadius = scenario == Scenario.BoostHalf || scenario == Scenario.TapHalf ? radius * 0.5f
                : scenario == Scenario.BoostAssist || scenario == Scenario.TapAssist ? radius - 4f : 0f;
            var startRadius = boost ? targetRadius - 3f
                : scenario == Scenario.DriveCenter ? 0f
                : scenario == Scenario.DriveSpawn || scenario == Scenario.Mouse ? radius * tuning.Arena.SpawnRadius
                : scenario == Scenario.IdleRing ? radius - 1f
                : pushed ? radius - 2f
                : ram ? radius - 6f : radius - 3f;

            var heading = scenario == Scenario.Tangent ? tangent
                : scenario == Scenario.Diagonal30 ? tangent * Mathf.Cos(30f * Mathf.Deg2Rad) + outward * 0.5f
                : scenario == Scenario.Diagonal60 ? tangent * 0.5f + outward * Mathf.Sin(60f * Mathf.Deg2Rad) : outward;
            if (!natural) {
                round.Player.Movement.Place(outward * startRadius + Vector3.up * tuning.Arena.SpawnHeight,
                    Quaternion.LookRotation(heading));
                // Nonparticipants stay counted by ArenaRound, so an isolated drive cannot end early as a win.
                // Their objects are disabled, never marked out; the report identifies these as isolated cases.
                for (var i = 1; i < round.Cars.Count; i++) {
                    round.Cars[i].gameObject.SetActive((ram || pushed) && i == 1);
                }

                if (ram || pushed) {
                    var target = round.Cars[1];
                    var position = outward * (pushed ? startRadius - 4f : radius - 2f);
                    if (scenario == Scenario.RamPassing) {
                        position -= tangent * 2f;
                    }

                    target.Movement.Place(position + Vector3.up * tuning.Arena.SpawnHeight,
                        Quaternion.LookRotation(pushed ? outward : scenario == Scenario.RamPassing ? tangent : -outward));
                }
            }

            // Let the placed bodies settle on the actual collider, without starting clocks or AI.
            for (var frame = 0; frame < 10; frame++) {
                yield return null;
            }

            RoundResult? result = null;
            var eliminationCause = FallCause.None;
            round.Finished += value => {
                result = value;
                if (value.Outcome == RoundOutcome.Out && value.KnockedOutBy == RoundResult.Nobody) {
                    eliminationCause = LegacyFallCause(round.Player, tuning, Time.time);
                }
            };
            round.Begin();
            if (!natural) {
                DisableBotDecisions(botDriver);
                if (scenario == Scenario.RamPassing) {
                    round.Cars[1].Movement.Drive(tangent, 0.35f);
                }

                if (pushed) {
                    round.Cars[1].Movement.Drive(outward, scenario == Scenario.BotPush ? 1f : 0.35f);
                }
            }

            var novice = scenario == Scenario.Novice ? new NoviceDriver(seed, round, camera, input) : null;
            var limit = natural || scenario == Scenario.IdleRing ? tuning.Round.Duration + 2f : 10f;
            var slowFrames = 0;
            var requested = false;
            var releaseFrames = 0;
            var requestNote = "No boost request.";
            var initial = round.Player.Movement.DiagnosticPosition;
            var started = Time.timeAsDouble;
            var realStarted = Time.realtimeSinceStartupAsDouble;
            while (!result.HasValue && Time.timeAsDouble - started < limit) {
                Assert.That(Time.realtimeSinceStartupAsDouble - realStarted, Is.LessThan(90d),
                    $"Scenario stalled in real time: {scenario}, seed {seed}.");
                if (novice != null) {
                    novice.Tick();
                }
                else if (scenario != Scenario.Idle && scenario != Scenario.IdleRing && !pushed) {
                    var position = round.Player.Movement.DiagnosticPosition;
                    // Follow the local tangent/diagonal continuously, rather than aiming along a straight chord.
                    var radial = new Vector3(position.x, 0f, position.z).normalized;
                    var along = Vector3.Cross(Vector3.up, radial);
                    var way = scenario == Scenario.Tangent ? along
                        : scenario == Scenario.Diagonal30 ? along * Mathf.Cos(30f * Mathf.Deg2Rad) + radial * 0.5f
                        : scenario == Scenario.Diagonal60 ? along * 0.5f + radial * Mathf.Sin(60f * Mathf.Deg2Rad) : heading;
                    if (scenario == Scenario.Mouse) {
                        // Just outside the rim, still inside the camera's view margin: a reachable mouse target.
                        var screen = camera.Camera.WorldToScreenPoint(outward * (radius + 1f));
                        input.SetPointer(new PointerDrag(1, screen, screen, true));
                    }
                    else {
                        SetHeading(camera, tap && releaseFrames > 0 ? Vector3.zero : way);
                    }

                    if (boost && !requested && Vector3.Dot(position, outward) >= targetRadius) {
                        if (tap && releaseFrames < 2) {
                            input.SetMove(Vector2.zero);
                            releaseFrames++;
                        }
                        else {
                            requested = true;
                            var before = round.Player.Movement.LastBoostTime;
                            input.PressBoost(tap ? BoostInputMethod.Tap : BoostInputMethod.Key);
                            var accepted = round.Player.Movement.LastBoostTime != before;
                            requestNote = FormattableString.Invariant($"Boost requested at round t={round.Elapsed:0.00}, radius={new Vector2(position.x, position.z).magnitude:0.00}, speed={round.Player.Movement.Velocity.magnitude:0.00}, method={(tap ? "Tap" : "Key")}, accepted={accepted}; release frames={releaseFrames}.");
                        }
                    }
                }

                yield return null;
                slowFrames = Time.timeScale < 1f ? slowFrames + 1 : 0;
                if (slowFrames >= 5) {
                    Time.timeScale = 1f;
                    hitStopRecoveries++;
                    slowFrames = 0;
                }
            }

            var observation = new Observation {
                Scenario = scenario,
                VisitRound = visitRound,
                Seed = seed,
                Fell = result.HasValue && result.Value.Outcome == RoundOutcome.Out,
                Cause = eliminationCause,
                GroundedCause = result?.FallCause ?? FallCause.None,
                Knocker = result?.KnockedOutBy ?? RoundResult.Nobody,
                PlayerKnockouts = round.PlayerKnockouts,
            };
            observations.Add(observation);
            details.AppendLine($"## {observations.Count}. Round {visitRound}, {scenario}: seed {seed}, angle {angle:0}°");
            details.AppendLine();
            details.AppendLine(FormattableString.Invariant($"Fell={observation.Fell}; legacy cause at elimination={observation.Cause}; actual cause at last grounded={observation.GroundedCause}; KO by={observation.Knocker}; player KOs={round.PlayerKnockouts}; round t={round.Elapsed:0.00}; outcome={(result.HasValue ? result.Value.Outcome.ToString() : "observation limit")}. Bots={(natural ? "normal AI, actual spawn" : ram || pushed ? "AI disabled, one dynamic target/pusher" : "disabled")}. Start body xyz={initial}. {requestNote}"));
            details.AppendLine(FormattableString.Invariant($"EdgeAssist={difficulty.EdgeAssist:0.00}; EdgeHoldMargin={tuning.Controls.EdgeHoldMargin:0.00} m; EdgeHoldBrake={tuning.Controls.EdgeHoldBrake:0.00} m/s²; EdgeHoldContactGrace={tuning.Controls.EdgeHoldContactGrace:0.00} s; fixedDeltaTime={Time.fixedDeltaTime:0.000} s."));
            if (boost && !requested) {
                details.AppendLine("Boost trigger was not reached; do not interpret as a successful boost trial.");
            }

            if ((ram || pushed) && round.FallTelemetry.EnterContacts == 0) {
                details.AppendLine("NO CAR CONTACT: scripted contact trial missed its target; not a contact reproduction.");
            }

            details.AppendLine();
            details.AppendLine(round.FallTelemetry.FallSummary ?? round.FallTelemetry.Summary("observation end; no player elimination"));
            details.AppendLine();
        }

        private void SetHeading(ArenaCamera camera, Vector3 heading)
        {
            var local = Quaternion.Inverse(Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f)) * heading;
            input.SetMove(new Vector2(local.x, local.z));
        }

        private static void DisableBotDecisions(BotDriver driver)
        {
            // Test-only: an empty bot list with isDriving=true skips lazy AI creation. Keep the registered Tick in
            // its original place; do not replace the dispatcher, tick anything twice, freeze bodies or alter tuning.
            var field = typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "BotDriver setup changed; update the scenario's AI isolation.");
            field.SetValue(driver, true);
        }

        private static bool IsBoost(Scenario scenario)
        {
            return scenario >= Scenario.BoostCenter && scenario <= Scenario.TapAssist;
        }

        private static FallCause LegacyFallCause(Car car, GameTuning tuning, float eliminationTime)
        {
            // Frozen E1 classifier for comparison only. The authoritative new cause comes from RoundResult.
            if (eliminationTime - car.LastRingFallTime <= tuning.Round.RingFallWindow) {
                return FallCause.Ring;
            }

            if (eliminationTime - car.Movement.LastBoostTime <= tuning.Round.BoostFallWindow) {
                return FallCause.Boost;
            }

            if (car.LastHitBy != null && car.LastHitBy != car && eliminationTime - car.LastHitTime <= tuning.Round.SlideWindow) {
                return FallCause.Slide;
            }

            return FallCause.Drive;
        }

        private void WriteReport(bool complete)
        {
            var report = new StringBuilder("# Edge fall regressions: Easy and Normal\n\n");
            report.AppendLine($"Status: {(complete ? "COMPLETE" : "IN PROGRESS; completed trials only")}; " +
                $"{observations.Count}/{TotalTrials} trials; wall={stopwatch.Elapsed.TotalSeconds:0.0}s; hit-stop recoveries={hitStopRecoveries}.");
            report.AppendLine("\nEasy: visit round 1, 18 scenarios x 3 seeds/angles plus 40 novice rounds. " +
                "Normal: visit round 7, Tangent/Diagonal30/Diagonal60 and six Boost/Tap scenarios x 3 seeds/angles. " +
                "Losing streak=0. Real Game scene, captureDeltaTime=1/30; fixedDeltaTime unchanged. " +
                "Outcomes are measurements, never assertions.\n");
            report.AppendLine("Idle explicitly begins the round, then sends no input. IdleRing stands on the outer ring; " +
                "Idle and Novice keep normal AI and actual spawn. BotPush/BotStayPush use a dynamic scripted pusher " +
                "at full/0.35 throttle against an idle player near the holding line. Other isolated trials disable AI. " +
                "Diagonal angles are OUTWARD FROM THE TANGENT. Isolated trials stop at 10 scaled seconds; " +
                "idle/novice run to the real round end. DriveSpawn uses the spawn radius with an outward-facing nose. " +
                "Mouse targets a visible point 1 m outside the rim. Tap releases steering for two frames, " +
                "then uses steering memory; it stays released afterwards.\n");
            report.AppendLine("The **new** cause is ArenaRound's actual RoundResult.FallCause using LastGroundedTime. " +
                "The **old** cause is the frozen E1 classifier evaluated at elimination on this same trajectory. " +
                "It is not a replay of the old movement. KO credit still uses elimination time. None means " +
                "a credited KO or no fall; missed contacts and blocked/unreached boosts are flagged below.\n");
            report.AppendLine("| visit round | scenario | trials | fell | old drive | new drive | new boost | new ring | new slide | KO by bot | player KOs |");
            report.AppendLine("|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var group in observations.GroupBy(value => new { value.VisitRound, value.Scenario })) {
                report.AppendLine($"| {group.Key.VisitRound} | {group.Key.Scenario} | {group.Count()} | {group.Count(value => value.Fell)} " +
                    $"| {group.Count(value => value.Cause == FallCause.Drive)} | {group.Count(value => value.GroundedCause == FallCause.Drive)} " +
                    $"| {group.Count(value => value.GroundedCause == FallCause.Boost)} | {group.Count(value => value.GroundedCause == FallCause.Ring)} " +
                    $"| {group.Count(value => value.GroundedCause == FallCause.Slide)} | {group.Count(value => value.Knocker != RoundResult.Nobody)} " +
                    $"| {group.Sum(value => value.PlayerKnockouts)} |");
            }

            report.AppendLine("\n## Per-trial classification\n");
            report.AppendLine("| # | visit round | scenario | seed | fell | old cause: elimination | new cause: last grounded | KO by bot | player KOs |");
            report.AppendLine("|---:|---:|---|---:|---|---|---|---:|---:|");
            for (var i = 0; i < observations.Count; i++) {
                var value = observations[i];
                report.AppendLine($"| {i + 1} | {value.VisitRound} | {value.Scenario} | {value.Seed} | {value.Fell} " +
                    $"| {value.Cause} | {value.GroundedCause} | {value.Knocker} | {value.PlayerKnockouts} |");
            }

            report.AppendLine($"\n## {(complete ? "Final" : "Interim")} summary\n");
            foreach (var group in observations.GroupBy(value => value.VisitRound)) {
                report.AppendLine($"- Round {group.Key}: {group.Count()} trials, {group.Count(value => value.Fell)} falls; " +
                    $"{group.Count(value => value.Knocker != RoundResult.Nobody)} credited to bots, " +
                    $"{group.Count(value => value.Fell && value.Knocker == RoundResult.Nobody)} self-falls; " +
                    $"{group.Count(value => value.Cause != value.GroundedCause)} causes changed by the ground-time clock, " +
                    $"including {group.Count(value => value.Cause == FallCause.Drive && value.GroundedCause == FallCause.Boost)} Drive -> Boost; " +
                    $"player KOs total={group.Sum(value => value.PlayerKnockouts)}.");
            }

            report.AppendLine();
            AppendCodeExpectations(report);
            report.Append(details);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, report.ToString());
        }

        private static void AppendCodeExpectations(StringBuilder report)
        {
            report.AppendLine("## E2 expectations, not pass/fail thresholds\n");
            report.AppendLine("E1 measured 9/9 Tangent/Diagonal falls and 12/12 central/half-radius Boost/Tap falls " +
                "on Easy, all labelled Drive. Straight drive, mouse, assisted boosts and rams had no falls. " +
                "Those are the baseline observations, not results from this run.\n");
            report.AppendLine("Holding uses d=max(0, ArenaRadius-EdgeHoldMargin-body radius), " +
                "limit=sqrt(2*EdgeHoldBrake*d), removed=max(0, outward speed-limit)*EdgeAssist. " +
                "Only the outward component is removed, after Steer/boost and before PhysX/StepVelocity. " +
                "The telemetry's hold limit/removed/suppressed columns show each actual correction. " +
                "Current tuning values are printed with every trial.\n");
            report.AppendLine("Easy (share 1): expect the isolated Tangent/Diagonal and central/half-radius Boost/Tap " +
                "falls to disappear; ordinary Drive/Mouse/Ram trials should remain safe. Normal (share 0.3): " +
                "only 30% of excess is removed each fixed step; outward residuals and falls remain possible, " +
                "so a fall-free result is not required. BoostAssist/TapAssist should still reject a purely " +
                "outward Easy boost, while Normal may accept it.\n");
            report.AppendLine("Pressure: holding is disabled while IsStaggered or within EdgeHoldContactGrace of " +
                "LastHitTime, refreshed by a car driving into the player on Enter or pushing on Stay. " +
                "BotPush/BotStayPush should show suppressed=true during pressure and allow the player to be " +
                "knocked out. Compare Easy Idle/Novice KO counts and player KOs to E1/simulation; physics and " +
                "bot decision code are unchanged, but trajectory changes can change statistical outcomes.\n");
            report.AppendLine("Rings: a shrinking safe radius may put the car beyond the holding line. " +
                "The limit then becomes zero, never inward: holding does not snap position or pull back. " +
                "IdleRing should still fall with the ring. Ground-time windows keep Ring/Boost/Slide priority " +
                "and existing 2/1/6 s values. A surviving delayed boost fall should read Boost when boost age " +
                "at last grounded <=1 s, even if its legacy elimination-time cause reads Drive. " +
                "No successful boost fall is required on Easy after holding fixes the trajectory.\n");
        }

        private struct Observation
        {
            public Scenario Scenario;
            public int VisitRound, Seed, PlayerKnockouts;
            public bool Fell;
            public FallCause Cause, GroundedCause;
            public int Knocker;
        }
    }
}
#endif
