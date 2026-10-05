using System;
using System.Linq;
using CadenceClub.Audio;
using NUnit.Framework;
using UnityEngine;

namespace CadenceClub.PlayModeTests
{
    /// <summary>
    /// The synthesised sound, measured (not listened to): music loops are exactly eight bars, never clip and have no
    /// seam where they repeat; effects stay below full scale.
    /// </summary>
    public class AudioTests
    {
        private const int MusicRate = 22050;

        [Test]
        public void The_home_loop_is_eight_bars_and_repeats_without_a_seam() => CheckLoop(ClubAudio.HomeLoopSamples(), 90f);

        [Test]
        public void The_level_loop_is_eight_bars_and_repeats_without_a_seam() => CheckLoop(ClubAudio.LevelLoopSamples(), 118f);

        [Test]
        public void Effects_stay_below_full_scale()
        {
            foreach (var clip in new[]
                     {
                         ClubAudio.Special, ClubAudio.Blast, ClubAudio.Power, ClubAudio.Win, ClubAudio.Lose, ClubAudio.Chime, ClubAudio.Bell,
                         ClubAudio.Flip, ClubAudio.Fanfare, ClubAudio.Coin,
                     })
            {
                var samples = new float[clip.samples];
                clip.GetData(samples, 0);
                Assert.IsFalse(samples.Any(float.IsNaN), clip.name);
                Assert.Less(samples.Max(Math.Abs), 0.95f, $"{clip.name} clips");
                Assert.Greater(samples.Max(Math.Abs), 0.05f, $"{clip.name} is silent");
            }
        }

        private static void CheckLoop(float[] samples, float bpm)
        {
            Assert.AreEqual(Mathf.RoundToInt(8 * 4 * 60f / bpm * MusicRate), samples.Length, "eight bars of 4/4");
            Assert.IsFalse(samples.Any(float.IsNaN));
            Assert.AreEqual(0.7f, samples.Max(Math.Abs), 0.001f, "normalised to 0.7, below clipping");
            float largestStep = 0f;
            for (int i = 1; i < samples.Length; i++)
            {
                largestStep = Mathf.Max(largestStep, Mathf.Abs(samples[i] - samples[i - 1]));
            }

            float seam = Mathf.Abs(samples[0] - samples[samples.Length - 1]);
            Assert.LessOrEqual(seam, largestStep, "the jump from the end back to the start is no bigger than any step inside the loop");
        }
    }
}
