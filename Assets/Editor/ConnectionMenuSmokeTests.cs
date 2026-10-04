using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class ConnectionMenuSmokeTests
    {
        public static IEnumerator Exercise()
        {
            List<InputDevice> devices = new();
            PanelSettings panel = null;
            RenderTexture capture = null;
            RenderTexture previousTarget = null;
            try
            {
                yield return null;
                CouchMultiplayerManager lobby = Object.FindAnyObjectByType<CouchMultiplayerManager>();
                GameManager round = GameManager.Instance;
                UIDocument document = Object.FindAnyObjectByType<UIDocument>();
                VisualElement root = document.rootVisualElement;
                panel = document.panelSettings;
                previousTarget = panel.targetTexture;
                capture = new RenderTexture(1920, 1080, 0);
                capture.Create();
                panel.targetTexture = capture;
                Require(lobby != null && round != null, "Scene wiring");
                Require(lobby.IsLobbyOpen && !round.IsPlaying && Time.timeScale == 0, "Lobby pauses simulation");
                Require(!root.Q("connectionMenu").ClassListContains("is-hidden"), "Menu visible");
                Require(root.Q("gameplayHud").ClassListContains("is-hidden"), "HUD hidden");
                Require(root.Q<Label>("playerCount").text == "2", "Default player count");
                Require(root.Q<Button>("startGame").text == "Start Game", "Start wording");
                Require(!lobby.TryStartGame(), "Cannot start empty lobby");
                Require(!lobby.TrySetPlayerCount(0) && !lobby.TrySetPlayerCount(5), "Count bounds");
                float initialTime = round.RemainingTime;
                float deadline = Time.realtimeSinceStartup + 1;
                while (Time.realtimeSinceStartup < deadline) yield return null;
                Require(round.RemainingTime == initialTime, "Timer waits for start");
                Require(Object.FindObjectsByType<PickupFlask>().Length == 0, "Machine waits for start");
                SaveCapture(capture, ".utmp/connection-menu-empty.png");
                yield return null;
                yield return null;

                Gamepad first = InputSystem.AddDevice<Gamepad>();
                Gamepad second = InputSystem.AddDevice<Gamepad>();
                Gamepad third = InputSystem.AddDevice<Gamepad>();
                devices.Add(first);
                devices.Add(second);
                devices.Add(third);
                InputSystem.QueueStateEvent(first, new GamepadState().WithButton(GamepadButton.South));
                InputSystem.QueueStateEvent(second, new GamepadState().WithButton(GamepadButton.South));
                yield return null;
                yield return null;
                Require(lobby.GetPlayer(0) != null && lobby.GetPlayer(1) != null, "Simultaneous controller joins");
                Require(lobby.CanStart && root.Q<Button>("startGame").enabledSelf, "Two connected players ready");
                Require(!lobby.TryJoin(first), "Duplicate join rejected");
                Require(!lobby.TryJoin(third), "Unselected slot cannot join");
                Require(!lobby.TrySetPlayerCount(1), "Count cannot remove a joined slot");
                InputSystem.QueueStateEvent(first, new GamepadState());
                InputSystem.QueueStateEvent(second, new GamepadState());
                InputSystem.RemoveDevice(second);
                yield return null;
                yield return null;
                Require(!lobby.CanStart, "Disconnected controller blocks start");
                Require(root.Q<Label>("connectionState2").text == "Reconnect device", "Reconnect prompt");
                Require(!lobby.TryStartGame(), "Start rejects disconnected controller");
                InputSystem.AddDevice(second);
                yield return null;
                yield return null;
                Require(lobby.CanStart, "Reconnection restores readiness");

                Require(lobby.TryLeave(0), "Leave slot");
                Require(!lobby.TryStartGame(), "Pending leave blocks same-frame start");
                yield return null;
                yield return null;
                Require(lobby.GetPlayer(0) == null && !lobby.CanStart, "Leave frees slot and blocks start");
                Require(lobby.TryJoin(first), "Join fills lowest vacant slot");
                Require(lobby.GetPlayer(0).devices[0] == first, "Rejoined device ownership");
                Require(lobby.TrySetPlayerCount(4), "Four-player selection");

                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                devices.Add(keyboard);
                devices.Add(mouse);
                Require(lobby.TryJoin(keyboard), "Keyboard player joins");
                Require(lobby.GetPlayer(2).currentControlScheme == "Keyboard&Mouse", "Keyboard scheme");
                Require(!lobby.TryJoin(keyboard), "Keyboard cannot fill two slots");
                Require(lobby.TryJoin(third), "Fourth player joins");
                Require(lobby.CanStart && PlayerInput.all.Count == 4, "Four players ready");
                Require(root.Q<Label>("connectionDevice3").text == "Keyboard / Mouse", "Keyboard card");
                yield return null;
                yield return null;
                SaveCapture(capture, ".utmp/connection-menu-ready.png");
                RenderTexture wideCapture = capture;
                capture = new RenderTexture(1440, 1080, 0);
                capture.Create();
                panel.targetTexture = capture;
                wideCapture.Release();
                Object.Destroy(wideCapture);
                yield return null;
                yield return null;
                yield return null;
                SaveCapture(capture, ".utmp/connection-menu-4x3.png");
                Rect menuBounds = root.Q("connectionMenu").worldBound;
                Rect startBounds = root.Q("startGame").worldBound;
                Require(menuBounds.Contains(startBounds.min) && menuBounds.Contains(startBounds.max), "Start button fits 4:3");
                for (int slot = 1; slot <= 4; slot++)
                {
                    Rect cardBounds = root.Q($"connectionPlayer{slot}").worldBound;
                    Require(menuBounds.Contains(cardBounds.min) && menuBounds.Contains(cardBounds.max), "Card fits 4:3");
                }

                InputSystem.QueueStateEvent(first, new GamepadState().WithButton(GamepadButton.Start));
                yield return null;
                yield return null;
                Require(round.IsPlaying && !lobby.IsLobbyOpen && Time.timeScale == 1, "Start input begins round");
                Require(root.Q("connectionMenu").ClassListContains("is-hidden"), "Start hides menu");
                Require(!root.Q("gameplayHud").ClassListContains("is-hidden"), "Start shows HUD");
                Require(!lobby.TryStartGame() && !lobby.TrySetPlayerCount(2) && !lobby.TryLeave(0), "Lobby mutations locked after start");
                Require(!lobby.GetComponent<PlayerInputManager>().joiningEnabled, "Late joining disabled");
                deadline = Time.realtimeSinceStartup + 1;
                while (Time.realtimeSinceStartup < deadline) yield return null;
                Require(round.RemainingTime < initialTime, "Timer runs after start");
                Require(Object.FindObjectsByType<PickupFlask>().Length > 0, "Spawner starts with round");
                round.EndRound();
                Require(round.IsRoundOver && !round.IsPlaying && Time.timeScale == 0, "Round end preserved");
                Require(root.Q("connectionMenu").ClassListContains("is-hidden"), "Round end does not reopen lobby");
            }
            finally
            {
                if (panel != null) panel.targetTexture = previousTarget;
                if (capture != null)
                {
                    capture.Release();
                    Object.Destroy(capture);
                }

                foreach (InputDevice device in devices)
                {
                    if (device.added) InputSystem.RemoveDevice(device);
                }
            }
        }

        private static void SaveCapture(RenderTexture capture, string path)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new(capture.width, capture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = capture;
                image.ReadPixels(new Rect(0, 0, capture.width, capture.height), 0, 0);
                image.Apply();
                Directory.CreateDirectory(".utmp");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(image);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
