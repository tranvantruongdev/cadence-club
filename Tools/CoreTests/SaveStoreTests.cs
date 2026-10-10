using System;
using System.IO;
using NUnit.Framework;
using Template.Core.Save;
using Template.Infra.Save;

namespace Template.Core.Tests
{
    [NonParallelizable]
    public class SaveStoreTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            _root = Path.GetFullPath(Path.Combine(temp, "template-save-tests-" + Guid.NewGuid().ToString("N")));
            Assert.IsTrue(_root.StartsWith(temp, StringComparison.OrdinalIgnoreCase));
            Directory.CreateDirectory(_root);
            UnityEngine.Application.persistentDataPath = _root;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(_root);
                if (full.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(full).StartsWith("template-save-tests-", StringComparison.Ordinal))
                {
                    if (Directory.Exists(full)) Directory.Delete(full, true);
                }
            }

            UnityEngine.Application.persistentDataPath = null;
        }

        [Test]
        public void Load_uses_valid_temp_when_primary_is_corrupt()
        {
            var store = new FileSaveStore("temp-recovery.json");
            string path = store.FilePath;
            File.WriteAllText(path, "{broken");
            File.WriteAllText(path + ".tmp", JsonWithScore(888));
            File.WriteAllText(path + ".bak", JsonWithScore(777));
            var service = new SaveService(store, Codec());

            service.Load();

            Assert.AreEqual("Temp", service.LoadedFrom);
            Assert.AreEqual(888, service.Data.bestScore);
            Assert.IsTrue(service.IsDirty);
        }

        [Test]
        public void Failed_promotion_keeps_valid_backup_and_temp_recoverable()
        {
            var store = new FileSaveStore("locked-recovery.json");
            string path = store.FilePath;
            File.WriteAllText(path, "{broken");
            File.WriteAllText(path + ".bak", JsonWithScore(777));
            var service = new SaveService(store, Codec());
            service.Load();
            Assert.AreEqual("Backup", service.LoadedFrom);
            service.Data.bestScore = 888;
            service.MarkDirty();

            // An occupied destination fails promotion on Windows and Unix; file-sharing locks do not.
            File.Delete(path);
            Directory.CreateDirectory(path);
            try
            {
                service.Save();
                Assert.IsTrue(service.IsDirty);
            }
            finally
            {
                Directory.Delete(path);
            }

            Assert.AreEqual(777, Codec().Deserialize(File.ReadAllText(path + ".bak")).bestScore);
            Assert.AreEqual(888, Codec().Deserialize(File.ReadAllText(path + ".tmp")).bestScore);

            var recovered = new SaveService(new FileSaveStore("locked-recovery.json"), Codec());
            recovered.Load();
            Assert.AreEqual("Temp", recovered.LoadedFrom);
            Assert.AreEqual(888, recovered.Data.bestScore);
        }

        [Test]
        public void Saving_recovered_data_does_not_back_up_corrupt_primary()
        {
            var store = new FileSaveStore("corrupt-primary.json");
            string path = store.FilePath;
            File.WriteAllText(path, "{broken");
            File.WriteAllText(path + ".bak", JsonWithScore(777));
            var service = new SaveService(store, Codec());
            service.Load();
            Assert.AreEqual("Backup", service.LoadedFrom);
            service.Data.bestScore = 888;

            service.Save();

            Assert.IsFalse(service.IsDirty);
            Assert.AreEqual(888, Codec().Deserialize(File.ReadAllText(path)).bestScore);
            Assert.AreEqual(777, Codec().Deserialize(File.ReadAllText(path + ".bak")).bestScore);
        }

        [Test]
        public void Successful_save_promotes_primary_and_preserves_previous_valid_json()
        {
            var store = new FileSaveStore("round-trip.json");
            string path = store.FilePath;
            File.WriteAllText(path, JsonWithScore(10));
            var service = new SaveService(store, Codec());
            service.Load();
            service.Data.bestScore = 11;
            service.MarkDirty();

            service.Save();

            Assert.IsFalse(service.IsDirty);
            Assert.AreEqual(11, Codec().Deserialize(File.ReadAllText(path)).bestScore);
            Assert.AreEqual(10, Codec().Deserialize(File.ReadAllText(path + ".bak")).bestScore);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        private static SaveCodec Codec() => new SaveCodec(SaveSchema.CreateMigrator());

        private static string JsonWithScore(int score)
        {
            var data = SaveCodec.New();
            data.bestScore = score;
            return Codec().Serialize(data);
        }
    }
}
