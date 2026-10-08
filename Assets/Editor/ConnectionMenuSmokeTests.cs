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
            InputSettings settings = InputSystem.settings;
            InputSettings.BackgroundBehavior originalBackgroundBehavior = settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode originalEditorBehavior = settings.editorInputBehaviorInPlayMode;
            try
            {
                // Hidden batch Editors have no focused Game view. Route synthetic devices as in a player.
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
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
                Require(root.Q<Button>("randomEvents").text == "Random Events: Off" && !lobby.RandomEventsEnabled,
                    "Random event toggle defaults off");
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
                Require(root.Q<Label>("playerCharacter1").text == "Criminal" &&
                    !root.Q("characterSelector1").ClassListContains("is-hidden"), "Default character selector");
                Require(lobby.CanStart && root.Q<Button>("startGame").enabledSelf,
                    $"Two connected players ready (roster={lobby.CanStart}, button={root.Q<Button>("startGame").enabledSelf})");
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
                Require(!lobby.TrySetPlayerColor(0, lobby.GetParticipant(1).ColorIndex), "Disconnected player keeps its color");
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
                int keyboardColor = lobby.GetParticipant(2).ColorIndex;
                int firstColor = lobby.GetParticipant(0).ColorIndex;
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                yield return null;
                Require(lobby.GetParticipant(2).ColorIndex != keyboardColor && lobby.GetParticipant(0).ColorIndex == firstColor,
                    $"Paired keyboard color command (before={keyboardColor}, after={lobby.GetParticipant(2).ColorIndex}, first={lobby.GetParticipant(0).ColorIndex}, paired={PlayerInput.FindFirstPairedToDevice(keyboard)}, pressed={keyboard.eKey.isPressed})");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                yield return null;
                yield return null;
                Require(lobby.GetParticipant(2).ColorIndex == keyboardColor, "Previous color command reverses selection");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
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

                Require(lobby.TryLeave(3), "Replace human with CPU for menu check");
                yield return null;
                yield return null;
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = root.Q<Button>("addCpu4");
                    root.Q<Button>("addCpu4").SendEvent(submit);
                }
                yield return null;
                yield return null;
                Require(lobby.GetParticipant(3) != null && lobby.GetParticipant(3).IsCpu, "Add CPU button");
                Require(lobby.GetParticipant(3).Cpu.Difficulty == CpuDifficulty.Noob &&
                    root.Q<Button>("cpuDifficulty4").text == "Noob", "Add CPU starts on the easiest difficulty");
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = root.Q<Button>("cpuDifficulty4");
                    root.Q<Button>("cpuDifficulty4").SendEvent(submit);
                }
                yield return null;
                yield return null;
                Require(lobby.GetParticipant(3).Cpu.Difficulty == CpuDifficulty.Pro &&
                    root.Q<Button>("cpuDifficulty4").text == "Pro", "Difficulty button cycles from Noob to Pro");
                int originalColor = lobby.GetParticipant(3).ColorIndex;
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = root.Q<Button>("nextColor4");
                    root.Q<Button>("nextColor4").SendEvent(submit);
                }
                yield return null;
                yield return null;
                Require(lobby.GetParticipant(3).ColorIndex != originalColor, "CPU color button changes selection");
                Require(root.Q<Label>("playerColor4").text == PlayerColorPalette.Get(lobby.GetParticipant(3).ColorIndex).Name,
                    "Color button label follows selection");
                yield return null;
                yield return null;
                Rect cpuCard = root.Q("connectionPlayer4").worldBound;
                Rect difficultyBounds = root.Q("cpuDifficulty4").worldBound;
                Rect removeBounds = root.Q("leavePlayer4").worldBound;
                Rect colorBounds = root.Q("colorSelector4").worldBound;
                Rect characterBounds = root.Q("characterSelector4").worldBound;
                Require(cpuCard.Contains(difficultyBounds.min) && cpuCard.Contains(difficultyBounds.max) &&
                    cpuCard.Contains(removeBounds.min) && cpuCard.Contains(removeBounds.max), "CPU controls fit card at 4:3");
                Require(cpuCard.Contains(colorBounds.min) && cpuCard.Contains(colorBounds.max), "Color selector fits 4:3 card");
                Require(cpuCard.Contains(characterBounds.min) && cpuCard.Contains(characterBounds.max),
                    "Character selector fits 4:3 card");
                SaveCapture(capture, ".utmp/connection-menu-cpu.png");
                Require(lobby.TryLeave(3), "Remove CPU before human rejoin");
                yield return null;
                yield return null;
                Require(lobby.TryJoin(third) && lobby.CanStart, "Human can rejoin former CPU slot");

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
                settings.editorInputBehaviorInPlayMode = originalEditorBehavior;
                settings.backgroundBehavior = originalBackgroundBehavior;
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
