using System.IO;
using System.Linq;
using NUnit.Framework;

namespace CadenceClub.Core.Tests
{
    /// <summary>The shipped master data (Assets/_Game/Resources/MasterData/*.csv) parses and validates.</summary>
    public class MasterDataFileTests
    {
        private static MasterData Load()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Game", "Resources", "MasterData")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Assets/_Game/Resources/MasterData not found above the test folder");
            string folder = Path.Combine(dir.FullName, "Assets", "_Game", "Resources", "MasterData");
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
