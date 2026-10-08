using System.Collections.Generic;
using UmdJam.Gameplay;

namespace UmdJam.Multiplayer
{
    public sealed class RoundResults
    {
        public IReadOnlyList<PlayerRoundResult> Players { get; }
        public IReadOnlyList<int> WinnerPlayerNumbers { get; }

        private RoundResults(List<PlayerRoundResult> players)
        {
            Players = players.AsReadOnly();
            List<int> winners = new();
            int highest = int.MinValue;
            foreach (PlayerRoundResult player in players)
            {
                if (player.FinalScore < highest) continue;
                if (player.FinalScore > highest)
                {
                    winners.Clear();
                    highest = player.FinalScore;
                }
                winners.Add(player.PlayerNumber);
            }
            WinnerPlayerNumbers = winners.AsReadOnly();
        }

        public static RoundResults Capture(GameManager gameManager,
            IReadOnlyList<CouchPlayerController> players, IReadOnlyList<PickupFlask> flasks)
        {
            List<PlayerRoundResult> results = new();
            for (int number = 1; number <= CouchMultiplayerManager.MaximumPlayers; number++)
            {
                CouchPlayerController participant = null;
                foreach (CouchPlayerController player in players)
                {
                    if (player != null && player.PlayerNumber == number) participant = player;
                }
                if (participant == null) continue;
                List<FlaskPenaltyEntry> entries = new();
                foreach (PickupFlask flask in flasks)
                {
                    if (gameManager.GetFlaskPenaltyPlayer(flask) == number)
                        entries.Add(new FlaskPenaltyEntry(flask.transform.position, flask.PointValue));
                }
                results.Add(new PlayerRoundResult(number, participant.DisplayName, participant.PlayerColor,
                    gameManager.GetPlayerZone(number), participant.Score, entries));
            }
            return new RoundResults(results);
        }
    }
}
