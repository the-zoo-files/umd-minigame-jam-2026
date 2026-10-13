using System;
using System.Collections;
using System.Reflection;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UmdJam.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class GameAudioSmokeTests
    {
        public static IEnumerator Exercise()
        {
            yield return null;
            var lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
            var audio = Object.FindAnyObjectByType<GameAudioFeedback>();
            Require(audio != null, "HUD installs shared audio feedback");
            AudioSource click = Source(audio, "clickSource");
            AudioSource ready = Source(audio, "readySource");
            AudioSource point = Source(audio, "pointSource");
            AudioSource pickup = Source(audio, "pickupSource");
            AudioSource connection = Source(audio, "connectionSource");
            AudioSource launch = Source(audio, "launchSource");
            AudioSource end = Source(audio, "endSource");
            AudioSource winner = Source(audio, "winnerSource");
            AudioSource throwing = Source(audio, "throwSource");
            Require(throwing.clip.name == "Throw", "Supplied whoosh loads");
            ValidatePreparedClip(throwing.clip);
            Require(winner.clip.name == "Winner" && !winner.isPlaying, "Winner cue loads and begins silent");
            ValidatePreparedClip(winner.clip);
            Require(end.clip.name == "End", "Supplied end clip loads");
            ValidatePreparedClip(end.clip);
            Require(click.clip.name == "ButtonClick" && ready.clip.name == "Ready" && point.clip.name == "Point" &&
                click.clip.length > 0f && ready.clip.length > 0f && point.clip.length > 0f, "All supplied clips import and load");
            ValidatePreparedClip(click.clip);
            ValidatePreparedClip(ready.clip);
            ValidatePreparedClip(point.clip);
            Require(pickup.clip.name == "Pickup" && pickup.clip.length < 0.4f, "Pickup uses one isolated bottle opening");
            ValidatePreparedClip(pickup.clip);
            Require(connection.clip.name == "Connection" && launch.clip.name == "PotionLaunch", "New supplied cues load");
            ValidatePreparedClip(connection.clip);
            ValidatePreparedClip(launch.clip);
            Require(!click.isPlaying && !ready.isPlaying && !point.isPlaying, "Lobby begins silent");
            GameManager.Instance.EndRound();
            Require(!end.isPlaying, "Ending an unstarted round stays silent");
            Button toggle = audio.GetComponent<UIDocument>().rootVisualElement.Q<Button>("randomEvents");
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = toggle;
                toggle.SendEvent(submit);
            }
            Require(click.isPlaying && lobby.RandomEventsEnabled, "UI submit executes the action and plays shared click");
            Require(lobby.TrySetPlayerCount(1) && lobby.TryAddCpu(0) && !ready.isPlaying,
                "CPU addition keeps an already startable auto-fill lobby silent");
            lobby.GetParticipant(0).Cpu.enabled = false;
            Require(lobby.TryCyclePlayerColor(0, 1) && !lobby.CanStart, "Disabled CPU makes lobby unready");
            lobby.GetParticipant(0).Cpu.enabled = true;
            Require(lobby.TryCyclePlayerColor(0, 1) && ready.isPlaying, "Restored readiness plays ready sound");
            Require(!connection.isPlaying, "CPU additions do not announce human connections");
            ready.Stop();
            Require(lobby.TryCyclePlayerColor(0, 1) && !ready.isPlaying, "Unchanged readiness does not replay sound");
            audio.enabled = false;
            Require(!click.isPlaying, "Disable stops sounds");
            audio.enabled = true;
            Require(!ready.isPlaying, "Re-enable seeds readiness without announcing it again");
            Require(lobby.TrySetPlayerCount(2) && lobby.TryAddCpu(1) && !ready.isPlaying, "CPU addition does not repeat unchanged readiness");
            Require(lobby.TryStartGame(), "Audio scoring fixture starts");
            MachineFlaskShooter machine = Object.FindAnyObjectByType<MachineFlaskShooter>();
            machine.enabled = false;
            MethodInfo launchFlask = typeof(MachineFlaskShooter).GetMethod("LaunchFlask", BindingFlags.Instance | BindingFlags.NonPublic);
            int flaskCount = PickupFlask.ActiveFlasks.Count;
            launchFlask.Invoke(machine, null);
            Require(launch.isPlaying && PickupFlask.ActiveFlasks.Count == flaskCount + 1, "Completed machine launch immediately plays potion cue");
            launch.Stop();
            FieldInfo cap = typeof(MachineFlaskShooter).GetField("maximumActiveFlasks", BindingFlags.Instance | BindingFlags.NonPublic);
            int previousCap = (int)cap.GetValue(machine);
            cap.SetValue(machine, 1);
            typeof(MachineFlaskShooter).GetField("launchTimer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(machine, 0f);
            typeof(MachineFlaskShooter).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(machine, null);
            Require(!launch.isPlaying && PickupFlask.ActiveFlasks.Count == flaskCount + 1, "Spawn cap produces no launch sound");
            cap.SetValue(machine, previousCap);
            PickupFlask prefab = AssetDatabase.LoadAssetAtPath<PickupFlask>("Assets/Prefabs/Gameplay/FlaskPlaceholder.prefab");
            PlayerFlaskCollector collector = PlayerFlaskCollector.GetForPlayer(1);
            PickupFlask flask = Object.Instantiate(prefab, collector.CollectionPoint, Quaternion.identity);
            Transform socket = new GameObject("Audio fixture socket").transform;
            socket.position = collector.CollectionPoint;
            Require(!flask.TryPickUp(null) && !pickup.isPlaying, "Rejected pickup stays silent");
            Require(flask.TryPickUp(socket) && pickup.isPlaying && flask.IsHeld && flask.transform.parent == socket,
                "Successful attachment immediately starts pickup audio");
            pickup.Stop();
            Require(!flask.TryPickUp(socket) && !pickup.isPlaying, "Already-held flask cannot replay pickup audio");
            Require(flask.TryThrow(Vector3.zero, 1) && flask.TryPickUp(socket) && pickup.isPlaying,
                "A flask can sound again on a later successful pickup");
            Require(flask.TryThrow(Vector3.zero, 1), "Audio fixture throws a flask");
            Require(throwing.isPlaying && throwing.volume == pickup.volume, "Successful flask release immediately plays normalized whoosh");
            throwing.Stop();
            Require(!flask.TryThrow(Vector3.zero, 1) && !throwing.isPlaying, "Rejected duplicate throw stays silent");
            collector.Collect(flask);
            Require(point.isPlaying && lobby.GetParticipant(0).Score > 0, "Successful scoring plays supplied point sound");
            Require(click.volume == ready.volume && ready.volume == point.volume && point.volume == pickup.volume && pickup.volume == launch.volume,
                "All normalized cues use the same playback gain");
            point.Stop();
            collector.Collect(flask);
            Require(!point.isPlaying, "Duplicate collection cannot replay scoring sound");
            PickupFlask direct = Object.Instantiate(prefab, socket.position, Quaternion.identity);
            Require(direct.TryPickUp(socket) && direct.TryThrowDirectly(socket.position, 1, 1f, () => { }) && throwing.isPlaying,
                "Direct transfer plays the same release whoosh");
            int voices = audio.GetComponents<AudioSource>().Length;
            audio.enabled = false;
            Require(!pickup.isPlaying, "Disable stops the pickup voice");
            Require(!throwing.isPlaying, "Disable stops the throw voice");
            audio.enabled = true;
            Require(audio.GetComponents<AudioSource>().Length == voices && !point.isPlaying, "Re-enable reuses sources without replaying scores");
            typeof(GameManager).GetField("remainingTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(GameManager.Instance, 0f);
            typeof(GameManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(GameManager.Instance, null);
            Require(GameManager.Instance.IsRoundOver && Time.timeScale == 0f && end.isPlaying && end.volume == pickup.volume,
                "Timer expiry plays the normalized end cue immediately, even with gameplay paused");
            end.Stop();
            GameManager.Instance.EndRound();
            Require(!end.isPlaying, "Repeated round end cannot replay end audio");
            end.Play();
            audio.enabled = false;
            Require(!end.isPlaying, "Disabling audio stops the end voice");
            audio.enabled = true;
            Require(!end.isPlaying && audio.GetComponents<AudioSource>().Length == voices,
                "Re-enabling after round end reuses voices without replaying end audio");
            launchFlask.Invoke(machine, null);
            Require(!launch.isPlaying, "Non-playing machine launch cannot announce gameplay audio");
            PickupFlask afterRound = Object.Instantiate(prefab, socket.position, Quaternion.identity);
            Require(afterRound.TryPickUp(socket) && !pickup.isPlaying, "Non-playing pickup cannot announce gameplay audio");
            Require(afterRound.TryThrow(Vector3.zero, 1) && !throwing.isPlaying, "Non-playing throw stays silent");
            yield return null;
            Require(!point.isPlaying, "Penalty deductions do not play reward sounds");
        }

        private static AudioSource Source(GameAudioFeedback audio, string name)
        {
            AudioSource source = (AudioSource)typeof(GameAudioFeedback).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            Require(source != null && source.clip != null, $"Sound source {name} is configured");
            return source;
        }

        private static void ValidatePreparedClip(AudioClip clip)
        {
            Require(clip.loadState == AudioDataLoadState.Loaded && clip.loadType == AudioClipLoadType.DecompressOnLoad,
                $"{clip.name} is decoded before its first interaction");
            float[] samples = new float[clip.samples * clip.channels];
            Require(clip.GetData(samples, 0), $"{clip.name} samples are readable");
            float peak = 0f;
            float initialPeak = 0f;
            int initialSamples = Mathf.CeilToInt(clip.frequency * 0.005f) * clip.channels;
            for (int i = 0; i < samples.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
                if (i < initialSamples) initialPeak = Mathf.Max(initialPeak, Mathf.Abs(samples[i]));
            }
            Require(peak > 0f && peak <= 0.708f && initialPeak >= peak * 0.01f,
                $"{clip.name} starts within 5 ms and retains at least 3 dB of peak headroom");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
