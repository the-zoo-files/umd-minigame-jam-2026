using System;
using UmdJam.Gameplay;
using UnityEngine;

namespace UmdJam.Multiplayer
{
    public sealed class GameManager : MonoBehaviour
    {
        public static event Action<float> RoundTimeChanged;
        public static event Action RoundEnded;

        public static GameManager Instance { get; private set; }

        [SerializeField, Min(1f)] private float roundDuration = 60f;
        [SerializeField] private Collider[] playerZones = new Collider[4];

        private float remainingTime;

        public float RemainingTime => remainingTime;
        public bool IsRoundOver { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one GameManager can be active at a time.", this);
                enabled = false;
                return;
            }

            Instance = this;
            Time.timeScale = 1f;
            remainingTime = roundDuration;

            if (!HasValidZones())
            {
                Debug.LogError("GameManager requires four player-zone colliders in player order.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            RoundTimeChanged?.Invoke(remainingTime);
        }

        private void Update()
        {
            if (IsRoundOver)
            {
                return;
            }

            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            RoundTimeChanged?.Invoke(remainingTime);

            if (remainingTime <= 0f)
            {
                EndRound();
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Instance = null;
            Time.timeScale = 1f;
        }

        public void EndRound()
        {
            if (IsRoundOver)
            {
                return;
            }

            IsRoundOver = true;
            remainingTime = 0f;
            ApplyZonePenalties();
            RoundTimeChanged?.Invoke(remainingTime);
            RoundEnded?.Invoke();
            Time.timeScale = 0f;
        }

        private void ApplyZonePenalties()
        {
            PickupFlask[] flasks = FindObjectsByType<PickupFlask>();
            int[] penalties = new int[playerZones.Length];

            foreach (PickupFlask flask in flasks)
            {
                if (flask == null || flask.IsCollected)
                {
                    continue;
                }

                for (int zoneIndex = 0; zoneIndex < playerZones.Length; zoneIndex++)
                {
                    if (playerZones[zoneIndex].bounds.Contains(flask.transform.position))
                    {
                        penalties[zoneIndex] += flask.PointValue;
                        break;
                    }
                }
            }

            for (int zoneIndex = 0; zoneIndex < penalties.Length; zoneIndex++)
            {
                CouchPlayerController.ChangeScore(zoneIndex + 1, -penalties[zoneIndex]);
            }
        }

        private bool HasValidZones()
        {
            if (playerZones == null || playerZones.Length != 4)
            {
                return false;
            }

            foreach (Collider playerZone in playerZones)
            {
                if (playerZone == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
