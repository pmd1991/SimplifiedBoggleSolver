using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace BoggleSolver
{
    /// <summary>
    /// describes locations on grid of possible word
    /// </summary>
    sealed class Guess
    {
        public byte X { get; set; }
        public byte Y { get; set; }
        public byte Dir { get; set; }
        public byte[] CharIndexes { get; set; }
        public short Index { get; set; }

        public Guess(byte x, byte y, byte dir, byte[] charIndexes, short index)
        {
            X = x;
            Y = y;
            Dir = dir;
            CharIndexes = charIndexes;
            Index = index;
        }

        public override string ToString()
        {
            return string.Format("x={0,2} y={1,2} dir={2,2} dir_max={3,2}", X, Y, Dir, CharIndexes.Length);
        }
    }

    /// <summary>
    /// set of 25 letters and operations on them
    /// </summary>
    sealed class Grid
    {
        class ValidWords
        {
            public short LineNum { get; set; }
            public string Word { get; set; }
            public Guess Guess { get; set; }
            public byte Score { get; set; }

            public ValidWords(short lineNum, string word, Guess guess, byte score)
            {
                LineNum = lineNum;
                Word = word;
                Guess = guess;
                Score = score;
            }
        }
        string Letter { get; set; }
        int GameNum { get; set; }
        ValidWords[] validWords;

        public Grid(string letter, int num)
        {
            Letter = letter;
            GameNum = num;
        }

        public void Score(Guess[] guesses, WordList validList)
        {
            validWords = new ValidWords[guesses.Length * Solver.MaxWordLen];
            StringBuilder builder = new StringBuilder();

            foreach (Guess guess in guesses)
            {
                bool afterQ = false;
                builder.Clear();
                int current = 0;
                short lineNum = 0;
                bool addUToScore = false;

                for (byte charIndex = 0; charIndex < guess.CharIndexes.Length; charIndex++)
                {
                    char ch = Letter[guess.CharIndexes[charIndex]];

                    if (ch == 'U' && afterQ)
                    {
                        builder.Append(ch); 
                        addUToScore = false;
                        continue;
                    }
                    afterQ = false;
                    if (builder.Length == 5 ||
                        (addUToScore && builder.Length == 4))
                        break;
                    else
                    {
                        builder.Append(ch);
                        bool hasScore = validList.Contains(ch, current, out lineNum, out current);
                        if (ch == 'Q' && !addUToScore && builder.Length < 5)
                        {
                            hasScore = validList.Contains('U', current, out lineNum, out current);
                            afterQ = true;
                            addUToScore = true;
                        }
                        if (hasScore)
                        {
                            string validWord = builder.ToString();
                            byte score = (byte)(addUToScore ? validWord.Length + 1 : validWord.Length);
                            validWords[guess.Index * 5 + charIndex] = new ValidWords(lineNum, validWord, guess, score);
                        }
                    }
                }
            }
        }

        public int Report(StringBuilder outputBuffer)
        {
            int score = 0;
            outputBuffer.AppendLine(string.Format("Game {0}", GameNum));
            foreach (ValidWords pair in validWords)
            {
                if (pair != null)
                {
                    outputBuffer.AppendLine(String.Format("{0} {1} {2} {3} {4} {5}",
                        pair.Word, pair.LineNum, pair.Score, pair.Guess.X, pair.Guess.Y, pair.Guess.Dir));
                    score += pair.Score;
                }
            }
            outputBuffer.AppendLine(string.Format("Total Game Points = {0}", score));
            return score;
        }
    }

    /// <summary>
    /// collection of grids
    /// </summary>
    sealed class Solver
    {
        public const int MaxWordLen = 5;
        const int MinWordLength = 2;
        const byte LastY = 5;
        const byte LastX = 5;
        const int InvalidGameNum = 0;
        enum Dir { First = 1, Last = 9 };
        static readonly Guess[] _guesses = new Guess[144];
        static readonly short[,] DirOffset = { { 0, -1 }, { 1, -1 }, 
                                               { 1,  0 }, 
                                               { 1,  1 }, { 0, 1 }, { -1, 1 }, { -1, 0 }, { -1, -1 } };
        readonly WordList _wordList;
        List<Grid> _gridList;

        public Solver(string[] lines, WordList wordList)
        {
            int gameNum = 1;

            _gridList = new List<Grid>();
            foreach (string game in lines)
            {
                if (game.Length < 25)
                    continue;

                _gridList.Add(new Grid(game, gameNum));
                gameNum++;
            }
            _wordList = wordList;
            MakeGuesses();
        }

        private bool IsInRange(int x, int y)
        {
            return x >= 0 && x < LastX && y >= 0 && y < LastY;
        }

        private void MakeGuesses()
        {
            short ordinal = 0;

            for (byte y = 0; y < LastY; y++)
            {
                for (byte x = 0; x < LastX; x++)
                {
                    for (Dir dir = Dir.First; dir < Dir.Last; dir++)
                    {
                        List<byte> charIndexes = new List<byte>();
                        short nextX = x;
                        short nextY = y;
                        byte len;

                        for (len = 1; len <= 5; len++)
                        {
                            charIndexes.Add((byte)(nextX + nextY * LastY));
                            nextX += DirOffset[(int)dir - 1, 0];
                            nextY += DirOffset[(int)dir - 1, 1];
                            if (!IsInRange(nextX, nextY))
                                break;
                        }
                        if (len >= MinWordLength)
                        {
                            _guesses[ordinal] = new Guess(x, y, (byte)dir, charIndexes.ToArray(), ordinal);
                            ordinal++;
                        }
                    }
                }
            }
        }

        public void ScoreGames(StringBuilder outputBuffer)
        {
            Parallel.ForEach<Tuple<int, int>>(Partitioner.Create(0, _gridList.Count), range =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                    _gridList[i].Score(_guesses, _wordList);
            });

            int totalScore = 0;

            foreach (Grid grid in _gridList)
            {
                totalScore += grid.Report(outputBuffer);
            }
            outputBuffer.AppendLine(string.Format("Games Totals Points = {0} ", totalScore));
        }
    }

    /// <summary>
    /// list of valid words from external dictionary file
    /// </summary>
    sealed class WordList
    {
        static readonly short[] table = new short[27 * 27 * 27 * 27 * 27];

        public WordList(string[] lines)
        {
            Parallel.ForEach<Tuple<int, int>>(Partitioner.Create(0, lines.Length), range =>
            {
                for (int line = range.Item1; line < range.Item2; line++)
                {
                    table[Hash(lines[line])] = (short)(line + 1);
                }
            });
        }

        private int Hash(string word)
        {
            int hashCode = 0;

            foreach (char c in word)
            {
                hashCode = hashCode * 27 + (int)c - (int)'A' + 1;
            }
            return hashCode;
        }

        public bool Contains(char ch, int current, out short lineNum, out int next)
        {
            next = current * 27 + (int)ch - (int)'A' + 1;

            if (table[next] != 0)
            {
                lineNum = table[next];
                return true;
            }
            else
            {
                lineNum = 0;
                return false;
            }
        }
    }

    /// <summary>
    /// main entry
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            string[] gameLines = File.ReadAllLines("games.txt");
            string[] words = File.ReadAllLines("words5.txt");

            Stopwatch stopwatch = new Stopwatch();
            const int TIMING_REPETITIONS = 800;
            double averageTime = 0.0;
            StringBuilder output = new StringBuilder();

            for (int i = 0; i < TIMING_REPETITIONS; ++i)
            {
                stopwatch.Reset();
                stopwatch.Start();

                output.Clear();
                WordList wordList = new WordList(words);
                Solver games = new Solver(gameLines, wordList);
                games.ScoreGames(output);

                stopwatch.Stop();

                averageTime += stopwatch.Elapsed.TotalSeconds;
                GC.Collect();
            }
            averageTime /= (double)TIMING_REPETITIONS;
            output.AppendLine(string.Format("Total Average Time = {0:0.000000} sec", averageTime));
            File.WriteAllText("results.txt", output.ToString());
        }
    }
}