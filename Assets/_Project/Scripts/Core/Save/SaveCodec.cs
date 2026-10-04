using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Template.Core.Save
{
    /// <summary>Turns <see cref="SaveData"/> into JSON and back, migrating old versions on the way in.</summary>
    public sealed class SaveCodec
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        private readonly SaveMigrator _migrator;

        public SaveCodec(SaveMigrator migrator)
        {
            _migrator = migrator;
        }

        public string Serialize(SaveData data)
        {
            data.saveVersion = _migrator.CurrentVersion;
            return JsonConvert.SerializeObject(data, JsonSettings);
        }

        /// <summary>Throws on corrupt JSON or a save from a newer build; callers fall back to the backup.</summary>
        public SaveData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return New();
            }

            var obj = JObject.Parse(json);
            _migrator.Migrate(obj);
            var data = obj.ToObject<SaveData>(JsonSerializer.Create(JsonSettings)) ?? New();
            if (data.settings == null)
            {
                data.settings = new SettingsData();
            }

            data.settings.Clamp();
            return data;
        }

        public static SaveData New() => new SaveData();
    }
}
