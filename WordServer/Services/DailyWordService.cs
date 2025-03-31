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
        private static DateTime _currentDate = DateTime.Today;

        public override Task<WordResponse> GetWord(WordRequest request, ServerCallContext context)
        {
            var response = new WordResponse();

            try
            {
                if (_wordleWords == null)
                {
                    ParseWordleFile();
                }

                // Check if date changed to a new day
                if (_currentDate != DateTime.Today)
                {
                    _currentDate = DateTime.Today;
                }

                // Use deterministic approach based on today's date
                int todayIndex = GetDeterministicIndex(DateTime.Today);
                _currWordIndex = todayIndex;

                response.Word = _wordleWords![_currWordIndex];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
            return Task.FromResult(response);
            //return Task.FromResult(new WordResponse { Word = "croak" }); // for test
        }

        private int GetDeterministicIndex(DateTime date)
        {
            // Create a deterministic seed based on the date
            int seed = date.Year * 10000 + date.Month * 100 + date.Day;
            Random seededRandom = new Random(seed);

            // Get today's index
            int todayIndex = seededRandom.Next(0, _wordleWords!.Length);

            // Get yesterday's index to avoid repeat words
            int yesterdayIndex = -1;
            if (date > DateTime.MinValue)
            {
                DateTime yesterday = date.AddDays(-1);
                int yesterdaySeed = yesterday.Year * 10000 + yesterday.Month * 100 + yesterday.Day;
                yesterdayIndex = new Random(yesterdaySeed).Next(0, _wordleWords!.Length);
            }

            // If today's word would be the same as yesterday's, increment by 1
            if (todayIndex == yesterdayIndex && yesterdayIndex >= 0)
            {
                todayIndex = (todayIndex + 1) % _wordleWords!.Length;
            }

            return todayIndex;
        }

        public override Task<MatchResponse> ValidateWord(MatchRequest request, ServerCallContext context)
        {
            var response = new MatchResponse();

            try
            {
                if (_wordleWords == null)
                {
                    ParseWordleFile();
                }

                if (_wordleWords == null || _wordleWords.Length == 0)
                {
                    throw new Exception($"ERROR: WordFile has a problem");
                }
                response.Valid = _wordleWords.Contains(request.ClientWord.ToLower());

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                response.Valid = false;
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
