using UnityEngine;

namespace UmdJam.Multiplayer
{
    public readonly struct FlaskPenaltyEntry
    {
        public Vector3 WorldPosition { get; }
        public int Points { get; }

        public FlaskPenaltyEntry(Vector3 worldPosition, int points)
        {
            WorldPosition = worldPosition;
            Points = points;
        }
    }
}
