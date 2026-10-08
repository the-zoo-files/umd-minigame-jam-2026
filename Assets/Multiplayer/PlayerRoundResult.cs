using System.Collections.Generic;
using UnityEngine;

namespace UmdJam.Multiplayer
{
    public sealed class PlayerRoundResult
    {
        public int PlayerNumber { get; }
        public string DisplayName { get; }
        public Color PlayerColor { get; }
        public Bounds ZoneBounds { get; }
        public int StartingScore { get; }
        public int Penalty { get; }
        public int FinalScore => StartingScore - Penalty;
        public IReadOnlyList<FlaskPenaltyEntry> Flasks { get; }

        public PlayerRoundResult(int playerNumber, string displayName, Color playerColor,
            Bounds zoneBounds, int startingScore, IReadOnlyList<FlaskPenaltyEntry> flasks)
        {
            PlayerNumber = playerNumber;
            DisplayName = displayName;
            PlayerColor = playerColor;
            ZoneBounds = zoneBounds;
            StartingScore = startingScore;
            List<FlaskPenaltyEntry> entries = new(flasks.Count);
            foreach (FlaskPenaltyEntry entry in flasks)
            {
                entries.Add(entry);
                Penalty += entry.Points;
            }
            // Stable insertion sort: preserve order for coincident positions.
            for (int i = 1; i < entries.Count; i++)
            {
                FlaskPenaltyEntry entry = entries[i];
                int j = i - 1;
                while (j >= 0 && ComesBefore(entry.WorldPosition, entries[j].WorldPosition))
                {
                    entries[j + 1] = entries[j];
                    j--;
                }
                entries[j + 1] = entry;
            }
            Flasks = entries.AsReadOnly();
        }

        private static bool ComesBefore(Vector3 a, Vector3 b)
        {
            return a.z > b.z || (a.z == b.z && a.x < b.x);
        }
    }
}
