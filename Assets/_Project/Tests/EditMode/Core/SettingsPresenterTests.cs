using System;
using NUnit.Framework;
using Template.Core.Save;
using Template.Core.Settings;

namespace Template.Core.Tests
{
    public class SettingsPresenterTests
    {
        private sealed class FakeView : ISettingsView
        {
            public event Action<float> MusicChanged;
            public event Action<float> SfxChanged;
            public event Action<bool> HapticsChanged;
            public event Action<bool> ReduceMotionChanged;
            public event Action<string> LanguageChanged;

            public SettingsData Shown;
            public int ShowCalls;

            public void Show(SettingsData settings)
            {
                Shown = settings;
                ShowCalls++;
            }

            public void Music(float v) => MusicChanged?.Invoke(v);
            public void Sfx(float v) => SfxChanged?.Invoke(v);
            public void Haptics(bool v) => HapticsChanged?.Invoke(v);
            public void Reduce(bool v) => ReduceMotionChanged?.Invoke(v);
            public void Language(string code) => LanguageChanged?.Invoke(code);

            public bool HasListeners =>
                MusicChanged != null || SfxChanged != null || HapticsChanged != null || ReduceMotionChanged != null || LanguageChanged != null;
        }

        [Test]
        public void Attach_shows_current_settings()
        {
            var settings = new SettingsData { musicVolume = 0.3f };
            var view = new FakeView();
            new SettingsPresenter(settings).Attach(view);

            Assert.AreEqual(1, view.ShowCalls);
            Assert.AreSame(settings, view.Shown);
        }

        [Test]
        public void View_input_updates_settings_and_reports_clamped_values()
        {
            var settings = new SettingsData();
            var view = new FakeView();
            var presenter = new SettingsPresenter(settings);
            int changes = 0;
            presenter.SettingsChanged += _ => changes++;
            presenter.Attach(view);

            view.Music(1.7f);
            view.Sfx(0.4f);
            view.Haptics(false);
            view.Reduce(true);

            Assert.AreEqual(1f, settings.musicVolume);
            Assert.AreEqual(0.4f, settings.sfxVolume);
            Assert.IsFalse(settings.haptics);
            Assert.IsTrue(settings.reduceMotion);
            Assert.AreEqual(4, changes);
        }

        [Test]
        public void Picking_a_language_stores_it_reports_it_and_shows_it()
        {
            var settings = new SettingsData();
            var view = new FakeView();
            var presenter = new SettingsPresenter(settings);
            string reported = null;
            presenter.SettingsChanged += s => reported = s.language;
            presenter.Attach(view);

            view.Language("ja");

            Assert.AreEqual(("ja", "ja", 2), (settings.language, reported, view.ShowCalls));
        }

        [Test]
        public void Dispose_unsubscribes_and_allows_reattach()
        {
            var view = new FakeView();
            var presenter = new SettingsPresenter(new SettingsData());
            presenter.Attach(view);
            presenter.Dispose();

            Assert.IsFalse(view.HasListeners);
            Assert.IsFalse(presenter.IsAttached);
            Assert.DoesNotThrow(() => presenter.Attach(view));
            Assert.Throws<InvalidOperationException>(() => presenter.Attach(new FakeView()));
        }
    }
}
