using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class CpuBalanceBenchmark
    {
        public static IEnumerator Exercise(float roundTimeLimit = 0f)
        {
            StringBuilder csv = new("seed,rotation,slot,difficulty,delivered,penalty,finalScore,idleSeconds,targetSwitches\n");
            Random.State originalRandom = Random.state;
            try
            {
                for (int seed = 0; seed < 2; seed++)
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    if (seed != 0 || rotation != 0)
                        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
                    yield return null;
                    yield return null;
                    Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
                    Random.InitState(4100 + seed);
                    System.Random spawnRandom = new(8100 + seed);
                    CouchMultiplayerManager lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                    if (!lobby.TrySetPlayerCount(4)) throw new System.InvalidOperationException("Benchmark lobby");
                    CouchPlayerController[] players = new CouchPlayerController[4];
                    for (int slot = 0; slot < 4; slot++)
                    {
                        if (!lobby.TryAddCpu(slot, (CpuDifficulty)((slot + rotation) % 4)))
                            throw new System.InvalidOperationException("Benchmark CPU creation");
                        players[slot] = lobby.GetParticipant(slot);
                    }
                    if (roundTimeLimit > 0f)
                        typeof(GameManager).GetField("remainingTime", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(GameManager.Instance, roundTimeLimit);
                    if (!lobby.TryStartGame()) throw new System.InvalidOperationException("Benchmark start");
                    PickupFlask prefab = AssetDatabase.LoadAssetAtPath<PickupFlask>("Assets/Gameplay/FlaskPlaceholder.prefab");
                    Vector3[] previousPositions = new Vector3[4];
                    PickupFlask[] previousTargets = new PickupFlask[4];
                    float[] idle = new float[4];
                    int[] switches = new int[4];
                    for (int slot = 0; slot < 4; slot++) previousPositions[slot] = players[slot].transform.position;
                    float started = Time.time;
                    float watchdog = Time.realtimeSinceStartup + 90f;
                    float nextSpawn = started;
                    int spawnIndex = 0;
                    while (GameManager.Instance.IsPlaying && Time.time - started < 20f)
                    {
                        if (Time.realtimeSinceStartup > watchdog)
                            throw new System.InvalidOperationException("Benchmark round did not advance within 90 seconds.");
                        if (Time.time >= nextSpawn)
                        {
                            nextSpawn += 0.6f;
                            // Equal quadrant opportunities; two seeded layouts, rotated difficulty assignments.
                            float angle = ((spawnIndex++ % 4) + 0.15f + (float)spawnRandom.NextDouble() * 0.7f) * Mathf.PI * 0.5f;
                            float radius = 3f + (float)spawnRandom.NextDouble() * 3.5f;
                            Object.Instantiate(prefab, new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius), Quaternion.identity);
                        }
                        yield return null;
                        for (int slot = 0; slot < 4; slot++)
                        {
                            Vector3 position = players[slot].transform.position;
                            if (CpuNavigation.PlanarDistance(position, previousPositions[slot]) < 0.1f * Time.deltaTime)
                                idle[slot] += Time.deltaTime;
                            PickupFlask target = CpuBehaviorChecks.Target(players[slot].Cpu);
                            if (previousTargets[slot] != null && previousTargets[slot].IsAvailable && target != null && target != previousTargets[slot])
                                switches[slot]++;
                            previousTargets[slot] = target;
                            previousPositions[slot] = position;
                        }
                    }
                    GameManager.Instance.EndRound();
                    foreach (PlayerRoundResult result in GameManager.Instance.Results.Players)
                    {
                        int slot = result.PlayerNumber - 1;
                        csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7:F2},{8}\n",
                            8100 + seed, rotation, slot + 1, players[slot].Cpu.Difficulty, result.StartingScore,
                            result.Penalty, result.FinalScore, idle[slot], switches[slot]);
                    }
                    Directory.CreateDirectory(".utmp");
                    File.WriteAllText(roundTimeLimit > 0f ? ".utmp/cpu-balance-short.csv" : ".utmp/cpu-balance.csv", csv.ToString());
                    Debug.Log($"CPU benchmark: completed seed {seed + 1}/2, rotation {rotation + 1}/4");
                }
            }
            finally
            {
                Random.state = originalRandom;
            }
        }
    }
}
