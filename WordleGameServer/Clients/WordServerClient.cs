// WordServerClient.cs  
// Anh Duc Vu, Jacob Wall, Jeong-Ah Yoon  
// March 14, 2025   
// Connects to the WordServer gRPC service to fetch and validate words.

using Grpc.Net.Client;
using WordServer.Protos;

namespace WordleGameServer.Clients
{
    public class WordServerClient
    {
        private static DailyWord.DailyWordClient? _wordServer = null;

        /// <summary>
        /// Gets today’s word from the WordServer.
        /// </summary>
        public static string GetWord()
        {
            ConnectToService();

            WordResponse? response = _wordServer?.GetWord(new WordRequest());

            return response?.Word ?? "";
        }

        /// <summary>
        /// Checks if guess word is valid by calling the WordServer ValidateWord.
        /// </summary>
        public static bool ValidateWord(string word)
        {
            ConnectToService();

            MatchResponse? reply = _wordServer?.ValidateWord(new MatchRequest { ClientWord = word });

            return reply!.Valid;
        }

        /// <summary>
        /// Establishes a connection to the WordServer if not already connected.
        /// </summary>
        private static void ConnectToService()
        {
            if (_wordServer is null)
            {
                var channel = GrpcChannel.ForAddress("https://localhost:7211");
                _wordServer = new DailyWord.DailyWordClient(channel);
            }

        }
    }
}
