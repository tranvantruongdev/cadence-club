using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;

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

        [TestCaseSource(nameof(Numbers))]
        public void Level_is_valid_and_wins_inside_its_band(int number)
        {
            var def = JsonConvert.DeserializeObject<LevelDef>(File.ReadAllText(Path.Combine(Folder(), $"level_{number:00}.json")));
            CollectionAssert.IsEmpty(def.Validate());
            Assert.AreEqual(number, def.id);

            var band = LevelBands.For(number);
            var result = LevelSimulator.Run(def, 200, BotKind.Greedy);
            TestContext.WriteLine($"level {number}, {def.moves} moves: {result}");
            Assert.That(result.WinRate, Is.InRange(band.min, band.max), $"level {number}: target {band.min:P0}–{band.max:P0}");
        }
    }
}
