// WordleGameService.cs  
// Anh Duc Vu, Jacob Wall, Jeong-Ah Yoon  
// March 31, 2025  
// Handles Wordle gameplay and daily stats using gRPC streaming.
// Validates guesses, sends results, and saves stats to disk.

using Grpc.Core;
using Newtonsoft.Json;
using WordleGameServer.Clients;
using WordleGameServer.Protos;

namespace WordleGameServer.Services
{
    public class WordleGameService : DailyWordle.DailyWordleBase
    {
        private static DateTime? _currentGameDate;

        // Stats for the current day
        private static Dictionary<string, Stats> _dailyStats = new Dictionary<string, Stats>();
        private static Mutex mutex = new Mutex();
        private static bool _statsLoaded = false;
        // Track player sessions by client ID
        private static Dictionary<string, DateTime> _playerGameDates = new Dictionary<string, DateTime>();

        // Constructor to load stats on startup
        
        /// <summary>
        /// Constructor that ensures today's game stats are loaded once when the service starts.
        /// </summary>
        public WordleGameService()
        {
            // Load stats from disk only once when service starts
            if (!_statsLoaded)
            {
                    LoadTodayStats();
                    _statsLoaded = true;
            }
        }

        /// <summary>
        /// Returns the stats filename for the given date.
        /// </summary>
        private string GetStatsFileName(DateTime date)
        {
            return $"stats_{date.ToString("yyyyMMdd")}.json";
        }

        /// <summary>
        /// Loads stats for today's date and sets the current game date.
        /// </summary>
        private void LoadTodayStats()
        {
            DateTime today = DateTime.Today;
            LoadStatsForDate(today);
            _currentGameDate = today;
        }

        /// <summary>
        /// Loads stats for the given date from disk, or creates new stats if needed.
        /// Ensures the word matches the one expected for that date.
        /// </summary>
        private void LoadStatsForDate(DateTime date)
        {
            string statsFileName = GetStatsFileName(date);
            string dateKey = date.ToString("yyyyMMdd");

            mutex.WaitOne();
            try
            {
                if (!_dailyStats.ContainsKey(dateKey))
                {
                    if (File.Exists(statsFileName))
                    {
                        string json = File.ReadAllText(statsFileName);
                        Stats stats = JsonConvert.DeserializeObject<Stats>(json)!;

                        // Verify that the loaded stats are for the expected word
                        string expectedWord = GetWordForDate(date);
                        if (stats.Word == expectedWord)
                        {
                            _dailyStats[dateKey] = stats;
                            Console.WriteLine($"Loaded existing stats for {dateKey} from disk");
                        }
                        else
                        {
                            // Word doesn't match, create fresh stats
                            _dailyStats[dateKey] = CreateNewStats(expectedWord);
                            Console.WriteLine($"Word mismatch for {dateKey}, created fresh stats");
                        }
                    }
                    else
                    {
                        // No stats file exists yet
                        string wordForDate = GetWordForDate(date);
                        _dailyStats[dateKey] = CreateNewStats(wordForDate);
                        Console.WriteLine($"No stats file found for {dateKey}, created fresh stats");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading stats for {dateKey}: {ex.Message}");
                _dailyStats[dateKey] = CreateNewStats(GetWordForDate(date));
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Creates a new Stats object with default values for a given word.
        /// </summary>
        private Stats CreateNewStats(string word)
        {
            return new Stats
            {
                Word = word,
                Players = 0,
                PlayersCorrect = 0,
                GuessDistribution = new int[6]
            };
        }

        /// <summary>
        /// Returns the word for the given date, using saved stats if available.
        /// </summary>
        private string GetWordForDate(DateTime date)
        {
            if (date.Date == DateTime.Today)
            {
                return WordServerClient.GetWord();
            }

            string statsFileName = GetStatsFileName(date);
            if (File.Exists(statsFileName))
            {
                try
                {
                    string json = File.ReadAllText(statsFileName);
                    Stats stats = JsonConvert.DeserializeObject<Stats>(json)!;
                    return stats.Word;
                }
                catch
                {
                    return WordServerClient.GetWord();
                }
            }

            return WordServerClient.GetWord();
        }

        /// <summary>
        /// Handles the main Wordle gameplay using bidirectional streaming.
        /// Validates guesses, updates stats, and sends feedback to the client.
        /// </summary>
        public override async Task Play(IAsyncStreamReader<WordGuessRequest> requestStream, IServerStreamWriter<GuessResultResponse> responseStream, ServerCallContext context)
        {
            // Store the date when the game starts
            DateTime gameStartDate = DateTime.Today;
            string gameStartDateKey = gameStartDate.ToString("yyyyMMdd");
            
            // Peer id as key
            string clientId = context.Peer;
            _playerGameDates[clientId] = gameStartDate;

            LoadStatsForDate(gameStartDate);

            // Game variables
            uint turnsUsed = 0;
            bool gameWon = false;
            LetterMatch[] results = new LetterMatch[5];
            Array.Fill(results, LetterMatch.Incorrect);

            SortedSet<string> included = new SortedSet<string>();
            SortedSet<string> available = new SortedSet<string>
            {
                "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
                "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z"
            };
            SortedSet<string> excluded = new SortedSet<string>();

            try
            {
                string wordToGuess = GetWordForDate(gameStartDate);

                while (!gameWon && turnsUsed < 6 && await requestStream.MoveNext() && !context.CancellationToken.IsCancellationRequested)
                {
                    string wordPlayed = requestStream.Current.Guess;

                    GuessResultResponse response = new();
                    if (!WordServerClient.ValidateWord(wordPlayed))
                    {
                        response.GuessValid = false;
                        await responseStream.WriteAsync(response);
                        continue;
                    }

                    response.GuessValid = true;
                    turnsUsed++;

                    if (turnsUsed == 6)
                    {
                        response.GameOver = true;
                    }

                    // Check if the guessed word is an exact match
                    if (wordPlayed == wordToGuess)
                    {
                        gameWon = true;
                        response.GuessCorrect = true;
                        response.GameOver = true;

                        for (int i = 0; i < results.Length; i++)
                        {
                            results[i] = LetterMatch.Correct;
                            response.Word.Add(new Letter { Character = wordPlayed[i].ToString(), Match = LetterMatch.Correct });
                        }
                    }
                    else
                    {
                        response = ValidWordResponse(response, wordPlayed, results, included, available, excluded, wordToGuess);
                    }

                    if (response.GameOver)
                    {
                        WriteStats(gameStartDateKey, wordToGuess, response, turnsUsed);
                    }
                    await responseStream.WriteAsync(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: serverside error - {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the daily stats and saves them to disk after each completed game.
        /// </summary>
        private void WriteStats(string dateKey, string wordToGuess, GuessResultResponse response, uint turnsUsed)
        {
            mutex.WaitOne();
            try
            {
                // Make sure stats for this date are loaded
                if (!_dailyStats.ContainsKey(dateKey))
                {
                    DateTime date = DateTime.ParseExact(dateKey, "yyyyMMdd", null);
                    LoadStatsForDate(date);
                }

                Stats stats = _dailyStats[dateKey];
                stats.Players++;

                if (response.GuessCorrect)
                {
                    stats.PlayersCorrect++;
                    stats.GuessDistribution[turnsUsed - 1]++;
                }

                stats.Word = wordToGuess;

                // Write the updated stats to disk
                string statsFileName = $"stats_{dateKey}.json";
                string json = JsonConvert.SerializeObject(stats, Formatting.Indented);
                File.WriteAllText(statsFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing stats: {ex.Message}");
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Returns game stats for the current or previously recorded date for the client.
        /// </summary>
        public override Task<StatsResponse> GetStats(StatsRequest request, ServerCallContext context)
        {
            // Get the client ID
            string clientId = context.Peer;
            
            // Default to today's stats if we can't find the player's game date
            DateTime targetDate = DateTime.Today;
            
            // Check if this player has a recorded game date
            if (_playerGameDates.ContainsKey(clientId))
            {
                targetDate = _playerGameDates[clientId];
            }
            
            string dateKey = targetDate.ToString("yyyyMMdd");

            LoadStatsForDate(targetDate);

            // Create response object
            StatsResponse response = new StatsResponse();

            mutex.WaitOne();
            try
            {
                if (_dailyStats.ContainsKey(dateKey))
                {
                    Stats stats = _dailyStats[dateKey];
                    response.DailyWord = stats.Word;
                    response.Players = stats.Players;
                    response.WinPercentage = stats.Players > 0
                        ? (int)(((double)stats.PlayersCorrect / stats.Players) * 100)
                        : 0;

                    int totalGuesses = 0;
                    for (int i = 0; i < stats.GuessDistribution.Length; i++)
                    {
                        totalGuesses += stats.GuessDistribution[i] * (i + 1);
                    }
                    response.GuessAverage = stats.PlayersCorrect > 0
                        ? (float)((double)totalGuesses / stats.PlayersCorrect)
                        : 0;
                }
                else
                {
                    throw new FileNotFoundException($"Stats for {dateKey} not found!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting stats: {ex.Message}");
                // Return empty response with default values
                response.DailyWord = GetWordForDate(targetDate);
                response.Players = 0;
                response.WinPercentage = 0;
                response.GuessAverage = 0;
            }
            finally
            {
                mutex.ReleaseMutex();
            }

            return Task.FromResult(response);
        }

        /// <summary>
        /// Compares the guessed word to the correct word and returns match results per letter.
        /// Write the code from the given suedo code
        /// Populates included, excluded, and available letters for feedback.
        /// </summary>
        private GuessResultResponse ValidWordResponse(GuessResultResponse res, string wordPlayed, LetterMatch[] results, 
            SortedSet<string> included, SortedSet<string> available, SortedSet<string> excluded, string wordToGuess)
        {
            GuessResultResponse response = res;

                    Dictionary<char, int> matches = new Dictionary<char, int> {
                        {'a', 0}, {'b', 0}, {'c', 0}, {'d', 0}, {'e', 0},
                        {'f', 0}, {'g', 0}, {'h', 0}, {'i', 0}, {'j', 0},
                        {'k', 0}, {'l', 0}, {'m', 0}, {'n', 0}, {'o', 0},
                        {'p', 0}, {'q', 0}, {'r', 0}, {'s', 0}, {'t', 0},
                        {'u', 0}, {'v', 0}, {'w', 0}, {'x', 0}, {'y', 0}, {'z', 0}
                    };
                    // search wordPlayed for letters that in in the correct position
                    for (int i = 0; i < results.Length; i++)
                    {
                        char letter = wordPlayed[i];
                        if (letter == wordToGuess[i])
                        {
                            results[i] = LetterMatch.Correct;
                            matches[letter] = matches[letter]++;
                            included.Add(letter.ToString());
                            available.Remove(letter.ToString());
                        }
                    }

                    // search wordPlayed for additional correct letters that are
                    // not in the correct position
                    for (int i = 0; i < results.Length; i++)
                    {
                        char letter = wordPlayed[i];
                        if (!wordToGuess.Contains(letter))
                        {
                            results[i] = LetterMatch.Incorrect;
                            excluded.Add(letter.ToString());
                            available.Remove(letter.ToString());
                        }
                        else if (letter != wordToGuess[i])
                        {
                            if (matches[letter] < wordToGuess.Count(e => e == letter))
                            {
                                results[i] = LetterMatch.WrongPos;
                                matches[letter]++;
                                included.Add(letter.ToString());
                                available.Remove(letter.ToString());
                            }
                        }
                    }

                // set each match result for each letter in response
                for (int i = 0; i < results.Length; i++)
                {
                    response.Word.Add(new Letter { Character = wordPlayed[i].ToString(), Match = results[i] });
                }

                // set each included letter
                response.Included.AddRange(included);
                // set each excluded letter
                response.Exluded.AddRange(excluded);
                // set each available letter
                response.Available.AddRange(available);

            return response;
        }
    }
}

