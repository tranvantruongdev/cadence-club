using System.Linq;
using CadenceClub.UI;
using NUnit.Framework;
using UnityEngine;

namespace CadenceClub.LevelTests
{
    /// <summary>
    /// Every shipped board, on every screen shape the game meets (3:4 tablets and the desktop column down to tall
    /// phones), fits between the goals bar and the squad portraits and within the width.
    /// </summary>
    public class BoardFitTests
    {
        private static readonly int[] All = Enumerable.Range(1, Levels.Count).ToArray();
        private static readonly float[] Aspects = { 3f / 4f, 2f / 3f, 9f / 16f, 9f / 19.5f };

        [TestCaseSource(nameof(All))]
        public void Board_clears_the_hud_on_every_shape(int number)
        {
            var def = Levels.Load(number);
            foreach (float aspect in Aspects)
            {
                float size = LevelController.FitSize(new Vector2(def.Width, def.Height), aspect);
                float canvasHeight = Mathf.Sqrt(1080f * 1920f / aspect);
                float unitsPerWorld = canvasHeight / (2f * size);
                float below = (size - LevelController.CameraY - def.Height * 0.5f) * unitsPerWorld;
                float above = (size + LevelController.CameraY - def.Height * 0.5f) * unitsPerWorld;
                Assert.That(below, Is.GreaterThanOrEqualTo(LevelHud.SquadTop - 0.5f), $"aspect {aspect:0.000}: bottom row under the squad");
                Assert.That(above, Is.GreaterThanOrEqualTo(LevelHud.GoalsBarBottom), $"aspect {aspect:0.000}: top row under the goals bar");
                Assert.That(size * aspect, Is.GreaterThanOrEqualTo(def.Width * 0.5f + 0.5f - 1e-4f), $"aspect {aspect:0.000}: board wider than the view");
            }
        }
    }
}
