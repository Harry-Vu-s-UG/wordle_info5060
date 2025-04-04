// WordleGameClient 
// Anh Duc Vu, Jacob Wall, Jeong-Ah Yoon  
// March 14, 2025  
// Connects to the Wordle game server using gRPC.  
// Handles user input and displays feedback in the console.  
// Plays one game per session and shows daily stats.

using Grpc.Net.Client;
using Grpc.Core;
using WordleGameServer.Protos;
using System.Threading.Channels;

namespace WordleGameClient
{
    public class Program
    {
        /// <summary>
        /// Runs the main game loop for the Wordle client.
        /// Connects to the server, sends guesses, and displays results.
        /// Shows daily stats and prompts to play again.
        /// </summary>
        static async Task Main(string[] args)
        {
            // initiate connection with server
            var channel = GrpcChannel.ForAddress("https://localhost:7097");
            var game = new DailyWordle.DailyWordleClient(channel);

            bool newGame = true;

            while (newGame)
            {

                // game start from server
                using (var call = game.Play())
                {
                    // check if the worldle server is up
                    CheckConnection(channel);

                    // attempt to get stats just to check if the wordle game server
                    // has access to the word server (is the word server online?)
                    try
                    {
                        game.GetStats(new StatsRequest());
                    }
                    catch (RpcException ex)
                    {
                        Console.Clear();
                        Console.Write("Error: " + ex.Status.Detail);
                        Environment.Exit(1);
                    }

                    // print the wordled title
                    Console.Clear();
                    PrintTitle();

                    bool gameOver = false;

                    int guessIteration = 1;
                    do
                    {
                        // build request object
                        WordGuessRequest request = new WordGuessRequest();

                        // get user guess and populate request
                        request.Guess = GetGuess(guessIteration);

                        try
                        {
                            // send request to server
                            await call.RequestStream.WriteAsync(request);

                            // get response from server
                            await call.ResponseStream.MoveNext();
                        }
                        catch (RpcException e)
                        {
                            Console.WriteLine(e.Message);
                            Environment.Exit(1);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Server Error: {e.Message}");
                            Environment.Exit(1);
                        }
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
                StatsResponse statsResponse = new StatsResponse();
                try
                {
                    // print daily statistics from server
                    statsResponse = game.GetStats(new StatsRequest());
                }
                catch (RpcException e)
                {
                    Console.WriteLine(e.Message);
                    Environment.Exit(1);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Server Error: {e.Message}");
                    Environment.Exit(1);
                }

                Console.WriteLine("\nStatistics");
                Console.WriteLine("----------\n");
                Console.WriteLine($"Today's Word:\t\t{statsResponse.DailyWord}");
                Console.WriteLine($"Players:\t\t{statsResponse.Players}");
                Console.WriteLine($"Winners:\t\t{statsResponse.WinPercentage}%");
                Console.WriteLine($"Average Guesses:\t{statsResponse.GuessAverage:F1}\n");


                // prompt user to play again as a new player
                Console.Write("Play game as new user? [Y/N]: ");
                newGame = GetYesNoDecision();
            }

        }

        /// <summary>
        /// Checks the connection between the client and the wordle game server
        /// </summary>
        /// <param name="channel">the Grpc channel for the game server</param>
        static void CheckConnection(GrpcChannel channel)
        {
            // check if the client has successfully connected to the server
            int connectAttempt = 0;
            string connectText = "Connecting to wordle server";
            Console.Write(connectText);
            while (channel.State != ConnectivityState.Ready)
            {


                if (++connectAttempt > 5)
                {
                    Console.Clear();
                    Console.Write("Unable to connect to wordle server.");
                    Environment.Exit(0);
                }
                else
                {
                    // simple animated loading
                    for (int i = 0; i < 3; i++)
                    {
                        Thread.Sleep(500);
                        Console.Write(".");
                    }
                    Thread.Sleep(500);
                    // moves the cursor to the three dots
                    Console.SetCursorPosition(connectText.Length, Console.CursorTop);
                    // removes the 3 dots
                    Console.Write("   ");
                    Console.SetCursorPosition(connectText.Length, Console.CursorTop);
                }
            }
        }

        /// <summary>
        /// Used to get and validate a yes or no decision from the user
        /// </summary>
        /// <returns>true if yes, false if no</returns>
        static bool GetYesNoDecision()
        {
            bool validInput = false;
            char input;
            while (!validInput)
            {
                input = Console.ReadKey().KeyChar;
                Console.WriteLine();
                switch (Char.ToLower(input))
                {
                    case 'y':
                        return true;
                    case 'n':
                        return false;
                    default:
                        Console.WriteLine("Unknown input, try again.");
                        validInput = false;
                        continue;
                }
            }
            return false;
        }

        /// <summary>
        /// Prints the game title and instructions to the console.
        /// </summary>
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

        /// <summary>
        /// Prompts the user to enter word guess for the current turn.
        /// </summary>
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
