using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Template.Core.Save
{
    public sealed class SaveVersionException : Exception
    {
        public SaveVersionException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Upgrades old save JSON one version at a time (v1 → v2 → v3 …) before it is deserialized,
    /// so players never lose progress when the data model changes.
    /// </summary>
    public sealed class SaveMigrator
    {
        private readonly Dictionary<int, Action<JObject>> _steps = new Dictionary<int, Action<JObject>>();

        public SaveMigrator(int currentVersion)
        {
            if (currentVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(currentVersion), "Save versions start at 1.");
            }

            CurrentVersion = currentVersion;
        }

        public int CurrentVersion { get; }

        /// <summary>Registers the step that upgrades data from <paramref name="fromVersion"/> to fromVersion + 1.</summary>
        public SaveMigrator Add(int fromVersion, Action<JObject> upgrade)
        {
            if (fromVersion < 1 || fromVersion >= CurrentVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(fromVersion), $"A step must start between v1 and v{CurrentVersion - 1}.");
            }

            if (_steps.ContainsKey(fromVersion))
            {
                throw new ArgumentException($"A step from v{fromVersion} is already registered.", nameof(fromVersion));
            }

            _steps[fromVersion] = upgrade ?? throw new ArgumentNullException(nameof(upgrade));
            return this;
        }

        /// <summary>Saves written before versioning existed count as v1.</summary>
        public static int ReadVersion(JObject data)
        {
            var token = data["saveVersion"];
            return token == null || token.Type == JTokenType.Null ? 1 : token.Value<int>();
        }

        /// <summary>Upgrades <paramref name="data"/> in place and returns the version it started at.</summary>
        public int Migrate(JObject data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            int original = ReadVersion(data);
            if (original > CurrentVersion)
            {
                throw new SaveVersionException($"This save is from a newer build (v{original}) than the current one (v{CurrentVersion}).");
            }

            for (int version = original; version < CurrentVersion; version++)
            {
                if (!_steps.TryGetValue(version, out var step))
                {
                    throw new SaveVersionException($"No migration step from v{version} to v{version + 1}.");
                }

                step(data);
                data["saveVersion"] = version + 1;
            }

            return original;
        }
    }

    /// <summary>The save format history of this project. Each game edits this file as its data evolves.</summary>
    public static class SaveSchema
    {
        public const int CurrentVersion = 2;

        public static SaveMigrator CreateMigrator()
        {
            return new SaveMigrator(CurrentVersion)
                // v1 stored the best score as "best" and had no run counter.
                .Add(1, data =>
                {
                    data["bestScore"] = data["best"] ?? 0;
                    data.Remove("best");
                    if (data["totalRuns"] == null)
                    {
                        data["totalRuns"] = 0;
                    }
                });
        }
    }
}
