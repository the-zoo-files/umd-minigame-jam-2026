using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UmdJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class GameplayFeedbackSmokeTests
    {
        public static IEnumerator Exercise()
        {
            List<Flask> definitions = new();
            Gamepad device = null;
            bool background = Application.runInBackground;
            Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow window = EditorWindow.GetWindow(gameViewType);
            PropertyInfo selection = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int oldSelection = (int)selection.GetValue(window);
            try
            {
                Application.runInBackground = true;
                RoundResultsVisualChecks.SetSize(window, gameViewType, 1920, 1080);
                yield return null;
                var lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                GameManager round = GameManager.Instance;
                Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
                GameplayFeedback feedback = Object.FindAnyObjectByType<GameplayFeedback>();
                Require(feedback != null, "HUD installs feedback without scene YAML changes");
                VisualElement root = feedback.GetComponent<UIDocument>().rootVisualElement;
                Label timer = root.Q<Label>("roundTimer");
                Require(!timer.ClassListContains("is-urgent") && root.Q("countdownWarning").ClassListContains("is-hidden"),
                    "Lobby has no countdown warning");
                device = InputSystem.AddDevice<Gamepad>();
                Require(lobby.TryJoin(device) && lobby.TryAddCpu(1) && lobby.TryStartGame(), "Mixed feedback fixture starts");
                lobby.GetParticipant(0).enabled = false;
                lobby.GetParticipant(1).Cpu.enabled = false;
                lobby.GetParticipant(1).enabled = false;
                Set(feedback, "scoringDuration", 2f);
                PickupFlask prefab = AssetDatabase.LoadAssetAtPath<PickupFlask>("Assets/Gameplay/FlaskPlaceholder.prefab");
                PlayerFlaskCollector collector = PlayerFlaskCollector.GetForPlayer(1);
                Transform socket = new GameObject("Feedback fixture socket").transform;
                socket.position = collector.CollectionPoint + Vector3.up;
                Label popup = root.Q<Label>("scorePopup1");
                PickupFlask collected = Spawn(prefab, socket.position, 3, definitions);
                Require(collected.TryPickUp(socket) && collected.TryThrow(Vector3.zero, 2), "Wrong-collector fixture throws");
                collector.Collect(collected);
                Require(lobby.GetParticipant(0).Score == 0 && popup.ClassListContains("is-hidden"), "Rejected collection has no reward feedback");
                Require(collected.TryPickUp(socket) && collected.TryThrow(Vector3.zero, 1), "Matching collector fixture throws");
                collector.Collect(collected);
                Require(lobby.GetParticipant(0).Score == 3 && popup.text == "+3" && !popup.ClassListContains("is-hidden"),
                    "Actual awarded points trigger the popup");
                collector.Collect(collected);
                Require(popup.text == "+3" && lobby.GetParticipant(0).Score == 3, "Duplicate collection cannot replay or rescore");
                PickupFlask second = Spawn(prefab, socket.position, 7, definitions);
                Require(second.TryPickUp(socket) && collector.TryTransfer(second, 0f), "Direct collection succeeds");
                Require(popup.text == "+10" && lobby.GetParticipant(0).Score == 10 &&
                    !root.Q("collectorPulse1").ClassListContains("is-hidden"), "Rapid rewards accumulate and pulse the collector");

                PickupFlask moving = Spawn(prefab, new Vector3(-4, 1, 4), 3, definitions);
                Spawn(prefab, new Vector3(-2, 1, 4), 7, definitions);
                Spawn(prefab, new Vector3(0, 1, 3), 2, definitions);
                Spawn(prefab, new Vector3(3, 1, 3), 5, definitions);
                Spawn(prefab, new Vector3(-3, 6, 3), 100, definitions);
                Spawn(prefab, new Vector3(30, 1, 3), 100, definitions);
                Spawn(prefab, new Vector3(-3, 1, -3), 100, definitions);
                PickupFlask inactive = Spawn(prefab, new Vector3(-3, 1, 3), 100, definitions);
                inactive.gameObject.SetActive(false);
                socket.position = new Vector3(-2, 1, 2);
                PickupFlask held = Spawn(prefab, socket.position, 4, definitions);
                Require(held.TryPickUp(socket), "Held penalty fixture");
                PickupFlask transfer = Spawn(prefab, socket.position, 6, definitions);
                Require(transfer.TryPickUp(socket) && collector.TryTransfer(transfer, 30f), "Transferring penalty fixture");
                Set(round, "remainingTime", 11.2f);
                yield return null;
                Require(!timer.ClassListContains("is-urgent"), "Normal timer remains unchanged before final ten seconds");
                Set(round, "remainingTime", 9.8f);
                yield return null;
                yield return null;
                Require(timer.ClassListContains("is-urgent") && !timer.ClassListContains("is-critical"), "Final ten seconds activate countdown");
                RoundResults snapshot = RoundResults.Capture(round, CouchPlayerController.ActivePlayers, PickupFlask.ActiveFlasks);
                Require(snapshot.Players[0].Penalty == 22 && root.Q<Label>("zoneRisk1").text == "At risk: −22" &&
                    root.Q<Label>("zoneRisk2").text == "At risk: −5", "Live risks match authoritative held/transfer/boundary eligibility");
                Require(root.Q("zoneRisk3").ClassListContains("is-hidden"), "Absent players have no risk labels");
                IEnumerator capture = Capture("gameplay-feedback-1920");
                while (capture.MoveNext()) yield return capture.Current;
                RoundResultsVisualChecks.SetSize(window, gameViewType, 1440, 1080);
                yield return null;
                yield return null;
                capture = Capture("gameplay-feedback-1440");
                while (capture.MoveNext()) yield return capture.Current;
                Rect panel = root.worldBound;
                Require(panel.Contains(root.Q("zoneRisk1").worldBound.center) && panel.Contains(root.Q("zoneRisk2").worldBound.center),
                    "Risk labels fit the 4:3 Game view");
                Rigidbody movingBody = moving.GetComponent<Rigidbody>();
                movingBody.position = new Vector3(3, 1, 2);
                moving.transform.position = movingBody.position;
                Physics.SyncTransforms();
                yield return null;
                yield return null;
                Require(root.Q<Label>("zoneRisk1").text == "At risk: −19" && root.Q<Label>("zoneRisk2").text == "At risk: −8",
                    "Crossing a zone updates both live risks");
                Action update = (Action)Delegate.CreateDelegate(typeof(Action), feedback,
                    typeof(GameplayFeedback).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic));
                update();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 200; i++) update();
                Require(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed unchanged feedback frames allocate no managed memory");
                Set(feedback, "scoringDuration", 0.2f);
                float expires = Time.time + 0.25f;
                while (Time.time < expires) yield return null;
                Require(popup.ClassListContains("is-hidden") && root.Q("collectorPulse1").ClassListContains("is-hidden"),
                    "Completed collection animation hides its popup and pulse");
                socket.position = collector.CollectionPoint + Vector3.up;
                PickupFlask zero = Spawn(prefab, socket.position, 0, definitions);
                Require(zero.TryPickUp(socket) && collector.TryTransfer(zero, 0f) && popup.ClassListContains("is-hidden") &&
                    lobby.GetParticipant(0).Score == 10, "Zero-point delivery produces no reward feedback");
                PickupFlask fresh = Spawn(prefab, socket.position, 3, definitions);
                Require(fresh.TryPickUp(socket) && collector.TryTransfer(fresh, 0f) && popup.text == "+3" &&
                    lobby.GetParticipant(0).Score == 13, "A reward after expiry starts a new popup total");
                Set(round, "remainingTime", 20f);
                yield return null;
                yield return null;
                Require(!timer.ClassListContains("is-urgent") && root.Q("zoneRisk1").ClassListContains("is-hidden"),
                    "Extending time clears countdown presentation");
                Set(round, "remainingTime", 2.8f);
                yield return null;
                yield return null;
                Require(timer.ClassListContains("is-critical"), "Final three seconds turn the timer red");
                feedback.enabled = false;
                Require(root.Q("scorePopup1") == null && !timer.ClassListContains("is-urgent"), "Disable removes transient UI and restores timer");
                feedback.enabled = true;
                yield return null;
                yield return null;
                Require(timer.ClassListContains("is-critical") && root.Q<Label>("zoneRisk1").text == "At risk: −19", "Re-enable reconstructs current countdown state");
                round.EndRound();
                Require(!timer.ClassListContains("is-urgent") && root.Q("countdownWarning").ClassListContains("is-hidden") &&
                    root.Q("zoneRisk1").ClassListContains("is-hidden") && root.Q("scorePopup1").ClassListContains("is-hidden"),
                    "Results clear gameplay feedback and penalty deductions create no reward popup");
                EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                yield return null;
                feedback = Object.FindAnyObjectByType<GameplayFeedback>();
                Require(feedback != null && !feedback.GetComponent<UIDocument>().rootVisualElement.Q<Label>("roundTimer").ClassListContains("is-urgent"),
                    "Replay starts with fresh feedback");
            }
            finally
            {
                if (device != null && device.added) InputSystem.RemoveDevice(device);
                foreach (Flask definition in definitions) if (definition != null) Object.Destroy(definition);
                selection.SetValue(window, oldSelection);
                Application.runInBackground = background;
            }
        }

        private static PickupFlask Spawn(PickupFlask prefab, Vector3 position, int points, List<Flask> definitions)
        {
            Flask definition = ScriptableObject.CreateInstance<Flask>();
            definitions.Add(definition);
            SerializedObject serialized = new(definition);
            serialized.FindProperty("points").intValue = points;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PickupFlask flask = Object.Instantiate(prefab, position, Quaternion.identity);
            flask.Initialize(definition);
            flask.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            return flask;
        }

        private static IEnumerator Capture(string name)
        {
            for (int i = 0; i < 5; i++) yield return null;
            ScreenCapture.CaptureScreenshot($".utmp/{name}.png");
            for (int i = 0; i < 5; i++) yield return null;
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
