using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UmdJam.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class RoundResultsSmokeTests
    {
        public static IEnumerator Exercise()
        {
            List<Flask> definitions = new();
            Gamepad device = null;
            Keyboard keyboard = null;
            InputSettings settings = InputSystem.settings;
            var background = settings.backgroundBehavior;
            var editorInput = settings.editorInputBehaviorInPlayMode;
            bool runInBackground = Application.runInBackground;
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Application.runInBackground = true;
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                yield return null;
                GameManager round = GameManager.Instance;
                CouchMultiplayerManager lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
                RoundResultsDirector director = Object.FindAnyObjectByType<RoundResultsDirector>();
                Require(director != null, "Scene has a results director");
                UIDocument document = Object.FindAnyObjectByType<UIDocument>();
                VisualElement root = document.rootVisualElement;
                Camera camera = Camera.main;
                Vector3 originalCamera = camera.transform.position;
                device = InputSystem.AddDevice<Gamepad>();
                Require(lobby.TrySetPlayerCount(3) && lobby.TryJoin(device) &&
                    lobby.TryAddCpu(1) && lobby.TryAddCpu(2), "Mixed results roster");
                Require(lobby.TryStartGame(), "Results fixture starts");
                lobby.GetParticipant(1).Cpu.enabled = false;
                lobby.GetParticipant(2).Cpu.enabled = false;
                CouchPlayerController.ChangeScore(1, 40);
                CouchPlayerController.ChangeScore(2, 5);
                CouchPlayerController.ChangeScore(3, 18);
                CheckBreakdowns();
                Require(round.GetZonePlayer(new Vector3(0, 1, 3)) == 1, "Shared boundary assigned once to P1");
                PickupFlask first = Spawn(new Vector3(-4, 1, 4), 3, definitions);
                Spawn(new Vector3(-2, 1, 4), 7, definitions);
                Spawn(new Vector3(0, 1, 3), 2, definitions);
                Spawn(new Vector3(3, 1, 3), 7, definitions);
                Spawn(new Vector3(-3, 6, 3), 100, definitions);
                Spawn(new Vector3(30, 1, 3), 100, definitions);
                Spawn(new Vector3(-3, 1, -3), 100, definitions);
                PickupFlask inactive = Spawn(new Vector3(-3, 1, 3), 100, definitions);
                inactive.gameObject.SetActive(false);
                Transform socket = new GameObject("Results fixture socket").transform;
                socket.position = new Vector3(-2, 1, 2);
                PickupFlask held = Spawn(socket.position, 4, definitions);
                Require(held.TryPickUp(socket), "Held fixture");
                PickupFlask transfer = Spawn(socket.position, 6, definitions);
                Require(transfer.TryPickUp(socket), "Transfer pickup");
                PlayerFlaskCollector collector = PlayerFlaskCollector.GetForPlayer(1);
                Require(collector.TryTransfer(transfer, 30f), "Long transfer starts before timeout");
                PickupFlask collected = Spawn(socket.position, 100, definitions);
                Require(collected.TryPickUp(socket) && collected.TryThrow(Vector3.zero, 1) &&
                    collected.TryCollect(1, out _), "Collected fixture");
                RoundResults before = RoundResults.Capture(round, CouchPlayerController.ActivePlayers, PickupFlask.ActiveFlasks);
                Require(before.Players.Count == 3 && before.Players[0].Penalty == 22 &&
                    before.Players[0].Flasks.Count == 5, "Held, transfer, boundary included; inactive/collected/outside excluded");
                Require(before.Players[1].FinalScore == -2 && before.Players[2].FinalScore == 18 &&
                    before.WinnerPlayerNumbers.Count == 2 && before.WinnerPlayerNumbers[0] == 1 &&
                    before.WinnerPlayerNumbers[1] == 3, "Tie, negative score, absent slot");
                Set(round, "remainingTime", 0.001f);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!round.IsRoundOver && Time.realtimeSinceStartup < deadline) yield return null;
                Require(round.IsRoundOver && round.RemainingTime == 0 && Time.timeScale == 0, "Actual timer timeout freezes round");
                Require(root.Q<Label>("roundTimer").text == "00:00", "Timer reaches zero");
                MachineFlaskShooter stoppedMachine = Object.FindAnyObjectByType<MachineFlaskShooter>();
                int flaskCountAtEnd = PickupFlask.ActiveFlasks.Count;
                Set(stoppedMachine, "launchTimer", 0f);
                typeof(MachineFlaskShooter).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(stoppedMachine, null);
                Require(PickupFlask.ActiveFlasks.Count == flaskCountAtEnd, "Spawner cannot launch after timeout snapshot");
                Require(lobby.GetParticipant(0).Score == 18 && lobby.GetParticipant(1).Score == -2,
                    "Authoritative scores finalized immediately");
                Require(director.IsPresenting && root.ClassListContains("results-active"), "Results owns presentation");
                foreach (Renderer fixture in GameObject.Find("CeilingLights").GetComponentsInChildren<Renderer>())
                    Require(fixture.forceRenderingOff, "Ceiling fixture geometry does not obscure the overhead arena");
                Require(root.Q("gameplayHud").resolvedStyle.display == DisplayStyle.None, "Final HUD scores hidden");
                Vector3 saved = round.Results.Players[0].Flasks[0].WorldPosition;
                Object.Destroy(first.gameObject);
                round.EndRound();
                collector.Collect(transfer);
                Require(!transfer.IsCollected && !collector.TryTransfer(held, 1f) &&
                    lobby.GetParticipant(0).Score == 18, "Post-timeout collection and repeated end cannot change score");
                Require(round.Results.Players[0].Flasks[0].WorldPosition == saved, "Destroyed visual leaves snapshot intact");
                Vector3 playerPosition = lobby.GetParticipant(0).transform.position;
                Vector3 transferPosition = transfer.transform.position;
                HashSet<string> counts = new();
                bool moved = false;
                deadline = Time.realtimeSinceStartup + 60f;
                while (!director.IsComplete && Time.realtimeSinceStartup < deadline)
                {
                    moved |= Vector3.Distance(camera.transform.position, originalCamera) > 0.1f;
                    if (root.Q<Label>("zonePlayerName").text == "Player 1")
                        counts.Add(root.Q<Label>("zoneCount").text);
                    yield return null;
                }
                Require(director.IsComplete && moved, "Camera and complete reveal run while time is paused");
                Require(Vector3.Dot(camera.transform.forward, Vector3.down) > 0.999f, "Camera finishes overhead");
                Require(counts.Contains("1 / 5 flasks") && counts.Contains("5 / 5 flasks"), "Each zone's count advances visibly");
                Require(lobby.GetParticipant(0).transform.position == playerPosition &&
                    transfer.transform.position == transferPosition, "Gameplay and transfer remain frozen during reveal");
                Require(root.Q<Label>("winnerTitle").text == "It's a tie!" &&
                    root.Q("resultsStandings").childCount == 3, "Tie view contains only participants");
                yield return null;
                Directory.CreateDirectory(".utmp");
                ScreenCapture.CaptureScreenshot(".utmp/round-results-tie.png");
                yield return null;
                yield return null;
                yield return null;
                // Re-enable must reuse the same saved result, restore camera on disable, and never rescore.
                director.enabled = false;
                foreach (Renderer fixture in GameObject.Find("CeilingLights").GetComponentsInChildren<Renderer>())
                    Require(!fixture.forceRenderingOff, "Ceiling fixture geometry restored when results releases camera");
                Require(Vector3.Distance(camera.transform.position, originalCamera) < 0.001f && Time.timeScale == 0,
                    "Disabling restores camera without unpausing");
                director.enabled = true;
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(device, new GamepadState().WithButton(GamepadButton.South));
                yield return null;
                yield return null;
                Require(director.IsComplete, "Gamepad submit reaches focused Skip count through UI Toolkit");
                director.SkipToResults();
                deadline = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < deadline) yield return null;
                Require(director.IsComplete && !root.Q<Button>("playAgain").enabledSelf,
                    "Held submit cannot skip and replay in one press");
                InputSystem.QueueStateEvent(device, new GamepadState());
                deadline = Time.realtimeSinceStartup + 3f;
                while (!root.Q<Button>("playAgain").enabledSelf && Time.realtimeSinceStartup < deadline) yield return null;
                Require(root.Q<Button>("playAgain").enabledSelf, "Released submit enables replay while paused");
                InputSystem.QueueStateEvent(device, new GamepadState().WithButton(GamepadButton.South));
                deadline = Time.realtimeSinceStartup + 12f;
                while (GameManager.Instance == round && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                yield return null;
                Require(GameManager.Instance != null && GameManager.Instance != round &&
                    !GameManager.Instance.HasStarted && Time.timeScale == 0 &&
                    CouchPlayerController.ActivePlayers.Count == 0 && PickupFlask.ActiveFlasks.Count == 0,
                    "Replay reloads a fresh paused lobby exactly once");
                lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
                Require(lobby.TrySetPlayerCount(1) && lobby.TryAddCpu(0) && lobby.TryStartGame(), "Second CPU-only round starts");
                InputSystem.QueueStateEvent(device, new GamepadState());
                yield return null;
                round = GameManager.Instance;
                Transform freezeSocket = new GameObject("Freeze socket").transform;
                freezeSocket.position = new Vector3(-2, 1, 2);
                PickupFlask frozenTransfer = Spawn(freezeSocket.position, 0, definitions);
                Require(frozenTransfer.TryPickUp(freezeSocket) &&
                    PlayerFlaskCollector.GetForPlayer(1).TryTransfer(frozenTransfer, 1f), "Same-frame transfer fixture");
                PickupFlask frozenHeld = Spawn(freezeSocket.position, 0, definitions);
                Require(frozenHeld.TryPickUp(freezeSocket), "Same-frame held fixture");
                CouchPlayerController.ChangeScore(1, -5);
                round.EndRound();
                Vector3 transferAtEnd = frozenTransfer.transform.position;
                typeof(PickupFlask).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(frozenTransfer, null);
                Require(frozenTransfer.transform.position == transferAtEnd, "Transfer cannot advance later in the timeout frame");
                Vector3 heldAtEnd = frozenHeld.transform.position;
                freezeSocket.position += Vector3.right;
                typeof(PickupFlask).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(frozenHeld, null);
                Require(frozenHeld.transform.position == heldAtEnd, "Held flask cannot follow an animated socket after timeout");
                Set(lobby.GetParticipant(0), "heldFlask", frozenHeld);
                Set(lobby.GetParticipant(0), "throwPending", true);
                typeof(CouchPlayerController).GetMethod("ReleaseFlaskFromAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(lobby.GetParticipant(0), null);
                Require(frozenHeld.IsHeld, "Late throw animation event cannot release after timeout");
                director = Object.FindAnyObjectByType<RoundResultsDirector>();
                director.SkipToResults();
                yield return null;
                root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
                Require(root.Q<Label>("winnerTitle").text == "Round complete" && round.Results.Players[0].FinalScore == -5,
                    "Single participant and negative score results");
                InputSystem.RemoveDevice(device);
                keyboard = InputSystem.AddDevice<Keyboard>();
                deadline = Time.realtimeSinceStartup + 5f;
                while (!root.Q<Button>("playAgain").enabledSelf && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                deadline = Time.realtimeSinceStartup + 12f;
                while (GameManager.Instance == round && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                Require(GameManager.Instance != null && GameManager.Instance != round &&
                    !GameManager.Instance.HasStarted && CouchPlayerController.ActivePlayers.Count == 0,
                    "Keyboard replay works after controller disconnect and a second round");
            }
            finally
            {
                settings.backgroundBehavior = background;
                Application.runInBackground = runInBackground;
                settings.editorInputBehaviorInPlayMode = editorInput;
                if (device != null && device.added) InputSystem.RemoveDevice(device);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                foreach (Flask definition in definitions) if (definition != null) Object.Destroy(definition);
            }
        }

        private static PickupFlask Spawn(Vector3 position, int points, List<Flask> definitions)
        {
            Flask definition = ScriptableObject.CreateInstance<Flask>();
            Set(definition, "points", points);
            definitions.Add(definition);
            PickupFlask prefab = AssetDatabase.LoadAssetAtPath<PickupFlask>("Assets/Gameplay/FlaskPlaceholder.prefab");
            PickupFlask flask = Object.Instantiate(prefab, position, Quaternion.identity);
            flask.Initialize(definition);
            return flask;
        }

        private static void CheckBreakdowns()
        {
            List<FlaskPenaltyEntry> entries = new()
            {
                new(new Vector3(-2, 1, 1), 7), new(new Vector3(-3, 1, 4), 3)
            };
            PlayerRoundResult result = new(1, "Player 1", Color.cyan, new Bounds(), 40, entries);
            Require(result.Penalty == 10 && result.FinalScore == 30, "Sum values, not flask count");
            entries.Clear();
            Require(result.Flasks.Count == 2 && result.Flasks[0].Points == 3, "Copied entries in spatial order");
        }

        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
