using System.Collections;
using System.IO;
using System.Linq;
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
    /// End-to-end check of the real game from a fresh save: the first session (boot into level 1, level 3, Home), the
    /// meta screens, then the greedy bot plays moves through the actual <see cref="LevelController"/> until the level
    /// ends — level 1 with boosters, then the obstacle level. Fails on any logged error, and on the board view drifting
    /// from the Core board (the view's resync warning).
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
            string shots = CameraShot.Folder("screenshots");
            if (Directory.Exists(shots))
            {
                Directory.Delete(shots, true); // a lost level writes "-lost": an old "-won" shot would mislead
            }

            // Start from a fresh save, like a first install (and like CI); the developer's save files go back afterwards.
            string savePath = new Template.Infra.Save.FileSaveStore().FilePath;
            var keptSave = new[] { savePath, savePath + ".tmp", savePath + ".bak" }.Where(File.Exists).ToDictionary(p => p, p => File.ReadAllBytes(p));
            foreach (var path in keptSave.Keys)
            {
                File.Delete(path);
            }

            try
            {
                // First session: a new player boots straight into level 1, and a fingertip shows the first swap.
                SceneManager.LoadScene("Boot");
                yield return WaitForScene("Game", 20f);
                yield return new WaitForSeconds(2f);
                var controller = Object.FindAnyObjectByType<LevelController>();
                var board = (BoardView)typeof(LevelController).GetField("_board", Private).GetValue(controller);
                Assert.AreEqual(1, State(controller).Def.id, "a new player starts in level 1");
                Assert.IsTrue(board.IsHinting && board.transform.Find("Finger").gameObject.activeSelf, "the first session shows a fingertip hint at once");
                Capture("first-level1-finger");
                yield return PlayToEnd(controller, "first-level1");
                Assert.AreEqual(LevelOutcome.Won, State(controller).Outcome, "the bot wins level 1's designed board");
                AssertWinCardOnlyLeadsOn(controller, "Next level");

                // Level 3, as if level 2 were won: its designed board opens on a bomb, and winning it leads Home.
                Club.Data.level = 3;
                typeof(LevelController).GetMethod("Play", Private).Invoke(controller, new object[] { 3 });
                yield return new WaitForSeconds(2f);
                Capture("first-level3-hint");
                yield return PlayToEnd(controller, "first-level3");
                Assert.AreEqual(LevelOutcome.Won, State(controller).Outcome, "the bot wins level 3's designed board");
                AssertWinCardOnlyLeadsOn(controller, "Continue");
                typeof(LevelController).GetMethod("NextLevel", Private).Invoke(controller, null);
                yield return WaitForScene("Title", 20f);
                yield return new WaitForSeconds(0.6f);
                Capture("0-title");

                var home = Object.FindAnyObjectByType<HomeController>();
                Assert.IsNotNull(home, "Home opens after the first session");
                var homeClub = Club.Data;
                var homeMd = Club.Master;
                var stack = (Template.UI.ScreenStack)typeof(HomeController).GetField("_stack", Private).GetValue(home);

                // Home greets with the free rider's reveal, then the daily gift.
                var freeReveal = (CadenceClub.UI.RevealScreen)typeof(HomeController).GetField("_reveal", Private).GetValue(home);
                Assert.AreSame(freeReveal, stack.Top, "the free rider from level 3 is revealed on arrival");
                freeReveal.Skip();
                yield return new WaitForSeconds(0.6f);
                Capture("first-free-rider");
                typeof(CadenceClub.UI.RevealScreen).GetMethod("Close", Private).Invoke(freeReveal, null);
                yield return new WaitForSeconds(0.6f);
                var daily = (CadenceClub.UI.DailyLoginScreen)typeof(HomeController).GetField("_daily", Private).GetValue(home);
                Assert.AreSame(daily, stack.Top, "the daily gift opens after the reveal");
                Capture("0-daily-gift");
                int gemsBeforeGift = homeClub.gems;
                typeof(CadenceClub.UI.DailyLoginScreen).GetMethod("Claim", Private).Invoke(daily, null);
                Assert.Greater(homeClub.gems, gemsBeforeGift, "the daily gift pays gems");
                yield return new WaitForSeconds(0.4f);
                Capture("0-daily-claimed");
                yield return stack.PopAsync().ToCoroutine();
                Assert.IsNotNull(GameObject.Find("Pointer"), "nothing built yet: a fingertip points at the first task");
                Capture("first-home-pointer");

                // Home: build a renovation task with a star.
                var task = homeMd.Tasks.FirstOrDefault(t => !homeClub.IsBuilt(t.id) && t.area == homeClub.CurrentArea(homeMd).id);
                if (task != null)
                {
                    homeClub.stars += task.stars;
                    int built = homeClub.built.Count;
                    typeof(HomeController).GetMethod("Build", Private).Invoke(home, new object[] { task });
                    Assert.AreEqual(built + 1, homeClub.built.Count, "a ★ builds the task");
                    yield return null; // the old area is destroyed at the end of the frame
                    Assert.IsNull(GameObject.Find("Pointer"), "the pointer goes once something is built");
                    yield return new WaitForSeconds(0.6f);
                    Capture("0a-home-built");
                }

                // Recruit: a 10-pull, the reveal mid-way and skipped to its summary; then the collection and a rider's card.
                homeClub.level = Mathf.Max(homeClub.level, homeMd.Int("recruit_after_level") + 1);
                homeClub.gems = Mathf.Max(homeClub.gems, homeMd.Int("ten_pull_cost"));
                var recruit = (CadenceClub.UI.RecruitScreen)typeof(HomeController).GetField("_recruit", Private).GetValue(home);
                var reveal = (CadenceClub.UI.RevealScreen)typeof(HomeController).GetField("_reveal", Private).GetValue(home);
                recruit.OpenAsync().Forget();
                yield return new WaitForSeconds(0.6f);
                Capture("0d-recruit");
                int gems = homeClub.gems;
                recruit.PullAsync(10).Forget();
                yield return new WaitForSeconds(2.2f);
                Capture("0e-reveal");
                Assert.AreEqual(gems - homeMd.Int("ten_pull_cost"), homeClub.gems, "a 10-pull costs its gems");
                reveal.Skip();
                yield return new WaitForSeconds(0.6f);
                Capture("0f-reveal-summary");
                typeof(CadenceClub.UI.RevealScreen).GetMethod("Close", Private).Invoke(reveal, null);
                yield return new WaitForSeconds(0.5f);
                yield return stack.PopAsync().ToCoroutine(); // Recruit

                var riders = (CadenceClub.UI.RidersScreen)typeof(HomeController).GetField("_riders", Private).GetValue(home);
                riders.OpenAsync().Forget();
                yield return new WaitForSeconds(0.6f);
                Capture("0g-riders");
                riders.OpenDetailAsync(homeClub.riders[0].id).Forget();
                yield return new WaitForSeconds(0.6f);
                Capture("0h-rider-detail");
                yield return stack.PopAsync().ToCoroutine();
                yield return stack.PopAsync().ToCoroutine();

                // Demo shop: a free pack adds its gems.
                var shop = (CadenceClub.UI.ShopScreen)typeof(HomeController).GetField("_shop", Private).GetValue(home);
                shop.OpenAsync().Forget();
                yield return new WaitForSeconds(0.6f);
                Capture("0i-shop");
                int gemsBeforeShop = homeClub.gems;
                typeof(CadenceClub.UI.ShopScreen).GetMethod("Claim", Private).Invoke(shop, new object[] { "handful" });
                Assert.AreEqual(gemsBeforeShop + 100, homeClub.gems, "a demo pack adds its gems");
                yield return stack.PopAsync().ToCoroutine();

                // Level start: pick two boosters, then Play pays for them and they start on the board.
                homeClub.coins = Mathf.Max(homeClub.coins, 1000);
                homeClub.lives = homeMd.Int("lives_max");
                var levelStart = (CadenceClub.UI.LevelStartScreen)typeof(HomeController).GetField("_levelStart", Private).GetValue(home);
                levelStart.OpenAsync(Levels.Load(1)).Forget();
                yield return new WaitForSeconds(0.6f);
                var toggle = typeof(CadenceClub.UI.LevelStartScreen).GetMethod("Toggle", Private);
                toggle.Invoke(levelStart, new object[] { homeMd.Boosters.First(b => b.id == "rockets") });
                toggle.Invoke(levelStart, new object[] { homeMd.Boosters.First(b => b.id == "bomb") });
                yield return null;
                Capture("0c-level-start");
                int coinsBeforeBoosters = homeClub.coins;
                Levels.Override = 1; // a save from an earlier run could be further along
                typeof(CadenceClub.UI.LevelStartScreen).GetMethod("Play", Private).Invoke(levelStart, null);
                Assert.AreEqual(coinsBeforeBoosters - homeClub.BoostersCost(homeMd, new[] { "rockets", "bomb" }), homeClub.coins, "Play pays for the boosters");
                yield return WaitForScene("Game", 20f);
                yield return new WaitForSeconds(0.6f);
                controller = Object.FindAnyObjectByType<LevelController>();
                var startBoard = State(controller).Board;
                int specials = Enumerable.Range(0, startBoard.Height).Sum(y => Enumerable.Range(0, startBoard.Width).Count(x => startBoard[x, y].IsSpecial));
                Assert.GreaterOrEqual(specials, 3, "the 2 rockets and the bomb start on the board");
                Capture("level1-start");

                // Nobody moves for 5 s: the board hints a move.
                board = (BoardView)typeof(LevelController).GetField("_board", Private).GetValue(controller);
                yield return new WaitForSeconds(5.5f);
                Assert.IsTrue(board.IsHinting, "a hint should show after 5 s without a move");
                Capture("level1-hint");

                yield return PlayToEnd(controller, "level1");
                Assert.AreEqual(0, drifts, "level 1: the board view should match the Core board after every move");
                if (State(controller).Outcome == LevelOutcome.Won)
                {
                    Assert.GreaterOrEqual(Club.Data.level, 2, "winning level 1 unlocks level 2");

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

                // Level 20, a bump: a ring of crates around ice, chains in the corners. Squad: Linh (row rockets)
                // and Bà Tư (breaks obstacles).
                var club = Club.Data;
                var md = Club.Master;
                club.Grant(md, "linh");
                club.Grant(md, "ba_tu");
                club.level = Mathf.Max(club.level, md.Int("recruit_after_level") + 1); // two squad slots
                Assert.IsTrue(club.SetSquad(md, 0, "linh") && club.SetSquad(md, 1, "ba_tu"));
                Levels.Override = 20;
                yield return EnterGame();
                controller = Object.FindAnyObjectByType<LevelController>();
                Assert.AreEqual(2, State(controller).Squad.Count, "the squad comes into the level");
                Capture("obstacles-start");

                // Fill Bà Tư's charge, show the glowing portrait, then fire her power.
                var batu = State(controller).Squad[1];
                typeof(RiderSlot).GetProperty("Charge").SetValue(batu, batu.Def.charge);
                var levelHud = (CadenceClub.UI.LevelHud)typeof(LevelController).GetField("_hud", Private).GetValue(controller);
                levelHud.Refresh(State(controller));
                yield return new WaitForSeconds(0.4f);
                Capture("obstacles-rider-full");
                int movesBefore = State(controller).MovesLeft;
                controller.UsePower(1);
                yield return null;
                yield return WaitIdle(controller);
                Assert.AreEqual(movesBefore, State(controller).MovesLeft, "a power uses no move");
                Assert.AreEqual(0, drifts, "the board view should match the Core board after a power");
                Capture("obstacles-after-power");

                // Cut to 2 moves so the level is lost, then buy +5 moves: it carries on and no life is charged.
                typeof(LevelState).GetProperty("MovesLeft").SetValue(State(controller), 2);
                yield return PlayToEnd(controller, "obstacles-short");
                Assert.AreEqual(LevelOutcome.Lost, State(controller).Outcome, "2 moves can't clear level 20");
                club.coins = Mathf.Max(club.coins, md.Int("extra_moves_cost"));
                int coinsBeforeContinue = club.coins;
                int livesBeforeContinue = club.Lives(md, Club.Now);
                typeof(LevelController).GetMethod("ContinueLevel", Private).Invoke(controller, null);
                Assert.AreEqual((LevelOutcome.Playing, md.Int("extra_moves")), (State(controller).Outcome, State(controller).MovesLeft));
                Assert.AreEqual(coinsBeforeContinue - md.Int("extra_moves_cost"), club.coins, "+5 moves cost their coins");
                Assert.AreEqual(livesBeforeContinue, club.Lives(md, Club.Now), "continuing costs no life");
                yield return new WaitForSeconds(0.3f);
                Capture("obstacles-continued");

                yield return PlayToEnd(controller, "obstacles");
                Assert.AreEqual(0, drifts, "obstacles: the board view should match the Core board after every move");

                yield return Services.Get<GameFlow>().GoToAsync(AppState.Title).ToCoroutine(); // the whole transition: GameFlow ignores requests mid-fade
                yield return WaitForScene("Title", 20f);
            }
            finally
            {
                Levels.Override = null;
                Application.logMessageReceived -= OnLog;
                foreach (var path in new[] { savePath, savePath + ".tmp", savePath + ".bak" }.Where(File.Exists))
                {
                    File.Delete(path);
                }

                foreach (var kept in keptSave)
                {
                    File.WriteAllBytes(kept.Key, kept.Value);
                }
            }
        }

        /// <summary>First-session win card: one button on (labelled <paramref name="next"/>), no Home.</summary>
        private static void AssertWinCardOnlyLeadsOn(LevelController controller, string next)
        {
            var hud = typeof(LevelController).GetField("_hud", Private).GetValue(controller);
            GameObject Button(string field) => (GameObject)hud.GetType().GetField(field, Private).GetValue(hud);
            var label = Button("_next").GetComponentsInChildren<Component>().First(c => c.GetType().Name == "TextMeshProUGUI");
            Assert.IsTrue(Button("_next").activeSelf && !Button("_home").activeSelf && !Button("_retry").activeSelf, "the win card only leads on");
            Assert.AreEqual(next, label.GetType().GetProperty("text").GetValue(label));
        }

        private static LevelState State(LevelController controller) =>
            (LevelState)typeof(LevelController).GetField("_state", Private).GetValue(controller);

        private static IEnumerator WaitIdle(LevelController controller)
        {
            var busy = typeof(LevelController).GetField("_busy", Private);
            float t = 0f;
            while ((bool)busy.GetValue(controller))
            {
                t += Time.unscaledDeltaTime;
                Assert.Less(t, 10f, "a move or power should finish animating within 10 s");
                yield return null;
            }
        }

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
            int usedBefore = state.MovesUsed; // a continued level is played to its end twice
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
            Assert.AreEqual(moves, state.MovesUsed - usedBefore, $"{name}: every bot move should count as one move");
            var hud = typeof(LevelController).GetField("_hud", Private).GetValue(controller);
            var bodyLabel = hud.GetType().GetField("_endBody", Private).GetValue(hud);
            string body = (string)bodyLabel.GetType().GetProperty("text").GetValue(bodyLabel);
            StringAssert.Contains("coins", body, $"{name}: the end card shows the payout, or the coins to keep going");
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
