using System;
using System.Collections;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class CpuSmokeTests
    {
        public static IEnumerator Exercise()
        {
            Gamepad device = null;
            try
            {
                yield return null;
                CouchMultiplayerManager lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                GameManager round = GameManager.Instance;
                Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
                Require(lobby.TrySetPlayerCount(4), "Four slots selected");
                VisualElement root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
                for (int i = 0; i < 4; i++)
                {
                    Require(lobby.TryAddCpu(i, (CpuDifficulty)i), "Add each CPU difficulty");
                    Require(lobby.GetParticipant(i).Cpu.Difficulty == (CpuDifficulty)i, "Difficulty retained");
                    Require(root.Q<Button>($"cpuDifficulty{i + 1}").text == ((CpuDifficulty)i).ToString(), "Difficulty label");
                    Require(!lobby.TryAddCpu(i), "Occupied slot rejected");
                }
                Require(PlayerInput.all.Count == 0, "CPU creation does not register or pair PlayerInput");
                Require(CouchPlayerController.ActivePlayers.Count == 4 && lobby.CanStart, "CPU roster ready");
                lobby.GetParticipant(0).Cpu.enabled = false;
                Require(!lobby.CanStart, "Disabled CPU driver blocks start");
                lobby.GetParticipant(0).Cpu.enabled = true;
                Require(!lobby.TryAddCpu(-1) && !lobby.TryAddCpu(4) && !lobby.TrySetPlayerCount(3), "Slot bounds and occupied count");
                Require(!lobby.TrySetCpuDifficulty(0, (CpuDifficulty)99), "Invalid difficulty rejected");
                Require(lobby.TrySetCpuDifficulty(0, CpuDifficulty.God) && lobby.TrySetCpuDifficulty(0, CpuDifficulty.Noob), "Difficulty selection");
                Require(lobby.GetParticipant(0).Cpu.Settings.ReactionDelay > lobby.GetParticipant(3).Cpu.Settings.ReactionDelay,
                    "Difficulty reaction ordering");
                Vector3 pausedPosition = lobby.GetParticipant(3).transform.position;
                yield return null;
                Require(lobby.GetParticipant(3).transform.position == pausedPosition, "CPU waits in lobby");

                Require(lobby.TryLeave(0) && !lobby.TryStartGame() && !lobby.TryLeave(0), "Pending removal blocks start and duplicate leave");
                yield return null;
                yield return null;
                device = InputSystem.AddDevice<Gamepad>();
                Require(lobby.TryJoin(device), "Human joins CPU vacancy");
                Require(lobby.GetPlayer(0) != null && lobby.GetPlayer(0).devices.Count == 1, "Human pairing preserved");
                Require(lobby.GetParticipant(1).IsCpu && PlayerInput.all.Count == 1 && lobby.CanStart, "Mixed roster ready");
                Require(!lobby.TryJoin(device), "Duplicate human rejected");
                Require(lobby.TryStartGame(), "Mixed round starts with navigation");
                Require(!lobby.TryAddCpu(0) && !lobby.TrySetCpuDifficulty(1, CpuDifficulty.God), "CPU changes lock during round");
                Require(root.Q<Label>("player4Name").text.StartsWith("CPU 4"), "HUD identifies CPU");

                // Isolate one bot's route across the central machine without changing its movement rules.
                lobby.GetParticipant(1).Cpu.enabled = false;
                lobby.GetParticipant(2).Cpu.enabled = false;
                CouchPlayerController god = lobby.GetParticipant(3);
                CharacterController blocker = lobby.GetParticipant(0).GetComponent<CharacterController>();
                blocker.enabled = false;
                blocker.transform.position = god.transform.position + new Vector3(0.8f, 0f, 0.8f);
                blocker.enabled = true;
                PickupFlask prefab = AssetDatabase.LoadAssetAtPath<PickupFlask>("Assets/Gameplay/FlaskPlaceholder.prefab");
                PickupFlask target = Object.Instantiate(prefab, new Vector3(5f, 0.5f, 4f), Quaternion.identity);
                float deadline = Time.realtimeSinceStartup + 20f;
                while (god.Score == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Require(god.Score > 0, $"God navigates, picks up, returns and scores; position {god.transform.position}, target {target}");
                Require(!god.IsCarrying, "Scoring releases held reference");
                Require(target == null || target.IsCollected, "Scored flask consumed once");

                // Each remaining difficulty can score through the same contact/throw/collector path.
                for (int slot = 1; slot < 3; slot++)
                {
                    CouchPlayerController cpu = lobby.GetParticipant(slot);
                    cpu.Cpu.enabled = true;
                    Object.Instantiate(prefab, cpu.transform.position, Quaternion.identity);
                    deadline = Time.realtimeSinceStartup + 8f;
                    while (cpu.Score == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                    Require(cpu.Score > 0, $"{cpu.Cpu.Difficulty} scores");
                }
                round.EndRound();
                Vector3 stopped = god.transform.position;
                god.Cpu.ReadCommand(out Vector2 movement, out bool attack);
                yield return null;
                Require(movement == Vector2.zero && !attack && god.transform.position == stopped, "CPU stops at round end");

                EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                yield return null;
                lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                round = GameManager.Instance;
                Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
                Require(lobby.TrySetPlayerCount(4), "CPU-only match selection");
                for (int i = 0; i < 4; i++) Require(lobby.TryAddCpu(i, (CpuDifficulty)i), "CPU-only roster");
                Require(PlayerInput.all.Count == 0 && lobby.TryStartGame(), "CPU-only round starts without devices");
                for (int i = 0; i < 4; i++)
                {
                    Object.Instantiate(prefab, lobby.GetParticipant(i).transform.position, Quaternion.identity);
                }
                deadline = Time.realtimeSinceStartup + 12f;
                bool allScored = false;
                while (!allScored && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    allScored = true;
                    for (int i = 0; i < 4; i++) allScored &= lobby.GetParticipant(i).Score > 0;
                }
                Require(allScored, "All four difficulties deliver and score");
                round.EndRound();
            }
            finally
            {
                if (device != null && device.added) InputSystem.RemoveDevice(device);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
