using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Template.Core.Save;

namespace Template.Core.Tests
{
    public class SaveTests
    {
        private sealed class MemoryStore : ISaveStore
        {
            public string Main;
            public string Backup;
            public int Writes;

            public bool TryLoad(out string json)
            {
                json = Main;
                return Main != null;
            }

            public bool TryLoadBackup(out string json)
            {
                json = Backup;
                return Backup != null;
            }

            public void Save(string json)
            {
                Backup = Main;
                Main = json;
                Writes++;
            }

            public void Delete()
            {
                Main = null;
                Backup = null;
            }
        }

        private static SaveCodec Codec() => new SaveCodec(SaveSchema.CreateMigrator());

        [Test]
        public void Migrator_upgrades_v1_save()
        {
            var data = JObject.Parse("{\"best\": 42, \"settings\": {\"musicVolume\": 0.5}}");
            int from = SaveSchema.CreateMigrator().Migrate(data);

            Assert.AreEqual(1, from);
            Assert.AreEqual(SaveSchema.CurrentVersion, (int)data["saveVersion"]);
            Assert.AreEqual(42, (int)data["bestScore"]);
            Assert.IsNull(data["best"]);
            Assert.AreEqual(0, (int)data["totalRuns"]);
        }

        [Test]
        public void Migrator_leaves_current_save_alone()
        {
            var data = JObject.Parse($"{{\"saveVersion\": {SaveSchema.CurrentVersion}, \"bestScore\": 7}}");
            SaveSchema.CreateMigrator().Migrate(data);
            Assert.AreEqual(7, (int)data["bestScore"]);
        }

        [Test]
        public void Migrator_rejects_newer_save_and_gaps()
        {
            var newer = JObject.Parse("{\"saveVersion\": 99}");
            Assert.Throws<SaveVersionException>(() => SaveSchema.CreateMigrator().Migrate(newer));

            var gappy = new SaveMigrator(3).Add(1, d => { });
            Assert.Throws<SaveVersionException>(() => gappy.Migrate(JObject.Parse("{\"saveVersion\": 1}")));
        }

        [Test]
        public void Migrator_runs_steps_in_order()
        {
            var order = new List<int>();
            var migrator = new SaveMigrator(4)
                .Add(3, d => order.Add(3))
                .Add(1, d => order.Add(1))
                .Add(2, d => order.Add(2));

            migrator.Migrate(JObject.Parse("{\"saveVersion\": 1}"));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void Codec_round_trips_and_clamps_settings()
        {
            var codec = Codec();
            var data = new SaveData { bestScore = 9, totalRuns = 3 };
            data.settings.musicVolume = 0.25f;
            data.settings.language = "vi";

            var back = codec.Deserialize(codec.Serialize(data));
            Assert.AreEqual(9, back.bestScore);
            Assert.AreEqual(3, back.totalRuns);
            Assert.AreEqual(0.25f, back.settings.musicVolume);
            Assert.AreEqual("vi", back.settings.language);

            var broken = codec.Deserialize("{\"saveVersion\":2,\"settings\":{\"musicVolume\":5,\"sfxVolume\":-1,\"language\":\"\"}}");
            Assert.AreEqual(1f, broken.settings.musicVolume);
            Assert.AreEqual(0f, broken.settings.sfxVolume);
            Assert.AreEqual("en", broken.settings.language);
        }

        [Test]
        public void Service_falls_back_to_backup_when_main_is_corrupt()
        {
            var store = new MemoryStore();
            var warnings = new List<string>();
            var service = new SaveService(store, Codec(), warnings.Add);
            service.Data.bestScore = 5;
            service.Save();
            service.Data.bestScore = 6;
            service.Save();

            store.Main = "{ not json";
            service.Load();

            Assert.AreEqual("Backup", service.LoadedFrom);
            Assert.AreEqual(5, service.Data.bestScore);
            Assert.IsNotEmpty(warnings);
        }

        [Test]
        public void Service_starts_fresh_when_nothing_is_readable()
        {
            var store = new MemoryStore { Main = "garbage", Backup = "{\"saveVersion\": 99}" };
            var service = new SaveService(store, Codec());
            service.Load();

            Assert.AreEqual("Fresh", service.LoadedFrom);
            Assert.AreEqual(0, service.Data.bestScore);
        }

        [Test]
        public void SaveIfDirty_only_writes_when_needed()
        {
            var store = new MemoryStore();
            var service = new SaveService(store, Codec());
            service.SaveIfDirty();
            Assert.AreEqual(0, store.Writes);

            service.MarkDirty();
            service.SaveIfDirty();
            service.SaveIfDirty();
            Assert.AreEqual(1, store.Writes);
            Assert.IsFalse(service.IsDirty);
        }
    }
}
