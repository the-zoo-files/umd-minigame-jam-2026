using System;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmdJam.UI
{
    // Owns only setup presentation; lobby state and settings remain with the manager.
    public sealed class CouchGameSetup : IDisposable
    {
        private readonly CouchMultiplayerManager multiplayer;
        private readonly VisualElement root;
        private readonly VisualElement screen;
        private readonly VisualElement canvas;
        private readonly Button start;
        private readonly Button back;
        private readonly Button previousRounds;
        private readonly Button nextRounds;
        private readonly Button previousCpu;
        private readonly Button nextCpu;
        private readonly Label rounds;
        private readonly Label cpuLevel;
        private readonly Label status;
        private readonly VisualElement[] cards = new VisualElement[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] readyLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] readyShadows = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] readyStrokes = new Label[CouchMultiplayerManager.MaximumPlayers];
        private bool wasOpen;

        public CouchGameSetup(VisualElement documentRoot, CouchMultiplayerManager manager)
        {
            root = documentRoot;
            multiplayer = manager;
            screen = Required<VisualElement>("gameSetup");
            canvas = Required<VisualElement>("setupCanvas");
            start = Required<Button>("setupStart");
            back = Required<Button>("setupBack");
            previousRounds = Required<Button>("previousSetupRounds");
            nextRounds = Required<Button>("nextSetupRounds");
            previousCpu = Required<Button>("previousSetupCpu");
            nextCpu = Required<Button>("nextSetupCpu");
            rounds = Required<Label>("setupRounds");
            cpuLevel = Required<Label>("setupCpuLevel");
            status = Required<Label>("setupStatus");
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = Required<VisualElement>($"setupPlayer{i + 1}");
                readyLabels[i] = Required<Label>($"setupReady{i + 1}");
                readyShadows[i] = Required<Label>($"setupReadyShadow{i + 1}");
                readyStrokes[i] = Required<Label>($"setupReadyStroke{i + 1}");
            }
            start.clicked += StartGame;
            back.clicked += Back;
            previousRounds.clicked += PreviousRounds;
            nextRounds.clicked += NextRounds;
            previousCpu.clicked += PreviousCpu;
            nextCpu.clicked += NextCpu;
            screen.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            multiplayer.LobbyChanged += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            start.clicked -= StartGame;
            back.clicked -= Back;
            previousRounds.clicked -= PreviousRounds;
            nextRounds.clicked -= NextRounds;
            previousCpu.clicked -= PreviousCpu;
            nextCpu.clicked -= NextCpu;
            screen.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            multiplayer.LobbyChanged -= Refresh;
        }

        private T Required<T>(string name) where T : VisualElement
        {
            return root.Q<T>(name) ?? throw new InvalidOperationException("Game setup is missing " + name + ".");
        }

        private void StartGame() => multiplayer.TryStartGame();
        private void Back() => multiplayer.TryCloseSetup();
        private void PreviousRounds() => multiplayer.TrySetRoundCount(multiplayer.SelectedRoundCount - 1);
        private void NextRounds() => multiplayer.TrySetRoundCount(multiplayer.SelectedRoundCount + 1);
        private void PreviousCpu() => multiplayer.TrySetSharedCpuDifficulty(multiplayer.SelectedCpuDifficulty - 1);
        private void NextCpu() => multiplayer.TrySetSharedCpuDifficulty(multiplayer.SelectedCpuDifficulty + 1);

        private void Refresh()
        {
            bool open = multiplayer.IsLobbyOpen && multiplayer.IsSetupOpen;
            screen.EnableInClassList("is-hidden", !open);
            if (open)
            {
                int count = multiplayer.SelectedRoundCount;
                rounds.text = $"{count} Round{(count == 1 ? "" : "s")}";
                rounds.tooltip = "Round selection is saved. Gameplay currently runs one round.";
                cpuLevel.text = multiplayer.SelectedCpuDifficulty == CpuDifficulty.Pro
                    ? "Normal" : multiplayer.SelectedCpuDifficulty.ToString();
                cpuLevel.tooltip = "Applies to every CPU player.";
                previousRounds.SetEnabled(count > 1);
                nextRounds.SetEnabled(count < 9);
                previousCpu.SetEnabled(multiplayer.SelectedCpuDifficulty > CpuDifficulty.Noob);
                nextCpu.SetEnabled(multiplayer.SelectedCpuDifficulty < CpuDifficulty.God);
                status.text = multiplayer.StartFailure ??
                    (multiplayer.CanStart ? "" : "A player is unavailable. Go Back to reconnect and ready up.");
                status.EnableInClassList("is-hidden", string.IsNullOrEmpty(status.text));
                for (int i = 0; i < cards.Length; i++)
                {
                    CouchPlayerController player = multiplayer.GetParticipant(i);
                    bool ready = multiplayer.IsPlayerReady(player);
                    string text = ready ? "Ready!" : player != null ? "Wait" : "";
                    readyLabels[i].text = text;
                    readyShadows[i].text = text;
                    readyStrokes[i].text = text;
                    cards[i].style.backgroundColor = ready ? player.PlayerColor : new Color(0.906f, 0.886f, 0.855f);
                    readyStrokes[i].style.color = ready ? player.PlayerColor : new Color(0.922f, 0.337f, 0.157f);
                    readyStrokes[i].style.unityTextOutlineColor = readyStrokes[i].style.color;
                    cards[i].tooltip = player != null ? player.DisplayName : $"Slot {i + 1} becomes a CPU on Start.";
                }
                ResizeCanvas();
                if (!wasOpen) screen.schedule.Execute(() => { if (multiplayer.IsSetupOpen) start.Focus(); }).StartingIn(250);
            }
            else if (wasOpen && multiplayer.IsLobbyOpen)
            {
                root.Q<Button>("startGame")?.Focus();
            }
            wasOpen = open;
        }

        private void OnGeometryChanged(GeometryChangedEvent _) => ResizeCanvas();

        private void ResizeCanvas()
        {
            float width = screen.contentRect.width;
            float height = screen.contentRect.height;
            if (width <= 0f || height <= 0f) return;
            float scale = Mathf.Min(width / 1920f, height / 1080f);
            canvas.style.scale = new Scale(new Vector3(scale, scale, 1f));
            canvas.style.left = (width - 1920f * scale) * 0.5f;
            canvas.style.top = (height - 1080f * scale) * 0.5f;
        }
    }
}
