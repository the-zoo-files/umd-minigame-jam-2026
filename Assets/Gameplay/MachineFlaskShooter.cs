using System.Collections.Generic;
using UnityEngine;

namespace UmdJam.Gameplay
{
    public sealed class MachineFlaskShooter : MonoBehaviour
    {
        [SerializeField] private PickupFlask flaskPrefab;
        [SerializeField] private Transform launchPoint;
        [SerializeField] private float launchInterval = 2.5f;
        [SerializeField] private float launchApexHeight = 4.5f;
        [SerializeField] private Vector2 quadrantLandingRange = new(4.5f, 8f);
        [SerializeField] private float landingHeight = 0.35f;
        [SerializeField] private float spinSpeed = 12f;
        [SerializeField] private int maximumActiveFlasks = 12;

        private static readonly Vector2[] QuadrantSigns =
        {
            new(-1f, 1f),
            new(1f, 1f),
            new(1f, -1f),
            new(-1f, -1f)
        };

        private readonly List<PickupFlask> spawnedFlasks = new();
        private readonly List<int> quadrantBag = new();
        private float launchTimer = 0.75f;

        private void Update()
        {
            launchTimer -= Time.deltaTime;
            if (launchTimer > 0f)
            {
                return;
            }

            launchTimer = launchInterval;
            RemoveDestroyedFlasks();
            if (spawnedFlasks.Count >= maximumActiveFlasks)
            {
                return;
            }

            LaunchFlask();
        }

        private void LaunchFlask()
        {
            if (flaskPrefab == null || launchPoint == null)
            {
                Debug.LogError("The flask machine is missing its prefab or launch point.", this);
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

            if (!Ballistics.TryCalculateVelocity(launchPoint.position, target, launchApexHeight, out Vector3 velocity))
            {
                Debug.LogError("Cannot launch flask: check trajectory settings and downward-only gravity.", this);
                enabled = false;
                return;
            }

            PickupFlask flask = Instantiate(flaskPrefab, launchPoint.position, rotation);
            Rigidbody body = flask.GetComponent<Rigidbody>();
            body.linearVelocity = velocity;
            body.angularVelocity = Random.onUnitSphere * spinSpeed;
            spawnedFlasks.Add(flask);
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
