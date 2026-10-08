using System;
using System.Collections;
using System.Reflection;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEditor;
using UnityEngine;
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
