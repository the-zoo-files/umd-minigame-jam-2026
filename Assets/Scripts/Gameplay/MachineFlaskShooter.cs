using System.Collections.Generic;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.Serialization;

namespace UmdJam.Gameplay
{
    public sealed class MachineFlaskShooter : MonoBehaviour
    {
        public static event System.Action<MachineFlaskShooter> FlaskLaunched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents() => FlaskLaunched = null;

        [SerializeField] private Flask[] flasks;
        [SerializeField, FormerlySerializedAs("launchPoint")] private Transform spawnPoint;
        [SerializeField] private float launchInterval = 2.5f;
        [SerializeField, Min(0.05f)] private float finalSecondsLaunchInterval = 0.25f;
        [SerializeField, Min(0f)] private float finalSecondsRampDuration = 10f;
        [SerializeField] private float launchApexHeight = 4.5f;
        [SerializeField] private Vector2 quadrantLandingRange = new(4.5f, 8f);
        [SerializeField] private float landingHeight = 0.35f;
        [SerializeField] private float spinSpeed = 12f;
        [SerializeField] private int maximumActiveFlasks = 12;
        [SerializeField, Min(1)] private int finalSecondsMaximumActiveFlasks = 36;

        private static readonly Vector2[] QuadrantSigns =
        {
            new(-1f, 1f),
            new(1f, 1f),
            new(1f, -1f),
            new(-1f, -1f)
        };

        private readonly List<PickupFlask> spawnedFlasks = new();
        private readonly Dictionary<Flask, Stack<PickupFlask>> pooledFlasks = new();
        private readonly List<int> quadrantBag = new();
        private float launchTimer = 0.75f;

        private void Awake()
        {
            if (!TryGetComponent(out MachineAmbience ambience)) gameObject.AddComponent<MachineAmbience>();
        }

        private void OnDestroy()
        {
            foreach (Stack<PickupFlask> available in pooledFlasks.Values)
            {
                foreach (PickupFlask flask in available)
                {
                    if (flask != null)
                    {
                        Destroy(flask.gameObject);
                    }
                }
            }
            pooledFlasks.Clear();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            launchTimer -= Time.deltaTime;
            if (launchTimer > 0f)
            {
                return;
            }

            launchTimer = CurrentLaunchInterval();
            RemoveDestroyedFlasks();
            if (spawnedFlasks.Count >= CurrentMaximumActiveFlasks())
            {
                return;
            }

            LaunchFlask();
        }

        private float CurrentLaunchInterval()
        {
            float normalInterval = Mathf.Max(0.05f, launchInterval);
            GameManager round = GameManager.Instance;
            if (round == null || !round.IsPlaying || finalSecondsRampDuration <= 0f ||
                round.RemainingTime >= finalSecondsRampDuration)
            {
                return normalInterval;
            }

            float ramp = Mathf.InverseLerp(finalSecondsRampDuration, 0f, round.RemainingTime);
            return Mathf.Lerp(normalInterval, Mathf.Max(0.05f, finalSecondsLaunchInterval), ramp);
        }

        private int CurrentMaximumActiveFlasks()
        {
            int normalMaximum = Mathf.Max(1, maximumActiveFlasks);
            GameManager round = GameManager.Instance;
            if (round == null || !round.IsPlaying || finalSecondsRampDuration <= 0f ||
                round.RemainingTime >= finalSecondsRampDuration)
            {
                return normalMaximum;
            }

            float ramp = Mathf.InverseLerp(finalSecondsRampDuration, 0f, round.RemainingTime);
            return Mathf.CeilToInt(Mathf.Lerp(
                normalMaximum, Mathf.Max(normalMaximum, finalSecondsMaximumActiveFlasks), ramp));
        }

        private void LaunchFlask()
        {
            Flask flaskDefinition = TakeRandomFlask();
            if (flaskDefinition == null || flaskDefinition.Prefab == null || spawnPoint == null)
            {
                Debug.LogError("The flask spawner is missing a flask definition, prefab, or spawn point.", this);
                enabled = false;
                return;
            }

            // Preserve the random draw order used by existing launch tuning.
            Quaternion rotation = Random.rotation;

            Vector2 quadrant = QuadrantSigns[TakeNextQuadrant()];
            Vector3 target = new(
                quadrant.x * Random.Range(quadrantLandingRange.x, quadrantLandingRange.y),
                landingHeight,
                quadrant.y * Random.Range(quadrantLandingRange.x, quadrantLandingRange.y));

            if (!Ballistics.TryCalculateVelocity(spawnPoint.position, target, launchApexHeight, out Vector3 velocity))
            {
                Debug.LogError("Cannot launch flask: check trajectory settings and downward-only gravity.", this);
                enabled = false;
                return;
            }

            PickupFlask flask = TakeFlask(flaskDefinition, spawnPoint.position, rotation);
            Rigidbody body = flask.GetComponent<Rigidbody>();
            body.linearVelocity = velocity;
            body.angularVelocity = Random.onUnitSphere * spinSpeed;
            spawnedFlasks.Add(flask);
            FlaskLaunched?.Invoke(this);
        }

        internal void ReturnToPool(PickupFlask flask, Flask definition)
        {
            if (!spawnedFlasks.Remove(flask))
            {
                return;
            }

            flask.gameObject.SetActive(false);
            // Inactive instances are owned by the machine and die with its scene object.
            flask.transform.SetParent(transform, false);
            if (!pooledFlasks.TryGetValue(definition, out Stack<PickupFlask> available))
            {
                available = new Stack<PickupFlask>();
                pooledFlasks.Add(definition, available);
            }

            if (available.Count < Mathf.Max(maximumActiveFlasks, finalSecondsMaximumActiveFlasks))
            {
                available.Push(flask);
            }
            else
            {
                Destroy(flask.gameObject);
            }
        }

        private PickupFlask TakeFlask(Flask definition, Vector3 position, Quaternion rotation)
        {
            if (pooledFlasks.TryGetValue(definition, out Stack<PickupFlask> available))
            {
                while (available.Count > 0)
                {
                    PickupFlask reused = available.Pop();
                    if (reused != null)
                    {
                        reused.ResetForSpawn(definition, position, rotation);
                        return reused;
                    }
                }
            }

            PickupFlask created = Instantiate(definition.Prefab, position, rotation);
            created.Initialize(definition);
            created.BindPool(this, definition);
            return created;
        }

        private Flask TakeRandomFlask()
        {
            if (flasks == null || flasks.Length == 0)
            {
                return null;
            }

            return flasks[Random.Range(0, flasks.Length)];
        }

        private int TakeNextQuadrant()
        {
            if (quadrantBag.Count == 0)
            {
                for (int i = 0; i < QuadrantSigns.Length; i++)
                {
                    quadrantBag.Add(i);
                }

                for (int i = quadrantBag.Count - 1; i > 0; i--)
                {
                    int swapIndex = Random.Range(0, i + 1);
                    (quadrantBag[i], quadrantBag[swapIndex]) = (quadrantBag[swapIndex], quadrantBag[i]);
                }
            }

            int lastIndex = quadrantBag.Count - 1;
            int quadrant = quadrantBag[lastIndex];
            quadrantBag.RemoveAt(lastIndex);
            return quadrant;
        }

        private void RemoveDestroyedFlasks()
        {
            spawnedFlasks.RemoveAll(flask => flask == null);
        }
    }
}
