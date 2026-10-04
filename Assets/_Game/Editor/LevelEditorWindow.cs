using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CadenceClub.Art;
using CadenceClub.Core;
using UnityEditor;
using UnityEngine;

namespace CadenceClub.EditorTools
{
    /// <summary>
    /// Cadence Club → Level Editor. Paint the board (cells, holes, crates, ice, chains), set goals, moves and colours,
    /// then Simulate: the greedy bot plays the level N times through Core and reports the win rate against the level's
    /// target band. "Fit moves to band" picks the move limit that lands in the band. Saves level_NN.json in Resources.
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        private const float CellSize = 34f;

        private static readonly char[] Brushes = { '.', '#', '1', '2', 'o', 'i', 'l' };
        private static readonly string[] BrushNames = { "Cell", "Hole", "Crate ×1", "Crate ×2", "Oil", "Ice", "Chain" };
        private static readonly Color[] BrushColors =
        {
            new Color(0.30f, 0.36f, 0.45f), new Color(0.08f, 0.08f, 0.10f), new Color(0.85f, 0.60f, 0.35f),
            new Color(0.55f, 0.33f, 0.16f), new Color(0.23f, 0.18f, 0.33f), new Color(0.70f, 0.88f, 1.00f), new Color(0.62f, 0.66f, 0.72f),
        };

        private int _number = 1;
        private LevelDef _def;
        private int _brush;
        private bool _dirty;
        private int _runs = 1000;
        private int[] _needed;
        private string _report;
        private Vector2 _scroll;

        [MenuItem("Cadence Club/Level Editor", priority = 10)]
        public static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

        private void OnEnable() => Load(_number);

        private void OnGUI()
        {
            if (_def == null)
            {
                Load(_number);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawFileBar();
            EditorGUILayout.Space();
            DrawSettings();
            EditorGUILayout.Space();
            _brush = GUILayout.Toolbar(_brush, BrushNames);
            DrawGrid();
            EditorGUILayout.Space();
            DrawGoals();
            EditorGUILayout.Space();
            DrawSimulation();
            EditorGUILayout.EndScrollView();
        }

        private void DrawFileBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _number > 1;
                if (GUILayout.Button("◀", GUILayout.Width(28)))
                {
                    Switch(_number - 1);
                }

                GUI.enabled = true;
                int picked = EditorGUILayout.IntSlider("Level", _number, 1, Levels.Count);
                if (picked != _number)
                {
                    Switch(picked);
                }

                GUI.enabled = _number < Levels.Count;
                if (GUILayout.Button("▶", GUILayout.Width(28)))
                {
                    Switch(_number + 1);
                }

                GUI.enabled = _dirty;
                if (GUILayout.Button(_dirty ? "Save*" : "Save", GUILayout.Width(60)))
                {
                    Save();
                }

                if (GUILayout.Button("Revert", GUILayout.Width(60)))
                {
                    Load(_number);
                }

                GUI.enabled = true;
            }

            EditorGUILayout.LabelField(Levels.AssetPath(_number), EditorStyles.miniLabel);
        }

        private void DrawSettings()
        {
            EditorGUI.BeginChangeCheck();
            int width = EditorGUILayout.IntSlider("Width", _def.Width, 5, 10);
            int height = EditorGUILayout.IntSlider("Height", _def.Height, 5, 11);
            int colors = EditorGUILayout.IntSlider("Colours", _def.colors, 3, 6);
            int moves = EditorGUILayout.IntField("Moves", _def.moves);
            int seed = EditorGUILayout.IntField(new GUIContent("Seed", "Starting board for the player; the simulator uses its own seeds"), (int)_def.seed);
            if (EditorGUI.EndChangeCheck())
            {
                bool sameBoard = _def.Width == width && _def.Height == height && _def.colors == colors;
                Resize(width, height);
                _def.colors = colors;
                _def.moves = Mathf.Max(1, moves);
                _def.seed = (ulong)Mathf.Max(1, seed);
                Changed(keepSimulation: sameBoard);
            }
        }

        /// <summary>Click or drag to paint the selected brush; the top row is drawn first, as in the JSON.</summary>
        private void DrawGrid()
        {
            var area = GUILayoutUtility.GetRect(_def.Width * CellSize, _def.Height * CellSize, GUILayout.ExpandWidth(false));
            var e = Event.current;
            for (int row = 0; row < _def.Height; row++)
            {
                for (int x = 0; x < _def.Width; x++)
                {
                    var rect = new Rect(area.x + x * CellSize, area.y + row * CellSize, CellSize - 2, CellSize - 2);
                    char c = _def.shape[row][x];
                    int kind = Array.IndexOf(Brushes, c);
                    EditorGUI.DrawRect(rect, kind >= 0 ? BrushColors[kind] : Color.magenta);
                    if (c != '.' && c != '#')
                    {
                        GUI.Label(rect, c.ToString(), EditorStyles.centeredGreyMiniLabel);
                    }

                    bool paint = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && rect.Contains(e.mousePosition);
                    if (paint && c != Brushes[_brush])
                    {
                        var chars = _def.shape[row].ToCharArray();
                        chars[x] = Brushes[_brush];
                        var rows = (string[])_def.shape.Clone();
                        rows[row] = new string(chars);
                        _def.shape = rows;
                        Changed(keepSimulation: false);
                        e.Use();
                    }
                }
            }
        }

        private void DrawGoals()
        {
            EditorGUILayout.LabelField("Goals", EditorStyles.boldLabel);
            var goals = _def.goals.ToList();
            int remove = -1;
            bool added = false;
            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < goals.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var goal = goals[i];
                    goal.kind = (GoalKind)EditorGUILayout.EnumPopup(goal.kind, GUILayout.Width(90));
                    if (goal.kind == GoalKind.Collect)
                    {
                        var names = Enumerable.Range(0, _def.colors).Select(c => $"{c} {PieceArt.ShapeNames[c]}").ToArray();
                        goal.color = EditorGUILayout.Popup(Mathf.Clamp(goal.color, 0, _def.colors - 1), names, GUILayout.Width(110));
                    }
                    else
                    {
                        goal.color = Piece.NoColor;
                        GUILayout.Space(114);
                    }

                    goal.count = EditorGUILayout.IntField(goal.count, GUILayout.Width(60));
                    goals[i] = goal;
                    if (GUILayout.Button("−", GUILayout.Width(24)))
                    {
                        remove = i;
                    }
                }
            }

            if (GUILayout.Button("+ Goal", GUILayout.Width(80)))
            {
                goals.Add(LevelDef.Collect(0, 20));
                added = true;
            }

            if (remove >= 0)
            {
                goals.RemoveAt(remove);
            }

            if (EditorGUI.EndChangeCheck() || remove >= 0 || added)
            {
                _def.goals = goals.ToArray();
                Changed(keepSimulation: false);
            }

            var problems = _def.Validate();
            if (problems.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Error);
            }
        }

        private void DrawSimulation()
        {
            var band = LevelBands.For(_number);
            EditorGUILayout.LabelField($"Target: the greedy bot wins {band.min:P0}–{band.max:P0}", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _runs = Mathf.Clamp(EditorGUILayout.IntField("Runs", _runs), 50, 5000);
                GUI.enabled = _def.Validate().Count == 0;
                if (GUILayout.Button($"Simulate {_runs:N0} runs"))
                {
                    Simulate();
                }

                GUI.enabled = _needed != null;
                if (GUILayout.Button("Fit moves to band"))
                {
                    int fit = LevelSimulator.FitMoves(_needed, band);
                    if (fit < 0)
                    {
                        _report += "\nNo move limit lands in the band: change the goals or the board.";
                    }
                    else
                    {
                        _def.moves = fit;
                        Changed(keepSimulation: true);
                    }
                }

                GUI.enabled = true;
            }

            if (!string.IsNullOrEmpty(_report))
            {
                EditorGUILayout.HelpBox(_report, MessageType.Info);
            }
        }

        /// <summary>
        /// One greedy pass with a high move cap gives the win rate for every move limit (the bot ignores moves left),
        /// run in chunks so the progress bar can show and cancel.
        /// </summary>
        private void Simulate()
        {
            const int chunk = 50;
            int cap = Mathf.Max(_def.moves * 2, 60);
            var needed = new List<int>(_runs);
            try
            {
                for (int start = 0; start < _runs; start += chunk)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Simulating", $"Level {_number}: run {start + 1} of {_runs}", (float)start / _runs))
                    {
                        return;
                    }

                    int count = Mathf.Min(chunk, _runs - start);
                    needed.AddRange(LevelSimulator.MovesToWin(_def, count, BotKind.Greedy, (ulong)start + 1, cap));
                }

                _needed = needed.ToArray();
                var random = LevelSimulator.Run(_def, Mathf.Min(_runs, 200), BotKind.Random);
                _report = Report(random.WinRate);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private string Report(double? randomWinRate = null)
        {
            if (_needed == null)
            {
                return null;
            }

            var band = LevelBands.For(_number);
            double win = LevelSimulator.WinRate(_needed, _def.moves);
            var wins = _needed.Where(n => n <= _def.moves).ToList();
            double left = wins.Count > 0 ? wins.Average(n => _def.moves - n) : 0;
            bool inside = win >= band.min && win <= band.max;
            string verdict = inside ? "inside the band" : win < band.min ? "too hard for its band" : "too easy for its band";
            string report = $"Greedy bot: {win:P1} won with {_def.moves} moves, {left:0.0} moves left on wins — {verdict}.";
            if (randomWinRate.HasValue)
            {
                report += $"\nRandom bot (a first-timer): {randomWinRate.Value:P0}.";
            }

            return report;
        }

        private void Changed(bool keepSimulation)
        {
            _dirty = true;
            if (!keepSimulation)
            {
                _needed = null;
                _report = null;
            }
            else
            {
                _report = Report();
            }

            Repaint();
        }

        private void Switch(int number)
        {
            if (_dirty && !EditorUtility.DisplayDialog("Unsaved level", $"Level {_number} has unsaved changes.", "Discard", "Stay"))
            {
                return;
            }

            Load(number);
        }

        private void Load(int number)
        {
            _number = number;
            string path = Levels.AssetPath(number);
            _def = File.Exists(path)
                ? JsonUtility.FromJson<LevelDef>(File.ReadAllText(path))
                : LevelDef.Rectangle(8, 8, 5, 20, LevelDef.Collect(0, 20));
            _def.id = number;
            _dirty = !File.Exists(path);
            _needed = null;
            _report = null;
            GUI.FocusControl(null);
        }

        private void Save()
        {
            string path = Levels.AssetPath(_number);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(_def, true) + "\n");
            AssetDatabase.ImportAsset(path);
            _dirty = false;
        }

        /// <summary>Grows with plain cells or crops from the right and the bottom.</summary>
        private void Resize(int width, int height)
        {
            if (width == _def.Width && height == _def.Height)
            {
                return;
            }

            var rows = new string[height];
            for (int row = 0; row < height; row++)
            {
                string old = row < _def.Height ? _def.shape[row] : "";
                rows[row] = old.Length >= width ? old.Substring(0, width) : old + new string('.', width - old.Length);
            }

            _def.shape = rows;
        }
    }
}
