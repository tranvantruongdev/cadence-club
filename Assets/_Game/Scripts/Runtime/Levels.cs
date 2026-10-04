using System;
using CadenceClub.Core;
using UnityEngine;

namespace CadenceClub
{
    /// <summary>
    /// The levels: one JSON file each in Resources/Levels (written by Cadence Club → Level Editor), tuned with the
    /// bot simulator to the win-rate bands in <see cref="LevelBands"/>.
    /// </summary>
    public static class Levels
    {
        public const int Count = 30;

        /// <summary>When set, the Game scene plays this level instead of the saved progress (tests, a future level select).</summary>
        public static int? Override { get; set; }

        /// <summary>Where level files live, for the editor: Assets/_Game/Resources/Levels/level_NN.json.</summary>
        public static string AssetPath(int number) => $"Assets/_Game/Resources/{ResourcePath(number)}.json";

        public static LevelDef Load(int number)
        {
            var file = Resources.Load<TextAsset>(ResourcePath(number));
            if (file == null)
            {
                throw new ArgumentOutOfRangeException(nameof(number), $"No level file at Resources/{ResourcePath(number)}.json");
            }

            var def = JsonUtility.FromJson<LevelDef>(file.text);
            def.id = number;
            return def;
        }

        /// <summary>The level to play next from saved progress; replays the last level once all are won.</summary>
        public static int Next(int savedLevel) => Mathf.Clamp(savedLevel, 1, Count);

        private static string ResourcePath(int number) => $"Levels/level_{number:00}";
    }
}
