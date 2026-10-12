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
            Require(click.clip.name == "ButtonClick" && ready.clip.name == "Ready" && point.clip.name == "Point" &&
                click.clip.length > 0f && ready.clip.length > 0f && point.clip.length > 0f, "All supplied clips import and load");
            Require(!click.isPlaying && !ready.isPlaying && !point.isPlaying, "Lobby begins silent");
            Button toggle = audio.GetComponent<UIDocument>().rootVisualElement.Q<Button>("randomEvents");
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = toggle;
                toggle.SendEvent(submit);
            }
            Require(click.isPlaying && lobby.RandomEventsEnabled, "UI submit executes the action and plays shared click");
            Require(lobby.TrySetPlayerCount(1) && lobby.TryAddCpu(0) && ready.isPlaying, "Becoming fully ready plays ready sound");
            ready.Stop();
            Require(lobby.TryCyclePlayerColor(0, 1) && !ready.isPlaying, "Unchanged readiness does not replay sound");
            audio.enabled = false;
            Require(!click.isPlaying, "Disable stops sounds");
            audio.enabled = true;
            Require(!ready.isPlaying, "Re-enable seeds readiness without announcing it again");
            Require(lobby.TrySetPlayerCount(2) && lobby.TryAddCpu(1) && ready.isPlaying, "Readiness announces after becoming unready again");
            Require(lobby.TryStartGame(), "Audio scoring fixture starts");
            Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
            PickupFlask prefab = AssetDatabase.LoadAssetAtPath<PickupFlask>("Assets/Prefabs/Gameplay/FlaskPlaceholder.prefab");
            PlayerFlaskCollector collector = PlayerFlaskCollector.GetForPlayer(1);
            PickupFlask flask = Object.Instantiate(prefab, collector.CollectionPoint, Quaternion.identity);
            Transform socket = new GameObject("Audio fixture socket").transform;
            socket.position = collector.CollectionPoint;
            Require(flask.TryPickUp(socket) && flask.TryThrow(Vector3.zero, 1), "Audio fixture throws a flask");
            collector.Collect(flask);
            Require(point.isPlaying && lobby.GetParticipant(0).Score > 0, "Successful scoring plays supplied point sound");
            point.Stop();
            collector.Collect(flask);
            Require(!point.isPlaying, "Duplicate collection cannot replay scoring sound");
            int voices = audio.GetComponents<AudioSource>().Length;
            audio.enabled = false;
            audio.enabled = true;
            Require(audio.GetComponents<AudioSource>().Length == voices && !point.isPlaying, "Re-enable reuses sources without replaying scores");
            GameManager.Instance.EndRound();
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

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
