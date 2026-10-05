using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CadenceClub.Core;
using CadenceClub.UI;
using CadenceClub.View;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Template.Game.Flow;
using Template.Infra;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CadenceClub.PlayModeTests
{
    /// <summary>
    /// Records the trailer's frames (the plan's shot list): a disco + rocket combo and the victory lap, two rider
    /// powers, an SSR reveal, a renovation that completes an area, and an end card. Explicit, so normal runs skip it:
    ///   Tools/run-unity-tests.ps1 -TestPlatform PlayMode -Graphics -TestFilter CadenceClub.PlayModeTests.TrailerCapture
    /// then Tools/make-trailer.ps1 turns Logs/frames into docs/cadence-club-trailer.mp4 and docs/cadence-club.gif.
    /// Starts from a fresh save and puts the developer's save back afterwards.
    /// </summary>
    [Explicit("Records trailer frames; run on demand")]
    public class TrailerCapture
    {
        private const int Fps = 30;
        private const int Width = 720;
        private const int Height = 1280;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private int _frame;
        private string _folder;
        private string _savePath;
        private System.Collections.Generic.Dictionary<string, byte[]> _keptSave;

        [SetUp]
        public void SetUp()
        {
            _folder = CameraShot.Folder("frames");
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }

            Directory.CreateDirectory(_folder);
            _frame = 0;
            _savePath = new Template.Infra.Save.FileSaveStore().FilePath;
            _keptSave = SaveFiles().Where(File.Exists).ToDictionary(p => p, p => File.ReadAllBytes(p));
            foreach (var path in _keptSave.Keys)
            {
                File.Delete(path);
            }

            Time.captureFramerate = Fps; // game time advances 1/30 s per frame, however long a frame takes to save
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            Levels.Override = null;
            foreach (var path in SaveFiles().Where(File.Exists))
            {
                File.Delete(path);
            }

            foreach (var kept in _keptSave)
            {
                File.WriteAllBytes(kept.Key, kept.Value);
            }
        }

        private string[] SaveFiles() => new[] { _savePath, _savePath + ".tmp", _savePath + ".bak" };

        [UnityTest]
        public IEnumerator Records_trailer_frames()
        {
            Assume.That(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "needs graphics (run without -nographics)");

            // A fresh save boots straight into level 1.
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Game");
            yield return Record(0.6f);

            // 0–5 s: a disco swapped with a rocket turns every red into a rocket; the win's moves left go off after.
            var controller = Object.FindAnyObjectByType<LevelController>();
            var state = State(controller);
            var board = (BoardView)typeof(LevelController).GetField("_board", Private).GetValue(controller);
            var a = new Cell(3, 3);
            var b = new Cell(4, 3);
            state.Board[a] = Piece.Make(Piece.NoColor, Special.Disco);
            state.Board[b] = Piece.Make(0, Special.RocketH);
            board.Sync(state.Board);
            yield return Record(0.5f);
            controller.PlayMove(a, b);
            yield return RecordWhileBusy(controller);
            var bot = new Bot(BotKind.Greedy, new Template.Core.Random.SeededRandom(3));
            for (int i = 0; i < 6 && state.Outcome == LevelOutcome.Playing; i++)
            {
                var move = bot.Choose(state);
                controller.PlayMove(move.Value.a, move.Value.b);
                yield return RecordWhileBusy(controller); // the winning move, then the victory lap
            }

            yield return Record(1.6f); // the end card

            // 5–12 s: level 20 with Linh and Bà Tư; both powers go off.
            var club = Club.Data;
            var md = Club.Master;
            club.level = 7;
            club.Grant(md, "linh");
            club.Grant(md, "ba_tu");
            club.SetSquad(md, 0, "linh");
            club.SetSquad(md, 1, "ba_tu");
            Levels.Override = 20;
            yield return Services.Get<GameFlow>().GoToAsync(AppState.Game).ToCoroutine();
            yield return WaitForScene("Game");
            controller = Object.FindAnyObjectByType<LevelController>();
            state = State(controller);
            var hud = (LevelHud)typeof(LevelController).GetField("_hud", Private).GetValue(controller);
            yield return Record(0.6f);
            foreach (var rider in state.Squad)
            {
                typeof(RiderSlot).GetProperty("Charge").SetValue(rider, rider.Def.charge);
            }

            hud.Refresh(state);
            yield return Record(0.8f); // the portraits glow
            controller.UsePower(0);
            yield return RecordWhileBusy(controller);
            controller.UsePower(1);
            yield return RecordWhileBusy(controller);
            yield return Record(0.6f);

            // 12–22 s: Recruit, a 10-pull whose last card is the pity SSR.
            Levels.Override = null;
            yield return Services.Get<GameFlow>().GoToAsync(AppState.Title).ToCoroutine();
            yield return WaitForScene("Title");
            var home = Object.FindAnyObjectByType<HomeController>();
            var stack = (ScreenStack)typeof(HomeController).GetField("_stack", Private).GetValue(home);
            yield return Wait(1f); // Home opens the daily gift on arrival; take it off-camera
            if (stack.Top is DailyLoginScreen)
            {
                yield return stack.PopAsync().ToCoroutine();
            }

            var banner = md.Banner(md.Text("banner"));
            club.gems = Mathf.Max(club.gems, md.Int("ten_pull_cost"));
            club.pity = banner.pity - 10;
            var recruit = (RecruitScreen)typeof(HomeController).GetField("_recruit", Private).GetValue(home);
            var reveal = (RevealScreen)typeof(HomeController).GetField("_reveal", Private).GetValue(home);
            recruit.OpenAsync().Forget();
            yield return Record(1.2f);
            recruit.PullAsync(10).Forget();
            var closed = typeof(RevealScreen).GetField("_closed", Private);
            for (int i = 0; i < Fps * 20 && closed.GetValue(reveal) == null; i++)
            {
                yield return Shot(); // until the summary is up
            }

            yield return Record(1.8f);
            typeof(RevealScreen).GetMethod("Close", Private).Invoke(reveal, null);
            yield return Wait(0.5f);
            yield return stack.PopAsync().ToCoroutine();

            // 24–31 s: the workshop's last two tasks pop in; the second completes it and tells its story beat.
            var area = md.Tasks.Where(t => t.area == club.CurrentArea(md).id).ToList();
            foreach (var task in area.Take(area.Count - 2).Where(t => !club.IsBuilt(t.id)))
            {
                club.built.Add(task.id);
            }

            club.stars = Mathf.Max(club.stars, 2);
            typeof(HomeController).GetMethod("BuildArea", Private).Invoke(home, new object[] { null });
            yield return Record(0.8f);
            var build = typeof(HomeController).GetMethod("Build", Private);
            build.Invoke(home, new object[] { area[area.Count - 2] });
            yield return Record(1.4f);
            build.Invoke(home, new object[] { area.Last() });
            yield return Record(3.6f);

            // End card.
            EndCard();
            yield return Record(3f);
        }

        /// <summary>The trailer's last frames: name, pitch, how it's made, where to find it.</summary>
        private static void EndCard()
        {
            var theme = UiTheme.Current;
            var canvas = UiFactory.CreateCanvas("End card", 500);
            UiFactory.CreatePanel(canvas.transform, ClubUi.Night);
            UiFactory.CreateText(canvas.transform, "Cadence Club", 128, new Vector2(0f, 220f), new Vector2(1000f, 180f), TextAlignmentOptions.Center, UiFont.Display)
                .color = theme.accent;
            UiFactory.CreateText(canvas.transform, "Match-3 to save your cycling club.\nRiders are your boosters.", 52, new Vector2(0f, 40f),
                new Vector2(980f, 160f)).color = Color.white;
            UiFactory.CreateText(canvas.transform, "Unity 6 · C# · pure C# board model · bot-tuned levels", 38, new Vector2(0f, -120f),
                new Vector2(980f, 70f)).color = new Color(1f, 1f, 1f, 0.7f);
            UiFactory.CreateText(canvas.transform, "github.com/tranvantruongdev/cadence-club", 40, new Vector2(0f, -260f), new Vector2(1000f, 70f),
                TextAlignmentOptions.Center, UiFont.Display).color = theme.accent;
        }

        private static LevelState State(LevelController controller) =>
            (LevelState)typeof(LevelController).GetField("_state", Private).GetValue(controller);

        private IEnumerator RecordWhileBusy(LevelController controller)
        {
            var busy = typeof(LevelController).GetField("_busy", Private);
            yield return Shot(); // PlayMove/UsePower set busy on their first frame
            for (int i = 0; i < Fps * 20 && (bool)busy.GetValue(controller); i++)
            {
                yield return Shot();
            }
        }

        private static IEnumerator WaitForScene(string name)
        {
            for (int i = 0; i < Fps * 30 && SceneManager.GetActiveScene().name != name; i++)
            {
                yield return null;
            }

            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds * Fps); i++)
            {
                yield return null;
            }
        }

        private IEnumerator Record(float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds * Fps); i++)
            {
                yield return Shot();
            }
        }

        private IEnumerator Shot()
        {
            yield return null; // CameraShot renders the camera itself, so any point in the frame works
            CameraShot.Save(Path.Combine(_folder, $"frame_{_frame++:D4}.tga"), Width, Height);
        }
    }
}
