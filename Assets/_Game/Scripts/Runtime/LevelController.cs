using System;
using System.Collections.Generic;
using System.Linq;
using CadenceClub.Core;
using CadenceClub.UI;
using CadenceClub.View;
using Cysharp.Threading.Tasks;
using Template.Core.Random;
using Template.Game.Flow;
using Template.Infra;
using Template.Infra.Audio;
using Template.Infra.Device;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CadenceClub
{
    /// <summary>
    /// The Game scene: owns one <see cref="LevelState"/>, turns swipes into moves, lets the <see cref="BoardView"/>
    /// replay each resolved move, and shows the end card. Input is locked while a move plays.
    /// </summary>
    public sealed class LevelController : MonoBehaviour
    {
        private const float SwipeThreshold = 0.35f;
        private const float HintDelay = 5f;

        private LevelDef _def;
        private LevelState _state;
        private BoardView _board;
        private LevelHud _hud;
        private Camera _camera;
        private AudioService _audio;
        private AudioClip _pop;
        private Bot _hintBot;
        private float _idle;
        private bool _busy;
        private bool _pressing;
        private Vector3 _pressWorld;
        private Cell _pressCell;
        private bool _pressOnBoard;

        private void Start()
        {
            if (!BootGuard.EnsureBooted())
            {
                return;
            }

            _audio = Services.Get<AudioService>();
            _pop = ToneFactory.Blip("match-pop", 660f, 0.07f, 0.45f);
            _camera = Camera.main;
            _camera.orthographic = true;
            _camera.backgroundColor = new Color(0.07f, 0.11f, 0.18f);
            _camera.clearFlags = CameraClearFlags.SolidColor;


            _hintBot = new Bot(BotKind.Greedy, new SeededRandom(1)); // hints the move a careful player would make
            AppLifecycle.BackPressed += GoHome;
            Play(Levels.Override ?? Levels.Next(Club.Data.level));
        }

        private void OnDestroy() => AppLifecycle.BackPressed -= GoHome;

        /// <summary>Loads a level and builds its HUD and board from scratch (shapes differ between levels).</summary>
        private void Play(int number)
        {
            _def = Levels.Load(number);
            if (_hud != null)
            {
                Destroy(_hud.gameObject);
            }

            if (_board != null)
            {
                Destroy(_board.gameObject);
                _board = null;
            }

            _hud = LevelHud.Create(_def);
            _hud.RetryPressed += Retry;
            _hud.NextPressed += NextLevel;
            _hud.HomePressed += GoHome;
            _hud.PowerPressed += slot => UsePower(slot).Forget();
            Restart();
        }

        private void NextLevel() => Play(Math.Min(_def.id + 1, Levels.Count));

        /// <summary>Playing again needs a life; with none, the end card says when the next one comes.</summary>
        private void Retry()
        {
            var club = Club.Data;
            if (club.Lives(Club.Master, Club.Now) <= 0)
            {
                _hud.ShowNoLives(club.NextLifeIn(Club.Master, Club.Now));
                return;
            }

            Restart();
        }

        /// <summary>Pays out a win (coins, the first-win ★, the scripted free rider) or charges a life for a loss, and saves.</summary>
        private string Settle()
        {
            var club = Club.Data;
            var md = Club.Master;
            string line;
            if (_state.Outcome == LevelOutcome.Won)
            {
                var reward = club.Win(md, _def.id, _state.MovesLeft);
                line = $"+{reward.coins} coins" + (reward.stars > 0 ? "  ·  +1 star" : "");
                var gift = club.TryGiveFreeRider(md);
                if (gift.HasValue)
                {
                    line += $"\n{md.Rider(gift.Value.riderId).name} joined the club!";
                    Club.PendingReveals.Add(new PullOutcome
                    {
                        pull = new PullResult { riderId = gift.Value.riderId, rarity = md.Rider(gift.Value.riderId).rarity },
                        grant = gift.Value,
                    });
                }
            }
            else
            {
                club.SpendLife(md, Club.Now);
                int lives = club.Lives(md, Club.Now);
                line = lives == 1 ? "1 life left" : $"{lives} lives left";
            }

            Club.Save();
            return line;
        }

        private void Restart()
        {
            var md = Club.Master;
            var squad = Club.Data.Squad(md).Where(o => md.Rider(o.id) != null).Select(o => new RiderSlot(md.Rider(o.id), o.level));
            _state = new LevelState(_def, (ulong)DateTime.UtcNow.Ticks, squad);
            if (_board == null)
            {
                _board = BoardView.Create(_state.Board);
                _camera.transform.position = new Vector3(0f, 0.6f, -10f); // zoom follows the aspect in FitBeforeRender
            }
            else
            {
                _board.Sync(_state.Board);
            }

            _hud.HideEnd();
            _hud.Refresh(_state);
            _idle = 0f;
            _busy = false;
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += FitBeforeRender;

        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= FitBeforeRender;

        /// <summary>
        /// Zooms so the whole board fits, right before every render: a resized window, a rotated tablet or an offscreen
        /// capture at another shape all get the right zoom (position stays put, so screen shakes still work).
        /// </summary>
        private void FitBeforeRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera || _board == null)
            {
                return;
            }

            var size = _board.Bounds.size;
            float aspect = Mathf.Max(0.1f, camera.aspect);
            // Leave room above the board for the goals bar and below it for breathing space.
            camera.orthographicSize = Mathf.Max(size.y * 0.5f + 2.4f, (size.x * 0.5f + 0.5f) / aspect);
        }

        private void Update()
        {
            if (_busy || _state == null || _state.Outcome != LevelOutcome.Playing)
            {
                return;
            }

            _idle += Time.deltaTime;
            if (_idle >= HintDelay && !_board.IsHinting)
            {
                var hint = _hintBot.Choose(_state);
                if (hint.HasValue)
                {
                    _board.ShowHint(hint.Value.a, hint.Value.b);
                }
            }

            var pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                _idle = 0f;
                _board.StopHint();
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                _pressWorld = ScreenToWorld(pointer.position.ReadValue());
                _pressOnBoard = !overUi && _board.TryWorldToCell(_pressWorld, out _pressCell);
                _pressing = true;
            }

            if (_pressing && pointer.press.isPressed && _pressOnBoard)
            {
                var drag = ScreenToWorld(pointer.position.ReadValue()) - _pressWorld;
                if (drag.magnitude >= SwipeThreshold)
                {
                    _pressing = false;
                    var dir = Mathf.Abs(drag.x) > Mathf.Abs(drag.y) ? new Vector2Int(Math.Sign(drag.x), 0) : new Vector2Int(0, Math.Sign(drag.y));
                    PlayMove(_pressCell, new Cell(_pressCell.x + dir.x, _pressCell.y + dir.y)).Forget();
                }
            }

            if (pointer.press.wasReleasedThisFrame)
            {
                _pressing = false;
            }
        }

        private Vector3 ScreenToWorld(Vector2 screen)
        {
            var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            world.z = 0f;
            return world;
        }

        /// <summary>Plays one move: resolves it in Core, then lets the view replay it.</summary>
        public async UniTaskVoid PlayMove(Cell a, Cell b)
        {
            if (_busy || !MoveFinder.CanSwap(_state.Board, a, b))
            {
                return;
            }

            _busy = true;
            var events = new List<BoardEvent>();
            _state.TryMove(a, b, events);
            await _board.Play(events, _state.Board, OnStep);
            Finish();
        }

        /// <summary>Fires a full rider's power (a portrait tap): no move used, and the view replays it like a move.</summary>
        public async UniTaskVoid UsePower(int slot)
        {
            var events = new List<BoardEvent>();
            if (_busy || !_state.TryUsePower(slot, events))
            {
                return;
            }

            _busy = true;
            Haptics.Medium();
            await _board.Play(events, _state.Board, OnStep);
            Finish();
        }

        /// <summary>After a move or power: refresh the HUD and, if the level is over, pay out and show the end card.</summary>
        private void Finish()
        {
            _hud.Refresh(_state);
            if (_state.Outcome != LevelOutcome.Playing)
            {
                if (_state.Outcome == LevelOutcome.Won)
                {
                    Haptics.Medium();
                }

                _hud.ShowEnd(_state, hasNext: _def.id < Levels.Count, Settle());
            }

            _idle = 0f;
            _busy = false;
        }

        private void OnStep(int step, List<BoardEvent> events, int from, int to)
        {
            _audio.PlaySfx(_pop, 0.6f, 1f + Mathf.Min(step, 8) * 0.1f); // each cascade step a little higher
            for (int i = from; i < to; i++)
            {
                if (events[i].type == BoardEventType.SpecialCreated)
                {
                    Haptics.Light();
                    break;
                }
            }
        }

        private void GoHome() => Services.Get<GameFlow>().GoToAsync(AppState.Title).Forget();
    }
}
