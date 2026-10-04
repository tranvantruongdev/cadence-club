using System.IO;
using Template.Core.Save;
using UnityEngine;

namespace Template.Infra.Save
{
    /// <summary>
    /// Saves to persistentDataPath safely: write a temp file completely, keep the previous save
    /// as a backup, then swap. A crash mid-write can never leave the player with no save.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private readonly string _path;
        private readonly string _tempPath;
        private readonly string _backupPath;

        public FileSaveStore(string fileName = "save.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
            _tempPath = _path + ".tmp";
            _backupPath = _path + ".bak";
        }

        public string FilePath => _path;

        public bool TryLoad(out string json)
        {
            // If a crash happened between deleting the old save and moving the new one in,
            // the finished temp file is the newest complete save.
            return TryRead(_path, out json) || TryRead(_tempPath, out json);
        }

        public bool TryLoadBackup(out string json) => TryRead(_backupPath, out json);

        public void Save(string json)
        {
            File.WriteAllText(_tempPath, json);
            if (File.Exists(_path))
            {
                File.Copy(_path, _backupPath, true);
                File.Delete(_path);
            }

            File.Move(_tempPath, _path);
        }

        public void Delete()
        {
            foreach (var path in new[] { _path, _tempPath, _backupPath })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
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
