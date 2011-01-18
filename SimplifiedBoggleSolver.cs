using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Collections;

namespace BoggleSolver
{
    struct Guess
    {
        public int x;
        public int y;
        public int dir;
        public int[] charIndexes;
        public int len;

        public Guess(int x, int y, int dir, int len, int[] charIndexes)
        {
            this.x = x;
            this.y = y;
            this.dir = dir;
            this.len = len;
            this.charIndexes = charIndexes;
        }
    }

    class Board
    {
        public Board(string board, int num)
        {
            Grid = board;
            GameNum = num;
        }

        public bool ScoreWord(Guess guess, WordList validList)
        {
            StringBuilder stringBuilder = new StringBuilder();
            StringBuilder builderWithQu = null;

            foreach (int i in guess.charIndexes)
            {
                stringBuilder.Append(Grid[i]);
                if (builderWithQu  != null)
                    builderWithQu.Append(Grid[i]);
                if (Grid[i] == 'Q')
                {
                    builderWithQu = new StringBuilder(stringBuilder.ToString());
                    builderWithQu.Append("U");
                }
            }
            string word = stringBuilder.ToString();
            int[] lineNums;
            bool hasScore = validList.Contains(word, out lineNums);

            if (!hasScore && builderWithQu != null && builderWithQu.Length <= Games.MaxWordLen)
            {
                word = builderWithQu.ToString();
                hasScore = validList.Contains(word, out lineNums);
            }
            if (hasScore)
            {
                StringBuilder prefix = new StringBuilder();
                int depth = 0;

                foreach (int lineNum in lineNums)
                {
                    if (depth >= word.Length)
                        break;
                    prefix.Append(word[depth]);
                    depth++;
                    if (lineNum != 0)
                    {
                        report.Add(String.Format("{0} {1} {2} {3} {4} {5}", 
                            prefix.ToString(), lineNum, prefix.Length, guess.x, guess.y, guess.dir));
                        score += prefix.Length;
                    }
                }
            }
            return hasScore;
        }

        public int Report(StringBuilder outputBuffer)
        {
            outputBuffer.AppendLine(string.Format("Game {0}", GameNum));
            foreach (string line in report)
            {
                outputBuffer.AppendLine(line);
            }
            outputBuffer.AppendLine(string.Format("Total Game Points = {0}", score));
            return score;
        }

        string Grid { get; set; }
        int GameNum { get; set; }
        List<string> report = new List<string>();
        int score;
    }

    class Games
    {
        public const int MaxWordLen = 5;

        List<Board> _boardList;
        enum Dir
        {
            First = 1, North = 1, NorthEast = 2, East = 3, SouthEast = 4, South = 5,
            SouthWest = 6, West = 7, NorthWest = 8, Last = 9
        };
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

        IEnumerable<Guess> MakeGuesses()
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
                        int len;

                        for (len = 1; len <= 5; len++)
                        {
                            charIndexes.Add(nextX + nextY * LastY);
                            if (!MoveToNeighbor(dir, ref nextX, ref nextY))
                                break;
                        }
                        yield return new Guess(x, y, (int)dir, len, charIndexes.ToArray());
                    }
                }
            }
        }

        bool MoveToNeighbor(Dir dir, ref int nextX, ref int nextY)
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

        public void ScoreGames(StringBuilder outputBuffer)
        {
            _boardList.AsParallel().ForAll<Board>(game =>
            {
                foreach (Guess guess in MakeGuesses())
                {
                    game.ScoreWord(guess, _wordList);
                }
            });

            int totalScore = 0;

            foreach (Board board in _boardList)
            {
                totalScore += board.Report(outputBuffer);
            }
            outputBuffer.AppendLine(string.Format("Games Totals Points = {0} ", totalScore));
        }
    }

    class WordList
    {
        class Trie
        {
            public bool isWord { get; set; }
            public int lineNum { get; set; }
            public Trie[] edges { get; set; }

            public Trie()
            {
                const int InvalidLineNum = 0;

                lineNum = InvalidLineNum;
                isWord = false;
                edges = new Trie[26];
            }
        }

        Trie trie;

        public WordList(string[] lines)
        {
            short lineNum = 1;

            trie = new Trie();
            foreach (string word in lines)
            {
                if (word != word.ToUpper())
                    throw new FormatException("Error with dictionary: " + word);

                Trie current = trie;
                foreach (char c in word)
                {
                    int key = (int)c - (int)'A';

                    if (current.edges[key] == null)
                    {
                        current.edges[key] = new Trie();
                    }
                    current = current.edges[key]; 
                }
                current.isWord = true;
                current.lineNum = lineNum;
                lineNum++;
            }
        }

        public bool Contains(string word, out int[] lineNum)
        {
            lineNum = new int[5];
            int depth = 0;
            Trie current = trie;
            bool hasAtLeastOne = false;

            foreach (char c in word)
            {
                int key = (int)c - (int)'A';

                if (current.edges[key] == null)
                    break;
                current = current.edges[key];
                if (current.isWord)
                {
                    lineNum[depth] = current.lineNum;
                    hasAtLeastOne = true;
                }
                depth++;
            }
            return hasAtLeastOne;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string[] gameLines = File.ReadAllLines("games.txt");
            string[] words = File.ReadAllLines("words5.txt");

            Stopwatch stopwatch = new Stopwatch();
            const int TIMING_REPETITIONS = 25;
            double averageTime = 0.0;
            StringBuilder output = new StringBuilder();

            for (int i = 0; i < TIMING_REPETITIONS; ++i)
            {
                stopwatch.Reset();
                stopwatch.Start();

                WordList wordList = new WordList(words);
                Games games = new Games(gameLines, wordList);
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
