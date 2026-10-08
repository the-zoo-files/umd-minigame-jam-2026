using System;
using System.Collections;
using System.Reflection;
using System.IO;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class RandomEventSmokeTests
    {
        public static IEnumerator Exercise()
        {
            Gamepad device = InputSystem.AddDevice<Gamepad>();
            try
            {
                yield return null;
                CouchMultiplayerManager lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                GameManager round = GameManager.Instance;
                Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
                VisualElement root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
                Require(!lobby.RandomEventsEnabled && RandomEventDirector.Instance == null, "Events default off without runtime effects");
                Button toggle = root.Q<Button>("randomEvents");
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = toggle;
                    toggle.SendEvent(submit);
                }
                Require(lobby.RandomEventsEnabled && toggle.text == "Random Events: On", "Menu event enables authoritative setting");
                Require(lobby.TryJoin(device) && lobby.TryAddCpu(1, CpuDifficulty.God) && lobby.TryStartGame(), "Mixed event round starts");
                Require(!lobby.TrySetRandomEvents(false), "Event option locks during round");
                RandomEventDirector director = RandomEventDirector.Instance;
                Require(director != null && director.ActiveEvent == RandomEventKind.None, "Round starts with a quiet interval");
                Set(director, "quietDuration", 120f);
                CouchPlayerController human = lobby.GetParticipant(0);
                CouchPlayerController god = lobby.GetParticipant(1);
                god.Cpu.enabled = false; // Isolate environmental movement from normal AI steering.
                PickupFlask prefab = AssetDatabase.LoadAssetAtPath<PickupFlask>("Assets/Gameplay/FlaskPlaceholder.prefab");
                PickupFlask flask = Object.Instantiate(prefab, new Vector3(2f, 1f, -3f), Quaternion.identity);
                Rigidbody body = flask.GetComponent<Rigidbody>();
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                Vector3 before = human.transform.position;
                Require(director.TryBeginEvent(RandomEventKind.Earthquake) &&
                    !director.TryBeginEvent(RandomEventKind.Lightning), "One event at a time");
                Require(root.Q<Label>("randomEventStatus").text == "Earthquake", "HUD names active event");
                float until = Time.time + 0.2f;
                while (Time.time < until) yield return null;
                Require(CpuNavigation.PlanarDistance(before, human.transform.position) > 0.01f, "Earthquake drifts a human with no input");
                Require(body.linearVelocity.sqrMagnitude > 0.01f, "Earthquake shakes free flasks");
                IEnumerator capture = Capture("earthquake");
                while (capture.MoveNext()) yield return capture.Current;
                director.StopEffects();
                Require(director.GetEarthquakeDrift(1) == Vector3.zero, "Earthquake drift clears");

                Require(director.TryBeginEvent(RandomEventKind.Tornadoes), "Tornado event starts");
                Vector3 center = director.TornadoPosition(0);
                // Isolate free-flask wind from contact pickup, then exercise player lift separately.
                human.enabled = false;
                god.enabled = false;
                body.position = center + Vector3.up + Vector3.right * 1.8f;
                flask.transform.position = body.position;
                Physics.SyncTransforms();
                until = Time.time + 0.2f;
                while (Time.time < until) yield return null;
                Require(body.linearVelocity.y > 0f && flask.IsAvailable, "Tornado lifts free flasks");
                body.position = new Vector3(0f, 10f, -6f);
                human.enabled = true;
                god.enabled = true;
                center = director.TornadoPosition(0);
                Move(human, center + Vector3.up + Vector3.right * 0.5f);
                Move(god, center + Vector3.up + Vector3.left * 0.5f);
                Physics.SyncTransforms();
                float humanHeight = human.transform.position.y;
                float godHeight = god.transform.position.y;
                until = Time.time + 0.4f;
                while (Time.time < until) yield return null;
                Require(human.transform.position.y > humanHeight + 0.1f && god.transform.position.y > godHeight + 0.1f,
                    "Tornado lifts both humans and God CPUs");
                capture = Capture("tornadoes");
                while (capture.MoveNext()) yield return capture.Current;
                center = director.TornadoPosition(0);
                Require(director.TryGetTornadoVelocity(center + Vector3.right, out Vector3 wind) &&
                    Mathf.Abs(wind.z) > 1f, "Tornado wind is circular");
                // Let the actual scheduler end the event: airborne momentum must survive the transition.
                Set(director, "nextTransition", Time.time);
                yield return null;
                yield return null;
                Require(director.ActiveEvent == RandomEventKind.None && human.transform.position.y > humanHeight,
                    "Tornado release keeps airborne players moving");
                director.StopEffects();

                Require(director.TryBeginEvent(RandomEventKind.Lightning), "Lightning starts");
                Vector3 strike = director.LightningPosition;
                Move(human, new Vector3(strike.x, 1f, strike.z));
                Move(god, new Vector3(strike.x + 0.5f, 1f, strike.z));
                Require(!human.IsStunned && director.IsWarning, "Lightning telegraphs before damage");
                Set(director, "nextStrike", Time.time);
                yield return null;
                yield return null;
                Require(human.IsStunned && god.IsStunned && director.StrikeFlash > 0f,
                    "Lightning flashes and stuns humans and CPUs equally");
                capture = Capture("lightning");
                while (capture.MoveNext()) yield return capture.Current;
                Require(!human.TryStun(float.NaN) && !human.TryStun(-1f), "Invalid stun requests rejected");
                director.enabled = false;
                Require(!human.IsStunned && !god.IsStunned, "Disabling event owner clears all status effects");
                director.enabled = true;
                Require(director.TryBeginEvent(RandomEventKind.Earthquake), "Effects restart after enable");
                flask.gameObject.SetActive(false);
                RandomEventVisuals visuals = Object.FindAnyObjectByType<RandomEventVisuals>();
                Action updateVisuals = (Action)Delegate.CreateDelegate(typeof(Action), visuals,
                    typeof(RandomEventVisuals).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic));
                Action updateForces = (Action)Delegate.CreateDelegate(typeof(Action), director,
                    typeof(RandomEventDirector).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic));
                updateVisuals();
                updateForces();
                long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 200; i++)
                {
                    updateVisuals();
                    updateForces();
                }
                Require(GC.GetAllocatedBytesForCurrentThread() == beforeAllocation,
                    "Warmed event visuals and roster iteration allocate no managed memory");
                director.StopEffects();
                Require(director.TryBeginEvent(RandomEventKind.Earthquake), "Earthquake restarts for camera recovery check");
                Camera camera = Camera.main;
                Vector3 cameraBefore = camera != null ? camera.transform.position : Vector3.zero;
                yield return null;
                director.StopEffects();
                if (camera != null) Require(Vector3.Distance(camera.transform.position, cameraBefore) < 0.001f,
                    "Camera shake restores its exact baseline");
                Set(director, "quietDuration", 0.2f);
                Set(director, "eventDuration", 0.2f);
                director.StopEffects();
                RandomEventKind visible = RandomEventKind.None;
                RandomEventKind last = RandomEventKind.None;
                int starts = 0;
                int seen = 0;
                until = Time.time + 6f;
                while (starts < 6 && Time.time < until)
                {
                    RandomEventKind current = director.ActiveEvent;
                    if (current != RandomEventKind.None && visible == RandomEventKind.None)
                    {
                        Require(current != last, "Automatic schedule avoids consecutive repeated events");
                        seen |= 1 << (int)current;
                        last = current;
                        starts++;
                    }
                    visible = current;
                    yield return null;
                }
                Require(starts == 6 && seen == 0b1110, "Automatic schedule visits every event and repeats its bounded bag");
                round.EndRound();
                Require(director.ActiveEvent == RandomEventKind.None && !human.IsStunned &&
                    root.Q<Label>("randomEventStatus").ClassListContains("is-hidden"), "Round end clears event HUD/status");
                Require(!director.TryBeginEvent(RandomEventKind.Tornadoes), "No events after round end");
                EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                yield return null;
                lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                Require(RandomEventDirector.Instance == null && !lobby.RandomEventsEnabled, "Replay resets event setting and singleton");
            }
            finally
            {
                if (device.added) InputSystem.RemoveDevice(device);
            }
        }

        private static void Move(CouchPlayerController player, Vector3 position)
        {
            CharacterController character = player.GetComponent<CharacterController>();
            character.enabled = false;
            player.transform.position = position;
            character.enabled = true;
        }

        private static IEnumerator Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            Camera camera = Camera.main;
            if (camera == null) yield break;
            RenderTexture previous = camera.targetTexture;
            RenderTexture target = new(1280, 720, 24);
            Texture2D image = new(1280, 720, TextureFormat.RGB24, false);
            try
            {
                target.Create();
                camera.targetTexture = target;
                yield return null;
                yield return null;
                RenderTexture active = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    image.Apply();
                    Directory.CreateDirectory(".utmp");
                    File.WriteAllBytes($".utmp/random-event-{name}.png", image.EncodeToPNG());
                }
                finally { RenderTexture.active = active; }
            }
            finally
            {
                camera.targetTexture = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
            }
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
