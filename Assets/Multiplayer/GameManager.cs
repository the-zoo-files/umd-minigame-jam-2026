using System;
using UmdJam.Gameplay;
using UnityEngine;

namespace UmdJam.Multiplayer
{
    public sealed class GameManager : MonoBehaviour
    {
        public static event Action<float> RoundTimeChanged;
        public static event Action RoundEnded;
        public static event Action RoundStarted;

        public static GameManager Instance { get; private set; }

        [SerializeField, Min(1f)] private float roundDuration = 60f;
        [SerializeField] private Collider[] playerZones = new Collider[4];

        private float remainingTime;

        public float RemainingTime => remainingTime;
        public bool IsRoundOver { get; private set; }
        public bool HasStarted { get; private set; }
        public bool IsPlaying => HasStarted && !IsRoundOver;
        public RoundResults Results { get; private set; }

        public Bounds ArenaBounds
        {
            get
            {
                Bounds bounds = playerZones[0].bounds;
                for (int i = 1; i < playerZones.Length; i++) bounds.Encapsulate(playerZones[i].bounds);
                return bounds;
            }
        }

        public int GetZonePlayer(Vector3 position)
        {
            for (int i = 0; i < playerZones.Length; i++)
            {
                if (playerZones[i].bounds.Contains(position)) return i + 1;
            }
            return 0;
        }

        public Bounds GetPlayerZone(int playerNumber) => playerZones[playerNumber - 1].bounds;

        public bool TryStartRound()
        {
            if (!isActiveAndEnabled || HasStarted || IsRoundOver)
            {
                return false;
            }

            HasStarted = true;
            Time.timeScale = 1f;
            RoundTimeChanged?.Invoke(remainingTime);
            RoundStarted?.Invoke();
            return true;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one GameManager can be active at a time.", this);
                enabled = false;
                return;
            }

            Instance = this;
            Time.timeScale = 0f;
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
            if (!IsPlaying)
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
            if (!IsPlaying)
            {
                return;
            }

            IsRoundOver = true;
            remainingTime = 0f;
            Time.timeScale = 0f;
            foreach (PickupFlask flask in PickupFlask.ActiveFlasks)
            {
                if (flask != null) flask.FreezeAttachment();
            }
            Results = RoundResults.Capture(this, CouchPlayerController.ActivePlayers, PickupFlask.ActiveFlasks);
            foreach (PlayerRoundResult player in Results.Players)
            {
                CouchPlayerController.ChangeScore(player.PlayerNumber, -player.Penalty);
            }
            RoundTimeChanged?.Invoke(remainingTime);
            RoundEnded?.Invoke();
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
