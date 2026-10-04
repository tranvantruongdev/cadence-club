using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Template.Core.Random;

namespace CadenceClub.Core.Tests
{
    /// <summary>
    /// The shipped level files (Assets/_Game/Resources/Levels): each is valid and the greedy bot wins it inside its
    /// target band over 200 runs, seeds 1–200 — the runs it was tuned with. dotnet only (this file lives outside
    /// Assets): Unity's editor runtime took 4 minutes for these simulations, dotnet takes seconds, and the core-tests
    /// workflow runs without a Unity licence. Unity's LevelFileTests checks that JsonUtility reads the same files.
    /// </summary>
    public class LevelFileBandTests
    {
        private const int LevelCount = 30;

        private static string Folder()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Game", "Resources", "Levels")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Assets/_Game/Resources/Levels not found above the test folder");
            return Path.Combine(dir.FullName, "Assets", "_Game", "Resources", "Levels");
        }

        private static IEnumerable<int> Numbers() => Enumerable.Range(1, LevelCount);

        [Test]
        public void There_is_one_file_per_level()
        {
            var files = Directory.GetFiles(Folder(), "level_*.json").Select(Path.GetFileName).OrderBy(f => f).ToList();
            CollectionAssert.AreEqual(Numbers().Select(n => $"level_{n:00}.json"), files);
        }

        private static LevelDef Load(int number) =>
            JsonConvert.DeserializeObject<LevelDef>(File.ReadAllText(Path.Combine(Folder(), $"level_{number:00}.json")));

        /// <summary>
        /// The first session plays levels on their file's seed, with the greedy bot as the hint: level 2's first hint
        /// makes a rocket, level 3's a bomb. On failure the message names a seed that works.
        /// </summary>
        [TestCase(2, Special.RocketH)]
        [TestCase(3, Special.Bomb)]
        public void First_session_levels_open_on_their_lesson(int number, Special lesson)
        {
            var def = Load(number);
            bool Teaches(ulong seed)
            {
                var state = new LevelState(def, seed);
                var hint = new Bot(BotKind.Greedy, new SeededRandom(1)).Choose(state); // LevelController's hint bot
                if (!hint.HasValue)
                {
                    return false;
                }

                MoveResolver.Swap(state.Board, hint.Value.a, hint.Value.b);
                return MatchFinder.Find(state.Board, hint.Value.a, hint.Value.b)
                    .Any(g => g.creates == lesson || (lesson == Special.RocketH && g.creates == Special.RocketV));
            }

            var works = Enumerable.Range(1, 500).Select(s => (ulong)s).FirstOrDefault(Teaches);
            Assert.IsTrue(Teaches(def.seed), $"level {number}'s seed {def.seed} doesn't open on a {lesson}; seed {works} does");
        }

        [TestCaseSource(nameof(Numbers))]
        public void Level_is_valid_and_wins_inside_its_band(int number)
        {
            var def = Load(number);
            CollectionAssert.IsEmpty(def.Validate());
            Assert.AreEqual(number, def.id);

            var band = LevelBands.For(number);
            var result = LevelSimulator.Run(def, 200, BotKind.Greedy);
            TestContext.WriteLine($"level {number}, {def.moves} moves: {result}");
            Assert.That(result.WinRate, Is.InRange(band.min, band.max), $"level {number}: target {band.min:P0}–{band.max:P0}");
        }
    }
}
