using System;
using System.Text;

namespace CadenceClub.Core
{
    /// <summary>Specials made by bigger matches. Rockets, gliders and bombs keep their colour; a disco has none.</summary>
    public enum Special : byte
    {
        None,
        RocketH,
        RocketV,
        Glider,
        Bomb,
        Disco,
    }

    public struct Piece : IEquatable<Piece>
    {
        public const int NoColor = -1;

        public int color;
        public Special special;

        public static Piece Empty => new Piece { color = NoColor, special = Special.None };

        public static Piece Normal(int color) => new Piece { color = color, special = Special.None };

        public static Piece Make(int color, Special special) =>
            new Piece { color = special == Special.Disco ? NoColor : color, special = special };

        public bool IsEmpty => color == NoColor && special == Special.None;

        public bool IsSpecial => special != Special.None;

        /// <summary>Takes part in colour matches: anything with a colour, including coloured specials.</summary>
        public bool CanMatch => color >= 0;

        public bool Equals(Piece other) => color == other.color && special == other.special;

        public override bool Equals(object obj) => obj is Piece other && Equals(other);

        public override int GetHashCode() => (color + 1) * 8 + (int)special;

        public override string ToString() => IsEmpty ? "." : special == Special.None ? ((char)('A' + color)).ToString() : $"{special}({color})";
    }

    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int x;
        public readonly int y;

        public Cell(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool IsAdjacentTo(Cell other) => Math.Abs(x - other.x) + Math.Abs(y - other.y) == 1;

        public bool Equals(Cell other) => x == other.x && y == other.y;

        public override bool Equals(object obj) => obj is Cell other && Equals(other);

        public override int GetHashCode() => x * 397 ^ y;

        public override string ToString() => $"({x},{y})";
    }

    /// <summary>
    /// The playing grid. (0,0) is the bottom-left; y grows upward, so gravity pulls toward y = 0.
    /// Holes are cells outside the board shape: never playable, never filled.
    /// </summary>
    public sealed class Board
    {
        private readonly bool[] _playable;
        private readonly Piece[] _pieces;

        public Board(int width, int height, bool[] playable = null)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "board needs a positive size");
            }

            Width = width;
            Height = height;
            _playable = new bool[width * height];
            _pieces = new Piece[width * height];
            for (int i = 0; i < _pieces.Length; i++)
            {
                _playable[i] = playable == null || playable[i];
                _pieces[i] = Piece.Empty;
            }
        }

        public int Width { get; }
        public int Height { get; }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsPlayable(int x, int y) => InBounds(x, y) && _playable[y * Width + x];

        public bool IsPlayable(Cell c) => IsPlayable(c.x, c.y);

        public Piece this[int x, int y]
        {
            get => InBounds(x, y) ? _pieces[y * Width + x] : Piece.Empty;
            set
            {
                if (!IsPlayable(x, y))
                {
                    throw new ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is not a playable cell");
                }

                _pieces[y * Width + x] = value;
            }
        }

        public Piece this[Cell c]
        {
            get => this[c.x, c.y];
            set => this[c.x, c.y] = value;
        }

        public Board Clone()
        {
            var copy = new Board(Width, Height, _playable);
            Array.Copy(_pieces, copy._pieces, _pieces.Length);
            return copy;
        }

        public bool IsFull()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (IsPlayable(x, y) && this[x, y].IsEmpty)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Builds a board from rows written top to bottom: 'A'–'F' colours, '@' disco, '.' empty, '#' hole.
        /// Coloured specials are set afterwards through the indexer.
        /// </summary>
        public static Board Parse(params string[] rowsTopToBottom)
        {
            int height = rowsTopToBottom.Length;
            int width = rowsTopToBottom[0].Length;
            var playable = new bool[width * height];
            for (int row = 0; row < height; row++)
            {
                if (rowsTopToBottom[row].Length != width)
                {
                    throw new ArgumentException("all rows must have the same length");
                }

                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    playable[y * width + x] = rowsTopToBottom[row][x] != '#';
                }
            }

            var board = new Board(width, height, playable);
            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    char c = rowsTopToBottom[row][x];
                    if (c >= 'A' && c <= 'F')
                    {
                        board[x, y] = Piece.Normal(c - 'A');
                    }
                    else if (c == '@')
                    {
                        board[x, y] = Piece.Make(Piece.NoColor, Special.Disco);
                    }
                }
            }

            return board;
        }

        /// <summary>Rows top to bottom: colour letters, '*' coloured special, '@' disco, '.' empty, '#' hole.</summary>
        public string[] ToRows()
        {
            var rows = new string[Height];
            var sb = new StringBuilder(Width);
            for (int row = 0; row < Height; row++)
            {
                int y = Height - 1 - row;
                sb.Clear();
                for (int x = 0; x < Width; x++)
                {
                    var p = this[x, y];
                    sb.Append(!IsPlayable(x, y) ? '#'
                        : p.IsEmpty ? '.'
                        : p.special == Special.Disco ? '@'
                        : p.special != Special.None ? '*'
                        : (char)('A' + p.color));
                }

                rows[row] = sb.ToString();
            }

            return rows;
        }

        public override string ToString() => string.Join("\n", ToRows());
    }
}
