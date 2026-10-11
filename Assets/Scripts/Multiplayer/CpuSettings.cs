using System;
using UnityEngine;

namespace UmdJam.Multiplayer
{
    [Serializable]
    public struct CpuSettings
    {
        [SerializeField, Min(0.02f)] private float planningInterval;
        [SerializeField, Min(0f)] private float reactionDelay;
        [SerializeField, Min(0f)] private float predictionHorizon;
        [SerializeField, Range(0f, 1f)] private float decisionNoise;
        [SerializeField, Range(0f, 1f)] private float strategyWeight;
        [SerializeField, Min(1f)] private float awarenessRadius;
        [SerializeField, Min(0f)] private float targetCommitment;
        [SerializeField, Range(0f, 1f)] private float switchAdvantage;
        [SerializeField, Min(0.1f)] private float steeringAcceleration;
        [SerializeField, Min(0.2f)] private float brakingDistance;
        [SerializeField, Min(0f)] private float throwPreparation;

        public float PlanningInterval => Mathf.Max(0.02f, planningInterval);
        public float ReactionDelay => Mathf.Max(0f, reactionDelay);
        public float PredictionHorizon => Mathf.Max(0f, predictionHorizon);
        public float DecisionNoise => Mathf.Clamp01(decisionNoise);
        public float StrategyWeight => Mathf.Clamp01(strategyWeight);
        public float AwarenessRadius => awarenessRadius > 0f ? awarenessRadius : 9f;
        public float TargetCommitment => Mathf.Max(0f, targetCommitment);
        public float SwitchAdvantage => Mathf.Clamp01(switchAdvantage);
        public float SteeringAcceleration => steeringAcceleration > 0f ? steeringAcceleration : 5f;
        public float BrakingDistance => Mathf.Max(0.2f, brakingDistance);
        public float ThrowPreparation => Mathf.Max(0f, throwPreparation);

        public CpuSettings WithoutHandicaps()
        {
            CpuSettings unrestricted = this;
            unrestricted.reactionDelay = 0f;
            unrestricted.decisionNoise = 0f;
            unrestricted.strategyWeight = 1f;
            unrestricted.awarenessRadius = float.PositiveInfinity;
            unrestricted.targetCommitment = 0f;
            unrestricted.switchAdvantage = 0f;
            unrestricted.throwPreparation = 0f;
            return unrestricted;
        }

        public CpuSettings(float interval, float reaction, float prediction, float noise, float strategy,
            float awareness = 9f, float commitment = 1f, float advantage = 0.3f,
            float acceleration = 5f, float braking = 0.8f, float preparation = 0.2f)
        {
            planningInterval = interval;
            reactionDelay = reaction;
            predictionHorizon = prediction;
            decisionNoise = noise;
            strategyWeight = strategy;
            awarenessRadius = awareness;
            targetCommitment = commitment;
            switchAdvantage = advantage;
            steeringAcceleration = acceleration;
            brakingDistance = braking;
            throwPreparation = preparation;
        }
    }
}
