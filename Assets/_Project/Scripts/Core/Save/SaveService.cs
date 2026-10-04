using System;

namespace Template.Core.Save
{
    /// <summary>Where save JSON lives. The Unity build uses a file; tests use memory.</summary>
    public interface ISaveStore
    {
        bool TryLoad(out string json);
        bool TryLoadBackup(out string json);
        void Save(string json);
        void Delete();
    }

    /// <summary>
    /// Owns the in-memory <see cref="SaveData"/>. Loads with backup fallback, so a corrupt
    /// or half-written file never wipes a player's progress.
    /// </summary>
    public sealed class SaveService
    {
        private delegate bool LoadFn(out string json);

        private readonly ISaveStore _store;
        private readonly SaveCodec _codec;
        private readonly Action<string> _warn;

        public SaveService(ISaveStore store, SaveCodec codec, Action<string> warn = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _warn = warn ?? (_ => { });
        }

        public SaveData Data { get; private set; } = SaveCodec.New();
        public bool IsDirty { get; private set; }

        /// <summary>Where the last load came from: Primary, Backup or Fresh.</summary>
        public string LoadedFrom { get; private set; } = "Fresh";

        public event Action Saved;

        public void Load()
        {
            if (TryDecode(_store.TryLoad, "save", out var data))
            {
                LoadedFrom = "Primary";
            }
            else if (TryDecode(_store.TryLoadBackup, "backup", out data))
            {
                LoadedFrom = "Backup";
                _warn("[Save] Main save unreadable; restored from backup.");
            }
            else
            {
                data = SaveCodec.New();
                LoadedFrom = "Fresh";
            }

            Data = data;
            IsDirty = false;
        }

        public void MarkDirty() => IsDirty = true;

        public void Save()
        {
            try
            {
                _store.Save(_codec.Serialize(Data));
                IsDirty = false;
                Saved?.Invoke();
            }
            catch (Exception e)
            {
                _warn($"[Save] Write failed: {e.Message}");
            }
        }

        public void SaveIfDirty()
        {
            if (IsDirty)
            {
                Save();
            }
        }

        public void ResetAll()
        {
            _store.Delete();
            Data = SaveCodec.New();
            Save();
        }

        private bool TryDecode(LoadFn load, string label, out SaveData data)
        {
            data = null;
            if (!load(out string json))
            {
                return false;
            }

            try
            {
                data = _codec.Deserialize(json);
                return true;
            }
            catch (Exception e)
            {
                _warn($"[Save] Could not read {label}: {e.Message}");
                return false;
            }
        }
    }
}
