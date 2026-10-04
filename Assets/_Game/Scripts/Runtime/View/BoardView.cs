using System;
using System.Collections.Generic;
using CadenceClub.Art;
using CadenceClub.Core;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Template.Feel;
using UnityEngine;

namespace CadenceClub.View
{
    /// <summary>
    /// Draws the board and replays a move's <see cref="BoardEvent"/>s as animation. The move is already resolved in
    /// Core; this only shows it, step by step: swap → clears and specials → falls and new pieces. Pieces are pooled.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float PieceScale = 0.86f;
        private const float SwapSeconds = 0.12f;
        private const float ClearSeconds = 0.14f;
        private const float PopSeconds = 0.18f;
        private const float FallSecondsPerCell = 0.055f;
        private const float MinFallSeconds = 0.12f;

        private readonly Dictionary<Cell, PieceView> _pieces = new Dictionary<Cell, PieceView>();
        private readonly Stack<PieceView> _pool = new Stack<PieceView>();
        private readonly List<PieceView> _clearing = new List<PieceView>();
        private readonly Dictionary<Cell, SpriteRenderer> _crates = new Dictionary<Cell, SpriteRenderer>();
        private readonly Dictionary<Cell, SpriteRenderer> _ice = new Dictionary<Cell, SpriteRenderer>();
        private readonly Dictionary<Cell, SpriteRenderer> _chains = new Dictionary<Cell, SpriteRenderer>();
        private readonly List<SpriteRenderer> _breaking = new List<SpriteRenderer>();
        private Transform _coverRoot;
        private Vector2 _origin;
        private Transform _pieceRoot;
        private Board _board;
        private Sequence _hint;
        private Cell _hintA;
        private Cell _hintB;
        private Transform _finger;

        private sealed class PieceView
        {
            public Transform root;
            public SpriteRenderer body;
            public SpriteRenderer mark;
        }

        public Bounds Bounds { get; private set; }

        public bool IsHinting => _hint.isAlive;

        public static BoardView Create(Board board)
        {
            var view = new GameObject("Board").AddComponent<BoardView>();
            view.Build(board);
            return view;
        }

        public Vector3 CellToWorld(Cell c) => new Vector3(_origin.x + c.x, _origin.y + c.y, 0f);

        public bool TryWorldToCell(Vector3 world, out Cell cell)
        {
            cell = new Cell(Mathf.RoundToInt(world.x - _origin.x), Mathf.RoundToInt(world.y - _origin.y));
            return _board.IsPlayable(cell);
        }

        private void Build(Board board)
        {
            _board = board;
            _origin = new Vector2(-(board.Width - 1) * 0.5f, -(board.Height - 1) * 0.5f);
            Bounds = new Bounds(Vector3.zero, new Vector3(board.Width, board.Height, 0f));

            var tiles = new GameObject("Tiles").transform;
            tiles.SetParent(transform, false);
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (!board.IsPlayable(x, y))
                    {
                        continue;
                    }

                    var tile = new GameObject($"Tile {x},{y}").AddComponent<SpriteRenderer>();
                    tile.transform.SetParent(tiles, false);
                    tile.transform.position = CellToWorld(new Cell(x, y));
                    tile.sprite = PieceArt.CellTile;
                    tile.color = (x + y) % 2 == 0 ? new Color(0.13f, 0.20f, 0.30f, 0.85f) : new Color(0.16f, 0.24f, 0.35f, 0.85f);
                    tile.sortingOrder = 0;
                }
            }

            _pieceRoot = new GameObject("Pieces").transform;
            _pieceRoot.SetParent(transform, false);
            _coverRoot = new GameObject("Covers").transform;
            _coverRoot.SetParent(transform, false);
            Sync(board);
        }

        /// <summary>Snaps the view to the board's current state (start of a level, after a shuffle, as a safety net).</summary>
        public void Sync(Board board)
        {
            StopHint();
            _board = board;
            foreach (var view in _pieces.Values)
            {
                Release(view);
            }

            _pieces.Clear();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new Cell(x, y);
                    if (board.IsPlayable(cell) && !board[cell].IsEmpty)
                    {
                        _pieces[cell] = Spawn(board[cell], CellToWorld(cell));
                    }
                }
            }

            SyncCovers(board);
        }

        /// <summary>Crates sit in place of a piece, ice under it (order 5), chains over it (order 12).</summary>
        private void SyncCovers(Board board)
        {
            foreach (var map in new[] { _crates, _ice, _chains })
            {
                foreach (var r in map.Values)
                {
                    Destroy(r.gameObject);
                }

                map.Clear();
            }

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new Cell(x, y);
                    var cover = board.CoverAt(cell);
                    if (!board.IsPlayable(cell) || cover.IsEmpty)
                    {
                        continue;
                    }

                    if (cover.crate > 0)
                    {
                        _crates[cell] = cover.oil
                            ? CoverSprite("Oil", cell, PieceArt.Oil, 10, 0.92f) // oil is a one-hit crate that spreads
                            : CoverSprite("Crate", cell, PieceArt.Crate(cover.crate), 10, 0.92f);
                    }

                    if (cover.ice)
                    {
                        _ice[cell] = CoverSprite("Ice", cell, PieceArt.Ice, 5, 1f);
                    }

                    if (cover.chain)
                    {
                        _chains[cell] = CoverSprite("Chain", cell, PieceArt.Chain, 12, 0.95f);
                    }
                }
            }
        }

        private SpriteRenderer CoverSprite(string name, Cell cell, Sprite sprite, int order, float scale)
        {
            var r = new GameObject($"{name} {cell.x},{cell.y}").AddComponent<SpriteRenderer>();
            r.transform.SetParent(_coverRoot, false);
            r.transform.position = CellToWorld(cell);
            r.transform.localScale = Vector3.one * scale;
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>Takes a cover out of its map and shrinks it away as part of the clear phase.</summary>
        private void Break(Dictionary<Cell, SpriteRenderer> map, Cell cell, Sequence clears)
        {
            if (map.Remove(cell, out var r))
            {
                _breaking.Add(r);
                clears.Group(Tween.Scale(r.transform, 0f, ClearSeconds, Ease.InBack));
            }
        }

        /// <summary>
        /// Replays one move. <paramref name="onStep"/> fires as each cascade step starts with (step, events, from, to)
        /// so the caller can play rising-pitch sounds and count goal pieces.
        /// </summary>
        public async UniTask Play(List<BoardEvent> events, Board finalBoard, Action<int, List<BoardEvent>, int, int> onStep = null)
        {
            StopHint();
            int i = 0;
            while (i < events.Count)
            {
                var e = events[i];
                switch (e.type)
                {
                    case BoardEventType.Swapped:
                    case BoardEventType.SwapRejected: // swaps back
                        await AnimateSwap(e.a, e.b);
                        i++;
                        break;
                    case BoardEventType.StepStarted:
                        int end = i + 1;
                        while (end < events.Count && events[end].type != BoardEventType.StepStarted && events[end].type != BoardEventType.Shuffled)
                        {
                            end++;
                        }

                        onStep?.Invoke(e.value, events, i + 1, end);
                        await PlayStep(events, i + 1, end);
                        i = end;
                        break;
                    case BoardEventType.Shuffled:
                        await Reshuffle(finalBoard);
                        i++;
                        break;
                    default:
                        i++;
                        break;
                }
            }

            if (!Matches(finalBoard))
            {
                Debug.LogWarning("[BoardView] View drifted from the board; resyncing.");
                Sync(finalBoard);
            }
        }

        /// <summary>
        /// Nudges the two pieces of a move toward each other, again and again, until <see cref="StopHint"/>. With
        /// <paramref name="finger"/>, a fingertip also slides along the swap (the first session's "do this").
        /// </summary>
        public void ShowHint(Cell a, Cell b, bool finger = false)
        {
            StopHint();
            if (!_pieces.TryGetValue(a, out var va) || !_pieces.TryGetValue(b, out var vb))
            {
                return;
            }

            _hintA = a;
            _hintB = b;
            var nudge = (CellToWorld(b) - CellToWorld(a)) * 0.14f;
            var hint = Sequence.Create(cycles: -1)
                .Group(Tween.Position(va.root, CellToWorld(a) + nudge, 0.16f, Ease.InOutSine, 2, CycleMode.Yoyo))
                .Group(Tween.Position(vb.root, CellToWorld(b) - nudge, 0.16f, Ease.InOutSine, 2, CycleMode.Yoyo));
            if (finger)
            {
                var tip = Finger();
                tip.gameObject.SetActive(true);
                hint = hint.Group(Tween.Position(tip, CellToWorld(a), CellToWorld(b), 0.6f, Ease.InOutSine));
            }

            _hint = hint.ChainDelay(0.9f);
        }

        private Transform Finger()
        {
            if (_finger != null)
            {
                return _finger;
            }

            _finger = new GameObject("Finger").transform;
            _finger.SetParent(transform, false);
            foreach (var (scale, color, order) in new[] { (0.56f, new Color(0.07f, 0.11f, 0.18f, 0.85f), 30), (0.44f, Color.white, 31) })
            {
                var disc = new GameObject("Disc").AddComponent<SpriteRenderer>();
                disc.transform.SetParent(_finger, false);
                disc.transform.localScale = Vector3.one * scale;
                disc.sprite = PieceArt.Disc;
                disc.color = color;
                disc.sortingOrder = order;
            }

            return _finger;
        }

        /// <summary>Stops the hint and puts both pieces back on their cells.</summary>
        public void StopHint()
        {
            if (_finger != null)
            {
                _finger.gameObject.SetActive(false);
            }

            if (!_hint.isAlive)
            {
                return;
            }

            _hint.Stop();
            foreach (var cell in new[] { _hintA, _hintB })
            {
                if (_pieces.TryGetValue(cell, out var view))
                {
                    view.root.position = CellToWorld(cell);
                }
            }
        }

        /// <summary>A swap that makes no match: the two pieces start to trade places, then spring back.</summary>
        public async UniTask Bounce(Cell a, Cell b)
        {
            StopHint();
            if (!_pieces.TryGetValue(a, out var va) || !_pieces.TryGetValue(b, out var vb))
            {
                return;
            }

            var half = (CellToWorld(b) - CellToWorld(a)) * 0.35f;
            await Sequence.Create()
                .Group(Tween.Position(va.root, CellToWorld(a) + half, SwapSeconds * 0.6f, Ease.OutQuad, 2, CycleMode.Yoyo))
                .Group(Tween.Position(vb.root, CellToWorld(b) - half, SwapSeconds * 0.6f, Ease.OutQuad, 2, CycleMode.Yoyo));
        }

        private async UniTask AnimateSwap(Cell a, Cell b)
        {
            if (!_pieces.TryGetValue(a, out var va) || !_pieces.TryGetValue(b, out var vb))
            {
                return;
            }

            _pieces[a] = vb;
            _pieces[b] = va;
            await Sequence.Create()
                .Group(Tween.Position(va.root, CellToWorld(b), SwapSeconds, Ease.OutQuad))
                .Group(Tween.Position(vb.root, CellToWorld(a), SwapSeconds, Ease.OutQuad));
        }

        private async UniTask PlayStep(List<BoardEvent> events, int from, int to)
        {
            // Phase 1: clears (all at once), specials going off, then new specials popping in.
            var clears = Sequence.Create();
            _clearing.Clear();
            _breaking.Clear();
            bool anyClear = false;
            var created = new List<BoardEvent>();
            var spills = new List<Cell>();
            for (int i = from; i < to; i++)
            {
                var e = events[i];
                if ((e.type == BoardEventType.Cleared || e.type == BoardEventType.TrophyCollected) && _pieces.TryGetValue(e.a, out var view))
                {
                    _pieces.Remove(e.a);
                    _clearing.Add(view);
                    clears.Group(Tween.Scale(view.root, 0f, ClearSeconds, Ease.InBack));
                    anyClear = true;
                }
                else if (e.type == BoardEventType.OilSpread)
                {
                    // The piece under the new oil shrinks away; the puddle grows in with the pops below.
                    if (_pieces.Remove(e.b, out var covered))
                    {
                        _clearing.Add(covered);
                        clears.Group(Tween.Scale(covered.root, 0f, ClearSeconds, Ease.InBack));
                    }

                    spills.Add(e.b);
                    anyClear = true;
                }
                else if (e.type == BoardEventType.CrateHit && e.value > 0 && _crates.TryGetValue(e.a, out var crate))
                {
                    crate.sprite = PieceArt.Crate(e.value);
                    clears.Group(Tween.Scale(crate.transform, 1.06f, ClearSeconds * 0.5f, Ease.OutQuad, 2, CycleMode.Yoyo));
                    anyClear = true;
                }
                else if (e.type == BoardEventType.CrateHit || e.type == BoardEventType.OilCleared || e.type == BoardEventType.IceBroken ||
                         e.type == BoardEventType.ChainBroken)
                {
                    var map = e.type == BoardEventType.IceBroken ? _ice : e.type == BoardEventType.ChainBroken ? _chains : _crates;
                    Break(map, e.a, clears);
                    anyClear = true;
                }
                else if (e.type == BoardEventType.SpecialActivated && (e.piece.special == Special.Bomb || e.piece.special == Special.Disco))
                {
                    JuiceFx.Shake(Camera.main != null ? Camera.main.transform : null, 0.12f, 0.2f);
                }
                else if (e.type == BoardEventType.SpecialCreated || e.type == BoardEventType.TrophyDropped)
                {
                    created.Add(e);
                }
            }

            if (anyClear)
            {
                await clears;
                foreach (var view in _clearing)
                {
                    Release(view);
                }

                foreach (var r in _breaking)
                {
                    Destroy(r.gameObject);
                }

                _clearing.Clear();
                _breaking.Clear();
            }
            else
            {
                clears.Stop();
            }

            if (created.Count > 0 || spills.Count > 0)
            {
                var pops = Sequence.Create();
                foreach (var cell in spills)
                {
                    var oil = CoverSprite("Oil", cell, PieceArt.Oil, 10, 0f);
                    _crates[cell] = oil;
                    pops.Group(Tween.Scale(oil.transform, 0.92f, PopSeconds, Ease.OutBack));
                }

                foreach (var e in created)
                {
                    if (_pieces.TryGetValue(e.a, out var kept))
                    {
                        Release(kept); // a chained piece survived its clear; the new special replaces it
                    }

                    var view = Spawn(e.piece, CellToWorld(e.a));
                    view.root.localScale = Vector3.zero;
                    _pieces[e.a] = view;
                    pops.Group(Tween.Scale(view.root, PieceScale, PopSeconds, Ease.OutBack));
                }

                await pops;
            }

            // Phase 2: everything falls and new pieces drop in, together.
            var falls = Sequence.Create();
            bool any = false;
            for (int i = from; i < to; i++)
            {
                var e = events[i];
                if (e.type == BoardEventType.Fell && _pieces.TryGetValue(e.a, out var view))
                {
                    _pieces.Remove(e.a);
                    _pieces[e.b] = view;
                    falls.Group(Tween.Position(view.root, CellToWorld(e.b), FallTime(e.a.y - e.b.y), Ease.InQuad));
                    any = true;
                }
                else if (e.type == BoardEventType.Spawned)
                {
                    var spawned = Spawn(e.piece, CellToWorld(e.b));
                    _pieces[e.a] = spawned;
                    falls.Group(Tween.Position(spawned.root, CellToWorld(e.a), FallTime(e.b.y - e.a.y), Ease.InQuad));
                    any = true;
                }
            }

            if (any)
            {
                await falls;
            }
            else
            {
                falls.Stop();
            }
        }

        private async UniTask Reshuffle(Board board)
        {
            var shrink = Sequence.Create();
            foreach (var view in _pieces.Values)
            {
                shrink.Group(Tween.Scale(view.root, 0f, 0.18f, Ease.InBack));
            }

            await shrink;
            Sync(board);
            var grow = Sequence.Create();
            foreach (var view in _pieces.Values)
            {
                view.root.localScale = Vector3.zero;
                grow.Group(Tween.Scale(view.root, PieceScale, 0.22f, Ease.OutBack));
            }

            await grow;
        }

        private static float FallTime(int cells) => Mathf.Max(MinFallSeconds, cells * FallSecondsPerCell);

        private bool Matches(Board board)
        {
            int count = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new Cell(x, y);
                    if (board.IsPlayable(cell) && !board[cell].IsEmpty)
                    {
                        count++;
                        if (!_pieces.ContainsKey(cell))
                        {
                            return false;
                        }
                    }

                    var cover = board.CoverAt(cell);
                    if ((cover.crate > 0) != _crates.ContainsKey(cell) || cover.ice != _ice.ContainsKey(cell) || cover.chain != _chains.ContainsKey(cell))
                    {
                        return false;
                    }
                }
            }

            return count == _pieces.Count;
        }

        private PieceView Spawn(Piece piece, Vector3 position)
        {
            var view = _pool.Count > 0 ? _pool.Pop() : CreateView();
            view.root.gameObject.SetActive(true);
            view.root.position = position;
            view.root.localScale = Vector3.one * PieceScale;
            view.body.sprite = piece.trophy ? PieceArt.Trophy : piece.special == Special.Disco ? PieceArt.Disco : PieceArt.Piece(piece.color);
            var mark = PieceArt.Mark(piece.special);
            view.mark.sprite = mark;
            view.mark.enabled = mark != null;
            return view;
        }

        private PieceView CreateView()
        {
            var root = new GameObject("Piece").transform;
            root.SetParent(_pieceRoot, false);
            var body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(root, false);
            body.sortingOrder = 10;
            var mark = new GameObject("Mark").AddComponent<SpriteRenderer>();
            mark.transform.SetParent(root, false);
            mark.sortingOrder = 11;
            return new PieceView { root = root, body = body, mark = mark };
        }

        private void Release(PieceView view)
        {
            Tween.StopAll(view.root);
            view.root.gameObject.SetActive(false);
            _pool.Push(view);
        }
    }
}
