namespace WordleGameServer.Services
{
    public class Stats
    {
        public string? Word { get; set; }
        public int Players { get; set; }
        public int PlayersCorrect { get; set; }
        public int[] GuessDistribution { get; set; } = new int[6];
    }
}
