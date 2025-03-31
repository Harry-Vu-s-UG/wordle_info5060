// Stats
// Anh Duc Vu, Jacob Wall,Jeong-Ah Yoon
// March 14, 2025
// This class is used to store the result of game and caculate stats

namespace WordleGameServer.Services
{
    public class Stats
    {
        public string Word { get; set; } = "";
        public int Players { get; set; }
        public int PlayersCorrect { get; set; }
        public int[] GuessDistribution { get; set; } = new int[6];
    }
}
