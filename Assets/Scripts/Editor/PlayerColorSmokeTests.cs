using System;
using System.Collections.Generic;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmdJam.Editor
{
    public static class PlayerColorSmokeTests
    {
        public static void Check(CouchMultiplayerManager lobby, VisualElement root)
        {
            Require(PlayerColorPalette.Count == 18, "All 18 selectable colors");
            HashSet<string> names = new();
            HashSet<Color> colors = new();
            HashSet<int> assigned = new();
            for (int index = 0; index < PlayerColorPalette.Count; index++)
            {
                PlayerColorPalette.Entry entry = PlayerColorPalette.Get(index);
                Require(names.Add(entry.Name) && colors.Add(entry.Color), "Palette entries are unique");
            }
            for (int slot = 0; slot < 4; slot++)
            {
                Require(assigned.Add(lobby.GetParticipant(slot).ColorIndex), "Unique initial CPU colors");
            }

            CouchPlayerController first = lobby.GetParticipant(0);
            Material body = first.GetComponent<Renderer>().sharedMaterial;
            Require(!lobby.TrySetPlayerColor(0, lobby.GetParticipant(1).ColorIndex), "Taken color rejected");
            Require(!lobby.TrySetPlayerColor(-1, 0) && !lobby.TrySetPlayerColor(4, 0) &&
                !lobby.TrySetPlayerColor(0, -1) && !lobby.TrySetPlayerColor(0, PlayerColorPalette.Count), "Invalid color and slot rejected");
            Require(lobby.TrySetPlayerColor(0, 17) && lobby.TryCyclePlayerColor(0, 1) && first.ColorIndex == 1,
                "Forward cycling wraps and skips occupied Red");
            Require(lobby.TryCyclePlayerColor(0, -1) && first.ColorIndex == 17, "Backward cycling skips occupied Red");
            for (int index = 0; index < PlayerColorPalette.Count; index++)
            {
                bool available = CouchPlayerController.IsColorAvailable(index, first);
                Require(lobby.TrySetPlayerColor(0, index) == available, "Each selectable color respects occupancy");
                if (!available) continue;
                Require(body == first.GetComponent<Renderer>().sharedMaterial && body.color == first.PlayerColor,
                    "Color changes reuse the owned material");
                Require(first.transform.Find("DirectionGizmo").GetComponent<Renderer>().sharedMaterial.color == first.PlayerColor,
                    "Direction marker color follows selection");
                Require(root.Q<Label>("playerColor1").text == PlayerColorPalette.Get(index).Name &&
                    root.Q<Label>("player1Name").style.borderTopColor.value == first.PlayerColor,
                    "Lobby label and HUD style follow selection");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
