using System;
using Newtonsoft.Json.Linq;

namespace Template.Core.Save
{
    /// <summary>
    /// Everything the game persists. Add fields freely; when you rename or remove one,
    /// bump <see cref="SaveSchema.CurrentVersion"/> and add a migration step.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int saveVersion = SaveSchema.CurrentVersion;
        public SettingsData settings = new SettingsData();
        public int bestScore;
        public int totalRuns;

        /// <summary>Game-specific state: each game keeps one object of its own here, through <see cref="GetGame{T}"/>.</summary>
        public JObject game;

        /// <summary>The game's own state object (a fresh one before the first <see cref="SetGame{T}"/>).</summary>
        public T GetGame<T>() where T : new() => game == null ? new T() : game.ToObject<T>() ?? new T();

        public void SetGame<T>(T value) => game = JObject.FromObject(value);
    }

    [Serializable]
    public sealed class SettingsData
    {
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public bool haptics = true;
        public bool reduceMotion;
        public string language = "en";

        /// <summary>Repairs out-of-range values, e.g. from an edited or corrupted save.</summary>
        public void Clamp()
        {
            musicVolume = Clamp01(musicVolume);
            sfxVolume = Clamp01(sfxVolume);
            if (string.IsNullOrWhiteSpace(language))
            {
                language = "en";
            }
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
