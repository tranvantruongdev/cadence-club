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

        /// <summary>A trophy (goal item): no colour, never matches or blasts away; it falls, and leaves at the bottom.</summary>
        public bool trophy;

        public static Piece Empty => new Piece { color = NoColor, special = Special.None };

        public static Piece Trophy => new Piece { color = NoColor, special = Special.None, trophy = true };

        public static Piece Normal(int color) => new Piece { color = color, special = Special.None };

        public static Piece Make(int color, Special special) =>
            new Piece { color = special == Special.Disco ? NoColor : color, special = special };

        public bool IsEmpty => color == NoColor && special == Special.None && !trophy;

        public bool IsSpecial => special != Special.None;

        /// <summary>Takes part in colour matches: anything with a colour, including coloured specials.</summary>
        public bool CanMatch => color >= 0;

        public bool Equals(Piece other) => color == other.color && special == other.special && trophy == other.trophy;

        public override bool Equals(object obj) => obj is Piece other && Equals(other);

        public override int GetHashCode() => (color + 1) * 8 + (int)special + (trophy ? 1000 : 0);

        public override string ToString() =>
            trophy ? "T" : IsEmpty ? "." : special == Special.None ? ((char)('A' + color)).ToString() : $"{special}({color})";
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
    /// What lies on a cell besides its piece. A crate fills the cell (no piece) and takes 1–2 hits; ice lies under the
    /// piece and breaks when that piece is cleared; a chain locks the piece (no swapping, no falling) until a clear
    /// breaks it, and the piece stays. Oil is a one-hit crate that spreads (see <see cref="LevelState"/>).
    /// </summary>
    public struct Cover : IEquatable<Cover>
    {
        /// <summary>Hits left before the crate breaks; 0 = no crate.</summary>
        public int crate;

        public bool ice;
        public bool chain;

        /// <summary>The crate here is an oil spill (always one hit).</summary>
        public bool oil;

        public bool IsEmpty => crate == 0 && !ice && !chain;

        public bool Equals(Cover other) => crate == other.crate && ice == other.ice && chain == other.chain && oil == other.oil;

        public override bool Equals(object obj) => obj is Cover other && Equals(other);

        public override int GetHashCode() => crate * 8 + (oil ? 4 : 0) + (ice ? 2 : 0) + (chain ? 1 : 0);
    }

    /// <summary>
    /// The playing grid. (0,0) is the bottom-left; y grows upward, so gravity pulls toward y = 0.
    /// Holes are cells outside the board shape: never playable, never filled.
    /// </summary>
    public sealed class Board
    {
        private readonly bool[] _playable;
        private readonly Piece[] _pieces;
        private readonly Cover[] _covers;

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
            _covers = new Cover[width * height];
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

        public Cover CoverAt(int x, int y) => InBounds(x, y) ? _covers[y * Width + x] : default;

        public Cover CoverAt(Cell c) => CoverAt(c.x, c.y);

        public void SetCover(Cell c, Cover cover)
        {
            if (!IsPlayable(c))
            {
                throw new ArgumentOutOfRangeException(nameof(c), $"{c} is not a playable cell");
            }

            _covers[c.y * Width + c.x] = cover;
        }

        public bool HasCrate(int x, int y) => CoverAt(x, y).crate > 0;

        public bool HasCrate(Cell c) => HasCrate(c.x, c.y);

        public bool HasOil(Cell c) => CoverAt(c).oil;

        public bool IsLocked(Cell c) => CoverAt(c).chain;

        /// <summary>Holds its place: a crate, or a chained piece. Pieces land on it and never pass through.</summary>
        public bool IsFixed(int x, int y)
        {
            var cover = CoverAt(x, y);
            return cover.crate > 0 || cover.chain;
        }

        /// <summary>
        /// Lays covers from rows written top to bottom (the level shape): '1'/'2' crate with that many hits,
        /// 'o' oil, 'i' ice, 'l' chain; anything else adds nothing. Crate and oil cells lose their piece.
        /// </summary>
        public void ApplyCovers(string[] rowsTopToBottom)
        {
            for (int row = 0; row < Height; row++)
            {
                int y = Height - 1 - row;
                for (int x = 0; x < Width; x++)
                {
                    char c = rowsTopToBottom[row][x];
                    if (!IsPlayable(x, y))
                    {
                        continue;
                    }

                    var cover = CoverAt(x, y);
                    if (c == '1' || c == '2' || c == 'o')
                    {
                        cover.crate = c == 'o' ? 1 : c - '0';
                        cover.oil = c == 'o';
                        _pieces[y * Width + x] = Piece.Empty;
                    }
                    else if (c == 'i')
                    {
                        cover.ice = true;
                    }
                    else if (c == 'l')
                    {
                        cover.chain = true;
                    }

                    _covers[y * Width + x] = cover;
                }
            }
        }

        public Board Clone()
        {
            var copy = new Board(Width, Height, _playable);
            Array.Copy(_pieces, copy._pieces, _pieces.Length);
            Array.Copy(_covers, copy._covers, _covers.Length);
            return copy;
        }

        /// <summary>Every playable cell holds a piece or a crate.</summary>
        public bool IsFull()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (IsPlayable(x, y) && this[x, y].IsEmpty && !HasCrate(x, y))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Builds a board from rows written top to bottom: 'A'–'F' colours, '@' disco, 'T' trophy, '.' empty, '#' hole.
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
                    else if (c == 'T')
                    {
                        board[x, y] = Piece.Trophy;
                    }
                }
            }

            return board;
        }

        /// <summary>Rows top to bottom: colour letters, '*' coloured special, '@' disco, 'T' trophy, '.' empty, '#' hole, '1'/'2' crate, 'o' oil.</summary>
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
                        : CoverAt(x, y).oil ? 'o'
                        : HasCrate(x, y) ? (char)('0' + CoverAt(x, y).crate)
                        : p.trophy ? 'T'
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
