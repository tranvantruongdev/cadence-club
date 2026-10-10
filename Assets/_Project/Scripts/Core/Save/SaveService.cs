using System;

namespace Template.Core.Save
{
    /// <summary>Where save JSON lives. The Unity build uses files; tests can use memory or files.</summary>
    public interface ISaveStore
    {
        bool TryLoad(out string json);
        bool TryLoadTemp(out string json);
        bool TryLoadBackup(out string json);
        void Save(string json, string lastKnownGoodJson);
        void Delete();
    }

    /// <summary>Owns the in-memory save and recovers from the first readable, valid candidate.</summary>
    public sealed class SaveService
    {
        private delegate bool LoadFn(out string json);

        private readonly ISaveStore _store;
        private readonly SaveCodec _codec;
        private readonly Action<string> _warn;
        private string _lastKnownGoodJson;

        public SaveService(ISaveStore store, SaveCodec codec, Action<string> warn = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _warn = warn ?? (_ => { });
        }

        public SaveData Data { get; private set; } = SaveCodec.New();
        public bool IsDirty { get; private set; }

        /// <summary>Where the last load came from: Primary, Temp, Backup or Fresh.</summary>
        public string LoadedFrom { get; private set; } = "Fresh";

        public event Action Saved;

        public void Load()
        {
            if (TryDecode(_store.TryLoad, "save", out var data, out var json))
            {
                AcceptLoaded(data, json, "Primary", false);
            }
            else if (TryDecode(_store.TryLoadTemp, "temporary save", out data, out json))
            {
                AcceptLoaded(data, json, "Temp", true);
            }
            else if (TryDecode(_store.TryLoadBackup, "backup", out data, out json))
            {
                AcceptLoaded(data, json, "Backup", true);
            }
            else
            {
                Data = SaveCodec.New();
                _lastKnownGoodJson = null;
                LoadedFrom = "Fresh";
                IsDirty = false;
            }

            if (LoadedFrom == "Temp" || LoadedFrom == "Backup")
            {
                _warn($"[Save] Main save unreadable; restored from {LoadedFrom.ToLowerInvariant()}.");
            }
        }

        public void MarkDirty() => IsDirty = true;

        public void Save()
        {
            IsDirty = true;
            string json;
            try
            {
                json = _codec.Serialize(Data);
                _store.Save(json, _lastKnownGoodJson);
            }
            catch (Exception e)
            {
                _warn($"[Save] Write failed: {e.Message}");
                return;
            }

            _lastKnownGoodJson = json;
            IsDirty = false;
            Saved?.Invoke();
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
            _lastKnownGoodJson = null;
            Save();
        }

        private void AcceptLoaded(SaveData data, string json, string source, bool needsRepair)
        {
            Data = data;
            _lastKnownGoodJson = json;
            LoadedFrom = source;
            IsDirty = needsRepair;
        }

        private bool TryDecode(LoadFn load, string label, out SaveData data, out string json)
        {
            data = null;
            json = null;
            try
            {
                if (!load(out json))
                {
                    return false;
                }

                data = _codec.Deserialize(json);
                return true;
            }
            catch (Exception e)
            {
                _warn($"[Save] Could not read {label}: {e.Message}");
                data = null;
                json = null;
                return false;
            }
        }
    }
}
