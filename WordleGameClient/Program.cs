using Grpc.Net.Client;
using Grpc.Core;
using WordleGameServer.Protos;

namespace WordleGameClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // initiate connection with server
            var channel = GrpcChannel.ForAddress("https://localhost:7097");
            var game = new DailyWordle.DailyWordleClient(channel);

            bool newGame = true;
            while (newGame)
            {

                PrintTitle();

                // game start from server
                using (var call = game.Play())
                {
                    bool gameOver = false;

                    int guessIteration = 1;
                    do
                    {
                        // build request object
                        WordGuessRequest request = new WordGuessRequest();

                        // get user guess and populate request
                        request.Guess = GetGuess(guessIteration);

                        // send request to server
                        await call.RequestStream.WriteAsync(request);

                        // get response from server
                        await call.ResponseStream.MoveNext();
                        GuessResultResponse response = call.ResponseStream.Current;

                        // if the response is not valid, stop and send another request
                        if (!response.GuessValid)
                        {
                            Console.WriteLine("Word entered is not valid. try again.");
                            continue;
                        }

                        // print out the result from the server
                        Console.Write(new string(' ', 5));
                        foreach (Letter letter in response.Word)
                        {
                            switch (letter.Match)
                            {
                                case LetterMatch.Incorrect:
                                    Console.Write("X");
                                    break;
                                case LetterMatch.WrongPos:
                                    Console.Write("?");
                                    break;
                                case LetterMatch.Correct:
                                    Console.Write("*");
                                    break;
                            }
                        }
                        Console.WriteLine();
                        guessIteration++;

                        // is the game over?
                        gameOver = response.GameOver;

                        if (response.GuessCorrect)
                        {
                            Console.WriteLine("You win!");
                        }
                        else if (response.GameOver)
                        {
                            Console.WriteLine("\nYou lose!");
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.Write($"{"",-5}Included:  {string.Join(",", response.Included)}\n");
                            Console.Write($"{"",-5}Available: {string.Join(",", response.Available)}\n");
                            Console.Write($"{"",-5}Excluded:  {string.Join(",", response.Exluded)}\n");
                            Console.WriteLine();
                        }

                    }
                    while (!gameOver);
                }
                // print daily statistics from server
                StatsResponse statsResponse = game.GetStats(new StatsRequest());
                Console.WriteLine("\nStatistics");
                Console.WriteLine("----------\n");
                Console.WriteLine($"Today's Word:\t\t{statsResponse.DailyWord}");
                Console.WriteLine($"Players:\t\t{statsResponse.Players}");
                Console.WriteLine($"Winners:\t\t{statsResponse.WinPercentage}%");
                Console.WriteLine($"Average Guesses:\t{statsResponse.GuessAverage}\n");


                // prompt user to play again as a new player
                Console.Write("Play game as new user? [Y/N]: ");
                bool validInput = false;
                char input;
                while (!validInput)
                {
                    input = Console.ReadKey().KeyChar;
                    Console.WriteLine();
                    switch (Char.ToLower(input))
                    {
                        case 'y':
                            newGame = true;
                            validInput = true;
                            break;
                        case 'n':
                            newGame = false;
                            validInput = true;
                            break;
                        default:
                            Console.WriteLine("Unknown input, try again.");
                            continue;
                    }
                }
            }

        }
        static void PrintTitle()
        {
            Console.WriteLine("+------------------+");
            Console.WriteLine("|  W O R D L E D  |");
            Console.WriteLine("+------------------+\n");
            Console.WriteLine("You have 6 chances to guess a 5-letter word.");
            Console.WriteLine("Each guess must be a 'playable' 5 letter word.");
            Console.WriteLine("After a guess the game will display a series of");
            Console.WriteLine("characters to show you how good your guess was.\n");
            Console.WriteLine("x - means the letter is not in the word.");
            Console.WriteLine("? - means the letter should be in another spot.");
            Console.WriteLine("* - means the letter is correct in this spot.\n");
        }
        static string GetGuess(int iteration)
        {
            bool valid = false;
            string input = "";
            do
            {
                Console.Write($"({iteration}): ");

                try
                {
                    input = (Console.ReadLine() ?? "").Trim().ToLower();

                    if (input.Length != 5)
                    {
                        Console.WriteLine($"Please enter a word that is 5 letters in length.");
                    }
                    else
                    {
                        valid = true;
                    }
                }
                catch (FormatException)
                {
                    Console.WriteLine("Your answer must be an word. Please try again.");
                }
            } while (!valid);

            return input;
        }
    }
}
