using System.Collections;
using System.IO;
using System.Reflection;
using CadenceClub.Core;
using CadenceClub.View;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Template.Core.Random;
using Template.Game.Flow;
using Template.Infra;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CadenceClub.PlayModeTests
{
    /// <summary>
    /// End-to-end check of the real game: boot, title, the Game scene, then the greedy bot plays moves through the
    /// actual <see cref="LevelController"/> until the level ends. Fails on any logged error, and on the board view
    /// drifting from the Core board (the view's resync warning). Screenshots go to Logs/screenshots.
    /// </summary>
    public class SmokeTests
    {
        private const int MaxMoves = 40;

        [UnityTest]
        public IEnumerator Boots_plays_a_level_with_the_bot_and_shows_the_end_card()
        {
            int drifts = 0;
            void OnLog(string message, string stack, LogType type)
            {
                if (message.Contains("[BoardView] View drifted"))
                {
                    drifts++;
                }
            }

            Application.logMessageReceived += OnLog;
            try
            {
                SceneManager.LoadScene("Boot");
                yield return WaitForScene("Title", 20f);
                yield return new WaitForSeconds(0.6f);
                Capture("0-title");

                Services.Get<GameFlow>().GoToAsync(AppState.Game).Forget();
                yield return WaitForScene("Game", 20f);
                yield return new WaitForSeconds(0.6f);

                var controller = Object.FindAnyObjectByType<LevelController>();
                Assert.IsNotNull(controller, "LevelController should exist in the Game scene");
                var stateField = typeof(LevelController).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
                var busyField = typeof(LevelController).GetField("_busy", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(stateField);
                Assert.IsNotNull(busyField);
                Capture("1-board");

                // Nobody moves for 5 s: the board hints a move.
                var board = (BoardView)typeof(LevelController).GetField("_board", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
                yield return new WaitForSeconds(5.5f);
                Assert.IsTrue(board.IsHinting, "a hint should show after 5 s without a move");
                Capture("1b-hint");

                var bot = new Bot(BotKind.Greedy, new SeededRandom(7));
                int moves = 0;
                var state = (LevelState)stateField.GetValue(controller);
                while (state.Outcome == LevelOutcome.Playing && moves < MaxMoves)
                {
                    var move = bot.Choose(state);
                    Assert.IsTrue(move.HasValue, "the generator and shuffler should always leave a move");
                    controller.PlayMove(move.Value.a, move.Value.b);
                    yield return null;
                    float t = 0f;
                    while ((bool)busyField.GetValue(controller))
                    {
                        t += Time.unscaledDeltaTime;
                        Assert.Less(t, 10f, "a move should finish animating within 10 s");
                        yield return null;
                    }

                    moves++;
                    if (moves == 3)
                    {
                        Capture("2-after-moves");
                    }
                }

                Assert.AreNotEqual(LevelOutcome.Playing, state.Outcome, $"the level should end within {MaxMoves} moves");
                Assert.AreEqual(state.MovesUsed, moves, "every bot move should count as one move");
                Assert.AreEqual(0, drifts, "the board view should match the Core board after every move");
                yield return new WaitForSecondsRealtime(0.5f);
                Capture(state.Outcome == LevelOutcome.Won ? "3-won" : "3-lost");

                Services.Get<GameFlow>().GoToAsync(AppState.Title).Forget();
                yield return WaitForScene("Title", 20f);
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
        }

        private static IEnumerator WaitForScene(string name, float timeout)
        {
            float t = 0f;
            while (SceneManager.GetActiveScene().name != name)
            {
                t += Time.unscaledDeltaTime;
                if (t > timeout)
                {
                    Assert.Fail($"Timed out waiting for scene {name}; active is {SceneManager.GetActiveScene().name}");
                }

                yield return null;
            }
        }

        /// <summary>Saves the screen at three shapes: 9:16 phone, 20:9 tall phone, 4:3 tablet.</summary>
        private static void Capture(string name)
        {
            string root = CameraShot.Folder("screenshots");
            CameraShot.Save(Path.Combine(root, name + ".png"));
            CameraShot.Save(Path.Combine(root, "tall", name + ".png"), 540, 1200);
            CameraShot.Save(Path.Combine(root, "tablet", name + ".png"), 768, 1024);
        }
    }
}
