using System.IO;
using Template.Core.Save;
using UnityEngine;

namespace Template.Infra.Save
{
    /// <summary>Stores primary, completed temporary and known-good backup save candidates.</summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private readonly string _path;
        private readonly string _tempPath;
        private readonly string _backupPath;
        private readonly string _backupTempPath;

        public FileSaveStore(string fileName = "save.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
            _tempPath = _path + ".tmp";
            _backupPath = _path + ".bak";
            _backupTempPath = _backupPath + ".tmp";
        }

        public string FilePath => _path;

        public bool TryLoad(out string json) => TryRead(_path, out json);

        public bool TryLoadTemp(out string json) => TryRead(_tempPath, out json);

        public bool TryLoadBackup(out string json) => TryRead(_backupPath, out json);

        public void Save(string json, string lastKnownGoodJson)
        {
            File.WriteAllText(_tempPath, json);
            if (lastKnownGoodJson != null)
            {
                WriteBackup(lastKnownGoodJson);
            }

            if (File.Exists(_path))
            {
                File.Replace(_tempPath, _path, null);
            }
            else
            {
                File.Move(_tempPath, _path);
            }
        }

        public void Delete()
        {
            foreach (var path in new[] { _path, _tempPath, _backupPath, _backupTempPath })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private void WriteBackup(string json)
        {
            File.WriteAllText(_backupTempPath, json);
            if (File.Exists(_backupPath))
            {
                File.Replace(_backupTempPath, _backupPath, null);
            }
            else
            {
                File.Move(_backupTempPath, _backupPath);
            }
        }

        private static bool TryRead(string path, out string json)
        {
            if (File.Exists(path))
            {
                json = File.ReadAllText(path);
                return true;
            }

            json = null;
            return false;
        }
    }
}
