using System;
using System.Collections;
using System.Reflection;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class MachineAmbienceSmokeTests
    {
        public static IEnumerator Exercise()
        {
            yield return null;
            MachineAmbience ambience = Object.FindAnyObjectByType<MachineAmbience>();
            Require(ambience != null, "Machine installs ambience without scene edits");
            AudioSource[] sources = ambience.GetComponents<AudioSource>();
            Require(sources.Length == 2 && !sources[0].isPlaying && !sources[1].isPlaying, "Exactly two voices, silent in lobby");
            foreach (AudioSource source in sources)
                Require(source.clip.name == "Bubbling" && source.clip.channels == 1 && source.spatialBlend == 1f &&
                    !source.loop && source.dopplerLevel == 0f && source.clip.loadState == AudioDataLoadState.Loaded,
                    "Preloaded mono bubbling emits spatially without a fixed loop");
            Vector3 center = GameManager.Instance.ArenaBounds.center;
            Require(Vector2.Distance(new Vector2(center.x, center.z),
                new Vector2(ambience.transform.position.x, ambience.transform.position.z)) < 0.1f, "Emitter is at arena center");
            var lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
            Require(lobby.TrySetPlayerCount(1) && lobby.TryAddCpu(0) && lobby.TryStartGame(), "Ambience fixture starts");
            ambience.enabled = false;
            typeof(MachineAmbience).GetField("random", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(ambience, new System.Random(123));
            Action<float> advance = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), ambience,
                typeof(MachineAmbience).GetMethod("Advance", BindingFlags.NonPublic | BindingFlags.Instance));
            UnityEngine.Random.State originalRandom = UnityEngine.Random.state;
            bool overlap = false;
            bool variedPitch = false;
            try
            {
                UnityEngine.Random.InitState(42);
                float expected = UnityEngine.Random.value;
                UnityEngine.Random.InitState(42);
                for (int i = 0; i < 160; i++)
                {
                    advance(0.05f);
                    overlap |= sources[0].volume > 0f && sources[1].volume > 0f;
                    variedPitch |= sources[0].isPlaying && sources[1].isPlaying && sources[0].pitch != sources[1].pitch;
                    foreach (AudioSource source in sources)
                        Require(float.IsFinite(source.volume) && source.volume <= 0.19f, "Crossfade gain remains bounded");
                }
                Require(UnityEngine.Random.value == expected, "Ambience does not alter gameplay randomness");
                Require(overlap && variedPitch, "Varying excerpts overlap smoothly with independent pitch variation");
                long allocation = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 200; i++) advance(0.05f);
                Require(GC.GetAllocatedBytesForCurrentThread() == allocation, "Warmed ambience scheduling allocates no managed memory");
            }
            finally
            {
                UnityEngine.Random.state = originalRandom;
            }
            ambience.enabled = true;
            Action update = (Action)Delegate.CreateDelegate(typeof(Action), ambience,
                typeof(MachineAmbience).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance));
            Time.timeScale = 0f;
            update();
            Require(!sources[0].isPlaying && !sources[1].isPlaying, "Pause suspends both voices");
            Time.timeScale = 1f;
            update();
            Require(sources[0].isPlaying || sources[1].isPlaying, "Resume continues ambience");
            GameManager.Instance.EndRound();
            Require(!sources[0].isPlaying && !sources[1].isPlaying, "Round end stops ambience synchronously");
            ambience.enabled = false;
            ambience.enabled = true;
            Require(ambience.GetComponents<AudioSource>().Length == 2, "Re-enable reuses owned voices");
            yield return null;
            Require(!sources[0].isPlaying && !sources[1].isPlaying, "Results remain silent");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
