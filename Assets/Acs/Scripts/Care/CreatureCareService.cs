using System;
using System.Collections.Generic;
using LumiWorld.Acs.TimeSystem;

namespace LumiWorld.Acs
{
    public enum CreatureHungerStatus { Healthy = 0, Hungry = 1, Eating = 2 }

    public sealed class CreatureCareView
    {
        public string IndividualId { get; internal set; }
        public CreatureQualityRank Rank { get; internal set; }
        public CreatureHungerStatus Status { get; internal set; }
        public double Satiety { get; internal set; }
        public double HungerThresholdSeconds { get; internal set; }
        public double WorkSeconds => (100 - Satiety) * HungerThresholdSeconds / 100;
        public double WorkSecondsRemaining => Satiety * HungerThresholdSeconds / 100;
        public bool IsTakingBite { get; internal set; }
        public string EatingResourceId { get; internal set; }
        public double BiteSecondsRemaining { get; internal set; }
        public bool WaitingForFood => Status == CreatureHungerStatus.Eating && !IsTakingBite;
        public bool CanProduce => Status == CreatureHungerStatus.Healthy;
    }

    public sealed class CreatureCareRules
    {
        private readonly double rankI, rankII, rankIII;
        public double AutoFeedAtSatiety { get; }
        public double FoodUnitsPerSecond { get; }
        public CreatureCareRules(double rankI = 60, double rankII = 90, double rankIII = 120,
            double autoFeedAtSatiety = 50, double foodUnitsPerSecond = 10)
        {
            Positive(rankI); Positive(rankII); Positive(rankIII); Positive(foodUnitsPerSecond);
            if (double.IsNaN(autoFeedAtSatiety) || autoFeedAtSatiety < 0 || autoFeedAtSatiety >= 100)
                throw new ArgumentOutOfRangeException(nameof(autoFeedAtSatiety), "Auto-feed threshold must be in [0, 100).");
            this.rankI = rankI; this.rankII = rankII; this.rankIII = rankIII;
            AutoFeedAtSatiety = autoFeedAtSatiety; FoodUnitsPerSecond = foodUnitsPerSecond;
        }
        public double Threshold(CreatureQualityRank rank)
        {
            switch (rank)
            {
                case CreatureQualityRank.RankI: return rankI;
                case CreatureQualityRank.RankII: return rankII;
                case CreatureQualityRank.RankIII: return rankIII;
                default: throw new ArgumentOutOfRangeException(nameof(rank));
            }
        }
        internal static void Positive(double value)
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be positive and finite.");
        }
    }

    // Per-individual satiety. The coordinator drives this cursor from the shared GameClock.
    // Work and prepaid bite countdowns use Time Core; no UI, species diet or stock access here.
    public sealed class CreatureCareService
    {
        // Accumulating 0.1-second bites may leave sub-nanosecond floating-point residue.
        private const double BoundaryTolerance = 1e-9;
        private sealed class Timeline : ITimeSource
        {
            public double MonotonicSeconds { get; set; }
            public double UtcSeconds => 0; // Owner persistence is not connected yet.
        }
        private sealed class Individual
        {
            public string id, food;
            public CreatureQualityRank rank;
            public double threshold, satiety = 100, biteStart, biteGain, biteDuration;
            public CreatureHungerStatus status;
            public bool biting;
        }
        private readonly Timeline timeline;
        private readonly TimerService timers;
        private readonly Dictionary<string, Individual> individuals = new Dictionary<string, Individual>(StringComparer.Ordinal);
        public double GameSeconds => timeline.MonotonicSeconds;
        public long Revision { get; private set; }

        public CreatureCareService(double initialGameSeconds)
        {
            ValidateTime(initialGameSeconds);
            timeline = new Timeline { MonotonicSeconds = initialGameSeconds };
            timers = new TimerService(new GameClock(timeline));
        }
        private static string WorkId(string id) => "work:" + id;
        private static string BiteId(string id) => "bite:" + id;

        public void Register(string id, CreatureQualityRank rank, CreatureCareRules rules)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Individual ID is required.");
            double threshold = rules.Threshold(rank);
            if (!individuals.TryGetValue(id, out var state))
            {
                state = new Individual { id = id, rank = rank, threshold = threshold };
                timers.StartWork(WorkId(id), threshold);
                individuals.Add(id, state); Revision++;
            }
            else if (state.rank != rank || state.threshold != threshold)
            {
                state.rank = rank; state.threshold = threshold;
                // Satiety is authoritative: changing rank changes future decay, never refills food.
                ResetWork(state); Revision++;
            }
        }
        private void ResetWork(Individual state)
        {
            timers.Cancel(WorkId(state.id));
            timers.StartWork(WorkId(state.id), state.threshold);
            timers.CreditWork(WorkId(state.id), (100 - state.satiety) * state.threshold / 100);
            timers.TryConsumeSignals(WorkId(state.id), out _);
        }
        public bool CanProduce(string id) => id != null && individuals.TryGetValue(id, out var state) &&
            state.status == CreatureHungerStatus.Healthy;

        public CreatureCareView Read(string id)
        {
            if (id == null || !individuals.TryGetValue(id, out var state)) return null;
            return new CreatureCareView { IndividualId = id, Rank = state.rank, Status = state.status,
                Satiety = state.satiety, HungerThresholdSeconds = state.threshold, IsTakingBite = state.biting,
                EatingResourceId = state.biting ? state.food : null,
                BiteSecondsRemaining = state.biting ? timers.Read(BiteId(id)).remainingSeconds : 0 };
        }
        public double SecondsToNextBoundary(ISet<string> workingIndividuals)
        {
            double seconds = double.PositiveInfinity;
            foreach (var state in individuals.Values)
            {
                if (state.biting) seconds = Math.Min(seconds, timers.Read(BiteId(state.id)).remainingSeconds);
                else if (state.status == CreatureHungerStatus.Healthy && workingIndividuals.Contains(state.id))
                    seconds = Math.Min(seconds, timers.Read(WorkId(state.id)).remainingSeconds);
            }
            return seconds;
        }
        // Caller splits at work/capacity/auto-feed/bite boundaries.
        public void AdvanceTo(double gameSeconds, ISet<string> workingIndividuals)
        {
            ValidateTime(gameSeconds);
            if (gameSeconds < GameSeconds) throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            double elapsed = gameSeconds - GameSeconds;
            foreach (string id in workingIndividuals)
            {
                if (!CanProduce(id)) continue;
                var state = individuals[id];
                timers.CreditWork(WorkId(id), elapsed);
                double remainingWork = timers.Read(WorkId(id)).remainingSeconds;
                state.satiety = Math.Max(0, 100 * remainingWork / state.threshold);
                if (timers.TryConsumeSignals(WorkId(id), out _) || remainingWork <= BoundaryTolerance)
                { state.satiety = 0; state.status = CreatureHungerStatus.Hungry; Revision++; }
            }
            timeline.MonotonicSeconds = gameSeconds;
            timers.Tick();
            foreach (var state in individuals.Values)
            {
                if (!state.biting) continue;
                double remaining = timers.Read(BiteId(state.id)).remainingSeconds;
                state.satiety = Math.Min(100, state.biteStart + state.biteGain * (1 - remaining / state.biteDuration));
                if (!timers.TryConsumeSignals(BiteId(state.id), out _) && remaining > BoundaryTolerance) continue;
                state.satiety = Math.Min(100, state.biteStart + state.biteGain);
                state.biting = false; timers.Cancel(BiteId(state.id));
                if (state.satiety >= 100 - 1e-8)
                {
                    state.satiety = 100; state.status = CreatureHungerStatus.Healthy;
                    ResetWork(state); Revision++;
                }
            }
        }
        public bool TryStartEating(string id)
        {
            if (id == null || !individuals.TryGetValue(id, out var state) ||
                state.status == CreatureHungerStatus.Eating || state.satiety >= 100) return false;
            state.status = CreatureHungerStatus.Eating; Revision++; return true;
        }
        // One whole unit is paid before this call. Its nutrition arrives gradually over the bite.
        public bool TryBeginBite(string id, string foodId, double nutrition, double seconds)
        {
            CreatureCareRules.Positive(nutrition); CreatureCareRules.Positive(seconds);
            if (string.IsNullOrWhiteSpace(foodId)) throw new ArgumentException("Food ID is required.");
            if (!individuals.TryGetValue(id, out var state) || state.status != CreatureHungerStatus.Eating || state.biting) return false;
            timers.StartCountdown(BiteId(id), seconds, OfflineTimerPolicy.Advance);
            state.biteStart = state.satiety; state.biteGain = Math.Min(nutrition, 100 - state.satiety);
            state.biteDuration = seconds; state.food = foodId; state.biting = true;
            return true;
        }
        private static void ValidateTime(double value)
        {
            if (value < 0 || double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
