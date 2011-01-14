using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace BoggleSolver
{
    class Guess
    {
        private int _x;
        private int _y;
        private int _dir;
        private int[] _charIndexes;

        public Guess(int x, int y, int dir, int len, int[] charIndexes)
        {
            _x = x;
            _y = y;
            _dir = dir;
            Len = len;
            _charIndexes = charIndexes;
        }

        public int LineNum { get; set; }
        public int GameNum { get; set; }
        public string Word { get; set; }
        public int Len { get; set; }
 
        public string ReadWord(string board)
        {
            StringBuilder builder = new StringBuilder();

            foreach (int i in _charIndexes)
            {
                builder.Append(board[i]);
            }
            Word = builder.ToString();

            return Word;
        }

        public string Report()
        {
            return String.Format("{0} {1} {2} {3} {4} {5}", Word, LineNum, Len, _x, _y, _dir);
        }
    }

    class Board
    {
        public Board(string board, int num)
        {
            Grid = board;
            GameNum = num;
        }

        public string Grid { get; set; }
        public int GameNum { get; set; }
    }

    class Games
    {
        List<Board> _boardList;
        enum Dir { First = 1, North = 1, NorthEast = 2, East = 3, SouthEast = 4, South = 5,
            SouthWest = 6, West = 7, NorthWest = 8, Last=9 };
        const int LastY = 5;
        const int LastX = 5;
        WordList _wordList;
        const int InvalidGameNum = 0;

        public Games(string[] lines, WordList wordList)
        {
            int gameNum = 1;

            _boardList = new List<Board>();
            foreach (string game in lines)
            {
                if (game != game.ToUpper())
                    throw new FormatException("Error with board: " + game);

                _boardList.Add(new Board(game, gameNum));
                gameNum++;
            }
            _wordList = wordList;
        }

        bool IsInRange(int x, int y)
        {
            return x >= 0 && x < LastX && y >= 0 && y < LastY;
        }

        IEnumerable<Guess> Permute()
        {
            for (int y = 0; y < LastY; y++)
            {
                for (int x = 0; x < LastX; x++)
                {
                    for (Dir dir = Dir.First; dir < Dir.Last; dir++)
                    {
                        List<int> charIndexes = new List<int>();
                        int nextX = x;
                        int nextY = y;

                        for (int len = 1; len <= 5; len++)
                        {
                            charIndexes.Add(nextX + nextY*LastY);
                            yield return new Guess(x, y, (int)dir, len, charIndexes.ToArray());
                            if (!MoveToNextGuess(dir, ref nextX, ref nextY))
                                break;
                        }
                    }
                }
            }
        }

        private bool MoveToNextGuess(Dir dir, ref int nextX, ref int nextY)
        {
            switch (dir)
            {
                case Dir.North:
                    nextY--;
                    break;
                case Dir.NorthEast:
                    nextY--;
                    nextX++;
                    break;
                case Dir.East:
                    nextX++;
                    break;
                case Dir.SouthEast:
                    nextX++;
                    nextY++;
                    break;
                case Dir.South:
                    nextY++;
                    break;
                case Dir.SouthWest:
                    nextX--;
                    nextY++;
                    break;
                case Dir.West:
                    nextX--;
                    break;
                case Dir.NorthWest:
                    nextX--;
                    nextY--;
                    break;
                default:
                    throw new ArgumentException();
            }
            return IsInRange(nextX, nextY);
        }

        public void ScoreGames()
        {
            IEnumerable<Guess> validWords = from game in _boardList.AsParallel() orderby game.GameNum
                                       from guess in Permute().AsParallel()
                                       where _wordList.TestAndSetWord(guess, game)
                                       select guess;

            int totalScore = 0;
            int lastGameNum = InvalidGameNum;
            int gameScore = 0;

            foreach (Guess guess in validWords)
            {
                if (lastGameNum != guess.GameNum)
                {
                    if (lastGameNum != InvalidGameNum)
                    {
                        Console.WriteLine("Total Game Points = {0}", gameScore);
                    }
                    Console.WriteLine("Game {0}", guess.GameNum);
                    lastGameNum = guess.GameNum;
                    gameScore = 0;
                }
                Console.WriteLine(guess.Report());
                gameScore += guess.Len;
                totalScore += guess.Len;
            }
            Console.Write("Games Totals Points = {0} ", totalScore);
        }
    }

    class WordList
    {
        static short[] _lineInfo = new short[26 * 26 * 26 * 26 * 26 * 26 + 1];

        public WordList(string[] lines)
        {
            short lineNum = 1;

            foreach (string word in lines)
            {
                if (word != word.ToUpper())
                    throw new FormatException("Error with dictionary: " + word);

                int hashCode = Hash(word);

                _lineInfo[hashCode] = lineNum;
                lineNum++;
            }
        }

        public bool TestAndSetWord(Guess word, Board board)
        {
            int hashCode = Hash(word.ReadWord(board.Grid));
 
            word.LineNum = _lineInfo[hashCode];
            word.GameNum = board.GameNum;

            return word.LineNum != 0;
        }

        int Hash(string word)
        {
            int result = 0;

            foreach (char c in word)
            {
                result = result * 26 + Convert.ToInt32(c) - Convert.ToInt32('A') + 1;
            }
            return result;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string[] gameLines = File.ReadAllLines("games.txt");
            string[] words = File.ReadAllLines("words5.txt");

            Stopwatch stopwatch = new Stopwatch();
            const int TIMING_REPETITIONS = 10;
            double averageTime = 0.0;

            for (int i = 0; i < TIMING_REPETITIONS; ++i)
            {
                stopwatch.Reset();
                stopwatch.Start();

                WordList wordList = new WordList(words);
                Games games = new Games(gameLines, wordList);
                games.ScoreGames();

                stopwatch.Stop();
                averageTime += stopwatch.Elapsed.TotalSeconds;
                GC.Collect();
            }
            averageTime /= (double)TIMING_REPETITIONS;
            Console.WriteLine(string.Format("Total Average Time = {0:0.000000} sec", averageTime));

            Console.ReadLine();
        }
    }
}

  