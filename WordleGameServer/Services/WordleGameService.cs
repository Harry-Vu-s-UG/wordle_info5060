using Grpc.Core;
using Newtonsoft.Json;
using WordleGameServer.Clients;
using WordleGameServer.Protos;

namespace WordleGameServer.Services
{
    public class WordleGameService : DailyWordle.DailyWordleBase
    {
        private static DateTime? _date;

        //Harry: Should we handle this in the file? in case server dies and restart
        private static uint numPlayers = 0;
        private static uint playersWon = 0;
        private static int[] guessDistribution = new int[6];


        private const string _StatsFile = "daily_stats.json";
        private static Mutex mutex = new Mutex();

        public override async Task Play(IAsyncStreamReader<WordGuessRequest> requestStream, IServerStreamWriter<GuessResultResponse> responseStream, ServerCallContext context)
        {
            _date ??= DateTime.Now;
            if (_date.Value.Day != DateTime.Now.Day)
            {
                _date = DateTime.Now;
                numPlayers = 0;
                playersWon = 0;
                guessDistribution = new int[6];
            }
            /*
             if we keep this logic then to be safer to compare even in the different months?
            if (_date?.Date != DateTime.Today)
                {
                    _date = DateTime.Now;
                    numPlayers = 0;
                    playersWon = 0;
                    guessDistribution = new int[6];
                }
             */

            uint turnsUsed = 0;
            bool gameWon = false;
            LetterMatch[] results = new LetterMatch[5];

            SortedSet<string> included = new SortedSet<string>();
            SortedSet<string> available = new SortedSet<string>
            {
                "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
                "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z"
            };
            SortedSet<string> excluded = new SortedSet<string>();

            try
            {
                string wordToGuess = WordServerClient.GetWord();

                while (!gameWon && turnsUsed < 6 && await requestStream.MoveNext() && !context.CancellationToken.IsCancellationRequested)
                {
                    string wordPlayed = requestStream.Current.Guess;

                    GuessResultResponse response = new();
                    var error = WordServerClient.ValidateWord(wordPlayed);
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
                        playersWon++;
                        guessDistribution[turnsUsed - 1]++;
                        for (int i = 0; i < results.Length; i++)
                        {
                            results[i] = LetterMatch.Correct;
                        }
                    }
                    else
                    {
                        response = ValidWordResponse(response, wordPlayed, results, included, available, excluded, wordToGuess);
                    }

                    if (response.GameOver)
                    {
                          WriteStats(wordToGuess);
                        //WriteStats(wordToGuess, response, turnsUsed);

                    }
                    await responseStream.WriteAsync(response);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Error: serverside error");
            }
            finally 
            { 
                
            }

        }

        private void WriteStats(string dailyWord)
        {

            // lock the file with mutex
            mutex.WaitOne();
            try
            {
                Stats gameStats = new Stats();
                gameStats.Word = dailyWord;
                gameStats.Players = (int)(++numPlayers);
                gameStats.PlayersCorrect = (int)playersWon;
                gameStats.GuessDistribution = guessDistribution;

                string json = JsonConvert.SerializeObject(gameStats, Formatting.Indented);

                File.WriteAllText(_StatsFile, json);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }


        // suggestion about static variable part -- int[] guessDistribution has to be moved to Play() and setup the variable which stores the right number to add
        private void WriteStats(string wordToguess ,GuessResultResponse response,uint turnsUsed)
        {
            mutex.WaitOne();
            try
            {
                string dateString = DateTime.Now.ToString("yyyyMMdd");
                Stats stats;
                if (!File.Exists($"{dateString}.json"))
                {
                    stats = new Stats();
                }
                else
                {
                    var json = File.ReadAllText($"{dateString}.json");
                    stats = JsonConvert.DeserializeObject<Stats>(json)?? new Stats();
                    
                }
                stats.Players++;
                if (response.GuessCorrect)
                {
                    stats.PlayersCorrect++;
                    stats.GuessDistribution[turnsUsed - 1]++;
                }
                stats.Word = wordToguess;
                string jsonString = JsonConvert.SerializeObject(stats);
                File.WriteAllText($"{dateString}.json", jsonString);
           
            }
            catch (Exception)
            {

            }
            
            finally
            { 
                mutex?.ReleaseMutex();
            }
        }


        public override Task<StatsResponse> GetStats(StatsRequest request, ServerCallContext context)
        {
            // create response object
            StatsResponse response = new StatsResponse();

            // lock the file with mutex
            mutex.WaitOne();
            try
            {
                if (File.Exists(_StatsFile))
                {
                    string json = File.ReadAllText(_StatsFile);

                    Stats stats = JsonConvert.DeserializeObject<Stats>(json)!;
                    response.DailyWord = stats.Word;
                    response.Players = stats.Players;
                    response.WinPercentage = (int)(((double)stats.PlayersCorrect / stats.Players) * 100);
                    int totalGuesses = 0;
                    for (int i = 0; i < stats.GuessDistribution.Length; i++)
                    {
                        totalGuesses += stats.GuessDistribution[i] * (i + 1);
                    }
                    response.GuessAverage = stats.PlayersCorrect > 0 ? (float)((double)totalGuesses / stats.PlayersCorrect) : 0;

                }
                else
                {
                    throw new FileNotFoundException($"File {_StatsFile} not found!");
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }

            return Task.FromResult(response);
        }

        //Harry!!!!!!!!!!!!: if we decide to change the static variable 

        //public override Task<StatsResponse> GetStats(StatsRequest request, ServerCallContext context)
        //{

        //    StatsResponse response = new StatsResponse();

        //    string dateString = DateTime.Now.ToString("yyyyMMdd");
        //    string filename = $"{dateString}.json";

        //    mutex.WaitOne();
        //    try
        //    {
        //        if (File.Exists(filename))
        //        {
        //            string json = File.ReadAllText(filename);

        //            Stats stats = JsonConvert.DeserializeObject<Stats>(json)!;
        //            response.DailyWord = stats.Word;
        //            response.Players = stats.Players;
        //            response.WinPercentage = (int)(((double)stats.PlayersCorrect / stats.Players) * 100);
        //            int totalGuesses = 0;
        //            for (int i = 0; i < stats.GuessDistribution.Length; i++)
        //            {
        //                totalGuesses += stats.GuessDistribution[i] * (i + 1);
        //            }
        //            response.GuessAverage = stats.PlayersCorrect > 0 ? (float)((double)totalGuesses / stats.PlayersCorrect) : 0;

        //        }
        //        else
        //        {
        //            throw new FileNotFoundException($"File {filename} not found!");
        //        }
        //    }
        //    finally
        //    {
        //        mutex?.ReleaseMutex();
        //    }

        //    return Task.FromResult(response);
        //}

        private GuessResultResponse ValidWordResponse(GuessResultResponse res, string wordPlayed, LetterMatch[] results, SortedSet<string> included, SortedSet<string> available, SortedSet<string> excluded, string wordToGuess)
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

