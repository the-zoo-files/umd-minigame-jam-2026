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

        public float PlanningInterval => Mathf.Max(0.02f, planningInterval);
        public float ReactionDelay => Mathf.Max(0f, reactionDelay);
        public float PredictionHorizon => Mathf.Max(0f, predictionHorizon);
        public float DecisionNoise => Mathf.Clamp01(decisionNoise);
        public float StrategyWeight => Mathf.Clamp01(strategyWeight);

        public CpuSettings(float interval, float reaction, float prediction, float noise, float strategy)
        {
            planningInterval = interval;
            reactionDelay = reaction;
            predictionHorizon = prediction;
            decisionNoise = noise;
            strategyWeight = strategy;
        }
    }
}
