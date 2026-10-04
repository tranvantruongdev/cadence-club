using System;
using Template.Core.Save;
using Template.Core.Ui;

namespace Template.Core.Settings
{
    public interface ISettingsView
    {
        event Action<float> MusicChanged;
        event Action<float> SfxChanged;
        event Action<bool> HapticsChanged;
        event Action<bool> ReduceMotionChanged;

        /// <summary>A language code the game offers (see <see cref="SettingsData.language"/>).</summary>
        event Action<string> LanguageChanged;

        void Show(SettingsData settings);
    }

    /// <summary>Applies settings-screen input to <see cref="SettingsData"/> and reports every change.</summary>
    public sealed class SettingsPresenter : Presenter<ISettingsView>
    {
        private readonly SettingsData _settings;

        public SettingsPresenter(SettingsData settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>Raised after each change, with the clamped settings.</summary>
        public event Action<SettingsData> SettingsChanged;

        protected override void OnAttach()
        {
            View.MusicChanged += OnMusicChanged;
            View.SfxChanged += OnSfxChanged;
            View.HapticsChanged += OnHapticsChanged;
            View.ReduceMotionChanged += OnReduceMotionChanged;
            View.LanguageChanged += OnLanguageChanged;
            View.Show(_settings);
        }

        protected override void OnDetach()
        {
            View.MusicChanged -= OnMusicChanged;
            View.SfxChanged -= OnSfxChanged;
            View.HapticsChanged -= OnHapticsChanged;
            View.ReduceMotionChanged -= OnReduceMotionChanged;
            View.LanguageChanged -= OnLanguageChanged;
        }

        private void OnMusicChanged(float value)
        {
            _settings.musicVolume = value;
            Commit();
        }

        private void OnSfxChanged(float value)
        {
            _settings.sfxVolume = value;
            Commit();
        }

        private void OnHapticsChanged(bool value)
        {
            _settings.haptics = value;
            Commit();
        }

        private void OnReduceMotionChanged(bool value)
        {
            _settings.reduceMotion = value;
            Commit();
        }

        private void OnLanguageChanged(string code)
        {
            _settings.language = code;
            Commit();
            View.Show(_settings); // the chosen language is marked
        }

        private void Commit()
        {
            _settings.Clamp();
            SettingsChanged?.Invoke(_settings);
        }
    }
}
