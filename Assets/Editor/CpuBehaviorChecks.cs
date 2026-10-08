using System;
using System.Collections;
using System.Reflection;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class CpuBehaviorChecks
    {
        public static IEnumerator Exercise()
        {
            yield return null;
            Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
            CouchMultiplayerManager lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
            Require(lobby.TrySetPlayerCount(1) && lobby.TryAddCpu(0), "Default CPU joins");
            CouchPlayerController player = lobby.GetParticipant(0);
            Require(player.Cpu.Difficulty == CpuDifficulty.Noob, "New CPUs default to Noob");
            Require(lobby.TryStartGame(), "Start CPU behavior fixture");
            yield return null;
            player.enabled = false; // Exercise commands without physically collecting the fixture.
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = new Vector3(-6f, 1f, 0f);
            CpuPlayerController cpu = player.Cpu;
            SerializedObject profile = new(cpu);
            profile.FindProperty("noob").FindPropertyRelative("decisionNoise").floatValue = 0f;
            profile.ApplyModifiedPropertiesWithoutUndo();
            PickupFlask prefab = AssetDatabase.LoadAssetAtPath<PickupFlask>("Assets/Gameplay/FlaskPlaceholder.prefab");
            PickupFlask first = Spawn(prefab, new Vector3(-6f, 0.5f, 4f));
            PickupFlask better = Spawn(prefab, new Vector3(-6f, 0.5f, 6.5f));
            cpu.ReadCommand(out Vector2 initialCommand, out _);
            Require(Target(cpu) == null, "A newly visible flask is not known instantly");
            Require(initialCommand.magnitude > 0f && initialCommand.magnitude < 1f,
                "Idle exploration accelerates instead of jumping to full speed");
            float deadline = Time.time + 3f;
            Vector2 command = Vector2.zero;
            while (Target(cpu) == null && Time.time < deadline)
            {
                yield return null;
                cpu.ReadCommand(out command, out _);
            }
            Require(Target(cpu) == first, "Visible flask is noticed after a bounded delay");
            Require(command.magnitude > 0f, "A newly noticed target does not introduce a reaction stop");

            // Both candidates were observed together. Changing relative value now isolates commitment
            // from the separate discovery delay; without commitment the next planning tick switches.
            better.transform.position = new Vector3(-6f, 0.5f, 1.5f);
            Physics.SyncTransforms();
            float acquired = Time.time;
            while (Time.time - acquired < 1f)
            {
                yield return null;
                cpu.ReadCommand(out command, out _);
                Require(Target(cpu) == first, "Commitment prevents abandoning a valid target immediately");
                Require(command.sqrMagnitude > 0f, "Noticing another flask does not stop valid movement");
            }
            deadline = Time.time + 2f;
            while (Target(cpu) != better && Time.time < deadline)
            {
                yield return null;
                cpu.ReadCommand(out _, out _);
            }
            Require(Target(cpu) == better, "A clearly better known target can win after commitment expires");
            better.gameObject.SetActive(false);
            cpu.ReadCommand(out _, out _);
            Require(Target(cpu) == first, "Unavailable target releases commitment immediately");
            // Reuse the same object entirely between CPU reads, as happens with the flask pool.
            first.gameObject.SetActive(false);
            first.gameObject.SetActive(true);
            cpu.ReadCommand(out _, out _);
            Require(Target(cpu) == null, "Reused flasks must be noticed again even between planning ticks");
            deadline = Time.time + cpu.Settings.ReactionDelay * 0.75f;
            while (Time.time < deadline)
            {
                yield return null;
                cpu.ReadCommand(out _, out _);
                Require(Target(cpu) == null, "Pooled relaunch observes the full discovery delay");
            }
            deadline = Time.time + 3f;
            while (Target(cpu) != first && Time.time < deadline)
            {
                yield return null;
                cpu.ReadCommand(out _, out _);
            }
            Require(Target(cpu) == first, "Reused flask can be acquired after rediscovery");
            GameObject hold = new("CPU observation test hold");
            hold.transform.position = first.transform.position;
            Require(first.TryPickUp(hold.transform) && first.TryThrow(Vector3.zero), "Fixture pickup and rethrow");
            cpu.ReadCommand(out _, out _);
            Require(Target(cpu) == null, "Pickup and rethrow between reads also invalidate awareness");
            Object.Destroy(hold);
            first.gameObject.SetActive(false);
            PickupFlask remote = Spawn(prefab, new Vector3(6f, 0.5f, -4f));
            deadline = Time.time + 2f;
            while (Time.time < deadline)
            {
                yield return null;
                cpu.ReadCommand(out _, out _);
                Require(Target(cpu) != remote, "Noob cannot target a flask across the whole arena");
            }
            GameManager.Instance.EndRound();
            cpu.ReadCommand(out command, out bool attack);
            Require(command == Vector2.zero && !attack, "Round end stops smoothed movement immediately");
            IEnumerator godChecks = CheckGod(prefab);
            while (godChecks.MoveNext()) yield return godChecks.Current;
        }

        private static IEnumerator CheckGod(PickupFlask prefab)
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
            CouchMultiplayerManager lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
            Require(lobby.TrySetPlayerCount(1) && lobby.TryAddCpu(0, CpuDifficulty.God), "God fixture joins");
            CouchPlayerController player = lobby.GetParticipant(0);
            CpuPlayerController cpu = player.Cpu;
            // Existing serialized prefab values must not silently reintroduce handicaps.
            SerializedObject serialized = new(cpu);
            SerializedProperty god = serialized.FindProperty("god");
            god.FindPropertyRelative("reactionDelay").floatValue = 5f;
            god.FindPropertyRelative("decisionNoise").floatValue = 1f;
            god.FindPropertyRelative("awarenessRadius").floatValue = 1f;
            god.FindPropertyRelative("targetCommitment").floatValue = 5f;
            god.FindPropertyRelative("switchAdvantage").floatValue = 1f;
            god.FindPropertyRelative("throwPreparation").floatValue = 5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(lobby.TryStartGame(), "God fixture starts");
            player.enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = new Vector3(-6f, 1f, 6f);
            PickupFlask distant = Spawn(prefab, new Vector3(6f, 0.5f, -6f));
            cpu.ReadCommand(out Vector2 movement, out _);
            Require(Target(cpu) == distant, "God notices an arena-wide flask on its first planning tick");
            Require(movement.magnitude > 0.99f, "God accelerates immediately without input smoothing");
            PickupFlask nearer = Spawn(prefab, new Vector3(-6f, 0.5f, 4f));
            float acquired = Time.time;
            float deadline = acquired + 0.5f;
            while (Target(cpu) != nearer && Time.time < deadline)
            {
                yield return null;
                cpu.ReadCommand(out _, out _);
            }
            Require(Target(cpu) == nearer, "God switches to the better flask without commitment or discovery waits");
            distant.gameObject.SetActive(false);
            Action<CpuSettings> observe = (Action<CpuSettings>)Delegate.CreateDelegate(typeof(Action<CpuSettings>), cpu,
                typeof(CpuPlayerController).GetMethod("Observe", BindingFlags.Instance | BindingFlags.NonPublic));
            CpuSettings settings = cpu.Settings;
            observe(settings);
            long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) observe(settings);
            Require(GC.GetAllocatedBytesForCurrentThread() == beforeAllocation,
                "Warmed bot observations allocate no managed memory with active flasks");
            // Approach within the natural braking distance, outside the waypoint arrival epsilon.
            player.transform.position = nearer.transform.position + Vector3.forward * 0.4f;
            player.transform.position = new Vector3(player.transform.position.x, 1f, player.transform.position.z);
            deadline = Time.time + cpu.Settings.PlanningInterval + 0.02f;
            while (Time.time < deadline) yield return null;
            cpu.ReadCommand(out movement, out _);
            Require(movement.magnitude > 0.99f, "God does not apply final-approach braking");
            nearer.gameObject.SetActive(false);
            PlayerFlaskCollector collector = PlayerFlaskCollector.GetForPlayer(1);
            player.transform.position = collector.CollectionPoint;
            PickupFlask carried = Spawn(prefab, collector.CollectionPoint + Vector3.up);
            Transform holdPoint = (Transform)typeof(CouchPlayerController).GetField("holdPoint",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            Require(carried.TryPickUp(holdPoint), "God carry fixture");
            typeof(CouchPlayerController).GetField("heldFlask", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player, carried);
            cpu.ReadCommand(out movement, out bool attack);
            Require(attack && movement == Vector2.zero, "God throws immediately without preparation");
            GameManager.Instance.EndRound();
            cpu.ReadCommand(out movement, out attack);
            Require(movement == Vector2.zero && !attack, "Unrestricted God still stops at round end");
        }

        internal static PickupFlask Target(CpuPlayerController cpu) =>
            (PickupFlask)typeof(CpuPlayerController).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cpu);

        private static PickupFlask Spawn(PickupFlask prefab, Vector3 position)
        {
            PickupFlask flask = Object.Instantiate(prefab, position, Quaternion.identity);
            flask.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            return flask;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
