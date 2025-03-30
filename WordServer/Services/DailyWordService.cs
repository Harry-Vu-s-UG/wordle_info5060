using Grpc.Core;
using System.Text.Json;
using WordServer.Protos;

namespace WordServer.Services
{
    public class DailyWordService : DailyWord.DailyWordBase
    {
        private static string[]? _wordleWords;
        private static int _currWordIndex;
        private const string wordleFile = "wordle.json";

        private static DateTime? _date;

        public override Task<WordResponse> GetWord(WordRequest request, ServerCallContext context)
        {
            Random rand = new Random();

            // Harry: What if server dies? Then today's word would change within the same day?
            // What is we inject seed to make sure that the random number is always the same 
            /* 
              
            int index = new Random(DateTime.Today.DayOfYear).Next(_wordleWords.Length);
            int yesterdayIndex = new Random(DateTime.Today.AddDays(-1).DayOfYear).Next(_wordleWords.Length);
            if (index == yesterdayIndex)
                {
                    index = (index + 1) % _wordleWords.Length;
                }

            */
            //If this is the better idea would you clean the code accordingly?

            var response = new WordResponse();
            try
            {

                if (_wordleWords == null)
            {

                ParseWordleFile();
               
                _currWordIndex = rand.Next(0, _wordleWords!.Length);

              
                response.Word = _wordleWords![_currWordIndex];
            }

                _date ??= DateTime.Now;

                if (_date.Value.Day != DateTime.Now.Day)
                {
                    _date = DateTime.Now;
                    int newIndex;
                    do
                    {
                        newIndex = rand.Next(0, _wordleWords!.Length);
                    } while (newIndex == _currWordIndex);
                    _currWordIndex = newIndex;
                }

                response.Word = _wordleWords[_currWordIndex];

            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
           // return Task.FromResult(new WordResponse { Word = "spoon" }); --for test
            return Task.FromResult(response);
        }

        public override Task<MatchResponse> ValidateWord(MatchRequest request, ServerCallContext context)
        {
            var response = new MatchResponse();

            //// try-catch added in case json parse error or IOexception when reading file
            try
            {
               
                if (_wordleWords == null)
                {
                    ParseWordleFile();
                }

                if (_wordleWords == null || _wordleWords.Length == 0)
                {
                    throw new Exception($"ERROR: WordFIle has a problem");
                }
                response.Valid = _wordleWords.Contains(request.ClientWord.ToLower());
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                response.Valid = false;    //// Check if this logic is okay
            }

            return Task.FromResult(response);
        }

        public void ParseWordleFile()
        {

            if (File.Exists(wordleFile))
            {
                string json = File.ReadAllText(wordleFile);
                _wordleWords = JsonSerializer.Deserialize<string[]>(json)!;
                Console.WriteLine("Wordle data file has been read and stored!");
            }
            else
            {
                throw new FileNotFoundException($"File {wordleFile} not found!");
            }

        }
    }
}
