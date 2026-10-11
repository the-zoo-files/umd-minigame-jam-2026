using System;
using System.Collections;
using System.Reflection;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class PlayerMovementSmokeTests
    {
        public static IEnumerator Exercise()
        {
            Gamepad device = null;
            var settings = InputSystem.settings;
            var originalBackground = settings.backgroundBehavior;
            var originalEditorInput = settings.editorInputBehaviorInPlayMode;
            try
            {
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                yield return null;
                Object.FindAnyObjectByType<MachineFlaskShooter>().enabled = false;
                var lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                device = InputSystem.AddDevice<Gamepad>();
                Require(lobby.TrySetPlayerCount(1) && lobby.TryJoin(device) && lobby.TryStartGame(), "Movement fixture starts");
                CouchPlayerController player = lobby.GetParticipant(0);
                player.enabled = false;
                Func<Vector2, float, Vector2> smooth = (Func<Vector2, float, Vector2>)Delegate.CreateDelegate(
                    typeof(Func<Vector2, float, Vector2>), player,
                    typeof(CouchPlayerController).GetMethod("SmoothHumanInput", BindingFlags.Instance | BindingFlags.NonPublic));
                Vector2 first = smooth(Vector2.right, 1f / 60f);
                Require(first.x > 0f && first.x < 0.5f && first.y == 0f, "Start accelerates without jumping to full input");
                for (int i = 0; i < 60; i++) smooth(Vector2.right, 1f / 60f);
                Require(Input(player) == Vector2.right, "Sustained input reaches exact full speed");
                Vector2 corner = smooth(Vector2.up, 1f / 60f);
                Require(corner.x > 0f && corner.y > 0f && corner.magnitude <= 1f, "Right-angle change follows a bounded curved transition");
                Vector2 reversal = smooth(Vector2.down, 1f / 60f);
                Require(reversal != Vector2.down && reversal.magnitude <= 1f, "Reversal is gradual and bounded");
                for (int i = 0; i < 60; i++) smooth(Vector2.zero, 1f / 60f);
                Require(Input(player) == Vector2.zero, "Release reaches exact rest with no persistent drift");
                for (int i = 0; i < 60; i++) smooth(new Vector2(0.4f, 0f), 1f / 60f);
                Require(Input(player) == new Vector2(0.4f, 0f), "Partial analog input retains its speed");
                for (int i = 0; i < 60; i++) smooth(Vector2.one, 1f / 60f);
                Require(Input(player).magnitude <= 1.000001f, "Diagonal movement cannot exceed top speed");
                Vector2 reference = Response(player, smooth, 30);
                Require(Vector2.Distance(reference, Response(player, smooth, 60)) < 0.00001f &&
                    Vector2.Distance(reference, Response(player, smooth, 120)) < 0.00001f,
                    "Equal elapsed time gives equal response at 30, 60, and 120 FPS");
                Vector2 before = Input(player);
                Require(smooth(Vector2.up, 0f) == before && smooth(Vector2.up, float.NaN) == before,
                    "Zero/invalid frame duration cannot advance movement");
                Require(smooth(new Vector2(float.NaN, 0f), 0.02f) == Vector2.zero, "Invalid input cannot poison movement state");
                Set(player, "movementSmoothingTime", 0f);
                Require(smooth(Vector2.right, 0.02f) == Vector2.right, "Zero smoothing preserves immediate input option");
                Set(player, "movementSmoothingTime", 0.05f);
                smooth(Vector2.up, 0.02f);
                long allocation = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 200; i++) smooth(Vector2.up, 1f / 60f);
                Require(GC.GetAllocatedBytesForCurrentThread() == allocation, "Warmed human smoothing allocates no managed memory");

                // Exercise the actual paired action, CharacterController path, and animation input.
                player.PlaceAtSpawn(0);
                InputSystem.QueueStateEvent(device, new GamepadState { leftStick = Vector2.right });
                yield return null;
                Action update = (Action)Delegate.CreateDelegate(typeof(Action), player,
                    typeof(CouchPlayerController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic));
                Vector3 origin = player.transform.position;
                update();
                float distance = CpuNavigation.PlanarDistance(origin, player.transform.position);
                Require(distance > 0f && distance < player.MoveSpeed * Time.deltaTime && Input(player).x < 1f,
                    "Paired stick movement uses the smoothed CharacterController command");
                player.enabled = true;
                Require(player.TryStun(0.2f) && Input(player) == Vector2.zero, "Stun immediately clears residual human movement");
                yield return null;
                Require(Input(player) == Vector2.zero, "Stunned input cannot rebuild residual movement");
                player.ClearEnvironmentalEffects();
                yield return null;
                Require(Input(player).x > 0f, "Human movement resumes after stun");
                player.enabled = false;
                Require(Input(player) == Vector2.zero, "Disable clears movement state");
                player.enabled = true;
                yield return null;
                GameManager.Instance.EndRound();
                Vector3 stopped = player.transform.position;
                yield return null;
                yield return null;
                Require(Input(player) == Vector2.zero && player.transform.position == stopped, "Round end stops and clears human movement");
            }
            finally
            {
                if (device != null && device.added) InputSystem.RemoveDevice(device);
                settings.backgroundBehavior = originalBackground;
                settings.editorInputBehaviorInPlayMode = originalEditorInput;
            }
        }

        private static Vector2 Response(CouchPlayerController player, Func<Vector2, float, Vector2> smooth, int fps)
        {
            Set(player, "smoothedHumanInput", Vector2.zero);
            for (int i = 0; i < fps / 10; i++) smooth(Vector2.right, 1f / fps);
            return Input(player);
        }

        private static Vector2 Input(CouchPlayerController player) =>
            (Vector2)typeof(CouchPlayerController).GetField("smoothedHumanInput", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
