using System;
using System.Collections.Generic;
using CadenceClub.Core;
using CadenceClub.UI;
using CadenceClub.View;
using Cysharp.Threading.Tasks;
using Template.Game.Flow;
using Template.Infra;
using Template.Infra.Audio;
using Template.Infra.Device;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CadenceClub
{
    /// <summary>Hand-made levels until the level files and editor arrive.</summary>
    public static class Levels
    {
        public static LevelDef First() => new LevelDef
        {
            id = 1,
            shape = new[] { ".......", ".......", ".......", ".......", ".......", ".......", ".......", "......." },
            colors = 4,
            moves = 20,
            goals = new[] { LevelDef.Collect(0, 15), LevelDef.Collect(2, 15) },
            seed = 1,
        };
    }

    /// <summary>
    /// The Game scene: owns one <see cref="LevelState"/>, turns swipes into moves, lets the <see cref="BoardView"/>
    /// replay each resolved move, and shows the end card. Input is locked while a move plays.
    /// </summary>
    public sealed class LevelController : MonoBehaviour
    {
        private const float SwipeThreshold = 0.35f;

        private LevelDef _def;
        private LevelState _state;
        private BoardView _board;
        private LevelHud _hud;
        private Camera _camera;
        private AudioService _audio;
        private AudioClip _pop;
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

            _def = Levels.First();
            _hud = LevelHud.Create(_def);
            _hud.RetryPressed += Restart;
            _hud.HomePressed += GoHome;
            AppLifecycle.BackPressed += GoHome;
            Restart();
        }

        private void OnDestroy() => AppLifecycle.BackPressed -= GoHome;

        private void Restart()
        {
            _state = new LevelState(_def, (ulong)DateTime.UtcNow.Ticks);
            if (_board == null)
            {
                _board = BoardView.Create(_state.Board);
                FitCamera();
            }
            else
            {
                _board.Sync(_state.Board);
            }

            _hud.HideEnd();
            _hud.Refresh(_state);
            _busy = false;
        }

        private void FitCamera()
        {
            var size = _board.Bounds.size;
            float aspect = Mathf.Max(0.1f, _camera.aspect);
            // Leave room above the board for the goals bar and below it for breathing space.
            _camera.orthographicSize = Mathf.Max(size.y * 0.5f + 2.4f, (size.x * 0.5f + 0.5f) / aspect);
            _camera.transform.position = new Vector3(0f, 0.6f, -10f);
        }

        private void Update()
        {
            if (_busy || _state == null || _state.Outcome != LevelOutcome.Playing || Pointer.current == null)
            {
                return;
            }

            var pointer = Pointer.current;
            if (pointer.press.wasPressedThisFrame)
            {
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
            _hud.Refresh(_state);
            if (_state.Outcome != LevelOutcome.Playing)
            {
                if (_state.Outcome == LevelOutcome.Won)
                {
                    Haptics.Medium();
                }

                _hud.ShowEnd(_state);
            }

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
