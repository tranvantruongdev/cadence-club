using System;
using System.Collections.Generic;

namespace CadenceClub.Core
{
    /// <summary>One connected set of same-coloured matched cells, and the special it creates.</summary>
    public sealed class MatchGroup
    {
        public readonly List<Cell> cells = new List<Cell>();
        public int color;
        public int longestRun;
        public Special creates;
        public Cell createAt;

        public override string ToString() => $"{cells.Count} × colour {color}, run {longestRun} → {creates} at {createAt}";
    }

    /// <summary>
    /// Finds matches and decides which special each one makes:
    /// 5 in a line → disco · L/T (a row and a column of 3+) → bomb · 4 in a line → rocket · 2×2 → glider.
    /// </summary>
    public static class MatchFinder
    {
        /// <param name="swapA">The swapped cells, if this is the first step of a move: a special appears where
        /// the player moved a piece, and a rocket points along the swipe.</param>
        public static List<MatchGroup> Find(Board board, Cell? swapA = null, Cell? swapB = null)
        {
            int w = board.Width;
            int h = board.Height;
            var runH = new int[w * h];
            var runV = new int[w * h];
            var square = new bool[w * h];

            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    int end = RunEnd(board, x, y, 1, 0);
                    int length = end - x;
                    if (length >= 3)
                    {
                        for (int i = x; i < end; i++)
                        {
                            runH[y * w + i] = length;
                        }
                    }

                    x = Math.Max(end, x + 1);
                }
            }

            for (int x = 0; x < w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    int end = RunEnd(board, x, y, 0, 1);
                    int length = end - y;
                    if (length >= 3)
                    {
                        for (int i = y; i < end; i++)
                        {
                            runV[i * w + x] = length;
                        }
                    }

                    y = Math.Max(end, y + 1);
                }
            }

            for (int y = 0; y + 1 < h; y++)
            {
                for (int x = 0; x + 1 < w; x++)
                {
                    int c = board[x, y].color;
                    if (board[x, y].CanMatch && board[x + 1, y].color == c && board[x, y + 1].color == c && board[x + 1, y + 1].color == c)
                    {
                        square[y * w + x] = square[y * w + x + 1] = square[(y + 1) * w + x] = square[(y + 1) * w + x + 1] = true;
                    }
                }
            }

            var groups = new List<MatchGroup>();
            var seen = new bool[w * h];
            var stack = new Stack<Cell>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (seen[i] || !Matched(i))
                    {
                        continue;
                    }

                    var group = new MatchGroup { color = board[x, y].color };
                    bool hasH = false;
                    bool hasV = false;
                    bool hasSquare = false;
                    seen[i] = true;
                    stack.Push(new Cell(x, y));
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        int ci = c.y * w + c.x;
                        group.cells.Add(c);
                        group.longestRun = Math.Max(group.longestRun, Math.Max(runH[ci], runV[ci]));
                        hasH |= runH[ci] > 0;
                        hasV |= runV[ci] > 0;
                        hasSquare |= square[ci];
                        Visit(group, c.x + 1, c.y);
                        Visit(group, c.x - 1, c.y);
                        Visit(group, c.x, c.y + 1);
                        Visit(group, c.x, c.y - 1);
                    }

                    Decide(board, group, hasH, hasV, hasSquare, runH, runV, swapA, swapB);
                    groups.Add(group);
                }
            }

            return groups;

            bool Matched(int i) => runH[i] > 0 || runV[i] > 0 || square[i];

            void Visit(MatchGroup group, int nx, int ny)
            {
                if (!board.InBounds(nx, ny))
                {
                    return;
                }

                int ni = ny * w + nx;
                if (!seen[ni] && Matched(ni) && board[nx, ny].color == group.color)
                {
                    seen[ni] = true;
                    stack.Push(new Cell(nx, ny));
                }
            }
        }

        /// <summary>Index just past the run of same-coloured matchable pieces starting at (x, y).</summary>
        private static int RunEnd(Board board, int x, int y, int dx, int dy)
        {
            var first = board[x, y];
            if (!board.IsPlayable(x, y) || !first.CanMatch)
            {
                return (dx != 0 ? x : y) + 1;
            }

            int cx = x + dx;
            int cy = y + dy;
            while (board.IsPlayable(cx, cy) && board[cx, cy].CanMatch && board[cx, cy].color == first.color)
            {
                cx += dx;
                cy += dy;
            }

            return dx != 0 ? cx : cy;
        }

        private static void Decide(Board board, MatchGroup group, bool hasH, bool hasV, bool hasSquare, int[] runH, int[] runV,
            Cell? swapA, Cell? swapB)
        {
            int w = board.Width;
            if (group.longestRun >= 5)
            {
                group.creates = Special.Disco;
            }
            else if (hasH && hasV)
            {
                group.creates = Special.Bomb;
            }
            else if (group.longestRun == 4)
            {
                bool swipedHorizontally = swapA.HasValue && swapB.HasValue && swapA.Value.y == swapB.Value.y;
                bool fromSwap = Contains(group, swapA) || Contains(group, swapB);
                group.creates = fromSwap ? (swipedHorizontally ? Special.RocketH : Special.RocketV) : (hasH ? Special.RocketH : Special.RocketV);
            }
            else if (hasSquare)
            {
                group.creates = Special.Glider;
            }
            else
            {
                group.creates = Special.None;
                group.createAt = group.cells[0];
                return;
            }

            // Where the special appears: where the player moved a piece if possible, else at the heart of the shape.
            if (Contains(group, swapB) && !board[swapB.Value].IsSpecial)
            {
                group.createAt = swapB.Value;
                return;
            }

            if (Contains(group, swapA) && !board[swapA.Value].IsSpecial)
            {
                group.createAt = swapA.Value;
                return;
            }

            Cell best = group.cells[0];
            int bestScore = int.MinValue;
            foreach (var c in group.cells)
            {
                int i = c.y * w + c.x;
                int score = (runH[i] > 0 && runV[i] > 0 ? 1000 : 0) + Math.Max(runH[i], runV[i]) * 10 - c.y - c.x * 2;
                if (board[c].IsSpecial)
                {
                    score -= 10000; // don't overwrite a special that is about to go off
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            group.createAt = best;
        }

        private static bool Contains(MatchGroup group, Cell? cell)
        {
            if (!cell.HasValue)
            {
                return false;
            }

            foreach (var c in group.cells)
            {
                if (c.Equals(cell.Value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
