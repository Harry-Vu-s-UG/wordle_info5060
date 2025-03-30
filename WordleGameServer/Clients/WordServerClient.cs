using Grpc.Net.Client;
using WordServer.Protos;

namespace WordleGameServer.Clients
{
    //Harry: choose one of these
    public class WordServerClient
    {
        private static DailyWord.DailyWordClient? _wordServer = null;

        /*
        public static string GetWord()
        {
            ConnectToService();

            if (_wordServer == null)
            {
                throw new Exception("Error: Something wrong in server. No Service Sorry");
            }

            WordResponse response = _wordServer.GetWord(new WordRequest());

            return response.Word;
        }
        */

        public static string GetWord()
        {
            ConnectToService();

            WordResponse? response = _wordServer?.GetWord(new WordRequest());

            return response?.Word ?? "";
        }
        /*


        public static bool ValidateWord(string word)
        {
            ConnectToService();

            if (_wordServer == null)
            {
                throw new Exception("Error: Something wrong in server. No Service Sorry");
            }

            MatchResponse reply = _wordServer.ValidateWord(new MatchRequest { ClientWord = word });

            return reply.Valid;
        }

        */
        public static bool ValidateWord(string word)
        {
            ConnectToService();

            MatchResponse? reply = _wordServer?.ValidateWord(new MatchRequest { ClientWord = word });

            return reply!.Valid;
        }

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
