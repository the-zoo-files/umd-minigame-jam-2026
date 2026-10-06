using UmdJam.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace UmdJam.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class CpuPlayerController : MonoBehaviour
    {
        [SerializeField] private CpuSettings noob = new(0.65f, 0.55f, 0f, 0.65f, 0f);
        [SerializeField] private CpuSettings pro = new(0.3f, 0.18f, 0.4f, 0.2f, 0.3f);
        [SerializeField] private CpuSettings hacker = new(0.15f, 0.06f, 1.2f, 0.04f, 0.7f);
        [SerializeField] private CpuSettings god = new(0.08f, 0f, 2f, 0f, 1f);
        [SerializeField, Range(0f, 1f)] private float stagingFraction = 0.35f;

        private NavMeshPath queryPath;
        private readonly Vector3[] queryCorners = new Vector3[64];
        private readonly Vector3[] route = new Vector3[64];
        private NavMeshPath returnPath;
        private readonly Vector3[] returnCorners = new Vector3[64];
        private CouchPlayerController player;
        private CpuNavigation navigation;
        private GameManager round;
        private PlayerFlaskCollector collector;
        private System.Random random;
        private PickupFlask target;
        private int routeCount;
        private int routeIndex;
        private float nextPlan;
        private float readyAt;
        private bool wasCarrying;
        private Vector3 lastProgressPosition;
        private float lastProgressTime;
        private int queryCount;
        private float passingSide = 1f;
        private float recoveryUntil;
        private Vector2 recoveryMovement;

        public CpuDifficulty Difficulty { get; private set; }
        public CpuSettings Settings => Difficulty switch
        {
            CpuDifficulty.Noob => noob,
            CpuDifficulty.Pro => pro,
            CpuDifficulty.Hacker => hacker,
            _ => god
        };

        public void Configure(CouchPlayerController owner, CpuNavigation map, GameManager game, CpuDifficulty difficulty)
        {
            player = owner;
            queryPath = new NavMeshPath();
            returnPath = new NavMeshPath();
            navigation = map;
            round = game;
            Difficulty = difficulty;
            random = new System.Random(7919 * owner.PlayerNumber);
            lastProgressPosition = transform.position;
        }

        public bool CanNavigate()
        {
            collector = PlayerFlaskCollector.GetForPlayer(player.PlayerNumber);
            return collector != null && navigation.TryPath(transform.position,
                collector.ApproachPoint(transform.position), queryPath, queryCorners, out _, out _);
        }

        public bool SetDifficulty(CpuDifficulty difficulty)
        {
            if (round == null || round.HasStarted || difficulty < CpuDifficulty.Noob || difficulty > CpuDifficulty.God)
            {
                return false;
            }
            Difficulty = difficulty;
            return true;
        }

        public void ReadCommand(out Vector2 movement, out bool attack)
        {
            movement = Vector2.zero;
            attack = false;
            if (!isActiveAndEnabled || player == null || round == null || !round.IsPlaying ||
                navigation == null || !navigation.IsReady || collector == null)
            {
                return;
            }
            if (player.MoveSpeed <= 0f) return;

            CpuSettings settings = Settings;
            bool carrying = player.IsCarrying;
            if (carrying != wasCarrying)
            {
                wasCarrying = carrying;
                target = null;
                routeCount = 0;
                nextPlan = 0f;
                readyAt = Time.time + settings.ReactionDelay;
                recoveryUntil = 0f;
            }
            if (Time.time < readyAt) return;

            if (carrying && PlayerFlaskCollector.TryGetNearby(player.PlayerNumber, transform.position, out _))
            {
                attack = true;
                return;
            }

            if (target != null && !target.IsAvailable)
            {
                target = null;
                routeCount = 0;
                nextPlan = 0f;
            }

            if (Time.time >= nextPlan)
            {
                nextPlan = Time.time + settings.PlanningInterval;
                Plan(carrying, settings);
            }
            if (Time.time < readyAt) return;

            if (Time.time < recoveryUntil)
            {
                movement = recoveryMovement;
                return;
            }

            while (routeIndex < routeCount && CpuNavigation.PlanarDistance(transform.position, route[routeIndex]) < 0.18f)
            {
                routeIndex++;
            }
            if (routeIndex >= routeCount) return;

            Vector3 offset = route[routeIndex] - transform.position;
            offset.y = 0f;
            Vector3 direction = offset.normalized;
            // Choose a consistent passing side when another player obstructs this route.
            for (int i = 0; i < CouchPlayerController.ActivePlayers.Count; i++)
            {
                CouchPlayerController other = CouchPlayerController.ActivePlayers[i];
                if (other == null || other == player || !other.isActiveAndEnabled) continue;
                Vector3 separation = other.transform.position - transform.position;
                separation.y = 0f;
                if (separation.sqrMagnitude < 1.6f * 1.6f && Vector3.Dot(separation, direction) > 0f)
                {
                    direction = (direction + Vector3.Cross(Vector3.up, direction) * (0.8f * passingSide)).normalized;
                }
            }

            float speedFraction = Mathf.Min(1f, offset.magnitude / Mathf.Max(0.001f, player.MoveSpeed * Time.deltaTime));
            movement = new Vector2(direction.x, direction.z) * speedFraction;
            if (CpuNavigation.PlanarDistance(transform.position, lastProgressPosition) > 0.3f)
            {
                lastProgressPosition = transform.position;
                lastProgressTime = Time.time;
            }
            else if (Time.time - lastProgressTime > 0.6f)
            {
                // Replan from the actual position; never teleport out of a blockage.
                nextPlan = 0f;
                lastProgressTime = Time.time;
                passingSide = -passingSide;
                Vector3 side = Vector3.Cross(Vector3.up, offset.normalized) * passingSide;
                recoveryMovement = new Vector2(side.x, side.z);
                recoveryUntil = Time.time + 0.25f;
                movement = recoveryMovement;
            }
        }

        private void Plan(bool carrying, CpuSettings settings)
        {
            routeCount = 0;
            routeIndex = 0;
            Vector3 position = transform.position;
            if (carrying)
            {
                Vector3 home = collector.ApproachPoint(position);
                if (TryRoute(home, out float distance)) SaveRoute();
                if (settings.StrategyWeight >= 0.7f && distance / player.MoveSpeed + 0.25f > round.RemainingTime)
                {
                    if (round.GetZonePlayer(position) == player.PlayerNumber) PlanPenaltyEscape(position);
                    else routeCount = 0;
                }
                return;
            }

            PickupFlask previousTarget = target;
            PickupFlask chosen = null;
            float best = float.NegativeInfinity;
            for (int i = 0; i < PickupFlask.ActiveFlasks.Count; i++)
            {
                PickupFlask flask = PickupFlask.ActiveFlasks[i];
                if (flask == null || !flask.IsAvailable) continue;
                Vector3 intercept = PredictIntercept(flask, settings.PredictionHorizon);
                intercept.y = position.y;
                if (!TryRoute(intercept, out float distance)) continue;
                float pickupTime = distance / player.MoveSpeed;
                float returnDistance = CpuNavigation.PlanarDistance(intercept, collector.ApproachPoint(intercept));
                if (settings.StrategyWeight > 0f && !navigation.TryPath(intercept, collector.ApproachPoint(intercept),
                    returnPath, returnCorners, out _, out returnDistance)) continue;
                float returnTime = returnDistance / player.MoveSpeed;
                float cost = 0.3f + pickupTime + returnTime * settings.StrategyWeight;
                float value = Mathf.Max(0.1f, flask.PointValue);
                if (round.GetZonePlayer(flask.transform.position) == player.PlayerNumber)
                {
                    value *= 1f + settings.StrategyWeight * Mathf.Clamp01(5f / Mathf.Max(1f, round.RemainingTime));
                }
                if (pickupTime + returnTime + 0.25f > round.RemainingTime) value *= 0.2f;

                float rivalTime = NearestRivalTime(intercept);
                if (rivalTime + 0.15f < pickupTime) cost += settings.StrategyWeight * (pickupTime - rivalTime + 0.5f);
                float score = value / cost;
                score *= 1f - settings.DecisionNoise * (float)random.NextDouble();
                // Small hysteresis avoids repeatedly abandoning almost-equivalent routes.
                if (flask == previousTarget) score *= 1.05f;
                if (score <= best) continue;
                best = score;
                chosen = flask;
                SaveRoute();
            }
            target = chosen;
            if (chosen != null && chosen != previousTarget) readyAt = Time.time + settings.ReactionDelay;
            if (chosen == null && settings.StrategyWeight >= 0.7f)
            {
                Vector3 staging = Vector3.Lerp(round.ArenaBounds.center, collector.CollectionPoint, stagingFraction);
                if (TryRoute(staging, out _)) SaveRoute();
            }
        }

        private Vector3 PredictIntercept(PickupFlask flask, float horizon)
        {
            Vector3 position = flask.transform.position;
            float time = Mathf.Min(horizon, CpuNavigation.PlanarDistance(transform.position, position) / player.MoveSpeed);
            Vector3 velocity = flask.Velocity;
            if (flask.UsesGravity && Physics.gravity.y < 0f)
            {
                // Stop horizontal extrapolation at the first estimated floor impact.
                float floor = transform.position.y - 0.65f;
                float gravity = -Physics.gravity.y;
                float fall = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * gravity * Mathf.Max(0f, position.y - floor))) / gravity;
                time = Mathf.Min(time, Mathf.Max(0f, fall));
            }
            return position + velocity * time;
        }

        private float NearestRivalTime(Vector3 position)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < CouchPlayerController.ActivePlayers.Count; i++)
            {
                CouchPlayerController other = CouchPlayerController.ActivePlayers[i];
                if (other == null || other == player || !other.isActiveAndEnabled || other.IsCarrying || other.MoveSpeed <= 0f) continue;
                best = Mathf.Min(best, CpuNavigation.PlanarDistance(other.transform.position, position) / other.MoveSpeed);
            }
            return best;
        }

        private void PlanPenaltyEscape(Vector3 position)
        {
            Bounds zone = round.GetPlayerZone(player.PlayerNumber);
            float best = float.PositiveInfinity;
            for (int edge = 0; edge < 4; edge++)
            {
                Vector3 point = position;
                if (edge < 2) point.x = edge == 0 ? zone.min.x - 0.8f : zone.max.x + 0.8f;
                else point.z = edge == 2 ? zone.min.z - 0.8f : zone.max.z + 0.8f;
                if (!round.ArenaBounds.Contains(point) || !TryRoute(point, out float distance) || distance >= best) continue;
                best = distance;
                SaveRoute();
            }
        }

        private bool TryRoute(Vector3 destination, out float distance)
        {
            return navigation.TryPath(transform.position, destination, queryPath, queryCorners, out queryCount, out distance);
        }

        private void SaveRoute()
        {
            routeCount = queryCount;
            routeIndex = 0;
            System.Array.Copy(queryCorners, route, queryCount);
        }
    }
}
