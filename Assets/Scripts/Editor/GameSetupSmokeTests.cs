using System;
using System.Collections;
using System.IO;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class GameSetupSmokeTests
    {
        public static IEnumerator Exercise()
        {
            Gamepad device = InputSystem.AddDevice<Gamepad>();
            try
            {
                yield return null;
                CouchMultiplayerManager lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                GameManager round = GameManager.Instance;
                VisualElement root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
                Require(lobby.TryJoin(device) && lobby.TrySetPlayerReady(0, true) && lobby.TryAddCpu(3), "Mixed setup fixture");
                CouchPlayerController human = lobby.GetParticipant(0);
                CouchPlayerController existingCpu = lobby.GetParticipant(3);
                Click(root, "startGame");
                yield return null;
                Require(lobby.IsSetupOpen && !round.HasStarted && Time.timeScale == 0f,
                    "OK opens setup without beginning gameplay");
                Require(CouchPlayerController.ActivePlayers.Count == 2, "Setup does not fill empty slots early");
                Require(!root.Q("gameSetup").ClassListContains("is-hidden") &&
                    root.Q("connectionMenu").ClassListContains("is-hidden"), "Setup owns pregame presentation");
                Require(root.Q<Label>("setupCpuLevel").text == "Normal" && lobby.SelectedRoundCount == 3,
                    "Figma settings defaults");
                IEnumerator visualChecks = CaptureSetup();
                while (visualChecks.MoveNext()) yield return visualChecks.Current;
                Click(root, "nextSetupRounds");
                Require(lobby.SelectedRoundCount == 4 && root.Q<Label>("setupRounds").text == "4 Rounds",
                    "Rounds selection stores the future match-system choice");
                Require(!lobby.TrySetRoundCount(0) && !lobby.TrySetRoundCount(10), "Rounds bounds");
                Click(root, "nextSetupCpu");
                Click(root, "nextSetupCpu");
                Require(lobby.SelectedCpuDifficulty == CpuDifficulty.God, "Shared CPU level selection");
                Require(!lobby.TryJoin(device) && !lobby.TryLeave(0), "Setup locks roster edits");
                Click(root, "setupBack");
                Require(!lobby.IsSetupOpen && !round.HasStarted && lobby.GetParticipant(0) == human,
                    "Back preserves the lobby and keeps gameplay paused");
                Click(root, "startGame");
                yield return null;
                Require(lobby.SelectedRoundCount == 4 && lobby.SelectedCpuDifficulty == CpuDifficulty.God,
                    "Back retains selected settings");
                float deadline = Time.unscaledTime + 0.3f;
                while (Time.unscaledTime < deadline) yield return null;
                Click(root, "setupStart");
                yield return null;
                Require(round.IsPlaying && !lobby.IsSetupOpen && Time.timeScale == 1f && lobby.GetParticipant(0) == human,
                    "Setup Start begins gameplay and preserves the human");
                Require(lobby.GetParticipant(3) == existingCpu, "Existing CPU is retained");
                for (int slot = 1; slot < 4; slot++)
                    Require(lobby.GetParticipant(slot).Cpu.Difficulty == CpuDifficulty.God, "Every CPU shares selected difficulty");
                Require(root.Q("gameSetup").ClassListContains("is-hidden") &&
                    !root.Q("gameplayHud").ClassListContains("is-hidden"), "Game replaces setup");
                Require(!lobby.TrySetSharedCpuDifficulty(CpuDifficulty.Noob) && !lobby.TrySetRoundCount(2),
                    "Settings lock during gameplay");
                round.EndRound();
            }
            finally
            {
                if (device.added) InputSystem.RemoveDevice(device);
            }
        }

        internal static void Click(VisualElement root, string name)
        {
            Button button = root.Q<Button>(name);
            Require(button != null, "Required setup button: " + name);
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        private static IEnumerator CaptureSetup()
        {
            UIDocument document = Object.FindAnyObjectByType<UIDocument>();
            PanelSettings panel = document.panelSettings;
            RenderTexture previous = panel.targetTexture;
            RenderTexture capture = null;
            try
            {
                foreach (int width in new[] { 1920, 1440 })
                {
                    // Rounded UI Toolkit clipping uses a stencil buffer.
                    capture = new RenderTexture(width, 1080, 24);
                    capture.Create();
                    panel.targetTexture = capture;
                    for (int frame = 0; frame < 8; frame++) yield return null;
                    VisualElement root = document.rootVisualElement;
                    Rect screen = root.worldBound;
                    foreach (string name in new[] { "setupStart", "setupBack", "previousSetupCpu", "nextSetupCpu",
                        "previousSetupRounds", "nextSetupRounds", "setupPracticePreview" })
                    {
                        Rect bounds = root.Q(name).worldBound;
                        Require(bounds.width > 0f && bounds.height > 0f &&
                            screen.Contains(bounds.min) && screen.Contains(bounds.max), "Setup element fits panel: " + name);
                    }
                    RenderTexture oldActive = RenderTexture.active;
                    Texture2D image = new(width, 1080, TextureFormat.RGBA32, false);
                    try
                    {
                        RenderTexture.active = capture;
                        image.ReadPixels(new Rect(0, 0, width, 1080), 0, 0);
                        image.Apply();
                        Directory.CreateDirectory(".utmp");
                        File.WriteAllBytes($".utmp/game-setup-{width}.png", image.EncodeToPNG());
                    }
                    finally
                    {
                        RenderTexture.active = oldActive;
                        Object.Destroy(image);
                    }
                    panel.targetTexture = previous;
                    capture.Release();
                    Object.Destroy(capture);
                    capture = null;
                }
            }
            finally
            {
                panel.targetTexture = previous;
                if (capture != null)
                {
                    capture.Release();
                    Object.Destroy(capture);
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
