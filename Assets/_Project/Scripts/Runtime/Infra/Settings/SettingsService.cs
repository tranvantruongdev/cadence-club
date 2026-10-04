using System;
using Template.Core.Save;
using Template.Feel;
using Template.Infra.Audio;
using Template.Infra.Device;

namespace Template.Infra.Settings
{
    /// <summary>Applies saved settings to the running game and persists changes.</summary>
    public sealed class SettingsService
    {
        private readonly SaveService _save;
        private readonly AudioService _audio;

        public SettingsService(SaveService save, AudioService audio)
        {
            _save = save;
            _audio = audio;
        }

        public SettingsData Current => _save.Data.settings;

        public event Action<SettingsData> Applied;

        public void Apply()
        {
            var settings = Current;
            settings.Clamp();
            _audio.SetVolumes(settings.musicVolume, settings.sfxVolume);
            Haptics.Enabled = settings.haptics;
            JuiceFx.ReduceMotion = settings.reduceMotion;
            Applied?.Invoke(settings);
        }

        public void Commit()
        {
            Apply();
            _save.MarkDirty();
            _save.Save();
        }
    }
}
