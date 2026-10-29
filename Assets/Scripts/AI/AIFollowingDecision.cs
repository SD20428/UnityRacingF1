using System;
using UnityEngine;

namespace Overdrive
{
    public enum AIBehaviourState { RACING, FOLLOWING, PREPARE_PASS, EVADING, RECOVERY, OVERTAKING }

    [Serializable]
    public sealed class AIFollowingSettings
    {
        [Tooltip("Initial tuning values; braking capability must be validated on the actual car.")]
        [Min(.5f)] public float standstillGap = 4;
        [Min(.2f)] public float timeHeadway = 1.4f;
        [Min(.5f)] public float comfortableDeceleration = 6;
        [Min(.1f)] public float emergencyTtc = 1.2f;
        [Min(.05f)] public float maximumObservationAge = .35f;
        [Min(.1f)] public float releaseGapMargin = 5;
        [Min(.1f)] public float clearRoadDelay = .5f;
        [Min(.1f)] public float minimumStateSeconds = .5f;
        [Range(1,20)] public float decisionHz = 10;
        [Min(.1f)] public float prepareAfterSeconds = 2;
        [Min(.1f)] public float passRetrySeconds = 4;
        [Min(1)] public float passSpeedAdvantageKmh = 8;

        public bool Valid => Positive(standstillGap) && Positive(timeHeadway) &&
            Positive(comfortableDeceleration) && Positive(emergencyTtc) && Positive(maximumObservationAge) &&
            Positive(releaseGapMargin) && Positive(clearRoadDelay) && Positive(minimumStateSeconds) &&
            Positive(decisionHz) && Positive(prepareAfterSeconds) && Positive(passRetrySeconds) && Positive(passSpeedAdvantageKmh);
        static bool Positive(float value) => TrackReferenceAdapter.Finite(value) && value > 0;
    }

    public struct AIFollowingInput
    {
        public bool referenceValid, hasLead;
        public float speedMps, cruiseKmh, observationAge;
        public float centreDistance, selfLength, leadLength, leadSpeedMps, closingSpeedMps;
    }

    public struct AIFollowingPlan
    {
        public AIBehaviourState state;
        public float speedLimitKmh, bumperGap, ttc;
        public bool emergencyBrake, blockedByTraffic;
        public string reason;
    }

    // Pure decision layer: consumes a sensor snapshot, never writes vehicle controls.
    public sealed class AIFollowingDecision
    {
        public AIBehaviourState State { get; private set; } = AIBehaviourState.RACING;
        float enteredAt, nextDecision, clearSince = -1, nextPassAttempt;
        float lastLimit = float.PositiveInfinity;

        public void Reset(float now)
        {
            State = AIBehaviourState.RACING;
            enteredAt = nextDecision = nextPassAttempt = now;
            clearSince = -1; lastLimit = float.PositiveInfinity;
        }
        public void Recover(float now)
        { Reset(now); State = AIBehaviourState.RECOVERY; }

        public AIFollowingPlan Evaluate(AIFollowingInput input, AIFollowingSettings settings, float now)
        {
            var plan = new AIFollowingPlan { state = State, speedLimitKmh = float.PositiveInfinity,
                bumperGap = float.PositiveInfinity, ttc = float.PositiveInfinity, reason = "Clear road" };
            if (settings == null || !settings.Valid || !input.referenceValid ||
                !Finite(input.speedMps) || input.speedMps < 0 || !Finite(input.cruiseKmh) ||
                !Finite(input.observationAge) || input.observationAge < 0 || input.observationAge > settings.maximumObservationAge || !Finite(now))
                return Stop(plan, now, "Invalid or stale perception");

            float desiredGap = settings.standstillGap + input.speedMps * settings.timeHeadway;
            bool restricted = false;
            if (input.hasLead)
            {
                if (!Finite(input.centreDistance) || input.centreDistance <= 0 || !Finite(input.selfLength) ||
                    !Finite(input.leadLength) || input.selfLength <= 0 || input.leadLength <= 0 ||
                    !Finite(input.leadSpeedMps) || !Finite(input.closingSpeedMps))
                    return Stop(plan, now, "Invalid lead observation");
                float leadSpeed = Mathf.Max(0, input.leadSpeedMps);
                float closing = Mathf.Max(0, input.closingSpeedMps);
                plan.bumperGap = Mathf.Max(0, input.centreDistance - (input.selfLength + input.leadLength) * .5f - closing * input.observationAge);
                plan.ttc = closing > .01f ? plan.bumperGap / closing : float.PositiveInfinity;
                float usable = Mathf.Max(0, plan.bumperGap - settings.standstillGap);
                float brakingLimit = Mathf.Sqrt(leadSpeed * leadSpeed + 2 * settings.comfortableDeceleration * usable);
                float headwayLimit = Mathf.Max(0, leadSpeed + (plan.bumperGap - desiredGap) / settings.timeHeadway);
                plan.speedLimitKmh = Mathf.Min(brakingLimit, headwayLimit) * 3.6f;
                restricted = plan.speedLimitKmh < input.cruiseKmh || plan.bumperGap < desiredGap + settings.releaseGapMargin;
                plan.blockedByTraffic = restricted;
                if (plan.bumperGap < .5f || (closing > .1f &&
                    (plan.ttc <= settings.emergencyTtc || plan.bumperGap <= settings.standstillGap)))
                {
                    Change(AIBehaviourState.EVADING, now);
                    clearSince = -1; lastLimit = 0;
                    plan.state = State; plan.speedLimitKmh = 0; plan.emergencyBrake = true;
                    plan.reason = "Emergency gap/TTC: braking first"; return plan;
                }
            }

            if (restricted)
            {
                clearSince = -1;
                plan.reason = "Distance/headway/braking limit";
                if (now >= nextDecision)
                {
                    nextDecision = now + 1 / settings.decisionHz;
                    if (State == AIBehaviourState.RACING || State == AIBehaviourState.RECOVERY ||
                        (State == AIBehaviourState.EVADING && now - enteredAt >= settings.minimumStateSeconds))
                        Change(AIBehaviourState.FOLLOWING, now);
                    else if (State == AIBehaviourState.PREPARE_PASS && now - enteredAt >= settings.minimumStateSeconds)
                    { Change(AIBehaviourState.FOLLOWING, now); nextPassAttempt = now + settings.passRetrySeconds; }
                    else if (State == AIBehaviourState.FOLLOWING && now - enteredAt >= settings.prepareAfterSeconds &&
                        now >= nextPassAttempt && input.cruiseKmh - Mathf.Max(0, input.leadSpeedMps) * 3.6f >= settings.passSpeedAdvantageKmh)
                        Change(AIBehaviourState.PREPARE_PASS, now);
                }
                if (State == AIBehaviourState.PREPARE_PASS) plan.reason = "Pass deferred: validated trajectory unavailable";
            }
            else if (State != AIBehaviourState.RACING)
            {
                if (clearSince < 0) clearSince = now;
                if (now >= nextDecision && now - clearSince >= settings.clearRoadDelay && now - enteredAt >= settings.minimumStateSeconds)
                { Change(AIBehaviourState.RACING, now); nextDecision = now + 1 / settings.decisionHz; }
                else
                {
                    plan.speedLimitKmh = Mathf.Min(plan.speedLimitKmh, lastLimit);
                    plan.reason = "Waiting for stable clear road";
                }
            }
            // Emergency exits retain braking until the state hold time has elapsed.
            if (State == AIBehaviourState.EVADING)
            { plan.speedLimitKmh = 0; plan.emergencyBrake = true; }
            plan.state = State; lastLimit = plan.speedLimitKmh; return plan;
        }
        AIFollowingPlan Stop(AIFollowingPlan plan, float now, string reason)
        {
            Change(AIBehaviourState.EVADING, Finite(now) ? now : 0);
            clearSince = -1; lastLimit = 0;
            plan.state = State; plan.speedLimitKmh = 0; plan.emergencyBrake = true; plan.reason = reason;
            return plan;
        }
        void Change(AIBehaviourState state, float now)
        { if (State == state) return; State = state; enteredAt = now; }
        static bool Finite(float value) => TrackReferenceAdapter.Finite(value);
    }
}
