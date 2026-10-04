using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CadenceClub.Core.Tests
{
    /// <summary>The shipped master data (Assets/_Game/Resources/MasterData/*.csv) parses and validates.</summary>
    public class MasterDataFileTests
    {
        private static string ProjectRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Game", "Resources", "MasterData")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Assets/_Game/Resources/MasterData not found above the test folder");
            return dir.FullName;
        }

        /// <summary>
        /// Everything the player reads has a Vietnamese and a Japanese row in strings.csv: each Loc.T/Loc.F literal in the
        /// game's code, the master data shown on screen (areas, stories, tasks, roles, banner, boosters, shop packs), and
        /// the template's Settings labels (translated through UiFactory.Localize).
        /// </summary>
        [Test]
        public void Every_text_on_screen_has_vietnamese_and_japanese()
        {
            var md = Load();
            string root = ProjectRoot();
            var texts = new HashSet<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Assets", "_Game", "Scripts", "Runtime"), "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"Loc\.[TF]\(""((?:[^""\\]|\\.)*)"""))
                {
                    texts.Add(Regex.Unescape(m.Groups[1].Value));
                }
            }

            string settings = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Runtime", "UI", "SettingsPanelView.cs"));
            foreach (Match m in Regex.Matches(settings, @"Create(?:Text|Slider|Toggle|Button)\(card, ""([^""]+)"""))
            {
                texts.Add(m.Groups[1].Value);
            }

            texts.UnionWith(md.Areas.SelectMany(a => new[] { a.name, a.story }));
            texts.UnionWith(md.Tasks.Select(t => t.name));
            texts.UnionWith(md.Riders.Select(r => r.role));
            texts.UnionWith(md.Banners.Select(b => b.name));
            texts.UnionWith(md.Boosters.Select(b => b.name));
            texts.UnionWith(md.Shop.Select(s => s.label));

            Assert.Greater(texts.Count, 100, "the scan found the game's texts");
            foreach (var language in new[] { "vi", "ja" })
            {
                var missing = texts.Where(t => !md.Translations[language].ContainsKey(t)).OrderBy(t => t).ToList();
                CollectionAssert.IsEmpty(missing, $"strings.csv has no {language} for these");
            }
        }

        private static MasterData Load()
        {
            string folder = Path.Combine(ProjectRoot(), "Assets", "_Game", "Resources", "MasterData");
            return MasterData.Parse(table =>
            {
                string path = Path.Combine(folder, table + ".csv");
                return File.Exists(path) ? File.ReadAllText(path) : null;
            });
        }

        [Test]
        public void Master_data_is_valid()
        {
            var md = Load();
            CollectionAssert.IsEmpty(md.Validate());
            Assert.AreEqual(12, md.Riders.Count, "12 riders at launch");
            Assert.AreEqual(3, md.Boosters.Count, "3 pre-level boosters");
            CollectionAssert.AreEqual(new[] { 20, 25, 30, 35, 40, 45, 50 }, md.DailyGems, "the plan's 20–50 gems a day");
            Assert.IsNotNull(md.Banner(md.Text("banner")), "config names the live banner");
        }

        [Test]
        public void One_star_per_level_pays_for_the_whole_renovation()
        {
            var md = Load();
            Assert.AreEqual(5, md.Areas.Count);
            Assert.AreEqual(30, md.Tasks.Sum(t => t.stars), "30 levels give 30 stars; the 30 tasks cost exactly that");
        }

        [Test]
        public void The_banner_shows_the_plan_rates_and_pity()
        {
            var md = Load();
            var banner = md.Banner(md.Text("banner"));
            var rates = md.RateTables[banner.rateTable];
            Assert.AreEqual((80, 17, 3), (rates[Rarity.R], rates[Rarity.SR], rates[Rarity.SSR]));
            Assert.AreEqual(60, banner.pity);
            Assert.AreEqual(50, banner.featuredShare);
        }
    }
}
