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

            PickupFlask flask = Instantiate(flaskPrefab, launchPoint.position, Random.rotation);
            Rigidbody body = flask.GetComponent<Rigidbody>();

            Vector2 quadrant = QuadrantSigns[TakeNextQuadrant()];
            Vector3 target = new(
                quadrant.x * Random.Range(quadrantLandingRange.x, quadrantLandingRange.y),
                landingHeight,
                quadrant.y * Random.Range(quadrantLandingRange.x, quadrantLandingRange.y));

            body.linearVelocity = CalculateBallisticVelocity(launchPoint.position, target);
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

        private Vector3 CalculateBallisticVelocity(Vector3 origin, Vector3 target)
        {
            float gravity = Mathf.Abs(Physics.gravity.y);
            float verticalSpeed = Mathf.Sqrt(2f * gravity * launchApexHeight);
            float apexY = origin.y + launchApexHeight;
            float riseTime = verticalSpeed / gravity;
            float fallTime = Mathf.Sqrt(2f * Mathf.Max(0.01f, apexY - target.y) / gravity);
            float flightTime = riseTime + fallTime;

            Vector3 horizontalDisplacement = target - origin;
            horizontalDisplacement.y = 0f;
            return horizontalDisplacement / flightTime + Vector3.up * verticalSpeed;
        }

        private void RemoveDestroyedFlasks()
        {
            spawnedFlasks.RemoveAll(flask => flask == null);
        }
    }
}
