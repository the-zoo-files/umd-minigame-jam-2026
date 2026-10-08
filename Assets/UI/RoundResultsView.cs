using System;
using System.Collections.Generic;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmdJam.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RoundResultsView : MonoBehaviour
    {
        [SerializeField] private Camera arenaCamera;
        [SerializeField, Range(0f, 1f)] private float maskOpacity = 0.65f;

        private VisualElement root;
        private VisualElement masks;
        private VisualElement marker;
        private VisualElement zoneScore;
        private VisualElement winnerPanel;
        private VisualElement standings;
        private readonly VisualElement[] maskEdges = new VisualElement[4];
        private Label flaskPenalty;
        private Label zoneName;
        private Label zoneCount;
        private Label zoneDeduction;
        private Label zoneFinal;
        private Label winnerTitle;
        private Button skip;
        private Button replay;
        private PlayerRoundResult focusedZone;
        private Vector3 markerPosition;
        private bool hasMarker;
        private float markerStarted;
        private Button pendingFocus;

        public event Action SkipRequested;
        public event Action ReplayRequested;
        public bool IsReady { get; private set; }

        public void Begin(RoundResults results)
        {
            focusedZone = null;
            hasMarker = false;
            root.RemoveFromClassList("is-hidden");
            masks.AddToClassList("is-hidden");
            marker.AddToClassList("is-hidden");
            zoneScore.AddToClassList("is-hidden");
            winnerPanel.AddToClassList("is-hidden");
            skip.RemoveFromClassList("is-hidden");
            replay.SetEnabled(false);
            pendingFocus = skip;
        }

        public void FocusZone(PlayerRoundResult player)
        {
            focusedZone = player;
            hasMarker = false;
            marker.AddToClassList("is-hidden");
            masks.RemoveFromClassList("is-hidden");
            zoneScore.RemoveFromClassList("is-hidden");
            zoneScore.AddToClassList("is-counting");
            SetBorder(zoneScore, player.PlayerColor);
            zoneName.text = player.DisplayName;
            zoneCount.text = player.Flasks.Count == 0 ? "No penalty" : $"0 / {player.Flasks.Count} flasks";
            zoneDeduction.text = "";
            zoneFinal.text = $"Score: {player.StartingScore}";
            Project();
        }

        public void ShowFlask(FlaskPenaltyEntry entry, int counted, int count, int penalty)
        {
            markerPosition = entry.WorldPosition;
            markerStarted = Time.unscaledTime;
            hasMarker = true;
            marker.RemoveFromClassList("is-hidden");
            flaskPenalty.text = $"−{entry.Points}";
            zoneCount.text = $"{counted} / {count} flasks";
            zoneDeduction.text = $"−{penalty}";
            Project();
        }

        public void ShowDeduction(PlayerRoundResult player, float progress)
        {
            zoneScore.RemoveFromClassList("is-counting");
            zoneCount.text = $"{player.Flasks.Count} flasks counted";
            hasMarker = false;
            marker.AddToClassList("is-hidden");
            zoneDeduction.text = player.Penalty == 0 ? "No penalty" : $"−{player.Penalty}";
            int displayed = Mathf.RoundToInt(Mathf.Lerp(player.StartingScore, player.FinalScore, progress));
            zoneFinal.text = $"{player.StartingScore} → {displayed}";
        }

        public void ShowWinners(RoundResults results)
        {
            focusedZone = null;
            hasMarker = false;
            masks.AddToClassList("is-hidden");
            marker.AddToClassList("is-hidden");
            zoneScore.AddToClassList("is-hidden");
            skip.AddToClassList("is-hidden");
            winnerPanel.RemoveFromClassList("is-hidden");
            replay.SetEnabled(false);
            List<PlayerRoundResult> sorted = new(results.Players);
            sorted.Sort((a, b) => a.FinalScore != b.FinalScore
                ? b.FinalScore.CompareTo(a.FinalScore) : a.PlayerNumber.CompareTo(b.PlayerNumber));
            winnerTitle.text = sorted.Count <= 1 ? "Round complete" : results.WinnerPlayerNumbers.Count > 1
                ? "It's a tie!" : $"{sorted[0].DisplayName} wins!";
            standings.Clear();
            foreach (PlayerRoundResult player in sorted)
            {
                bool winner = false;
                foreach (int number in results.WinnerPlayerNumbers)
                    if (number == player.PlayerNumber) winner = true;
                VisualElement card = new();
                card.AddToClassList("result-card");
                SetBorder(card, player.PlayerColor);
                Label status = new(winner && sorted.Count > 1 ? "WINNER" : "FINAL SCORE");
                status.AddToClassList("result-card-status");
                Label name = new(player.DisplayName);
                name.AddToClassList("result-card-name");
                Label score = new(player.FinalScore.ToString());
                score.AddToClassList("result-card-score");
                Label breakdown = new($"{player.StartingScore} − {player.Penalty}");
                breakdown.AddToClassList("result-card-breakdown");
                card.Add(status);
                card.Add(name);
                card.Add(score);
                card.Add(breakdown);
                standings.Add(card);
            }
        }

        public void ShowReplay()
        {
            replay.SetEnabled(true);
            pendingFocus = replay;
        }

        public void DisableReplay() => replay.SetEnabled(false);

        public void End()
        {
            pendingFocus = null;
            focusedZone = null;
            hasMarker = false;
            root?.AddToClassList("is-hidden");
        }

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement.Q("roundResults");
            if (root == null || arenaCamera == null)
            {
                Debug.LogError("Round results view requires its arena camera and roundResults UI root.", this);
                return;
            }
            masks = root.Q("zoneMasks");
            marker = root.Q("flaskMarker");
            zoneScore = root.Q("zoneScore");
            winnerPanel = root.Q("winnerPanel");
            standings = root.Q("resultsStandings");
            flaskPenalty = root.Q<Label>("flaskPenalty");
            zoneName = root.Q<Label>("zonePlayerName");
            zoneCount = root.Q<Label>("zoneCount");
            zoneDeduction = root.Q<Label>("zoneDeduction");
            zoneFinal = root.Q<Label>("zoneFinalScore");
            winnerTitle = root.Q<Label>("winnerTitle");
            skip = root.Q<Button>("skipCount");
            replay = root.Q<Button>("playAgain");
            string[] names = { "maskTop", "maskBottom", "maskLeft", "maskRight" };
            for (int i = 0; i < names.Length; i++) maskEdges[i] = root.Q(names[i]);
            IsReady = masks != null && marker != null && zoneScore != null && winnerPanel != null &&
                standings != null && flaskPenalty != null && zoneName != null && zoneCount != null &&
                zoneDeduction != null && zoneFinal != null && winnerTitle != null && skip != null && replay != null;
            foreach (VisualElement edge in maskEdges) IsReady &= edge != null;
            if (!IsReady)
            {
                Debug.LogError("Round results view is missing required named UI elements.", this);
                return;
            }
            foreach (VisualElement edge in maskEdges)
                edge.style.backgroundColor = new Color(15f / 255f, 12f / 255f, 43f / 255f, maskOpacity);
            skip.clicked += OnSkip;
            replay.clicked += OnReplay;
            End();
        }

        private void OnDisable()
        {
            if (skip != null) skip.clicked -= OnSkip;
            if (replay != null) replay.clicked -= OnReplay;
            End();
            IsReady = false;
        }

        private void LateUpdate()
        {
            if (!IsReady || root.ClassListContains("is-hidden")) return;
            // A newly unhidden tree is not focusable until UI Toolkit resolves its styles.
            if (pendingFocus != null && pendingFocus.canGrabFocus)
            {
                pendingFocus.Focus();
                pendingFocus = null;
            }
            Project();
            if (hasMarker)
            {
                float pulse = 1f + 0.35f * (1f - Mathf.Clamp01((Time.unscaledTime - markerStarted) / 0.2f));
                marker.style.scale = new Scale(new Vector3(pulse, pulse, 1f));
            }
        }

        private void Project()
        {
            if (root.panel == null || root.layout.width <= 0 || float.IsNaN(root.layout.width)) return;
            if (focusedZone != null)
            {
                Bounds bounds = focusedZone.ZoneBounds;
                // The results target is overhead, so the four ground corners form an axis-aligned opening.
                Vector2 a = ToLocal(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));
                Vector2 b = ToLocal(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
                float width = root.layout.width;
                float height = root.layout.height;
                float left = Mathf.Clamp(Mathf.Min(a.x, b.x), 0, width);
                float right = Mathf.Clamp(Mathf.Max(a.x, b.x), 0, width);
                float top = Mathf.Clamp(Mathf.Min(a.y, b.y), 0, height);
                float bottom = Mathf.Clamp(Mathf.Max(a.y, b.y), 0, height);
                SetRect(maskEdges[0], 0, 0, width, top);
                SetRect(maskEdges[1], 0, bottom, width, height - bottom);
                SetRect(maskEdges[2], 0, top, left, bottom - top);
                SetRect(maskEdges[3], right, top, width - right, bottom - top);
                if (zoneScore.ClassListContains("is-counting"))
                {
                    PositionCentered(zoneScore, new Vector2(width / 2f, 60f));
                }
                else
                {
                    PositionCentered(zoneScore, ToLocal(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)));
                }
            }
            if (hasMarker)
            {
                marker.style.visibility = arenaCamera.WorldToViewportPoint(markerPosition).z > 0
                    ? Visibility.Visible : Visibility.Hidden;
                PositionCentered(marker, ToLocal(markerPosition));
            }
        }

        private Vector2 ToLocal(Vector3 world)
        {
            return root.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, arenaCamera));
        }

        private void PositionCentered(VisualElement element, Vector2 center)
        {
            float width = float.IsNaN(element.layout.width) ? 0f : element.layout.width;
            float height = float.IsNaN(element.layout.height) ? 0f : element.layout.height;
            element.style.left = Mathf.Clamp(center.x - width / 2f, 8f, Mathf.Max(8f, root.layout.width - width - 8f));
            element.style.top = Mathf.Clamp(center.y - height / 2f, 8f, Mathf.Max(8f, root.layout.height - height - 8f));
        }

        private static void SetRect(VisualElement element, float x, float y, float width, float height)
        {
            element.style.left = x;
            element.style.top = y;
            element.style.width = width;
            element.style.height = height;
        }

        private static void SetBorder(VisualElement element, Color color)
        {
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
        }

        private void OnSkip() => SkipRequested?.Invoke();
        private void OnReplay() => ReplayRequested?.Invoke();
    }
}
