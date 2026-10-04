using System.Linq;
using NUnit.Framework;

namespace CadenceClub.LevelTests
{
    /// <summary>
    /// The shipped level files load through the game's own loader (Resources + JsonUtility) and are valid.
    /// The win-rate band check runs in dotnet (Tools/GameTests/LevelFileBandTests.cs): seconds there, minutes here.
    /// </summary>
    public class LevelFileTests
    {
        private static readonly int[] All = Enumerable.Range(1, Levels.Count).ToArray();

        [TestCaseSource(nameof(All))]
        public void Level_file_loads_and_is_valid(int number)
        {
            var def = Levels.Load(number);
            Assert.AreEqual(number, def.id);
            CollectionAssert.IsEmpty(def.Validate());
        }
    }
}
