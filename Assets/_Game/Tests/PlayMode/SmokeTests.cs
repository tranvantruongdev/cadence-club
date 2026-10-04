using System.Collections;
using System.IO;
using System.Reflection;
using CadenceClub.Core;
using CadenceClub.View;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Template.Core.Random;
using Template.Core.Save;
using Template.Game.Flow;
using Template.Infra;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CadenceClub.PlayModeTests
{
    /// <summary>
    /// End-to-end check of the real game: boot, title, the Game scene, then the greedy bot plays moves through the
    /// actual <see cref="LevelController"/> until the level ends — level 1, then the obstacle level. Fails on any
    /// logged error, and on the board view drifting from the Core board (the view's resync warning).
    /// Screenshots go to Logs/screenshots.
    /// </summary>
    public class SmokeTests
    {
        private const int MaxMoves = 40;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTest]
        public IEnumerator Boots_plays_levels_with_the_bot_and_shows_the_end_card()
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

                Levels.Override = 1; // a save from an earlier run could be further along
                yield return EnterGame();
                var controller = Object.FindAnyObjectByType<LevelController>();
                Capture("level1-start");

                // Nobody moves for 5 s: the board hints a move.
                var board = (BoardView)typeof(LevelController).GetField("_board", Private).GetValue(controller);
                yield return new WaitForSeconds(5.5f);
                Assert.IsTrue(board.IsHinting, "a hint should show after 5 s without a move");
                Capture("level1-hint");

                yield return PlayToEnd(controller, "level1");
                Assert.AreEqual(0, drifts, "level 1: the board view should match the Core board after every move");
                if (State(controller).Outcome == LevelOutcome.Won)
                {
                    Assert.GreaterOrEqual(Services.Get<SaveService>().Data.level, 2, "winning level 1 unlocks level 2");

                    // "Next level" rebuilds the board for level 2's shape.
                    typeof(LevelController).GetMethod("NextLevel", Private).Invoke(controller, null);
                    yield return new WaitForSeconds(0.5f);
                    Assert.AreEqual(2, State(controller).Def.id);
                    Assert.AreEqual(LevelOutcome.Playing, State(controller).Outcome);
                    Capture("level2-start");
                }

                yield return Services.Get<GameFlow>().GoToAsync(AppState.Title).ToCoroutine(); // the whole transition: GameFlow ignores requests mid-fade
                yield return WaitForScene("Title", 20f);
                yield return new WaitForSeconds(0.4f);
                Capture("0b-title-after-win");

                // Level 20, a bump: a ring of crates around ice, chains in the corners.
                Levels.Override = 20;
                yield return EnterGame();
                controller = Object.FindAnyObjectByType<LevelController>();
                Capture("obstacles-start");
                yield return PlayToEnd(controller, "obstacles");
                Assert.AreEqual(0, drifts, "obstacles: the board view should match the Core board after every move");

                yield return Services.Get<GameFlow>().GoToAsync(AppState.Title).ToCoroutine(); // the whole transition: GameFlow ignores requests mid-fade
                yield return WaitForScene("Title", 20f);
            }
            finally
            {
                Levels.Override = null;
                Application.logMessageReceived -= OnLog;
            }
        }

        private static LevelState State(LevelController controller) =>
            (LevelState)typeof(LevelController).GetField("_state", Private).GetValue(controller);

        private static IEnumerator EnterGame()
        {
            yield return Services.Get<GameFlow>().GoToAsync(AppState.Game).ToCoroutine();
            yield return WaitForScene("Game", 20f);
            yield return new WaitForSeconds(0.6f);
            Assert.IsNotNull(Object.FindAnyObjectByType<LevelController>(), "LevelController should exist in the Game scene");
        }

        /// <summary>The greedy bot plays through the real controller, one animated move at a time, until the level ends.</summary>
        private static IEnumerator PlayToEnd(LevelController controller, string name)
        {
            var state = (LevelState)typeof(LevelController).GetField("_state", Private).GetValue(controller);
            var busy = typeof(LevelController).GetField("_busy", Private);
            var bot = new Bot(BotKind.Greedy, new SeededRandom(7));
            int moves = 0;
            while (state.Outcome == LevelOutcome.Playing && moves < MaxMoves)
            {
                var move = bot.Choose(state);
                Assert.IsTrue(move.HasValue, "the generator and shuffler should always leave a move");
                controller.PlayMove(move.Value.a, move.Value.b);
                yield return null;
                float t = 0f;
                while ((bool)busy.GetValue(controller))
                {
                    t += Time.unscaledDeltaTime;
                    Assert.Less(t, 10f, "a move should finish animating within 10 s");
                    yield return null;
                }

                moves++;
                if (moves == 3)
                {
                    Capture($"{name}-after-moves");
                }
            }

            Assert.AreNotEqual(LevelOutcome.Playing, state.Outcome, $"{name}: the level should end within {MaxMoves} moves");
            Assert.AreEqual(state.MovesUsed, moves, $"{name}: every bot move should count as one move");
            yield return new WaitForSecondsRealtime(0.5f);
            Capture($"{name}-{(state.Outcome == LevelOutcome.Won ? "won" : "lost")}");
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
