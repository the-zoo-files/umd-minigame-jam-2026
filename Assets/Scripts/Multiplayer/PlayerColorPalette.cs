using UnityEngine;

namespace UmdJam.Multiplayer
{
    public static class PlayerColorPalette
    {
        // Among Us's 18 selectable player colors, in its standard palette order.
        private static readonly Entry[] Entries =
        {
            new("Red", 197, 17, 17),
            new("Blue", 19, 46, 209),
            new("Green", 17, 127, 45),
            new("Pink", 237, 84, 186),
            new("Orange", 239, 125, 14),
            new("Yellow", 245, 245, 87),
            new("Black", 63, 71, 78),
            new("White", 214, 224, 240),
            new("Purple", 107, 47, 187),
            new("Brown", 113, 73, 30),
            new("Cyan", 56, 254, 220),
            new("Lime", 80, 239, 57),
            new("Maroon", 107, 43, 58),
            new("Rose", 236, 192, 211),
            new("Banana", 255, 254, 190),
            new("Gray", 112, 132, 150),
            new("Tan", 146, 135, 118),
            new("Coral", 236, 117, 120)
        };

        private static int[] defaultColors = { 0, 1, 5, 2 };

        public static int Count => Entries.Length;
        public static bool IsValid(int index) => index >= 0 && index < Count;
        public static Entry Get(int index) => Entries[index];
        public static int DefaultForSlot(int slot) => defaultColors[Mathf.Clamp(slot, 0, defaultColors.Length - 1)];

        public static void ConfigureDefaults(int[] colors)
        {
            if (colors == null || colors.Length < CouchMultiplayerManager.MaximumPlayers) return;
            int[] validated = new int[CouchMultiplayerManager.MaximumPlayers];
            for (int i = 0; i < validated.Length; i++)
            {
                if (!IsValid(colors[i])) return;
                validated[i] = colors[i];
            }
            defaultColors = validated;
        }

        public readonly struct Entry
        {
            public string Name { get; }
            public Color Color { get; }
            public Color TextColor => Color.grayscale > 0.5f ? UnityEngine.Color.black : UnityEngine.Color.white;

            public Entry(string name, byte red, byte green, byte blue)
            {
                Name = name;
                Color = new Color32(red, green, blue, 255);
            }
        }
    }
}
