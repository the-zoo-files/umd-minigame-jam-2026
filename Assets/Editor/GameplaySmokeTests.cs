using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    [InitializeOnLoad]
    public static class GameplaySmokeTests
    {
        private const string RunningKey = "UmdJam.SmokeTests.Running";
        private const string ResultKey = "UmdJam.SmokeTests.Result";
        private static readonly List<string> Errors = new();
        private static IEnumerator exercise;
        private static string expectedError;
        private static int lastFrame = -1;

        static GameplaySmokeTests()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("Tools/UmdJam/Run Gameplay Smoke Tests")]
        public static void Run()
        {
            SessionState.SetBool("UmdJam.SmokeTests.Cpu", false);
            SessionState.SetBool("UmdJam.SmokeTests.ConnectionMenu", false);
            // Batch executeMethod runs before delayed Editor startup work (such as search indexing).
            // Start the measured Play Mode session after those callbacks have completed.
            EditorApplication.delayCall += () => EditorApplication.delayCall += BeginRun;
        }

        [MenuItem("Tools/UmdJam/Run Connection Menu Smoke Tests")]
        public static void RunConnectionMenu()
        {
            SessionState.SetBool("UmdJam.SmokeTests.Cpu", false);
            SessionState.SetBool("UmdJam.SmokeTests.ConnectionMenu", true);
            EditorApplication.delayCall += () => EditorApplication.delayCall += BeginRun;
        }

        [MenuItem("Tools/UmdJam/Run CPU Smoke Tests")]
        public static void RunCpu()
        {
            SessionState.SetBool("UmdJam.SmokeTests.Cpu", true);
            EditorApplication.delayCall += () => EditorApplication.delayCall += BeginRun;
        }

        private static void BeginRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Errors.Clear();
            expectedError = null;
            lastFrame = -1;
            SessionState.SetString(ResultKey, string.Empty);
            SessionState.SetBool(RunningKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RunningKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                exercise = SessionState.GetBool("UmdJam.SmokeTests.Cpu", false) ? CpuSmokeTests.Exercise() :
                    SessionState.GetBool("UmdJam.SmokeTests.ConnectionMenu", false)
                    ? ConnectionMenuSmokeTests.Exercise() : Exercise();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                string result = SessionState.GetString(ResultKey, "Interrupted");
                SessionState.SetBool(RunningKey, false);
                bool passed = result == "PASS";
                Debug.Log($"Gameplay smoke tests: {result}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(passed ? 0 : 1);
                }
            }
        }

        private static void Update()
        {
            if (exercise == null || !EditorApplication.isPlaying || Time.frameCount == lastFrame)
            {
                return;
            }

            lastFrame = Time.frameCount;
            try
            {
                if (exercise.MoveNext())
                {
                    return;
                }

                Require(Errors.Count == 0, string.Join("\n", Errors));
                Finish("PASS");
            }
            catch (Exception exception)
            {
                Finish(exception.ToString());
            }
        }

        private static void Finish(string result)
        {
            (exercise as IDisposable)?.Dispose();
            exercise = null;
            SessionState.SetString(ResultKey, result);
            EditorApplication.isPlaying = false;
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (!SessionState.GetBool(RunningKey, false) ||
                (type != LogType.Error && type != LogType.Exception && type != LogType.Warning && type != LogType.Assert))
            {
                return;
            }

            if (type == LogType.Error && message == expectedError)
            {
                expectedError = null;
                return;
            }

            Errors.Add($"{type}: {message}\n{stackTrace}");
        }

        private static IEnumerator Exercise()
        {
            Vector3 originalGravity = Physics.gravity;
            List<Gamepad> devices = new();
            try
            {
                yield return null;
                MachineFlaskShooter machine = Object.FindAnyObjectByType<MachineFlaskShooter>();
                machine.enabled = false;
                PlayerInputManager manager = Object.FindAnyObjectByType<PlayerInputManager>();
                CouchMultiplayerManager lobby = manager.GetComponent<CouchMultiplayerManager>();
                Require(lobby.TrySetPlayerCount(4), "Four-player lobby");
                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Multiplayer/Player.prefab");
                PickupFlask flaskPrefab = AssetDatabase.LoadAssetAtPath<PickupFlask>("Assets/Gameplay/FlaskPlaceholder.prefab");
                List<CouchPlayerController> players = new();
                Vector3[] spawns = { new(-6, 1, 6), new(6, 1, 6), new(6, 1, -6), new(-6, 1, -6) };
                Color[] colors = { new(0.22f, 0.78f, 1f), new(1f, 0.38f, 0.46f), new(1f, 0.82f, 0.25f), new(0.46f, 0.9f, 0.42f) };

                for (int i = 0; i < 4; i++)
                {
                    Gamepad device = InputSystem.AddDevice<Gamepad>();
                    devices.Add(device);
                    PlayerInput input = manager.JoinPlayer(i, controlScheme: "Gamepad", pairWithDevice: device);
                    Require(input != null, $"Player {i + 1} joined");
                    CouchPlayerController player = input.GetComponent<CouchPlayerController>();
                    players.Add(player);
                    Require(player.PlayerNumber == i + 1, "Identity is final in joined callback");
                    Require(Vector3.Distance(player.transform.position, spawns[i]) < 0.001f, "Quadrant spawn");
                    Require(player.GetComponent<Renderer>().sharedMaterial.color == colors[i], "Player color");
                    Require(Get<InputAction>(player, "moveAction") == input.actions.FindAction("Player/Move"), "Paired move action");
                    Require(Get<InputAction>(player, "attackAction") == input.actions.FindAction("Player/Attack"), "Paired attack action");
                    Require(input.devices.Count == 1 && input.devices[0] == device, "Device isolation");
                    if (i > 0)
                    {
                        Require(Get<InputAction>(player, "moveAction") != Get<InputAction>(players[0], "moveAction"), "Cloned actions");
                    }
                }

                yield return null;
                Require(CouchPlayerController.ActivePlayers.Count == 4, "One roster entry per player");
                Require(lobby.TryStartGame(), "Start gameplay smoke test round");
                VisualElement root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
                for (int i = 1; i <= 4; i++)
                {
                    Require(root.Q<Label>($"player{i}Name").ClassListContains("is-connected"), "Connected HUD");
                }

                CheckBallistics();
                Physics.gravity = originalGravity;
                // The scene now places collectors near spawns. Exercise the ballistic path,
                // not the newer direct-to-collector transfer that intentionally skips gravity.
                CharacterController mover = players[0].GetComponent<CharacterController>();
                mover.enabled = false;
                players[0].transform.position = new Vector3(-3f, 1f, 3f);
                mover.enabled = true;
                Require(!PlayerFlaskCollector.TryGetNearby(1, players[0].transform.position, out _), "Outside direct collection range");
                Transform socket = Get<Transform>(players[0], "holdPoint");
                PickupFlask flask = Object.Instantiate(flaskPrefab, players[0].transform.position, Quaternion.identity);
                Rigidbody body = flask.GetComponent<Rigidbody>();
                RigidbodyInterpolation interpolation = body.interpolation;
                Require(!flask.TryThrow(Vector3.forward), "Cannot throw a free flask");
                Require(!flask.TryPickUp(null), "Null socket rejected");
                Require(!flask.TryPickUp(flask.transform), "Self socket rejected");
                float contactDeadline = Time.fixedTime + 0.1f;
                while (Time.fixedTime < contactDeadline)
                {
                    yield return null;
                }

                Require(Get<PickupFlask>(players[0], "heldFlask") == flask && flask.IsHeld, "Physics contact pickup");
                Require(!flask.TryPickUp(Get<Transform>(players[1], "holdPoint")), "Second holder rejected");
                Require(!flask.TryThrow(new Vector3(float.NaN, 0, 0)) && flask.IsHeld, "Invalid velocity preserves carry");
                Require(body.isKinematic && !body.useGravity && !body.detectCollisions, "Held physics");
                Require(Vector3.Distance(flask.transform.position, socket.position) < 0.001f, "Socket attachment");

                Physics.gravity = Vector3.zero;
                ExpectError("Cannot throw flask: check trajectory settings and downward-only gravity.",
                    () => Invoke(players[0], "ThrowHeldFlask"));
                Require(flask.IsHeld && Get<PickupFlask>(players[0], "heldFlask") == flask, "Invalid trajectory retains ownership");
                Physics.gravity = originalGravity;
                Invoke(players[0], "ThrowHeldFlask");
                Require(!flask.IsHeld && Get<PickupFlask>(players[0], "heldFlask") == null, "Throw releases ownership");
                Require(!body.isKinematic && body.useGravity && body.detectCollisions, "Free physics restored");
                Require(body.interpolation == interpolation, "Interpolation restored");
                Require(Vector3.Distance(flask.transform.position, Get<Transform>(players[0], "throwPoint").position) < 0.001f, "Release socket");
                Require(body.linearVelocity.y > 0 && Ballistics.IsFinite(body.linearVelocity), "Finite launch velocity");
                foreach (Collider collider in flask.GetComponentsInChildren<Collider>())
                {
                    Require(collider.enabled, "Collider restored");
                }

                Object.Destroy(flask.gameObject);
                int before = Object.FindObjectsByType<PickupFlask>().Length;
                Physics.gravity = Vector3.zero;
                ExpectError("Cannot launch flask: check trajectory settings and downward-only gravity.",
                    () => Invoke(machine, "LaunchFlask"));
                Require(Object.FindObjectsByType<PickupFlask>().Length == before, "Invalid machine launch creates nothing");
                Physics.gravity = originalGravity;
                Invoke(machine, "LaunchFlask");
                Require(Get<List<PickupFlask>>(machine, "spawnedFlasks").Count == 1, "Valid machine launch");

                IEnumerator pooling = CheckPooling(machine, socket);
                while (pooling.MoveNext())
                {
                    yield return pooling.Current;
                }

                CheckHudTimer(root);

                Material bodyMaterial = players[0].GetComponent<Renderer>().sharedMaterial;
                Material markerMaterial = players[0].transform.Find("DirectionGizmo").GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(players[0].gameObject);
                yield return null;
                yield return null;
                Require(bodyMaterial == null && markerMaterial == null, "Owned materials destroyed");
                Require(CouchPlayerController.ActivePlayers.Count == 3, "Player removed from roster");
                Require(!root.Q<Label>("player1Name").ClassListContains("is-connected"), "Disconnected HUD");
                PlayerInput replacement = PlayerInput.Instantiate(playerPrefab, 0, controlScheme: "Gamepad", pairWithDevice: devices[0]);
                Require(replacement.GetComponent<CouchPlayerController>().PlayerNumber == 1, "Rejoin identity");
                Require(CouchPlayerController.ActivePlayers.Count == 4, "Rejoin roster");

                foreach (CouchPlayerController player in Object.FindObjectsByType<CouchPlayerController>())
                {
                    Object.Destroy(player.gameObject);
                }

                yield return null;
                yield return null;
                manager.DisableJoining();
                Object.Destroy(manager.gameObject);
                yield return null;
                yield return null;

                GameObject invalidPlayer = Object.Instantiate(playerPrefab);
                CouchPlayerController invalidController = invalidPlayer.GetComponent<CouchPlayerController>();
                Set(invalidController, "holdPoint", null);
                ExpectError("Player prefab is missing its flask hold point. Pickup is disabled.", invalidController.InitializePlayer);
                Require(invalidController.enabled && !Get<bool>(invalidController, "pickupConfigured"), "Missing socket disables only pickup");
                invalidController.InitializePlayer();
                Invoke(invalidController, "OnTriggerStay", machine.GetComponent<Collider>());
                Object.Destroy(invalidPlayer);
                yield return null;
                yield return null;

                GameObject missingActions = Object.Instantiate(playerPrefab);
                missingActions.GetComponent<PlayerInput>().actions = null;
                CouchPlayerController missingController = missingActions.GetComponent<CouchPlayerController>();
                ExpectError("Player requires Move and Attack actions and a player index from 0 to 3.", missingController.InitializePlayer);
                Require(!missingController.enabled, "Missing actions disable controller");
                missingController.InitializePlayer();
                Object.Destroy(missingActions);
                yield return null;
                yield return null;
                Require(CouchPlayerController.ActivePlayers.Count == 0, "Roster cleaned up");

                PickupFlask pooled = Get<List<PickupFlask>>(machine, "spawnedFlasks")[0];
                Require(pooled.TryPickUp(machine.transform) && pooled.TryThrow(Vector3.up, 1) &&
                    pooled.TryCollect(1, out _), "Return flask before owner teardown");
                GameObject machineObject = machine.gameObject;
                Object.Destroy(machine);
                yield return null;
                yield return null;
                Require(pooled == null && machineObject != null, "Removing spawner component destroys its inactive pool");
            }
            finally
            {
                Physics.gravity = originalGravity;
                foreach (Gamepad device in devices)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
        }

        private static IEnumerator CheckPooling(MachineFlaskShooter machine, Transform socket)
        {
            List<PickupFlask> active = Get<List<PickupFlask>>(machine, "spawnedFlasks");
            PickupFlask flask = active[0];
            Rigidbody body = flask.GetComponent<Rigidbody>();
            Vector3 originalScale = flask.transform.localScale;
            Require(flask.TryPickUp(socket) && flask.TryThrow(Vector3.up, 1), "Pool test throw");
            Require(!flask.TryCollect(2, out _), "Wrong collector cannot return flask");
            Require(flask.TryCollect(1, out int points) && points == flask.PointValue, "Pooled collection points");
            Require(!flask.gameObject.activeSelf && active.Count == 0, "Collected flask leaves active cap immediately");
            Require(!flask.TryCollect(1, out _) && !flask.TryPickUp(socket), "Inactive flask cannot score or be picked up");
            yield return null;
            Require(flask != null, "Collected machine flask retained for reuse");
            Invoke(machine, "LaunchFlask");
            Require(active.Count == 1 && active[0] == flask, "Machine reuses collected instance");
            Require(!flask.IsHeld && !flask.IsCollected && flask.LastThrowerPlayerNumber == 0, "Ownership reset");
            Require(!flask.TryCollect(1, out _), "Old thrower cannot score reused flask");
            Require(flask.transform.parent == null && flask.transform.localScale == originalScale, "Spawn transform reset");
            Require(!body.isKinematic && body.detectCollisions && body.useGravity, "Reused dynamic physics");
            Require(body.interpolation == RigidbodyInterpolation.Interpolate && body.linearVelocity.y > 0, "Reused interpolation and launch");
            foreach (Collider collider in flask.GetComponentsInChildren<Collider>())
            {
                Require(collider.enabled, "Reused collider enabled");
            }

            bool arrived = false;
            Require(flask.TryPickUp(socket), "Reused flask pickup");
            Require(flask.TryThrowDirectly(socket.position + Vector3.up, 1, 0.02f,
                () => { arrived = flask.TryCollect(1, out _); }), "Direct transfer before reuse");
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!arrived && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Require(arrived && !flask.gameObject.activeSelf, "Direct transfer returns to pool");
            Invoke(machine, "LaunchFlask");
            Require(active[0] == flask && !body.isKinematic && body.detectCollisions, "Direct transfer state reset");

            bool staleCallback = false;
            Require(flask.TryPickUp(socket) && flask.TryThrowDirectly(socket.position, 1, 0.02f,
                () => staleCallback = true), "Start cancellable transfer");
            Require(flask.TryCollect(1, out _), "Collect during transfer");
            Invoke(machine, "LaunchFlask");
            float until = Time.time + 0.05f;
            while (Time.time < until)
            {
                yield return null;
            }
            Require(!staleCallback && !flask.IsCollected, "Old transfer cannot affect reused flask");

            int cap = Get<int>(machine, "maximumActiveFlasks");
            Set(machine, "maximumActiveFlasks", 1);
            Set(machine, "launchTimer", 0f);
            Invoke(machine, "Update");
            Require(active.Count == 1, "Active cap still enforced after reuse");
            Set(machine, "maximumActiveFlasks", cap);

            Require(flask.TryPickUp(socket) && flask.TryThrow(Vector3.up, 1) && flask.TryCollect(1, out _), "Return before external destruction");
            Object.Destroy(flask.gameObject);
            yield return null;
            Invoke(machine, "LaunchFlask");
            Require(active.Count == 1 && active[0] != null && active[0] != flask, "Destroyed pool entry replaced");
        }

        private static void CheckHudTimer(VisualElement root)
        {
            UmdJam.UI.CouchPlayerHud hud = Object.FindAnyObjectByType<UmdJam.UI.CouchPlayerHud>();
            Label label = root.Q<Label>("roundTimer");
            Invoke(hud, "OnRoundTimeChanged", 59.9f);
            string first = label.text;
            Invoke(hud, "OnRoundTimeChanged", 59.1f);
            Require(ReferenceEquals(first, label.text), "Same displayed second retains timer string");
            Invoke(hud, "OnRoundTimeChanged", 59f);
            Require(label.text == "00:59", "Timer second boundary");
            Invoke(hud, "OnRoundTimeChanged", 0f);
            Require(label.text == "00:00", "Timer reaches zero");
            hud.enabled = false;
            hud.enabled = true;
            Require(Get<int>(hud, "displayedSeconds") == Mathf.CeilToInt(GameManager.Instance.RemainingTime), "Reenabled HUD refreshes timer");
        }

        private static void CheckBallistics()
        {
            Physics.gravity = new Vector3(0, -9.81f, 0);
            Vector3 origin = new(0, 1, 0);
            Vector3 target = new(8, 0.35f, 0);
            Require(Ballistics.TryCalculateVelocity(origin, target, 3, out Vector3 velocity), "Valid trajectory");
            float flightTime = 8 / velocity.x;
            Vector3 landing = origin + velocity * flightTime + 0.5f * Physics.gravity * flightTime * flightTime;
            Require(Vector3.Distance(landing, target) < 0.001f, "Ballistic landing point");
            Require(Mathf.Abs(velocity.y * velocity.y / 19.62f - 3) < 0.001f, "Ballistic apex");
            Require(!Ballistics.TryCalculateVelocity(origin, target, -1, out _), "Negative apex rejected");
            Require(!Ballistics.TryCalculateVelocity(origin, target, float.NaN, out _), "NaN apex rejected");
            Require(!Ballistics.TryCalculateVelocity(origin, target, float.PositiveInfinity, out _), "Infinite apex rejected");
            Require(!Ballistics.TryCalculateVelocity(origin, new Vector3(0, 10, 0), 3, out _), "Unreachable target rejected");
            Require(!Ballistics.TryCalculateVelocity(new Vector3(float.NaN, 0, 0), target, 3, out _), "Invalid origin rejected");
            Physics.gravity = Vector3.zero;
            Require(!Ballistics.TryCalculateVelocity(origin, target, 3, out _), "Zero gravity rejected");
            Physics.gravity = Vector3.up;
            Require(!Ballistics.TryCalculateVelocity(origin, target, 3, out _), "Upward gravity rejected");
            Physics.gravity = new Vector3(1, -9.81f, 0);
            Require(!Ballistics.TryCalculateVelocity(origin, target, 3, out _), "Lateral gravity rejected");
        }

        private static void ExpectError(string message, Action action)
        {
            expectedError = message;
            action();
            Require(expectedError == null, $"Expected diagnostic: {message}");
        }

        private static T Get<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void Invoke(object target, string name, params object[] arguments)
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
